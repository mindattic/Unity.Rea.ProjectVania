using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// Draws the main menu as <c>MainMenu.Draw</c> did (RECONSTRUCTION 9.2), only while the game is in the MainMenu
    /// state: <c>menu1</c> stretched over the whole 800 x 600 screen at alpha 0.8 (which greys out the frozen world
    /// behind it), the 16 x 16 <c>arrow1</c> 18 px left of the highlighted item, the shown level's items in Lucida
    /// Console 14 at (50, 100 + 50 * index), yellow or red when highlighted, then the level's title at (10, 15) and the
    /// highlighted item's description at (10, H - 20), both white, every figure menu.json's. UI Toolkit elements in the
    /// pixel-perfect panel, over the world and its names, as the GUI drew after the scene. It only reads the world.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public sealed class MainMenuView : MonoBehaviour
    {
        public const string LayerName = "main-menu";
        public const string ResourceFolder = "Gui";

        GameWorld _world;
        MenuData _data;
        readonly List<Label> _items = new List<Label>();

        public VisualElement Layer { get; private set; }
        public VisualElement Background { get; private set; }
        public VisualElement Arrow { get; private set; }
        public Label Title { get; private set; }
        public Label Description { get; private set; }

        /// <summary>One label per item slot (the most items any level has); slots past the shown level's are hidden.</summary>
        public IReadOnlyList<Label> Items => _items;

        /// <summary>Whether the original's sprite font was found on this machine (else Unity's built-in font stands in).</summary>
        public bool FontFound { get; private set; }

        public static string PathOf(string asset) => $"{ResourceFolder}/{asset}";

        public void Initialize(GameWorld world, MenuData data, VisualElement root)
        {
            _world = world;
            _data = data;

            Layer = new VisualElement { name = LayerName, pickingMode = PickingMode.Ignore };
            Absolute(Layer, 0, 0, PixelPerfect.ReferenceWidth, PixelPerfect.ReferenceHeight);
            root.Add(Layer);

            // GuiItem("menu1", g.Alpha(0.8f), (0, 0, W, H)): the whole texture stretched over the screen, tinted white at 0.8.
            Background = new VisualElement { name = "menu-background", pickingMode = PickingMode.Ignore };
            Absolute(Background, 0, 0, PixelPerfect.ReferenceWidth, PixelPerfect.ReferenceHeight);
            Background.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>(PathOf(data.Background)));
            Background.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Background.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, data.BackgroundAlpha);
            Layer.Add(Background);

            var arrow = Resources.Load<Texture2D>(PathOf(data.Arrow));
            Arrow = new VisualElement { name = "menu-arrow", pickingMode = PickingMode.Ignore };
            Absolute(Arrow, 0, 0, arrow != null ? arrow.width : 16, arrow != null ? arrow.height : 16);
            Arrow.style.backgroundImage = new StyleBackground(arrow);
            Arrow.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Layer.Add(Arrow);

            var font = Font(data);
            var slots = 0;
            foreach (var level in data.Levels)
            {
                slots = Mathf.Max(slots, level.Items.Count);
            }

            for (var i = 0; i < slots; i++)
            {
                var item = Text($"menu-item-{i}", font);
                _items.Add(item);
                Layer.Add(item);
            }

            Title = Text("menu-title", font);
            Title.style.left = data.TitlePosition.X;
            Title.style.top = data.TitlePosition.Y;
            Title.style.color = NameTags.Colour(data.TextColour);
            Layer.Add(Title);

            Description = Text("menu-description", font);
            Description.style.left = data.TitlePosition.X;
            Description.style.top = PixelPerfect.ReferenceHeight - data.DescriptionFromBottom;
            Description.style.color = NameTags.Colour(data.TextColour);
            Layer.Add(Description);

            Refresh();
        }

        void LateUpdate()
        {
            Refresh();
        }

        /// <summary>Shows the menu in the MainMenu state with the world's current level and highlight, and hides it otherwise.</summary>
        public void Refresh()
        {
            if (_world == null)
            {
                return;
            }

            var menu = _world.Menu;
            var open = _world.State == GameState.MainMenu && menu != null;
            Layer.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open)
            {
                return;
            }

            var items = menu.CurrentLevel.Items;
            for (var i = 0; i < _items.Count; i++)
            {
                var label = _items[i];
                if (i >= items.Count)
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                var at = menu.ItemPosition(i);
                label.style.display = DisplayStyle.Flex;
                label.text = items[i].Text;
                label.style.left = at.X;
                label.style.top = at.Y;
                label.style.color = NameTags.Colour(i == menu.Item ? _data.SelectedColour : _data.ItemColour);
            }

            var arrow = menu.ArrowPosition;
            Arrow.style.left = arrow.X;
            Arrow.style.top = arrow.Y;
            Title.text = menu.Title;
            Description.text = menu.Description;
        }

        Label Text(string name, FontDefinition? font)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            // XNA rendered a sprite font's point size at 96 dpi.
            label.style.fontSize = Mathf.Round(_data.FontPoints * 96f / 72f);
            if (font.HasValue)
            {
                label.style.unityFontDefinition = font.Value;
            }

            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            label.style.unityTextAlign = TextAnchor.UpperLeft;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            return label;
        }

        /// <summary>
        /// The menu's sprite font, Lucida Console, taken from the machine's installed fonts (it ships with Windows, and a
        /// Microsoft font is not ours to redistribute); Unity's built-in font stands in where it is missing.
        /// </summary>
        FontDefinition? Font(MenuData data)
        {
            try
            {
                var asset = FontAsset.CreateFontAsset(data.Font, "Regular");
                if (asset != null)
                {
                    FontFound = true;
                    return FontDefinition.FromSDFFont(asset);
                }
            }
            catch (System.Exception)
            {
                // No such font here (or no font engine in a headless run): fall back below.
            }

            var builtin = Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
            return builtin != null ? FontDefinition.FromFont(builtin) : (FontDefinition?)null;
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
