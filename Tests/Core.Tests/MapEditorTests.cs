using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Editing;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    /// <summary>
    /// U.VAN-US-A14: the map editor (RECONSTRUCTION 8, 12). M or the middle button opens it; the mouse is a joystick that
    /// scrolls the Free camera beyond a dead zone and is warped back to the centre; the cursor sits on the view's centre;
    /// left paints a Hard cell from the selected palette tile, right clears it, no button tints it yellow; a character can
    /// be dragged; Space puts the cursor on the screen to pick a tile; nothing happens outside the grid.
    /// </summary>
    public class MapEditorTests
    {
        static readonly PlayerId P1 = PlayerId.One;
        static readonly GameSettings Settings = GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));
        static readonly Rgba Yellow = new Rgba(255, 255, 0, 255);
        static readonly Rgba White = Rgba.White;
        static readonly Rgba Fresh = new Rgba(255, 255, 255, 64);
        const float CentreX = 400f;
        const float CentreY = 300f;

        static GameWorld World(EditorData editor = null)
        {
            var s = Settings;
            return new GameWorld(Map.FromSettings(s.Grid), s.Player, s.Characters.Find(s.Characters.DefaultCharacter), s.Names, new Random(3), s.Camera,
                s.Menu, s.Console, s.Audio, s.Hud, s.Animations, editor ?? s.Editor);
        }

        static void Steps(GameWorld world, int count)
        {
            for (var i = 0; i < count; i++)
            {
                world.Advance(1.0 / 60);
            }
        }

        /// <summary>A world whose camera has eased onto the landed character, then put in the editor for one step.</summary>
        static GameWorld InEditor()
        {
            var world = World();
            Steps(world, 180);
            world.Submit(new ToggleMapEditor(P1));
            Point(world, CentreX, CentreY);
            Steps(world, 1);
            return world;
        }

        /// <summary>One editor step with the mouse at (x, y) and those buttons held (Space not held).</summary>
        static void Point(GameWorld world, float x, float y, bool left = false, bool right = false, bool screen = false, bool active = true)
        {
            world.Submit(new SetEditorCursor(P1, screen ? EditorCursorMode.Screen : EditorCursorMode.World));
            world.Submit(new PointEditor(P1, x, y, left, right, active));
        }

        static void Step(GameWorld world, float x, float y, bool left = false, bool right = false, bool screen = false)
        {
            Point(world, x, y, left, right, screen);
            Steps(world, 1);
        }

        // ---- data ----

        [Test]
        public void EditorJsonHoldsTheOriginalsFigures()
        {
            var e = Settings.Editor;
            Assert.That((e.Cursor.Asset, e.Cursor.Size.X, e.Cursor.Size.Y, e.Cursor.DeadZone), Is.EqualTo(("cursor_hand_topleft", 13f, 18f, 2f)));
            Assert.That((e.Cursor.LeftColour, e.Cursor.RightColour, e.Cursor.Colour), Is.EqualTo((SettingsColour.Yellow, SettingsColour.Red, SettingsColour.White)));
            Assert.That((e.PaintColour, e.HighlightColour, e.TileSize), Is.EqualTo((SettingsColour.White, SettingsColour.Yellow, 16)));
            Assert.That((e.PlayerBounds.X, e.PlayerBounds.Y), Is.EqualTo((0.4f, 0.8f)));
            var p = e.Palette;
            Assert.That((p.Background, p.BackgroundAlpha, p.BackgroundX, p.BackgroundFromBottom), Is.EqualTo(("tilepalette_background", 0.4f, 5, 305)));
            Assert.That((p.BackgroundSize.X, p.BackgroundSize.Y, p.Offset.X, p.Offset.Y), Is.EqualTo((300f, 295f, 16f, 300f)));
            Assert.That((p.TilesetSize.X, p.TilesetSize.Y, p.TileAlpha, p.Selector), Is.EqualTo((303f, 173f, 128, "square_selector")));
            Assert.That(Settings.Camera.EditorScrollSteps.X, Is.EqualTo(2f), "the mouse joystick scrolls by 2");

            var editor = new MapEditor(e, Settings.Animations.FrameSize);
            Assert.That((editor.TilesAcross, editor.TilesDown), Is.EqualTo((18, 10)), "303 x 173 holds 18 x 10 whole 16 px tiles");
            Assert.That((editor.CursorOffset.X, editor.CursorOffset.Y), Is.EqualTo((6f, 9f)), "half the 13 x 18 hand, whole pixels");
        }

        [Test]
        public void ATileThatIsNotACellIsRejected()
        {
            var shipped = new DirectorySettingsSource(GameSettingsTests.ShippedFolder);
            var patched = new MemorySettingsSource(shipped).Set(GameSettings.EditorFile,
                shipped.Read(GameSettings.EditorFile).Replace("\"tileSize\": 16", "\"tileSize\": 8"));
            var error = Assert.Throws<SettingsException>(() => GameSettings.Load(patched));
            Assert.That(string.Join("\n", error.Errors), Does.Contain("editor.json").And.Contain("tileSize"));
        }

        // ---- the World cursor and the mouse joystick (8, 12.1) ----

        [Test]
        public void EnteringTheEditorPutsTheCursorOnTheViewsCentrePlusHalfTheHand()
        {
            var world = World();
            Steps(world, 180);
            world.Submit(new ToggleMapEditor(P1));
            Assert.That(world.EditorCursor(P1).X, Is.EqualTo(0f), "not opened yet");
            Steps(world, 1);
            var camera = world.Camera(P1);
            Assert.That(camera.Mode, Is.EqualTo(CameraMode.Free));
            var editor = world.Editor(P1);
            Assert.That(editor.Cursor.X, Is.EqualTo(-camera.Position.X + 6f));
            Assert.That(editor.Cursor.Y, Is.EqualTo(-camera.Position.Y + 9f));
            Assert.That(editor.SelectedCell, Is.EqualTo(world.Map.GetGridCoordinates(editor.Cursor)));
        }

        [TestCase(401f, 300f, 0f, 0f)]
        [TestCase(402f, 300f, -2f, 0f)]
        [TestCase(398f, 300f, 2f, 0f)]
        [TestCase(399f, 300f, 0f, 0f)]
        [TestCase(400f, 302f, 0f, -2f)]
        [TestCase(400f, 298f, 0f, 2f)]
        [TestCase(800f, 0f, -2f, 2f)]
        public void TheMouseBeyondTheDeadZoneScrollsTheCameraByTwo(float x, float y, float vx, float vy)
        {
            var world = InEditor();
            var camera = world.Camera(P1);
            Steps(world, 30); // the entry's velocity damps away
            Assert.That((camera.Velocity.X, camera.Velocity.Y), Is.EqualTo((0f, 0f)));
            Step(world, x, y);
            Assert.That((camera.Velocity.X, camera.Velocity.Y), Is.EqualTo((vx, vy)), "ScrollRight(2) adds -2 to the velocity (Position is the negative centre)");
        }

        [Test]
        public void TheMouseIsWarpedBackToTheCentreOnlyWhileTheWindowIsActive()
        {
            var world = InEditor();
            var camera = world.Camera(P1);
            Step(world, 500f, 200f);
            Assert.That((world.Editor(P1).Pointer.X, world.Editor(P1).Pointer.Y), Is.EqualTo((CentreX, CentreY)));
            var before = camera.Velocity.X;
            Steps(world, 1);
            Assert.That(camera.Velocity.X, Is.EqualTo(before * 0.7f).Within(1e-4f), "warped: the next step reads the mouse at rest");

            Point(world, 500f, 200f, active: false);
            Steps(world, 1);
            Assert.That((world.Editor(P1).Pointer.X, world.Editor(P1).Pointer.Y), Is.EqualTo((500f, 200f)), "an inactive window keeps its mouse");
            before = camera.Velocity.X;
            Steps(world, 1);
            Assert.That(camera.Velocity.X, Is.EqualTo(before * 0.7f - 2f).Within(1e-4f), "so it goes on scrolling");
        }

        [Test]
        public void TheCursorIsTintedByTheButtonsHeld()
        {
            var world = InEditor();
            Step(world, CentreX, CentreY, left: true);
            Assert.That(world.Editor(P1).Colour, Is.EqualTo(Yellow));
            Step(world, CentreX, CentreY, right: true);
            Assert.That(world.Editor(P1).Colour, Is.EqualTo(new Rgba(255, 0, 0, 255)));
            Step(world, CentreX, CentreY, left: true, right: true);
            Assert.That(world.Editor(P1).Colour, Is.EqualTo(Yellow), "left first");
            Step(world, CentreX, CentreY);
            Assert.That(world.Editor(P1).Colour, Is.EqualTo(White));
        }

        // ---- painting (12.1) ----

        [Test]
        public void LeftPaintsAHardCellFromTheSelectedTileAndRightClearsIt()
        {
            var world = InEditor();
            var editor = world.Editor(P1);
            world.Submit(new StepEditor(P1, 1, 0, selectTile: true, moveCell: false));
            world.Submit(new StepEditor(P1, 0, 1, selectTile: true, moveCell: false));
            world.Submit(new StepEditor(P1, 1, 0, selectTile: true, moveCell: false));
            Step(world, CentreX, CentreY, left: true);
            Assert.That(editor.SelectedTile, Is.EqualTo(new CellIndex(2, 1)));
            var at = editor.SelectedCell;
            var cell = world.Map[at.X, at.Y];
            Assert.That((cell.State, cell.Tint, cell.Tile), Is.EqualTo((CellState.Hard, White, new TileRect(32, 16, 16, 16))));

            Step(world, CentreX, CentreY, right: true);
            Assert.That(world.Map[at.X, at.Y], Is.EqualTo(world.Map.Fresh));
            Assert.That(world.Map[at.X, at.Y].Tint, Is.EqualTo(Fresh));
        }

        [Test]
        public void APaintedCellIsSolidOnceTheGameIsBackInPlay()
        {
            var world = InEditor();
            var at = world.Editor(P1).SelectedCell;
            Step(world, CentreX, CentreY, left: true);
            world.Submit(new ToggleMapEditor(P1));
            Steps(world, 1);
            Assert.That(world.State, Is.EqualTo(GameState.Play));
            Assert.That(world.Map.CellMatchesState(at.X, at.Y, CellState.Hard), Is.True);
            Assert.That(MapDrawing.IsDrawn(world.Map[at.X, at.Y], GameState.Play), Is.True);
        }

        [Test]
        public void NoButtonHighlightsTheCellUnderTheCursorAndLeavingItUndoesTheHighlight()
        {
            // Up off the floor into the air, then let the camera come to rest.
            var world = InEditor();
            var editor = world.Editor(P1);
            for (var i = 0; i < 10; i++) Step(world, CentreX, 0f);
            for (var i = 0; i < 60; i++) Step(world, CentreX, CentreY);
            var first = editor.SelectedCell;
            Assert.That(world.Map[first.X, first.Y].Tint, Is.EqualTo(Yellow), "the cell under the cursor is highlighted");
            Assert.That(world.Map[first.X, first.Y].State, Is.EqualTo(CellState.Empty), "only the colour changes");

            // Scroll right until the cursor is over another cell.
            while (editor.SelectedCell == first)
            {
                Step(world, 800f, CentreY);
            }

            Assert.That(world.Map[first.X, first.Y].Tint, Is.EqualTo(Fresh), "an empty cell goes back to the fresh tint");
            Assert.That(world.Map[editor.SelectedCell.X, editor.SelectedCell.Y].Tint, Is.EqualTo(Yellow));
        }

        [Test]
        public void AHighlightedHardCellGoesBackToWhite()
        {
            var world = InEditor();
            var editor = world.Editor(P1);
            var first = editor.SelectedCell;
            Step(world, CentreX, CentreY, left: true);
            Step(world, CentreX, CentreY);
            Assert.That(world.Map[first.X, first.Y].Tint, Is.EqualTo(Yellow));
            Assert.That(world.Map[first.X, first.Y].State, Is.EqualTo(CellState.Hard));
            while (editor.SelectedCell == first)
            {
                Step(world, CentreX, 600f);
            }

            Assert.That(world.Map[first.X, first.Y].Tint, Is.EqualTo(White));
            Assert.That(world.Map[first.X, first.Y].State, Is.EqualTo(CellState.Hard));
        }

        [Test]
        public void AnUnchangedCellIsNotRewritten()
        {
            var world = InEditor();
            var revision = world.Map.Revision;
            Steps(world, 10);
            Step(world, CentreX, CentreY);
            Assert.That(world.Map.Revision, Is.EqualTo(revision), "the highlight is already there");
        }

        [Test]
        public void NothingIsPaintedOrHighlightedOutsideTheGrid()
        {
            // Scroll to the map's right edge: the camera's centre stops at 1900, so the cursor reaches 1906, column 119 of 118.
            var world = InEditor();
            var editor = world.Editor(P1);
            for (var i = 0; i < 600 && -world.Camera(P1).Position.X < world.Map.Width; i++)
            {
                Step(world, 800f, CentreY);
            }

            Assert.That(editor.Cursor.X, Is.EqualTo(world.Map.Width + 6f));
            Assert.That(world.Map.CellInRange(editor.SelectedCell.X, editor.SelectedCell.Y), Is.False);
            var revision = world.Map.Revision;
            Assert.DoesNotThrow(() => Step(world, CentreX, CentreY, left: true));
            Assert.DoesNotThrow(() => Step(world, CentreX, CentreY, right: true));
            Assert.DoesNotThrow(() => Step(world, CentreX, CentreY));
            Assert.That(world.Map.Revision, Is.EqualTo(revision), "the original threw here (BIBLE defect 10)");
        }

        [Test]
        public void TheEditorDoesNothingOutsideItsState()
        {
            var world = World();
            Steps(world, 180);
            var revision = world.Map.Revision;
            Point(world, CentreX, CentreY, left: true);
            Steps(world, 5);
            Assert.That(world.Map.Revision, Is.EqualTo(revision));
        }

        // ---- dragging a character (12.1) ----

        [Test]
        public void LeftHeldOverACharacterDragsItToTheCursorAndPaintsTheCellItStandsOn()
        {
            var world = InEditor();
            var editor = world.Editor(P1);
            var morris = world.Driven(P1);
            var dragged = false;
            // Scroll up with left held until the hand's box touches the character's 25 x 51 box.
            for (var i = 0; i < 120 && !dragged; i++)
            {
                Step(world, CentreX, 0f, left: true);
                dragged = morris.Position.X == editor.Cursor.X && morris.Position.Y == editor.Cursor.Y;
            }

            Assert.That(dragged, Is.True);
            var box = editor.BoundsOf(morris);
            Assert.That((box.Width, box.Height), Is.EqualTo((25, 51)));
            Assert.That((box.X, box.Y), Is.EqualTo(((int)morris.Position.X - 12, (int)morris.Position.Y - 51)));

            for (var i = 0; i < 20; i++)
            {
                Step(world, CentreX, 0f);
            }

            // Left both drags and paints, so the cell under the dragged feet is Hard: back in Play the character stands on it.
            var held = world.Map.GetGridCoordinates(morris.Position);
            Assert.That(world.Map.CellMatchesState(held.X, held.Y, CellState.Hard), Is.True);
            world.Submit(new ToggleMapEditor(P1));
            Steps(world, 2);
            Assert.That(morris.Position.Y, Is.EqualTo(held.Y * 16f), "it lands on the top of the painted cell");
            Assert.That(morris.Terrain, Is.EqualTo(Characters.TerrainState.OnGround));
        }

        [Test]
        public void TheDragBoxIsTheOriginalsPlayerBounds()
        {
            var editor = new MapEditor(Settings.Editor, 64);
            var world = World();
            var box = editor.BoundsOf(world.Characters[0]);
            Assert.That((box.X, box.Y, box.Width, box.Height), Is.EqualTo((500 - 12, 100 - 51, 25, 51)));
            Assert.That(new PixelBox(0, 0, 13, 18).Intersects(new PixelBox(13, 0, 5, 5)), Is.False, "edges are exclusive");
            Assert.That(new PixelBox(0, 0, 13, 18).Intersects(new PixelBox(12, 17, 5, 5)), Is.True);
        }

        // ---- Space: the Screen cursor and the palette (12.1, 12.2) ----

        [Test]
        public void SpaceHeldPicksThePaletteTileUnderTheMouseAndPaintsNothing()
        {
            var world = InEditor();
            var editor = world.Editor(P1);
            var revision = world.Map.Revision;
            var camera = world.Camera(P1).Position;

            Step(world, 16f + 16 * 5, 300f + 16 * 3, left: true, screen: true);
            Assert.That(editor.Mode, Is.EqualTo(EditorCursorMode.Screen));
            Assert.That((editor.Cursor.X, editor.Cursor.Y), Is.EqualTo((96f, 348f)), "the cursor is the mouse");
            Assert.That(editor.SelectedTile, Is.EqualTo(new CellIndex(5, 3)));
            Assert.That(world.Map.Revision, Is.EqualTo(revision), "Screen mode paints nothing");
            Assert.That(world.Camera(P1).Position.X, Is.EqualTo(camera.X), "nor scrolls");
        }

        [TestCase(16f + 8f, 300f, 0, 0)]      // 0.5 rounds to even: 0
        [TestCase(16f + 24f, 300f, 2, 0)]     // 1.5 rounds to even: 2
        [TestCase(16f + 23f, 300f, 1, 0)]
        [TestCase(0f, 0f, 0, 0)]
        [TestCase(790f, 590f, 17, 9)]         // clamped to the sheet's last column and row (the original reached row 10)
        public void ThePickRoundsAndStaysOnTheSheet(float x, float y, int tx, int ty)
        {
            var world = InEditor();
            Step(world, x, y, screen: true);
            Assert.That(world.Editor(P1).SelectedTile, Is.EqualTo(new CellIndex(tx, ty)));
        }

        [Test]
        public void TheScreenCursorStaysOnTheScreen()
        {
            var world = InEditor();
            Step(world, 900f, 700f, screen: true);
            Assert.That((world.Editor(P1).Cursor.X, world.Editor(P1).Cursor.Y), Is.EqualTo((787f, 582f)));
            Step(world, -20f, -5f, screen: true);
            Assert.That((world.Editor(P1).Cursor.X, world.Editor(P1).Cursor.Y), Is.EqualTo((0f, 0f)));
        }

        [Test]
        public void ReleasingSpacePutsTheCursorBackOnTheView()
        {
            var world = InEditor();
            Step(world, 100f, 100f, screen: true);
            Step(world, CentreX, CentreY);
            var camera = world.Camera(P1).Position;
            Assert.That((world.Editor(P1).Cursor.X, world.Editor(P1).Cursor.Y), Is.EqualTo((-camera.X + 6f, -camera.Y + 9f)));
        }

        [Test]
        public void TheArrowStepsMoveTheTileButNeverOffTheSheet()
        {
            var world = InEditor();
            var editor = world.Editor(P1);
            world.Submit(new StepEditor(P1, -1, 0, true, false));
            world.Submit(new StepEditor(P1, 0, -1, true, false));
            Steps(world, 1);
            Assert.That(editor.SelectedTile, Is.EqualTo(new CellIndex(0, 0)), "left and up stop at 0, as the original's did");

            for (var i = 0; i < 40; i++)
            {
                world.Submit(new StepEditor(P1, 1, 0, true, false));
                world.Submit(new StepEditor(P1, 0, 1, true, false));
            }

            Steps(world, 1);
            Assert.That(editor.SelectedTile, Is.EqualTo(new CellIndex(17, 9)), "right and down stop at the sheet's edge (BIBLE defect 10)");

            world.Submit(new StepEditor(P1, -1, 0, selectTile: false, moveCell: true));
            Steps(world, 1);
            Assert.That(editor.SelectedTile, Is.EqualTo(new CellIndex(17, 9)), "Ctrl keeps the tile");
        }

        // ---- a world without editor.json ----

        [Test]
        public void WithoutEditorDataTheCursorOnlyKeepsWhereItWasPut()
        {
            var s = Settings;
            var world = new GameWorld(Map.FromSettings(s.Grid), s.Player, s.Characters.Find(s.Characters.DefaultCharacter), s.Names, new Random(3), s.Camera);
            world.Submit(new ToggleMapEditor(P1));
            Point(world, 800f, 0f, left: true);
            var revision = world.Map.Revision;
            Steps(world, 3);
            Assert.That(world.EditorCursor(P1).X, Is.EqualTo(world.Driven(P1).Position.X));
            Assert.That(world.Map.Revision, Is.EqualTo(revision));
        }

        // ---- the one-cell repaint the editor's changes need (BIBLE, A8) ----

        [Test]
        public void TheMapListsTheCellsChangedSinceARevision()
        {
            var map = Map.FromSettings(Settings.Grid);
            var start = map.Revision;
            var world = new GameWorld(map);
            var changed = new List<CellIndex>();
            Assert.That(map.ChangesSince(start, changed), Is.True);
            Assert.That(changed, Is.Empty);

            var editorWorld = InEditor();
            var m = editorWorld.Map;
            var before = m.Revision;
            Step(editorWorld, CentreX, CentreY, left: true);
            changed.Clear();
            Assert.That(m.ChangesSince(before, changed), Is.True);
            Assert.That(changed, Is.EqualTo(new[] { editorWorld.Editor(P1).SelectedCell }));
            Assert.That(m.ChangesSince(-1, changed), Is.False, "before the log began");
            Assert.That(m.ChangesSince(m.Revision + 1, changed), Is.False, "a revision not reached");
            Assert.That(world.Map, Is.SameAs(map));
        }

        [Test]
        public void TheLogDropsWhatIsTooOldAndSaysSo()
        {
            var world = InEditor();
            var map = world.Map;
            var old = map.Revision;
            for (var i = 0; i <= Map.LoggedChanges; i++)
            {
                Step(world, CentreX, CentreY, left: i % 2 == 0, right: i % 2 == 1);
            }

            Assert.That(map.ChangesSince(old, new List<CellIndex>()), Is.False);
            var recent = new List<CellIndex>();
            Assert.That(map.ChangesSince(map.Revision - 1, recent), Is.True);
            Assert.That(recent, Has.Count.EqualTo(1));
        }

        [Test]
        public void RepaintingOneCellGivesTheWholeRepaintsPixels()
        {
            var world = InEditor();
            var map = world.Map;
            var tileset = new PixelImage(303, 173);
            for (var y = 0; y < tileset.Height; y++)
            {
                for (var x = 0; x < tileset.Width; x++)
                {
                    tileset[x, y] = new Rgba((byte)(x * 7), (byte)(y * 5), (byte)(x + y), (byte)(128 + (x * y) % 128));
                }
            }

            var canvas = MapDrawing.CellCanvas(map);
            MapDrawing.PaintCells(map, world.State, tileset, canvas);
            var before = map.Revision;
            world.Submit(new StepEditor(P1, 1, 0, true, false));
            Step(world, CentreX, CentreY, left: true);
            while (world.Editor(P1).SelectedCell == world.Editor(P1).PreviousSelectedCell)
            {
                Step(world, 800f, CentreY);
            }

            var changed = new List<CellIndex>();
            Assert.That(map.ChangesSince(before, changed), Is.True);
            Assert.That(changed.Count, Is.GreaterThanOrEqualTo(3), "painted, unhighlighted and the new highlight");
            foreach (var cell in changed)
            {
                Assert.That(MapDrawing.RepaintCell(map, cell.X, cell.Y, world.State, tileset, canvas), Is.True);
            }

            var whole = MapDrawing.CellCanvas(map);
            MapDrawing.PaintCells(map, world.State, tileset, whole);
            Assert.That(canvas.Pixels, Is.EqualTo(whole.Pixels));
        }
    }
}
