using System;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Input;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using CoreKey = Unity.Rea.ProjectVania.Core.Input.Key;
using UnityKey = UnityEngine.InputSystem.Key;
using MouseButton = Unity.Rea.ProjectVania.Core.Input.MouseButton;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// The keyboard, the first Xbox pad and the mouse through the Input System, read into Core's
    /// <see cref="InputFrame"/> once per 60 Hz step (RECONSTRUCTION 3). The game renders at any rate, so a press that
    /// starts and ends between two steps is latched from its input event and reported down to the next step, and
    /// wheel movement is queued as notches, one per step: nothing pressed is lost and nothing is read twice. It also drives both pad motors from the left trigger every frame, as the original did.
    /// <para>
    /// The mouse is reported on the 800 x 600 reference (pixels, y down, through the letterboxed viewport). While
    /// <see cref="Joystick"/> is set (the editor's World mode) the original warped the OS mouse back to the screen centre
    /// every frame and read its offset next frame; here the cursor is locked and the mouse's travel since the last step,
    /// in reference pixels, is reported as an offset from the centre, which is the same reading. An unfocused window
    /// reports the mouse at the centre.
    /// </para>
    /// </summary>
    public sealed class InputSystemDevice : IInputDevice, IDisposable
    {
        static readonly (UnityKey Unity, CoreKey Core)[] Keys =
        {
            (UnityKey.Backspace, CoreKey.Back), (UnityKey.Tab, CoreKey.Tab), (UnityKey.Enter, CoreKey.Enter),
            (UnityKey.NumpadEnter, CoreKey.Enter), (UnityKey.Pause, CoreKey.Pause), (UnityKey.CapsLock, CoreKey.CapsLock),
            (UnityKey.Escape, CoreKey.Escape), (UnityKey.Space, CoreKey.Space), (UnityKey.PageUp, CoreKey.PageUp),
            (UnityKey.PageDown, CoreKey.PageDown), (UnityKey.End, CoreKey.End), (UnityKey.Home, CoreKey.Home),
            (UnityKey.LeftArrow, CoreKey.Left), (UnityKey.UpArrow, CoreKey.Up), (UnityKey.RightArrow, CoreKey.Right),
            (UnityKey.DownArrow, CoreKey.Down), (UnityKey.PrintScreen, CoreKey.PrintScreen), (UnityKey.Insert, CoreKey.Insert),
            (UnityKey.Delete, CoreKey.Delete),
            (UnityKey.Digit0, CoreKey.D0), (UnityKey.Digit1, CoreKey.D1), (UnityKey.Digit2, CoreKey.D2), (UnityKey.Digit3, CoreKey.D3),
            (UnityKey.Digit4, CoreKey.D4), (UnityKey.Digit5, CoreKey.D5), (UnityKey.Digit6, CoreKey.D6), (UnityKey.Digit7, CoreKey.D7),
            (UnityKey.Digit8, CoreKey.D8), (UnityKey.Digit9, CoreKey.D9),
            (UnityKey.A, CoreKey.A), (UnityKey.B, CoreKey.B), (UnityKey.C, CoreKey.C), (UnityKey.D, CoreKey.D), (UnityKey.E, CoreKey.E),
            (UnityKey.F, CoreKey.F), (UnityKey.G, CoreKey.G), (UnityKey.H, CoreKey.H), (UnityKey.I, CoreKey.I), (UnityKey.J, CoreKey.J),
            (UnityKey.K, CoreKey.K), (UnityKey.L, CoreKey.L), (UnityKey.M, CoreKey.M), (UnityKey.N, CoreKey.N), (UnityKey.O, CoreKey.O),
            (UnityKey.P, CoreKey.P), (UnityKey.Q, CoreKey.Q), (UnityKey.R, CoreKey.R), (UnityKey.S, CoreKey.S), (UnityKey.T, CoreKey.T),
            (UnityKey.U, CoreKey.U), (UnityKey.V, CoreKey.V), (UnityKey.W, CoreKey.W), (UnityKey.X, CoreKey.X), (UnityKey.Y, CoreKey.Y),
            (UnityKey.Z, CoreKey.Z),
            (UnityKey.LeftMeta, CoreKey.LeftWindows), (UnityKey.RightMeta, CoreKey.RightWindows), (UnityKey.ContextMenu, CoreKey.Apps),
            (UnityKey.Numpad0, CoreKey.NumPad0), (UnityKey.Numpad1, CoreKey.NumPad1), (UnityKey.Numpad2, CoreKey.NumPad2),
            (UnityKey.Numpad3, CoreKey.NumPad3), (UnityKey.Numpad4, CoreKey.NumPad4), (UnityKey.Numpad5, CoreKey.NumPad5),
            (UnityKey.Numpad6, CoreKey.NumPad6), (UnityKey.Numpad7, CoreKey.NumPad7), (UnityKey.Numpad8, CoreKey.NumPad8),
            (UnityKey.Numpad9, CoreKey.NumPad9), (UnityKey.NumpadMultiply, CoreKey.Multiply), (UnityKey.NumpadPlus, CoreKey.Add),
            (UnityKey.NumpadMinus, CoreKey.Subtract), (UnityKey.NumpadPeriod, CoreKey.Decimal), (UnityKey.NumpadDivide, CoreKey.Divide),
            (UnityKey.F1, CoreKey.F1), (UnityKey.F2, CoreKey.F2), (UnityKey.F3, CoreKey.F3), (UnityKey.F4, CoreKey.F4),
            (UnityKey.F5, CoreKey.F5), (UnityKey.F6, CoreKey.F6), (UnityKey.F7, CoreKey.F7), (UnityKey.F8, CoreKey.F8),
            (UnityKey.F9, CoreKey.F9), (UnityKey.F10, CoreKey.F10), (UnityKey.F11, CoreKey.F11), (UnityKey.F12, CoreKey.F12),
            (UnityKey.NumLock, CoreKey.NumLock), (UnityKey.ScrollLock, CoreKey.Scroll),
            (UnityKey.LeftShift, CoreKey.LeftShift), (UnityKey.RightShift, CoreKey.RightShift),
            (UnityKey.LeftCtrl, CoreKey.LeftControl), (UnityKey.RightCtrl, CoreKey.RightControl),
            (UnityKey.LeftAlt, CoreKey.LeftAlt), (UnityKey.RightAlt, CoreKey.RightAlt),
            (UnityKey.Semicolon, CoreKey.OemSemicolon), (UnityKey.Equals, CoreKey.OemPlus), (UnityKey.Comma, CoreKey.OemComma),
            (UnityKey.Minus, CoreKey.OemMinus), (UnityKey.Period, CoreKey.OemPeriod), (UnityKey.Slash, CoreKey.OemQuestion),
            (UnityKey.Backquote, CoreKey.OemTilde), (UnityKey.LeftBracket, CoreKey.OemOpenBrackets),
            (UnityKey.Backslash, CoreKey.OemPipe), (UnityKey.RightBracket, CoreKey.OemCloseBrackets), (UnityKey.Quote, CoreKey.OemQuotes),
        };

        readonly float _pressPoint;
        readonly InputFrame _latched = new InputFrame();
        int _wheel;
        float _rumble;
        Gamepad _rumbling;
        Gamepad _buttonsOf;
        (ButtonControl, PadButton)[] _buttons;
        Mouse _mouseButtonsOf;
        (ButtonControl, MouseButton)[] _mouseButtons;
        UnityEngine.Vector2 _travel;

        /// <summary>Whether the mouse is read as a joystick about the screen centre (the editor's World mode).</summary>
        public bool Joystick { get; set; }

        /// <param name="stickPressPoint">How far a stick or trigger must go to count as pressed (input.json).</param>
        public InputSystemDevice(float stickPressPoint)
        {
            _pressPoint = stickPressPoint;
            InputSystem.onEvent += OnEvent;
        }

        /// <summary>
        /// Called once per rendered frame, after the Input System's update: queues the wheel (one notch per frame it
        /// moved) and drives the rumble. Presses are latched as their events arrive (<see cref="OnEvent"/>).
        /// </summary>
        public void Poll()
        {
            var pad = Gamepad.current;
            if (pad != null)
            {
                // GamePad.SetVibration(One, Triggers.Left, Triggers.Left), every frame (RECONSTRUCTION 3.1).
                var trigger = pad.leftTrigger.ReadValue();
                if (pad != _rumbling || trigger != _rumble)
                {
                    pad.SetMotorSpeeds(trigger, trigger);
                    _rumbling = pad;
                    _rumble = trigger;
                }
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                var scroll = mouse.scroll.ReadValue().y;
                _wheel += scroll > 0f ? 1 : scroll < 0f ? -1 : 0;
                _travel = Joystick ? _travel + mouse.delta.ReadValue() : UnityEngine.Vector2.zero;
            }
        }

        /// <summary>
        /// Latches every button that any state event shows pressed, so a press and release that both arrive before
        /// the next step (even within one Input System update, where the control's own state ends up released)
        /// still reaches that step as down.
        /// </summary>
        void OnEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
            {
                return;
            }

            if (device is Keyboard keyboard && keyboard == Keyboard.current)
            {
                foreach (var (unity, core) in Keys)
                {
                    if (PressedIn(keyboard[unity], eventPtr))
                    {
                        _latched.Set(core);
                    }
                }
            }
            else if (device is Gamepad pad && pad == Gamepad.current)
            {
                foreach (var (control, button) in Buttons(pad))
                {
                    if (PressedIn(control, eventPtr))
                    {
                        _latched.Set(button);
                    }
                }
            }
            else if (device is Mouse mouse && mouse == Mouse.current)
            {
                foreach (var (control, button) in MouseButtons(mouse))
                {
                    if (PressedIn(control, eventPtr))
                    {
                        _latched.Set(button);
                    }
                }
            }
        }

        static bool PressedIn(ButtonControl control, InputEventPtr eventPtr) =>
            control.ReadValueFromEvent(eventPtr, out var value) && value >= control.pressPointOrDefault;

        public void Sample(InputFrame frame)
        {
            var keyboard = Keyboard.current;
            foreach (var (unity, core) in Keys)
            {
                if (_latched.IsDown(core) || (keyboard != null && keyboard[unity].isPressed))
                {
                    frame.Set(core);
                }
            }

            var pad = Gamepad.current;
            frame.PadConnected = pad != null;
            if (pad != null)
            {
                foreach (var (control, button) in Buttons(pad))
                {
                    if (_latched.IsDown(button) || control.isPressed)
                    {
                        frame.Set(button);
                    }
                }

                Stick(frame, pad.leftStick.ReadValue(), PadButton.LeftThumbstickLeft, PadButton.LeftThumbstickRight, PadButton.LeftThumbstickUp, PadButton.LeftThumbstickDown);
                Stick(frame, pad.rightStick.ReadValue(), PadButton.RightThumbstickLeft, PadButton.RightThumbstickRight, PadButton.RightThumbstickUp, PadButton.RightThumbstickDown);
                frame.Set(PadButton.LeftTrigger, pad.leftTrigger.ReadValue() > _pressPoint);
                frame.Set(PadButton.RightTrigger, pad.rightTrigger.ReadValue() > _pressPoint);
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                foreach (var (control, button) in MouseButtons(mouse))
                {
                    if (_latched.IsDown(button) || control.isPressed)
                    {
                        frame.Set(button);
                    }
                }

                SampleMouse(frame, mouse);
            }

            // One queued notch per step, so each notch is one zoom step as in the original.
            frame.Wheel = Math.Sign(_wheel);
            _wheel -= frame.Wheel;
            _latched.Clear();
        }

        /// <summary>The mouse on the reference screen: an offset from its centre while a joystick, else where it is.</summary>
        void SampleMouse(InputFrame frame, Mouse mouse)
        {
            frame.WindowActive = UnityEngine.Application.isFocused;
            var width = UnityEngine.Screen.width;
            var height = UnityEngine.Screen.height;
            const float centreX = PixelPerfect.ReferenceWidth / 2f;
            const float centreY = PixelPerfect.ReferenceHeight / 2f;
            if (width <= 0 || height <= 0)
            {
                frame.SetMouse(centreX, centreY);
                return;
            }

            if (Joystick)
            {
                var scale = PixelPerfect.ReferenceWidth / (float)PixelPerfect.Fit(width, height).Width;
                var travel = frame.WindowActive ? _travel : UnityEngine.Vector2.zero;
                frame.SetMouse(centreX + travel.x * scale, centreY - travel.y * scale);
                _travel = UnityEngine.Vector2.zero;
                return;
            }

            var at = HudTextView.ToReference(mouse.position.ReadValue(), width, height);
            frame.SetMouse(at.x, at.y);
        }

        /// <summary>Stops the rumble the left trigger started.</summary>
        public void Dispose()
        {
            InputSystem.onEvent -= OnEvent;
            _rumbling?.ResetHaptics();
            _rumbling = null;
            _rumble = 0f;
        }

        void Stick(InputFrame frame, UnityEngine.Vector2 value, PadButton left, PadButton right, PadButton up, PadButton down)
        {
            frame.Set(left, value.x < -_pressPoint);
            frame.Set(right, value.x > _pressPoint);
            frame.Set(up, value.y > _pressPoint);
            frame.Set(down, value.y < -_pressPoint);
        }

        (ButtonControl, PadButton)[] Buttons(Gamepad pad)
        {
            if (pad != _buttonsOf)
            {
                _buttonsOf = pad;
                _buttons = PadButtons(pad);
            }

            return _buttons;
        }

        (ButtonControl, MouseButton)[] MouseButtons(Mouse mouse)
        {
            if (mouse != _mouseButtonsOf)
            {
                _mouseButtonsOf = mouse;
                _mouseButtons = MouseButtonsOf(mouse);
            }

            return _mouseButtons;
        }

        static (ButtonControl, PadButton)[] PadButtons(Gamepad pad) => new[]
        {
            (pad.buttonSouth, PadButton.A), (pad.buttonEast, PadButton.B), (pad.buttonWest, PadButton.X), (pad.buttonNorth, PadButton.Y),
            (pad.startButton, PadButton.Start), (pad.selectButton, PadButton.Back),
            (pad.leftShoulder, PadButton.LeftShoulder), (pad.rightShoulder, PadButton.RightShoulder),
            (pad.leftStickButton, PadButton.LeftStick), (pad.rightStickButton, PadButton.RightStick),
            (pad.dpad.up, PadButton.DPadUp), (pad.dpad.down, PadButton.DPadDown),
            (pad.dpad.left, PadButton.DPadLeft), (pad.dpad.right, PadButton.DPadRight),
        };

        static (ButtonControl, MouseButton)[] MouseButtonsOf(Mouse mouse) => new[]
        {
            (mouse.leftButton, MouseButton.Left), (mouse.rightButton, MouseButton.Right), (mouse.middleButton, MouseButton.Middle),
        };
    }
}
