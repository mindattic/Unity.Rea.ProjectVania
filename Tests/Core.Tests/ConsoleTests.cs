using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Gui;
using Unity.Rea.ProjectVania.Core.Input;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    /// <summary>
    /// U.VAN-US-A11: the tilde key slides the original's 200 px console down over the world with its blinking prompt,
    /// 100-line history, command recall and typed-key rules, and runs its script language (RECONSTRUCTION 10), with no
    /// crash on a short command (BIBLE defect 11). The slide and blink are replayed against a literal copy of
    /// <c>Console.SlideConsole</c> and <c>UpdateTimer</c>.
    /// </summary>
    public class ConsoleTests
    {
        static readonly GameSettings Settings = GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));
        static readonly PlayerId P1 = PlayerId.One;

        /// <summary>The shipped world (characters, cameras, menu, console) driven by a scripted keyboard.</summary>
        sealed class Rig : IInputDevice
        {
            readonly InputFrame _held = new InputFrame();

            public Rig(AudioData audio = null)
            {
                World = new GameWorld(Map.FromSettings(Settings.Grid), Settings.Player, Settings.Characters.Find(Settings.Characters.DefaultCharacter),
                    Settings.Names, new Random(3), Settings.Camera, Settings.Menu, Settings.Console, audio ?? Settings.Audio);
                Input = new PlayerInput(P1, Settings.Input, this);
            }

            public GameWorld World { get; }
            public GameConsole Console => World.Console;
            public PlayerInput Input { get; }

            /// <summary>Every event since the last <see cref="Take{T}"/> of that type.</summary>
            public List<IGameEvent> Events { get; } = new List<IGameEvent>();

            public void Step()
            {
                World.Advance(1.0 / 60, Input);
                Events.AddRange(World.Events);
            }

            public void Steps(int count)
            {
                for (var i = 0; i < count; i++) Step();
            }

            public Rig Tap(Key key)
            {
                _held.Set(key);
                Step();
                _held.Set(key, false);
                Step();
                return this;
            }

            public Rig Hold(Key key) { _held.Set(key); return this; }
            public Rig Release(Key key) { _held.Set(key, false); return this; }

            /// <summary>Taps the tilde and lets the slide finish: the console is down and the state is Console.</summary>
            public Rig Open()
            {
                Tap(Key.OemTilde);
                while (Console.Sliding) Step();
                Assert.That(World.State, Is.EqualTo(GameState.Console));
                return this;
            }

            /// <summary>Types <paramref name="text"/> key by key (letters, digits, space) and presses Enter.</summary>
            public Rig Run(string text)
            {
                foreach (var c in text)
                {
                    Tap(KeyOf(c));
                }

                Tap(Key.Enter);
                return this;
            }

            public List<T> Take<T>()
            {
                var taken = Events.OfType<T>().ToList();
                Events.RemoveAll(e => e is T);
                return taken;
            }

            public void Sample(InputFrame frame) => frame.CopyFrom(_held);
        }

        static Key KeyOf(char c)
        {
            if (c == ' ') return Key.Space;
            if (c >= '0' && c <= '9') return (Key)Enum.Parse(typeof(Key), "D" + c);
            return (Key)Enum.Parse(typeof(Key), char.ToUpperInvariant(c).ToString());
        }

        /// <summary>A world with only the console, to run lines straight through the script.</summary>
        static GameWorld Bare(out GameConsole console)
        {
            var world = new GameWorld(null, Settings.Console);
            console = world.Console;
            return world;
        }

        static void Submit(GameWorld world, string line)
        {
            world.Submit(new EditConsole(P1, ConsoleInput.Clear));
            world.Submit(new EditConsole(P1, ConsoleInput.Append, line));
            world.Submit(new EditConsole(P1, ConsoleInput.Submit));
            world.Advance(1.0 / 60);
        }

        // ---- the window and its slide (10.1) ----

        [Test]
        public void TheConsoleStartsHiddenAboveTheScreen()
        {
            var console = new Rig().Console;
            Assert.That((console.Y, console.Height), Is.EqualTo((-200, 200)));
            Assert.That(console.Visible, Is.False);
            Assert.That(console.Texture, Is.EqualTo("console1"));
            Assert.That(console.History, Has.Count.EqualTo(100));
            Assert.That(console.History.All(h => h == string.Empty));
            Assert.That((console.TextColour, console.HistoryColour), Is.EqualTo((SettingsColour.White, SettingsColour.LightGray)));
        }

        [Test]
        public void TildeSlidesItDownFrameForFrameAndOnlyThenEntersTheConsole()
        {
            // Console.SlideConsole(Down), literally: Y += (int)(1 + 1 * (1 - Y / 16)) while Y < 0, then snap to 0.
            var expected = new List<int>();
            var y = -200;
            while (y < 0)
            {
                y += (int)(1 + 1 * (1 - y / 16));
                expected.Add(y);
            }

            expected.Add(0);

            var rig = new Rig();
            rig.Hold(Key.OemTilde).Step();
            Assert.That(rig.Take<CueRequested>().Single().Cue, Is.EqualTo("machine_1_short"));
            Assert.That(rig.Take<ConsoleToggled>().Single().Opening, Is.True);
            rig.Release(Key.OemTilde);
            var seen = new List<int> { rig.Console.Y };
            Assert.That(rig.World.State, Is.EqualTo(GameState.Play), "the state stays until the slide ends");
            while (rig.Console.Sliding)
            {
                Assert.That(rig.World.State, Is.EqualTo(GameState.Play));
                rig.Step();
                seen.Add(rig.Console.Y);
            }

            Assert.That(seen, Is.EqualTo(expected));
            Assert.That(expected[0], Is.EqualTo(-186), "14 px at the top");
            Assert.That(rig.World.State, Is.EqualTo(GameState.Console));
            Assert.That(rig.World.ConsoleOpen, Is.True);
        }

        [Test]
        public void TheWorldKeepsMovingWhileItSlidesDownAndFreezesOnceItIsDown()
        {
            var rig = new Rig();
            var start = rig.World.Characters[0].Position.Y;
            rig.Tap(Key.OemTilde);
            Assert.That(rig.World.Characters[0].Position.Y, Is.GreaterThan(start), "still falling from the spawn during the slide");
            while (rig.Console.Sliding) rig.Step();
            rig.Step();
            var frozen = rig.World.Characters[0].Position;
            rig.Steps(10);
            Assert.That(rig.World.Characters[0].Position, Is.EqualTo(frozen));
        }

        [Test]
        public void TildeAgainSlidesItUp12PxAFrameInPlay()
        {
            var rig = new Rig().Open();
            rig.Take<CueRequested>();
            rig.Hold(Key.OemTilde).Step();
            rig.Release(Key.OemTilde);
            Assert.That(rig.World.State, Is.EqualTo(GameState.Play), "Play from the slide's first frame");
            Assert.That(rig.World.ConsoleOpen, Is.False);
            var seen = new List<int> { rig.Console.Y };
            while (rig.Console.Sliding)
            {
                rig.Step();
                seen.Add(rig.Console.Y);
            }

            var expected = Enumerable.Range(1, 17).Select(i => -12 * i).Concat(new[] { -200 }).ToList();
            Assert.That(seen, Is.EqualTo(expected));
            Assert.That(rig.Take<CueRequested>().Select(c => c.Cue), Is.EqualTo(new[] { "machine_1_short" }));
        }

        [Test]
        public void ATildeDuringASlideIsIgnoredButStillPlaysTheCue()
        {
            var rig = new Rig();
            rig.Tap(Key.OemTilde);
            Assert.That(rig.Console.Sliding, Is.True);
            rig.Take<CueRequested>();
            rig.Tap(Key.OemTilde);
            Assert.That(rig.Take<CueRequested>(), Has.Count.EqualTo(1), "Command_ToggleConsole plays before ToggleConsole ignores it");
            Assert.That(rig.Console.Visible, Is.True, "still sliding down");
            while (rig.Console.Sliding) rig.Step();
            Assert.That(rig.World.State, Is.EqualTo(GameState.Console));
        }

        [Test]
        public void EscWhileItSlidesDownIsOverriddenWhenTheSlideEnds()
        {
            // The slide sets Console as it ends, whatever the state; the original did the same.
            var rig = new Rig();
            rig.Tap(Key.OemTilde);
            rig.Tap(Key.Escape);
            Assert.That(rig.World.State, Is.EqualTo(GameState.MainMenu));
            while (rig.Console.Sliding) rig.Step();
            Assert.That(rig.World.State, Is.EqualTo(GameState.Console));
        }

        [Test]
        public void ThePromptBlinksEvery500MsAsTheOriginalsTimerCounted()
        {
            // UpdateTimer, literally: Timer += 16.6667 (float) every frame; above 500 the blink flips and Timer restarts.
            var rig = new Rig();
            var flips = new List<long>();
            var blink = rig.Console.Blink;
            float timer = 0f;
            var expected = new List<long>();
            for (long frame = 1; frame <= 200; frame++)
            {
                timer += (float)(166667 / 10000.0);
                if (timer > 500) { expected.Add(frame); timer = 0f; }
                rig.Step();
                if (rig.Console.Blink != blink) { flips.Add(frame); blink = rig.Console.Blink; }
            }

            Assert.That(flips, Is.EqualTo(expected));
            Assert.That(flips.First(), Is.EqualTo(30));
        }

        [Test]
        public void ThePromptAndCaretMoveWithTheBlink()
        {
            var rig = new Rig().Open();
            rig.Tap(Key.H);
            while (!rig.Console.Blink) rig.Step();
            Assert.That((rig.Console.PromptX, rig.Console.PromptY), Is.EqualTo((6, 180)));
            Assert.That(rig.Console.Line, Is.EqualTo("h|"));
            while (rig.Console.Blink) rig.Step();
            Assert.That(rig.Console.PromptX, Is.EqualTo(4));
            Assert.That(rig.Console.Line, Is.EqualTo("h"));
            Assert.That(rig.Console.TextY, Is.EqualTo(180));
            Assert.That(rig.Console.HistoryY(0), Is.EqualTo(166));
            Assert.That(rig.Console.HistoryY(1), Is.EqualTo(152));
        }

        // ---- typing (10.2) ----

        [Test]
        public void EnterEchoesTheLineIntoTheHistoryRunsItAndClearsIt()
        {
            var rig = new Rig().Open();
            rig.Run("say hi");
            Assert.That(rig.Console.History[0], Is.EqualTo("say hi"));
            Assert.That(rig.Console.Text, Is.Empty);
            var message = rig.Take<MessageRequested>().Single();
            Assert.That(message.Text, Is.EqualTo("''hi''"));
            Assert.That(message.Colour, Is.EqualTo(new Rgba(255, 255, 0, 255)));

            rig.Run("ver");
            Assert.That(rig.Console.History.Take(3), Is.EqualTo(new[] { "Version 0.0.1", "say hi", "" }), "version replaces the echo");
        }

        [Test]
        public void UpRecallsOlderLinesAndEnterRestartsFromTheNewest()
        {
            var rig = new Rig().Open();
            rig.Run("a1").Run("b2").Run("c3");
            rig.Tap(Key.Up);
            Assert.That(rig.Console.Text, Is.EqualTo("c3"));
            rig.Tap(Key.Up);
            Assert.That(rig.Console.Text, Is.EqualTo("b2"));
            rig.Tap(Key.Enter);
            Assert.That(rig.Console.HistoryIndex, Is.Zero);
            rig.Tap(Key.Up);
            Assert.That(rig.Console.Text, Is.EqualTo("b2"), "the recalled line was entered again, so it is the newest");
            for (var i = 0; i < 150; i++) rig.Tap(Key.Up);
            Assert.That(rig.Console.HistoryIndex, Is.EqualTo(99), "bounded by the 100 lines");
            Assert.That(rig.Console.Text, Is.Empty);
        }

        [Test]
        public void DeleteClearsTheLineAndBackspaceRemovesOneCharacter()
        {
            var rig = new Rig().Open();
            rig.Tap(Key.A).Tap(Key.B).Tap(Key.C);
            rig.Tap(Key.Back);
            Assert.That(rig.Console.Text, Is.EqualTo("ab"));
            rig.Hold(Key.LeftShift).Hold(Key.Back).Steps(5);
            Assert.That(rig.Console.Text, Is.Empty, "Shift+Backspace repeats every frame");
            rig.Release(Key.Back).Release(Key.LeftShift).Step();
            rig.Tap(Key.X).Tap(Key.Delete);
            Assert.That(rig.Console.Text, Is.Empty);
        }

        [Test]
        public void KeysTypeTheOriginalsCharactersAndNames()
        {
            var rig = new Rig().Open();
            rig.Tap(Key.H).Tap(Key.D1).Tap(Key.Space).Tap(Key.F5);
            Assert.That(rig.Console.Text, Is.EqualTo("h1 f5"));
        }

        [Test]
        public void TheHistoryHoldsAHundredLinesAndDropsTheOldest()
        {
            var world = Bare(out var console);
            for (var i = 0; i < 105; i++) Submit(world, "line " + i);
            Assert.That(console.History, Has.Count.EqualTo(100));
            Assert.That(console.History[0], Is.EqualTo("line 104"));
            Assert.That(console.History[99], Is.EqualTo("line 5"));
        }

        // ---- the script language (10.3) ----

        [Test]
        public void SayAndYellQueueMessagesAsTheOriginalBuiltThem()
        {
            var world = Bare(out _);
            Submit(world, "say  two  spaces");
            var say = world.Events.OfType<MessageRequested>().Single();
            Assert.That(say.Text, Is.EqualTo("'' two  spaces''"), "every empty word kept and rejoined with a space, as the original's loop did");

            Submit(world, "yell hi there");
            var yell = world.Events.OfType<MessageRequested>().Single();
            Assert.That(yell.Text, Is.EqualTo("''HI THERE!''"));
            Assert.That(yell.Colour, Is.EqualTo(new Rgba(255, 0, 0, 255)));

            Submit(world, "say");
            Assert.That(world.Events.OfType<MessageRequested>().Single().Text, Is.EqualTo("''''"));

            Submit(world, "MSG HI");
            var hello = world.Events.OfType<MessageRequested>().Single();
            Assert.That(hello.Text, Is.EqualTo("Hello!"), "the line is lower-cased first");
            Assert.That(hello.Colour, Is.EqualTo(new Rgba(245, 222, 179, 255)), "Wheat");
        }

        [TestCase("fade in", FadeState.In)]
        [TestCase("fade out", FadeState.Out)]
        [TestCase("fade off", FadeState.Off)]
        public void FadeSetsTheLens(string line, FadeState fade)
        {
            var world = Bare(out _);
            Submit(world, line);
            Assert.That(world.Fade, Is.EqualTo(fade));
            Assert.That(world.Events.OfType<FadeRequested>().Single().Fade, Is.EqualTo(fade));
        }

        [Test]
        public void PlayStartsASongAndPauseFlipsIt()
        {
            var world = Bare(out _);
            Submit(world, "pause");
            Assert.That(world.Events.OfType<MessageRequested>().Single().Text, Is.EqualTo("Music Unpaused"), "nothing playing: Resume");
            Assert.That(world.Events.OfType<MusicPauseChanged>(), Is.Empty);

            Submit(world, "play battle");
            Assert.That(world.Events.OfType<SongRequested>().Single().Song, Is.EqualTo("battle"));
            Assert.That(world.Music, Is.EqualTo(MusicState.Playing));

            Submit(world, "pause");
            var paused = world.Events.OfType<MessageRequested>().Single();
            Assert.That(paused.Text, Is.EqualTo("Music Paused"));
            Assert.That(paused.Colour, Is.EqualTo(new Rgba(176, 196, 222, 255)), "LightSteelBlue");
            Assert.That(world.Events.OfType<MusicPauseChanged>().Single().Paused, Is.True);

            Submit(world, "unpause");
            Assert.That(world.Events.OfType<MessageRequested>().Single().Text, Is.EqualTo("Music Unpaused"));
            Assert.That(world.Music, Is.EqualTo(MusicState.Playing));

            world.Submit(new EndSong(P1));
            Submit(world, "play music");
            Assert.That(world.Events.OfType<SongRequested>().Single().Song, Is.EqualTo("music"));
        }

        [Test]
        public void MuteSilencesCuesAndSongsAndSaysSo()
        {
            var rig = new Rig().Open();
            rig.Take<CueRequested>();
            rig.Run("mute");
            Assert.That(rig.World.Muted, Is.True);
            Assert.That(rig.Take<MuteChanged>().Single().Muted, Is.True);
            Assert.That(rig.Take<MessageRequested>().Single().Text, Is.EqualTo("Mute: On"));
            rig.Run("play music");
            Assert.That(rig.Take<SongRequested>(), Is.Empty, "PlayMusic returns while muted");
            rig.Tap(Key.Escape);
            Assert.That(rig.Console.Text, Is.EqualTo("escape"), "Esc is also a newly pressed key with no character, so it types its name, as the original's did");
            rig.Tap(Key.Escape);
            Assert.That(rig.Take<CueRequested>(), Is.Empty, "no menu or console cue while muted");

            rig.Tap(Key.OemTilde);
            while (rig.Console.Sliding) rig.Step();
            rig.Tap(Key.OemTilde);
            while (rig.Console.Sliding) rig.Step();
            rig.Tap(Key.Delete).Run("unmute");
            Assert.That(rig.Take<MessageRequested>().Single().Text, Is.EqualTo("Mute: Off"));
            Assert.That(rig.World.Muted, Is.False);
        }

        [Test]
        public void TheWorldStartsMutedIfTheAudioTableSaysSo()
        {
            var muted = GameSettings.Load(new MemorySettingsSource(new DirectorySettingsSource(GameSettingsTests.ShippedFolder))
                .Set(GameSettings.AudioFile, new DirectorySettingsSource(GameSettingsTests.ShippedFolder).Read(GameSettings.AudioFile).Replace("\"muted\": false", "\"muted\": true")));
            var rig = new Rig(muted.Audio);
            Assert.That(rig.World.Muted, Is.True);
            rig.Tap(Key.OemTilde);
            Assert.That(rig.Take<CueRequested>(), Is.Empty);
        }

        [TestCase("exit")]
        [TestCase("quit")]
        public void ExitAndQuitEndTheProgram(string line)
        {
            var world = Bare(out _);
            Submit(world, line);
            Assert.That(world.Events.OfType<QuitRequested>().Count(), Is.EqualTo(1));
        }

        [TestCase("lee")]
        [TestCase("buzzer")]
        public void LeeWritesTheHeartBanner(string line)
        {
            var world = Bare(out var console);
            Submit(world, line);
            Assert.That(console.History.Take(9), Is.EqualTo(Settings.Console.Script.Banner));
            Assert.That(console.History[0], Is.EqualTo(" <3  <3  <3  <3  <3  <3  <3 "));
            Assert.That(console.History[2], Is.EqualTo("|___   |___    |___     * "));
        }

        [Test]
        public void ConsoleClearBlanksAllButTheNewestLine()
        {
            var world = Bare(out var console);
            Submit(world, "one");
            Submit(world, "two");
            Submit(world, "console clear");
            Assert.That(console.History[0], Is.EqualTo("console clear"));
            Assert.That(console.History.Skip(1).All(h => h == string.Empty));
        }

        [Test]
        public void ConsoleFullMakesItScreenHighAndTheToggleCueLong()
        {
            var rig = new Rig().Open();
            rig.Run("console full");
            Assert.That(rig.Console.Height, Is.EqualTo(600));
            Assert.That(rig.Console.PromptY, Is.EqualTo(580));
            rig.Take<CueRequested>();
            rig.Tap(Key.OemTilde);
            Assert.That(rig.Take<CueRequested>().Single().Cue, Is.EqualTo("machine_1"));
            while (rig.Console.Sliding) rig.Step();
            Assert.That(rig.Console.Y, Is.EqualTo(-600), "slides up to minus its new height");
        }

        [TestCase("console text red", SettingsColour.Red, SettingsColour.LightGray)]
        [TestCase("console text black", SettingsColour.Black, SettingsColour.LightGray)]
        [TestCase("console history blue", SettingsColour.White, SettingsColour.Blue)]
        [TestCase("console history green", SettingsColour.White, SettingsColour.Green)]
        [TestCase("console text purple", SettingsColour.White, SettingsColour.LightGray)]
        [TestCase("unit text yellow a b", SettingsColour.Yellow, SettingsColour.LightGray)]
        [TestCase("unit history white a b", SettingsColour.White, SettingsColour.White)]
        public void TheColourParametersSetTheTextAndHistoryColours(string line, SettingsColour text, SettingsColour history)
        {
            var world = Bare(out var console);
            Submit(world, line);
            Assert.That((console.TextColour, console.HistoryColour), Is.EqualTo((text, history)));
        }

        [TestCase("console bg red", "red")]
        [TestCase("console bg blue", "console1")]
        [TestCase("unit bg red a b", "red")]
        public void ConsoleBgRedIsTheOnlyBackground(string line, string texture)
        {
            var world = Bare(out var console);
            Submit(world, line);
            Assert.That(console.Texture, Is.EqualTo(texture));
        }

        [TestCase("console text", "usage: console text <red|white|blue|yellow|green|black>")]
        [TestCase("console history", "usage: console history <red|white|blue|yellow|green|black>")]
        [TestCase("console bg", "usage: console bg <red>")]
        [TestCase("commander x", "usage: commander <attribute> <x|y> <value>")]
        public void AShortCommandWritesItsUsageInsteadOfCrashing(string line, string usage)
        {
            var world = Bare(out var console);
            Assert.DoesNotThrow(() => Submit(world, line));
            Assert.That(console.History.Take(2), Is.EqualTo(new[] { usage, line }));
        }

        [TestCase("")]
        [TestCase("console")]
        [TestCase("hello world")]
        [TestCase("run")]
        [TestCase("execute x")]
        [TestCase("commander position x 5")]
        [TestCase("unit a b c d")]
        public void AnythingElseDoesNothing(string line)
        {
            var world = Bare(out var console);
            Submit(world, line);
            Assert.That(world.Events.Where(e => !(e is GameStateChanged)), Is.Empty);
            Assert.That(console.History[0], Is.EqualTo(line));
            Assert.That(console.History.Skip(1).All(h => h == string.Empty));
        }

        [Test]
        public void NoLineOfTheLanguagesWordsThrows()
        {
            var words = new[] { "", "console", "commander", "unit", "text", "history", "bg", "full", "clear", "red", "x", "say", "yell", "play", "music", "fade", "in" };
            var world = Bare(out _);
            foreach (var a in words)
            foreach (var b in words)
            {
                Assert.DoesNotThrow(() => Submit(world, a + " " + b));
                Assert.DoesNotThrow(() => Submit(world, a));
                foreach (var c in new[] { "", "red", "x" })
                {
                    Assert.DoesNotThrow(() => Submit(world, a + " " + b + " " + c));
                    Assert.DoesNotThrow(() => Submit(world, a + " " + b + " " + c + " " + c));
                    Assert.DoesNotThrow(() => Submit(world, a + " " + b + " " + c + " " + c + " " + c));
                }
            }
        }

        [Test]
        public void TheConsolesWordsAndTextsAreData()
        {
            var data = Settings.Console;
            Assert.That(data.Colours.Select(c => c.Word), Is.EqualTo(new[] { "red", "white", "blue", "yellow", "green", "black" }));
            Assert.That(data.Backgrounds.Select(b => (b.Word, b.Value)), Is.EqualTo(new[] { ("red", "red") }));
            Assert.That(data.Texture, Is.EqualTo("console1"));
            Assert.That(data.Script.Banner, Has.Count.EqualTo(9));
            Assert.That(SettingsColour.Green.ToRgba(), Is.EqualTo(new Rgba(0, 128, 0, 255)), "XNA's Green is half bright");
        }
    }
}
