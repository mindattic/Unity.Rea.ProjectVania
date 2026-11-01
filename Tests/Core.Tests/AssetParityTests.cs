using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Settings;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    /// <summary>
    /// U.VAN-US-A17: every sprite, texture, sound, font and data file the original loaded is in the rebuild. The
    /// original's live asset list (RECONSTRUCTION 13, captured once by REA-ing the running XNA build's texture, font,
    /// effect and audio managers) is reproduced here as <see cref="OriginalPngs"/>, <see cref="LoadedWaves"/>/
    /// <see cref="NeverLoadedWaves"/>, <see cref="Songs"/>, the font entries content.json carries and
    /// <see cref="OriginalDataFiles"/>/<see cref="OriginalEffects"/>, the same way the figures in GameSettingsTests are
    /// the original's own numbers. Each test diffs that list against the rebuild's content registry: the files
    /// actually shipped under Resources (the asset side), GameSettings.Content/Segments/Audio/Effects, the settings
    /// registry's parsed data (the data side), and SpriteEffect's own dispatch (the effect side), so none of them can
    /// silently drop something the original had.
    /// </summary>
    public class AssetParityTests
    {
        internal static string ResourcesFolder
        {
            get
            {
                var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while (dir != null)
                {
                    var candidate = Path.Combine(dir.FullName, "Assets", "Unity.Rea.ProjectVania", "Resources");
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }

                    dir = dir.Parent;
                }

                throw new DirectoryNotFoundException("Assets/Unity.Rea.ProjectVania/Resources not found above the test folder.");
            }
        }

        private static GameSettings Load() => GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));

        private static bool Exists(string resourceRelativePathWithoutExtension, string extension) =>
            File.Exists(Path.Combine(ResourcesFolder, resourceRelativePathWithoutExtension.Replace('/', Path.DirectorySeparatorChar) + extension));

        // --- The original's live asset list (RECONSTRUCTION 13), one entry per file the content project shipped,
        // paired with where the rebuild keeps it. 132 PNGs in total. ---------------------------------------------

        internal static readonly (string original, string rebuilt)[] OriginalPngs = new (string original, string rebuilt)[]
        {
            ("Application/GameThumbnail", "Gui/GameThumbnail"),
            ("Areas/area_1", "World/area_1"),
            ("Areas/area_1_b1", "World/area_1_b1"),
            ("Areas/area_1_b2", "World/area_1_b2"),
            ("Areas/area_1_f1", "World/area_1_f1"),
            ("Areas/area_1_f2", "World/area_1_f2"),
            ("Characters/Playable/Leo/idle", "Characters/leo/idle"),
            ("Characters/Playable/Leo/run", "Characters/leo/run"),
            ("Characters/Playable/Morris/fall", "Characters/morris/fall"),
            ("Characters/Playable/Morris/idle", "Characters/morris/idle"),
            ("Characters/Playable/Morris/jump", "Characters/morris/jump"),
            ("Characters/Playable/Morris/kneel", "Characters/morris/kneel"),
            ("Characters/Playable/Morris/run", "Characters/morris/run"),
            ("Characters/Playable/Morris/slide", "Characters/morris/slide"),
            ("Characters/Playable/Morris/wallslide", "Characters/morris/wallslide"),
            ("Characters/Playable/Richter/idle", "Characters/richter/idle"),
            ("Characters/Playable/Richter/run", "Characters/richter/run"),
            ("Characters/Playable/Shanoa/idle", "Characters/shanoa/idle"),
            ("Characters/Playable/Shanoa/run", "Characters/shanoa/run"),
            ("Characters/Playable/Untitled", "Characters/Untitled"),
            ("Characters/frame_10", "Characters/frame_10"),
            ("Gui/Hud/action_cross_frame", "Gui/action_cross_frame"),
            ("Gui/Hud/action_cross_glass", "Gui/action_cross_glass"),
            ("Gui/Hud/arrow1", "Gui/arrow1"),
            ("Gui/Hud/bar_health", "Gui/bar_health"),
            ("Gui/Hud/demo_gui", "Gui/demo_gui"),
            ("Gui/Hud/demo_gui_0", "Gui/demo_gui_0"),
            ("Gui/Hud/gui_0", "Gui/gui_0"),
            ("Gui/Hud/gui_1", "Gui/gui_1"),
            ("Gui/Hud/portrait_frame", "Gui/portrait_frame"),
            ("Gui/Hud/portrait_glass", "Gui/portrait_glass"),
            ("Gui/Hud/quick_bar_frame", "Gui/quick_bar_frame"),
            ("Gui/Hud/quick_bar_glass", "Gui/quick_bar_glass"),
            ("Gui/Icons/action_A", "Gui/action_A"),
            ("Gui/Icons/action_B", "Gui/action_B"),
            ("Gui/Icons/action_X", "Gui/action_X"),
            ("Gui/Icons/action_Y", "Gui/action_Y"),
            ("MapEditor/block", "World/block"),
            ("MapEditor/cell", "World/cell"),
            ("MapEditor/maps1", "World/maps1"),
            ("Textures/blank", "Gui/blank"),
            ("Textures/block", "Gui/block"),
            ("Textures/brick", "Gui/brick"),
            ("Textures/console1", "Gui/console1"),
            ("Textures/console2", "Gui/console2"),
            ("Textures/cursor_hand_topleft", "Gui/cursor_hand_topleft"),
            ("Textures/foggy_woods", "Gui/foggy_woods"),
            ("Textures/menu1", "Gui/menu1"),
            ("Textures/menu2", "Gui/menu2"),
            ("Textures/metal1", "Gui/metal1"),
            ("Textures/red", "Gui/red"),
            ("Textures/white", "Gui/white"),
            ("TilePalette/square_selector", "Gui/square_selector"),
            ("TilePalette/tilepalette_background", "Gui/tilepalette_background"),
            ("Tileset/default", "World/tileset_default"),
        }
        .Concat(SkillIcons().Select(icon => ($"Gui/Icons/icon_{icon}", $"Gui/icon_{icon}")))
        .Concat(Portraits().Select(p => ($"Gui/Portraits/{p}", $"Gui/{p}")))
        .Concat(Banners().Select(b => ($"Banners/{b}", $"Banners/{b}")))
        .Concat(SpriteSheets().Select(s => ($"SpriteSheets/{s}", $"SpriteSheets/{s}")))
        .Concat(Particles().Select(p => ($"Particles/{p}", $"Particles/{p}")))
        .Concat(Selectors().Select(s => ($"Selectors/{s}", $"Selectors/{s}")))
        .ToArray();

        private static string[] SkillIcons() => new[]
        {
            "assassinate", "attack", "aura", "battlecry", "bow", "consecrate", "crossbow", "falcon", "fireball",
            "flashheal", "icewall", "lifewell", "lightning", "mace", "might", "parry", "phalanx", "poisondart",
            "pray", "redemption", "regen", "repair", "selectsquad", "shield", "shoot", "stab", "trap", "vanish", "wand",
        };

        private static string[] Portraits() => new[]
        {
            "portrait_1", "portrait_archer", "portrait_paladin", "portrait_priest", "portrait_rogue", "portrait_soldier", "portrait_wizard",
        };

        private static string[] Banners() => Enumerable.Range(1, 15).Select(i => $"banner_{i}").ToArray();

        private static string[] SpriteSheets() => new[]
        {
            "hunter", "knight", "leo", "magitek_brown", "magitek_yellow", "paladin", "priest", "rogue", "soldier",
            "soldier_blue", "soldier_brown", "soldier_gray", "soldier_halo", "soldier_red", "soldier_yellow", "wizard",
        };

        private static string[] Particles() => new[] { "particle_bug", "particle_leaf", "particle_rain", "particle_sand", "particle_snow" };

        private static string[] Selectors() => new[] { "selector", "selector_S", "selector_M", "selector_L", "selector_XL" };

        /// <summary>
        /// 14 WAVs in the original's Content\Audio folder; 2 are never loaded by any AudioEngine bank
        /// (RECONSTRUCTION §11: <c>bonga.wav</c> is in the folder and not in any bank; <c>music1.wav</c>'s bank,
        /// <c>music</c>, is never created) and are deliberately not shipped. The other 12 are every cue's own wave.
        /// </summary>
        internal static readonly string[] LoadedWaves =
        {
            "footstep_1", "footstep_2", "drag", "step1", "jump1", "step2",
            "machine_1", "machine_1_short", "menu_open", "menu_close", "menu_move", "menu_select",
        };

        internal static readonly string[] NeverLoadedWaves = { "bonga", "music1" };

        internal static readonly string[] Songs = { "music", "battle" };

        /// <summary>
        /// The original's non-picture, non-audio data files it actually loaded (RECONSTRUCTION 5.1, 11, 13), paired
        /// with the settings-registry data that now carries everything each one held. <c>MapEditor\maps.txt</c> is not
        /// here: it is the same eleven definitions as <c>maps.zdx</c> under a plain extension, and the original's own
        /// editor reads <c>maps.zdx</c>, not this copy, so it is deliberately not a second source.
        /// </summary>
        internal static readonly (string original, string rebuiltHome)[] OriginalDataFiles =
        {
            ("Content/maps.zdx", "segments.json (the eleven segment definitions)"),
            ("Content/Audio/audio.xap", "audio.json (every cue, category and song the XACT project defined)"),
            ("Content/Audio/Win/audio.xgs", "audio.json's five categories (Global, Default, Music, Sfx, Ambience)"),
            ("Content/Audio/Win/sfx.xsb", "audio.json's six 'sfx' bank cues"),
            ("Content/Audio/Win/ambience.xsb", "audio.json's six 'ambience' bank cues"),
            ("Content/Audio/Win/music.xsb", "audio.json's one 'music' bank cue (music1, the bank the original's own AudioEngine never loads)"),
        };

        [Test]
        public void DataFile_MapsZdxIsFullyCapturedAsTheSegmentDefinitions()
        {
            var segments = Load().Segments;
            Assert.That(segments.Definitions.Count, Is.EqualTo(11), OriginalDataFiles[0].original);
            Assert.That(segments.Definitions.Select(d => d.Name), Is.EquivalentTo(new[]
            {
                "grass1", "grass2", "grass3", "rock1", "rock2", "grave1", "grave2", "block1", "block2", "blackbox", "torch",
            }));
        }

        [Test]
        public void DataFile_TheXactProjectAndItsBanksAreFullyCapturedInAudioJson()
        {
            var audio = Load().Audio;
            Assert.That(audio.Categories.Count, Is.EqualTo(5), OriginalDataFiles[2].original);
            Assert.That(audio.Cues.Count, Is.EqualTo(13), OriginalDataFiles[0].original);
            Assert.That(audio.Cues.Count(c => c.Bank == "sfx"), Is.EqualTo(6), OriginalDataFiles[3].original);
            Assert.That(audio.Cues.Count(c => c.Bank == "ambience"), Is.EqualTo(6), OriginalDataFiles[4].original);
            Assert.That(audio.Cues.Count(c => c.Bank == "music"), Is.EqualTo(1), OriginalDataFiles[5].original);
            Assert.That(audio.Songs.Count, Is.EqualTo(2));
        }

        /// <summary>The five shaders <c>EffectManager</c> loaded (RECONSTRUCTION 6.1, 13); rebuilt as
        /// <see cref="EffectPreset"/>'s five cases, each one implemented in <see cref="SpriteEffect.ShadeFloat"/>.</summary>
        internal static readonly string[] OriginalEffects = { "filter", "negative", "bloom", "pause", "water" };

        [Test]
        public void EveryOriginalEffectIsImplemented()
        {
            Assert.That(Enum.GetNames(typeof(EffectPreset)).Select(n => n.ToLowerInvariant()), Is.EquivalentTo(OriginalEffects));
            var data = Load().Effects;
            var texture = new PixelImage(1, 1) { [0, 0] = new Rgba(255, 255, 255, 255) };
            foreach (EffectPreset preset in Enum.GetValues(typeof(EffectPreset)))
            {
                var effect = new SpriteEffect(preset, data);
                Assert.DoesNotThrow(() => effect.ShadeFloat(texture, 0.5f, 0.5f, new Rgba(255, 255, 255, 255)), preset.ToString());
            }
        }

        [Test]
        public void EveryOriginalPictureHasItsOwnFileInTheRebuild()
        {
            var missing = OriginalPngs.Where(p => !Exists(p.rebuilt, ".png")).Select(p => p.original).ToList();
            Assert.That(missing, Is.Empty, "pictures the original shipped but the rebuild does not: " + string.Join(", ", missing));
        }

        [Test]
        public void TheRebuildShipsExactlyOneHundredAndThirtyTwoPngs()
        {
            // Not >=: a stray duplicate or a leftover Unity default asset would hide a real gap just as easily
            // as a missing file would, so the count is pinned to the original's own (RECONSTRUCTION 13).
            var count = Directory.EnumerateFiles(ResourcesFolder, "*.png", SearchOption.AllDirectories).Count();
            Assert.That(count, Is.EqualTo(132));
            Assert.That(OriginalPngs.Length, Is.EqualTo(132), "the manifest above must itself list all 132");
        }

        [Test]
        public void EveryLoadedCueWaveIsShipped_AndTheTwoNeverLoadedOnesAreNot()
        {
            foreach (var wave in LoadedWaves)
            {
                Assert.That(Exists("Audio/" + wave, ".wav"), Is.True, $"'{wave}.wav' is one of the original's cue waves and must ship");
            }

            foreach (var wave in NeverLoadedWaves)
            {
                Assert.That(Exists("Audio/" + wave, ".wav"), Is.False,
                    $"'{wave}.wav' is never loaded by any AudioEngine bank in the original (RECONSTRUCTION 11) and is deliberately not shipped");
            }

            var shipped = Directory.EnumerateFiles(Path.Combine(ResourcesFolder, "Audio"), "*.wav").Count();
            Assert.That(shipped, Is.EqualTo(LoadedWaves.Length));
        }

        [Test]
        public void BothSongsAreShipped()
        {
            foreach (var song in Songs)
            {
                Assert.That(Exists("Music/" + song, ".mp3"), Is.True, $"'{song}.mp3' is one of the console's two songs");
            }

            var shipped = Directory.EnumerateFiles(Path.Combine(ResourcesFolder, "Music"), "*.mp3").Count();
            Assert.That(shipped, Is.EqualTo(Songs.Length));
        }

        // --- The data side: every content.json inventory entry names a real, inventoried file -------------------

        [Test]
        public void ContentRegistry_IconsNameTheirOwnFiles()
        {
            var content = Load().Content;
            foreach (var icon in content.Icons.ActionButtons)
            {
                Assert.That(Exists("Gui/" + icon, ".png"), Is.True, icon);
            }

            foreach (var skill in content.Icons.Skills)
            {
                Assert.That(Exists("Gui/icon_" + skill, ".png"), Is.True, "icon_" + skill);
            }

            // U.VAN-US-A17's own requirement: poisondart and consecrate are each their own file, not a shared one.
            Assert.That(Exists("Gui/icon_poisondart", ".png"), Is.True);
            Assert.That(Exists("Gui/icon_consecrate", ".png"), Is.True);
        }

        [Test]
        public void ContentRegistry_OrphanCharacterArtNamesRealFiles()
        {
            var content = Load().Content;
            foreach (var sheet in content.OrphanCharacterArt.Assets)
            {
                Assert.That(Exists("Characters/" + sheet, ".png"), Is.True, sheet);
            }
        }

        [Test]
        public void ContentRegistry_TexturesAndHudMockupsNameRealFiles()
        {
            var content = Load().Content;
            foreach (var asset in content.Textures.LoadedButUnused.Concat(content.Textures.NeverLoaded))
            {
                Assert.That(Exists("Gui/" + asset, ".png"), Is.True, asset);
            }

            foreach (var asset in content.HudMockups.LoadedButUnused.Concat(content.HudMockups.NeverLoaded))
            {
                Assert.That(Exists("Gui/" + asset, ".png"), Is.True, asset);
            }
        }

        [Test]
        public void ContentRegistry_MapEditorArtAndApplicationIconNameRealFiles()
        {
            var content = Load().Content;
            Assert.That(Exists("World/" + content.MapEditorArt.UsedForDebugBox, ".png"), Is.True);
            foreach (var asset in content.MapEditorArt.LoadedButUnused)
            {
                Assert.That(Exists("World/" + asset, ".png"), Is.True, asset);
            }

            Assert.That(Exists("Gui/" + content.Application.Asset, ".png"), Is.True);
        }

        [Test]
        public void ContentRegistry_ListsAllThreeOfTheOriginalsFonts()
        {
            var fonts = Load().Content.Fonts.Entries;
            Assert.That(fonts.Select(f => f.Name), Is.EquivalentTo(new[] { "Arial_10", "Lucida", "Webdings_14" }));
            var arial = fonts.Single(f => f.Name == "Arial_10");
            Assert.That((arial.Family, arial.Points, arial.Used), Is.EqualTo(("Arial", 10f, true)));
            var lucida = fonts.Single(f => f.Name == "Lucida");
            Assert.That((lucida.Family, lucida.Points, lucida.Used), Is.EqualTo(("Lucida Console", 14f, true)));
            var webdings = fonts.Single(f => f.Name == "Webdings_14");
            Assert.That((webdings.Family, webdings.Points, webdings.Used), Is.EqualTo(("Webdings", 14f, false)));
            Assert.That(fonts.Select(f => (f.RegionStart, f.RegionEnd)), Is.All.EqualTo((32, 126)));
        }
    }
}
