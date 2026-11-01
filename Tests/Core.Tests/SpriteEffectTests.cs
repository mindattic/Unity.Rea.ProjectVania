using System;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    /// <summary>
    /// The original's five shaders (U.VAN-US-A15, RECONSTRUCTION 6.1), run per sprite in its own texture coordinates:
    /// effects.json's defaults, each shader's arithmetic against a literal transcription of its .fx, the tint applied
    /// before the effect (BIBLE defect 19), and the map's cells painted through the world's effect texel by texel.
    /// </summary>
    public class SpriteEffectTests
    {
        static GameSettings Shipped() => GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));

        static EffectData Effects() => Shipped().Effects;

        static SpriteEffect Effect(EffectPreset preset) => new SpriteEffect(preset, Effects());

        static PixelImage Solid(int width, int height, Rgba colour)
        {
            var image = new PixelImage(width, height);
            image.Clear(colour);
            return image;
        }

        [Test]
        public void Settings_TheWorldDrawsThroughFilterAtTheShadersDefaults()
        {
            var e = Effects();
            Assert.That(e.World, Is.EqualTo(EffectPreset.Filter), "SceneManager's effect = \"filter\"; nothing selects another");
            Assert.That(e.Filter.Burn, Is.EqualTo(0.01f));
            Assert.That(e.Filter.Saturation, Is.EqualTo(1f));
            Assert.That((e.Filter.R, e.Filter.G, e.Filter.B), Is.EqualTo((1f, 1f, 1f)));
            Assert.That(e.Filter.Brite, Is.EqualTo(0f));
            Assert.That(e.Bloom.V, Is.EqualTo(0.01f));
            Assert.That(e.Bloom.A, Is.EqualTo(0.2f));
            Assert.That(e.Water.Horizon, Is.EqualTo(0.5f));
            Assert.That((e.Water.Delta, e.Water.Theta), Is.EqualTo((0f, 0f)));
            Assert.That(Enum.GetNames(typeof(EffectPreset)), Is.EqualTo(new[] { "Filter", "Negative", "Bloom", "Pause", "Water" }));
        }

        [Test]
        public void Settings_RejectAnUnknownPresetAndAMisspeltParameter()
        {
            var shipped = new DirectorySettingsSource(GameSettingsTests.ShippedFolder);
            var source = new MemorySettingsSource(shipped).Set(GameSettings.EffectsFile,
                shipped.Read(GameSettings.EffectsFile).Replace("\"world\": \"Filter\"", "\"world\": \"Sepia\"").Replace("\"burn\"", "\"burnt\""));
            var e = Assert.Throws<SettingsException>(() => GameSettings.Load(source));
            Assert.That(e.Errors, Has.Some.Contain("world"));
            Assert.That(e.Errors, Has.Some.Contain("burnt"));
        }

        [Test]
        public void Filter_IsAMildVignetteMeasuredInTheSpritesOwnTextureCoordinates()
        {
            var filter = Effect(EffectPreset.Filter);
            var white = Solid(100, 100, Rgba.White);

            // The corner texel's centre is (0.005, 0.005), 0.7000 from the middle: 1 - 0.007 of white is 253.2.
            Assert.That(filter.ShadeTexel(white, 0, 0, Rgba.White), Is.EqualTo(new Rgba(253, 253, 253, 255)));
            Assert.That(filter.ShadeTexel(white, 99, 99, Rgba.White), Is.EqualTo(new Rgba(253, 253, 253, 255)));
            Assert.That(filter.ShadeTexel(white, 50, 50, Rgba.White), Is.EqualTo(Rgba.White), "the middle is untouched");

            // The same pixel in a sprite of another size is darkened by where it lies in that sprite, not on the screen.
            var small = Solid(2, 2, Rgba.White);
            var corner = filter.ShadeFloat(small, 0.25f, 0.25f, Rgba.White);
            Assert.That(corner.R, Is.EqualTo(1f - (float)Math.Sqrt(0.125) * 0.01f).Within(1e-6f));
            Assert.That(corner.A, Is.EqualTo(1f), "alpha is left as it is");
        }

        [Test]
        public void Filter_MatchesTheFxLineForLineWithEveryParameter()
        {
            var shipped = new DirectorySettingsSource(GameSettingsTests.ShippedFolder);
            var text = shipped.Read(GameSettings.EffectsFile)
                .Replace("\"burn\": 0.01", "\"burn\": 0.15").Replace("\"saturation\": 1.0", "\"saturation\": 0.5")
                .Replace("\"g\": 1.0", "\"g\": 0.98").Replace("\"b\": 1.0", "\"b\": 0.85").Replace("\"brite\": 0.0", "\"brite\": 0.05");
            // The values SceneManager.Draw's commented-out lines would have set.
            var data = GameSettings.Load(new MemorySettingsSource(shipped).Set(GameSettings.EffectsFile, text)).Effects;
            var filter = new SpriteEffect(EffectPreset.Filter, data);

            var texture = Solid(4, 4, new Rgba(200, 100, 50, 180));
            float u = 0.125f, v = 0.625f;
            var got = filter.ShadeFloat(texture, u, v, Rgba.White);

            // filter.fx, transcribed.
            float r = 200 / 255f, g = 100 / 255f, b = 50 / 255f;
            float d = (float)Math.Sqrt(Math.Pow(u - 0.5, 2) + Math.Pow(v - 0.5, 2));
            r -= d * 0.15f; g -= d * 0.15f; b -= d * 0.15f;
            float a = (r + g + b) / 3f;
            a *= 1f - 0.5f;
            float er = (r * 0.5f + a) * 1f + 0.05f, eg = (g * 0.5f + a) * 0.98f + 0.05f, eb = (b * 0.5f + a) * 0.85f + 0.05f;

            Assert.That(got.R, Is.EqualTo(er).Within(1e-5f));
            Assert.That(got.G, Is.EqualTo(eg).Within(1e-5f));
            Assert.That(got.B, Is.EqualTo(eb).Within(1e-5f));
            Assert.That(got.A, Is.EqualTo(180 / 255f).Within(1e-6f));
        }

        [Test]
        public void Filter_AppliesTheSpritesTintFirst()
        {
            var filter = Effect(EffectPreset.Filter);
            var white = Solid(100, 100, Rgba.White);
            // A yellow name or a yellow-highlighted cell keeps its yellow (the original's shader dropped it: defect 19).
            Assert.That(filter.ShadeTexel(white, 50, 50, new Rgba(255, 255, 0, 255)), Is.EqualTo(new Rgba(255, 255, 0, 255)));
            Assert.That(filter.ShadeTexel(white, 0, 0, new Rgba(255, 255, 0, 255)), Is.EqualTo(new Rgba(253, 253, 0, 255)), "blue cannot go below 0");
            Assert.That(filter.ShadeTexel(white, 50, 50, new Rgba(255, 255, 255, 64)).A, Is.EqualTo(64), "the tint's alpha");
        }

        [Test]
        public void Negative_InvertsTheColourAndKeepsTheAlpha()
        {
            var texture = Solid(8, 8, new Rgba(200, 100, 0, 77));
            Assert.That(Effect(EffectPreset.Negative).ShadeTexel(texture, 3, 3, Rgba.White), Is.EqualTo(new Rgba(55, 155, 255, 77)));
        }

        [Test]
        public void Bloom_AveragesThirteenTapsAndWritesAlphaA()
        {
            var bloom = Effect(EffectPreset.Bloom);
            Assert.That(bloom.ShadeTexel(Solid(64, 64, new Rgba(90, 30, 240, 255)), 10, 10, Rgba.White), Is.EqualTo(new Rgba(90, 30, 240, 51)), "a = 0.2");

            // Half black, half white: a texel by the edge averages the taps that fall either side, at v = 0.01.
            var split = new PixelImage(100, 1);
            for (int x = 0; x < 100; x++)
            {
                split[x, 0] = x < 50 ? new Rgba(0, 0, 0, 255) : Rgba.White;
            }

            float u = 50.5f / 100f;
            int whites = u >= 0.5f ? 1 : 0;
            for (int i = 0; i < 12; i++)
            {
                if (u + 0.01f * SpriteEffect.Taps[i, 0] >= 0.5f) whites++;
            }

            var got = bloom.ShadeFloat(split, u, 0.5f, Rgba.White);
            Assert.That(got.R, Is.EqualTo(whites / 13f).Within(1e-6f));
        }

        [Test]
        public void Pause_IsAGreyOfTwelveTapsWithTheirAlphasSummed()
        {
            var pause = Effect(EffectPreset.Pause);
            var got = pause.ShadeFloat(Solid(64, 64, new Rgba(255, 0, 0, 51)), 0.5f, 0.5f, Rgba.White);
            Assert.That(got.R, Is.EqualTo(1f / 3f).Within(1e-6f));
            Assert.That(got.G, Is.EqualTo(got.R));
            Assert.That(got.B, Is.EqualTo(got.R));
            Assert.That(got.A, Is.EqualTo(12 * 0.2f).Within(1e-5f), "the sum, which the target clamps");
            Assert.That(pause.ShadeTexel(Solid(64, 64, new Rgba(255, 0, 0, 51)), 30, 30, Rgba.White), Is.EqualTo(new Rgba(85, 85, 85, 255)));
        }

        [Test]
        public void Water_ReflectsAboutTheHorizonDarkenedAndFadesInFromTheTop()
        {
            var water = Effect(EffectPreset.Water);
            var texture = new PixelImage(1, 100);
            for (int y = 0; y < 100; y++)
            {
                texture[0, y] = new Rgba((byte)y, 0, 0, 255);
            }

            Assert.That(water.ShadeFloat(texture, 0.5f, 0.1f, Rgba.White).A, Is.EqualTo(0f), "above v 0.2: transparent");
            Assert.That(water.ShadeFloat(texture, 0.5f, 0.225f, Rgba.White).A, Is.EqualTo((0.225f - 0.2f) * 20f).Within(1e-6f));
            Assert.That(water.ShadeFloat(texture, 0.5f, 0.6f, Rgba.White).A, Is.EqualTo(1f));

            // water.fx at u 0.5, v 0.3 with delta = theta = 0, transcribed.
            float tx = 0.5f, ty = 0.3f;
            ty = 0.5f - ty;
            ty += (float)Math.Cos(tx * 50f) / 500f;
            ty += (float)Math.Sin(ty * 250f) / 120f;
            tx += ty * ((float)Math.Sin(ty * 750f) / 250f);
            int row = (int)Math.Floor(ty * 100);
            var got = water.ShadeFloat(texture, 0.5f, 0.3f, Rgba.White);
            Assert.That(got.R, Is.EqualTo(row / 255f * 0.35f).Within(1e-6f));
        }

        [Test]
        public void PaintCells_ThroughTheWorldsEffectShadesEachCellInTheTilesetsCoordinates()
        {
            var settings = Shipped();
            var map = Map.FromSettings(settings.Grid);
            var tileset = Solid(303, 173, Rgba.White);
            var effect = SpriteEffect.World(settings.Effects);
            var canvas = MapDrawing.CellCanvas(map);
            MapDrawing.PaintCells(map, GameState.MapEditor, tileset, canvas, effect);

            // Every cell is a sprite of its own source rectangle: the first tile's corner is the tileset's corner texel,
            // darkened by its distance from the tileset's middle, wherever the cell is on the map.
            var corner = effect.ShadeTexel(tileset, 0, 0, map[0, 0].Tint);
            Assert.That(canvas[0, 0], Is.EqualTo(corner));
            Assert.That(canvas[7 * 16, 3 * 16], Is.EqualTo(Rgba.Over(effect.ShadeTexel(tileset, map[7, 3].Tile.X, map[7, 3].Tile.Y, map[7, 3].Tint), Rgba.Transparent)));
            Assert.That(canvas[0, 0].R, Is.LessThan(255), "the corner is vignetted");

            var plain = MapDrawing.CellCanvas(map);
            MapDrawing.PaintCells(map, GameState.MapEditor, tileset, plain);
            Assert.That(plain[0, 0].R, Is.EqualTo(255), "without an effect nothing is darkened");

            // A repainted cell is the whole repaint's.
            var cell = new GridCell(CellState.Hard, 255, new TileRect(32, 16, 16, 16));
            map.Set(4, 4, cell);
            var whole = MapDrawing.CellCanvas(map);
            MapDrawing.PaintCells(map, GameState.MapEditor, tileset, whole, effect);
            Assert.That(MapDrawing.RepaintCell(map, 4, 4, GameState.MapEditor, tileset, canvas, effect), Is.True);
            Assert.That(canvas.Pixels, Is.EqualTo(whole.Pixels));
        }
    }
}
