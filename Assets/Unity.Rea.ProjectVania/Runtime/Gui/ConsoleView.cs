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
    /// Draws the console as <c>Console.Draw</c> did (RECONSTRUCTION 10.1), every frame in every state: the panel, its
    /// texture (<c>console1</c>, or <c>red</c> after <c>console bg red</c>) stretched to the screen's width and the console's
    /// height at alpha 0.75, its top edge where the slide has it; then, only in the Console state, in an Arial 10
    /// equivalent, the yellow prompt at (6 or 4, height - 20), the typed line with its blinking caret at (16, height - 20)
    /// and the 100 history lines, line h at (2, height - 34 - 14 h), in the console's two colours. The text is placed
    /// on the screen, not on the panel, as the original's was. UI Toolkit elements in the pixel-perfect panel, after the
    /// main menu, as the GUI drew the console after the menu. It only reads the world.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class ConsoleView : MonoBehaviour
    {
        public const string LayerName = "console";

        GameWorld _world;
        GameConsole _console;
        string _texture;
        readonly List<Label> _history = new List<Label>();
        readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();

        public VisualElement Layer { get; private set; }
        public VisualElement Panel { get; private set; }
        public Label Prompt { get; private set; }
        public Label Line { get; private set; }

        /// <summary>One label per history line, newest first.</summary>
        public IReadOnlyList<Label> History => _history;

        public void Initialize(GameWorld world, VisualElement root)
        {
            _world = world;
            _console = world.Console;

            Layer = new VisualElement { name = LayerName, pickingMode = PickingMode.Ignore };
            Absolute(Layer, 0, 0, PixelPerfect.ReferenceWidth, PixelPerfect.ReferenceHeight);
            Layer.style.overflow = Overflow.Hidden;
            root.Add(Layer);
            if (_console == null)
            {
                Layer.style.display = DisplayStyle.None;
                return;
            }

            var data = _console.Data;

            // GuiItem("console1", g.Alpha(0.75f), (0, 0, W, 200)): the texture stretched over the panel, tinted white at 0.75.
            Panel = new VisualElement { name = "console-panel", pickingMode = PickingMode.Ignore };
            Absolute(Panel, 0, 0, PixelPerfect.ReferenceWidth, data.Height);
            Panel.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Panel.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, data.TextureAlpha);
            Layer.Add(Panel);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var fontPixels = Mathf.Round(data.FontPoints * 96f / 72f); // XNA drew a sprite font's points at 96 dpi
            for (var h = 0; h < data.HistoryLines; h++)
            {
                var line = Text($"console-history-{h}", font, fontPixels);
                _history.Add(line);
                Layer.Add(line);
            }

            Prompt = Text("console-prompt", font, fontPixels);
            Prompt.text = data.Prompt;
            Prompt.style.color = NameTags.Colour(data.PromptColour);
            Layer.Add(Prompt);

            Line = Text("console-line", font, fontPixels);
            Line.style.left = data.TextX;
            Layer.Add(Line);

            Refresh();
        }

        void LateUpdate()
        {
            Refresh();
        }

        /// <summary>Places the panel where the slide has it and writes the prompt, line and history in the Console state.</summary>
        public void Refresh()
        {
            if (_console == null)
            {
                return;
            }

            Panel.style.top = _console.Y;
            Panel.style.height = _console.Height;
            if (_texture != _console.Texture)
            {
                _texture = _console.Texture;
                Panel.style.backgroundImage = new StyleBackground(Load(_texture));
            }

            var writing = _world.State == GameState.Console;
            var display = writing ? DisplayStyle.Flex : DisplayStyle.None;
            Prompt.style.display = display;
            Line.style.display = display;
            for (var h = 0; h < _history.Count; h++)
            {
                _history[h].style.display = display;
            }

            if (!writing)
            {
                return;
            }

            Prompt.style.left = _console.PromptX;
            Prompt.style.top = _console.PromptY;
            Line.style.top = _console.TextY;
            Line.text = _console.Line;
            Line.style.color = NameTags.Colour(_console.TextColour);

            var historyColour = NameTags.Colour(_console.HistoryColour);
            for (var h = 0; h < _history.Count; h++)
            {
                var label = _history[h];
                label.text = _console.History[h];
                label.style.left = _console.Data.HistoryX;
                label.style.top = _console.HistoryY(h);
                label.style.color = historyColour;
            }
        }

        Texture2D Load(string texture)
        {
            if (!_textures.TryGetValue(texture, out var loaded))
            {
                loaded = Resources.Load<Texture2D>(MainMenuView.PathOf(texture));
                _textures[texture] = loaded;
            }

            return loaded;
        }

        static Label Text(string name, Font font, float fontPixels)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.fontSize = fontPixels;
            if (font != null)
            {
                label.style.unityFontDefinition = FontDefinition.FromFont(font);
            }

            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            label.style.unityTextAlign = TextAnchor.UpperLeft;
            label.style.whiteSpace = WhiteSpace.Pre;
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
