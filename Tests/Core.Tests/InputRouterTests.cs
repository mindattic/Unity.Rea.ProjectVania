using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Input;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    /// <summary>
    /// U.VAN-US-A4: the keyboard and Xbox pad do what the original's InputManager did in each game state, with its
    /// edge (on press) and hold semantics, through the shipped input.json (RECONSTRUCTION 3).
    /// </summary>
    public class InputRouterTests
    {
        static readonly GameSettings Settings = GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));

        /// <summary>A world driven by a scripted device: hold or release controls, then step one 1/60 s frame.</summary>
        sealed class Rig : IInputDevice
        {
            readonly InputFrame _held = new InputFrame();

            public Rig(GameState start = GameState.Play, PlayerId? player = null)
            {
                Input = new PlayerInput(player ?? PlayerId.One, Settings.Input, this);
                if (start != GameState.Play)
                {
                    World.Submit(new SetGameState(PlayerId.One, start));
                    World.Advance(1.0 / 60);
                }
            }

            public GameWorld World { get; } = new GameWorld();
            public PlayerInput Input { get; }

            public Rig Hold(params Key[] keys) { foreach (var k in keys) _held.Set(k); return this; }
            public Rig Release(params Key[] keys) { foreach (var k in keys) _held.Set(k, false); return this; }
            public Rig Hold(params PadButton[] buttons) { foreach (var b in buttons) _held.Set(b); return this; }
            public Rig Release(params PadButton[] buttons) { foreach (var b in buttons) _held.Set(b, false); return this; }
            public Rig Hold(MouseButton button) { _held.Set(button); return this; }
            public Rig Release(MouseButton button) { _held.Set(button, false); return this; }
            public Rig Wheel(int notch) { _held.Wheel = notch; return this; }
            public Rig Mouse(float x, float y, bool active = true) { _held.SetMouse(x, y); _held.WindowActive = active; return this; }
            public Rig Connect(bool connected = true) { _held.PadConnected = connected; return this; }

            /// <summary>One step; returns the commands its input issued.</summary>
            public List<ICommand> Step()
            {
                World.Advance(1.0 / 60, Input);
                return Input.LastCommands.ToList();
            }

            /// <summary>Holds the key for one step, then releases it.</summary>
            public List<ICommand> Tap(Key key)
            {
                Hold(key);
                var commands = Step();
                Release(key);
                Step();
                return commands;
            }

            public void Sample(InputFrame frame)
            {
                frame.CopyFrom(_held);
                _held.Wheel = 0; // a wheel movement is one step's
            }
        }

        static List<T> Of<T>(IEnumerable<ICommand> commands) => commands.OfType<T>().ToList();

        static List<string> Typed(IEnumerable<ICommand> commands) =>
            Of<EditConsole>(commands).Where(c => c.Input == ConsoleInput.Append).Select(c => c.Text).ToList();

        // ---- the bindings data (3.1) ----

        [Test]
        public void ShippedBindingsAreTheOriginals()
        {
            var input = Settings.Input;
            Assert.That(input[InputAction.MoveLeft].Keys, Is.EqualTo(new[] { Key.A }));
            Assert.That(input[InputAction.MoveRight].Keys, Is.EqualTo(new[] { Key.D }));
            Assert.That(input[InputAction.Kneel].Buttons, Is.EqualTo(new[] { PadButton.LeftThumbstickDown }));
            Assert.That(input[InputAction.Jump].Keys, Is.EqualTo(new[] { Key.Space }));
            Assert.That(input[InputAction.Jump].Buttons, Is.EqualTo(new[] { PadButton.A }));
            Assert.That(input[InputAction.CycleLeft].Buttons, Is.EqualTo(new[] { PadButton.LeftShoulder }));
            Assert.That(input[InputAction.MainMenu].Buttons, Is.EqualTo(new[] { PadButton.Start }));
            Assert.That(input[InputAction.Console].Keys, Is.EqualTo(new[] { Key.OemTilde }));
            Assert.That(input[InputAction.Console].Buttons, Is.EqualTo(new[] { PadButton.Back }));
            Assert.That(input.MapEditorMouse, Is.EqualTo(MouseButton.Middle));
            Assert.That(input.ConsoleTyping.Count, Is.EqualTo(40));
        }

        [Test]
        public void ABindingWithNoControlIsRejected()
        {
            var shipped = new DirectorySettingsSource(GameSettingsTests.ShippedFolder);
            var text = shipped.Read(GameSettings.InputFile)
                .Replace("\"reset\": { \"keys\": [\"R\"], \"buttons\": [] }", "\"reset\": { \"keys\": [], \"buttons\": [] }")
                .Replace("\"quickBar\": { \"keys\": [\"U\"]", "\"quickBar\": { \"keys\": [\"Ü\"]");
            var e = Assert.Throws<SettingsException>(() => GameSettings.Load(new MemorySettingsSource(shipped).Set(GameSettings.InputFile, text)));
            Assert.That(e.Errors, Has.Some.Contains("'reset' must bind at least one key or button"));
            Assert.That(e.Errors, Has.Some.Contains("Ü"));
        }

        [Test]
        public void AMissingActionIsRejected()
        {
            var shipped = new DirectorySettingsSource(GameSettingsTests.ShippedFolder);
            var text = shipped.Read(GameSettings.InputFile).Replace("\"fullscreen\":", "\"fulscreen\":");
            var e = Assert.Throws<SettingsException>(() => GameSettings.Load(new MemorySettingsSource(shipped).Set(GameSettings.InputFile, text)));
            Assert.That(e.Errors, Has.Some.Contains("'fullscreen' is missing"));
            Assert.That(e.Errors, Has.Some.Contains("unknown key 'fulscreen'"));
        }

        // ---- Play: moving, kneeling, jumping, cycling (3.1, 3.2) ----

        [Test]
        public void MoveKneelOrStandIsIssuedEveryPlayStepFromHeldKeys()
        {
            var rig = new Rig();
            Assert.That(Of<StandCharacter>(rig.Step()), Has.Count.EqualTo(1));
            rig.Hold(Key.A);
            Assert.That(Of<MoveCharacter>(rig.Step()).Single().Direction, Is.EqualTo(-1));
            Assert.That(Of<MoveCharacter>(rig.Step()).Single().Direction, Is.EqualTo(-1), "held, not on press");
            rig.Hold(Key.D);
            Assert.That(Of<MoveCharacter>(rig.Step()).Single().Direction, Is.EqualTo(-1), "left wins the chain");
            rig.Release(Key.A);
            Assert.That(Of<MoveCharacter>(rig.Step()).Single().Direction, Is.EqualTo(1));
            rig.Hold(Key.S);
            Assert.That(Of<MoveCharacter>(rig.Step()).Single().Direction, Is.EqualTo(1), "moving beats kneeling");
            rig.Release(Key.D);
            var kneel = rig.Step();
            Assert.That(Of<KneelCharacter>(kneel), Has.Count.EqualTo(1));
            Assert.That(Of<StandCharacter>(kneel), Is.Empty);
            Assert.That(Of<MoveCharacter>(kneel), Is.Empty);
        }

        [Test]
        public void TheLeftStickMovesAndKneels()
        {
            var rig = new Rig();
            rig.Hold(PadButton.LeftThumbstickRight);
            Assert.That(Of<MoveCharacter>(rig.Step()).Single().Direction, Is.EqualTo(1));
            rig.Release(PadButton.LeftThumbstickRight).Hold(PadButton.LeftThumbstickDown);
            Assert.That(Of<KneelCharacter>(rig.Step()), Has.Count.EqualTo(1));
        }

        [Test]
        public void ShiftOrPadAHeldNoLongerBlocksMoving()
        {
            // BIBLE defect 5, fixed: the original's Command_Move ignored the key while Shift or pad A was held.
            var rig = new Rig();
            rig.Hold(Key.LeftShift, Key.D);
            Assert.That(Of<MoveCharacter>(rig.Step()), Has.Count.EqualTo(1));
            rig.Release(Key.LeftShift).Hold(PadButton.A);
            Assert.That(Of<MoveCharacter>(rig.Step()), Has.Count.EqualTo(1));
        }

        [Test]
        public void SpaceJumpsOnPressButPadAJumpsEveryStepItIsHeld()
        {
            var rig = new Rig();
            rig.Hold(Key.Space);
            Assert.That(Of<JumpCharacter>(rig.Step()), Has.Count.EqualTo(1));
            Assert.That(Of<JumpCharacter>(rig.Step()), Is.Empty, "Space held does not jump again");
            rig.Release(Key.Space);
            rig.Step();
            rig.Hold(Key.Space);
            Assert.That(Of<JumpCharacter>(rig.Step()), Has.Count.EqualTo(1), "pressed again");

            var pad = new Rig();
            pad.Hold(PadButton.A);
            for (var i = 0; i < 3; i++)
            {
                Assert.That(Of<JumpCharacter>(pad.Step()), Has.Count.EqualTo(1), $"step {i}");
            }
        }

        [Test]
        public void BracketsAndShouldersCyclePlayersOnPressLeftFirst()
        {
            var rig = new Rig();
            Assert.That(Of<CycleCharacter>(rig.Tap(Key.OemOpenBrackets)).Single().Direction, Is.EqualTo(-1));
            Assert.That(Of<CycleCharacter>(rig.Tap(Key.OemCloseBrackets)).Single().Direction, Is.EqualTo(1));
            rig.Hold(Key.OemCloseBrackets);
            rig.Step();
            Assert.That(Of<CycleCharacter>(rig.Step()), Is.Empty, "held does not repeat");
            rig.Release(Key.OemCloseBrackets).Step();

            rig.Hold(Key.OemOpenBrackets, Key.OemCloseBrackets);
            Assert.That(Of<CycleCharacter>(rig.Step()).Single().Direction, Is.EqualTo(-1), "an else-if: left wins");
            rig.Release(Key.OemOpenBrackets, Key.OemCloseBrackets).Step();

            rig.Hold(PadButton.RightShoulder);
            Assert.That(Of<CycleCharacter>(rig.Step()).Single().Direction, Is.EqualTo(1));
            rig.Release(PadButton.RightShoulder).Hold(PadButton.LeftShoulder);
            Assert.That(Of<CycleCharacter>(rig.Step()).Single().Direction, Is.EqualTo(-1));
        }

        [Test]
        public void TheDriversIdIsOnEveryCommand()
        {
            var rig = new Rig(player: new PlayerId(1));
            rig.Hold(Key.A, Key.Space, Key.Escape);
            var commands = rig.Step();
            Assert.That(commands, Is.Not.Empty);
            Assert.That(commands.All(c => c.Player == new PlayerId(1)), Is.True);
        }

        // ---- game states (2) ----

        [Test]
        public void EscOrStartOpensAndClosesTheMenu()
        {
            var rig = new Rig();
            rig.Tap(Key.Escape);
            Assert.That(rig.World.State, Is.EqualTo(GameState.MainMenu));
            rig.Tap(Key.Escape);
            Assert.That(rig.World.State, Is.EqualTo(GameState.Play));
            rig.Hold(PadButton.Start).Step();
            Assert.That(rig.World.State, Is.EqualTo(GameState.MainMenu));
            rig.Step();
            Assert.That(rig.World.State, Is.EqualTo(GameState.MainMenu), "held Start does not toggle again");
            rig.Release(PadButton.Start).Step();
            rig.Hold(PadButton.Start).Step();
            Assert.That(rig.World.State, Is.EqualTo(GameState.Play));
        }

        [Test]
        public void TheMenuOpenedFromTheEditorClosesToPlay()
        {
            var rig = new Rig(GameState.MapEditor);
            rig.Tap(Key.Escape);
            Assert.That(rig.World.State, Is.EqualTo(GameState.MainMenu));
            rig.Tap(Key.Escape);
            Assert.That(rig.World.State, Is.EqualTo(GameState.Play));
        }

        [Test]
        public void MTogglesTheEditorFromPlayAndBack()
        {
            var rig = new Rig();
            rig.Tap(Key.M);
            Assert.That(rig.World.State, Is.EqualTo(GameState.MapEditor));
            rig.Tap(Key.M);
            Assert.That(rig.World.State, Is.EqualTo(GameState.Play));
        }

        [TestCase(GameState.Play, GameState.MapEditor)]
        [TestCase(GameState.MapEditor, GameState.Play)]
        [TestCase(GameState.MainMenu, GameState.MapEditor)]
        [TestCase(GameState.Console, GameState.MapEditor)]
        public void TheMiddleMouseButtonTogglesTheEditorInEveryState(GameState start, GameState expected)
        {
            var rig = new Rig(start);
            rig.Hold(MouseButton.Middle).Step();
            Assert.That(rig.World.State, Is.EqualTo(expected));
            rig.Step();
            Assert.That(rig.World.State, Is.EqualTo(expected), "on press only");
        }

        [Test]
        public void MIsNotReadInTheMenu()
        {
            var rig = new Rig(GameState.MainMenu);
            rig.Tap(Key.M);
            Assert.That(rig.World.State, Is.EqualTo(GameState.MainMenu));
        }

        [TestCase(GameState.Play, GameState.Play)]
        [TestCase(GameState.MapEditor, GameState.Play)]
        [TestCase(GameState.MainMenu, GameState.MainMenu)]
        public void RResetsToPlayFromPlayAndTheEditorOnly(GameState start, GameState expected)
        {
            var rig = new Rig(start);
            var commands = rig.Tap(Key.R);
            Assert.That(rig.World.State, Is.EqualTo(expected));
            Assert.That(Of<ResetGame>(commands), Has.Count.EqualTo(start == GameState.MainMenu ? 0 : 1));
        }

        [Test]
        public void TildeOrBackOpensAndClosesTheConsole()
        {
            var rig = new Rig();
            rig.Tap(Key.OemTilde);
            Assert.That(rig.World.State, Is.EqualTo(GameState.Console));
            Assert.That(rig.World.ConsoleOpen, Is.True);
            rig.Tap(Key.OemTilde);
            Assert.That(rig.World.State, Is.EqualTo(GameState.Play));
            Assert.That(rig.World.ConsoleOpen, Is.False);

            rig.Hold(PadButton.Back).Step();
            Assert.That(rig.World.State, Is.EqualTo(GameState.Console));
            rig.Release(PadButton.Back).Step();
            rig.Hold(PadButton.Back).Step();
            Assert.That(rig.World.State, Is.EqualTo(GameState.Play));
        }

        [Test]
        public void EscFromTheConsoleLeavesItDownUnderTheMenu()
        {
            var rig = new Rig();
            rig.Tap(Key.OemTilde);
            rig.Tap(Key.Escape);
            Assert.That(rig.World.State, Is.EqualTo(GameState.MainMenu));
            Assert.That(rig.World.ConsoleOpen, Is.True);
            rig.Tap(Key.Escape);
            Assert.That(rig.World.State, Is.EqualTo(GameState.Play));
            rig.Tap(Key.OemTilde);
            Assert.That(rig.World.ConsoleOpen, Is.False, "tilde in Play closes the console that is still down");
            Assert.That(rig.World.State, Is.EqualTo(GameState.Play));
        }

        [Test]
        public void ToggledActionsActInOrderWithinAStep()
        {
            // Esc and M in one Play frame: the menu opens, then M toggles the editor from the menu (as in the original).
            var rig = new Rig();
            rig.Hold(Key.Escape, Key.M).Step();
            Assert.That(rig.World.State, Is.EqualTo(GameState.MapEditor));
            var changes = rig.World.Events.OfType<GameStateChanged>().ToList();
            Assert.That(changes.Select(c => c.To), Is.EqualTo(new[] { GameState.MainMenu, GameState.MapEditor }));
        }

        // ---- the toolbox keys (3.1) ----

        [TestCase(GameState.Play, true)]
        [TestCase(GameState.MapEditor, true)]
        [TestCase(GameState.MainMenu, true)]
        [TestCase(GameState.Console, false)]
        public void F12TogglesFullscreenExceptInTheConsole(GameState start, bool toggles)
        {
            var rig = new Rig(start);
            rig.Hold(Key.F12).Step();
            Assert.That(rig.World.Events.OfType<FullscreenToggled>().Count(), Is.EqualTo(toggles ? 1 : 0));
            rig.Step();
            Assert.That(rig.World.Events.OfType<FullscreenToggled>(), Is.Empty, "on press only");
        }

        [TestCase(GameState.Play, true)]
        [TestCase(GameState.MapEditor, true)]
        [TestCase(GameState.MainMenu, false)]
        public void UTheQuickBarAndCTheCameraModeOnPress(GameState start, bool read)
        {
            var rig = new Rig(start);
            rig.Hold(Key.U, Key.C);
            var first = rig.Step();
            Assert.That(Of<ToggleQuickBar>(first), Has.Count.EqualTo(read ? 1 : 0));
            Assert.That(Of<CycleCameraMode>(first), Has.Count.EqualTo(read ? 1 : 0));
            var held = rig.Step();
            Assert.That(Of<ToggleQuickBar>(held), Is.Empty);
            Assert.That(Of<CycleCameraMode>(held), Is.Empty);
        }

        [TestCase(Key.Delete, CameraNudge.ScrollLeft)]
        [TestCase(Key.PageDown, CameraNudge.ScrollRight)]
        [TestCase(Key.Home, CameraNudge.ScrollUp)]
        [TestCase(Key.End, CameraNudge.ScrollDown)]
        [TestCase(Key.OemPlus, CameraNudge.ZoomIn)]
        [TestCase(Key.OemMinus, CameraNudge.ZoomOut)]
        [TestCase(Key.Insert, CameraNudge.RotateLeft)]
        [TestCase(Key.PageUp, CameraNudge.RotateRight)]
        public void EachCameraKeyNudgesEveryStepItIsHeld(Key key, CameraNudge nudge)
        {
            foreach (var state in new[] { GameState.Play, GameState.MapEditor })
            {
                var rig = new Rig(state);
                rig.Hold(key);
                for (var i = 0; i < 3; i++)
                {
                    Assert.That(Of<NudgeCamera>(rig.Step()).Single().Nudge, Is.EqualTo(nudge), $"{state} step {i}");
                }
            }
        }

        [Test]
        public void OnlyTheFirstHeldCameraKeyActsInAStep()
        {
            var rig = new Rig();
            rig.Hold(Key.PageUp, Key.OemMinus, Key.Delete);
            Assert.That(Of<NudgeCamera>(rig.Step()).Single().Nudge, Is.EqualTo(CameraNudge.ScrollLeft));
            rig.Release(Key.Delete);
            Assert.That(Of<NudgeCamera>(rig.Step()).Single().Nudge, Is.EqualTo(CameraNudge.ZoomOut));
        }

        [TestCase(GameState.Play)]
        [TestCase(GameState.MapEditor)]
        [TestCase(GameState.MainMenu)]
        [TestCase(GameState.Console)]
        public void TheWheelZoomsOneStepPerNotchInEveryState(GameState start)
        {
            var rig = new Rig(start);
            Assert.That(Of<NudgeCamera>(rig.Wheel(1).Step()).Single().Nudge, Is.EqualTo(CameraNudge.ZoomIn));
            Assert.That(Of<NudgeCamera>(rig.Step()), Is.Empty);
            Assert.That(Of<NudgeCamera>(rig.Wheel(-3).Step()).Single().Nudge, Is.EqualTo(CameraNudge.ZoomOut));
        }

        [Test]
        public void TheCameraKeysAreNotReadInTheMenu()
        {
            var rig = new Rig(GameState.MainMenu);
            rig.Hold(Key.Delete);
            Assert.That(Of<NudgeCamera>(rig.Step()), Is.Empty);
        }

        [Test]
        public void AConnectingPadIsAnnouncedOnceInPlayAndTheEditor()
        {
            var rig = new Rig();
            rig.Connect();
            Assert.That(Of<AnnounceGamepad>(rig.Step()), Has.Count.EqualTo(1));
            Assert.That(Of<AnnounceGamepad>(rig.Step()), Is.Empty);

            var menu = new Rig(GameState.MainMenu);
            menu.Connect();
            Assert.That(Of<AnnounceGamepad>(menu.Step()), Is.Empty);
        }

        // ---- MapEditor (3.2, 12.1) ----

        [Test]
        public void TheEditorReadsItsKeysInsteadOfMovingAndJumping()
        {
            var rig = new Rig(GameState.MapEditor);
            rig.Hold(Key.A);
            var commands = rig.Step();
            Assert.That(Of<MoveCharacter>(commands), Is.Empty);
            Assert.That(Of<StandCharacter>(commands), Is.Empty);
            var step = Of<StepEditor>(commands).Single();
            Assert.That((step.Dx, step.Dy, step.SelectTile, step.MoveCell), Is.EqualTo((-1, 0, true, true)));
            Assert.That(Of<StepEditor>(rig.Step()), Is.Empty, "on press only");
        }

        [Test]
        public void TheEditorReadsTheMouseEveryStepWithItsButtons()
        {
            var rig = new Rig(GameState.MapEditor);
            Assert.That(Of<PointEditor>(rig.Step()), Is.Empty, "a device that reports no mouse points nothing");

            rig.Mouse(410f, 290f, active: false).Hold(MouseButton.Left);
            var point = Of<PointEditor>(rig.Step()).Single();
            Assert.That((point.X, point.Y, point.Left, point.Right, point.WindowActive), Is.EqualTo((410f, 290f, true, false, false)));
            point = Of<PointEditor>(rig.Step()).Single();
            Assert.That(point.Left, Is.True, "held, not pressed");

            rig.Release(MouseButton.Left).Hold(MouseButton.Right).Mouse(400f, 300f);
            point = Of<PointEditor>(rig.Step()).Single();
            Assert.That((point.Left, point.Right, point.WindowActive), Is.EqualTo((false, true, true)));

            var play = new Rig();
            play.Mouse(410f, 290f).Hold(MouseButton.Left);
            Assert.That(Of<PointEditor>(play.Step()), Is.Empty, "only the editor reads it");
        }

        [Test]
        public void SpaceHeldPutsTheEditorCursorOnTheScreenWithoutJumping()
        {
            var rig = new Rig(GameState.MapEditor);
            Assert.That(Of<SetEditorCursor>(rig.Step()).Single().Mode, Is.EqualTo(EditorCursorMode.World));
            rig.Hold(Key.Space);
            for (var i = 0; i < 2; i++)
            {
                var commands = rig.Step();
                Assert.That(Of<SetEditorCursor>(commands).Single().Mode, Is.EqualTo(EditorCursorMode.Screen));
                Assert.That(Of<JumpCharacter>(commands), Is.Empty);
            }

            rig.Release(Key.Space);
            Assert.That(Of<SetEditorCursor>(rig.Step()).Single().Mode, Is.EqualTo(EditorCursorMode.World));
        }

        [Test]
        public void CtrlKeepsThePaletteAndShiftKeepsTheCursor()
        {
            var rig = new Rig(GameState.MapEditor);
            rig.Hold(Key.RightControl, Key.D);
            var ctrl = Of<StepEditor>(rig.Step()).Single();
            Assert.That((ctrl.Dx, ctrl.SelectTile, ctrl.MoveCell), Is.EqualTo((1, false, true)));
            rig.Release(Key.RightControl, Key.D).Step();
            rig.Hold(Key.LeftShift, Key.W);
            var shift = Of<StepEditor>(rig.Step()).Single();
            Assert.That((shift.Dy, shift.SelectTile, shift.MoveCell), Is.EqualTo((-1, true, false)));
        }

        [Test]
        public void OneHorizontalAndOneVerticalEditorStepPerFrame()
        {
            var rig = new Rig(GameState.MapEditor);
            rig.Hold(Key.A, Key.D, Key.S);
            var steps = Of<StepEditor>(rig.Step());
            Assert.That(steps.Select(s => (s.Dx, s.Dy)), Is.EqualTo(new[] { (-1, 0), (0, 1) }));
            rig.Release(Key.A, Key.D, Key.S).Hold(PadButton.DPadUp);
            Assert.That(Of<StepEditor>(rig.Step()).Single().Dy, Is.EqualTo(-1));
        }

        [Test]
        public void TheEditorDoesNotCyclePlayers()
        {
            var rig = new Rig(GameState.MapEditor);
            Assert.That(Of<CycleCharacter>(rig.Tap(Key.OemOpenBrackets)), Is.Empty);
        }

        // ---- MainMenu (9.2) ----

        [TestCase(Key.A)]
        [TestCase(Key.Left)]
        public void LeftAtTheRootClosesTheMenu(Key key)
        {
            var rig = new Rig(GameState.MainMenu);
            var commands = rig.Tap(key);
            Assert.That(Of<NavigateMenu>(commands).Single().Input, Is.EqualTo(MenuInput.Back));
            Assert.That(rig.World.State, Is.EqualTo(GameState.Play));
        }

        [TestCase(PadButton.B)]
        [TestCase(PadButton.DPadLeft)]
        [TestCase(PadButton.LeftThumbstickLeft)]
        public void BAndPadLeftGoBack(PadButton button)
        {
            var rig = new Rig(GameState.MainMenu);
            rig.Hold(button);
            Assert.That(Of<NavigateMenu>(rig.Step()).Single().Input, Is.EqualTo(MenuInput.Back));
        }

        [Test]
        public void MenuKeysSelectAndMoveOnPress()
        {
            foreach (var key in new[] { Key.Enter, Key.D, Key.Right })
            {
                Assert.That(Of<NavigateMenu>(new Rig(GameState.MainMenu).Tap(key)).Single().Input, Is.EqualTo(MenuInput.Select), key.ToString());
            }

            foreach (var key in new[] { Key.W, Key.Up })
            {
                Assert.That(Of<NavigateMenu>(new Rig(GameState.MainMenu).Tap(key)).Single().Input, Is.EqualTo(MenuInput.Up), key.ToString());
            }

            foreach (var key in new[] { Key.S, Key.Down })
            {
                Assert.That(Of<NavigateMenu>(new Rig(GameState.MainMenu).Tap(key)).Single().Input, Is.EqualTo(MenuInput.Down), key.ToString());
            }

            var pad = new Rig(GameState.MainMenu);
            pad.Hold(PadButton.A);
            Assert.That(Of<NavigateMenu>(pad.Step()).Single().Input, Is.EqualTo(MenuInput.Select));
            Assert.That(Of<NavigateMenu>(pad.Step()), Is.Empty, "pad A selects on press in the menu");
        }

        [Test]
        public void MenuEdgesArePerKeyNotPerAction()
        {
            // The original tested each key's own edge: holding S, a press of Down still moves down.
            var rig = new Rig(GameState.MainMenu);
            rig.Hold(Key.S);
            rig.Step();
            rig.Hold(Key.Down);
            Assert.That(Of<NavigateMenu>(rig.Step()).Single().Input, Is.EqualTo(MenuInput.Down));
        }

        [Test]
        public void BackBeatsSelectAndUpBeatsDownInOneStep()
        {
            var rig = new Rig(GameState.MainMenu);
            rig.Hold(Key.Up, Key.Down, Key.Enter, Key.Left);
            var inputs = Of<NavigateMenu>(rig.Step()).Select(n => n.Input);
            Assert.That(inputs, Is.EqualTo(new[] { MenuInput.Back, MenuInput.Up }));
        }

        [Test]
        public void TheMenuDoesNotMoveOrJump()
        {
            var rig = new Rig(GameState.MainMenu);
            rig.Hold(Key.D, Key.Space);
            var commands = rig.Step();
            Assert.That(Of<MoveCharacter>(commands), Is.Empty);
            Assert.That(Of<JumpCharacter>(commands), Is.Empty);
            Assert.That(Of<StandCharacter>(commands), Is.Empty);
        }

        // ---- Console typing (10.2) ----

        [Test]
        public void TheConsoleTypesTheOriginalsCharacters()
        {
            var rig = new Rig(GameState.Console);
            Assert.That(Typed(rig.Tap(Key.H)), Is.EqualTo(new[] { "h" }));
            Assert.That(Typed(rig.Tap(Key.D1)), Is.EqualTo(new[] { "1" }));
            Assert.That(Typed(rig.Tap(Key.Space)), Is.EqualTo(new[] { " " }));
            Assert.That(Typed(rig.Tap(Key.OemPipe)), Is.EqualTo(new[] { "\\" }));
            rig.Hold(Key.LeftShift);
            Assert.That(Typed(rig.Step()), Is.Empty, "Left Shift types nothing");
            Assert.That(Typed(rig.Tap(Key.D1)), Is.EqualTo(new[] { "!" }));
            Assert.That(Typed(rig.Tap(Key.OemQuotes)), Is.EqualTo(new[] { "''" }), "the original's double apostrophe");
            Assert.That(Typed(rig.Tap(Key.H)), Is.EqualTo(new[] { "h" }), "no upper case");
        }

        [Test]
        public void KeysWithoutACharacterTypeTheirOwnNames()
        {
            var rig = new Rig(GameState.Console);
            Assert.That(Typed(rig.Tap(Key.RightShift)), Is.EqualTo(new[] { "rightshift" }));
            Assert.That(Typed(rig.Tap(Key.F12)), Is.EqualTo(new[] { "f12" }));
            Assert.That(Typed(rig.Tap(Key.Left)), Is.EqualTo(new[] { "left" }));
            rig.Hold(Key.B, Key.A);
            Assert.That(Typed(rig.Step()), Is.EqualTo(new[] { "a", "b" }), "every new key, in key-code order");
            rig.Release(Key.A, Key.B).Step();
            rig.Hold(Key.Z, Key.D5);
            Assert.That(Typed(rig.Step()), Is.EqualTo(new[] { "5" }), "the chain wins over the fallback");
        }

        [Test]
        public void TheConsoleEditKeys()
        {
            var rig = new Rig(GameState.Console);
            Assert.That(Of<EditConsole>(rig.Tap(Key.Enter)).Single().Input, Is.EqualTo(ConsoleInput.Submit));
            Assert.That(Of<EditConsole>(rig.Tap(Key.Delete)).Single().Input, Is.EqualTo(ConsoleInput.Clear));
            Assert.That(Of<EditConsole>(rig.Tap(Key.Up)).Single().Input, Is.EqualTo(ConsoleInput.Recall));

            rig.Hold(Key.Back);
            Assert.That(Of<EditConsole>(rig.Step()).Single().Input, Is.EqualTo(ConsoleInput.Backspace));
            Assert.That(Of<EditConsole>(rig.Step()), Is.Empty, "Backspace alone acts on press");
            rig.Hold(Key.LeftShift);
            Assert.That(Of<EditConsole>(rig.Step()).Single().Input, Is.EqualTo(ConsoleInput.Backspace));
            Assert.That(Of<EditConsole>(rig.Step()).Single().Input, Is.EqualTo(ConsoleInput.Backspace), "Shift+Backspace repeats while held");
        }

        [Test]
        public void TheConsoleDoesNotMoveJumpOrReadTheToolbox()
        {
            var rig = new Rig(GameState.Console);
            rig.Hold(Key.D, Key.Space, Key.R, Key.M, Key.Delete);
            var commands = rig.Step();
            Assert.That(Of<MoveCharacter>(commands), Is.Empty);
            Assert.That(Of<JumpCharacter>(commands), Is.Empty);
            Assert.That(Of<ResetGame>(commands), Is.Empty);
            Assert.That(Of<ToggleMapEditor>(commands), Is.Empty);
            Assert.That(Of<NudgeCamera>(commands), Is.Empty);
            Assert.That(rig.World.State, Is.EqualTo(GameState.Console));
        }
    }
}
