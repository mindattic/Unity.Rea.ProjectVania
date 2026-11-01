using System.Collections.Generic;
using System.Linq;
using Unity.Rea.ProjectVania.Core.Display;

namespace Unity.Rea.ProjectVania.Core.Settings
{
    /// <summary>Where one of the <see cref="ActionCrossData"/>'s four slots sits (<c>ActionCross.cs</c>).</summary>
    public enum ActionCrossPosition
    {
        Top,
        Left,
        Right,
        Bottom,
    }

    /// <summary>
    /// content.json: everything the original built but never wired into play (RECONSTRUCTION 5.1, 9.1, 13, 15),
    /// kept as data only so the rebuild loses none of it (U.VAN-US-A16). Nothing in Core or Runtime reads this data
    /// for behaviour.
    /// </summary>
    public sealed class ContentData
    {
        public SegmentAtlasData SegmentAtlas { get; private set; }
        public LedgeShapeData Ledges { get; private set; }
        public SunData Sun { get; private set; }
        public ActionCrossData ActionCross { get; private set; }
        public PortraitInventory Portraits { get; private set; }
        public IconInventory Icons { get; private set; }
        public AreaLayerInventory AreaLayers { get; private set; }
        public AssetInventory Particles { get; private set; }
        public AssetInventory Selectors { get; private set; }
        public AssetInventory Banners { get; private set; }
        public AssetInventory SpriteSheets { get; private set; }
        public UnwiredCharacterArt UnwiredCharacterArt { get; private set; }
        public AssetInventory OrphanCharacterArt { get; private set; }
        public UnusedAssetInventory Textures { get; private set; }
        public UnusedAssetInventory HudMockups { get; private set; }
        public MapEditorArtData MapEditorArt { get; private set; }
        public ApplicationAsset Application { get; private set; }
        public FontInventory Fonts { get; private set; }

        internal static ContentData Read(SettingsReader r)
        {
            var data = new ContentData
            {
                SegmentAtlas = r.Section("segmentAtlas", SegmentAtlasData.Read),
                Ledges = r.Section("ledges", LedgeShapeData.Read),
                Sun = r.Section("sun", SunData.Read),
                ActionCross = r.Section("actionCross", ActionCrossData.Read),
                Portraits = r.Section("portraits", PortraitInventory.Read),
                Icons = r.Section("icons", IconInventory.Read),
                AreaLayers = r.Section("areaLayers", AreaLayerInventory.Read),
                Particles = r.Section("particles", AssetInventory.Read),
                Selectors = r.Section("selectors", AssetInventory.Read),
                Banners = r.Section("banners", AssetInventory.Read),
                SpriteSheets = r.Section("spriteSheets", AssetInventory.Read),
                UnwiredCharacterArt = r.Section("unwiredCharacterArt", UnwiredCharacterArt.Read),
                OrphanCharacterArt = r.Section("orphanCharacterArt", AssetInventory.Read),
                Textures = r.Section("textures", UnusedAssetInventory.Read),
                HudMockups = r.Section("hudMockups", UnusedAssetInventory.Read),
                MapEditorArt = r.Section("mapEditorArt", MapEditorArtData.Read),
                Application = r.Section("application", ApplicationAsset.Read),
                Fonts = r.Section("fonts", FontInventory.Read),
            };
            r.Check(data.Particles.Assets.Count == 5, "particles", "must list the original's five particle pictures");
            r.Check(data.Selectors.Assets.Count == 5, "selectors", "must list the original's five selector sizes");
            r.Check(data.Banners.Assets.Count == 15, "banners", "must list the original's fifteen banners");
            r.Check(data.SpriteSheets.Assets.Count == 16, "spriteSheets", "must list the original's sixteen character sheets");
            r.Check(data.OrphanCharacterArt.Assets.Count == 2, "orphanCharacterArt", "must list Untitled and frame_10");
            r.Check(data.Textures.LoadedButUnused.Count + data.Textures.NeverLoaded.Count == 7, "textures", "must list the original's seven remaining Textures pictures");
            r.Check(data.HudMockups.LoadedButUnused.Count + data.HudMockups.NeverLoaded.Count == 5, "hudMockups", "must list the original's five remaining Hud pictures");
            r.Check(data.Fonts.Entries.Count == 3, "fonts", "must list all three of the original's fonts");

            // Every ActionCross slot must name either the blank texture or one of the inventoried icons (the skills
            // list holds bare names, e.g. "selectsquad", matched here against the slot's own "icon_selectsquad").
            var knownIcons = new HashSet<string>(data.Icons.ActionButtons);
            foreach (var skill in data.Icons.Skills)
            {
                knownIcons.Add("icon_" + skill);
            }

            knownIcons.Add(data.ActionCross.BlankIcon);
            foreach (var slot in data.ActionCross.Slots)
            {
                r.Check(knownIcons.Contains(slot.Icon), "actionCross", $"slot '{slot.Position}' names icon '{slot.Icon}', which content.json does not list");
            }

            return data;
        }
    }

    /// <summary><c>MapEditor\maps1.png</c> (RECONSTRUCTION 5.1): the source image segments.json's definitions cut from.</summary>
    public sealed class SegmentAtlasData
    {
        public string Asset { get; private set; }
        public Pair Size { get; private set; }

        internal static SegmentAtlasData Read(SettingsReader r) => new SegmentAtlasData
        {
            Asset = r.Text("asset"),
            Size = r.Pair("size", 1f),
        };
    }

    /// <summary><c>Map.cs</c>'s <c>Ledge[16]</c> (RECONSTRUCTION 5): the shape of the dead allocation, not content.</summary>
    public sealed class LedgeShapeData
    {
        public int Slots { get; private set; }
        public int NodesPerLedge { get; private set; }

        internal static LedgeShapeData Read(SettingsReader r)
        {
            var data = new LedgeShapeData
            {
                Slots = r.Integer("slots", 1, 1000),
                NodesPerLedge = r.Integer("nodesPerLedge", 1, 1000),
            };
            r.Check(data.Slots == 16, "slots", "must be 16, as Map.cs allocated");
            r.Check(data.NodesPerLedge == 16, "nodesPerLedge", "must be 16, as Ledge.cs allocated");
            return data;
        }
    }

    /// <summary><c>World\Sun.cs</c> (RECONSTRUCTION 9.1, defect 12): the day-night cycle that never ticks.</summary>
    public sealed class SunData
    {
        public float TickMilliseconds { get; private set; }
        public int AlphaStep { get; private set; }
        public IReadOnlyList<SunPeriod> Periods { get; private set; }

        internal static SunData Read(SettingsReader r)
        {
            var data = new SunData
            {
                TickMilliseconds = r.Positive("tickMilliseconds"),
                AlphaStep = r.Integer("alphaStep", 1, 255),
                Periods = r.Each("periods", SunPeriod.Read),
            };
            r.Check(data.Periods.Count == 4, "periods", "must list all four of Day, Dusk, Night and Dawn");
            var expected = new[] { "Day", "Dusk", "Night", "Dawn" };
            for (var i = 0; i < data.Periods.Count && i < expected.Length; i++)
            {
                r.Check(data.Periods[i].Name == expected[i], "periods", $"entry {i} must be '{expected[i]}' (PeriodOfDay's own order)");
            }

            return data;
        }
    }

    public sealed class SunPeriod
    {
        public string Name { get; private set; }
        public Rgba Colour { get; private set; }

        internal static SunPeriod Read(SettingsReader r) => new SunPeriod
        {
            Name = r.Text("name"),
            Colour = Rgba.FromQuad(r.Quad("colour", 0f, 255f)),
        };
    }

    /// <summary><c>Gui\ActionCross.cs</c> (RECONSTRUCTION 9.1, defect 12): commented out in GuiManager, never built.</summary>
    public sealed class ActionCrossData
    {
        public string Glass { get; private set; }
        public string Frame { get; private set; }
        public Pair Size { get; private set; }
        public Pair SlotSize { get; private set; }

        /// <summary><c>Content\Textures\blank.png</c>, a 1x1 the original drew the cross's three empty slots with.</summary>
        public string BlankIcon { get; private set; }

        public IReadOnlyList<ActionCrossSlot> Slots { get; private set; }

        internal static ActionCrossData Read(SettingsReader r)
        {
            var data = new ActionCrossData
            {
                Glass = r.Text("glass"),
                Frame = r.Text("frame"),
                Size = r.Pair("size", 1f),
                SlotSize = r.Pair("slotSize", 1f),
                BlankIcon = r.Text("blankIcon"),
                Slots = r.Each("slots", ActionCrossSlot.Read),
            };
            r.Check(data.Slots.Count == 4, "slots", "must list all four of the cross's slots");
            var seen = new HashSet<ActionCrossPosition>();
            foreach (var slot in data.Slots)
            {
                r.Check(seen.Add(slot.Position), "slots", $"lists '{slot.Position}' twice");
            }

            return data;
        }
    }

    public sealed class ActionCrossSlot
    {
        public ActionCrossPosition Position { get; private set; }
        public string Icon { get; private set; }

        internal static ActionCrossSlot Read(SettingsReader r) => new ActionCrossSlot
        {
            Position = r.Choice<ActionCrossPosition>("position"),
            Icon = r.Text("icon"),
        };
    }

    /// <summary><c>Gui\Portraits</c> (RECONSTRUCTION 9.1, 13): the one portrait drawn and the six that never are.</summary>
    public sealed class PortraitInventory
    {
        public string Used { get; private set; }
        public IReadOnlyList<string> Unused { get; private set; }

        internal static PortraitInventory Read(SettingsReader r)
        {
            var data = new PortraitInventory
            {
                Used = r.Text("used"),
                Unused = r.Texts("unused"),
            };
            r.Check(data.Unused.Count == 6, "unused", "must list the other six portraits");
            r.Check(!data.Unused.Contains(data.Used), "unused", $"must not list '{data.Used}' again; it is already 'used'");
            return data;
        }
    }

    /// <summary><c>Gui\Icons</c> (RECONSTRUCTION 13, defect 14): the action buttons and skill icons, all unused.</summary>
    public sealed class IconInventory
    {
        public IReadOnlyList<string> ActionButtons { get; private set; }
        public IReadOnlyList<string> Skills { get; private set; }

        internal static IconInventory Read(SettingsReader r)
        {
            var data = new IconInventory
            {
                ActionButtons = r.Texts("actionButtons"),
                Skills = r.Texts("skills"),
            };
            r.Check(data.ActionButtons.Count == 4, "actionButtons", "must list all four action buttons");
            r.Check(data.Skills.Count == 29, "skills", "must list all twenty-nine skill icons");
            var seen = new HashSet<string>();
            foreach (var skill in data.Skills)
            {
                r.Check(seen.Add(skill), "skills", $"lists '{skill}' twice");
            }

            return data;
        }
    }

    /// <summary>Areas (RECONSTRUCTION 5, 13): the one layer drawn and the four parallax layers never loaded.</summary>
    public sealed class AreaLayerInventory
    {
        public string Drawn { get; private set; }
        public IReadOnlyList<string> Unused { get; private set; }

        internal static AreaLayerInventory Read(SettingsReader r)
        {
            var data = new AreaLayerInventory
            {
                Drawn = r.Text("drawn"),
                Unused = r.Texts("unused"),
            };
            r.Check(data.Unused.Count == 4, "unused", "must list the four parallax layers");
            r.Check(!data.Unused.Contains(data.Drawn), "unused", $"must not list '{data.Drawn}' again; it is already 'drawn'");
            return data;
        }
    }

    /// <summary>A flat list of asset names kept as content only (particles, selectors, banners, sprite sheets).</summary>
    public sealed class AssetInventory
    {
        public IReadOnlyList<string> Assets { get; private set; }

        internal static AssetInventory Read(SettingsReader r)
        {
            var data = new AssetInventory
            {
                Assets = r.Texts("assets"),
            };
            var seen = new HashSet<string>();
            foreach (var asset in data.Assets)
            {
                r.Check(seen.Add(asset), "assets", $"lists '{asset}' twice");
            }

            return data;
        }
    }

    /// <summary>
    /// <c>Characters\Playable\Richter</c> (RECONSTRUCTION 13): idle and run art the original never loaded and the
    /// rebuild never made playable (unlike Shanoa and Leo, U.VAN-T25).
    /// </summary>
    public sealed class UnwiredCharacterArt
    {
        public string Sheet { get; private set; }
        public Pair Size { get; private set; }

        internal static UnwiredCharacterArt Read(SettingsReader r) => new UnwiredCharacterArt
        {
            Sheet = r.Text("sheet"),
            Size = r.Pair("size", 1f),
        };
    }

    /// <summary>
    /// A flat inventory split by whether the original's content manager ever loaded it (RECONSTRUCTION 13,
    /// U.VAN-US-A17): <see cref="LoadedButUnused"/> names pictures a manager loaded but nothing ever drew;
    /// <see cref="NeverLoaded"/> names pictures the content project built that no manager ever loaded at all.
    /// Both are kept as the original's own files regardless.
    /// </summary>
    public sealed class UnusedAssetInventory
    {
        public IReadOnlyList<string> LoadedButUnused { get; private set; }
        public IReadOnlyList<string> NeverLoaded { get; private set; }

        internal static UnusedAssetInventory Read(SettingsReader r)
        {
            var data = new UnusedAssetInventory
            {
                LoadedButUnused = r.Texts("loadedButUnused", allowEmpty: true),
                NeverLoaded = r.Texts("neverLoaded", allowEmpty: true),
            };
            var seen = new HashSet<string>();
            foreach (var asset in data.LoadedButUnused.Concat(data.NeverLoaded))
            {
                r.Check(seen.Add(asset), "loadedButUnused/neverLoaded", $"lists '{asset}' twice");
            }

            return data;
        }
    }

    /// <summary><c>Content\MapEditor</c> (RECONSTRUCTION 13, U.VAN-US-A17): block.png and cell.png, beside maps1
    /// (<see cref="SegmentAtlasData"/>) and null (<c>Textures\blank.png</c>, <see cref="ActionCrossData.BlankIcon"/>).</summary>
    public sealed class MapEditorArtData
    {
        public string UsedForDebugBox { get; private set; }
        public IReadOnlyList<string> LoadedButUnused { get; private set; }

        internal static MapEditorArtData Read(SettingsReader r) => new MapEditorArtData
        {
            UsedForDebugBox = r.Text("usedForDebugBox"),
            LoadedButUnused = r.Texts("loadedButUnused"),
        };
    }

    /// <summary><c>Content\Application\GameThumbnail.png</c> (RECONSTRUCTION 13, U.VAN-US-A17): the ClickOnce
    /// installer's icon, loaded by the deployment manifest rather than the game's content pipeline.</summary>
    public sealed class ApplicationAsset
    {
        public string Asset { get; private set; }

        internal static ApplicationAsset Read(SettingsReader r) => new ApplicationAsset
        {
            Asset = r.Text("asset"),
        };
    }

    /// <summary><c>Content\Fonts</c> (RECONSTRUCTION 13, U.VAN-US-A17): the three .spritefont descriptors the
    /// original built, each naming a system font rasterised at build time; no binary font file ships.</summary>
    public sealed class FontInventory
    {
        public IReadOnlyList<FontEntry> Entries { get; private set; }

        internal static FontInventory Read(SettingsReader r) => new FontInventory
        {
            Entries = r.Each("entries", FontEntry.Read),
        };
    }

    public sealed class FontEntry
    {
        public string Name { get; private set; }
        public string Family { get; private set; }
        public float Points { get; private set; }
        public int RegionStart { get; private set; }
        public int RegionEnd { get; private set; }
        public bool Used { get; private set; }

        internal static FontEntry Read(SettingsReader r)
        {
            var region = r.Pair("region", 1f);
            return new FontEntry
            {
                Name = r.Text("name"),
                Family = r.Text("family"),
                Points = r.Positive("points"),
                RegionStart = region.XInt,
                RegionEnd = region.YInt,
                Used = r.Flag("used"),
            };
        }
    }
}
