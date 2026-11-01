using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Editing;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// The editor's and the menu's GUI (<c>GuiManager.Draw</c>'s state branch, RECONSTRUCTION 8, 12.2), drawn after the
    /// main menu and before the console. In the editor: the tile palette (<c>TilePalette.Draw</c>: its background at 0.4
    /// stretched over (5, H - 305, 300, 295), the tileset's tiles each once at its place at half alpha, BIBLE defect 9 fixed,
    /// and the selector over the selected tile), then the Screen cursor while Space is held. In the main menu: the GUI's
    /// hand cursor at the mouse, kept on the screen. Each cursor is the 13 x 18 hand tinted by the buttons held. UI Toolkit
    /// elements in the pixel-perfect panel; every figure is editor.json's; it only reads the world.
    /// </summary>
    [DefaultExecutionOrder(115)]
    public sealed class EditorGuiView : MonoBehaviour
    {
        public const string LayerName = "editor-gui";

        GameWorld _world;
        PlayerId _player;
        EditorData _data;

        public VisualElement Layer { get; private set; }

        /// <summary>The palette: background, tiles and selector, shown only in the editor.</summary>
        public VisualElement Palette { get; private set; }

        public VisualElement PaletteBackground { get; private set; }

        /// <summary>The window onto the tileset that shows its whole tiles (18 x 10 of 16 px) at the palette's offset.</summary>
        public VisualElement Tiles { get; private set; }

        public VisualElement Selector { get; private set; }

        /// <summary>The editor's Screen cursor (Space held).</summary>
        public VisualElement ScreenCursor { get; private set; }

        /// <summary>The GUI's cursor over the main menu.</summary>
        public VisualElement MenuCursor { get; private set; }

        /// <summary>Where the mouse is on the reference screen, and which buttons are held; read from the mouse each frame.</summary>
        public Vector2 Mouse { get; set; }

        public bool LeftHeld { get; set; }

        public bool RightHeld { get; set; }

        public void Initialize(GameWorld world, PlayerId player, EditorData data, string tilesetAsset, VisualElement root)
        {
            _world = world;
            _player = player;
            _data = data;
            Layer = new VisualElement { name = LayerName, pickingMode = PickingMode.Ignore };
            Absolute(Layer, 0, 0, PixelPerfect.ReferenceWidth, PixelPerfect.ReferenceHeight);
            Layer.style.overflow = Overflow.Hidden;
            root.Add(Layer);

            var palette = data.Palette;
            Palette = Element("editor-palette", Layer, 0, 0, PixelPerfect.ReferenceWidth, PixelPerfect.ReferenceHeight);
            PaletteBackground = Element(palette.Background, Palette, palette.BackgroundX, PixelPerfect.ReferenceHeight - palette.BackgroundFromBottom,
                palette.BackgroundSize.X, palette.BackgroundSize.Y);
            Picture(PaletteBackground, MainMenuView.PathOf(palette.Background), new Color(1f, 1f, 1f, palette.BackgroundAlpha));

            // Tileset.Draw meant each tile at Offset + its place, at Tile.Color; drawn once each, the whole tiles of the
            // sheet are the sheet cropped to whole tiles.
            var across = (int)palette.TilesetSize.X / data.TileSize;
            var down = (int)palette.TilesetSize.Y / data.TileSize;
            Tiles = Element("editor-palette-tiles", Palette, palette.Offset.X, palette.Offset.Y, across * data.TileSize, down * data.TileSize);
            Tiles.style.overflow = Overflow.Hidden;
            var sheet = Element(tilesetAsset, Tiles, 0, 0, palette.TilesetSize.X, palette.TilesetSize.Y);
            Picture(sheet, MapView.PathOf(tilesetAsset), new Color(1f, 1f, 1f, palette.TileAlpha / 255f));

            Selector = Element(palette.Selector, Palette, palette.Offset.X, palette.Offset.Y, palette.SelectorSize.X, palette.SelectorSize.Y);
            Picture(Selector, MainMenuView.PathOf(palette.Selector), Color.white);

            ScreenCursor = Cursor("editor-screen-cursor");
            MenuCursor = Cursor("menu-cursor");
            Refresh();
        }

        void LateUpdate()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && Screen.width > 0 && Screen.height > 0)
            {
                Mouse = HudTextView.ToReference(mouse.position.ReadValue(), Screen.width, Screen.height);
                LeftHeld = mouse.leftButton.isPressed;
                RightHeld = mouse.rightButton.isPressed;
            }

            Refresh();
        }

        /// <summary>Shows the palette and cursors the state calls for, where the world has them.</summary>
        public void Refresh()
        {
            if (_world == null)
            {
                return;
            }

            var editor = _world.Editor(_player);
            var inEditor = _world.State == GameState.MapEditor;
            Palette.style.display = inEditor ? DisplayStyle.Flex : DisplayStyle.None;
            if (editor != null)
            {
                var tile = editor.SelectedTile;
                Selector.style.left = _data.Palette.Offset.X + tile.X * _data.TileSize;
                Selector.style.top = _data.Palette.Offset.Y + tile.Y * _data.TileSize;
            }

            var screen = inEditor && editor != null && editor.Mode == EditorCursorMode.Screen;
            ScreenCursor.style.display = screen ? DisplayStyle.Flex : DisplayStyle.None;
            if (screen)
            {
                Place(ScreenCursor, editor.Cursor.X, editor.Cursor.Y, editor.Colour);
            }

            var menu = _world.State == GameState.MainMenu;
            MenuCursor.style.display = menu ? DisplayStyle.Flex : DisplayStyle.None;
            if (menu)
            {
                var at = MapEditor.KeepOnScreen(Mouse.x, Mouse.y, _data.Cursor.Size);
                var cursor = _data.Cursor;
                Place(MenuCursor, at.X, at.Y, (LeftHeld ? cursor.LeftColour : RightHeld ? cursor.RightColour : cursor.Colour).ToRgba());
            }
        }

        VisualElement Cursor(string name)
        {
            var element = Element(name, Layer, 0, 0, _data.Cursor.Size.X, _data.Cursor.Size.Y);
            Picture(element, MainMenuView.PathOf(_data.Cursor.Asset), Color.white);
            return element;
        }

        static void Place(VisualElement cursor, float x, float y, Rgba colour)
        {
            cursor.style.left = x;
            cursor.style.top = y;
            cursor.style.unityBackgroundImageTintColor = (Color)MapView.ToColor(colour);
        }

        static VisualElement Element(string name, VisualElement parent, float left, float top, float width, float height)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            Absolute(element, left, top, width, height);
            parent.Add(element);
            return element;
        }

        /// <summary>One <c>spriteBatch.Draw(texture, rectangle, colour)</c>: the picture stretched over the element, tinted.</summary>
        static void Picture(VisualElement element, string path, Color tint)
        {
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogError($"[ProjectVania] The editor picture {path} is missing.");
                return;
            }

            element.style.backgroundImage = new StyleBackground(texture);
            element.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            element.style.unityBackgroundImageTintColor = tint;
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
