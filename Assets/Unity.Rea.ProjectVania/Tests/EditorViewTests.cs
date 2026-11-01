using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.ProjectVania.Tests
{
    /// <summary>
    /// The map editor on screen (U.VAN-US-A14, RECONSTRUCTION 8, 12): the World cursor's hand on the view's centre between
    /// the cells and the characters, tinted by the buttons; the tile palette with its selector and the Screen cursor in the
    /// GUI while the game is in the editor; the GUI's hand over the main menu; and a painted cell repainted on its own.
    /// </summary>
    public class EditorViewTests
    {
        static string SettingsFolder => Path.Combine(Application.dataPath, "StreamingAssets", GameSettings.Folder);
        static readonly PlayerId P1 = PlayerId.One;

        GameHost _host;
        EditorCursorView _cursor;
        EditorGuiView _gui;
        MapView _map;

        [SetUp]
        public void SetUp()
        {
            _host = Bootstrap.Run(new DirectorySettingsSource(SettingsFolder));
            Assert.That(_host, Is.Not.Null);
            _cursor = _host.GetComponentInChildren<EditorCursorView>();
            _gui = _host.GetComponentInChildren<EditorGuiView>();
            _map = _host.GetComponentInChildren<MapView>();
            Assert.That(_cursor, Is.Not.Null, "the bootstrap builds the editor's world cursor");
            Assert.That(_gui, Is.Not.Null, "the bootstrap builds the editor's GUI");
        }

        [TearDown]
        public void TearDown()
        {
            var root = GameObject.Find(Bootstrap.RootName);
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        void Step(params ICommand[] commands)
        {
            foreach (var command in commands)
            {
                _host.World.Submit(command);
            }

            _host.World.Advance(1.0 / 60);
            _host.Present(_host.World.Events);
            _cursor.Present(1f);
            _gui.Refresh();
            _map.Present();
        }

        /// <summary>One editor step with the mouse at (x, y) and those buttons (Space held for <paramref name="screen"/>).</summary>
        void Edit(float x, float y, bool left = false, bool right = false, bool screen = false) => Step(
            new SetEditorCursor(P1, screen ? EditorCursorMode.Screen : EditorCursorMode.World),
            new PointEditor(P1, x, y, left, right));

        void OpenEditor()
        {
            for (var i = 0; i < 180; i++)
            {
                Step();
            }

            Step(new ToggleMapEditor(P1));
            Edit(400f, 300f);
        }

        static (float, float) At(VisualElement e) => (e.style.left.value.value, e.style.top.value.value);

        static (float, float) Size(VisualElement e) => (e.style.width.value.value, e.style.height.value.value);

        static bool Shown(VisualElement e) => e.style.display.value == DisplayStyle.Flex;

        [Test]
        public void NothingOfTheEditorShowsInPlay()
        {
            Step();
            Assert.That(_cursor.Renderer.enabled, Is.False);
            Assert.That(Shown(_gui.Palette), Is.False);
            Assert.That(Shown(_gui.ScreenCursor), Is.False);
            Assert.That(Shown(_gui.MenuCursor), Is.False);
        }

        [Test]
        public void TheWorldCursorIsTheHandOnTheViewsCentreBetweenTheCellsAndTheCharacters()
        {
            OpenEditor();
            var renderer = _cursor.Renderer;
            Assert.That(renderer.enabled, Is.True);
            Assert.That(renderer.sprite.texture.name, Is.EqualTo("cursor_hand_topleft"));
            Assert.That((renderer.sprite.texture.width, renderer.sprite.texture.height), Is.EqualTo((13, 18)));
            Assert.That(renderer.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(renderer.sortingOrder, Is.GreaterThan(MapView.CellsOrder));
            Assert.That(renderer.sortingOrder, Is.LessThan(_host.GetComponentInChildren<CharacterView>().GetComponent<SpriteRenderer>().sortingOrder));

            var camera = _host.World.Camera(P1).Position;
            var editor = _host.World.Editor(P1);
            Assert.That((editor.Cursor.X, editor.Cursor.Y), Is.EqualTo((-camera.X + 6f, -camera.Y + 9f)));
            var position = _cursor.transform.position;
            Assert.That(position.x, Is.EqualTo(editor.Cursor.X / 16f).Within(1e-4f), "its top-left on the cursor");
            Assert.That(position.y, Is.EqualTo(-editor.Cursor.Y / 16f).Within(1e-4f));
            Assert.That((Color32)renderer.color, Is.EqualTo(new Color32(255, 255, 255, 255)));

            Edit(400f, 300f, left: true);
            Assert.That((Color32)renderer.color, Is.EqualTo(new Color32(255, 255, 0, 255)), "yellow while left is held");
            Edit(400f, 300f, right: true);
            Assert.That((Color32)renderer.color, Is.EqualTo(new Color32(255, 0, 0, 255)), "red while right is held");
        }

        [Test]
        public void ThePaletteIsDrawnInTheEditorWithTheSelectorOnTheSelectedTile()
        {
            OpenEditor();
            Assert.That(Shown(_gui.Palette), Is.True);
            Assert.That(At(_gui.PaletteBackground), Is.EqualTo((5f, 295f)));
            Assert.That(Size(_gui.PaletteBackground), Is.EqualTo((300f, 295f)));
            var background = _gui.PaletteBackground.style.backgroundImage.value.texture;
            Assert.That(background.name, Is.EqualTo("tilepalette_background"));
            Assert.That(background.filterMode, Is.EqualTo(FilterMode.Bilinear), "stretched from 300 x 300");
            Assert.That(_gui.PaletteBackground.style.unityBackgroundImageTintColor.value.a, Is.EqualTo(0.4f).Within(1e-4f));

            Assert.That(At(_gui.Tiles), Is.EqualTo((16f, 300f)));
            Assert.That(Size(_gui.Tiles), Is.EqualTo((288f, 160f)), "18 x 10 whole tiles, each drawn once (BIBLE defect 9)");
            var sheet = _gui.Tiles.Children().Single();
            Assert.That(sheet.style.backgroundImage.value.texture.name, Is.EqualTo("tileset_default"));
            Assert.That(sheet.style.unityBackgroundImageTintColor.value.a, Is.EqualTo(128f / 255f).Within(1e-4f));

            Assert.That(At(_gui.Selector), Is.EqualTo((16f, 300f)));
            Assert.That(Size(_gui.Selector), Is.EqualTo((16f, 16f)));
            Step(new StepEditor(P1, 1, 0, true, false), new StepEditor(P1, 0, 1, true, false));
            Assert.That(At(_gui.Selector), Is.EqualTo((32f, 316f)));

            // The palette is drawn after the menu and before the console, as GuiManager.Draw did.
            var root = _gui.Layer.parent;
            var layers = root.Children().Select(c => c.name).ToList();
            Assert.That(layers.IndexOf(EditorGuiView.LayerName), Is.GreaterThan(layers.IndexOf(MainMenuView.LayerName)));
            Assert.That(layers.IndexOf(EditorGuiView.LayerName), Is.LessThan(layers.IndexOf(ConsoleView.LayerName)));
        }

        [Test]
        public void SpaceShowsTheScreenCursorAndTheTileItPicks()
        {
            OpenEditor();
            Edit(16f + 16 * 4, 300f + 16 * 2, screen: true);
            Assert.That(_cursor.Renderer.enabled, Is.False, "the World cursor is not drawn in Screen mode");
            Assert.That(Shown(_gui.ScreenCursor), Is.True);
            Assert.That(At(_gui.ScreenCursor), Is.EqualTo((80f, 332f)));
            Assert.That(Size(_gui.ScreenCursor), Is.EqualTo((13f, 18f)));
            Assert.That(At(_gui.Selector), Is.EqualTo((16f + 64f, 300f + 32f)));
            Edit(400f, 300f);
            Assert.That(Shown(_gui.ScreenCursor), Is.False);
            Assert.That(_cursor.Renderer.enabled, Is.True);
        }

        [Test]
        public void TheMenuShowsTheGuisHandAtTheMouseKeptOnTheScreen()
        {
            Step(new ToggleMainMenu(P1));
            _gui.Mouse = new Vector2(900f, 250f);
            _gui.LeftHeld = true;
            _gui.Refresh();
            Assert.That(Shown(_gui.MenuCursor), Is.True);
            Assert.That(Shown(_gui.Palette), Is.False);
            Assert.That(At(_gui.MenuCursor), Is.EqualTo((787f, 250f)));
            Assert.That((Color32)_gui.MenuCursor.style.unityBackgroundImageTintColor.value, Is.EqualTo(new Color32(255, 255, 0, 255)));
        }

        [Test]
        public void APaintedCellIsRepaintedOnItsOwn()
        {
            OpenEditor();
            var repaints = _map.Repaints;
            var cells = _map.CellRepaints;
            Step(new StepEditor(P1, 1, 0, true, false));
            Edit(400f, 300f, left: true);
            Assert.That(_map.Repaints, Is.EqualTo(repaints), "no whole repaint for one cell");
            Assert.That(_map.CellRepaints, Is.GreaterThan(cells));

            var at = _host.World.Editor(P1).SelectedCell;
            var cell = _host.World.Map[at.X, at.Y];
            Assert.That((cell.State, cell.Tile), Is.EqualTo((CellState.Hard, new TileRect(16, 0, 16, 16))));
            var texture = _map.Cells.sprite.texture;
            var canvas = _map.CellCanvas;
            for (var v = 0; v < 16; v += 5)
            {
                for (var u = 0; u < 16; u += 5)
                {
                    var x = at.X * 16 + u;
                    var y = at.Y * 16 + v;
                    var expected = canvas[x, y];
                    var actual = texture.GetPixel(x, canvas.Height - 1 - y);
                    Assert.That((Color32)actual, Is.EqualTo(new Color32(expected.R, expected.G, expected.B, expected.A)), $"pixel ({x}, {y})");
                }
            }

            var whole = MapDrawing.CellCanvas(_host.World.Map);
            MapDrawing.PaintCells(_host.World.Map, _host.World.State, MapView.Download(Resources.Load<Texture2D>(MapView.PathOf("tileset_default"))), whole, _map.Effect);
            Assert.That(canvas.Pixels, Is.EqualTo(whole.Pixels), "the same picture as a whole repaint");
        }

        [Test]
        public void TheHostLocksTheMouseToAJoystickOnlyInTheWorldMode()
        {
            Step();
            _host.Tick(0.0);
            Assert.That(_host.Device.Joystick, Is.False);
            OpenEditor();
            _host.Tick(0.0);
            Assert.That(_host.Device.Joystick, Is.True);
            Edit(10f, 10f, screen: true);
            _host.Tick(0.0);
            Assert.That(_host.Device.Joystick, Is.False, "Space puts the mouse back on the screen");
        }
    }
}
