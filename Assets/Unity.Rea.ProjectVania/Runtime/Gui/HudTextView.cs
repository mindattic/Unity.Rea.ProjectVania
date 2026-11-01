using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Gui;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// Writes the HUD's text (RECONSTRUCTION 9.1) last, as <c>GuiManager.Draw</c> did after the console, every frame in
    /// every state, in an Arial 10 equivalent: the debug readout (the driven character, the camera, the GUI cursor written
    /// twice, the editor's selected cell, the frame rate) in white at the original's places, then the message stack, each
    /// message at its place in its colour and fading alpha once it has fallen below H / 2 + 16. The text is
    /// <see cref="HudReadout"/>'s; the GUI cursor is the mouse on the 800 x 600 reference kept inside the screen as the
    /// original's Screen-mode cursor (the cursor itself is drawn by the editor's story). It only reads the world.
    /// </summary>
    [DefaultExecutionOrder(130)]
    public sealed class HudTextView : MonoBehaviour
    {
        public const string LayerName = "hud-text";

        GameWorld _world;
        PlayerId _player;
        HudData _data;
        Font _font;
        float _fontPixels;
        readonly List<Label> _cursor = new List<Label>();
        readonly List<Label> _messages = new List<Label>();

        public VisualElement Layer { get; private set; }
        public Label PlayerReadout { get; private set; }
        public Label CameraReadout { get; private set; }
        public Label CellReadout { get; private set; }
        public Label FrameRateReadout { get; private set; }

        /// <summary>The GUI cursor's position, written twice at the same place as the original's two identical writes.</summary>
        public IReadOnlyList<Label> CursorReadout => _cursor;

        /// <summary>One label per queued message, front first; a message not yet below H / 2 + 16 is hidden.</summary>
        public IReadOnlyList<Label> Messages => _messages;

        /// <summary>The frame rate written (frames per second): the real one each frame, or the original's 59.99988 (hud.json's realFrameRate).</summary>
        public float FrameRate { get; set; } = HudReadout.OriginalFrameRate;

        /// <summary>Where the GUI cursor is on the reference screen, before it is kept inside the screen; the mouse each frame.</summary>
        public Vector2 Mouse { get; set; }

        public void Initialize(GameWorld world, PlayerId player, VisualElement root)
        {
            _world = world;
            _player = player;
            Layer = new VisualElement { name = LayerName, pickingMode = PickingMode.Ignore };
            Absolute(Layer, 0, 0, PixelPerfect.ReferenceWidth, PixelPerfect.ReferenceHeight);
            Layer.style.overflow = Overflow.Hidden;
            root.Add(Layer);
            if (world.Hud == null)
            {
                Layer.style.display = DisplayStyle.None;
                return;
            }

            _data = world.Hud.Data;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _fontPixels = Mathf.Round(_data.FontPoints * 96f / 72f); // XNA drew a sprite font's points at 96 dpi
            var readout = _data.Readout;
            PlayerReadout = Text("hud-player", readout.Player.X, readout.Player.Y);
            CameraReadout = Text("hud-camera", readout.Camera.X, readout.Camera.Y);
            for (var i = 0; i < readout.CursorWrites; i++)
            {
                _cursor.Add(Text($"hud-cursor-{i}", readout.Cursor.X, readout.Cursor.Y));
            }

            CellReadout = Text("hud-cell", readout.Cell.X, readout.Cell.Y);
            FrameRateReadout = Text("hud-fps", _data.Screen.X - readout.FpsFromRight, readout.FpsY);
            Refresh();
        }

        void LateUpdate()
        {
            if (_data == null)
            {
                return;
            }

            if (_data.Readout.RealFrameRate && Time.unscaledDeltaTime > 0f)
            {
                FrameRate = 1f / Time.unscaledDeltaTime;
            }

            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && Screen.width > 0 && Screen.height > 0)
            {
                Mouse = ToReference(mouse.position.ReadValue(), Screen.width, Screen.height);
            }

            Refresh();
        }

        /// <summary>
        /// A window position (pixels, origin bottom-left) on the reference screen (whole pixels, origin top-left), through
        /// the viewport <see cref="PixelPerfect.Fit"/> gives the game, as XNA reported the mouse in back-buffer pixels.
        /// </summary>
        public static Vector2 ToReference(Vector2 window, int screenWidth, int screenHeight)
        {
            var viewport = PixelPerfect.Fit(screenWidth, screenHeight);
            var x = (window.x - viewport.X) * PixelPerfect.ReferenceWidth / viewport.Width;
            var y = (viewport.Y + viewport.Height - window.y) * PixelPerfect.ReferenceHeight / viewport.Height;
            return new Vector2(Mathf.Floor(x), Mathf.Floor(y));
        }

        /// <summary>Writes the readout and places every message.</summary>
        public void Refresh()
        {
            var hud = _world?.Hud;
            if (hud == null)
            {
                return;
            }

            PlayerReadout.text = HudReadout.Player(_world, _player);
            CameraReadout.text = HudReadout.Camera(_world, _player, _data.Readout);
            var cursor = HudReadout.ClampCursor(Mouse.x, Mouse.y, _data);
            var cursorText = HudReadout.Cursor(cursor.X, cursor.Y);
            foreach (var label in _cursor)
            {
                label.text = cursorText;
            }

            CellReadout.text = HudReadout.Cell(_world, _player, _data.Readout);
            FrameRateReadout.text = HudReadout.FrameRate(FrameRate);

            var stack = hud.Messages;
            var messages = stack.Messages;
            while (_messages.Count < messages.Count)
            {
                _messages.Add(Text($"hud-message-{_messages.Count}", 0, 0));
            }

            for (var m = 0; m < _messages.Count; m++)
            {
                var label = _messages[m];
                if (m >= messages.Count || !stack.IsShown(messages[m]))
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                var message = messages[m];
                label.style.display = DisplayStyle.Flex;
                label.text = message.Text;
                label.style.left = message.X;
                label.style.top = message.Y;
                label.style.color = NameTags.Colour(message.Colour);
            }
        }

        Label Text(string name, float left, float top)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.left = left;
            label.style.top = top;
            label.style.fontSize = _fontPixels;
            if (_font != null)
            {
                label.style.unityFontDefinition = FontDefinition.FromFont(_font);
            }

            label.style.color = NameTags.Colour(_data.Readout.Colour);
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            label.style.unityTextAlign = TextAnchor.UpperLeft;
            label.style.whiteSpace = WhiteSpace.Pre;
            Layer.Add(label);
            return label;
        }

        static void Absolute(VisualElement element, float left, float top, float width, float height)
        {
            element.style.position = Position.Absolute;
            element.style.left = left;
            element.style.top = top;
            element.style.width = width;
            element.style.height = height;
        }
    }
}
