using System;
using System.Collections.Generic;

namespace Unity.Rea.ProjectVania.Core.Settings
{
    /// <summary>
    /// The settings registry: every tuning figure, content definition and text of the game, loaded from the data
    /// files (StreamingAssets/Settings in the build) through an <see cref="ISettingsSource"/> and validated on load.
    /// The files hold the original's own figures in its own units (pixels, pixels per second, per frame); Core keeps
    /// them as they are, and <see cref="Units"/> is the one place they meet the Runtime's units.
    /// </summary>
    public sealed class GameSettings
    {
        /// <summary>The folder under StreamingAssets the data files live in.</summary>
        public const string Folder = "Settings";

        public const string PlayerFile = "player.json";
        public const string CharactersFile = "characters.json";
        public const string AnimationsFile = "animations.json";
        public const string GridFile = "grid.json";
        public const string MenuFile = "menu.json";
        public const string CameraFile = "camera.json";
        public const string ConsoleFile = "console.json";
        public const string AudioFile = "audio.json";
        public const string SegmentsFile = "segments.json";
        public const string NamesFile = "names.json";
        public const string InputFile = "input.json";
        public const string HudFile = "hud.json";
        public const string EditorFile = "editor.json";
        public const string EffectsFile = "effects.json";
        public const string ContentFile = "content.json";
        public const string OnlineFile = "online.json";

        /// <summary>Every data file the registry reads.</summary>
        public static readonly string[] Files =
        {
            PlayerFile, CharactersFile, AnimationsFile, GridFile, MenuFile, CameraFile, ConsoleFile, AudioFile, SegmentsFile, NamesFile, InputFile, HudFile, EditorFile, EffectsFile, ContentFile, OnlineFile,
        };

        private GameSettings()
        {
        }

        public PlayerData Player { get; private set; }
        public CharacterData Characters { get; private set; }
        public AnimationData Animations { get; private set; }
        public GridData Grid { get; private set; }
        public MenuData Menu { get; private set; }
        public CameraData Camera { get; private set; }
        public ConsoleData Console { get; private set; }
        public AudioData Audio { get; private set; }
        public SegmentData Segments { get; private set; }
        public NameData Names { get; private set; }
        public InputData Input { get; private set; }
        public HudData Hud { get; private set; }
        public EditorData Editor { get; private set; }
        public EffectData Effects { get; private set; }
        public ContentData Content { get; private set; }

        /// <summary>The online session's figures (U.VAN-T31); local play never reads them.</summary>
        public OnlineData Online { get; private set; }

        /// <summary>The original-to-Runtime unit mapping (grid.json units).</summary>
        public OriginalUnits Units { get; private set; }

        /// <summary>
        /// Reads and validates every data file from <paramref name="source"/>. Throws a <see cref="SettingsException"/>
        /// listing every problem (a missing file, bad JSON with its line, a missing or unknown key, a value of the wrong
        /// type or out of range, a reference to something that does not exist) if anything is wrong.
        /// </summary>
        public static GameSettings Load(ISettingsSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var errors = new List<string>();
            var settings = new GameSettings
            {
                Player = Read(source, PlayerFile, errors, PlayerData.Read),
                Characters = Read(source, CharactersFile, errors, CharacterData.Read),
                Animations = Read(source, AnimationsFile, errors, AnimationData.Read),
                Grid = Read(source, GridFile, errors, GridData.Read),
                Menu = Read(source, MenuFile, errors, MenuData.Read),
                Camera = Read(source, CameraFile, errors, CameraData.Read),
                Console = Read(source, ConsoleFile, errors, ConsoleData.Read),
                Audio = Read(source, AudioFile, errors, AudioData.Read),
                Segments = Read(source, SegmentsFile, errors, SegmentData.Read),
                Names = Read(source, NamesFile, errors, NameData.Read),
                Input = Read(source, InputFile, errors, InputData.Read),
                Hud = Read(source, HudFile, errors, HudData.Read),
                Editor = Read(source, EditorFile, errors, EditorData.Read),
                Effects = Read(source, EffectsFile, errors, EffectData.Read),
                Content = Read(source, ContentFile, errors, ContentData.Read),
                Online = Read(source, OnlineFile, errors, OnlineData.Read),
            };

            if (errors.Count == 0)
            {
                settings.CheckReferences(errors);
            }

            if (errors.Count > 0)
            {
                throw new SettingsException(errors, source.Describe);
            }

            settings.Units = new OriginalUnits(settings.Grid.PixelsPerUnit, settings.Grid.StepsPerSecond);
            return settings;
        }

        private static T Read<T>(ISettingsSource source, string file, List<string> errors, Func<SettingsReader, T> read)
            where T : class
        {
            string text;
            try
            {
                text = source.Read(file);
            }
            catch (Exception e) when (!(e is SettingsException))
            {
                errors.Add($"{file}: could not be read ({e.Message})");
                return null;
            }

            if (text == null)
            {
                errors.Add($"{file}: not found");
                return null;
            }

            JsonValue root;
            try
            {
                root = Json.Parse(text);
            }
            catch (JsonException e)
            {
                errors.Add($"{file}: is not valid JSON ({e.Message})");
                return null;
            }

            if (root.Kind != JsonKind.Object)
            {
                errors.Add($"{file}: must hold one JSON object {{ ... }}");
                return null;
            }

            var before = errors.Count;
            var reader = new SettingsReader(root, file, errors);
            var data = read(reader);
            reader.Done();
            return errors.Count == before ? data : null;
        }

        /// <summary>The checks that span files: every name one file uses must exist in the file that defines it.</summary>
        private void CheckReferences(List<string> errors)
        {
            foreach (var character in Characters.Characters)
            {
                var sheet = Animations.Find(character.Sheet);
                if (sheet == null)
                {
                    errors.Add($"{CharactersFile}: '{character.Name}' uses sheet '{character.Sheet}', which {AnimationsFile} does not define");
                    continue;
                }

                // Every move in the character's move set needs the strip its action plays (U.VAN-T25: a missing one used
                // to fall back to idle, as the original's KeyNotFoundException crash was not carried over).
                foreach (var move in character.MoveSet)
                {
                    var required = global::Unity.Rea.ProjectVania.Core.Characters.CharacterAnimator.ClipFor(move);
                    if (required != null && sheet.Find(required) == null)
                    {
                        errors.Add($"{AnimationsFile}: sheet '{sheet.Sheet}' has no '{required}' animation, which '{character.Name}''s {move} plays");
                    }
                }
            }

            if (Characters.Roster.Count > Player.SpawnPositions.Count)
            {
                errors.Add($"{CharactersFile}: the roster has {Characters.Roster.Count} characters but {PlayerFile} has only {Player.SpawnPositions.Count} spawn positions");
            }

            foreach (var cue in new[] { Console.LongCue, Console.ShortCue, Menu.MoveCue, Menu.SelectCue, Menu.OpenCue, Menu.CloseCue })
            {
                if (Audio.FindCue(cue) == null)
                {
                    errors.Add($"{AudioFile}: has no cue '{cue}', which {ConsoleFile} or {MenuFile} plays");
                }
            }

            foreach (var cue in new[] { Audio.RunCue, Audio.WallSlideCue })
            {
                if (Audio.FindCue(cue) == null)
                {
                    errors.Add($"{AudioFile}: has no cue '{cue}', which a running or wall-sliding character plays");
                }
            }

            foreach (var song in new[] { Gui.ConsoleScript.MusicSong, Gui.ConsoleScript.BattleSong })
            {
                if (Audio.FindSong(song) == null)
                {
                    errors.Add($"{AudioFile}: has no song '{song}', which the console's 'play {song}' starts");
                }
            }

            var width = Grid.MapWidth;
            var height = Grid.MapHeight;
            foreach (var position in Player.SpawnPositions)
            {
                CheckInMap(errors, position, width, height, "a spawn position");
            }

            CheckInMap(errors, Player.ResetPosition, width, height, "the reset position");

            // Online every seat starts on its own character, with that character's camera (U.VAN-T31).
            if (Online.MaxPlayers > Characters.Roster.Count)
            {
                errors.Add($"{OnlineFile}: maxPlayers {Online.MaxPlayers} is more than the {Characters.Roster.Count} characters of {CharactersFile}'s roster");
            }

            if (Hud.Screen.X != Grid.ReferenceScreen.X || Hud.Screen.Y != Grid.ReferenceScreen.Y)
            {
                errors.Add($"{HudFile}: screen ({Hud.Screen.X}, {Hud.Screen.Y}) must be {GridFile}'s referenceScreen ({Grid.ReferenceScreen.X}, {Grid.ReferenceScreen.Y})");
            }

            // A painted cell takes one palette tile, so a tile must be exactly a cell.
            if (Editor.TileSize != Grid.CellSize)
            {
                errors.Add($"{EditorFile}: tileSize {Editor.TileSize} must be {GridFile}'s cellSize {Grid.CellSize}");
            }

            if (Editor.Palette.TilesetSize.X < Editor.TileSize || Editor.Palette.TilesetSize.Y < Editor.TileSize)
            {
                errors.Add($"{EditorFile}: palette.tilesetSize must hold at least one {Editor.TileSize} px tile");
            }

            // content.json's segment atlas is the image segments.json's definitions were cut from.
            var atlasImage = Content.SegmentAtlas.Asset + ".png";
            var sourceImages = new List<string>();
            foreach (var segmentSource in Segments.Sources)
            {
                sourceImages.Add(segmentSource.Image);
            }

            if (!sourceImages.Contains(atlasImage))
            {
                errors.Add($"{ContentFile}: segmentAtlas.asset '{Content.SegmentAtlas.Asset}' is not {SegmentsFile}'s source image");
            }

            // content.json's drawn area layer is the one grid.json's draw.layers actually draws.
            foreach (var layer in Grid.Draw.Layers)
            {
                if (layer.Asset != Content.AreaLayers.Drawn)
                {
                    errors.Add($"{GridFile}: draw.layers draws '{layer.Asset}', but {ContentFile}'s areaLayers.drawn is '{Content.AreaLayers.Drawn}'");
                }
            }
        }

        private static void CheckInMap(List<string> errors, Pair position, int width, int height, string what)
        {
            if (position.X >= width || position.Y >= height)
            {
                errors.Add($"{PlayerFile}: {what} ({position.X}, {position.Y}) is outside the {width} x {height} map");
            }
        }
    }

    /// <summary>
    /// The one mapping from the original's units to the Runtime's, read from grid.json: pixels become units at
    /// <see cref="PixelsPerUnit"/> (one grid cell is one unit), and figures the original added once per frame become
    /// per-second at <see cref="StepsPerSecond"/>. Core itself keeps the original's pixels and frames.
    /// </summary>
    public readonly struct OriginalUnits
    {
        public OriginalUnits(float pixelsPerUnit, int stepsPerSecond)
        {
            PixelsPerUnit = pixelsPerUnit;
            StepsPerSecond = stepsPerSecond;
        }

        public float PixelsPerUnit { get; }
        public int StepsPerSecond { get; }

        /// <summary>A length in pixels as units.</summary>
        public float ToUnits(float pixels) => pixels / PixelsPerUnit;

        /// <summary>A length in units as pixels.</summary>
        public float ToPixels(float units) => units * PixelsPerUnit;

        /// <summary>A figure added once per step, as a figure per second.</summary>
        public float PerSecond(float perStep) => perStep * StepsPerSecond;
    }
}
