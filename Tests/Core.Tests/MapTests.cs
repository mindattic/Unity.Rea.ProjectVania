using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    /// <summary>
    /// The 16 px grid and its queries (U.VAN-US-A5, RECONSTRUCTION 5 and 4.4). Every expected value is worked by hand
    /// from the original's <c>Map.cs</c> arithmetic: divide by 16, truncate toward zero, test the cell, snap.
    /// </summary>
    public class MapTests
    {
        static Map Bridge() => Map.FromSettings(GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder)).Grid);

        static Map Empty() => new Map(1900, 1200, 16, new GridCell(CellState.Empty, 64, new TileRect(0, 0, 16, 16)));

        static readonly GridCell Hard = new GridCell(CellState.Hard, 255, new TileRect(16, 0, 16, 16));

        static Map EmptyWithFloor()
        {
            var map = Empty();
            for (int x = 0; x < map.Columns; x++) map.Set(x, 30, Hard);
            return map;
        }

        static PixelVector At(float x, float y) => new PixelVector(x, y);

        // ---- The grid and the bridge level ----

        [Test]
        public void Grid_Is118By75CellsOf16PxOverA1900By1200Map()
        {
            var map = Bridge();
            Assert.That(map.Columns, Is.EqualTo(118));
            Assert.That(map.Rows, Is.EqualTo(75));
            Assert.That(map.CellSize, Is.EqualTo(16));
            Assert.That(map.Width, Is.EqualTo(1900));
            Assert.That(map.Height, Is.EqualTo(1200));
        }

        // U.VAN-T25's block structures (grid.json structures), [column, row] rectangles, both corners included.
        static readonly (int X0, int Y0, int X1, int Y1)[] Blocks =
        {
            (35, 27, 36, 29), (37, 24, 39, 29), (32, 21, 33, 21), (29, 18, 30, 18), (32, 15, 33, 15), (35, 12, 37, 12),
        };

        static bool InBlock(int x, int y) => Blocks.Any(b => x >= b.X0 && x <= b.X1 && y >= b.Y0 && y <= b.Y1);

        [Test]
        public void Bridge_IsAFullFloorOnRow30AndFullHeightWallsOnColumns20And40_PlusTheBlockStructures()
        {
            var map = Bridge();
            for (int y = 0; y < map.Rows; y++)
            {
                for (int x = 0; x < map.Columns; x++)
                {
                    bool expected = y == 30 || x == 20 || x == 40 || InBlock(x, y);
                    Assert.That(map[x, y].State, Is.EqualTo(expected ? CellState.Hard : CellState.Empty), $"[{x}, {y}]");
                }
            }
        }

        [Test]
        public void Bridge_Has266HardCellsPlusThe33OfTheStructuresAndNoSoftOnes()
        {
            var map = Bridge();
            var states = Enumerable.Range(0, map.Columns).SelectMany(x => Enumerable.Range(0, map.Rows).Select(y => map[x, y].State)).ToList();
            Assert.That(states.Count(s => s == CellState.Hard), Is.EqualTo(118 + 75 + 75 - 2 + 33));
            Assert.That(states.Count(s => s == CellState.Soft), Is.Zero);
        }

        [Test]
        public void TheStructuresLeaveTheSpawnColumnsAndTheLeftWallClear()
        {
            var map = Bridge();
            for (int y = 0; y < 30; y++)
            {
                foreach (var x in new[] { 21, 22, 23, 24, 25, 26, 28, 31, 34 })
                {
                    Assert.That(map[x, y].State, Is.EqualTo(CellState.Empty), $"[{x}, {y}]");
                }
            }

            // A structure cell looks like the bridge's.
            Assert.That(map[35, 27], Is.EqualTo(map[20, 0]));
        }

        [Test]
        public void BridgeCells_AreOpaqueWithTheSecondTile_FreshCellsAreAlpha64WithTheFirst()
        {
            var map = Bridge();
            Assert.That(map[39, 30].Alpha, Is.EqualTo(255));
            Assert.That(map[39, 30].Tile, Is.EqualTo(new TileRect(16, 0, 16, 16)));
            Assert.That(map[20, 0].Tile, Is.EqualTo(new TileRect(16, 0, 16, 16)));
            Assert.That(map[0, 0].Alpha, Is.EqualTo(64));
            Assert.That(map[0, 0].Tile, Is.EqualTo(new TileRect(0, 0, 16, 16)));
            Assert.That(map[0, 0].IsSolidLooking, Is.False);
            Assert.That(map[0, 30].IsSolidLooking, Is.True);
        }

        [Test]
        public void TheSpawnPoints_LieBetweenTheWallsAboveTheFloor()
        {
            var map = Bridge();
            Assert.That(map.GetGridCoordinates(At(500, 100)), Is.EqualTo(new CellIndex(31, 6)));
            Assert.That(map.GetGridCoordinates(At(550, 100)), Is.EqualTo(new CellIndex(34, 6)));
        }

        // ---- Range and lookups ----

        [TestCase(0, 0, true)]
        [TestCase(117, 74, true)]
        [TestCase(-1, 0, false)]
        [TestCase(0, -1, false)]
        [TestCase(118, 0, false)]
        [TestCase(0, 75, false)]
        public void CellInRange_IsTheGrid(int x, int y, bool expected)
        {
            Assert.That(Bridge().CellInRange(x, y), Is.EqualTo(expected));
        }

        [Test]
        public void CellMatchesState_IsFalseOffTheGrid()
        {
            var map = Bridge();
            Assert.That(map.CellMatchesState(20, 0, CellState.Hard), Is.True);
            Assert.That(map.CellMatchesState(20, -1, CellState.Hard), Is.False);
            Assert.That(map.CellMatchesState(118, 30, CellState.Hard), Is.False);
            Assert.That(map.CellMatchesState(118, 30, CellState.Empty), Is.False);
        }

        [TestCase(0f, 0f, 0, 0)]
        [TestCase(15.99f, 15.99f, 0, 0)]
        [TestCase(16f, 16f, 1, 1)]
        [TestCase(-0.5f, -15.9f, 0, 0)] // truncation toward zero: a fraction left of or above the map is still cell 0
        [TestCase(-16f, -16f, -1, -1)]
        [TestCase(1899f, 1199f, 118, 74)] // the last 12 px of width have no cell (1900 / 16 = 118.75)
        public void GetGridCoordinates_DividesBy16AndTruncatesTowardZero(float x, float y, int cx, int cy)
        {
            Assert.That(Bridge().GetGridCoordinates(At(x, y)), Is.EqualTo(new CellIndex(cx, cy)));
        }

        // ---- Floor ----

        [TestCase(485f)]
        [TestCase(480f)]
        [TestCase(495.9f)]
        public void Floor_FeetInsideRow30_SnapToItsTop(float y)
        {
            var p = At(500, y);
            Assert.That(Bridge().CheckFloorCollision(ref p), Is.True);
            Assert.That(p.Y, Is.EqualTo(480f));
            Assert.That(p.X, Is.EqualTo(500f));
        }

        [TestCase(479.9f)]
        [TestCase(496f)]
        [TestCase(100f)]
        public void Floor_FeetOutsideAHardCell_AreLeftAlone(float y)
        {
            var p = At(500, y);
            Assert.That(Bridge().CheckFloorCollision(ref p), Is.False);
            Assert.That(p.Y, Is.EqualTo(y));
        }

        [Test]
        public void Floor_InsideAWall_SnapsToTheWallCellsTop()
        {
            var p = At(325, 107);
            Assert.That(Bridge().CheckFloorCollision(ref p), Is.True);
            Assert.That(p.Y, Is.EqualTo(96f));
        }

        [Test]
        public void Floor_BelowTheGrid_IsNeverHard()
        {
            var p = At(500, 1205);
            Assert.That(Bridge().CheckFloorCollision(ref p), Is.False);
        }

        // ---- Ceiling ----

        [Test]
        public void Ceiling_ThreeCellsAboveTheFeetHard_SnapsToTheOriginalsOffset()
        {
            // y = (530 - 48) / 16 = 30.125 -> row 30 (the floor seen from below); snap to 30*16 + 4*16 = 544.
            var p = At(500, 530);
            Assert.That(Bridge().CheckCeilingCollision(ref p, 3), Is.True);
            Assert.That(p.Y, Is.EqualTo(544f));
        }

        [Test]
        public void Ceiling_HeightChangesTheTestedRowAndTheSnap()
        {
            // Height 1: y = (500 - 16) / 16 = 30.25 -> row 30; snap to 30*16 + 2*16 = 512.
            var p = At(500, 500);
            Assert.That(Bridge().CheckCeilingCollision(ref p, 1), Is.True);
            Assert.That(p.Y, Is.EqualTo(512f));
        }

        [TestCase(527.9f)] // (527.9 - 48) / 16 = 29.99 -> row 29, empty
        [TestCase(544f)]   // row 31, empty
        public void Ceiling_ClearOfHardCells_IsLeftAlone(float y)
        {
            var p = At(500, y);
            Assert.That(Bridge().CheckCeilingCollision(ref p, 3), Is.False);
            Assert.That(p.Y, Is.EqualTo(y));
        }

        [Test]
        public void Ceiling_AboveTheMap_ReadsRowZeroByTruncation()
        {
            // (40 - 48) / 16 = -0.5 -> 0: the original truncates toward zero, so at the top it reads row 0.
            var p = At(325, 40);
            Assert.That(Bridge().CheckCeilingCollision(ref p, 3), Is.True);
            Assert.That(p.Y, Is.EqualTo(64f));
        }

        // ---- Horizontal ----

        [Test]
        public void Horizontal_StandingAgainstTheLeftWallFacingRight_IsAFullThreeCellWall()
        {
            // x = (312 - 8) / 16 = 19, column 20 ahead; y = (480 - 8) / 16 = 29.5 -> 29: rows 29, 28, 27.
            var p = At(312, 480);
            Assert.That(Bridge().CheckHorizontalCollision(ref p, 1, 3), Is.EqualTo(3));
            Assert.That(p.X, Is.EqualTo(304f));
        }

        [Test]
        public void Horizontal_OnePixelShortOfTheContactCell_IsNoWall()
        {
            // x = (311 - 8) / 16 = 18.9 -> 18, column 19 ahead: empty.
            var p = At(311, 480);
            Assert.That(Bridge().CheckHorizontalCollision(ref p, 1, 3), Is.Zero);
            Assert.That(p.X, Is.EqualTo(311f));
        }

        [Test]
        public void Horizontal_FacingLeftAgainstTheLeftWall_UsesThePlus8Buffer()
        {
            // x = (344 + 8) / 16 = 22 -> column 21: empty. x = (343 + 8) / 16 = 21.9 -> 21, column 20: the wall.
            var clear = At(344, 480);
            Assert.That(Bridge().CheckHorizontalCollision(ref clear, -1, 3), Is.Zero);
            var touching = At(343, 480);
            Assert.That(Bridge().CheckHorizontalCollision(ref touching, -1, 3), Is.EqualTo(3));
            Assert.That(touching.X, Is.EqualTo(336f));
        }

        [Test]
        public void Horizontal_NoFacingYet_CountsAsRight()
        {
            var p = At(312, 480);
            Assert.That(Bridge().CheckHorizontalCollision(ref p, 0, 3), Is.EqualTo(3));
        }

        [Test]
        public void Horizontal_TheFloorUnderfoot_IsNeverAWall()
        {
            // Standing on row 30 in open space: the foot row tested is 29, so the floor never counts.
            var p = At(500, 480);
            Assert.That(Bridge().CheckHorizontalCollision(ref p, 1, 3), Is.Zero);
            Assert.That(Bridge().CheckHorizontalCollision(ref p, -1, 3), Is.Zero);
        }

        [Test]
        public void Horizontal_AtTheMapEdge_OffGridCellsAreNotHard()
        {
            var left = At(2, 480);
            Assert.That(Bridge().CheckHorizontalCollision(ref left, -1, 3), Is.Zero);
            var right = At(1899, 480);
            Assert.That(Bridge().CheckHorizontalCollision(ref right, 1, 3), Is.Zero);
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(4, 3)] // at most three cells are ever tested
        public void Horizontal_TestsOnlyAsManyCellsAsTheHeight(int height, int expected)
        {
            var p = At(312, 480);
            Assert.That(Bridge().CheckHorizontalCollision(ref p, 1, height), Is.EqualTo(expected));
        }

        // ---- The partial-wall rule ----

        [Test]
        public void AOneCellStep_IsAPartialWallToAThreeCellPlayer()
        {
            var map = EmptyWithFloor();
            map.Set(26, 29, Hard); // a step on the floor, x 416..431
            var p = At(408, 480);  // (408 - 8) / 16 = 25 -> column 26 ahead
            int count = map.CheckHorizontalCollision(ref p, 1, 3);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(Map.ClassifyWall(count, 3), Is.EqualTo(WallContact.Partial));
        }

        [Test]
        public void ATwoCellLedge_IsPartialToAThreeCellPlayer_ButFullToATwoCellOne()
        {
            var map = EmptyWithFloor();
            map.Set(26, 29, Hard);
            map.Set(26, 28, Hard);
            // Each query gets its own copy, as the original's caller did: a hit snaps X to 400, which would move the next lookup.
            var tall = At(408, 480);
            Assert.That(Map.ClassifyWall(map.CheckHorizontalCollision(ref tall, 1, 3), 3), Is.EqualTo(WallContact.Partial));
            Assert.That(tall.X, Is.EqualTo(400f));
            var shorter = At(408, 480);
            Assert.That(Map.ClassifyWall(map.CheckHorizontalCollision(ref shorter, 1, 2), 2), Is.EqualTo(WallContact.Full));
        }

        [Test]
        public void AOneCellStep_IsAFullWallToASlidingOneCellPlayer()
        {
            var map = EmptyWithFloor();
            map.Set(26, 29, Hard);
            var p = At(408, 480);
            Assert.That(Map.ClassifyWall(map.CheckHorizontalCollision(ref p, 1, 1), 1), Is.EqualTo(WallContact.Full));
        }

        [Test]
        public void AnOverhangAboveTheFootRow_IsStillPartial()
        {
            // Only the top cell of a three-tall player is blocked (a low ceiling edge): count 1 of 3.
            var map = EmptyWithFloor();
            map.Set(26, 27, Hard);
            var p = At(408, 480);
            Assert.That(map.CheckHorizontalCollision(ref p, 1, 3), Is.EqualTo(1));
        }

        [TestCase(0, 3, WallContact.None)]
        [TestCase(1, 3, WallContact.Partial)]
        [TestCase(2, 3, WallContact.Partial)]
        [TestCase(3, 3, WallContact.Full)]
        [TestCase(1, 1, WallContact.Full)]
        [TestCase(2, 2, WallContact.Full)]
        public void ClassifyWall_FullOnlyWhenTheCountIsTheHeight(int count, int height, WallContact expected)
        {
            Assert.That(Map.ClassifyWall(count, height), Is.EqualTo(expected));
        }

        // ---- The world ----

        [Test]
        public void GameWorld_CarriesTheMapItWasBuiltOn()
        {
            var map = Bridge();
            Assert.That(new GameWorld(map).Map, Is.SameAs(map));
            Assert.That(new GameWorld().Map, Is.Null);
        }
    }
}
