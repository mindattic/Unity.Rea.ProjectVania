using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.ProjectVania.Tests
{
    /// <summary>
    /// The main menu on screen and heard (U.VAN-US-A10, RECONSTRUCTION 9.2): hidden in Play, drawn over the world in the
    /// MainMenu state with menu1 stretched at 0.8, the arrow, the yellow and red items, the title and the description at
    /// the original's places; its cues play from the imported waves; Quit exits.
    /// </summary>
    public class MainMenuViewTests
    {
        static string SettingsFolder => Path.Combine(Application.dataPath, "StreamingAssets", GameSettings.Folder);
        static readonly PlayerId P1 = PlayerId.One;

        GameHost _host;
        MainMenuView _view;

        [SetUp]
        public void SetUp()
        {
            _host = Bootstrap.Run(new DirectorySettingsSource(SettingsFolder));
            Assert.That(_host, Is.Not.Null);
            _view = _host.GetComponentInChildren<MainMenuView>();
            Assert.That(_view, Is.Not.Null, "the bootstrap builds the menu");
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

        void Step(params MenuInput[] inputs)
        {
            foreach (var input in inputs)
            {
                _host.World.Submit(new NavigateMenu(P1, input));
            }

            _host.World.Advance(1.0 / 60);
            _host.Present(_host.World.Events);
            _view.Refresh();
        }

        void Open()
        {
            _host.World.Submit(new ToggleMainMenu(P1));
            Step();
        }

        static Color Rgb(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

        [Test]
        public void TheMenuIsHiddenInPlayAndDrawnOverTheWorldInTheMenu()
        {
            Assert.That(_view.Layer.style.display.value, Is.EqualTo(DisplayStyle.None));
            var root = _view.Layer.parent;
            var tags = _host.GetComponentInChildren<NameTags>().Layer;
            Assert.That(root.IndexOf(_view.Layer), Is.GreaterThan(root.IndexOf(tags)), "the GUI draws after the names");

            Open();
            Assert.That(_host.World.State, Is.EqualTo(GameState.MainMenu));
            Assert.That(_view.Layer.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            _host.World.Submit(new ToggleMainMenu(P1));
            Step();
            Assert.That(_view.Layer.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheBackgroundIsMenu1StretchedOverTheScreenAt80Percent()
        {
            var background = _view.Background.style;
            var texture = background.backgroundImage.value.texture;
            Assert.That(texture, Is.Not.Null, "Resources/Gui/menu1.png");
            Assert.That((texture.width, texture.height), Is.EqualTo((543, 742)), "imported unscaled");
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Bilinear), "XNA's SpriteBatch sampled the stretched picture linearly");
            Assert.That((background.left.value.value, background.top.value.value), Is.EqualTo((0f, 0f)));
            Assert.That((background.width.value.value, background.height.value.value), Is.EqualTo((800f, 600f)));
            Assert.That(background.unityBackgroundImageTintColor.value, Is.EqualTo(new Color(1f, 1f, 1f, 0.8f)));

            var arrow = _view.Arrow.style.backgroundImage.value.texture;
            Assert.That((arrow.width, arrow.height), Is.EqualTo((16, 16)), "Resources/Gui/arrow1.png");
        }

        [Test]
        public void TheRootLevelIsWrittenAtTheOriginalsPlaces()
        {
            Open();
            var shown = _view.Items.Where(l => l.style.display.value == DisplayStyle.Flex).ToList();
            Assert.That(shown.Select(l => l.text), Is.EqualTo(new[] { "Game", "Video", "Audio", "Controls", "Quit" }));
            for (var i = 0; i < shown.Count; i++)
            {
                Assert.That((shown[i].style.left.value.value, shown[i].style.top.value.value), Is.EqualTo((50f, 100f + 50 * i)));
                Assert.That(shown[i].style.color.value, Is.EqualTo(i == 0 ? Rgb(255, 0, 0) : Rgb(255, 255, 0)), "red when highlighted, else yellow");
                Assert.That(shown[i].style.fontSize.value.value, Is.EqualTo(19f), "Lucida Console 14 pt at XNA's 96 dpi");
            }

            Assert.That((_view.Arrow.style.left.value.value, _view.Arrow.style.top.value.value), Is.EqualTo((32f, 100f)));
            Assert.That(_view.Title.text, Is.EqualTo("Main Menu"));
            Assert.That((_view.Title.style.left.value.value, _view.Title.style.top.value.value), Is.EqualTo((10f, 15f)));
            Assert.That(_view.Title.style.color.value, Is.EqualTo(Color.white));
            Assert.That(_view.Description.text, Is.EqualTo("Game : Change various game settings"));
            Assert.That((_view.Description.style.left.value.value, _view.Description.style.top.value.value), Is.EqualTo((10f, 580f)));
        }

        [Test]
        public void TheShownLevelFollowsTheWorld()
        {
            Open();
            Step(MenuInput.Down, MenuInput.Select);
            var shown = _view.Items.Where(l => l.style.display.value == DisplayStyle.Flex).ToList();
            Assert.That(shown.Select(l => l.text), Is.EqualTo(new[] { "Toggle Fullscreen", "2-1" }));
            Assert.That(_view.Title.text, Is.EqualTo("Main Menu : Video"));
            Assert.That(_view.Description.text, Is.EqualTo("Toggle Fullscreen : Switch between Fullscreen and Windowed mode"));

            Step(MenuInput.Up);
            Assert.That(_view.Description.text, Is.EqualTo(""), "2-1 has no description");
            Assert.That(_view.Items[1].style.color.value, Is.EqualTo(Rgb(255, 0, 0)));
            Assert.That(_view.Items[0].style.color.value, Is.EqualTo(Rgb(255, 255, 0)));
            Assert.That(_view.Arrow.style.top.value.value, Is.EqualTo(150f));
        }

        [Test]
        public void TheMenusCuesPlayFromTheImportedWaves()
        {
            foreach (var cue in new[] { "menu_open", "menu_close", "menu_move", "menu_select" })
            {
                var clip = _host.Cues.Clip(cue);
                Assert.That(clip, Is.Not.Null, $"Resources/Audio/{cue}.wav");
                Assert.That(clip.length, Is.EqualTo(_host.Settings.Audio.FindCue(cue).Seconds).Within(0.02f), cue);
            }

            Open();
            Step(MenuInput.Down);
            Step(MenuInput.Select);
            Step(MenuInput.Back);
            Step(MenuInput.Back);
            Assert.That(_host.Cues.Played, Is.EqualTo(new[] { "menu_open", "menu_move", "menu_select", "menu_close" }));
            Assert.That(_host.World.State, Is.EqualTo(GameState.Play));
            Assert.That(CuePlayer.Gain(0), Is.EqualTo(1f));
            Assert.That(CuePlayer.Gain(-1200), Is.EqualTo(0.2512f).Within(1e-4f));
        }

        [Test]
        public void QuitExitsTheProgram()
        {
            var exits = 0;
            _host.Exit = () => exits++;
            Open();
            Step(MenuInput.Up);
            Assert.That(exits, Is.Zero);
            Step(MenuInput.Select);
            Assert.That(exits, Is.EqualTo(1));
        }

        [Test]
        public void TheGameCameraHearsTheCues()
        {
            Assert.That(_host.GetComponentsInChildren<AudioListener>().Length, Is.EqualTo(1));
        }
    }
}
