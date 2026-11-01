using System;
using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Settings;

namespace Unity.Rea.ProjectVania.Core.World
{
    /// <summary>
    /// The world's collision grid (<c>World\Map\Map.cs</c>, RECONSTRUCTION 5): a map of <see cref="Width"/> x
    /// <see cref="Height"/> pixels cut into <see cref="Columns"/> x <see cref="Rows"/> cells of <see cref="CellSize"/>
    /// pixels (118 x 75 of 16 px for 1900 x 1200), and the original's floor, ceiling and horizontal queries.
    /// Every query takes pixel positions (y down), divides by the cell size and truncates toward zero exactly as the
    /// original's <c>(int)</c> casts did, so a position a fraction of a pixel left of or above the map still reads
    /// cell 0. Cells outside the grid are never Hard. Cells change only inside Core (the editor's commands, A14).
    /// </summary>
    public sealed class Map
    {
        readonly GridCell[,] _cells;

        /// <summary>An all-Empty grid; every cell starts as <paramref name="fresh"/>.</summary>
        public Map(int width, int height, int cellSize, GridCell fresh)
        {
            if (cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (width < cellSize || height < cellSize) throw new ArgumentOutOfRangeException(nameof(width), "The map must hold at least one cell.");

            Width = width;
            Height = height;
            CellSize = cellSize;
            Fresh = fresh;
            _cells = new GridCell[width / cellSize, height / cellSize];
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    _cells[x, y] = fresh;
                }
            }
        }

        /// <summary>The map's width in pixels (its <c>Bounds()</c> is (0, 0, Width, Height)).</summary>
        public int Width { get; }

        public int Height { get; }

        public int CellSize { get; }

        /// <summary>Cells across: Width / CellSize, truncated (1900 / 16 = 118; the last 12 px have no cell).</summary>
        public int Columns => _cells.GetLength(0);

        /// <summary>Cells down: Height / CellSize, truncated (1200 / 16 = 75).</summary>
        public int Rows => _cells.GetLength(1);

        /// <summary>What a fresh (or erased) cell is: Empty, white at alpha 64, the tileset's first tile.</summary>
        public GridCell Fresh { get; }

        /// <summary>
        /// The starting level, built as the original's constructor did: a fresh grid (<c>LoadGrid</c>), then
        /// <c>SpawnBridge</c>: every cell of the floor row and of each wall column Hard, opaque, with the bridge tile.
        /// (SpawnBridge also set [38, 30] and [39, 30], which the floor row already covers.) Then the rebuild's block
        /// structures (grid.json <c>structures</c>, U.VAN-T25).
        /// </summary>
        public static Map FromSettings(GridData grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            var fresh = new GridCell(CellState.Empty, (byte)grid.FreshCellAlpha, TileRect.FromQuad(grid.FreshCellAssetBounds));
            var map = new Map(grid.MapWidth, grid.MapHeight, grid.CellSize, fresh);
            var bridge = new GridCell(CellState.Hard, 255, TileRect.FromQuad(grid.BridgeTileBounds));
            for (int x = 0; x < map.Columns; x++)
            {
                map.Set(x, grid.FloorRow, bridge);
            }

            foreach (var column in grid.WallColumns)
            {
                for (int y = 0; y < map.Rows; y++)
                {
                    map.Set(column, y, bridge);
                }
            }

            // U.VAN-T25 (owner decision): the block structures between the walls, Hard like the bridge, for Leo (who
            // cannot wall slide) to climb and jump across.
            var block = new GridCell(CellState.Hard, 255, TileRect.FromQuad(grid.StructureTileBounds));
            foreach (var structure in grid.Blocks)
            {
                for (int y = structure.From.YInt; y <= structure.To.YInt; y++)
                {
                    for (int x = structure.From.XInt; x <= structure.To.XInt; x++)
                    {
                        map.Set(x, y, block);
                    }
                }
            }

            return map;
        }

        /// <summary>The cell at an index; throws outside the grid (use <see cref="CellInRange"/> first).</summary>
        public GridCell this[int x, int y]
        {
            get
            {
                if (!CellInRange(x, y)) throw new ArgumentOutOfRangeException(nameof(x), $"Cell [{x}, {y}] is outside the {Columns} x {Rows} grid.");
                return _cells[x, y];
            }
        }

        /// <summary>The most changes <see cref="ChangesSince"/> can list; a view further behind repaints everything.</summary>
        public const int LoggedChanges = 4096;

        // The cells changed since revision _logBase, in order: change i made revision _logBase + i + 1.
        readonly List<CellIndex> _changes = new List<CellIndex>();
        int _logBase;

        /// <summary>Replaces one cell. Core-only: the grid changes through commands (the editor, A14), never from presentation.</summary>
        internal void Set(int x, int y, GridCell cell)
        {
            if (!CellInRange(x, y)) throw new ArgumentOutOfRangeException(nameof(x), $"Cell [{x}, {y}] is outside the {Columns} x {Rows} grid.");
            _cells[x, y] = cell;
            if (_changes.Count == LoggedChanges)
            {
                _changes.Clear();
                _logBase = Revision;
            }

            _changes.Add(new CellIndex(x, y));
            Revision++;
        }

        /// <summary>Counts every change to a cell, so a view can redraw the grid only when it has changed.</summary>
        public int Revision { get; private set; }

        /// <summary>
        /// Adds to <paramref name="changed"/> every cell changed after <paramref name="revision"/>, oldest first (a cell
        /// changed twice is listed twice), so a view can repaint only those; false, adding nothing, when the log no longer
        /// reaches back that far (the view then repaints everything).
        /// </summary>
        public bool ChangesSince(int revision, List<CellIndex> changed)
        {
            if (changed == null) throw new ArgumentNullException(nameof(changed));
            if (revision < _logBase || revision > Revision) return false;
            for (int i = revision - _logBase; i < _changes.Count; i++)
            {
                changed.Add(_changes[i]);
            }

            return true;
        }

        /// <summary><c>CellInRange</c>: whether an index is on the grid.</summary>
        public bool CellInRange(int x, int y) => x >= 0 && y >= 0 && x < Columns && y < Rows;

        /// <summary><c>CellMatchesState</c>: whether the cell is on the grid and in that state.</summary>
        public bool CellMatchesState(int x, int y, CellState state) => CellInRange(x, y) && _cells[x, y].State == state;

        /// <summary><c>GetGridCoordinates</c>: the cell containing a pixel position (each axis divided and truncated toward zero).</summary>
        public CellIndex GetGridCoordinates(PixelVector location) =>
            new CellIndex((int)(location.X / CellSize), (int)(location.Y / CellSize));

        /// <summary>
        /// <c>CheckFloorCollision</c>: if the cell containing <paramref name="location"/> (the feet) is Hard, snaps
        /// its Y to that cell's top and returns true; otherwise leaves it and returns false.
        /// </summary>
        public bool CheckFloorCollision(ref PixelVector location)
        {
            int x = (int)(location.X / CellSize);
            int y = (int)(location.Y / CellSize);
            if (!CellMatchesState(x, y, CellState.Hard))
            {
                return false;
            }

            location.Y = y * CellSize;
            return true;
        }

        /// <summary>
        /// <c>CheckCeilingCollision</c>: if the cell <paramref name="heightInCells"/> cells above the feet is Hard, snaps
        /// Y to <c>(y + heightInCells + 1) * cellSize</c> (as the original wrote it: one cell below where the head would
        /// clear the ceiling) and returns true.
        /// </summary>
        public bool CheckCeilingCollision(ref PixelVector location, int heightInCells)
        {
            float size = CellSize;
            int x = (int)(location.X / size);
            int y = (int)((location.Y - heightInCells * size) / size);
            if (!CellMatchesState(x, y, CellState.Hard))
            {
                return false;
            }

            location.Y = (int)(y * size + (heightInCells + 1) * size);
            return true;
        }

        /// <summary>
        /// <c>CheckHorizontalCollision</c>: how many of the cells stacked up from the foot row, one column ahead in the
        /// facing direction, are Hard (0 to <paramref name="heightInCells"/>, at most 3). The column is the one 8 px
        /// behind the centre plus one in the facing direction, and the foot row is the one 8 px above the feet, so the
        /// floor underfoot is never a wall. Each hit snaps X to the column's left edge (the original's callers pass a
        /// copy, so only the count matters). <paramref name="direction"/> below zero is left; zero (no facing yet)
        /// counts as right, as the original's did.
        /// </summary>
        public int CheckHorizontalCollision(ref PixelVector location, int direction, int heightInCells)
        {
            float size = CellSize;
            int facing = direction < 0 ? -1 : 1;
            int buffer = facing == 1 ? -8 : 8;
            int x = (int)((location.X + buffer) / size);
            int y = (int)((location.Y - 8) / size);

            int count = 0;
            for (int row = 0; row < 3 && row < heightInCells; row++)
            {
                if (CellMatchesState(x + facing, y - row, CellState.Hard))
                {
                    location.X = (int)(x * size);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// The partial-wall rule (RECONSTRUCTION 4.4): a count equal to the player's height is a full wall, any smaller
        /// non-zero count (a step or ledge) is partial. A one-cell step is not a wall to a three-cell-tall player.
        /// </summary>
        public static WallContact ClassifyWall(int count, int heightInCells)
        {
            if (count <= 0) return WallContact.None;
            return count == heightInCells ? WallContact.Full : WallContact.Partial;
        }
    }
}
