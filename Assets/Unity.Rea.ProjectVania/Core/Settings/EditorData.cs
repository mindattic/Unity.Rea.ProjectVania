namespace Unity.Rea.ProjectVania.Core.Settings
{
    /// <summary>
    /// editor.json: the map editor of RECONSTRUCTION 8 and 12 (<c>MapEditor\MapEditor.cs</c>, <c>Cursor.cs</c>,
    /// <c>TilePalette.cs</c>, <c>Tileset.cs</c>, <c>Tile.cs</c>), every figure and colour the original wrote, in its screen and
    /// map pixels (y down).
    /// </summary>
    public sealed class EditorData
    {
        public EditorCursorData Cursor { get; private set; }

        /// <summary>What a left click paints a cell with (<c>Color.White</c>): the cell turns Hard, opaque, with the selected tile.</summary>
        public SettingsColour PaintColour { get; private set; }

        /// <summary>The tint of the cell under the cursor while no button is held (<c>Color.Yellow</c>).</summary>
        public SettingsColour HighlightColour { get; private set; }

        /// <summary>A palette tile's size in the tileset, which is also the rectangle a painted cell takes (16, the cell size).</summary>
        public int TileSize { get; private set; }

        /// <summary>
        /// <c>Player.localBounds</c>: the drag test's box is these fractions of the idle frame's width and height
        /// (<c>(int)(FrameWidth * 0.4)</c> by <c>(int)(FrameWidth * 0.8)</c>, 25 x 51 for the 64 px frames), standing on the feet.
        /// </summary>
        public Pair PlayerBounds { get; private set; }

        public EditorPaletteData Palette { get; private set; }

        internal static EditorData Read(SettingsReader r) => new EditorData
        {
            Cursor = r.Section("cursor", EditorCursorData.Read),
            PaintColour = r.Choice<SettingsColour>("paintColour"),
            HighlightColour = r.Choice<SettingsColour>("highlightColour"),
            TileSize = r.Integer("tileSize", 1, 4096),
            PlayerBounds = r.Pair("playerBounds", 0f, 1f),
            Palette = r.Section("palette", EditorPaletteData.Read),
        };
    }

    /// <summary>
    /// <c>Cursor</c>: the hand picture (<c>cursor_hand_topleft</c>, drawn with its top-left at the cursor's position), the
    /// World mode's dead zone about the screen centre, and its colour by mouse button (left, else right, else neither).
    /// </summary>
    public sealed class EditorCursorData
    {
        public string Asset { get; private set; }

        /// <summary>The picture's size; World mode puts the cursor half of it (whole pixels) right of and below the view's centre.</summary>
        public Pair Size { get; private set; }

        /// <summary>The mouse must be more than half of this from the screen centre to scroll the camera (<c>deadZone = 2</c>).</summary>
        public float DeadZone { get; private set; }

        public SettingsColour LeftColour { get; private set; }
        public SettingsColour RightColour { get; private set; }
        public SettingsColour Colour { get; private set; }

        internal static EditorCursorData Read(SettingsReader r) => new EditorCursorData
        {
            Asset = r.Text("asset"),
            Size = r.Pair("size", 1f, 4096f),
            DeadZone = r.Number("deadZone", 0f, 100000f),
            LeftColour = r.Choice<SettingsColour>("leftColour"),
            RightColour = r.Choice<SettingsColour>("rightColour"),
            Colour = r.Choice<SettingsColour>("colour"),
        };
    }

    /// <summary>
    /// <c>TilePalette</c>: a translucent background at (x, H - fromBottom), the tileset's tiles drawn at
    /// <see cref="Offset"/> each at its place (BIBLE defect 9 fixed), and the selector over the selected tile.
    /// </summary>
    public sealed class EditorPaletteData
    {
        public string Background { get; private set; }
        public float BackgroundAlpha { get; private set; }
        public int BackgroundX { get; private set; }
        public int BackgroundFromBottom { get; private set; }

        /// <summary>The rectangle the background is stretched over (its picture is 300 x 300, drawn 300 x 295).</summary>
        public Pair BackgroundSize { get; private set; }

        /// <summary>Where the tileset's top-left is drawn, and what the Screen cursor's tile pick is measured from.</summary>
        public Pair Offset { get; private set; }

        /// <summary>The tileset picture's size (<c>Tileset/default.png</c>, 303 x 173): whole tiles across and down are its size over the tile size.</summary>
        public Pair TilesetSize { get; private set; }

        /// <summary><c>Tile.Color</c>'s alpha (255, 255, 255, 128): each palette tile is drawn white at it.</summary>
        public int TileAlpha { get; private set; }

        public string Selector { get; private set; }
        public Pair SelectorSize { get; private set; }

        internal static EditorPaletteData Read(SettingsReader r) => new EditorPaletteData
        {
            Background = r.Text("background"),
            BackgroundAlpha = r.Number("backgroundAlpha", 0f, 1f),
            BackgroundX = r.Integer("backgroundX", 0, 100000),
            BackgroundFromBottom = r.Integer("backgroundFromBottom", 0, 100000),
            BackgroundSize = r.Pair("backgroundSize", 1f, 100000f),
            Offset = r.Pair("offset", 0f, 100000f),
            TilesetSize = r.Pair("tilesetSize", 1f, 100000f),
            TileAlpha = r.Integer("tileAlpha", 0, 255),
            Selector = r.Text("selector"),
            SelectorSize = r.Pair("selectorSize", 1f, 4096f),
        };
    }
}
