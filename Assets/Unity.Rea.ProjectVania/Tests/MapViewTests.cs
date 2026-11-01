using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.World;
using UnityEngine;

namespace Unity.Rea.ProjectVania.Tests
{
    /// <summary>
    /// The map on screen (U.VAN-US-A8, RECONSTRUCTION 5, 6): the CornflowerBlue clear colour, the white outline, the
    /// area_1 backdrop whole at its original size, every Hard cell from the tileset at its source rectangle, every cell
    /// while the editor is open, all in the original's order (outline, layers, cells) under the characters.
    /// </summary>
    public class MapViewTests
    {
        static string SettingsFolder => Path.Combine(Application.dataPath, "StreamingAssets", GameSettings.Folder);

        GameHost _host;
        MapView _view;

        [SetUp]
        public void SetUp()
        {
            _host = Bootstrap.Run(new DirectorySettingsSource(SettingsFolder));
            Assert.That(_host, Is.Not.Null);
            _view = _host.GetComponentInChildren<MapView>();
            Assert.That(_view, Is.Not.Null);
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

        [Test]
        public void TheGameCameraClearsToCornflowerBlue()
        {
            var camera = _host.GetComponentsInChildren<Camera>().Single(c => c.name == "Camera");
            Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That((Color32)camera.backgroundColor, Is.EqualTo(new Color32(100, 149, 237, 255)));
        }

        [Test]
        public void TheBackdropIsArea1WholeAndUnscaledAtTheMapsTopLeft()
        {
            Assert.That(_view.Layers.Count, Is.EqualTo(1));
            var layer = _view.Layers[0];
            var sprite = layer.sprite;
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.texture.width, Is.EqualTo(1920), "imported at its original size, not scaled to a power of two");
            Assert.That(sprite.texture.height, Is.EqualTo(1200));
            Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, 1920, 1200)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16f));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(0, 1200)), "top-left, as SpriteBatch.Draw placed it");
            Assert.That(layer.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(layer.transform.lossyScale, Is.EqualTo(Vector3.one));
            Assert.That(layer.bounds.size.x, Is.EqualTo(120f).Within(1e-4f), "1920 px at 16 px per unit");
            Assert.That(layer.bounds.max.y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(layer.color, Is.EqualTo(Color.white));
        }

        [Test]
        public void TheOutlineIsAOnePixelWhiteLineJustOutsideTheMap()
        {
            Assert.That(_view.Outline.Count, Is.EqualTo(4));
            var expected = new[]
            {
                new Rect(-1, 0, 1, 1200),
                new Rect(0, 1200, 1900, 1),
                new Rect(1900, 0, 1, 1200),
                new Rect(0, -1, 1900, 1),
            };

            for (var i = 0; i < 4; i++)
            {
                var strip = _view.Outline[i];
                Assert.That(strip.color, Is.EqualTo(Color.white));
                var b = strip.bounds;
                // Back to the original's pixels, y down.
                var px = new Rect(b.min.x * 16f, -b.max.y * 16f, b.size.x * 16f, b.size.y * 16f);
                Assert.That(px.x, Is.EqualTo(expected[i].x).Within(1e-3f), $"strip {i}");
                Assert.That(px.y, Is.EqualTo(expected[i].y).Within(1e-3f), $"strip {i}");
                Assert.That(px.width, Is.EqualTo(expected[i].width).Within(1e-3f), $"strip {i}");
                Assert.That(px.height, Is.EqualTo(expected[i].height).Within(1e-3f), $"strip {i}");
            }
        }

        [Test]
        public void OutlineLayersAndCellsDrawInThatOrderUnderTheCharacters()
        {
            var outline = _view.Outline.Select(r => r.sortingOrder).Distinct().Single();
            var layer = _view.Layers[0].sortingOrder;
            var cells = _view.Cells.sortingOrder;
            var lowestCharacter = _host.GetComponentsInChildren<CharacterView>().Min(v => v.Renderer.sortingOrder);
            Assert.That(outline, Is.LessThan(layer));
            Assert.That(layer, Is.LessThan(cells));
            Assert.That(cells, Is.LessThan(lowestCharacter));
            Assert.That(_view.Outline.Concat(_view.Layers).Append(_view.Cells).Select(r => r.sortingLayerID).Distinct().Count(), Is.EqualTo(1));
        }

        [Test]
        public void EveryHardCellIsTheTilesetsSourceRectangleAndEmptyCellsAreClearInPlay()
        {
            var tileset = Resources.Load<Texture2D>(MapView.PathOf(_host.Settings.Grid.TilesetAsset));
            Assert.That(tileset.width, Is.EqualTo(303));
            Assert.That(tileset.height, Is.EqualTo(173));
            var source = MapView.Download(tileset);
            var canvas = _view.CellCanvas;
            var map = _host.World.Map;
            Assert.That(canvas.Width, Is.EqualTo(map.Columns * 16));
            Assert.That(_view.Cells.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(_view.Cells.transform.position, Is.EqualTo(Vector3.zero));

            for (var y = 0; y < map.Rows; y++)
            {
                for (var x = 0; x < map.Columns; x++)
                {
                    var cell = map[x, y];
                    var pixel = canvas[x * 16 + 5, y * 16 + 11];
                    if (cell.State == CellState.Hard)
                    {
                        // Each cell is its own sprite through the world's filter, in the tileset's coordinates (A15).
                        var shaded = _view.Effect.ShadeTexel(source, cell.Tile.X + 5, cell.Tile.Y + 11, cell.Tint);
                        Assert.That(pixel, Is.EqualTo(shaded), $"[{x}, {y}]");
                    }
                    else
                    {
                        Assert.That(pixel.A, Is.EqualTo(0), $"[{x}, {y}] is empty and not drawn in play");
                    }
                }
            }

            // And the texture on screen holds what Core painted (Unity's rows run bottom-up): wall column 20, row 0.
            var shown = _view.Cells.sprite.texture.GetPixel(20 * 16 + 5, canvas.Height - 1 - 11);
            var bridge = _view.Effect.ShadeTexel(source, 16 + 5, 11, Rgba.White);
            Assert.That((Color32)shown, Is.EqualTo(new Color32(bridge.R, bridge.G, bridge.B, bridge.A)));
        }

        [Test]
        public void EveryCellShowsWhileTheEditorIsOpenAndHidesAgainOnLeaving()
        {
            var painted = _view.Repaints;
            _view.Present();
            Assert.That(_view.Repaints, Is.EqualTo(painted), "nothing changed, nothing repainted");

            _host.World.Submit(new ToggleMapEditor(PlayerId.One));
            _host.World.Advance(1.0 / 60);
            Assert.That(_host.World.State, Is.EqualTo(GameState.MapEditor));
            _view.Present();
            Assert.That(_view.Repaints, Is.EqualTo(painted + 1));

            var tileset = MapView.Download(Resources.Load<Texture2D>(MapView.PathOf(_host.Settings.Grid.TilesetAsset)));
            var map = _host.World.Map;
            for (var y = 0; y < map.Rows; y++)
            {
                for (var x = 0; x < map.Columns; x++)
                {
                    var cell = map[x, y];
                    var expected = _view.Effect.ShadeTexel(tileset, cell.Tile.X + 3, cell.Tile.Y + 4, cell.Tint);
                    Assert.That(_view.CellCanvas[x * 16 + 3, y * 16 + 4], Is.EqualTo(expected),
                        $"[{x}, {y}] ({cell.State}) is drawn in the editor; an empty one is the first tile at alpha 64");
                }
            }

            Assert.That(map[1, 0].Alpha, Is.EqualTo(64));
            Assert.That(map[1, 0].Tile.X, Is.EqualTo(0));
            var under = _host.World.Editor(PlayerId.One).SelectedCell;
            Assert.That((under.X, under.Y), Is.EqualTo((0, 0)), "the Free camera's centre is clamped to the map's corner");
            Assert.That(map[0, 0].Tint, Is.EqualTo(new Rgba(255, 255, 0, 255)), "the cell under the editor's cursor is tinted yellow (A14)");

            _host.World.Submit(new ToggleMapEditor(PlayerId.One));
            _host.World.Advance(1.0 / 60);
            _view.Present();
            Assert.That(_view.Repaints, Is.EqualTo(painted + 2));
            Assert.That(_view.CellCanvas[3, 4].A, Is.EqualTo(0), "back in play the empty cell is not drawn");
        }
    }
}
