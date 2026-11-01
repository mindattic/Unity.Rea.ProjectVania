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
    /// The console on screen and heard (U.VAN-US-A11, RECONSTRUCTION 10.1): console1 stretched to 800 x 200 at 0.75 above
    /// the screen, sliding down with the world's slide; the prompt, typed line and history written only in the Console
    /// state at the original's places and colours; console full and console bg red; the toggle's cues from the imported
    /// waves; exit ends the program.
    /// </summary>
    public class ConsoleViewTests
    {
        static string SettingsFolder => Path.Combine(Application.dataPath, "StreamingAssets", GameSettings.Folder);
        static readonly PlayerId P1 = PlayerId.One;

        GameHost _host;
        ConsoleView _view;

        [SetUp]
        public void SetUp()
        {
            _host = Bootstrap.Run(new DirectorySettingsSource(SettingsFolder));
            Assert.That(_host, Is.Not.Null);
            _view = _host.GetComponentInChildren<ConsoleView>();
            Assert.That(_view, Is.Not.Null, "the bootstrap builds the console");
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
        }

        void Toggle()
        {
            Step(new ToggleConsole(P1));
            while (_host.World.Console.Sliding)
            {
                Step();
            }
        }

        void Run(string line) => Step(
            new EditConsole(P1, ConsoleInput.Clear),
            new EditConsole(P1, ConsoleInput.Append, line),
            new EditConsole(P1, ConsoleInput.Submit));

        static Color Rgb(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

        static (float, float) At(VisualElement e) => (e.style.left.value.value, e.style.top.value.value);

        [Test]
        public void ThePanelIsConsole1StretchedAboveTheScreenAt75Percent()
        {
            var panel = _view.Panel.style;
            var texture = panel.backgroundImage.value.texture;
            Assert.That(texture, Is.Not.Null, "Resources/Gui/console1.png");
            Assert.That((texture.width, texture.height), Is.EqualTo((608, 203)), "imported unscaled");
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Bilinear), "stretched, so sampled linearly as XNA did");
            Assert.That(At(_view.Panel), Is.EqualTo((0f, -200f)));
            Assert.That((panel.width.value.value, panel.height.value.value), Is.EqualTo((800f, 200f)));
            Assert.That(panel.unityBackgroundImageTintColor.value, Is.EqualTo(new Color(1f, 1f, 1f, 0.75f)));
            Assert.That(_view.Prompt.style.display.value, Is.EqualTo(DisplayStyle.None), "no text outside the Console state");

            var root = _view.Layer.parent;
            var menu = _host.GetComponentInChildren<MainMenuView>().Layer;
            Assert.That(root.IndexOf(_view.Layer), Is.GreaterThan(root.IndexOf(menu)), "the GUI draws the console after the menu");
        }

        [Test]
        public void ThePanelFollowsTheSlideAndTheTextAppearsWhenItIsDown()
        {
            Step(new ToggleConsole(P1));
            Assert.That(At(_view.Panel).Item2, Is.EqualTo(-186f));
            Assert.That(_view.Line.style.display.value, Is.EqualTo(DisplayStyle.None), "still sliding, still Play");
            while (_host.World.Console.Sliding)
            {
                Step();
            }

            Assert.That(At(_view.Panel).Item2, Is.EqualTo(0f));
            Assert.That(_view.Prompt.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Prompt.text, Is.EqualTo(">"));
            Assert.That(_view.Prompt.style.color.value, Is.EqualTo(Rgb(255, 255, 0)));
            Assert.That(_view.Prompt.style.top.value.value, Is.EqualTo(180f));
            Assert.That(_view.Prompt.style.fontSize.value.value, Is.EqualTo(13f), "Arial 10 pt at XNA's 96 dpi");
            Assert.That(At(_view.Line), Is.EqualTo((16f, 180f)));
            Assert.That(_view.History, Has.Count.EqualTo(100));
            Assert.That(At(_view.History[0]), Is.EqualTo((2f, 166f)));
            Assert.That(At(_view.History[2]), Is.EqualTo((2f, 138f)));
        }

        [Test]
        public void TypedLinesGoToTheHistoryInItsColours()
        {
            Toggle();
            Run("lee");
            Run("console text red");
            Run("console history blue");
            Step(new EditConsole(P1, ConsoleInput.Append, "abc"));
            while (!_host.World.Console.Blink)
            {
                Step();
            }

            Assert.That(_view.Line.text, Is.EqualTo("abc|"), "the caret with the blink");
            Assert.That(_view.Line.style.color.value, Is.EqualTo(Rgb(255, 0, 0)));
            Assert.That(_view.History[0].text, Is.EqualTo("console history blue"));
            Assert.That(_view.History[0].style.color.value, Is.EqualTo(Rgb(0, 0, 255)));
            Assert.That(_view.History[4].text, Is.EqualTo("|___   |___    |___     * "));
            Assert.That(_view.Prompt.style.left.value.value, Is.EqualTo(6f));
        }

        [Test]
        public void ConsoleFullAndBgRedChangeThePanel()
        {
            Toggle();
            Run("console full");
            Run("console bg red");
            var texture = _view.Panel.style.backgroundImage.value.texture;
            Assert.That(texture, Is.Not.Null, "Resources/Gui/red.png");
            Assert.That((texture.width, texture.height), Is.EqualTo((1, 1)));
            Assert.That(_view.Panel.style.height.value.value, Is.EqualTo(600f));
            Assert.That(_view.Prompt.style.top.value.value, Is.EqualTo(580f));
            Assert.That(_view.History[0].text, Is.EqualTo("console bg red"));
        }

        [Test]
        public void TheToggleCuesPlayFromTheImportedWaves()
        {
            foreach (var cue in new[] { "machine_1", "machine_1_short" })
            {
                var clip = _host.Cues.Clip(cue);
                Assert.That(clip, Is.Not.Null, $"Resources/Audio/{cue}.wav");
                Assert.That(clip.length, Is.EqualTo(_host.Settings.Audio.FindCue(cue).Seconds).Within(0.05f), cue);
            }

            Toggle();
            Run("console full");
            Toggle();
            Assert.That(_host.Cues.Played, Is.EqualTo(new[] { "machine_1_short", "machine_1" }));
        }

        [Test]
        public void MuteSilencesTheConsolesCues()
        {
            Toggle();
            Run("mute");
            Toggle();
            Toggle();
            Assert.That(_host.Cues.Played, Is.EqualTo(new[] { "machine_1_short" }));
        }

        [Test]
        public void ExitEndsTheProgram()
        {
            var exits = 0;
            _host.Exit = () => exits++;
            Toggle();
            Run("say exit");
            Assert.That(exits, Is.Zero);
            Run("exit");
            Assert.That(exits, Is.EqualTo(1));
        }

        [Test]
        public void AShortCommandDoesNotCrashTheGame()
        {
            Toggle();
            Assert.DoesNotThrow(() => Run("console text"));
            Assert.That(_view.History[0].text, Does.StartWith("usage: console text"));
            Assert.That(_view.History[1].text, Is.EqualTo("console text"));
        }
    }
}
