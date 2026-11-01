using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Settings;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    /// <summary>
    /// The settings registry (U.VAN-US-A2): every shipped data file in Assets/StreamingAssets/Settings loads and holds
    /// the original's figures in its own units, and bad files are rejected with errors that name file, key and line.
    /// </summary>
    public class GameSettingsTests
    {
        internal static string ShippedFolder
        {
            get
            {
                var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while (dir != null)
                {
                    var candidate = Path.Combine(dir.FullName, "Assets", "StreamingAssets", GameSettings.Folder);
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }

                    dir = dir.Parent;
                }

                throw new DirectoryNotFoundException("Assets/StreamingAssets/Settings not found above the test folder.");
            }
        }

        private static ISettingsSource Shipped => new DirectorySettingsSource(ShippedFolder);

        private static GameSettings Load() => GameSettings.Load(Shipped);

        internal static MemorySettingsSource Patched(string file, Func<string, string> change) =>
            new MemorySettingsSource(Shipped).Set(file, change(Shipped.Read(file)));

        internal static SettingsException LoadFails(ISettingsSource source) =>
            Assert.Throws<SettingsException>(() => GameSettings.Load(source));

        [Test]
        public void ShippedFiles_LoadWithoutErrors()
        {
            Assert.DoesNotThrow(() => Load());
        }

        [TestCaseSource(nameof(AllFiles))]
        public void EachFile_ExistsAndIsReadByTheRegistry(string file)
        {
            Assert.That(Shipped.Read(file), Is.Not.Null, file);
            var broken = LoadFails(new MemorySettingsSource(Shipped).Set(file, null));
            Assert.That(broken.Errors.Single(), Does.Contain(file).And.Contain("not found"));
        }

        private static string[] AllFiles => GameSettings.Files;

        [Test]
        public void PlayerConstants_AreTheOriginalsFigures()
        {
            var p = Load().Player;
            Assert.That(p.MoveAcceleration, Is.EqualTo(14000f));
            Assert.That(p.MaxMoveSpeed, Is.EqualTo(200f));
            Assert.That(p.GroundDragFactor, Is.EqualTo(0.58f));
            Assert.That(p.AirDragFactor, Is.EqualTo(0.65f));
            Assert.That(p.MaxJumpTime, Is.EqualTo(0.6f));
            Assert.That(p.JumpLaunchVelocity, Is.EqualTo(-1100f));
            Assert.That(p.JumpControlPower, Is.EqualTo(0.14f));
            Assert.That(p.GravityAcceleration, Is.EqualTo(10f));
            Assert.That(p.MaxFallSpeed, Is.EqualTo(500f));
            Assert.That(p.WallSlideSpeed, Is.EqualTo(83.3333f).Within(0.001f));
            Assert.That(p.WallSlideDecay, Is.EqualTo(0.97f));
            Assert.That(p.SlideFriction, Is.EqualTo(0.7f));
            Assert.That(p.SlideUnitSize.XInt, Is.EqualTo(3));
            Assert.That(p.SlideUnitSize.YInt, Is.EqualTo(1));
            Assert.That(p.SpawnPositions.Count, Is.EqualTo(3), "the original's two and Leo's (U.VAN-T25)");
            Assert.That(p.ResetPosition.X, Is.EqualTo(500f));
            Assert.That(p.Ability.WallJump, Is.True);
            Assert.That(p.Ability.BulletTime, Is.False);
        }

        [Test]
        public void OwnerDecisions_AreDataSlots()
        {
            var settings = Load();
            Assert.That(settings.Player.KneelReleaseStandsUp, Is.True);
            Assert.That(settings.Player.WallJumpPushOff, Is.True);
            Assert.That(settings.Player.WallJumpPushSpeed, Is.GreaterThan(0f));
            var playable = settings.Characters.Characters.Where(c => c.Playable).Select(c => c.Name);
            Assert.That(playable, Is.EquivalentTo(new[] { "Morris", "Shanoa", "Leo" }));
            Assert.That(settings.Characters.Characters.All(c => c.MoveSet.Count > 0));
        }

        [Test]
        public void TheRosterIsMorrisShanoaAndLeoWithTheirMoveSets()
        {
            var characters = Load().Characters;
            Assert.That(characters.Roster.Select(c => c.Name), Is.EqualTo(new[] { "Morris", "Shanoa", "Leo" }));
            Assert.That(characters.Find("Shanoa").MoveSet, Is.EquivalentTo(characters.Find("Morris").MoveSet));
            Assert.That(characters.Find("Leo").MoveSet,
                Is.EquivalentTo(new[] { ActionKind.Standing, ActionKind.Running, ActionKind.Jumping, ActionKind.Falling }));
        }

        [Test]
        public void ARosterLongerThanTheSpawnPositionsOrNamingNoCharacter_IsRejected()
        {
            var e = LoadFails(Patched(GameSettings.CharactersFile, t => t.Replace("[\"Morris\", \"Shanoa\", \"Leo\"]", "[\"Morris\", \"Shanoa\", \"Leo\", \"Leo\"]")));
            Assert.That(e.Errors.Single(), Does.Contain("only 3 spawn positions"));
            e = LoadFails(Patched(GameSettings.CharactersFile, t => t.Replace("[\"Morris\", \"Shanoa\", \"Leo\"]", "[\"Morris\", \"Richter\"]")));
            Assert.That(e.Errors.Single(), Does.Contain("'Richter', which is not one of the characters"));
        }

        [Test]
        public void AMoveWithoutItsStripOrAMoveSetWithoutStandingRunningAndFalling_IsRejected()
        {
            var e = LoadFails(Patched(GameSettings.CharactersFile, t => t.Replace(
                "\"moveSet\": [\"Standing\", \"Running\", \"Jumping\", \"Falling\"]", "\"moveSet\": [\"Standing\", \"Running\", \"Jumping\", \"Falling\", \"Kneeling\"]")));
            Assert.That(e.Errors.Single(), Does.Contain("sheet 'leo' has no 'kneel' animation"));
            e = LoadFails(Patched(GameSettings.CharactersFile, t => t.Replace(
                "\"moveSet\": [\"Standing\", \"Running\", \"Jumping\", \"Falling\"]", "\"moveSet\": [\"Standing\", \"Running\", \"Jumping\"]")));
            Assert.That(e.Errors.Single(), Does.Contain("must include Falling"));
        }

        [Test]
        public void TheStructuresAreBlocksOfWholeCellsInsideTheGrid()
        {
            var grid = Load().Grid;
            Assert.That(grid.Blocks, Has.Count.EqualTo(6));
            var e = LoadFails(Patched(GameSettings.GridFile, t => t.Replace("\"from\": [35, 12], \"to\": [37, 12]", "\"from\": [35, 12], \"to\": [200, 12]")));
            Assert.That(e.Errors.Single(), Does.Contain("reaches outside the grid"));
        }

        [Test]
        public void Characters_HaveTheOriginalsSizesAndSheets()
        {
            var characters = Load().Characters;
            Assert.That(characters.DefaultCharacter, Is.EqualTo("Morris"));
            Assert.That(characters.Find("Morris").UnitSize.YInt, Is.EqualTo(3));
            Assert.That(characters.Find("Leo").UnitSize.YInt, Is.EqualTo(2));
            Assert.That(characters.Find("Shanoa").Sheet, Is.EqualTo("shanoa"));
        }

        [Test]
        public void AnimationTable_MatchesSection48()
        {
            var animations = Load().Animations;
            Assert.That(animations.FrameSize, Is.EqualTo(64));
            var run = animations.Find("morris").Find("run");
            Assert.That(run.Frames, Is.EqualTo(15));
            Assert.That(run.FrameTime, Is.EqualTo(0.05f));
            Assert.That(run.Loop, Is.True);
            Assert.That(animations.Find("morris").Find("jump").Loop, Is.False);
            Assert.That(animations.Find("leo").Find("run").Frames, Is.EqualTo(4));
            Assert.That(animations.Find("morris").Animations.Count, Is.EqualTo(7));
        }

        [Test]
        public void Grid_IsTheBridgeLevel()
        {
            var grid = Load().Grid;
            Assert.That(grid.CellSize, Is.EqualTo(16));
            Assert.That(grid.Columns, Is.EqualTo(118));
            Assert.That(grid.Rows, Is.EqualTo(75));
            Assert.That(grid.FloorRow, Is.EqualTo(30));
            Assert.That(grid.WallColumns, Is.EqualTo(new[] { 20, 40 }));
            Assert.That(grid.FreshCellAlpha, Is.EqualTo(64));
            Assert.That(grid.BridgeTileBounds, Is.EqualTo(new[] { 16f, 0f, 16f, 16f }));
        }

        [Test]
        public void Units_AreMappedInOnePlace()
        {
            var units = Load().Units;
            Assert.That(units.ToUnits(32f), Is.EqualTo(2f));
            Assert.That(units.ToPixels(units.ToUnits(100f)), Is.EqualTo(100f).Within(0.0001f));
            Assert.That(units.PerSecond(10f), Is.EqualTo(600f));
        }

        [Test]
        public void MenuTree_HasThreeReachableLevels()
        {
            var menu = Load().Menu;
            Assert.That(menu.Levels.Count, Is.EqualTo(5));
            Assert.That(menu.Levels[0].Items.Select(i => i.Text), Is.EqualTo(new[] { "Game", "Video", "Audio", "Controls", "Quit" }));
            Assert.That(menu.Levels[0].Items[4].Action, Is.EqualTo(MenuAction.Quit));
            Assert.That(menu.Levels[2].Items[0].Action, Is.EqualTo(MenuAction.ToggleFullscreen));
            Assert.That(menu.Levels[1].Parent, Is.EqualTo(0));
        }

        [Test]
        public void CameraAndConsole_HoldTheOriginalsConstants()
        {
            var settings = Load();
            Assert.That(settings.Camera.InitialMode, Is.EqualTo(CameraMode.LoosePlayer));
            Assert.That(settings.Camera.FollowGain, Is.EqualTo(0.02f));
            Assert.That(settings.Camera.Damping, Is.EqualTo(0.7f));
            Assert.That(settings.Console.Height, Is.EqualTo(200));
            Assert.That(settings.Console.HistoryLines, Is.EqualTo(100));
            Assert.That(settings.Console.Version, Is.EqualTo("Version 0.0.1"));
        }

        [Test]
        public void AudioCues_AreTheXactTable()
        {
            var audio = Load().Audio;
            Assert.That(audio.Cues.Count, Is.EqualTo(13));
            Assert.That(audio.FindCue("step2").Volume, Is.EqualTo(-1200));
            Assert.That(audio.FindCue("music1").Loop, Is.EqualTo(255));
            Assert.That(audio.Categories.Count, Is.EqualTo(5));
            Assert.That(audio.Songs.Select(s => s.Name), Is.EqualTo(new[] { "music", "battle" }));
        }

        [Test]
        public void Segments_AreTheElevenDefinitions()
        {
            var segments = Load().Segments;
            Assert.That(segments.Definitions.Count, Is.EqualTo(11));
            var torch = segments.Definitions.Single(d => d.Name == "torch");
            Assert.That(torch.Flag, Is.EqualTo(1));
            Assert.That(torch.Width, Is.EqualTo(42));
            Assert.That(torch.Height, Is.EqualTo(109));
        }

        [Test]
        public void Content_KeepsEverythingTheOriginalNeverWired()
        {
            var content = Load().Content;
            Assert.That(content.SegmentAtlas.Asset, Is.EqualTo("maps1"));
            Assert.That(content.SegmentAtlas.Size.XInt, Is.EqualTo(1024));
            Assert.That(content.Ledges.Slots, Is.EqualTo(16));
            Assert.That(content.Ledges.NodesPerLedge, Is.EqualTo(16));
            Assert.That(content.Sun.Periods.Select(p => p.Name), Is.EqualTo(new[] { "Day", "Dusk", "Night", "Dawn" }));
            Assert.That(content.Sun.Periods.Single(p => p.Name == "Night").Colour, Is.EqualTo(new Rgba(128, 0, 255, 64)));
            Assert.That(content.ActionCross.Slots.Count, Is.EqualTo(4));
            Assert.That(content.ActionCross.Slots.Single(s => s.Position == ActionCrossPosition.Top).Icon, Is.EqualTo("icon_selectsquad"));
            Assert.That(content.ActionCross.BlankIcon, Is.EqualTo("blank"));
            Assert.That(content.ActionCross.Slots.Where(s => s.Position != ActionCrossPosition.Top).Select(s => s.Icon), Is.All.EqualTo("blank"));
            Assert.That(content.Portraits.Used, Is.EqualTo("portrait_1"));
            Assert.That(content.Portraits.Unused.Count, Is.EqualTo(6));
            Assert.That(content.Icons.ActionButtons.Count, Is.EqualTo(4));
            Assert.That(content.Icons.Skills.Count, Is.EqualTo(29));
            Assert.That(content.Icons.Skills, Does.Contain("poisondart").And.Contain("consecrate"));
            Assert.That(content.AreaLayers.Drawn, Is.EqualTo("area_1"));
            Assert.That(content.AreaLayers.Unused.Count, Is.EqualTo(4));
            Assert.That(content.Particles.Assets.Count, Is.EqualTo(5));
            Assert.That(content.Selectors.Assets.Count, Is.EqualTo(5));
            Assert.That(content.Banners.Assets.Count, Is.EqualTo(15));
            Assert.That(content.SpriteSheets.Assets.Count, Is.EqualTo(16));
            Assert.That(content.UnwiredCharacterArt.Sheet, Is.EqualTo("richter"));
            Assert.That(content.UnwiredCharacterArt.Size.XInt, Is.EqualTo(640));
            Assert.That(content.OrphanCharacterArt.Assets, Is.EquivalentTo(new[] { "Untitled", "frame_10" }));
            Assert.That(content.Textures.LoadedButUnused, Is.EquivalentTo(new[] { "white", "brick", "console2", "foggy_woods", "metal1" }));
            Assert.That(content.Textures.NeverLoaded, Is.EquivalentTo(new[] { "block", "menu2" }));
            Assert.That(content.HudMockups.LoadedButUnused, Is.EquivalentTo(new[] { "bar_health" }));
            Assert.That(content.HudMockups.NeverLoaded, Is.EquivalentTo(new[] { "demo_gui", "demo_gui_0", "gui_0", "gui_1" }));
            Assert.That(content.MapEditorArt.UsedForDebugBox, Is.EqualTo("cell"));
            Assert.That(content.MapEditorArt.LoadedButUnused, Is.EquivalentTo(new[] { "block" }));
            Assert.That(content.Application.Asset, Is.EqualTo("GameThumbnail"));
            Assert.That(content.Fonts.Entries.Select(f => f.Name), Is.EquivalentTo(new[] { "Arial_10", "Lucida", "Webdings_14" }));
            Assert.That(content.Fonts.Entries.Single(f => f.Name == "Webdings_14").Used, Is.False);
        }

        [Test]
        public void ContentFontCount_MustBeAllThree()
        {
            var original = Shipped.Read(GameSettings.ContentFile);
            var webdingsEntry = new Regex(@",\s*\{\s*""name""\s*:\s*""Webdings_14""[^}]*\}", RegexOptions.Singleline);
            var patched = webdingsEntry.Replace(original, "");
            Assert.That(patched, Is.Not.EqualTo(original), "the Webdings_14 entry must actually be removed by the regex above");
            var e = LoadFails(new MemorySettingsSource(Shipped).Set(GameSettings.ContentFile, patched));
            Assert.That(e.Errors.Single(), Does.Contain("fonts").And.Contain("all three"));
        }

        [Test]
        public void ContentOrphanCharacterArtCount_MustBeTwo()
        {
            var e = LoadFails(Patched(GameSettings.ContentFile, t => t.Replace("\"assets\": [\"Untitled\", \"frame_10\"]", "\"assets\": [\"Untitled\"]")));
            Assert.That(e.Errors.Single(), Does.Contain("orphanCharacterArt"));
        }

        [Test]
        public void Names_AreTheOriginalsTables()
        {
            var names = Load().Names;
            Assert.That(names.Male.Count, Is.EqualTo(1219));
            Assert.That(names.Female.Count, Is.EqualTo(4073));
            Assert.That(names.Female[names.Female.Count - 1], Is.EqualTo("Vanna"));
            Assert.That(names.Beast.Count, Is.EqualTo(15));
            Assert.That(names.Male[names.Male.Count - 1], Is.EqualTo("Zane"));
            Assert.That(names.For(Gender.Female), Is.SameAs(names.Female));
        }

        // --- Bad files are rejected with file, key and line ---------------------------------------

        [Test]
        public void MissingKey_IsNamed()
        {
            var e = LoadFails(Patched(GameSettings.PlayerFile, t => t.Replace("\"maxFallSpeed\": 500,", "")));
            Assert.That(e.Errors.Single(), Does.Contain("player.json").And.Contain("'maxFallSpeed' is missing"));
        }

        [Test]
        public void UnknownKey_IsNamedWithItsLine()
        {
            var e = LoadFails(Patched(GameSettings.PlayerFile, t => t.Replace("\"maxMoveSpeed\": 200,", "\"maxMoveSpeed\": 200,\n  \"maxMoveSped\": 1,")));
            Assert.That(e.Errors.Single(), Does.Contain("unknown key 'maxMoveSped'").And.Contain("(line "));
        }

        [Test]
        public void OutOfRangeValue_NamesFileKeyAndLine()
        {
            var e = LoadFails(Patched(GameSettings.PlayerFile, t => t.Replace("\"groundDragFactor\": 0.58", "\"groundDragFactor\": 1.5")));
            Assert.That(e.Errors.Single(), Does.Contain("player.json").And.Contain("'groundDragFactor'").And.Contain("(line 5)"));
        }

        [Test]
        public void BadJson_NamesFileAndPosition()
        {
            var e = LoadFails(Patched(GameSettings.GridFile, t => t.Replace("\"cellSize\": 16,", "\"cellSize\": 16 \"x\": 1,")));
            Assert.That(e.Errors.Single(), Does.Contain("grid.json").And.Contain("not valid JSON").And.Contain("line"));
        }

        [Test]
        public void WrongType_IsReported()
        {
            var e = LoadFails(Patched(GameSettings.ConsoleFile, t => t.Replace("\"height\": 200", "\"height\": \"tall\"")));
            Assert.That(e.Errors.Single(), Does.Contain("console.json").And.Contain("'height'").And.Contain("whole number"));
        }

        [Test]
        public void EveryProblemIsReportedInOneLoad()
        {
            var source = new MemorySettingsSource(Shipped)
                .Set(GameSettings.PlayerFile, Shipped.Read(GameSettings.PlayerFile).Replace("\"maxFallSpeed\": 500,", ""))
                .Set(GameSettings.CameraFile, Shipped.Read(GameSettings.CameraFile).Replace("\"damping\": 0.7", "\"damping\": 2"))
                .Set(GameSettings.AudioFile, null);
            var e = LoadFails(source);
            Assert.That(e.Errors.Count, Is.EqualTo(3));
        }

        [Test]
        public void UnknownSheet_IsACrossFileError()
        {
            var e = LoadFails(Patched(GameSettings.CharactersFile, t => t.Replace("\"sheet\": \"leo\"", "\"sheet\": \"nobody\"")));
            Assert.That(e.Errors.Single(), Does.Contain("'Leo'").And.Contain("nobody"));
        }

        [Test]
        public void MenuOpeningAMissingLevel_IsRejected()
        {
            var e = LoadFails(Patched(GameSettings.MenuFile, t => t.Replace("\"target\": 1 }", "\"target\": 9 }")));
            Assert.That(e.Errors.Single(), Does.Contain("opens level 9"));
        }

        [Test]
        public void MenuOpeningALevelWithNoItems_IsRejected()
        {
            // Levels 3 and 4 are kept as data with no items; opening one would leave nothing to highlight.
            var e = LoadFails(Patched(GameSettings.MenuFile, t => t.Replace("\"target\": 1 }", "\"target\": 3 }")));
            Assert.That(e.Errors.Single(), Does.Contain("opens level 3").And.Contain("no item"));
        }

        [Test]
        public void MenuCueMissingFromTheAudioTable_IsRejected()
        {
            var e = LoadFails(Patched(GameSettings.MenuFile, t => t.Replace("\"openCue\": \"menu_open\"", "\"openCue\": \"menu_opne\"")));
            Assert.That(e.Errors.Single(), Does.Contain("menu_opne"));
        }

        [Test]
        public void AnimationFrameCountThatDisagreesWithTheStrip_IsRejected()
        {
            var e = LoadFails(Patched(GameSettings.AnimationsFile, t => t.Replace("\"width\": 960, \"frames\": 15", "\"width\": 960, \"frames\": 14")));
            Assert.That(e.Errors.Single(), Does.Contain("'frames'").And.Contain("960 / 64"));
        }

        [Test]
        public void SegmentOutsideItsImage_IsRejected()
        {
            var e = LoadFails(Patched(GameSettings.SegmentsFile, t => t.Replace("[437, 300, 479, 409]", "[437, 300, 479, 2000]")));
            Assert.That(e.Errors.Single(), Does.Contain("'torch'").And.Contain("outside"));
        }

        [Test]
        public void ContentSegmentAtlas_MustBeSegmentsOwnSourceImage()
        {
            var e = LoadFails(Patched(GameSettings.ContentFile, t => t.Replace("\"asset\": \"maps1\",", "\"asset\": \"wrong\",")));
            Assert.That(e.Errors.Single(), Does.Contain("segmentAtlas.asset").And.Contain("wrong"));
        }

        [Test]
        public void ContentDrawnAreaLayer_MustMatchWhatGridActuallyDraws()
        {
            var e = LoadFails(Patched(GameSettings.ContentFile, t => t.Replace("\"drawn\": \"area_1\",", "\"drawn\": \"nope\",")));
            Assert.That(e.Errors.Single(), Does.Contain("draws 'area_1'").And.Contain("nope"));
        }

        [Test]
        public void ActionCrossSlotIcon_MustBeAnInventoriedIconOrBlank()
        {
            var e = LoadFails(Patched(GameSettings.ContentFile, t => t.Replace("{ \"position\": \"Top\", \"icon\": \"icon_selectsquad\" }", "{ \"position\": \"Top\", \"icon\": \"icon_nope\" }")));
            Assert.That(e.Errors.Single(), Does.Contain("'Top'").And.Contain("icon_nope"));
        }

        [Test]
        public void PlayerSpawnOutsideTheMap_IsRejected()
        {
            var e = LoadFails(Patched(GameSettings.PlayerFile, t => t.Replace("[[500, 100], [550, 100], [450, 100]]", "[[5000, 100], [550, 100], [450, 100]]")));
            Assert.That(e.Errors.Single(), Does.Contain("outside"));
        }

        [Test]
        public void MenuParentLoop_IsRejected()
        {
            var e = LoadFails(Patched(GameSettings.MenuFile, t =>
            {
                var text = t.Replace("\"title\": \"Main Menu : Game\",\n      \"parent\": 0", "\"title\": \"Main Menu : Game\",\n      \"parent\": 2");
                return text.Replace("\"title\": \"Main Menu : Video\",\n      \"parent\": 0", "\"title\": \"Main Menu : Video\",\n      \"parent\": 1");
            }));
            Assert.That(e.Errors.Any(m => m.Contains("never reaches the root")));
        }

        [Test]
        public void FractionalSegmentRect_IsRejected()
        {
            var e = LoadFails(Patched(GameSettings.SegmentsFile, t => t.Replace("[437, 300, 479, 409]", "[437, 300, 479, 409.5]")));
            Assert.That(e.Errors.Any(m => m.Contains("whole number")));
        }

        [Test]
        public void MistypedColour_IsRejected()
        {
            var e = LoadFails(Patched(GameSettings.ConsoleFile, t => t.Replace("LightGray", "LightGrey")));
            Assert.That(e.Errors.Single(), Does.Contain("'historyColour'"));
        }

        [Test]
        public void AConsoleWordTheScriptCouldNeverRead_IsRejected()
        {
            var twice = LoadFails(Patched(GameSettings.ConsoleFile, t => t.Replace("\"word\": \"white\"", "\"word\": \"red\"")));
            Assert.That(twice.Errors.Single(), Does.Contain("console.json").And.Contain("'red' twice"));
            var upper = LoadFails(Patched(GameSettings.ConsoleFile, t => t.Replace("\"word\": \"blue\"", "\"word\": \"Blue\"")));
            Assert.That(upper.Errors.Single(), Does.Contain("'Blue'").And.Contain("lower-case"));
        }

        [Test]
        public void TheConsoleBannerMayHoldBlankLines()
        {
            Assert.That(Load().Console.Script.Banner[1], Is.Empty);
        }

        [Test]
        public void UnitsThatDisagreeWithTheGridOrStepper_AreRejected()
        {
            Assert.That(LoadFails(Patched(GameSettings.GridFile, t => t.Replace("\"pixelsPerUnit\": 16", "\"pixelsPerUnit\": 32"))).Errors.Single(), Does.Contain("pixelsPerUnit"));
            Assert.That(LoadFails(Patched(GameSettings.GridFile, t => t.Replace("\"stepsPerSecond\": 60", "\"stepsPerSecond\": 30"))).Errors.Single(), Does.Contain("stepsPerSecond"));
        }
    }
}
