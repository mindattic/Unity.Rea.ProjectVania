using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Gui;
using Unity.Rea.ProjectVania.Core.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.ProjectVania.Tests
{
    /// <summary>
    /// The always-on HUD on screen (U.VAN-US-A12, RECONSTRUCTION 9.1): the portrait and quick bar from the original's art
    /// at their places, the quick bar following U's slide, the fade lens darkening the world under the menu and the
    /// console, the debug readout and the falling message stack written last, in Arial 10 at the original's places.
    /// </summary>
    public class HudViewTests
    {
        static string SettingsFolder => Path.Combine(Application.dataPath, "StreamingAssets", GameSettings.Folder);
        static readonly PlayerId P1 = PlayerId.One;

        GameHost _host;
        HudView _view;
        HudTextView _text;

        [SetUp]
        public void SetUp()
        {
            _host = Bootstrap.Run(new DirectorySettingsSource(SettingsFolder));
            Assert.That(_host, Is.Not.Null);
            _view = _host.GetComponentInChildren<HudView>();
            _text = _host.GetComponentInChildren<HudTextView>();
            Assert.That(_view, Is.Not.Null, "the bootstrap builds the HUD");
            Assert.That(_text, Is.Not.Null, "the bootstrap builds the HUD's text");
        }

        [TearDown]
        public void TearDown()
        {
            var root = GameObject.Find(Bootstrap.RootName);
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        void Step(params ICommand[] commands)
        {
            foreach (var command in commands)
            {
                _host.World.Submit(command);
            }

            _host.World.Advance(1.0 / 60);
            _host.Present(_host.World.Events);
            _view.Refresh();
            _text.Refresh();
        }

        void Steps(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Step();
            }
        }

        void Run(string line) => Step(
            new EditConsole(P1, ConsoleInput.Clear),
            new EditConsole(P1, ConsoleInput.Append, line),
            new EditConsole(P1, ConsoleInput.Submit));

        static (float, float) At(VisualElement e) => (e.style.left.value.value, e.style.top.value.value);

        static (float, float) Size(VisualElement e) => (e.style.width.value.value, e.style.height.value.value);

        static Texture2D Picture(VisualElement e) => e.style.backgroundImage.value.texture;

        [Test]
        public void ThePortraitIsGlassPhotoAndFrameAtTheBottomLeft()
        {
            Assert.That(At(_view.Portrait), Is.EqualTo((0f, 490f)));
            Assert.That(Size(_view.Portrait), Is.EqualTo((99f, 110f)));
            Assert.That(_view.PortraitLayers.Select(l => l.name), Is.EqualTo(new[] { "portrait_glass", "portrait_1", "portrait_frame" }));
            var sizes = _view.PortraitLayers.Select(l => (Picture(l).width, Picture(l).height)).ToArray();
            Assert.That(sizes, Is.EqualTo(new[] { (99, 110), (84, 94), (99, 110) }), "the original art, imported unscaled");
            Assert.That(_view.PortraitLayers.All(l => Size(l) == (99f, 110f)), "each stretched over the frame's rectangle");
            Assert.That(Picture(_view.PortraitLayers[1]).filterMode, Is.EqualTo(FilterMode.Bilinear), "the stretched photo");
            Assert.That(Picture(_view.PortraitLayers[2]).filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(_view.PortraitLayers[0].style.unityBackgroundImageTintColor.value.a, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(_view.PortraitLayers[1].style.unityBackgroundImageTintColor.value.a, Is.EqualTo(1f));
        }

        [Test]
        public void UTheQuickBarSlidesOffTheBottomAndBack()
        {
            Assert.That(At(_view.QuickBar), Is.EqualTo((103f, 495f)));
            Assert.That(Size(_view.QuickBar), Is.EqualTo((261f, 105f)));
            Assert.That(_view.QuickBarLayers.Select(l => (Picture(l).width, Picture(l).height)), Is.EqualTo(new[] { (261, 105), (261, 105) }));
            Step(new ToggleQuickBar(P1));
            Assert.That(At(_view.QuickBar).Item2, Is.EqualTo(498f));
            Steps(40);
            Assert.That(At(_view.QuickBar).Item2, Is.EqualTo(600f), "off the screen");
            Step(new ToggleQuickBar(P1));
            Steps(40);
            Assert.That(At(_view.QuickBar).Item2, Is.EqualTo(495f));
        }

        [Test]
        public void TheFadeLensDarkensUnderTheMenuAndTheConsole()
        {
            Assert.That(_view.Lens.style.backgroundColor.value.a, Is.Zero);
            Assert.That(Size(_view.Lens), Is.EqualTo((800f, 600f)));
            Run("fade out");
            Steps(254);
            Assert.That(_view.Lens.style.backgroundColor.value, Is.EqualTo(new Color(0f, 0f, 0f, 1f)));
            Steps(60);
            Assert.That(_view.Lens.style.backgroundColor.value.a, Is.EqualTo(1f), "held black (BIBLE defect 13)");
            Run("fade off");
            Assert.That(_view.Lens.style.backgroundColor.value.a, Is.Zero);

            var root = _view.Layer.parent;
            var tags = _host.GetComponentInChildren<NameTags>().Layer;
            Assert.That(root.IndexOf(_view.Layer), Is.GreaterThan(root.IndexOf(tags)), "over the names");
            Assert.That(root.IndexOf(_view.Layer), Is.LessThan(root.IndexOf(_host.GetComponentInChildren<MainMenuView>().Layer)), "under the menu");
            Assert.That(root.IndexOf(_view.Layer), Is.LessThan(root.IndexOf(_host.GetComponentInChildren<ConsoleView>().Layer)), "under the console");
            Assert.That(root.IndexOf(_text.Layer), Is.EqualTo(root.childCount - 1), "the text is written last");
        }

        [Test]
        public void TheReadoutIsWrittenAtTheOriginalsPlaces()
        {
            Assert.That(At(_text.PlayerReadout), Is.EqualTo((100f, 100f)));
            Assert.That(At(_text.CameraReadout), Is.EqualTo((300f, 100f)));
            Assert.That(_text.CursorReadout, Has.Count.EqualTo(2));
            Assert.That(_text.CursorReadout.All(l => At(l) == (600f, 100f)));
            Assert.That(At(_text.CellReadout), Is.EqualTo((700f, 100f)));
            Assert.That(At(_text.FrameRateReadout), Is.EqualTo((700f, 10f)));
            Assert.That(_text.PlayerReadout.style.fontSize.value.value, Is.EqualTo(13f), "Arial 10 pt at XNA's 96 dpi");
            Assert.That(_text.PlayerReadout.style.color.value, Is.EqualTo(Color.white));

            Step();
            var driven = _host.World.Driven(P1);
            Assert.That(_text.PlayerReadout.text, Does.StartWith(driven.Name + "\n"));
            Assert.That(_text.CameraReadout.text, Does.StartWith("Mode: LoosePlayer\n"));
            Assert.That(_text.CellReadout.text, Is.EqualTo("Coords:0, 0"));

            _text.Mouse = new Vector2(900f, -20f);
            _text.FrameRate = HudReadout.OriginalFrameRate;
            _text.Refresh();
            Assert.That(_text.CursorReadout[0].text, Is.EqualTo("787, 0"), "kept inside the screen by the 13 x 18 cursor");
            Assert.That(_text.FrameRateReadout.text, Is.EqualTo("59.99988"));
        }

        [Test]
        public void TheMouseIsReadOnTheLetterboxedReference()
        {
            // A 1920 x 1080 window holds the reference at x1 (800 x 600) centred: (560, 240) .. (1360, 840), y up.
            Assert.That(HudTextView.ToReference(new Vector2(560f, 840f), 1920, 1080), Is.EqualTo(new Vector2(0f, 0f)));
            Assert.That(HudTextView.ToReference(new Vector2(960.5f, 540f), 1920, 1080), Is.EqualTo(new Vector2(400f, 300f)));
            // 1600 x 1200 holds it at x2 with no bars.
            Assert.That(HudTextView.ToReference(new Vector2(801f, 1199f), 1600, 1200), Is.EqualTo(new Vector2(400f, 0f)));
        }

        [Test]
        public void TheStartUpMessagesFallIntoViewAndFade()
        {
            Assert.That(_text.Messages.All(l => l.style.display.value == DisplayStyle.None), "not yet below the centre");
            Steps(99);
            var first = _text.Messages[0];
            Assert.That(first.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(first.text, Is.EqualTo("test1"));
            Assert.That(At(first), Is.EqualTo((400f, 317f)));
            var colour = (Color32)first.style.color.value;
            Assert.That((colour.r, colour.g, colour.b, colour.a), Is.EqualTo(((byte)245, (byte)222, (byte)179, (byte)222)), "Wheat, 33 steps faded");
            Assert.That(_text.Messages[1].style.display.value, Is.EqualTo(DisplayStyle.None), "16 px behind, still above the line");

            Run("say hi");
            Steps(2000);
            Assert.That(_text.Messages.All(l => l.style.display.value == DisplayStyle.None), "every message gone");
        }
    }
}
