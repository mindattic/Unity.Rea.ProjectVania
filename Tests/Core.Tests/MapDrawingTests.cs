using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    /// <summary>
    /// What the map draws (U.VAN-US-A8, RECONSTRUCTION 5, 6): the outline worked from <c>Shape.CreateBox</c> and its
    /// stretched pixel, the draw test of <c>Map.DrawGrid</c>, and the cells painted from the tileset at their source
    /// rectangles, white-tinted at their alpha.
    /// </summary>
    public class MapDrawingTests
    {
        static GameSettings Shipped() => GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));

        /// <summary>A tileset whose every pixel is unique: r = x, g = y, b = 7, opaque.</summary>
        static PixelImage CodedTileset(int width = 303, int height = 173)
        {
            var image = new PixelImage(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    image[x, y] = new Rgba((byte)x, (byte)y, 7, 255);
                }
            }

            return image;
        }

        [Test]
        public void Settings_DrawOnCornflowerBlueWithAWhiteOutlineAndTheArea1Backdrop()
        {
            var draw = Shipped().Grid.Draw;
            Assert.That(draw.ClearColour, Is.EqualTo(Rgba.CornflowerBlue));
            Assert.That(draw.ClearColour, Is.EqualTo(new Rgba(100, 149, 237, 255)));
            Assert.That(draw.OutlineColour, Is.EqualTo(Rgba.White));
            Assert.That(draw.OutlineThickness, Is.EqualTo(1f));
            Assert.That(draw.ShowLayers, Is.True);
            Assert.That(draw.Layers.Count, Is.EqualTo(1), "only area_1 was ever loaded");
            Assert.That(draw.Layers[0].Asset, Is.EqualTo("area_1"));
            Assert.That(draw.Layers[0].Position.X, Is.EqualTo(0f));
            Assert.That(draw.Layers[0].Position.Y, Is.EqualTo(0f));
            Assert.That(draw.Layers[0].Colour, Is.EqualTo(Rgba.White));
        }

        [Test]
        public void Settings_RejectAColourOutOfRangeAndAMisspeltLayerKey()
        {
            var source = new MemorySettingsSource(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));
            var text = source.Read(GameSettings.GridFile)
                .Replace("\"clearColour\": [100, 149, 237, 255]", "\"clearColour\": [100, 149, 256, 255]")
                .Replace("\"asset\": \"area_1\"", "\"assett\": \"area_1\"");
            source.Set(GameSettings.GridFile, text);
            var e = Assert.Throws<SettingsException>(() => GameSettings.Load(source));
            Assert.That(e.Errors, Has.Some.Contain("clearColour"));
            Assert.That(e.Errors, Has.Some.Contain("assett"));
            Assert.That(e.Errors, Has.Some.Contain("'asset' is missing"));
        }

        [Test]
        public void Outline_IsShapesFourStretchedPixelsOutsideTheMapEdges()
        {
            var strips = MapDrawing.Outline(1900, 1200, 1);
            Assert.That(strips.Count, Is.EqualTo(4));

            // (0,0) -> (0,1200): angle 90, the pixel's 1 px thickness swings to x = -1.
            Assert.That(strips[0].ToString(), Is.EqualTo(new PixelRect(-1, 0, 1, 1200).ToString()));
            // (0,1200) -> (1900,1200): angle 0, just below the map.
            Assert.That(strips[1].ToString(), Is.EqualTo(new PixelRect(0, 1200, 1900, 1).ToString()));
            // (1900,1200) -> (1900,0): angle -90, just right of the map.
            Assert.That(strips[2].ToString(), Is.EqualTo(new PixelRect(1900, 0, 1, 1200).ToString()));
            // (1900,0) -> (0,0): angle 180, just above the map.
            Assert.That(strips[3].ToString(), Is.EqualTo(new PixelRect(0, -1, 1900, 1).ToString()));
        }

        [Test]
        public void IsDrawn_HardAndSoftAlwaysAndEveryCellInTheEditor()
        {
            var tile = new TileRect(0, 0, 16, 16);
            var hard = new GridCell(CellState.Hard, 255, tile);
            var soft = new GridCell(CellState.Soft, 255, tile);
            var empty = new GridCell(CellState.Empty, 64, tile);
            foreach (var state in new[] { GameState.Play, GameState.MainMenu, GameState.Console })
            {
                Assert.That(MapDrawing.IsDrawn(hard, state), Is.True, $"{state}");
                Assert.That(MapDrawing.IsDrawn(soft, state), Is.True, $"{state}");
                Assert.That(MapDrawing.IsDrawn(empty, state), Is.False, $"{state}");
            }

            Assert.That(MapDrawing.IsDrawn(hard, GameState.MapEditor), Is.True);
            Assert.That(MapDrawing.IsDrawn(empty, GameState.MapEditor), Is.True);
        }

        [Test]
        public void PaintCells_InPlayDrawsEveryHardBridgeCellFromItsSourceRectangleAndNothingElse()
        {
            var map = Map.FromSettings(Shipped().Grid);
            var tileset = CodedTileset();
            var canvas = MapDrawing.CellCanvas(map);
            Assert.That(canvas.Width, Is.EqualTo(118 * 16));
            Assert.That(canvas.Height, Is.EqualTo(75 * 16));

            MapDrawing.PaintCells(map, GameState.Play, tileset, canvas);

            for (int cy = 0; cy < map.Rows; cy++)
            {
                for (int cx = 0; cx < map.Columns; cx++)
                {
                    bool hard = map[cx, cy].State == CellState.Hard;
                    // Sample the cell's corners and centre.
                    foreach (var (u, v) in new[] { (0, 0), (15, 0), (0, 15), (15, 15), (8, 7) })
                    {
                        var pixel = canvas[cx * 16 + u, cy * 16 + v];
                        var expected = hard ? new Rgba((byte)(16 + u), (byte)v, 7, 255) : Rgba.Transparent;
                        Assert.That(pixel, Is.EqualTo(expected), $"cell [{cx}, {cy}] pixel ({u}, {v})");
                    }
                }
            }
        }

        [Test]
        public void PaintCells_InTheEditorDrawsEmptyCellsAsTheFirstTileAtAlpha64()
        {
            var map = Map.FromSettings(Shipped().Grid);
            var canvas = MapDrawing.CellCanvas(map);
            MapDrawing.PaintCells(map, GameState.MapEditor, CodedTileset(), canvas);

            Assert.That(canvas[0, 0], Is.EqualTo(new Rgba(0, 0, 7, 64)), "the first tile, white at alpha 64");
            Assert.That(canvas[5 * 16 + 3, 2 * 16 + 9], Is.EqualTo(new Rgba(3, 9, 7, 64)));
            Assert.That(canvas[20 * 16 + 1, 2 * 16], Is.EqualTo(new Rgba(17, 0, 7, 255)), "a wall cell is opaque bridge tile");
            Assert.That(canvas.Pixels.Any(p => p.A == 0), Is.False, "every cell is visible in the editor");
        }

        [Test]
        public void PaintCells_TintsATranslucentTileAndRedrawsOnlyWhatTheMapHolds()
        {
            var map = new Map(32, 16, 16, new GridCell(CellState.Empty, 64, new TileRect(0, 0, 16, 16)));
            var tileset = new PixelImage(32, 16);
            tileset.Clear(new Rgba(200, 100, 50, 128));
            int before = map.Revision;
            map.Set(1, 0, new GridCell(CellState.Hard, 64, new TileRect(16, 0, 16, 16)));
            Assert.That(map.Revision, Is.EqualTo(before + 1), "a change to a cell is counted");

            var canvas = MapDrawing.CellCanvas(map);
            MapDrawing.PaintCells(map, GameState.Play, tileset, canvas);
            Assert.That(canvas[0, 0], Is.EqualTo(Rgba.Transparent), "the empty cell is not drawn in play");
            Assert.That(canvas[16, 0], Is.EqualTo(new Rgba(200, 100, 50, 32)), "alpha 128 x 64 / 255, colour unchanged");
        }

        [Test]
        public void PaintCells_SkipsSourcePixelsOutsideTheTileset()
        {
            var map = new Map(16, 16, 16, new GridCell(CellState.Hard, 255, new TileRect(296, 0, 16, 16)));
            var canvas = MapDrawing.CellCanvas(map);
            MapDrawing.PaintCells(map, GameState.Play, CodedTileset(), canvas);
            Assert.That(canvas[6, 0], Is.EqualTo(new Rgba(302 - 256, 0, 7, 255)), "x 302, the tileset's last column (r wraps at a byte)");
            Assert.That(canvas[7, 0], Is.EqualTo(Rgba.Transparent), "x 303 is past the 303 px tileset");
        }

        [Test]
        public void Over_OfStraightAlphaMatchesDrawingEachLayerInTurn()
        {
            var backdrop = new Rgba(10, 200, 30, 255);
            var first = new Rgba(255, 0, 0, 128);
            var second = new Rgba(0, 0, 255, 64);

            var direct = Rgba.Over(second, Rgba.Over(first, backdrop));
            var layered = Rgba.Over(Rgba.Over(second, Rgba.Over(first, Rgba.Transparent)), backdrop);
            Assert.That(layered.R, Is.EqualTo(direct.R).Within(1));
            Assert.That(layered.G, Is.EqualTo(direct.G).Within(1));
            Assert.That(layered.B, Is.EqualTo(direct.B).Within(1));
            Assert.That(layered.A, Is.EqualTo(255));
        }
    }
}
