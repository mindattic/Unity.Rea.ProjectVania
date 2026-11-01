using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Simulation;

namespace Unity.Rea.ProjectVania.Core.Settings
{
    public enum Gender
    {
        Male,
        Female,
    }

    /// <summary>The player's actions (RECONSTRUCTION 4.6); a character's move set lists the ones it can do.</summary>
    public enum ActionKind
    {
        Standing,
        Running,
        Kneeling,
        Sliding,
        Jumping,
        WallSliding,
        Hanging,
        Falling,
    }

    public enum CameraMode
    {
        Free,
        LockPlayer,
        LoosePlayer,
        LooseCursor,
    }

    /// <summary>The XNA named colours the original used for text.</summary>
    public enum SettingsColour
    {
        White,
        LightGray,
        Yellow,
        Red,
        Blue,
        Green,
        Black,
        Wheat,
        LightSteelBlue,
        Peru,
    }

    /// <summary>The XNA <c>Color</c> each <see cref="SettingsColour"/> names, opaque.</summary>
    public static class SettingsColours
    {
        public static Rgba ToRgba(this SettingsColour colour)
        {
            switch (colour)
            {
                case SettingsColour.LightGray: return new Rgba(211, 211, 211, 255);
                case SettingsColour.Yellow: return new Rgba(255, 255, 0, 255);
                case SettingsColour.Red: return new Rgba(255, 0, 0, 255);
                case SettingsColour.Blue: return new Rgba(0, 0, 255, 255);
                case SettingsColour.Green: return new Rgba(0, 128, 0, 255);
                case SettingsColour.Black: return new Rgba(0, 0, 0, 255);
                case SettingsColour.Wheat: return new Rgba(245, 222, 179, 255);
                case SettingsColour.LightSteelBlue: return new Rgba(176, 196, 222, 255);
                case SettingsColour.Peru: return new Rgba(205, 133, 63, 255);
                default: return Rgba.White;
            }
        }
    }

    public enum MenuAction
    {
        None,
        Open,
        Quit,
        ToggleFullscreen,
    }

    /// <summary>player.json: the constants of RECONSTRUCTION 4.2 and the owner's later decisions, as data slots.</summary>
    public sealed class PlayerData
    {
        public float MoveAcceleration { get; private set; }
        public float MaxMoveSpeed { get; private set; }
        public float GroundDragFactor { get; private set; }
        public float AirDragFactor { get; private set; }
        public float MaxJumpTime { get; private set; }
        public float JumpLaunchVelocity { get; private set; }
        public float JumpControlPower { get; private set; }
        public float GravityAcceleration { get; private set; }
        public float MaxFallSpeed { get; private set; }
        public float WallSlideSpeedDivisor { get; private set; }
        public float WallSlideDecay { get; private set; }
        public float MaxWallSlideTime { get; private set; }
        public float FallDamageTime { get; private set; }
        public float MaxWalljumpDelayTime { get; private set; }
        public float MaxLockOutTime { get; private set; }
        public float SlideLaunchAcceleration { get; private set; }
        public float SlideFriction { get; private set; }
        public Pair SlideUnitSize { get; private set; }

        /// <summary>How many cells above the feet the ceiling test looks (<c>CheckCeilingCollision(ref position, 3)</c>).</summary>
        public int CeilingHeightCells { get; private set; }

        public IReadOnlyList<Pair> SpawnPositions { get; private set; }
        public Pair ResetPosition { get; private set; }
        public Abilities Ability { get; private set; }

        /// <summary>Owner decision: releasing a kneel stands the character up (U.VAN-T23).</summary>
        public bool KneelReleaseStandsUp { get; private set; }

        /// <summary>Owner decision: a wall jump pushes off the wall (U.VAN-T24).</summary>
        public bool WallJumpPushOff { get; private set; }

        /// <summary>The push-off speed in pixels per second, a slot U.VAN-T24 tunes.</summary>
        public float WallJumpPushSpeed { get; private set; }

        /// <summary>The wall-slide speed cap: <see cref="MaxFallSpeed"/> over <see cref="WallSlideSpeedDivisor"/> (83.33).</summary>
        public float WallSlideSpeed => MaxFallSpeed / WallSlideSpeedDivisor;

        internal static PlayerData Read(SettingsReader r)
        {
            var data = new PlayerData
            {
                MoveAcceleration = r.Positive("moveAcceleration"),
                MaxMoveSpeed = r.Positive("maxMoveSpeed"),
                GroundDragFactor = r.Number("groundDragFactor", 0.0001f, 1f),
                AirDragFactor = r.Number("airDragFactor", 0.0001f, 1f),
                MaxJumpTime = r.Positive("maxJumpTime"),
                JumpLaunchVelocity = r.Number("jumpLaunchVelocity", float.MinValue, -0.0001f),
                JumpControlPower = r.Positive("jumpControlPower"),
                GravityAcceleration = r.Positive("gravityAcceleration"),
                MaxFallSpeed = r.Positive("maxFallSpeed"),
                WallSlideSpeedDivisor = r.Positive("wallSlideSpeedDivisor"),
                WallSlideDecay = r.Number("wallSlideDecay", 0.0001f, 1f),
                MaxWallSlideTime = r.Positive("maxWallSlideTime"),
                FallDamageTime = r.Positive("fallDamageTime"),
                MaxWalljumpDelayTime = r.Number("maxWalljumpDelayTime", 0f),
                MaxLockOutTime = r.Number("maxLockOutTime", 0f),
                SlideLaunchAcceleration = r.Positive("slideLaunchAcceleration"),
                SlideFriction = r.Number("slideFriction", 0.0001f, 1f),
                SlideUnitSize = r.Pair("slideUnitSize", 1f, 100f),
                CeilingHeightCells = r.Integer("ceilingHeightCells", 1, 100),
            };

            var spawn = r.Section("spawn", s => new
            {
                Positions = s.Pairs("positions", 0f),
                Reset = s.Pair("resetPosition", 0f),
            });
            data.SpawnPositions = spawn.Positions;
            data.ResetPosition = spawn.Reset;
            r.Check(spawn.Positions.Count >= 1, "spawn", "must list at least one spawn position");
            data.Ability = r.Section("abilities", Abilities.Read);
            data.KneelReleaseStandsUp = r.Section("kneel", s => s.Flag("releaseStandsUp"));
            var wall = r.Section("wallJump", s => new { Push = s.Flag("pushOff"), Speed = s.Number("pushSpeed", 0f) });
            data.WallJumpPushOff = wall.Push;
            data.WallJumpPushSpeed = wall.Speed;
            return data;
        }
    }

    /// <summary>AbilityList (RECONSTRUCTION 4.1).</summary>
    public sealed class Abilities
    {
        public bool Run { get; private set; }
        public bool Jump { get; private set; }
        public bool Kneel { get; private set; }
        public bool WallSlide { get; private set; }
        public bool WallJump { get; private set; }
        public bool Slide { get; private set; }
        public bool SinkInWater { get; private set; }
        public bool BulletTime { get; private set; }

        internal static Abilities Read(SettingsReader r) => new Abilities
        {
            Run = r.Flag("run"),
            Jump = r.Flag("jump"),
            Kneel = r.Flag("kneel"),
            WallSlide = r.Flag("wallSlide"),
            WallJump = r.Flag("wallJump"),
            Slide = r.Flag("slide"),
            SinkInWater = r.Flag("sinkInWater"),
            BulletTime = r.Flag("bulletTime"),
        };
    }

    /// <summary>characters.json: who can be played, with a move set each (owner: Shanoa and Leo playable too, U.VAN-T25).</summary>
    public sealed class CharacterData
    {
        public string DefaultCharacter { get; private set; }

        /// <summary>How every character's name is drawn over it (RECONSTRUCTION 6, item 4).</summary>
        public NameTagData NameTag { get; private set; }

        public IReadOnlyList<Character> Characters { get; private set; }

        /// <summary>
        /// Who is spawned, in order, one at each of player.json's spawn positions (owner decision, U.VAN-T25: Morris,
        /// Shanoa and Leo; the original spawned two Morrises). <c>[</c> / <c>]</c> cycle through them in this order.
        /// </summary>
        public IReadOnlyList<Character> Roster { get; private set; }

        public Character Find(string name)
        {
            foreach (var character in Characters)
            {
                if (character.Name == name)
                {
                    return character;
                }
            }

            return null;
        }

        internal static CharacterData Read(SettingsReader r)
        {
            var data = new CharacterData
            {
                DefaultCharacter = r.Text("defaultCharacter"),
                NameTag = r.Section("nameTag", NameTagData.Read),
                Characters = r.Each("characters", Character.Read),
            };
            r.Check(data.Find(data.DefaultCharacter) != null, "defaultCharacter", "must name one of the characters");
            var roster = new List<Character>();
            foreach (var name in r.Texts("roster"))
            {
                var character = data.Find(name);
                r.Check(character != null, "roster", $"names '{name}', which is not one of the characters");
                r.Check(character == null || character.Playable, "roster", $"names '{name}', which is not playable");
                if (character != null) roster.Add(character);
            }

            data.Roster = roster;
            var seen = new HashSet<string>();
            foreach (var character in data.Characters)
            {
                r.Check(seen.Add(character.Name), "characters", $"lists '{character.Name}' twice");
            }

            return data;
        }
    }

    /// <summary>
    /// characters.json's <c>nameTag</c>: <c>SceneManager.DrawText</c>'s two writes of each name, in the original's pixels
    /// relative to the character's feet, y down.
    /// </summary>
    public sealed class NameTagData
    {
        public string Font { get; private set; }

        /// <summary>The sprite font's size in points (TextWriter's <c>fontSize</c> and the .spritefont's <c>Size</c>).</summary>
        public float FontPoints { get; private set; }

        /// <summary>The font's measured line height in pixels (<c>SpriteFont.MeasureString(name).Y</c>).</summary>
        public float LineHeight { get; private set; }

        /// <summary>TextWriter's Top alignment factor: the text is lifted by line height * points * this.</summary>
        public float TopAlignFactor { get; private set; }

        /// <summary>The writes, in draw order (later ones over earlier ones).</summary>
        public IReadOnlyList<NameTagLabel> Labels { get; private set; }

        /// <summary>How far Top alignment lifts the text, in pixels (64 for Arial 10).</summary>
        public float TopAlignLift => LineHeight * FontPoints * TopAlignFactor;

        internal static NameTagData Read(SettingsReader r) => new NameTagData
        {
            Font = r.Text("font"),
            FontPoints = r.Positive("fontPoints"),
            LineHeight = r.Positive("lineHeight"),
            TopAlignFactor = r.Number("topAlignFactor", 0f, 10f),
            Labels = r.Each("labels", NameTagLabel.Read),
        };
    }

    public sealed class NameTagLabel
    {
        public SettingsColour Colour { get; private set; }

        /// <summary>From the feet to the text's anchor (its top centre before Top alignment lifts it), pixels, y down.</summary>
        public Pair Offset { get; private set; }

        internal static NameTagLabel Read(SettingsReader r) => new NameTagLabel
        {
            Colour = r.Choice<SettingsColour>("colour"),
            Offset = r.Pair("offset", -1000f, 1000f),
        };
    }

    public sealed class Character
    {
        public string Name { get; private set; }
        public string Sheet { get; private set; }

        /// <summary>The collision size in cells (width, height).</summary>
        public Pair UnitSize { get; private set; }

        public bool Playable { get; private set; }
        public Gender Gender { get; private set; }
        public IReadOnlyList<ActionKind> MoveSet { get; private set; }

        /// <summary>Whether <paramref name="action"/> is in the character's move set.</summary>
        public bool Can(ActionKind action)
        {
            foreach (var move in MoveSet)
            {
                if (move == action) return true;
            }

            return false;
        }

        internal static Character Read(SettingsReader r)
        {
            var character = new Character
            {
                Name = r.Text("name"),
                Sheet = r.Text("sheet"),
                UnitSize = r.Pair("unitSize", 1f, 100f),
                Playable = r.Flag("playable"),
                Gender = r.Choice<Gender>("gender"),
                MoveSet = r.Choices<ActionKind>("moveSet"),
            };

            // Every character stands, runs and falls: the terrain machine reaches those whatever the controls do.
            foreach (var required in new[] { ActionKind.Standing, ActionKind.Running, ActionKind.Falling })
            {
                r.Check(character.Can(required), "moveSet", $"must include {required} for '{character.Name}'");
            }

            return character;
        }
    }

    /// <summary>animations.json: the animation table of RECONSTRUCTION 4.8.</summary>
    public sealed class AnimationData
    {
        public int FrameSize { get; private set; }
        public IReadOnlyList<AnimationSheet> Sheets { get; private set; }

        public AnimationSheet Find(string sheet)
        {
            foreach (var s in Sheets)
            {
                if (s.Sheet == sheet)
                {
                    return s;
                }
            }

            return null;
        }

        internal static AnimationData Read(SettingsReader r)
        {
            var data = new AnimationData
            {
                FrameSize = r.Integer("frameSize", 1, 4096),
            };
            data.Sheets = r.Each("sheets", s => AnimationSheet.Read(s, data.FrameSize));
            var seen = new HashSet<string>();
            foreach (var sheet in data.Sheets)
            {
                r.Check(seen.Add(sheet.Sheet), "sheets", $"lists '{sheet.Sheet}' twice");
            }

            return data;
        }
    }

    public sealed class AnimationSheet
    {
        public string Sheet { get; private set; }
        public IReadOnlyList<AnimationClip> Animations { get; private set; }

        public AnimationClip Find(string name)
        {
            foreach (var a in Animations)
            {
                if (a.Name == name)
                {
                    return a;
                }
            }

            return null;
        }

        internal static AnimationSheet Read(SettingsReader r, int frameSize)
        {
            var sheet = new AnimationSheet
            {
                Sheet = r.Text("sheet"),
                Animations = r.Each("animations", a => AnimationClip.Read(a, frameSize)),
            };
            var seen = new HashSet<string>();
            foreach (var a in sheet.Animations)
            {
                r.Check(seen.Add(a.Name), "animations", $"lists '{a.Name}' twice in {sheet.Sheet}");
            }

            // A borrowed clip cuts its frames from a strip of the same sheet that has a PNG of its own.
            foreach (var a in sheet.Animations)
            {
                if (!a.Borrowed) continue;
                var source = sheet.Find(a.Strip);
                if (source == null || source.Borrowed)
                {
                    r.Check(false, "animations", $"'{a.Name}' in {sheet.Sheet} borrows '{a.Strip}', which is not a strip of its own in that sheet");
                    continue;
                }

                foreach (var index in a.FrameIndices)
                {
                    r.Check(index < source.Frames, "animations", $"'{a.Name}' in {sheet.Sheet} borrows frame {index} of '{a.Strip}', which has {source.Frames}");
                }
            }

            return sheet;
        }
    }

    public sealed class AnimationClip
    {
        public string Name { get; private set; }

        /// <summary>The PNG the frames are cut from (<c>&lt;sheet&gt;/&lt;Strip&gt;.png</c>): the clip's own name, or another strip of the sheet.</summary>
        public string Strip { get; private set; }

        /// <summary>The strip's width in pixels; 0 for a clip that borrows another strip's frames.</summary>
        public int Width { get; private set; }

        public int Frames { get; private set; }
        public float FrameTime { get; private set; }
        public bool Loop { get; private set; }

        /// <summary>Whether this clip borrows frames of another strip (<see cref="Strip"/>) instead of having a PNG of its own.</summary>
        public bool Borrowed => Strip != Name;

        /// <summary>Which frames of <see cref="Strip"/> it plays, in order (0 .. Frames - 1 for a clip with its own strip).</summary>
        public IReadOnlyList<int> FrameIndices { get; private set; }

        /// <summary>The frame of <see cref="Strip"/> that this clip's frame <paramref name="frame"/> shows.</summary>
        public int SourceFrame(int frame) => FrameIndices[frame];

        internal static AnimationClip Read(SettingsReader r, int frameSize)
        {
            var name = r.Text("name");

            // U.VAN-T25: a sheet without a strip for some action (Shanoa, Leo) borrows frames of one it has, as
            // { "name", "strip", "frameIndices", "frameTime", "loop" } instead of width and frames.
            if (r.Has("strip"))
            {
                var borrowed = new AnimationClip
                {
                    Name = name,
                    Strip = r.Text("strip"),
                    FrameIndices = r.Integers("frameIndices", 0, 1000),
                    FrameTime = r.Positive("frameTime"),
                    Loop = r.Flag("loop"),
                };
                borrowed.Frames = borrowed.FrameIndices.Count;
                r.Check(borrowed.Frames > 0, "frameIndices", $"must list at least one frame for '{name}'");
                r.Check(borrowed.Strip != name, "strip", $"must name another strip than '{name}' itself");
                return borrowed;
            }

            var clip = new AnimationClip
            {
                Name = name,
                Width = r.Integer("width", 1, 100000),
                Frames = r.Integer("frames", 1, 1000),
                FrameTime = r.Positive("frameTime"),
                Loop = r.Flag("loop"),
            };
            clip.Strip = name;
            var indices = new int[clip.Frames];
            for (var i = 0; i < indices.Length; i++) indices[i] = i;
            clip.FrameIndices = indices;
            r.Check(frameSize <= 0 || clip.Frames == clip.Width / frameSize, "frames",
                $"must be width / frameSize truncated ({clip.Width} / {frameSize} = {(frameSize > 0 ? clip.Width / frameSize : 0)}) for '{clip.Name}'");
            return clip;
        }
    }

    /// <summary>grid.json: the 16 px grid, the bridge level and the pixel-to-unit mapping.</summary>
    public sealed class GridData
    {
        public int CellSize { get; private set; }
        public int MapWidth { get; private set; }
        public int MapHeight { get; private set; }
        public int FreshCellAlpha { get; private set; }
        public float[] FreshCellAssetBounds { get; private set; }
        public string TilesetAsset { get; private set; }
        public int FloorRow { get; private set; }
        public IReadOnlyList<int> WallColumns { get; private set; }
        public float[] BridgeTileBounds { get; private set; }
        public float PixelsPerUnit { get; private set; }
        public int StepsPerSecond { get; private set; }
        public Pair ReferenceScreen { get; private set; }

        /// <summary>How the map is drawn (grid.json draw: the clear colour, the outline and the layers).</summary>
        public MapDrawData Draw { get; private set; }

        /// <summary>
        /// The block structures built on the bridge level for Leo to climb and jump across (owner decision, U.VAN-T25; the
        /// original's level was the bridge alone): each a rectangle of Hard cells.
        /// </summary>
        public IReadOnlyList<BlockData> Blocks { get; private set; }

        /// <summary>The tileset rectangle every structure cell shows.</summary>
        public float[] StructureTileBounds { get; private set; }

        /// <summary>Columns of cells (118 for 1900 / 16, truncated).</summary>
        public int Columns => MapWidth / CellSize;

        /// <summary>Rows of cells (75 for 1200 / 16, truncated).</summary>
        public int Rows => MapHeight / CellSize;

        internal static GridData Read(SettingsReader r)
        {
            var data = new GridData
            {
                CellSize = r.Integer("cellSize", 1, 1024),
                MapWidth = r.Integer("mapWidth", 1, 100000),
                MapHeight = r.Integer("mapHeight", 1, 100000),
                FreshCellAlpha = r.Integer("freshCellAlpha", 0, 255),
                FreshCellAssetBounds = r.Quad("freshCellAssetBounds", 0f),
                TilesetAsset = r.Text("tilesetAsset"),
            };
            var bridge = r.Section("bridge", b => new
            {
                Floor = b.Integer("floorRow", 0, 100000),
                Walls = b.Integers("wallColumns", 0, 100000),
                Tile = b.Quad("tileBounds", 0f),
            });
            data.FloorRow = bridge.Floor;
            data.WallColumns = bridge.Walls;
            data.BridgeTileBounds = bridge.Tile;
            var units = r.Section("units", u => new { Ppu = u.Positive("pixelsPerUnit"), Steps = u.Integer("stepsPerSecond", 1, 1000) });
            data.PixelsPerUnit = units.Ppu;
            data.StepsPerSecond = units.Steps;
            data.ReferenceScreen = r.Pair("referenceScreen", 1f);
            data.Draw = r.Section("draw", MapDrawData.Read);
            var structures = r.Section("structures", s => new
            {
                Tile = s.Quad("tileBounds", 0f),
                Blocks = s.Each("blocks", BlockData.Read, allowEmpty: true),
            });
            data.StructureTileBounds = structures.Tile;
            data.Blocks = structures.Blocks;
            foreach (var block in data.Blocks)
            {
                r.Check(data.CellSize <= 0 || (block.To.XInt < data.MapWidth / data.CellSize && block.To.YInt < data.MapHeight / data.CellSize),
                    "structures", $"block '{block.Name}' reaches outside the grid");
            }
            r.Check(data.PixelsPerUnit == data.CellSize, "units", "pixelsPerUnit must equal cellSize (one grid cell is one unit)");
            r.Check(data.StepsPerSecond == FixedStepper.StepsPerSecond, "units", $"stepsPerSecond must equal the stepper's {FixedStepper.StepsPerSecond}");
            r.Check(data.MapWidth >= data.CellSize && data.MapHeight >= data.CellSize, "cellSize", "must fit inside the map");
            r.Check(data.CellSize <= 0 || data.FloorRow < data.MapHeight / data.CellSize, "bridge", "floorRow is outside the grid");
            foreach (var column in data.WallColumns)
            {
                r.Check(data.CellSize <= 0 || column < data.MapWidth / data.CellSize, "bridge", $"wall column {column} is outside the grid");
            }

            return data;
        }
    }

    /// <summary>grid.json structures.blocks: one rectangle of Hard cells, by cell index, both corners included.</summary>
    public sealed class BlockData
    {
        public string Name { get; private set; }

        /// <summary>The top-left cell (column, row).</summary>
        public Pair From { get; private set; }

        /// <summary>The bottom-right cell (column, row), included.</summary>
        public Pair To { get; private set; }

        internal static BlockData Read(SettingsReader r)
        {
            var block = new BlockData
            {
                Name = r.Text("name"),
                From = r.Pair("from", 0f, 100000f),
                To = r.Pair("to", 0f, 100000f),
            };
            r.Check(block.From.X == block.From.XInt && block.From.Y == block.From.YInt && block.To.X == block.To.XInt && block.To.Y == block.To.YInt,
                "from", $"block '{block.Name}' must name whole cells");
            r.Check(block.From.X <= block.To.X && block.From.Y <= block.To.Y, "to", $"block '{block.Name}' must not end before it starts");
            return block;
        }
    }

    /// <summary>
    /// grid.json draw:<c>SceneManager.Draw</c>'s clear colour and <c>Map.Draw</c>'s outline and layers (RECONSTRUCTION 5,
    /// 6). Colours are [r, g, b, a] of 0..255.
    /// </summary>
    public sealed class MapDrawData
    {
        public Rgba ClearColour { get; private set; }
        public Rgba OutlineColour { get; private set; }
        public float OutlineThickness { get; private set; }

        /// <summary><c>Map.ShowLayers</c> (true; nothing in the original turns it off).</summary>
        public bool ShowLayers { get; private set; }

        /// <summary><c>Map.Layers</c>, drawn in this order after the outline and before the cells.</summary>
        public IReadOnlyList<MapLayerData> Layers { get; private set; }

        internal static MapDrawData Read(SettingsReader r) => new MapDrawData
        {
            ClearColour = Rgba.FromQuad(r.Quad("clearColour", 0f, 255f)),
            OutlineColour = Rgba.FromQuad(r.Quad("outlineColour", 0f, 255f)),
            OutlineThickness = r.Positive("outlineThickness"),
            ShowLayers = r.Flag("showLayers"),
            Layers = r.Each("layers", MapLayerData.Read, allowEmpty: true),
        };
    }

    /// <summary>One <c>Layer</c>: a texture drawn whole and unscaled at a position, tinted by a colour.</summary>
    public sealed class MapLayerData
    {
        /// <summary>The texture's name (the original's TextureManager key, and the art's file name).</summary>
        public string Asset { get; private set; }

        /// <summary>Its top-left corner in the map's pixels.</summary>
        public Pair Position { get; private set; }

        public Rgba Colour { get; private set; }

        internal static MapLayerData Read(SettingsReader r) => new MapLayerData
        {
            Asset = r.Text("asset"),
            Position = r.Pair("position"),
            Colour = Rgba.FromQuad(r.Quad("colour", 0f, 255f)),
        };
    }

    /// <summary>camera.json: CameraManager's constants (RECONSTRUCTION 7).</summary>
    public sealed class CameraData
    {
        public Pair InitialPosition { get; private set; }
        public float InitialRotation { get; private set; }
        public float InitialScale { get; private set; }
        public CameraMode InitialMode { get; private set; }
        public List<CameraMode> ModeCycle { get; private set; }
        public float FollowGain { get; private set; }
        public float SnapBelow { get; private set; }
        public float Damping { get; private set; }
        public float ScrollStep { get; private set; }
        public Pair EditorScrollSteps { get; private set; }
        public float ZoomStep { get; private set; }
        public float RotateStep { get; private set; }
        public float ScaleExponent { get; private set; }

        internal static CameraData Read(SettingsReader r)
        {
            var data = new CameraData
            {
                InitialPosition = r.Pair("initialPosition"),
                InitialRotation = r.Number("initialRotation"),
                InitialScale = r.Positive("initialScale"),
                InitialMode = r.Choice<CameraMode>("initialMode"),
                ModeCycle = r.Choices<CameraMode>("modeCycle"),
                FollowGain = r.Positive("followGain"),
                SnapBelow = r.Number("snapBelow", 0f),
                Damping = r.Number("damping", 0.0001f, 0.9999f),
                ScrollStep = r.Positive("scrollStep"),
                EditorScrollSteps = r.Pair("editorScrollSteps", 0f),
                ZoomStep = r.Positive("zoomStep"),
                RotateStep = r.Positive("rotateStep"),
                ScaleExponent = r.Positive("scaleExponent"),
            };
            r.Check(data.ModeCycle.Contains(data.InitialMode), "initialMode", "must be in modeCycle");
            return data;
        }
    }

    /// <summary>console.json: the console window, typing and script constants (RECONSTRUCTION 10).</summary>
    public sealed class ConsoleData
    {
        public int Height { get; private set; }
        public int FullHeight { get; private set; }
        public float TextureAlpha { get; private set; }
        public int StartY { get; private set; }
        public int SlideDownStep { get; private set; }
        public int SlideDownEase { get; private set; }
        public int SlideEaseDivisor { get; private set; }
        public int SlideUpStep { get; private set; }
        public int HistoryLines { get; private set; }
        public int HistoryOffset { get; private set; }
        public int HistoryLineHeight { get; private set; }
        public int PromptX { get; private set; }
        public int PromptBlinkX { get; private set; }
        public int PromptFromBottom { get; private set; }
        public int TextX { get; private set; }
        public int TextFromBottom { get; private set; }
        public int HistoryX { get; private set; }
        public int BlinkMilliseconds { get; private set; }
        public string Prompt { get; private set; }
        public string Caret { get; private set; }
        public string Version { get; private set; }
        public SettingsColour TextColour { get; private set; }
        public SettingsColour HistoryColour { get; private set; }
        public SettingsColour PromptColour { get; private set; }
        public string LongCue { get; private set; }
        public string ShortCue { get; private set; }

        /// <summary>Arial_10.spritefont's size, in points (every console text is written in it).</summary>
        public float FontPoints { get; private set; }

        /// <summary>The panel's texture at start (<c>GuiItem("console1", ...)</c>).</summary>
        public string Texture { get; private set; }

        /// <summary>The words <c>console text|history &lt;colour&gt;</c> accept and the XNA colour each sets.</summary>
        public IReadOnlyList<ConsoleWord<SettingsColour>> Colours { get; private set; }

        /// <summary>The words <c>console bg &lt;word&gt;</c> accepts and the texture each sets (only <c>red</c> in the original).</summary>
        public IReadOnlyList<ConsoleWord<string>> Backgrounds { get; private set; }

        /// <summary>The script's texts and colours (<c>ScriptManager.ExecuteScript</c>).</summary>
        public ConsoleScriptData Script { get; private set; }

        public bool TryColour(string word, out SettingsColour colour)
        {
            foreach (var entry in Colours)
            {
                if (entry.Word == word)
                {
                    colour = entry.Value;
                    return true;
                }
            }

            colour = default;
            return false;
        }

        public string FindBackground(string word)
        {
            foreach (var entry in Backgrounds)
            {
                if (entry.Word == word)
                {
                    return entry.Value;
                }
            }

            return null;
        }

        internal static ConsoleData Read(SettingsReader r)
        {
            var data = new ConsoleData
            {
                Height = r.Integer("height", 1, 10000),
                FullHeight = r.Integer("fullHeight", 1, 10000),
                TextureAlpha = r.Number("textureAlpha", 0f, 1f),
                StartY = r.Integer("startY", -10000, 0),
                SlideDownStep = r.Integer("slideDownStep", 1, 1000),
                SlideDownEase = r.Integer("slideDownEase", 0, 1000),
                SlideEaseDivisor = r.Integer("slideEaseDivisor", 1, 1000),
                SlideUpStep = r.Integer("slideUpStep", 1, 1000),
                HistoryLines = r.Integer("historyLines", 1, 10000),
                HistoryOffset = r.Integer("historyOffset", 0, 10000),
                HistoryLineHeight = r.Integer("historyLineHeight", 1, 1000),
                PromptX = r.Integer("promptX", 0, 10000),
                PromptBlinkX = r.Integer("promptBlinkX", 0, 10000),
                PromptFromBottom = r.Integer("promptFromBottom", 0, 10000),
                TextX = r.Integer("textX", 0, 10000),
                TextFromBottom = r.Integer("textFromBottom", 0, 10000),
                HistoryX = r.Integer("historyX", 0, 10000),
                BlinkMilliseconds = r.Integer("blinkMilliseconds", 1, 100000),
                Prompt = r.Text("prompt"),
                Caret = r.Text("caret"),
                Version = r.Text("version"),
                TextColour = r.Choice<SettingsColour>("textColour"),
                HistoryColour = r.Choice<SettingsColour>("historyColour"),
                PromptColour = r.Choice<SettingsColour>("promptColour"),
                LongCue = r.Text("longCue"),
                ShortCue = r.Text("shortCue"),
                FontPoints = r.Positive("fontPoints"),
                Texture = r.Text("texture"),
                Colours = r.Each("colours", c => new ConsoleWord<SettingsColour>(c.Text("word"), c.Choice<SettingsColour>("colour"))),
                Backgrounds = r.Each("backgrounds", b => new ConsoleWord<string>(b.Text("word"), b.Text("texture"))),
                Script = r.Section("script", ConsoleScriptData.Read),
            };
            r.Check(data.FullHeight > data.Height, "fullHeight", "must be taller than the console's height");
            CheckWords(r, "colours", data.Colours);
            CheckWords(r, "backgrounds", data.Backgrounds);
            r.Check(data.Script == null || data.Script.Banner.Count <= data.HistoryLines, "script", "banner has more lines than the history");
            return data;
        }

        static void CheckWords<T>(SettingsReader r, string key, IReadOnlyList<ConsoleWord<T>> words)
        {
            var seen = new HashSet<string>();
            foreach (var word in words)
            {
                r.Check(seen.Add(word.Word), key, $"lists '{word.Word}' twice");
                r.Check(word.Word == word.Word.ToLowerInvariant() && word.Word.IndexOf(' ') < 0, key, $"'{word.Word}' must be one lower-case word (the script lower-cases the line and splits it on spaces)");
            }
        }
    }

    /// <summary>A script word and what it stands for.</summary>
    public sealed class ConsoleWord<T>
    {
        public ConsoleWord(string word, T value)
        {
            Word = word;
            Value = value;
        }

        public string Word { get; }
        public T Value { get; }
    }

    /// <summary>console.json script: the texts and colours <c>ScriptManager.ExecuteScript</c> writes (RECONSTRUCTION 10.3).</summary>
    public sealed class ConsoleScriptData
    {
        /// <summary><c>msg hi</c>'s message and colour ("Hello!", Wheat).</summary>
        public string Hello { get; private set; }
        public SettingsColour HelloColour { get; private set; }

        /// <summary>What <c>say</c> and <c>yell</c> wrap their words in (two apostrophes).</summary>
        public string Quote { get; private set; }

        /// <summary>What <c>yell</c> adds before the closing quote ("!").</summary>
        public string YellEnd { get; private set; }
        public SettingsColour SayColour { get; private set; }
        public SettingsColour YellColour { get; private set; }

        public string MusicPaused { get; private set; }
        public string MusicUnpaused { get; private set; }

        /// <summary><c>"Mute: " + g.OnOff(Muted)</c>.</summary>
        public string MutePrefix { get; private set; }
        public string On { get; private set; }
        public string Off { get; private set; }

        /// <summary>The pause and mute messages' colour (LightSteelBlue).</summary>
        public SettingsColour NoticeColour { get; private set; }

        /// <summary><c>lee</c> / <c>buzzer</c>'s banner: line i is written to History[i].</summary>
        public IReadOnlyList<string> Banner { get; private set; }

        /// <summary>The line a too-short <c>console text|history|bg</c> or <c>commander</c> writes instead of crashing (BIBLE defect 11, fixed).</summary>
        public string UsageConsoleText { get; private set; }
        public string UsageConsoleHistory { get; private set; }
        public string UsageConsoleBg { get; private set; }
        public string UsageCommander { get; private set; }

        internal static ConsoleScriptData Read(SettingsReader r) => new ConsoleScriptData
        {
            Hello = r.Text("hello"),
            HelloColour = r.Choice<SettingsColour>("helloColour"),
            Quote = r.Text("quote"),
            YellEnd = r.Text("yellEnd"),
            SayColour = r.Choice<SettingsColour>("sayColour"),
            YellColour = r.Choice<SettingsColour>("yellColour"),
            MusicPaused = r.Text("musicPaused"),
            MusicUnpaused = r.Text("musicUnpaused"),
            MutePrefix = r.Text("mutePrefix"),
            On = r.Text("on"),
            Off = r.Text("off"),
            NoticeColour = r.Choice<SettingsColour>("noticeColour"),
            Banner = r.Texts("banner", blankEntries: true),
            UsageConsoleText = r.Text("usageConsoleText"),
            UsageConsoleHistory = r.Text("usageConsoleHistory"),
            UsageConsoleBg = r.Text("usageConsoleBg"),
            UsageCommander = r.Text("usageCommander"),
        };
    }

    /// <summary>audio.json: the cue table, categories and songs (RECONSTRUCTION 11).</summary>
    public sealed class AudioData
    {
        public float Volume { get; private set; }
        public bool Muted { get; private set; }
        public IReadOnlyList<AudioCategory> Categories { get; private set; }
        public IReadOnlyList<AudioCue> Cues { get; private set; }
        public IReadOnlyList<Song> Songs { get; private set; }

        /// <summary>The cue a running character makes (<c>Action_Run</c>'s <c>step2</c>), once per run-strip frame.</summary>
        public string RunCue { get; private set; }

        /// <summary>The cue a wall-sliding character makes (<c>Action_Wallslide</c>'s <c>drag</c>), once per clip.</summary>
        public string WallSlideCue { get; private set; }

        public Song FindSong(string name)
        {
            foreach (var s in Songs)
            {
                if (s.Name == name)
                {
                    return s;
                }
            }

            return null;
        }

        public AudioCue FindCue(string cue)
        {
            foreach (var c in Cues)
            {
                if (c.Cue == cue)
                {
                    return c;
                }
            }

            return null;
        }

        internal static AudioData Read(SettingsReader r)
        {
            var data = new AudioData
            {
                Volume = r.Number("volume", 0f, 1f),
                Muted = r.Flag("muted"),
                RunCue = r.Text("runCue"),
                WallSlideCue = r.Text("wallSlideCue"),
                Categories = r.Each("categories", c => new AudioCategory
                {
                    Name = c.Text("name"),
                    MaxInstances = c.Integer("maxInstances", 1, 255),
                }),
                Cues = r.Each("cues", AudioCue.Read),
                Songs = r.Each("songs", s => new Song
                {
                    Name = s.Text("name"),
                    File = s.Text("file"),
                    PlayedBy = s.Text("playedBy", true),
                }),
            };
            var seen = new HashSet<string>();
            foreach (var cue in data.Cues)
            {
                r.Check(seen.Add(cue.Cue), "cues", $"lists '{cue.Cue}' twice");
            }

            var songs = new HashSet<string>();
            foreach (var song in data.Songs)
            {
                r.Check(songs.Add(song.Name), "songs", $"lists '{song.Name}' twice");
            }

            return data;
        }
    }

    public sealed class AudioCategory
    {
        public string Name { get; internal set; }
        public int MaxInstances { get; internal set; }
    }

    public sealed class Song
    {
        public string Name { get; internal set; }
        public string File { get; internal set; }
        public string PlayedBy { get; internal set; }
    }

    public sealed class AudioCue
    {
        public string Bank { get; private set; }
        public string Cue { get; private set; }
        public string Wave { get; private set; }
        public float Seconds { get; private set; }

        /// <summary>XACT volume in hundredths of a dB.</summary>
        public int Volume { get; private set; }

        /// <summary>XACT loop count (255 is infinite, 0 plays once).</summary>
        public int Loop { get; private set; }

        public string PlayedBy { get; private set; }

        internal static AudioCue Read(SettingsReader r) => new AudioCue
        {
            Bank = r.Text("bank"),
            Cue = r.Text("cue"),
            Wave = r.Text("wave"),
            Seconds = r.Number("seconds", 0f),
            Volume = r.Integer("volume", -10000, 0),
            Loop = r.Integer("loop", 0, 255),
            PlayedBy = r.Text("playedBy", true),
        };
    }

    /// <summary>menu.json: the main menu tree and its look (RECONSTRUCTION 9.2).</summary>
    public sealed class MenuData
    {
        public string Background { get; private set; }
        public float BackgroundAlpha { get; private set; }
        public string Arrow { get; private set; }
        public int ArrowOffset { get; private set; }
        public int ItemX { get; private set; }
        public int ItemY { get; private set; }
        public int ItemStep { get; private set; }
        public Pair TitlePosition { get; private set; }
        public int DescriptionFromBottom { get; private set; }
        public SettingsColour ItemColour { get; private set; }
        public SettingsColour SelectedColour { get; private set; }
        public SettingsColour TextColour { get; private set; }
        public string MoveCue { get; private set; }
        public string SelectCue { get; private set; }

        /// <summary>The cue <c>Action_ToggleMainMenu</c> plays opening the menu (<c>menu_open</c>).</summary>
        public string OpenCue { get; private set; }

        /// <summary>The cue <c>Action_ToggleMainMenu</c> plays closing the menu (<c>menu_close</c>).</summary>
        public string CloseCue { get; private set; }

        /// <summary>The menu's sprite font (<c>Lucida.spritefont</c>: Lucida Console), used for items, title and description.</summary>
        public string Font { get; private set; }

        /// <summary>The sprite font's size in points (14).</summary>
        public float FontPoints { get; private set; }

        public IReadOnlyList<MenuLevel> Levels { get; private set; }

        internal static MenuData Read(SettingsReader r)
        {
            var data = new MenuData
            {
                Background = r.Text("background"),
                BackgroundAlpha = r.Number("backgroundAlpha", 0f, 1f),
                Arrow = r.Text("arrow"),
                ArrowOffset = r.Integer("arrowOffset", -1000, 1000),
                ItemX = r.Integer("itemX", 0, 10000),
                ItemY = r.Integer("itemY", 0, 10000),
                ItemStep = r.Integer("itemStep", 1, 1000),
                TitlePosition = r.Pair("titlePosition", 0f),
                DescriptionFromBottom = r.Integer("descriptionFromBottom", 0, 10000),
                ItemColour = r.Choice<SettingsColour>("itemColour"),
                SelectedColour = r.Choice<SettingsColour>("selectedColour"),
                TextColour = r.Choice<SettingsColour>("textColour"),
                MoveCue = r.Text("moveCue"),
                SelectCue = r.Text("selectCue"),
                OpenCue = r.Text("openCue"),
                CloseCue = r.Text("closeCue"),
                Font = r.Text("font"),
                FontPoints = r.Number("fontPoints", 1f, 200f),
                Levels = r.Each("levels", MenuLevel.Read),
            };
            if (data.Levels.Count > 0)
            {
                r.Check(data.Levels[0].Parent == -1, "levels", "level 0 is the root and its parent must be -1");
                r.Check(data.Levels[0].Items.Count > 0, "levels", "level 0, the root, must have an item to highlight");
            }

            for (var i = 0; i < data.Levels.Count; i++)
            {
                var level = data.Levels[i];
                r.Check(i == 0 || (level.Parent >= 0 && level.Parent < data.Levels.Count && level.Parent != i), "levels",
                    $"level {i} ('{level.Title}') has parent {level.Parent}, which is not another level");
                var walk = i;
                var steps = 0;
                while (walk > 0 && steps++ <= data.Levels.Count)
                {
                    var parent = data.Levels[walk].Parent;
                    walk = parent >= 0 && parent < data.Levels.Count ? parent : 0;
                }

                r.Check(walk == 0, "levels", $"level {i} ('{level.Title}') never reaches the root by its parents");
                foreach (var item in level.Items)
                {
                    var opens = item.Action == MenuAction.Open;
                    r.Check(!opens || (item.Target >= 0 && item.Target < data.Levels.Count), "levels",
                        $"item '{item.Text}' of level {i} opens level {item.Target}, which does not exist");
                    r.Check(!opens || item.Target < 0 || item.Target >= data.Levels.Count || data.Levels[item.Target].Items.Count > 0, "levels",
                        $"item '{item.Text}' of level {i} opens level {item.Target}, which has no item to highlight");
                }
            }

            return data;
        }
    }

    public sealed class MenuLevel
    {
        public string Title { get; private set; }
        public int Parent { get; private set; }
        public IReadOnlyList<MenuItem> Items { get; private set; }

        internal static MenuLevel Read(SettingsReader r) => new MenuLevel
        {
            Title = r.Text("title"),
            Parent = r.Integer("parent", -1, 1000),
            Items = r.Each("items", MenuItem.Read, allowEmpty: true),
        };
    }

    public sealed class MenuItem
    {
        public string Text { get; private set; }
        public string Description { get; private set; }
        public MenuAction Action { get; private set; }
        public int Target { get; private set; }

        internal static MenuItem Read(SettingsReader r) => new MenuItem
        {
            Text = r.Text("text"),
            Description = r.Text("description", true),
            Action = r.Choice<MenuAction>("action"),
            Target = r.Integer("target", -1, 1000),
        };
    }

    /// <summary>segments.json: the map segment definitions of maps.zdx (RECONSTRUCTION 5.1).</summary>
    public sealed class SegmentData
    {
        public IReadOnlyList<SegmentSource> Sources { get; private set; }
        public IReadOnlyList<SegmentDefinition> Definitions { get; private set; }

        internal static SegmentData Read(SettingsReader r)
        {
            var data = new SegmentData
            {
                Sources = r.Each("sources", s => new SegmentSource
                {
                    Id = s.Integer("id", 1, 1000),
                    Image = s.Text("image"),
                    Size = s.Pair("size", 1f),
                }),
                Definitions = r.Each("definitions", SegmentDefinition.Read),
            };
            var names = new HashSet<string>();
            foreach (var d in data.Definitions)
            {
                r.Check(names.Add(d.Name), "definitions", $"lists '{d.Name}' twice");
                SegmentSource source = null;
                foreach (var s in data.Sources)
                {
                    if (s.Id == d.Source)
                    {
                        source = s;
                    }
                }

                r.Check(source != null, "definitions", $"'{d.Name}' uses source {d.Source}, which is not listed");
                r.Check(d.X1 > d.X0 && d.Y1 > d.Y0, "definitions", $"'{d.Name}' must have x1 > x0 and y1 > y0");
                r.Check(source == null || (d.X1 <= source.Size.X && d.Y1 <= source.Size.Y), "definitions",
                    $"'{d.Name}' reaches outside its source image");
            }

            return data;
        }
    }

    public sealed class SegmentSource
    {
        public int Id { get; internal set; }
        public string Image { get; internal set; }
        public Pair Size { get; internal set; }
    }

    public sealed class SegmentDefinition
    {
        public string Name { get; private set; }
        public int Source { get; private set; }
        public int X0 { get; private set; }
        public int Y0 { get; private set; }
        public int X1 { get; private set; }
        public int Y1 { get; private set; }
        public int Flag { get; private set; }
        public int Width => X1 - X0;
        public int Height => Y1 - Y0;

        internal static SegmentDefinition Read(SettingsReader r)
        {
            var d = new SegmentDefinition
            {
                Name = r.Text("name"),
                Source = r.Integer("source", 1, 1000),
                Flag = r.Integer("flag", 0, 1000),
            };
            var rect = r.Integers("rect", 0, 100000);
            r.Check(rect.Count == 4, "rect", "must hold exactly four whole numbers");
            if (rect.Count == 4)
            {
                d.X0 = rect[0];
                d.Y0 = rect[1];
                d.X1 = rect[2];
                d.Y1 = rect[3];
            }
            return d;
        }
    }

    /// <summary>names.json: the name tables of RandomName (RECONSTRUCTION 4.1, 14).</summary>
    public sealed class NameData
    {
        public IReadOnlyList<string> Male { get; private set; }
        public IReadOnlyList<string> Female { get; private set; }
        public IReadOnlyList<string> Beast { get; private set; }

        public IReadOnlyList<string> For(Gender gender) => gender == Gender.Male ? Male : Female;

        internal static NameData Read(SettingsReader r) => new NameData
        {
            Male = r.Texts("male"),
            Female = r.Texts("female"),
            Beast = r.Texts("beast"),
        };
    }
}
