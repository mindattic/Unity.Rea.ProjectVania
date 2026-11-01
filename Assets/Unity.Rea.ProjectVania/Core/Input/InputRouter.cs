using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Settings;

namespace Unity.Rea.ProjectVania.Core.Input
{
    /// <summary>
    /// The original's InputManager dispatch (RECONSTRUCTION 3.2): one handler per game state, chosen by the state at
    /// the start of the step, reading the controls in the original's order with its own <c>if</c> / <c>else if</c>
    /// chains and its edge or hold test per control, then the mouse in every state. It only issues commands; the world
    /// applies them in order, so a handler that opens the menu and then reads M behaves as the original did.
    /// <para>
    /// Deviation (BIBLE, defect 5, fixed): the original's <c>Command_Move</c> did nothing while Shift or pad A was
    /// held; here neither blocks moving. The original's empty stubs (Q, W, Enter and Backspace in play, J, K, L, 1-5,
    /// pad X/Y/B and the ActionCross stand-ins, the jump-release branch) issue nothing because they did nothing.
    /// </para>
    /// </summary>
    public static class InputRouter
    {
        public static void Route(GameState state, InputState input, InputData bindings, PlayerId player, List<ICommand> output)
        {
            var read = new Reader(input, bindings);
            switch (state)
            {
                case GameState.Play:
                    Play(read, player, output);
                    break;
                case GameState.MapEditor:
                    MapEditor(read, player, output);
                    break;
                case GameState.Console:
                    Console(read, bindings, player, output);
                    break;
                case GameState.MainMenu:
                    MainMenu(read, player, output);
                    break;
            }

            Mouse(input, bindings, player, output);
        }

        /// <summary><c>CheckInput_Play</c>.</summary>
        static void Play(Reader read, PlayerId player, List<ICommand> output)
        {
            if (read.Input.PadJustConnected)
            {
                output.Add(new AnnounceGamepad(player));
            }

            MenuAndConsole(read, player, output);

            // Held, one of four each step: left, else right, else down (kneel), else stand.
            if (read.Down(InputAction.MoveLeft))
            {
                output.Add(new MoveCharacter(player, -1));
            }
            else if (read.Down(InputAction.MoveRight))
            {
                output.Add(new MoveCharacter(player, 1));
            }
            else if (read.Down(InputAction.Kneel))
            {
                output.Add(new KneelCharacter(player));
            }
            else
            {
                output.Add(new StandCharacter(player));
            }

            // The key jumps on press; the pad button jumps on any step it is held.
            if (read.KeyPressed(InputAction.Jump) || read.ButtonDown(InputAction.Jump))
            {
                output.Add(new JumpCharacter(player));
            }

            if (read.Pressed(InputAction.Reset))
            {
                output.Add(new ResetGame(player));
            }

            if (read.Pressed(InputAction.CycleLeft))
            {
                output.Add(new CycleCharacter(player, -1));
            }
            else if (read.Pressed(InputAction.CycleRight))
            {
                output.Add(new CycleCharacter(player, 1));
            }

            Toolbox(read, player, output);
        }

        /// <summary><c>CheckInput_MapEditor</c>: the editor's keys instead of moving and jumping.</summary>
        static void MapEditor(Reader read, PlayerId player, List<ICommand> output)
        {
            if (read.Input.PadJustConnected)
            {
                output.Add(new AnnounceGamepad(player));
            }

            output.Add(new SetEditorCursor(player, read.Down(InputAction.EditorScreenCursor) ? EditorCursorMode.Screen : EditorCursorMode.World));
            var frame = read.Input.Current;
            if (frame.HasMouse)
            {
                // The mouse the editor's update reads after the camera moves (MapEditor.Update's Input.CurrentMouseState).
                output.Add(new PointEditor(player, frame.MouseX, frame.MouseY, frame.IsDown(MouseButton.Left), frame.IsDown(MouseButton.Right), frame.WindowActive));
            }

            MenuAndConsole(read, player, output);

            var selectTile = !read.Down(InputAction.Ctrl);
            var moveCell = !read.Down(InputAction.Shift);
            if (read.Pressed(InputAction.EditorLeft))
            {
                output.Add(new StepEditor(player, -1, 0, selectTile, moveCell));
            }
            else if (read.Pressed(InputAction.EditorRight))
            {
                output.Add(new StepEditor(player, 1, 0, selectTile, moveCell));
            }

            if (read.Pressed(InputAction.EditorUp))
            {
                output.Add(new StepEditor(player, 0, -1, selectTile, moveCell));
            }
            else if (read.Pressed(InputAction.EditorDown))
            {
                output.Add(new StepEditor(player, 0, 1, selectTile, moveCell));
            }

            if (read.Pressed(InputAction.Reset))
            {
                output.Add(new ResetGame(player));
            }

            Toolbox(read, player, output);
        }

        /// <summary><c>CheckInput_Console</c>: one chain, so at most one edit a step; Shift+Backspace repeats while held.</summary>
        static void Console(Reader read, InputData bindings, PlayerId player, List<ICommand> output)
        {
            if (read.Pressed(InputAction.MainMenu))
            {
                output.Add(new ToggleMainMenu(player));
            }

            var shift = read.Down(InputAction.Shift);
            if (read.Pressed(InputAction.Console))
            {
                output.Add(new ToggleConsole(player));
            }
            else if (read.Pressed(InputAction.ConsoleSubmit))
            {
                output.Add(new EditConsole(player, ConsoleInput.Submit));
            }
            else if (read.Pressed(InputAction.ConsoleClear))
            {
                output.Add(new EditConsole(player, ConsoleInput.Clear));
            }
            else if (read.Pressed(InputAction.ConsoleBackspace) || (read.Down(InputAction.ConsoleBackspace) && shift))
            {
                output.Add(new EditConsole(player, ConsoleInput.Backspace));
            }
            else if (read.Pressed(InputAction.ConsoleRecall))
            {
                output.Add(new EditConsole(player, ConsoleInput.Recall));
            }
            else if (read.Pressed(InputAction.ConsoleSpace))
            {
                output.Add(new EditConsole(player, ConsoleInput.Append, " "));
            }
            else if (!Type(read, bindings, shift, player, output))
            {
                // Anything else newly pressed appends its own name in lower case, as the original did.
                var input = read.Input;
                input.Current.ForEachKeyDown(key =>
                {
                    if (input.Pressed(key) && !Contains(bindings.ConsoleIgnores, key))
                    {
                        output.Add(new EditConsole(player, ConsoleInput.Append, key.ToString().ToLowerInvariant()));
                    }
                });
            }
        }

        /// <summary>The typing chain: the first entry whose key was pressed (and, for a shifted entry, with Shift) appends its text.</summary>
        static bool Type(Reader read, InputData bindings, bool shift, PlayerId player, List<ICommand> output)
        {
            foreach (var typed in bindings.ConsoleTyping)
            {
                if (read.Input.Pressed(typed.Key) && (shift || !typed.Shift))
                {
                    output.Add(new EditConsole(player, ConsoleInput.Append, typed.Text));
                    return true;
                }
            }

            return false;
        }

        /// <summary><c>CheckInput_MainMenu</c>.</summary>
        static void MainMenu(Reader read, PlayerId player, List<ICommand> output)
        {
            if (read.Pressed(InputAction.Console))
            {
                output.Add(new ToggleConsole(player));
            }

            if (read.Pressed(InputAction.MainMenu))
            {
                output.Add(new ToggleMainMenu(player));
            }

            if (read.Pressed(InputAction.Fullscreen))
            {
                output.Add(new ToggleFullscreen(player));
            }

            if (read.Pressed(InputAction.MenuBack))
            {
                output.Add(new NavigateMenu(player, MenuInput.Back));
            }
            else if (read.Pressed(InputAction.MenuSelect))
            {
                output.Add(new NavigateMenu(player, MenuInput.Select));
            }

            if (read.Pressed(InputAction.MenuUp))
            {
                output.Add(new NavigateMenu(player, MenuInput.Up));
            }
            else if (read.Pressed(InputAction.MenuDown))
            {
                output.Add(new NavigateMenu(player, MenuInput.Down));
            }
        }

        /// <summary><c>UpdateMouse</c>, after every state's handler: one zoom step per wheel step, the editor button on press.</summary>
        static void Mouse(InputState input, InputData bindings, PlayerId player, List<ICommand> output)
        {
            if (input.Current.Wheel > 0)
            {
                output.Add(new NudgeCamera(player, CameraNudge.ZoomIn));
            }
            else if (input.Current.Wheel < 0)
            {
                output.Add(new NudgeCamera(player, CameraNudge.ZoomOut));
            }

            if (input.Pressed(bindings.MapEditorMouse))
            {
                output.Add(new ToggleMapEditor(player));
            }
        }

        /// <summary>Menu then console, on press: the first two checks of the Play and MapEditor handlers.</summary>
        static void MenuAndConsole(Reader read, PlayerId player, List<ICommand> output)
        {
            if (read.Pressed(InputAction.MainMenu))
            {
                output.Add(new ToggleMainMenu(player));
            }

            if (read.Pressed(InputAction.Console))
            {
                output.Add(new ToggleConsole(player));
            }
        }

        /// <summary>The keys Play and MapEditor share after their own: F12, U, the camera chain, C, M.</summary>
        static void Toolbox(Reader read, PlayerId player, List<ICommand> output)
        {
            if (read.Pressed(InputAction.Fullscreen))
            {
                output.Add(new ToggleFullscreen(player));
            }

            if (read.Pressed(InputAction.QuickBar))
            {
                output.Add(new ToggleQuickBar(player));
            }

            // Held, and only the first in this order acts in a step (one if / else if chain in the original).
            for (var i = 0; i < CameraKeys.Length; i++)
            {
                if (read.Down(CameraKeys[i].Action))
                {
                    output.Add(new NudgeCamera(player, CameraKeys[i].Nudge));
                    break;
                }
            }

            if (read.Pressed(InputAction.CameraMode))
            {
                output.Add(new CycleCameraMode(player));
            }

            if (read.Pressed(InputAction.MapEditor))
            {
                output.Add(new ToggleMapEditor(player));
            }
        }

        static readonly (InputAction Action, CameraNudge Nudge)[] CameraKeys =
        {
            (InputAction.ScrollLeft, CameraNudge.ScrollLeft),
            (InputAction.ScrollRight, CameraNudge.ScrollRight),
            (InputAction.ScrollUp, CameraNudge.ScrollUp),
            (InputAction.ScrollDown, CameraNudge.ScrollDown),
            (InputAction.ZoomIn, CameraNudge.ZoomIn),
            (InputAction.ZoomOut, CameraNudge.ZoomOut),
            (InputAction.RotateLeft, CameraNudge.RotateLeft),
            (InputAction.RotateRight, CameraNudge.RotateRight),
        };

        static bool Contains(IReadOnlyList<Key> keys, Key key)
        {
            for (var i = 0; i < keys.Count; i++)
            {
                if (keys[i] == key)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>An action is down if any of its controls is; pressed if any of its controls was pressed this step.</summary>
        readonly struct Reader
        {
            public readonly InputState Input;
            readonly InputData _bindings;

            public Reader(InputState input, InputData bindings)
            {
                Input = input;
                _bindings = bindings;
            }

            public bool Down(InputAction action) => KeyDown(action) || ButtonDown(action);

            public bool Pressed(InputAction action) => KeyPressed(action) || ButtonPressed(action);

            public bool KeyDown(InputAction action)
            {
                var keys = _bindings[action].Keys;
                for (var i = 0; i < keys.Count; i++)
                {
                    if (Input.Down(keys[i]))
                    {
                        return true;
                    }
                }

                return false;
            }

            public bool KeyPressed(InputAction action)
            {
                var keys = _bindings[action].Keys;
                for (var i = 0; i < keys.Count; i++)
                {
                    if (Input.Pressed(keys[i]))
                    {
                        return true;
                    }
                }

                return false;
            }

            public bool ButtonDown(InputAction action)
            {
                var buttons = _bindings[action].Buttons;
                for (var i = 0; i < buttons.Count; i++)
                {
                    if (Input.Down(buttons[i]))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool ButtonPressed(InputAction action)
            {
                var buttons = _bindings[action].Buttons;
                for (var i = 0; i < buttons.Count; i++)
                {
                    if (Input.Pressed(buttons[i]))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
