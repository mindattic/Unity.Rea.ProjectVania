using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Input;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    /// <summary>
    /// U.VAN-US-A10: Esc opens the original's three-level main menu (RECONSTRUCTION 9.2) with its titles, descriptions,
    /// arrow, wrap-around navigation and cues; the world freezes while it is open; Quit exits; and the game states
    /// change as RECONSTRUCTION 2's table says. Replays <c>CheckInput_MainMenu</c> and the menu methods of InputManager.
    /// </summary>
    public class MainMenuTests
    {
        static readonly GameSettings Settings = GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));
        static readonly PlayerId P1 = PlayerId.One;

        /// <summary>The shipped world (two characters, cameras, the menu) driven by a scripted keyboard and pad.</summary>
        sealed class Rig : IInputDevice
        {
            readonly InputFrame _held = new InputFrame();

            public Rig()
            {
                World = new GameWorld(Map.FromSettings(Settings.Grid), Settings.Player, Settings.Characters.Find(Settings.Characters.DefaultCharacter),
                    Settings.Names, new Random(3), Settings.Camera, Settings.Menu);
                Input = new PlayerInput(P1, Settings.Input, this);
            }

            public GameWorld World { get; }
            public PlayerInput Input { get; }

            /// <summary>Every event of every step since the last call.</summary>
            public List<IGameEvent> Events { get; } = new List<IGameEvent>();

            public void Step()
            {
                World.Advance(1.0 / 60, Input);
                Events.AddRange(World.Events);
            }

            /// <summary>Holds the key for one step, then releases it for one.</summary>
            public Rig Tap(Key key)
            {
                _held.Set(key);
                Step();
                _held.Set(key, false);
                Step();
                return this;
            }

            public Rig Tap(PadButton button)
            {
                _held.Set(button);
                Step();
                _held.Set(button, false);
                Step();
                return this;
            }

            public List<string> TakeCues()
            {
                var cues = Events.OfType<CueRequested>().Select(c => c.Cue).ToList();
                Events.Clear();
                return cues;
            }

            public void Sample(InputFrame frame) => frame.CopyFrom(_held);
        }

        static string[] Items(GameWorld world) => world.Menu.CurrentLevel.Items.Select(i => i.Text).ToArray();

        // ---- opening, closing, the frozen world (RECONSTRUCTION 2) ----

        [Test]
        public void EscOpensTheMenuWithMenuOpenAndClosesItWithMenuClose()
        {
            var rig = new Rig();
            rig.Tap(Key.Escape);
            Assert.That(rig.World.State, Is.EqualTo(GameState.MainMenu));
            Assert.That(rig.TakeCues(), Is.EqualTo(new[] { "menu_open" }));

            rig.Tap(Key.Escape);
            Assert.That(rig.World.State, Is.EqualTo(GameState.Play));
            Assert.That(rig.TakeCues(), Is.EqualTo(new[] { "menu_close" }));

            rig.Tap(PadButton.Start);
            Assert.That(rig.World.State, Is.EqualTo(GameState.MainMenu));
            rig.Tap(PadButton.Start);
            Assert.That(rig.World.State, Is.EqualTo(GameState.Play));
            Assert.That(rig.TakeCues(), Is.EqualTo(new[] { "menu_open", "menu_close" }));
        }

        [TestCase(GameState.MapEditor)]
        [TestCase(GameState.Console)]
        public void TheMenuOpensFromTheEditorAndTheConsoleAndAlwaysClosesToPlay(GameState from)
        {
            var rig = new Rig();
            rig.Tap(from == GameState.MapEditor ? Key.M : Key.OemTilde);
            Assert.That(rig.World.State, Is.EqualTo(from));
            rig.TakeCues();

            rig.Tap(Key.Escape);
            Assert.That(rig.World.State, Is.EqualTo(GameState.MainMenu));
            rig.Tap(Key.Escape);
            Assert.That(rig.World.State, Is.EqualTo(GameState.Play), "Action_ToggleMainMenu always returns to Play");
            Assert.That(rig.TakeCues(), Is.EqualTo(new[] { "menu_open", "menu_close" }));
        }

        [Test]
        public void TheWorldFreezesWhileTheMenuIsOpen()
        {
            var rig = new Rig();
            for (var i = 0; i < 10; i++) rig.Step(); // the characters are falling from their spawn
            rig.Tap(Key.Escape);
            var frozen = rig.World.Characters.Select(c => (c.Position.X, c.Position.Y, c.Velocity.X, c.Velocity.Y, c.Action)).ToList();

            for (var i = 0; i < 120; i++) rig.Step();
            rig.Tap(Key.D).Tap(Key.Space); // in the menu these select, they do not move or jump
            Assert.That(rig.World.Characters.Select(c => (c.Position.X, c.Position.Y, c.Velocity.X, c.Velocity.Y, c.Action)), Is.EqualTo(frozen));

            rig.Tap(Key.Escape);
            rig.Step();
            Assert.That(rig.World.Characters[0].Position.Y, Is.Not.EqualTo(frozen[0].Item2), "the world runs again in Play");
        }

        // ---- the root level ----

        [Test]
        public void TheRootLevelIsTheOriginals()
        {
            var menu = new Rig().World.Menu;
            Assert.That(menu.Level, Is.EqualTo(0));
            Assert.That(menu.Item, Is.EqualTo(0));
            Assert.That(menu.Title, Is.EqualTo("Main Menu"));
            Assert.That(menu.CurrentLevel.Items.Select(i => i.Text), Is.EqualTo(new[] { "Game", "Video", "Audio", "Controls", "Quit" }));
            Assert.That(menu.Description, Is.EqualTo("Game : Change various game settings"));
            Assert.That(menu.AtRoot, Is.True);

            // MenuChoice positions (50, 100 + 50 * index) and the arrow 18 px to the left of the highlighted one.
            for (var i = 0; i < 5; i++)
            {
                Assert.That(menu.ItemPosition(i).X, Is.EqualTo(50f));
                Assert.That(menu.ItemPosition(i).Y, Is.EqualTo(100f + 50 * i));
            }

            Assert.That((menu.ArrowPosition.X, menu.ArrowPosition.Y), Is.EqualTo((32f, 100f)));
        }

        [Test]
        public void UpAndDownWrapAroundAndPlayMenuMoveEachTime()
        {
            var rig = new Rig();
            rig.Tap(Key.Escape).TakeCues();
            var menu = rig.World.Menu;

            var descriptions = new List<string>();
            foreach (var key in new[] { Key.Down, Key.S, Key.Down, Key.Down, Key.Down })
            {
                rig.Tap(key);
                descriptions.Add(menu.Description);
            }

            Assert.That(menu.Item, Is.EqualTo(0), "five downs wrap back to the first item");
            Assert.That(descriptions, Is.EqualTo(new[]
            {
                "Video : Change video settings", "Audio : Change audio settings", "Controls : Change game controls",
                "Quit : Quit game", "Game : Change various game settings",
            }));

            rig.Tap(Key.Up);
            Assert.That(menu.Item, Is.EqualTo(4), "up from the first item wraps to the last");
            Assert.That((menu.ArrowPosition.X, menu.ArrowPosition.Y), Is.EqualTo((32f, 300f)));
            rig.Tap(Key.W);
            Assert.That(menu.Item, Is.EqualTo(3));
            rig.Tap(PadButton.DPadDown);
            Assert.That(menu.Item, Is.EqualTo(4));
            Assert.That(rig.TakeCues(), Is.EqualTo(Enumerable.Repeat("menu_move", 8)));
        }

        // ---- select and back ----

        [Test]
        public void GameOpensItsLevelAndBackReturnsToTheRootSilently()
        {
            var rig = new Rig();
            rig.Tap(Key.Escape).TakeCues();
            var menu = rig.World.Menu;

            rig.Tap(Key.Enter);
            Assert.That(menu.Level, Is.EqualTo(1));
            Assert.That(menu.Title, Is.EqualTo("Main Menu : Game"));
            Assert.That(Items(rig.World), Is.EqualTo(new[] { "1-0", "1-1" }));
            Assert.That(menu.Description, Is.EqualTo(""));
            Assert.That(menu.AtRoot, Is.False);
            Assert.That(rig.TakeCues(), Is.EqualTo(new[] { "menu_select" }));

            rig.Tap(Key.Down);
            rig.Tap(Key.Down);
            Assert.That(menu.Item, Is.EqualTo(0), "two items wrap");
            rig.Tap(Key.Down);
            rig.Tap(Key.Right); // a placeholder: selecting it does nothing but reset the highlight
            Assert.That((menu.Level, menu.Item), Is.EqualTo((1, 0)));
            rig.TakeCues();

            rig.Tap(Key.Left);
            Assert.That((menu.Level, menu.Item), Is.EqualTo((0, 0)), "Action_GoToParentMenuLevel highlights the parent's first item");
            Assert.That(rig.World.State, Is.EqualTo(GameState.MainMenu));
            Assert.That(rig.TakeCues(), Is.Empty, "going up a level plays nothing");

            rig.Tap(Key.A);
            Assert.That(rig.World.State, Is.EqualTo(GameState.Play), "back at the root closes the menu");
            Assert.That(rig.TakeCues(), Is.EqualTo(new[] { "menu_close" }));
        }

        [Test]
        public void ToggleFullscreenInVideoFlipsTheWindowAndStaysOnItsLevel()
        {
            var rig = new Rig();
            rig.Tap(Key.Escape).Tap(Key.Down).Tap(Key.D);
            var menu = rig.World.Menu;
            Assert.That(menu.Level, Is.EqualTo(2));
            Assert.That(menu.Title, Is.EqualTo("Main Menu : Video"));
            Assert.That(Items(rig.World), Is.EqualTo(new[] { "Toggle Fullscreen", "2-1" }));
            Assert.That(menu.Description, Is.EqualTo("Toggle Fullscreen : Switch between Fullscreen and Windowed mode"));
            rig.TakeCues();

            rig.Tap(PadButton.A);
            Assert.That(rig.Events.OfType<FullscreenToggled>().Count(), Is.EqualTo(1));
            Assert.That(rig.TakeCues(), Is.EqualTo(new[] { "menu_select" }));
            Assert.That((menu.Level, menu.Item), Is.EqualTo((2, 0)));
            Assert.That(rig.World.State, Is.EqualTo(GameState.MainMenu));

            // F12 also toggles inside the menu, without a cue.
            rig.Tap(Key.F12);
            Assert.That(rig.Events.OfType<FullscreenToggled>().Count(), Is.EqualTo(1));
            Assert.That(rig.TakeCues(), Is.Empty);
        }

        [TestCase(2)]
        [TestCase(3)]
        public void AudioAndControlsDoNothingButResetTheHighlight(int item)
        {
            var rig = new Rig();
            rig.Tap(Key.Escape);
            for (var i = 0; i < item; i++) rig.Tap(Key.Down);
            rig.TakeCues();

            rig.Tap(Key.Enter);
            var menu = rig.World.Menu;
            Assert.That((menu.Level, menu.Item), Is.EqualTo((0, 0)));
            Assert.That(rig.World.State, Is.EqualTo(GameState.MainMenu));
            Assert.That(rig.Events.Where(e => !(e is CueRequested)), Is.Empty, "no fullscreen, quit or state change");
            Assert.That(rig.TakeCues(), Is.EqualTo(new[] { "menu_select" }));
        }

        [Test]
        public void QuitExits()
        {
            var rig = new Rig();
            rig.Tap(Key.Escape).Tap(Key.Up).TakeCues();
            Assert.That(rig.World.Menu.CurrentItem.Text, Is.EqualTo("Quit"));

            rig.Tap(Key.Enter);
            Assert.That(rig.Events.OfType<QuitRequested>().Count(), Is.EqualTo(1));
            Assert.That(rig.TakeCues(), Is.EqualTo(new[] { "menu_select" }));
        }

        [Test]
        public void TheSelectionSurvivesClosingTheMenu()
        {
            var rig = new Rig();
            rig.Tap(Key.Escape).Tap(Key.Down).Tap(Key.Enter).Tap(Key.Down);
            rig.Tap(Key.Escape);
            Assert.That(rig.World.State, Is.EqualTo(GameState.Play));
            rig.Tap(Key.Escape);
            Assert.That((rig.World.Menu.Level, rig.World.Menu.Item), Is.EqualTo((2, 1)), "the original never resets the menu on open or close");
        }

        [Test]
        public void BackThenUpInOneStepActInOrder()
        {
            // CheckInput_MainMenu reads back (else select) before up (else down), and the world applies them in that order.
            var rig = new Rig();
            rig.Tap(Key.Escape).Tap(Key.Enter); // into Game
            rig.TakeCues();
            var world = rig.World;
            world.Submit(new NavigateMenu(P1, MenuInput.Back));
            world.Submit(new NavigateMenu(P1, MenuInput.Up));
            rig.Step();
            Assert.That((world.Menu.Level, world.Menu.Item), Is.EqualTo((0, 4)), "back to the root, then up wraps to Quit");
        }

        [Test]
        public void MenuCommandsDoNothingOutsideTheMenu()
        {
            var rig = new Rig();
            foreach (var input in new[] { MenuInput.Select, MenuInput.Down, MenuInput.Back })
            {
                rig.World.Submit(new NavigateMenu(P1, input));
            }

            rig.Step();
            Assert.That((rig.World.Menu.Level, rig.World.Menu.Item), Is.EqualTo((0, 0)));
            Assert.That(rig.World.State, Is.EqualTo(GameState.Play));
            Assert.That(rig.TakeCues(), Is.Empty);
        }

        [Test]
        public void AWorldWithoutMenuDataStillOpensAndClosesTheState()
        {
            var world = new GameWorld();
            world.Submit(new ToggleMainMenu(P1));
            world.Submit(new NavigateMenu(P1, MenuInput.Down));
            world.Advance(1.0 / 60);
            Assert.That(world.State, Is.EqualTo(GameState.MainMenu));
            Assert.That(world.Events.OfType<CueRequested>(), Is.Empty);
            world.Submit(new NavigateMenu(P1, MenuInput.Back));
            world.Advance(1.0 / 60);
            Assert.That(world.State, Is.EqualTo(GameState.Play));
        }

        [Test]
        public void TheMenuOnlyWorldUsesTheMenuData()
        {
            var world = new GameWorld(Settings.Menu);
            world.Submit(new ToggleMainMenu(P1));
            world.Submit(new NavigateMenu(P1, MenuInput.Up));
            world.Advance(1.0 / 60);
            Assert.That(world.Menu.CurrentItem.Text, Is.EqualTo("Quit"));
            Assert.That(world.Events.OfType<CueRequested>().Select(c => c.Cue), Is.EqualTo(new[] { "menu_open", "menu_move" }));
        }

        [Test]
        public void TheMenuDataNamesTheOriginalsCuesAndFont()
        {
            var menu = Settings.Menu;
            Assert.That((menu.OpenCue, menu.CloseCue, menu.MoveCue, menu.SelectCue), Is.EqualTo(("menu_open", "menu_close", "menu_move", "menu_select")));
            Assert.That((menu.Font, menu.FontPoints), Is.EqualTo(("Lucida Console", 14f)));
            Assert.That((menu.Background, menu.BackgroundAlpha, menu.Arrow), Is.EqualTo(("menu1", 0.8f, "arrow1")));
            Assert.That((menu.TitlePosition.X, menu.TitlePosition.Y, menu.DescriptionFromBottom), Is.EqualTo((10f, 15f, 20)));
            Assert.That((menu.ItemColour, menu.SelectedColour, menu.TextColour), Is.EqualTo((SettingsColour.Yellow, SettingsColour.Red, SettingsColour.White)));
        }
    }
}
