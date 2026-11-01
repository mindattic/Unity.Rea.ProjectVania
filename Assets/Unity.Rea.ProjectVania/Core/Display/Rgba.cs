using System;

namespace Unity.Rea.ProjectVania.Core.Display
{
    /// <summary>
    /// An XNA <c>Color</c>: four bytes, not premultiplied (XNA 3.0's <c>SpriteBlendMode.AlphaBlend</c> blends
    /// SourceAlpha / InverseSourceAlpha, so a tint's alpha scales the colour at blend time, not before).
    /// </summary>
    public readonly struct Rgba : IEquatable<Rgba>
    {
        public Rgba(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
        public byte A { get; }

        public static Rgba White => new Rgba(255, 255, 255, 255);

        public static Rgba Transparent => new Rgba(0, 0, 0, 0);

        /// <summary>XNA's <c>Color.CornflowerBlue</c>, the original's clear colour (RECONSTRUCTION 0, 6).</summary>
        public static Rgba CornflowerBlue => new Rgba(100, 149, 237, 255);

        /// <summary>A settings quad [r, g, b, a] of 0..255, truncated to bytes.</summary>
        public static Rgba FromQuad(float[] quad) => new Rgba((byte)quad[0], (byte)quad[1], (byte)quad[2], (byte)quad[3]);

        /// <summary>
        /// <paramref name="source"/> tinted by <paramref name="tint"/> (XNA multiplies each channel, as
        /// <c>SpriteBatch.Draw</c>'s colour argument does).
        /// </summary>
        public static Rgba Modulate(Rgba source, Rgba tint) =>
            new Rgba(Mul(source.R, tint.R), Mul(source.G, tint.G), Mul(source.B, tint.B), Mul(source.A, tint.A));

        /// <summary>
        /// <paramref name="source"/> drawn over <paramref name="destination"/> (Porter-Duff "over" on straight alpha), so a
        /// stack of draws composited into a transparent layer and that layer drawn over a backdrop gives the backdrop the
        /// same colour as drawing each one straight onto it.
        /// </summary>
        public static Rgba Over(Rgba source, Rgba destination)
        {
            if (source.A == 255 || destination.A == 0)
            {
                return source;
            }

            if (source.A == 0)
            {
                return destination;
            }

            float sa = source.A / 255f;
            float da = destination.A / 255f * (1f - sa);
            float a = sa + da;
            return new Rgba(
                Blend(source.R, destination.R, sa, da, a),
                Blend(source.G, destination.G, sa, da, a),
                Blend(source.B, destination.B, sa, da, a),
                (byte)Math.Round(a * 255f));
        }

        public bool Equals(Rgba other) => R == other.R && G == other.G && B == other.B && A == other.A;

        public override bool Equals(object obj) => obj is Rgba other && Equals(other);

        public override int GetHashCode() => (R << 24) | (G << 16) | (B << 8) | A;

        public static bool operator ==(Rgba left, Rgba right) => left.Equals(right);

        public static bool operator !=(Rgba left, Rgba right) => !left.Equals(right);

        public override string ToString() => $"({R}, {G}, {B}, {A})";

        static byte Mul(byte a, byte b) => (byte)((a * b + 127) / 255);

        static byte Blend(byte s, byte d, float sa, float da, float a) => (byte)Math.Round((s * sa + d * da) / a);
    }
}
