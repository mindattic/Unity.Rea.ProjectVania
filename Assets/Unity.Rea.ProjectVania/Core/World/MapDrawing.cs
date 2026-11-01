using System;
using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Display;

namespace Unity.Rea.ProjectVania.Core.World
{
    /// <summary>A rectangle in the map's pixels (y down), as a stretched sprite covers it.</summary>
    public readonly struct PixelRect
    {
        public PixelRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }

        public override string ToString() => $"({X}, {Y}, {Width}, {Height})";
    }

    /// <summary>
    /// What <c>Map.Draw</c> puts on screen (RECONSTRUCTION 5, 6), decided without an engine: the outline's strips, which
    /// cells are drawn, and the cells painted from the tileset at their source rectangles in the original's order. The
    /// order of the whole is outline, then layers (when shown), then cells, over the clear colour; the Runtime keeps it
    /// with sorting orders.
    /// </summary>
    public static class MapDrawing
    {
        /// <summary>
        /// The map's outline as <c>Shape</c> drew it: <c>CreateBox((0, 0), (width, height))</c> makes the polyline
        /// (0,0) -> (0,h) -> (w,h) -> (w,0) -> (0,0), and each segment is a 1x1 white pixel drawn at its start point,
        /// rotated by the segment's angle about its top-left corner and stretched to (length, thickness). So the
        /// thickness lies to the segment's right in screen space (y down): the left strip is at x = -1, the bottom one at
        /// y = h, the right one at x = w and the top one at y = -1, each exactly the edge's length, so the four corner
        /// pixels outside the map are never covered. Segments are returned in drawing order.
        /// </summary>
        public static IReadOnlyList<PixelRect> Outline(float width, float height, float thickness)
        {
            var points = new[]
            {
                (0f, 0f),
                (0f, height),
                (width, height),
                (width, 0f),
                (0f, 0f),
            };

            var strips = new List<PixelRect>(4);
            for (int i = 1; i < points.Length; i++)
            {
                strips.Add(StretchedPixel(points[i - 1], points[i], thickness));
            }

            return strips;
        }

        /// <summary>
        /// One <c>Shape</c> segment: a pixel at <paramref name="from"/> rotated toward <paramref name="to"/> and scaled to
        /// (length, thickness). Only the four axis-aligned directions occur (a box), and each is worked exactly.
        /// </summary>
        static PixelRect StretchedPixel((float X, float Y) from, (float X, float Y) to, float thickness)
        {
            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            if (dx != 0f && dy != 0f)
            {
                throw new ArgumentException("Only axis-aligned outline segments are supported (Map.Draw only draws a box).");
            }

            if (dx > 0f) return new PixelRect(from.X, from.Y, dx, thickness);              // angle 0
            if (dx < 0f) return new PixelRect(from.X + dx, from.Y - thickness, -dx, thickness); // angle 180
            if (dy > 0f) return new PixelRect(from.X - thickness, from.Y, thickness, dy);  // angle 90
            return new PixelRect(from.X, from.Y + dy, thickness, -dy);                      // angle -90
        }

        /// <summary>
        /// <c>Map.DrawGrid</c>'s test: a cell is drawn if it is Hard or Soft, or whatever its state while the game is in
        /// the map editor (then an empty cell shows as its translucent first-tile square).
        /// </summary>
        public static bool IsDrawn(GridCell cell, GameState state) => state == GameState.MapEditor || cell.IsSolidLooking;

        /// <summary>
        /// Paints every drawn cell into <paramref name="target"/> (cleared to transparent first): the cell's source
        /// rectangle of <paramref name="tileset"/>, unscaled, at the cell's position (index x cell size), tinted by the
        /// cell's colour, composited in <c>foreach (GridCell cell in cells)</c> order (column-major: x outer, y inner).
        /// Pixels falling outside the tileset or the target are skipped. Drawing the result over the layers is the same as
        /// drawing each cell over them. With <paramref name="effect"/> each cell is drawn through it as the original drew
        /// every cell through <c>filter</c>: per sprite, in the tileset's texture coordinates (RECONSTRUCTION 6.1).
        /// </summary>
        public static void PaintCells(Map map, GameState state, PixelImage tileset, PixelImage target, SpriteEffect effect = null)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (tileset == null) throw new ArgumentNullException(nameof(tileset));
            if (target == null) throw new ArgumentNullException(nameof(target));

            target.Clear(Rgba.Transparent);
            for (int x = 0; x < map.Columns; x++)
            {
                for (int y = 0; y < map.Rows; y++)
                {
                    var cell = map[x, y];
                    if (IsDrawn(cell, state))
                    {
                        PaintCell(cell, x * map.CellSize, y * map.CellSize, tileset, target, effect);
                    }
                }
            }
        }

        /// <summary>
        /// Repaints one cell of a canvas <see cref="PaintCells"/> painted: clears the cell's square and paints the cell
        /// there if it is drawn. Cells never overlap while every tile is at most a cell (every tile the data and the
        /// editor give is exactly one), so the result is the whole repaint's; returns false, painting nothing, for a cell
        /// whose tile is larger, which needs the whole repaint.
        /// </summary>
        public static bool RepaintCell(Map map, int x, int y, GameState state, PixelImage tileset, PixelImage target, SpriteEffect effect = null)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (tileset == null) throw new ArgumentNullException(nameof(tileset));
            if (target == null) throw new ArgumentNullException(nameof(target));

            var cell = map[x, y];
            if (cell.Tile.Width > map.CellSize || cell.Tile.Height > map.CellSize)
            {
                return false;
            }

            int left = x * map.CellSize;
            int top = y * map.CellSize;
            for (int v = 0; v < map.CellSize; v++)
            {
                for (int u = 0; u < map.CellSize; u++)
                {
                    if (target.Contains(left + u, top + v))
                    {
                        target[left + u, top + v] = Rgba.Transparent;
                    }
                }
            }

            if (IsDrawn(cell, state))
            {
                PaintCell(cell, left, top, tileset, target, effect);
            }

            return true;
        }

        /// <summary>The pixel size <see cref="PaintCells"/> needs to hold every cell of <paramref name="map"/>.</summary>
        public static PixelImage CellCanvas(Map map) => new PixelImage(map.Columns * map.CellSize, map.Rows * map.CellSize);

        static void PaintCell(GridCell cell, int left, int top, PixelImage tileset, PixelImage target, SpriteEffect effect)
        {
            var tint = cell.Tint;
            var tile = cell.Tile;
            // A cell is drawn unscaled from its source rectangle, so each of its pixels is one texel's fragment.
            var shaded = effect?.Shaded(tileset, tint);
            for (int v = 0; v < tile.Height; v++)
            {
                for (int u = 0; u < tile.Width; u++)
                {
                    int sx = tile.X + u;
                    int sy = tile.Y + v;
                    int tx = left + u;
                    int ty = top + v;
                    if (!tileset.Contains(sx, sy) || !target.Contains(tx, ty))
                    {
                        continue;
                    }

                    var drawn = shaded != null ? shaded[sx, sy] : Rgba.Modulate(tileset[sx, sy], tint);
                    target[tx, ty] = Rgba.Over(drawn, target[tx, ty]);
                }
            }
        }
    }
}
