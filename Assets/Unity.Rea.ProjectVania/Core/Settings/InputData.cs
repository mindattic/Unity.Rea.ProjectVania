using System;
using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Input;

namespace Unity.Rea.ProjectVania.Core.Settings
{
    /// <summary>
    /// Every bindable action of the original's InputManager (RECONSTRUCTION 3.1, 3.2, 9.2, 10.2). Which controls
    /// trigger each one is data (input.json); how each is read (on press, held, in which game state, in which order)
    /// is the original's and lives in <see cref="InputRouter"/>.
    /// </summary>
    public enum InputAction
    {
        MainMenu,
        Console,
        MoveLeft,
        MoveRight,
        Kneel,
        Jump,
        Reset,
        CycleLeft,
        CycleRight,
        Fullscreen,
        QuickBar,
        CameraMode,
        MapEditor,
        ScrollLeft,
        ScrollRight,
        ScrollUp,
        ScrollDown,
        ZoomIn,
        ZoomOut,
        RotateLeft,
        RotateRight,
        EditorScreenCursor,
        EditorLeft,
        EditorRight,
        EditorUp,
        EditorDown,
        Ctrl,
        Shift,
        MenuBack,
        MenuSelect,
        MenuUp,
        MenuDown,
        ConsoleSubmit,
        ConsoleClear,
        ConsoleBackspace,
        ConsoleRecall,
        ConsoleSpace,
    }

    /// <summary>The keys and pad buttons bound to one action.</summary>
    public sealed class Binding
    {
        public Binding(IReadOnlyList<Key> keys, IReadOnlyList<PadButton> buttons)
        {
            Keys = keys;
            Buttons = buttons;
        }

        public IReadOnlyList<Key> Keys { get; }
        public IReadOnlyList<PadButton> Buttons { get; }
    }

    /// <summary>One entry of the console's typing chain: a key (with or without Shift) and the text it appends.</summary>
    public sealed class TypedKey
    {
        public Key Key { get; private set; }
        public bool Shift { get; private set; }
        public string Text { get; private set; }

        internal static TypedKey Read(SettingsReader r) => new TypedKey
        {
            Key = r.Choice<Key>("key"),
            Shift = r.Flag("shift"),
            Text = r.Text("text"),
        };
    }

    /// <summary>input.json: the original's bindings (InputManager.SetupKeys/SetupButtons and its literal keys) as data.</summary>
    public sealed class InputData
    {
        readonly Dictionary<InputAction, Binding> _bindings = new Dictionary<InputAction, Binding>();

        /// <summary>How far a stick or trigger must go to count as a pressed button (0..1).</summary>
        public float StickPressPoint { get; private set; }

        /// <summary>The mouse button that toggles the map editor in every state.</summary>
        public MouseButton MapEditorMouse { get; private set; }

        /// <summary>The console's typing chain, in the original's order: the first entry that matches wins.</summary>
        public IReadOnlyList<TypedKey> ConsoleTyping { get; private set; }

        /// <summary>Keys the console's fallback (append the key's name) skips.</summary>
        public IReadOnlyList<Key> ConsoleIgnores { get; private set; }

        public Binding this[InputAction action] => _bindings[action];

        internal static InputData Read(SettingsReader r)
        {
            var data = new InputData
            {
                StickPressPoint = r.Number("stickPressPoint", 0.01f, 1f),
                MapEditorMouse = r.Choice<MouseButton>("mapEditorMouse"),
            };

            r.Section("bindings", s =>
            {
                foreach (InputAction action in Enum.GetValues(typeof(InputAction)))
                {
                    var name = char.ToLowerInvariant(action.ToString()[0]) + action.ToString().Substring(1);
                    var binding = s.Section(name, b => new Binding(b.Choices<Key>("keys"), b.Choices<PadButton>("buttons")));
                    s.Check(binding.Keys.Count + binding.Buttons.Count > 0, name, "must bind at least one key or button");
                    data._bindings[action] = binding;
                }

                return 0;
            });

            data.ConsoleTyping = r.Each("consoleTyping", TypedKey.Read);
            data.ConsoleIgnores = r.Choices<Key>("consoleIgnores");
            return data;
        }
    }
}
