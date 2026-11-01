using System;
using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Settings;

namespace Unity.Rea.ProjectVania.Core.Display
{
    /// <summary>
    /// The original's five pixel shaders (<c>Content\Effects\*.fx</c>, RECONSTRUCTION 6.1), each run per sprite in that
    /// sprite's own texture coordinates (u right, v down, 0..1 over the whole texture, as XNA's <c>TEXCOORD0</c>), the way
    /// <c>SceneManager</c> wrapped every world sprite in one. Core paints the map's cells with it; the Runtime's
    /// <c>WorldEffect</c> shader is the same arithmetic on the GPU for every other world sprite. Textures are sampled at the
    /// nearest texel, clamped at the edges, as every world texture is drawn. The sprite's tint is applied to each sample
    /// before the effect (BIBLE defect 19: the original's shaders read no vertex colour, so every tint was dropped).
    /// </summary>
    public sealed class SpriteEffect
    {
        /// <summary>The 12 Poisson-disc offsets <c>bloom.fx</c> and <c>pause.fx</c> both declared.</summary>
        public static readonly float[,] Taps =
        {
            { -0.326212f, -0.405805f },
            { -0.840144f, -0.073580f },
            { -0.695914f, 0.457137f },
            { -0.203345f, 0.620716f },
            { 0.962340f, -0.194983f },
            { 0.473434f, -0.480026f },
            { 0.519456f, 0.767022f },
            { 0.185461f, -0.893124f },
            { 0.507431f, 0.064425f },
            { 0.896420f, 0.412458f },
            { -0.321940f, -0.932615f },
            { -0.791559f, -0.597705f },
        };

        /// <summary><c>pause.fx</c>'s literal tap scale.</summary>
        public const float PauseSpread = 0.005f;

        /// <summary><c>water.fx</c>'s literal darkening (<c>sum *= 0.35f</c>, alpha included).</summary>
        public const float WaterDarken = 0.35f;

        /// <summary><c>water.fx</c>: transparent above v 0.2, ramping to opaque at v 0.25 (<c>(y - 0.2) * 20</c>).</summary>
        public const float WaterFadeFrom = 0.2f;
        public const float WaterFadeTo = 0.25f;
        public const float WaterFadeRate = 20f;

        readonly Dictionary<(PixelImage, Rgba), PixelImage> _shaded = new Dictionary<(PixelImage, Rgba), PixelImage>();

        public SpriteEffect(EffectPreset preset, EffectData data)
        {
            Preset = preset;
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>The effect effects.json names for the world.</summary>
        public static SpriteEffect World(EffectData data) => new SpriteEffect(data.World, data);

        public EffectPreset Preset { get; }
        public EffectData Data { get; }

        /// <summary>
        /// One fragment: <paramref name="texture"/> drawn tinted by <paramref name="tint"/>, shaded at texture coordinate
        /// (<paramref name="u"/>, <paramref name="v"/>), written to an 8-bit target (clamped and rounded).
        /// </summary>
        public Rgba Shade(PixelImage texture, float u, float v, Rgba tint)
        {
            var c = ShadeFloat(texture, u, v, tint);
            return new Rgba(ToByte(c.R), ToByte(c.G), ToByte(c.B), ToByte(c.A));
        }

        /// <summary>
        /// Texel (<paramref name="x"/>, <paramref name="y"/>) of <paramref name="texture"/> drawn unscaled through the
        /// effect: the fragment over a texel's centre has the coordinate ((x + 0.5) / width, (y + 0.5) / height).
        /// </summary>
        public Rgba ShadeTexel(PixelImage texture, int x, int y, Rgba tint) =>
            Shade(texture, (x + 0.5f) / texture.Width, (y + 0.5f) / texture.Height, tint);

        /// <summary>
        /// Every texel of <paramref name="texture"/> shaded with <paramref name="tint"/>, made once per texture and tint and
        /// kept: a sprite drawn unscaled from any source rectangle of it shows these pixels.
        /// </summary>
        public PixelImage Shaded(PixelImage texture, Rgba tint)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (_shaded.TryGetValue((texture, tint), out var cached))
            {
                return cached;
            }

            var image = new PixelImage(texture.Width, texture.Height);
            for (int y = 0; y < texture.Height; y++)
            {
                for (int x = 0; x < texture.Width; x++)
                {
                    image[x, y] = ShadeTexel(texture, x, y, tint);
                }
            }

            _shaded[(texture, tint)] = image;
            return image;
        }

        /// <summary>Forgets the shaded copies (call when a texture's pixels change).</summary>
        public void ClearCache() => _shaded.Clear();

        /// <summary>The shader's output before the target clamps it.</summary>
        public Colour ShadeFloat(PixelImage texture, float u, float v, Rgba tint)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            var t = Colour.From(tint);
            switch (Preset)
            {
                case EffectPreset.Filter: return Filter(texture, u, v, t);
                case EffectPreset.Negative: return Negative(texture, u, v, t);
                case EffectPreset.Bloom: return Bloom(texture, u, v, t);
                case EffectPreset.Pause: return Pause(texture, u, v, t);
                case EffectPreset.Water: return Water(texture, u, v, t);
                default: throw new InvalidOperationException($"Unknown effect {Preset}.");
            }
        }

        // filter.fx. The refract sampler was never bound, so GetDif read black and displaced nothing.
        Colour Filter(PixelImage texture, float u, float v, Colour tint)
        {
            var f = Data.Filter;
            var col = Sample(texture, u, v) * tint;
            float d = (float)Math.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));
            col = col.AddRgb(-d * f.Burn);
            float a = (col.R + col.G + col.B) / 3f;
            a *= 1f - f.Saturation;
            col = new Colour(
                (col.R * f.Saturation + a) * f.R,
                (col.G * f.Saturation + a) * f.G,
                (col.B * f.Saturation + a) * f.B,
                col.A);
            return col.AddRgb(f.Brite);
        }

        // negative.fx
        static Colour Negative(PixelImage texture, float u, float v, Colour tint)
        {
            var col = Sample(texture, u, v) * tint;
            return new Colour(1f - col.R, 1f - col.G, 1f - col.B, col.A);
        }

        // bloom.fx
        Colour Bloom(PixelImage texture, float u, float v, Colour tint)
        {
            var b = Data.Bloom;
            var col = Sample(texture, u, v) * tint;
            for (int i = 0; i < 12; i++)
            {
                col += Sample(texture, u + b.V * Taps[i, 0], v + b.V * Taps[i, 1]) * tint;
            }

            col = col * (1f / 13f);
            return new Colour(col.R, col.G, col.B, b.A);
        }

        // pause.fx: the alpha is the 12 taps' sum, which the target clamps.
        static Colour Pause(PixelImage texture, float u, float v, Colour tint)
        {
            var col = new Colour(0f, 0f, 0f, 0f);
            for (int i = 0; i < 12; i++)
            {
                col += Sample(texture, u + PauseSpread * Taps[i, 0], v + PauseSpread * Taps[i, 1]) * tint;
            }

            float a = (col.R + col.G + col.B) / 3f;
            a /= 12f;
            return new Colour(a, a, a, col.A);
        }

        // water.fx
        Colour Water(PixelImage texture, float u, float v, Colour tint)
        {
            var w = Data.Water;
            var sum = new Colour(0f, 0f, 0f, 0f);
            float x = u;
            float y = v;
            if (y < 1f)
            {
                y = w.Horizon - y;
                y += (float)Math.Cos(x * 50f + w.Delta) / 500f;
                y += (float)Math.Sin(y * 250f + w.Theta) / 120f;
                x += y * ((float)Math.Sin(y * 750f + w.Theta) / 250f);
                sum = Sample(texture, x, y) * tint;
            }

            sum = sum * WaterDarken;
            float alpha;
            if (v < WaterFadeFrom) alpha = 0f;
            else if (v < WaterFadeTo) alpha = (v - WaterFadeFrom) * WaterFadeRate;
            else alpha = 1f;
            return new Colour(sum.R, sum.G, sum.B, alpha);
        }

        /// <summary>The nearest texel to (u, v), clamped to the edge, as 0..1 floats.</summary>
        public static Colour Sample(PixelImage texture, float u, float v)
        {
            int x = Clamp((int)Math.Floor(u * texture.Width), texture.Width - 1);
            int y = Clamp((int)Math.Floor(v * texture.Height), texture.Height - 1);
            return Colour.From(texture[x, y]);
        }

        static int Clamp(int value, int max) => value < 0 ? 0 : value > max ? max : value;

        static byte ToByte(float value)
        {
            if (float.IsNaN(value) || value <= 0f) return 0;
            if (value >= 1f) return 255;
            return (byte)Math.Round(value * 255f, MidpointRounding.AwayFromZero);
        }

        /// <summary>A shader's float4.</summary>
        public readonly struct Colour
        {
            public Colour(float r, float g, float b, float a)
            {
                R = r;
                G = g;
                B = b;
                A = a;
            }

            public float R { get; }
            public float G { get; }
            public float B { get; }
            public float A { get; }

            public static Colour From(Rgba c) => new Colour(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

            public Colour AddRgb(float value) => new Colour(R + value, G + value, B + value, A);

            public static Colour operator +(Colour x, Colour y) => new Colour(x.R + y.R, x.G + y.G, x.B + y.B, x.A + y.A);

            public static Colour operator *(Colour x, Colour y) => new Colour(x.R * y.R, x.G * y.G, x.B * y.B, x.A * y.A);

            public static Colour operator *(Colour x, float s) => new Colour(x.R * s, x.G * s, x.B * s, x.A * s);

            public override string ToString() => $"({R}, {G}, {B}, {A})";
        }
    }
}
