using System.Collections.Generic;

namespace Unity.Rea.ProjectVania.Core.Settings
{
    /// <summary>
    /// hud.json: the always-on HUD of RECONSTRUCTION 9.1 (<c>Portrait</c>, <c>QuickBar</c>, <c>Lenses</c>,
    /// <c>MessageHandler</c> and <c>GuiManager.Draw</c>'s debug text), every figure, text and colour the original wrote,
    /// in its screen pixels (y down), and the slots of the defects the BIBLE fixed.
    /// </summary>
    public sealed class HudData
    {
        /// <summary>The screen the HUD is placed on (<c>Camera.Screen</c>, 800 x 600); must be grid.json's referenceScreen.</summary>
        public Pair Screen { get; private set; }

        /// <summary>Arial_10.spritefont's size, in points: every HUD text is written in it.</summary>
        public float FontPoints { get; private set; }

        public HudPortraitData Portrait { get; private set; }
        public HudQuickBarData QuickBar { get; private set; }
        public HudLensData Lens { get; private set; }
        public HudMessageData Messages { get; private set; }
        public HudReadoutData Readout { get; private set; }

        internal static HudData Read(SettingsReader r)
        {
            var data = new HudData
            {
                Screen = r.Pair("screen", 1f, 100000f),
                FontPoints = r.Positive("fontPoints"),
                Portrait = r.Section("portrait", HudPortraitData.Read),
                QuickBar = r.Section("quickBar", HudQuickBarData.Read),
                Lens = r.Section("lens", HudLensData.Read),
                Messages = r.Section("messages", HudMessageData.Read),
                Readout = r.Section("readout", HudReadoutData.Read),
            };
            return data;
        }
    }

    /// <summary>A picture the HUD draws: its texture (the original's TextureManager key and the art's file name) and its white tint's alpha (<c>g.Alpha</c>).</summary>
    public sealed class HudPicture
    {
        public string Asset { get; private set; }
        public float Alpha { get; private set; }

        internal static HudPicture Read(SettingsReader r) => new HudPicture
        {
            Asset = r.Text("asset"),
            Alpha = r.Number("alpha", 0f, 1f),
        };
    }

    /// <summary><c>Portrait</c>: glass, photo and frame, in draw order, each stretched over one rectangle at (x, H - fromBottom).</summary>
    public sealed class HudPortraitData
    {
        public int X { get; private set; }
        public int FromBottom { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public IReadOnlyList<HudPicture> Layers { get; private set; }

        internal static HudPortraitData Read(SettingsReader r) => new HudPortraitData
        {
            X = r.Integer("x", 0, 100000),
            FromBottom = r.Integer("fromBottom", 0, 100000),
            Width = r.Integer("width", 1, 100000),
            Height = r.Integer("height", 1, 100000),
            Layers = r.Each("layers", HudPicture.Read),
        };
    }

    /// <summary>
    /// <c>QuickBar</c>: glass and frame over one rectangle at (x, H - fromBottom), sliding <see cref="SlideStep"/> px a
    /// frame down to the screen's bottom edge or back (<c>ToggleQuickBar</c>, <c>SlideQuickBar</c>).
    /// </summary>
    public sealed class HudQuickBarData
    {
        public int X { get; private set; }
        public int FromBottom { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int SlideStep { get; private set; }

        /// <summary>The bar starts shown (<c>Visible = true</c>).</summary>
        public bool StartsVisible { get; private set; }

        public IReadOnlyList<HudPicture> Layers { get; private set; }

        internal static HudQuickBarData Read(SettingsReader r) => new HudQuickBarData
        {
            X = r.Integer("x", 0, 100000),
            FromBottom = r.Integer("fromBottom", 1, 100000),
            Width = r.Integer("width", 1, 100000),
            Height = r.Integer("height", 1, 100000),
            SlideStep = r.Integer("slideStep", 1, 100000),
            StartsVisible = r.Flag("startsVisible"),
            Layers = r.Each("layers", HudPicture.Read),
        };
    }

    /// <summary>
    /// <c>Lenses</c>' one lens, <c>fade</c>: the whole screen tinted [r, g, b] at an alpha that <c>CheckFade</c> moves by
    /// <see cref="AlphaStep"/> each time its timer passes <see cref="StepMilliseconds"/> (every frame at 60 Hz).
    /// </summary>
    public sealed class HudLensData
    {
        public byte R { get; private set; }
        public byte G { get; private set; }
        public byte B { get; private set; }
        public float StepMilliseconds { get; private set; }
        public int AlphaStep { get; private set; }

        /// <summary>
        /// BIBLE defect 13, fixed: a finished <c>fade out</c> holds black until <c>fade in</c> or <c>fade off</c>; false is
        /// the original, which went Off and snapped clear the next frame.
        /// </summary>
        public bool HoldBlackAfterFadeOut { get; private set; }

        internal static HudLensData Read(SettingsReader r)
        {
            var colour = r.Integers("colour", 0, 255);
            r.Check(colour.Count == 3, "colour", "must hold exactly three whole numbers [r, g, b]");
            return new HudLensData
            {
                R = colour.Count == 3 ? (byte)colour[0] : (byte)0,
                G = colour.Count == 3 ? (byte)colour[1] : (byte)0,
                B = colour.Count == 3 ? (byte)colour[2] : (byte)0,
                StepMilliseconds = r.Number("stepMilliseconds", 0f, 100000f),
                AlphaStep = r.Integer("alphaStep", 1, 255),
                HoldBlackAfterFadeOut = r.Flag("holdBlackAfterFadeOut"),
            };
        }
    }

    /// <summary>A message and its colour.</summary>
    public sealed class HudLine
    {
        public string Text { get; private set; }
        public SettingsColour Colour { get; private set; }

        internal static HudLine Read(SettingsReader r) => new HudLine
        {
            Text = r.Text("text"),
            Colour = r.Choice<SettingsColour>("colour"),
        };
    }

    /// <summary>
    /// <c>MessageHandler</c>: a message starts at (W / 2, H / 2 - margin); every time the timer passes
    /// <see cref="StepMilliseconds"/> each message that may move falls <see cref="Fall"/> px and fades by
    /// <see cref="FadeStep"/>, and is written only once it is below H / 2 + margin.
    /// </summary>
    public sealed class HudMessageData
    {
        public int Margin { get; private set; }
        public float StepMilliseconds { get; private set; }
        public int Fall { get; private set; }
        public int FadeStep { get; private set; }

        /// <summary>The constructor's <c>//DEBUG</c> messages, queued at start ("test1" .. "test6", Wheat).</summary>
        public IReadOnlyList<HudLine> Startup { get; private set; }

        /// <summary>What a pad connecting writes over the front message (<c>InvokeMessage</c>).</summary>
        public HudLine GamepadNotice { get; private set; }

        /// <summary>
        /// BIBLE: the original's <c>InvokeMessage</c> did nothing with no message queued (its "show the first message"
        /// branch is commented out), so the notice was lost once the start-up messages were gone; true queues it then.
        /// </summary>
        public bool NoticeWhenEmpty { get; private set; }

        internal static HudMessageData Read(SettingsReader r) => new HudMessageData
        {
            Margin = r.Integer("margin", 0, 100000),
            StepMilliseconds = r.Number("stepMilliseconds", 0f, 100000f),
            Fall = r.Integer("fall", 0, 100000),
            FadeStep = r.Integer("fadeStep", 1, 255),
            Startup = r.Each("startup", HudLine.Read, allowEmpty: true),
            GamepadNotice = r.Section("gamepadNotice", HudLine.Read),
            NoticeWhenEmpty = r.Flag("noticeWhenEmpty"),
        };
    }

    /// <summary><c>GuiManager.Draw</c>'s debug text: where each piece is written, its prefixes, and the GUI cursor's size (its clamp).</summary>
    public sealed class HudReadoutData
    {
        public SettingsColour Colour { get; private set; }
        public Pair Player { get; private set; }
        public Pair Camera { get; private set; }
        public Pair Cursor { get; private set; }

        /// <summary>The cursor's position is written twice at the same place, as the original's two identical writes.</summary>
        public int CursorWrites { get; private set; }

        public Pair Cell { get; private set; }

        /// <summary>The frame rate is written at (W - fpsFromRight, fpsY).</summary>
        public int FpsFromRight { get; private set; }

        public int FpsY { get; private set; }
        public string ModePrefix { get; private set; }
        public string CellPrefix { get; private set; }

        /// <summary><c>cursor_hand_topleft</c>'s size: Screen mode keeps the GUI cursor inside (0, 0) .. (W - width, H - height).</summary>
        public Pair CursorSize { get; private set; }

        /// <summary>
        /// The BIBLE's choice: the readout shows the real frame rate. The original wrote <c>1 / ElapsedGameTime</c>, which
        /// with XNA's fixed step always read 59.99988; false shows that.
        /// </summary>
        public bool RealFrameRate { get; private set; }

        internal static HudReadoutData Read(SettingsReader r) => new HudReadoutData
        {
            Colour = r.Choice<SettingsColour>("colour"),
            Player = r.Pair("player", 0f, 100000f),
            Camera = r.Pair("camera", 0f, 100000f),
            Cursor = r.Pair("cursor", 0f, 100000f),
            CursorWrites = r.Integer("cursorWrites", 1, 10),
            Cell = r.Pair("cell", 0f, 100000f),
            FpsFromRight = r.Integer("fpsFromRight", 0, 100000),
            FpsY = r.Integer("fpsY", 0, 100000),
            ModePrefix = r.Text("modePrefix"),
            CellPrefix = r.Text("cellPrefix"),
            CursorSize = r.Pair("cursorSize", 0f, 100000f),
            RealFrameRate = r.Flag("realFrameRate"),
        };
    }
}
