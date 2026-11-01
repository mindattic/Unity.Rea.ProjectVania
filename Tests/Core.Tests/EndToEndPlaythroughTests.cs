using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Characters;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Editing;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    /// <summary>
    /// U.VAN-US-A18: one scripted run through the fully composed, engine-free world (the same roster, camera, menu,
    /// console, HUD, audio table and editor <c>GameHost.Initialize</c> builds for the real game, not the single-system
    /// minimal worlds the other Core.Tests files use) that plays every state and mechanic
    /// RECONSTRUCTION records and the owner asked U.VAN-T21 to verify end to end: run, jump, kneel (and its release),
    /// slide, wall slide, wall jump (and its push-off), all four camera modes, the main menu, console commands, map
    /// editor painting and audio cues. Each phase is commented with the RECONSTRUCTION section and BIBLE decision it
    /// is standing in for; the precise frame-by-frame arithmetic is already pinned by the per-system tests (notably
    /// PlayerCharacterTests, CameraTests, MainMenuTests, ConsoleTests, MapEditorTests, HudTests) - this test instead
    /// proves the whole game plays through these mechanics together without needing Unity, and settles the two
    /// behaviours BIBLE §15 defects 3 and 6 had left for "A18's play-through" to decide (U.VAN-T23's kneel release and
    /// U.VAN-T24's wall jump push-off): both are kept exactly as those tickets tuned them (player.json's
    /// kneel.releaseStandsUp and wallJump.pushOff/pushSpeed 200), because this run shows them working end to end in
    /// the composed game, not only in PlayerCharacterTests' isolated physics world. <c>unity.ps1 capture</c>'s
    /// SceneCapture plays the same states with Unity's graphics for the screenshots the story also asks for.
    /// </summary>
    public class EndToEndPlaythroughTests
    {
        const double Frame = 1.0 / 60;
        static readonly PlayerId P1 = PlayerId.One;
        static readonly ICommand Left = new MoveCharacter(P1, -1);
        static readonly ICommand Idle = new StandCharacter(P1);
        static readonly ICommand Down = new KneelCharacter(P1);
        static readonly ICommand Jump = new JumpCharacter(P1);

        static GameSettings Settings() => GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));

        /// <summary>The shipped world, built exactly as <c>GameHost.Initialize</c> composes it: characters.json's roster
        /// (Morris, Shanoa, Leo; U.VAN-T25) at player.json's spawns, camera, menu, console, audio (so cues are named),
        /// HUD (so the message stack and fade exist) and the editor, so every one of the story's systems is present
        /// together, as it is in the real game.</summary>
        static GameWorld ShippedWorld(out List<IGameEvent> recorded)
        {
            var s = Settings();
            var world = new GameWorld(Map.FromSettings(s.Grid), s.Player, s.Characters.Roster, s.Names, new Random(42),
                s.Camera, s.Menu, s.Console, s.Audio, s.Hud, s.Animations, s.Editor);
            recorded = new List<IGameEvent>();
            return world;
        }

        static void Advance(GameWorld world, List<IGameEvent> recorded, int frames = 1)
        {
            for (var i = 0; i < frames; i++)
            {
                world.Advance(Frame);
                recorded.AddRange(world.Events);
            }
        }

        static void Step(GameWorld world, List<IGameEvent> recorded, params ICommand[] commands)
        {
            foreach (var c in commands) world.Submit(c);
            Advance(world, recorded);
        }

        /// <summary>Clears the console's line, types <paramref name="line"/> and runs it, in one step (as the original's
        /// Enter did): mirrors <c>SceneCapture.Type</c>, the same script path <c>unity.ps1 capture</c> exercises.</summary>
        static void RunConsoleLine(GameWorld world, List<IGameEvent> recorded, string line)
        {
            world.Submit(new EditConsole(P1, ConsoleInput.Clear));
            world.Submit(new EditConsole(P1, ConsoleInput.Append, line));
            world.Submit(new EditConsole(P1, ConsoleInput.Submit));
            Advance(world, recorded);
        }

        [Test]
        public void FullPlaythrough_PlaysEveryStateAndMechanicAndMatchesReconstruction()
        {
            var world = ShippedWorld(out var events);
            var c = world.Characters[0]; // player one drives character 0 (Morris) at spawn until it cycles (RECONSTRUCTION 4, U.VAN-T25)

            Assert.That(world.State, Is.EqualTo(GameState.Play), "the original starts in Play (RECONSTRUCTION 2)");
            Assert.That(world.Characters, Has.Count.EqualTo(3), "the roster is Morris, Shanoa and Leo (U.VAN-T25)");
            Assert.That(world.DrivenIndex(P1), Is.EqualTo(0));

            // ---- Falling and landing on the 16 px grid (RECONSTRUCTION 5) ----
            for (var i = 1; i <= 200 && !(c.Terrain == TerrainState.OnGround && c.Velocity.Y == 0); i++)
            {
                Step(world, events, Idle);
            }

            Assert.That(c.Terrain, Is.EqualTo(TerrainState.OnGround));
            Assert.That(c.Position.Y, Is.EqualTo(480f), "feet rest on the row-30 floor (RECONSTRUCTION 5, the bridge level)");

            // ---- Run (RECONSTRUCTION 4.2) ----
            // Left, toward the bridge's left wall: grid.json's structures (U.VAN-T25, Leo's climbing blocks) sit to the
            // right of spawn between columns 35 and 39, so the run and the jump that follows stay on the clear floor
            // (columns 21-34) the owner's _about comment on grid.json's structures promises, and end up conveniently
            // placed for the wall phase below.
            for (var i = 0; i < 15; i++) Step(world, events, Left);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Running));
            Assert.That(c.Facing, Is.EqualTo(-1));
            Assert.That(c.Velocity.X, Is.LessThan(0f));

            // ---- Jump: a full fixed curve releasing the key does not shorten (RECONSTRUCTION 4.3-4.4) ----
            var beforeJump = c.Position.Y;
            Step(world, events, Left, Jump);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Jumping));
            var apex = beforeJump;
            var jumpFrames = 1;
            while (c.Action == ActionKind.Jumping && jumpFrames < 60)
            {
                Step(world, events, Left);
                apex = Math.Min(apex, c.Position.Y);
                jumpFrames++;
            }

            Assert.That(c.Action, Is.EqualTo(ActionKind.Falling), "the curve ends on its own, not by releasing the key");
            Assert.That(beforeJump - apex, Is.EqualTo(76f).Within(2f), "about 76 px, as PlayerCharacterTests pins exactly");
            for (var i = 1; i <= 200 && c.Action != ActionKind.Running; i++) Step(world, events, Left);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Running), "lands back on the floor and keeps running");

            // ---- Kneel and its release settled (BIBLE defect 3, U.VAN-T23, player.json kneel.releaseStandsUp) ----
            Step(world, events, Idle);
            Step(world, events, Down);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Kneeling));
            Step(world, events, Idle); // releasing S: U.VAN-T23 stands him up at once instead of the original's held kneel
            Assert.That(c.Action, Is.EqualTo(ActionKind.Standing), "U.VAN-T21 settles this: the release-stands-up fix plays in the composed game");

            // ---- Slide: a moving kneel (RECONSTRUCTION 4.5) ----
            for (var i = 0; i < 10; i++) Step(world, events, Left);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Running));
            for (var i = 0; i < 15; i++)
            {
                Step(world, events, Down);
                Assert.That(c.Action, Is.EqualTo(ActionKind.Sliding));
            }

            Step(world, events, Idle);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Standing), "a slide is not a kneel, so releasing stands him up directly");

            // ---- Wall slide and wall jump push-off settled (RECONSTRUCTION 4.6-4.7, 4.9; BIBLE defect 6, U.VAN-T24) ----
            // Placed in the air beside the bridge's left wall (column 20, right face x = 336), the way PlayerCharacterTests'
            // OnLeftWall does: running the character into the wall at floor level only blocks him against it (wall sliding
            // needs an airborne approach, RECONSTRUCTION 4.6), so this is positioned the same way the per-system physics
            // tests reach it. From here the character is again driven only by commands, same as every other phase.
            c.Reset(new PixelVector(350f, 300f));
            var reachedWall = 0;
            while (c.Action != ActionKind.WallSliding && reachedWall < 60)
            {
                Step(world, events, Left);
                reachedWall++;
            }

            Assert.That(c.Action, Is.EqualTo(ActionKind.WallSliding));
            Assert.That(c.Terrain, Is.EqualTo(TerrainState.OnWall));

            // Held long enough to settle onto the cap (PlayerCharacterTests pins this at 30 frames): proves the slide is
            // actually capped at 83.33 px/s, not just still accelerating and coincidentally under it.
            for (var i = 0; i < 40 && c.Action == ActionKind.WallSliding; i++) Step(world, events, Left);
            Assert.That(c.Action, Is.EqualTo(ActionKind.WallSliding));
            Assert.That(c.Terrain, Is.EqualTo(TerrainState.OnWall));
            Assert.That(c.Velocity.Y, Is.EqualTo(500f / 6f).Within(1e-2f), "capped at 83.33 px/s, not the 500 px/s terminal fall");

            var wallX0 = c.Position.X;
            Step(world, events, Left, Jump);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Jumping));
            Assert.That(c.IsWallJumping, Is.True);
            Assert.That(c.Velocity.X, Is.EqualTo(200f * 0.65f).Within(1e-2f), "player.json's wallJump.pushSpeed (200) through one frame of air drag, kicking him off the wall at once");
            Assert.That(c.Position.X, Is.GreaterThan(wallX0), "pushed away from the wall, not pinned to it as the original was (defect 6)");

            var wallJumpFrames = 0;
            while (c.Action == ActionKind.Jumping && wallJumpFrames < 60)
            {
                Step(world, events, Left);
                wallJumpFrames++;
            }

            Assert.That(c.Action, Is.EqualTo(ActionKind.Falling));
            Assert.That(c.IsWallJumping, Is.False);
            Assert.That(c.Position.X, Is.GreaterThan(wallX0 + 10f), "U.VAN-T21 settles this: the push-off carries him clear of the wall end to end");

            // ---- Camera modes: Free -> LockPlayer -> LoosePlayer -> LooseCursor -> Free (RECONSTRUCTION 7) ----
            var order = new[] { CameraMode.Free, CameraMode.LockPlayer, CameraMode.LoosePlayer, CameraMode.LooseCursor };
            var camera = world.Camera(P1);
            Assert.That(camera, Is.Not.Null);
            var startMode = camera.Mode;
            var seen = new HashSet<CameraMode> { startMode };
            for (var i = 0; i < 4; i++)
            {
                Step(world, events, new CycleCameraMode(P1));
                seen.Add(camera.Mode);
            }

            Assert.That(camera.Mode, Is.EqualTo(startMode), "four cycles of a four-mode wheel return to the start");
            Assert.That(seen, Is.EquivalentTo(order), "every one of the original's four camera modes was visited");

            // ---- Main menu: Esc opens it over the frozen world (RECONSTRUCTION 2, 9.2) ----
            var beforeMenu = c.Position;
            Step(world, events, new ToggleMainMenu(P1));
            Assert.That(world.State, Is.EqualTo(GameState.MainMenu));
            Assert.That(events.OfType<CueRequested>().Any(e => e.Cue == "menu_open"), Is.True);
            for (var i = 0; i < 30; i++) Step(world, events, Idle); // held frames while the menu is open freeze Play
            Assert.That(c.Position, Is.EqualTo(beforeMenu), "only Play steps the characters (RECONSTRUCTION 2)");

            Step(world, events, new NavigateMenu(P1, MenuInput.Down));
            Assert.That(events.OfType<CueRequested>().Any(e => e.Cue == "menu_move"), Is.True);
            Step(world, events, new NavigateMenu(P1, MenuInput.Select));
            Assert.That(events.OfType<CueRequested>().Any(e => e.Cue == "menu_select"), Is.True);
            Assert.That(world.Menu.AtRoot, Is.False, "Select opened a level (the Video level, as unity.ps1 capture's menu-video.png shows)");

            Step(world, events, new NavigateMenu(P1, MenuInput.Back));
            Assert.That(world.Menu.AtRoot, Is.True);
            Step(world, events, new ToggleMainMenu(P1));
            Assert.That(world.State, Is.EqualTo(GameState.Play));
            Assert.That(events.OfType<CueRequested>().Any(e => e.Cue == "menu_close"), Is.True);

            // ---- Console: the script language runs a line and feeds the HUD and the mute flag (RECONSTRUCTION 10, 10.1, 9.1) ----
            Step(world, events, new ToggleConsole(P1));
            for (var i = 0; i < 400 && world.State != GameState.Console; i++) Step(world, events, Idle);
            Assert.That(world.State, Is.EqualTo(GameState.Console), "the slide finished and the state flipped (RECONSTRUCTION 10)");

            RunConsoleLine(world, events, "say the end to end run reached the console");
            Assert.That(world.Console.History[0], Is.EqualTo("say the end to end run reached the console"));
            Assert.That(world.Hud.Messages.Messages.Any(m => m.Text == "''the end to end run reached the console''"), Is.True, "the script's say joins the HUD's message stack (RECONSTRUCTION 9.1, 10)");

            RunConsoleLine(world, events, "mute");
            Assert.That(world.Muted, Is.True);
            Assert.That(events.OfType<MessageRequested>().Any(e => e.Text == "Mute: On"), Is.True);
            RunConsoleLine(world, events, "mute");
            Assert.That(world.Muted, Is.False);

            Step(world, events, new ToggleConsole(P1));
            for (var i = 0; i < 400 && world.Console.Sliding; i++) Step(world, events, Idle);
            Assert.That(world.State, Is.EqualTo(GameState.Play));

            // ---- Map editor: paint a Hard cell and leave it solid in Play (RECONSTRUCTION 8, 12) ----
            const float centreX = 400f, centreY = 300f;
            Step(world, events, new ToggleMapEditor(P1));
            Assert.That(world.State, Is.EqualTo(GameState.MapEditor));
            Assert.That(world.Camera(P1).Mode, Is.EqualTo(CameraMode.Free), "M puts the camera in Free mode over an editor cursor (RECONSTRUCTION 8)");

            var editor = world.Editor(P1);
            Assert.That(editor, Is.Not.Null, "opening the editor puts the cursor on the driven character at once");
            var before = editor.SelectedCell;
            Assert.That(world.Map[before.X, before.Y].State, Is.Not.EqualTo(CellState.Hard), "the cell under the cursor starts out paintable, not already Hard");

            world.Submit(new SetEditorCursor(P1, EditorCursorMode.World));
            world.Submit(new PointEditor(P1, centreX, centreY, left: true, right: false));
            Advance(world, events);
            var painted = editor.SelectedCell;
            Assert.That(painted, Is.EqualTo(before), "the mouse never left the dead zone, so the same cell is still selected");
            Assert.That(world.Map[painted.X, painted.Y].State, Is.EqualTo(CellState.Hard), "left paints a Hard cell from the selected palette tile");

            Step(world, events, new ToggleMapEditor(P1));
            Assert.That(world.State, Is.EqualTo(GameState.Play));
            Assert.That(world.Map.CellMatchesState(painted.X, painted.Y, CellState.Hard), Is.True, "the paint persists once the editor closes (RECONSTRUCTION 12)");

            // ---- Audio: the cue table names played through the whole run (RECONSTRUCTION 11) ----
            var cues = events.OfType<CueRequested>().Select(e => e.Cue).ToHashSet();
            Assert.That(cues, Does.Contain("step2"), "running names the run cue at least once (audio.json's runCue)");
            Assert.That(cues, Does.Contain("menu_open").And.Contain("menu_move").And.Contain("menu_select").And.Contain("menu_close"));
            Assert.That(cues, Does.Contain("machine_1").Or.Contain("machine_1_short"), "the console's slide names its cue (audio.json's ambience bank)");
        }
    }
}
