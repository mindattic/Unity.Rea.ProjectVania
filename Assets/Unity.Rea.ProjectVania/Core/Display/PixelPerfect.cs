using System;

namespace Unity.Rea.ProjectVania.Core.Display
{
    /// <summary>A viewport inside a window, in pixels, origin bottom-left.</summary>
    public readonly struct PixelViewport
    {
        public PixelViewport(int x, int y, int width, int height, int scale)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Scale = scale;
        }

        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>The whole-number magnification of the reference image (0 when the window is smaller than the reference and the image is shrunk to fit).</summary>
        public int Scale { get; }
    }

    /// <summary>
    /// The pixel-perfect 800x600 reference (RECONSTRUCTION 0): the original draws to an 800x600 back buffer, so the
    /// rebuild draws to that reference and magnifies it by the largest whole number the window holds, centred, so a
    /// source pixel is always a whole number of screen pixels. Engine-free and pipeline-agnostic: the Runtime applies
    /// the result to a camera rectangle and the UI's reference resolution, whatever render pipeline draws.
    /// </summary>
    public static class PixelPerfect
    {
        public const int ReferenceWidth = 800;
        public const int ReferenceHeight = 600;

        /// <summary>The viewport the reference occupies in a window of <paramref name="screenWidth"/> x <paramref name="screenHeight"/>.</summary>
        public static PixelViewport Fit(int screenWidth, int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(screenWidth <= 0 ? nameof(screenWidth) : nameof(screenHeight), "A window has a positive size.");
            }

            var scale = Math.Min(screenWidth / ReferenceWidth, screenHeight / ReferenceHeight);
            int width, height;
            if (scale >= 1)
            {
                width = ReferenceWidth * scale;
                height = ReferenceHeight * scale;
            }
            else
            {
                // Smaller than the reference: shrink to fit, keeping the 4:3 shape.
                var ratio = Math.Min(screenWidth / (double)ReferenceWidth, screenHeight / (double)ReferenceHeight);
                width = Math.Max(1, (int)(ReferenceWidth * ratio));
                height = Math.Max(1, (int)(ReferenceHeight * ratio));
                scale = 0;
            }

            return new PixelViewport((screenWidth - width) / 2, (screenHeight - height) / 2, width, height, scale);
        }

        /// <summary>The orthographic half-height, in world units, that shows the reference's 600 rows at <paramref name="pixelsPerUnit"/>.</summary>
        public static float OrthographicSize(float pixelsPerUnit)
        {
            if (!(pixelsPerUnit > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(pixelsPerUnit), "Pixels per unit must be positive.");
            }

            return ReferenceHeight / 2f / pixelsPerUnit;
        }
    }
}
