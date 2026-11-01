using System;

namespace Unity.Rea.ProjectVania.Core.Display
{
    /// <summary>
    /// An image as the original addressed it: <see cref="Width"/> x <see cref="Height"/> pixels, row 0 at the top (y down).
    /// Core paints into these so what the world shows is decided and tested without an engine; the Runtime copies one to
    /// and from a texture (whose rows run bottom-up).
    /// </summary>
    public sealed class PixelImage
    {
        public PixelImage(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
            Pixels = new Rgba[width * height];
        }

        public int Width { get; }
        public int Height { get; }

        /// <summary>Row-major from the top-left: pixel (x, y) is <c>Pixels[y * Width + x]</c>.</summary>
        public Rgba[] Pixels { get; }

        public Rgba this[int x, int y]
        {
            get => Pixels[Index(x, y)];
            set => Pixels[Index(x, y)] = value;
        }

        public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public void Clear(Rgba colour)
        {
            for (int i = 0; i < Pixels.Length; i++)
            {
                Pixels[i] = colour;
            }
        }

        int Index(int x, int y)
        {
            if (!Contains(x, y)) throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}) is outside the {Width} x {Height} image.");
            return y * Width + x;
        }
    }
}
