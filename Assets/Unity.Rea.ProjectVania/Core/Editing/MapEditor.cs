using System;
using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Characters;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Viewing;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Editing
{
    /// <summary>A whole-pixel rectangle tested as XNA's <c>Rectangle</c> tested one (edges exclusive).</summary>
    public readonly struct PixelBox
    {
        public PixelBox(int x, int y, int width, int height)
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

        /// <summary><c>Rectangle.Intersects</c>: the two overlap by at least a pixel.</summary>
        public bool Intersects(PixelBox other) =>
            other.X < X + Width && X < other.X + other.Width && other.Y < Y + Height && Y < other.Y + other.Height;

        /// <summary><c>Rectangle.Contains(Rectangle)</c>: <paramref name="other"/> lies wholly inside.</summary>
        public bool Contains(PixelBox other) =>
            X <= other.X && other.X + other.Width <= X + Width && Y <= other.Y && other.Y + other.Height <= Y + Height;

        public override string ToString() => $"({X}, {Y}, {Width}, {Height})";
    }

    /// <summary>
    /// One player's map editor (<c>MapEditor\MapEditor.cs</c>, <c>Cursor.cs</c> and <c>TilePalette.cs</c>, RECONSTRUCTION 8
    /// and 12), stepped by the world after the GUI while the game is in the MapEditor state, as the original's component
    /// was updated last. Only the world changes it, applying that player's commands.
    /// <list type="bullet">
    /// <item><b>World mode</b> (Space not held): the mouse is a joystick. Beyond the dead zone about the screen centre it
    /// scrolls the camera (camera.json's <c>editorScrollSteps</c>), the cursor sits on the view's centre plus half its
    /// picture, and the mouse is warped back to the centre (when the window has the focus). Left held drags any character
    /// the cursor's box touches to the cursor and paints the cell under it (Hard, opaque, the selected palette tile);
    /// right held clears it to the fresh cell; with neither, the cell is tinted the highlight colour, and the cell the
    /// cursor has just left loses the highlight.</item>
    /// <item><b>Screen mode</b> (Space held): the cursor follows the mouse on the screen and picks the palette tile under it.</item>
    /// </list>
    /// Fixed (BIBLE defects 9, 10): the palette's pick and its arrow steps stay on the sheet, and nothing is painted,
    /// highlighted or unhighlighted outside the grid, where the original threw.
    /// </summary>
    public sealed class MapEditor
    {
        readonly EditorData _data;
        readonly int _frameSize;
        PixelVector _cursor;
        PixelVector _pointer = new PixelVector(PixelPerfect.ReferenceWidth / 2f, PixelPerfect.ReferenceHeight / 2f);
        bool _left;
        bool _right;
        bool _windowActive = true;

        /// <param name="data">editor.json; without it the editor keeps where its cursor was put and does nothing else.</param>
        /// <param name="frameSize">The characters' frame size (animations.json), which their drag box is cut from; 0 drags nothing.</param>
        public MapEditor(EditorData data, int frameSize)
        {
            _data = data;
            _frameSize = frameSize;
            Colour = data != null ? data.Cursor.Colour.ToRgba() : Rgba.White;
            if (data != null)
            {
                TilesAcross = Math.Max(1, (int)data.Palette.TilesetSize.X / data.TileSize);
                TilesDown = Math.Max(1, (int)data.Palette.TilesetSize.Y / data.TileSize);
                CursorOffset = new PixelVector((int)data.Cursor.Size.X / 2, (int)data.Cursor.Size.Y / 2);
            }
        }

        public EditorData Data => _data;

        /// <summary>World (the mouse is a joystick over the map) or Screen (Space held: the mouse picks a palette tile).</summary>
        public EditorCursorMode Mode { get; private set; }

        /// <summary>
        /// The cursor's position (<c>Cursor.Position</c>): in World mode a map point (pixels, y down), the top-left of the
        /// hand; in Screen mode a point on the 800 x 600 screen. LooseCursor follows it, as the original's camera did.
        /// </summary>
        public PixelVector Cursor => _cursor;

        /// <summary>World mode puts the cursor this far right of and below the view's centre: half the hand picture, whole pixels (6, 9).</summary>
        public PixelVector CursorOffset { get; }

        /// <summary>The hand's tint: the left-button colour while left is held, else the right's, else white.</summary>
        public Rgba Colour { get; private set; }

        /// <summary>The cell under the cursor (<c>SelectedCell</c>), set by every World-mode step; (0, 0) before the first.</summary>
        public CellIndex SelectedCell { get; private set; }

        /// <summary>The cell the step before selected (<c>PreviousSelectedCell</c>).</summary>
        public CellIndex PreviousSelectedCell { get; private set; }

        /// <summary>The palette tile a left click paints with (<c>TilePalette.SelectedTile</c>): column and row on the tileset.</summary>
        public CellIndex SelectedTile { get; private set; }

        /// <summary>Whole tiles across the tileset (303 / 16 = 18).</summary>
        public int TilesAcross { get; } = 1;

        /// <summary>Whole tiles down the tileset (173 / 16 = 10).</summary>
        public int TilesDown { get; } = 1;

        /// <summary>The mouse as the editor last read it (screen pixels); World mode warps it back to the centre after each step.</summary>
        public PixelVector Pointer => _pointer;

        /// <summary>The source rectangle a left click paints: the selected tile's square of the tileset.</summary>
        public TileRect SelectedTileRect =>
            _data == null ? default : new TileRect(SelectedTile.X * _data.TileSize, SelectedTile.Y * _data.TileSize, _data.TileSize, _data.TileSize);

        /// <summary><c>ToggleMapEditor</c>: the cursor goes to <paramref name="at"/> (the driven character's feet).</summary>
        internal void Open(PixelVector at) => _cursor = at;

        /// <summary>
        /// Space held or not, read every editor step. Holding it also puts the cursor at <c>PreviousGuiPosition</c>, which
        /// nothing ever set, (0, 0), before the step's update moves it to the mouse.
        /// </summary>
        internal void SetMode(EditorCursorMode mode)
        {
            if (mode == EditorCursorMode.Screen)
            {
                _cursor = new PixelVector(0f, 0f);
            }

            Mode = mode;
        }

        /// <summary>The mouse for this step's update; it holds until the next report (or World mode's warp).</summary>
        internal void Point(PointEditor pointer)
        {
            _pointer = new PixelVector(pointer.X, pointer.Y);
            _left = pointer.Left;
            _right = pointer.Right;
            _windowActive = pointer.WindowActive;
        }

        /// <summary>
        /// An arrow press (<c>SelectTileLeft</c> ...): the selected tile steps one way, never off the sheet (the original's
        /// right and down steps had no bound, BIBLE defect 10).
        /// </summary>
        internal void StepTile(int dx, int dy)
        {
            var x = SelectedTile.X;
            var y = SelectedTile.Y;
            if (dx < 0 && x > 0) x--;
            if (dx > 0 && x < TilesAcross - 1) x++;
            if (dy < 0 && y > 0) y--;
            if (dy > 0 && y < TilesDown - 1) y++;
            SelectedTile = new CellIndex(x, y);
        }

        /// <summary>
        /// <c>MapEditor.Update</c>: the cursor's update (<c>Cursor.Update</c>), then by mode the World mode's drag, paint,
        /// erase or highlight, or the Screen mode's tile pick.
        /// </summary>
        internal void Step(WorldCamera camera, Map map, IReadOnlyList<PlayerCharacter> characters, float mouseScroll)
        {
            if (_data == null)
            {
                return;
            }

            UpdateCursor(camera, mouseScroll);
            if (Mode == EditorCursorMode.World)
            {
                if (_left && characters != null) Drag(characters);
                if (map != null) Edit(map);

                // Mouse.SetPosition(W / 2, H / 2) when the window is active: the mouse stays a joystick.
                if (_windowActive)
                {
                    _pointer = new PixelVector(PixelPerfect.ReferenceWidth / 2f, PixelPerfect.ReferenceHeight / 2f);
                }
            }
            else
            {
                PickTile();
            }
        }

        void UpdateCursor(WorldCamera camera, float mouseScroll)
        {
            var cursor = _data.Cursor;
            if (Mode == EditorCursorMode.World)
            {
                // The "3D" world cursor: beyond half the dead zone either side of the centre the camera scrolls.
                var centreX = PixelPerfect.ReferenceWidth / 2;
                var centreY = PixelPerfect.ReferenceHeight / 2;
                var half = cursor.DeadZone / 2f;
                if (camera != null)
                {
                    if (_pointer.X > centreX + half) camera.Scroll(1, 0, mouseScroll);
                    else if (_pointer.X < centreX - half) camera.Scroll(-1, 0, mouseScroll);

                    if (_pointer.Y < centreY - half) camera.Scroll(0, -1, mouseScroll);
                    else if (_pointer.Y > centreY + half) camera.Scroll(0, 1, mouseScroll);

                    _cursor = new PixelVector(-camera.Position.X + CursorOffset.X, -camera.Position.Y + CursorOffset.Y);
                }
            }
            else
            {
                _cursor = KeepOnScreen(_pointer.X, _pointer.Y, cursor.Size);
            }

            Colour = (_left ? cursor.LeftColour : _right ? cursor.RightColour : cursor.Colour).ToRgba();
        }

        /// <summary>
        /// A Screen-mode cursor (<c>Cursor.Update</c>): the mouse, kept so the whole picture of <paramref name="size"/> stays
        /// on the 800 x 600 screen, in the original's order (left, right, top, bottom). The GUI's cursor over the menu is one too.
        /// </summary>
        public static PixelVector KeepOnScreen(float x, float y, Pair size)
        {
            if (x < 0) x = 0;
            if (x > PixelPerfect.ReferenceWidth - size.X) x = PixelPerfect.ReferenceWidth - size.X;
            if (y < 0) y = 0;
            if (y > PixelPerfect.ReferenceHeight - size.Y) y = PixelPerfect.ReferenceHeight - size.Y;
            return new PixelVector(x, y);
        }

        /// <summary>The cursor's box (<c>Cursor.Bounds</c>): the hand picture at the cursor.</summary>
        public PixelBox CursorBox =>
            _data == null ? default : new PixelBox((int)_cursor.X, (int)_cursor.Y, (int)_data.Cursor.Size.X, (int)_data.Cursor.Size.Y);

        /// <summary>
        /// <c>Player.Bounds()</c>: a box of editor.json's fractions of the frame (25 x 51 of 64), centred on the feet and
        /// standing on them; the drag's test.
        /// </summary>
        public PixelBox BoundsOf(PlayerCharacter character)
        {
            if (_data == null || character == null) return default;
            var width = (int)(_frameSize * (double)(decimal)_data.PlayerBounds.X);
            var height = (int)(_frameSize * (double)(decimal)_data.PlayerBounds.Y);
            return new PixelBox((int)character.Position.X - width / 2, (int)character.Position.Y - height, width, height);
        }

        /// <summary>Left held over a character (<c>cursor.Intersects(player.Bounds())</c>): it is put at the cursor, and falls when let go.</summary>
        void Drag(IReadOnlyList<PlayerCharacter> characters)
        {
            if (_frameSize <= 0) return;
            for (var i = 0; i < characters.Count; i++)
            {
                var box = BoundsOf(characters[i]);
                var cursor = CursorBox;
                if (cursor.Contains(box) || box.Contains(cursor) || cursor.Intersects(box))
                {
                    characters[i].Place(_cursor);
                }
            }
        }

        void Edit(Map map)
        {
            PreviousSelectedCell = SelectedCell;
            SelectedCell = map.GetGridCoordinates(_cursor);
            var highlight = _data.HighlightColour.ToRgba();

            if (map.CellInRange(SelectedCell.X, SelectedCell.Y))
            {
                var cell = map[SelectedCell.X, SelectedCell.Y];
                GridCell next;
                if (_left)
                {
                    next = new GridCell(CellState.Hard, _data.PaintColour.ToRgba(), SelectedTileRect);
                }
                else if (_right)
                {
                    next = map.Fresh;
                }
                else
                {
                    next = cell.WithTint(highlight);
                }

                Change(map, SelectedCell, cell, next);
            }

            // The cell just left loses the highlight it still has: back to the fresh tint if empty, else opaque white.
            if (SelectedCell != PreviousSelectedCell && map.CellInRange(PreviousSelectedCell.X, PreviousSelectedCell.Y))
            {
                var left = map[PreviousSelectedCell.X, PreviousSelectedCell.Y];
                if (left.Tint == highlight)
                {
                    var tint = left.State == CellState.Empty ? map.Fresh.Tint : _data.PaintColour.ToRgba();
                    Change(map, PreviousSelectedCell, left, left.WithTint(tint));
                }
            }
        }

        // The original wrote the cell every frame; only a real change counts as one, so a view repaints only those.
        static void Change(Map map, CellIndex at, GridCell from, GridCell to)
        {
            if (!from.Equals(to))
            {
                map.Set(at.X, at.Y, to);
            }
        }

        /// <summary>
        /// The Screen mode's pick: the tile nearest the cursor, <c>round((cursor - Offset) / 16)</c> (to even, as
        /// <c>Math.Round</c>), kept on the sheet in both axes (the original let the row reach one past the last, BIBLE
        /// defect 9).
        /// </summary>
        void PickTile()
        {
            var palette = _data.Palette;
            var size = (float)_data.TileSize;
            var x = (int)Math.Round((double)((_cursor.X - palette.Offset.X) / size), 0);
            var y = (int)Math.Round((double)((_cursor.Y - palette.Offset.Y) / size), 0);
            SelectedTile = new CellIndex(Clamp(x, 0, TilesAcross - 1), Clamp(y, 0, TilesDown - 1));
        }

        static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
