using System;
using Unity.Rea.ProjectVania.Core.Display;

namespace Unity.Rea.ProjectVania.Core.World
{
    /// <summary>
    /// A cell's state (<c>GridCell.CellState</c>, RECONSTRUCTION 5). Only <see cref="Hard"/> collides; <see cref="Soft"/>
    /// is declared, drawn like Hard, and never assigned or collided by the original, so it is kept and inert.
    /// </summary>
    public enum CellState
    {
        Hard,
        Soft,
        Empty,
    }

    /// <summary>A cell index on the grid (the original's <c>Point</c> from <c>GetGridCoordinates</c>).</summary>
    public readonly struct CellIndex : IEquatable<CellIndex>
    {
        public CellIndex(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(CellIndex other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is CellIndex other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Y;

        public static bool operator ==(CellIndex left, CellIndex right) => left.Equals(right);

        public static bool operator !=(CellIndex left, CellIndex right) => !left.Equals(right);

        public override string ToString() => $"[{X}, {Y}]";
    }

    /// <summary>A pixel rectangle in the tileset (a cell's <c>assetBounds</c>).</summary>
    public readonly struct TileRect : IEquatable<TileRect>
    {
        public TileRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>A settings quad [x, y, width, height], truncated to whole pixels.</summary>
        public static TileRect FromQuad(float[] quad) => new TileRect((int)quad[0], (int)quad[1], (int)quad[2], (int)quad[3]);

        public bool Equals(TileRect other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

        public override bool Equals(object obj) => obj is TileRect other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ (Y * 131) ^ (Width * 17) ^ Height;

        public override string ToString() => $"({X}, {Y}, {Width}, {Height})";
    }

    /// <summary>
    /// One 16 px cell (<c>MapEditor\GridCell.cs</c>): its state, the colour it is tinted (white at alpha 64 for a fresh
    /// cell, opaque white for a placed one, yellow under the editor's cursor) and its source rectangle in the tileset. Its
    /// position is its index times the cell size.
    /// </summary>
    public readonly struct GridCell : IEquatable<GridCell>
    {
        /// <summary>A cell tinted white at <paramref name="alpha"/>.</summary>
        public GridCell(CellState state, byte alpha, TileRect tile)
            : this(state, new Rgba(255, 255, 255, alpha), tile)
        {
        }

        public GridCell(CellState state, Rgba tint, TileRect tile)
        {
            State = state;
            Tint = tint;
            Tile = tile;
        }

        public CellState State { get; }

        /// <summary>The cell's <c>Color</c>, which <c>Map.DrawGrid</c> tints its tile with.</summary>
        public Rgba Tint { get; }

        /// <summary>The tint's alpha.</summary>
        public byte Alpha => Tint.A;

        public TileRect Tile { get; }

        /// <summary>Hard or Soft: the cells <c>Map.DrawGrid</c> draws outside the editor.</summary>
        public bool IsSolidLooking => State == CellState.Hard || State == CellState.Soft;

        /// <summary>The same cell in another colour (the editor's highlight and its undoing change only the colour).</summary>
        public GridCell WithTint(Rgba tint) => new GridCell(State, tint, Tile);

        public bool Equals(GridCell other) => State == other.State && Tint == other.Tint && Tile.Equals(other.Tile);

        public override bool Equals(object obj) => obj is GridCell other && Equals(other);

        public override int GetHashCode() => ((int)State * 397) ^ Tint.GetHashCode() ^ (Tile.GetHashCode() * 31);
    }

    /// <summary>
    /// How a player treats a <see cref="Map.CheckHorizontalCollision"/> count (RECONSTRUCTION 4.4, the partial-wall rule).
    /// </summary>
    public enum WallContact
    {
        /// <summary>No hard cell ahead: horizontal acceleration applies.</summary>
        None,

        /// <summary>Some but not all of the player's height is blocked (a step or ledge): neither accelerated nor stopped, so drag decays him.</summary>
        Partial,

        /// <summary>Every cell of the player's height is blocked: horizontal velocity is held at zero.</summary>
        Full,
    }
}
