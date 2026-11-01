using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Characters;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Gui;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    /// <summary>
    /// U.VAN-US-A13: the original's sounds from audio.json's cue table (RECONSTRUCTION 11). The run's <c>step2</c> is named
    /// once per run-strip frame and the wall slide's <c>drag</c> once per clip (BIBLE defect 4, fixed from every frame);
    /// mute silences cues and songs; the two songs start only from the console; the unplayed cues stay in the table.
    /// </summary>
    public class AudioTests
    {
        const double Frame = 1.0 / 60;
        static readonly PlayerId P1 = PlayerId.One;
        static GameSettings Settings() => GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));

        static GameWorld Shipped(GameSettings s, AnimationData animations = null) =>
            new GameWorld(Map.FromSettings(s.Grid), s.Player, s.Characters.Find(s.Characters.DefaultCharacter), s.Names, new Random(5), s.Camera, s.Menu,
                s.Console, s.Audio, s.Hud, animations ?? s.Animations);

        /// <summary>Steps once with <paramref name="commands"/>, returning the cues named.</summary>
        static List<string> Step(GameWorld w, params ICommand[] commands)
        {
            foreach (var c in commands) w.Submit(c);
            Assert.That(w.Advance(Frame), Is.EqualTo(1));
            return w.Events.OfType<CueRequested>().Select(e => e.Cue).ToList();
        }

        static ICommand Left => new MoveCharacter(P1, -1);
        static ICommand Right => new MoveCharacter(P1, 1);
        static ICommand Idle => new StandCharacter(P1);

        /// <summary>The shipped world with character 0 standing on the floor and character 1 moved out of the way.</summary>
        static GameWorld Landed(GameSettings s)
        {
            var w = Shipped(s);
            w.Characters[1].Reset(new PixelVector(1500, 100));
            for (var i = 0; i < 300; i++)
            {
                Step(w, Idle);
                if (w.Characters[0].Action == ActionKind.Standing && w.Characters[1].Action == ActionKind.Standing) return w;
            }

            throw new AssertionException("never landed");
        }

        [Test]
        public void TheCueTableKeepsEveryXactCueWithTheRunAndWallSlideCuesNamed()
        {
            var audio = Settings().Audio;
            Assert.That(audio.Cues.Select(c => c.Cue), Is.EquivalentTo(new[]
            {
                "music1", "footstep_1", "footstep_2", "drag", "step1", "jump1", "step2",
                "machine_1", "machine_1_short", "menu_open", "menu_close", "menu_move", "menu_select",
            }));
            Assert.That(audio.RunCue, Is.EqualTo("step2"));
            Assert.That(audio.WallSlideCue, Is.EqualTo("drag"));
            Assert.That(audio.FindCue("drag").Seconds, Is.EqualTo(0.26f));
            Assert.That(audio.FindSong(ConsoleScript.MusicSong).File, Is.EqualTo("music.mp3"));
            Assert.That(audio.FindSong(ConsoleScript.BattleSong).File, Is.EqualTo("battle.mp3"));
        }

        [Test]
        public void RunningNamesOneStepPerRunStripFrame()
        {
            var s = Settings();
            var w = Landed(s);
            var run = s.Animations.Find("morris").Find(CharacterAnimator.Run);
            var cues = new List<string>();
            var steps = 0;
            // Two seconds of running: the original named step2 120 times; once per strip frame is 2 / 0.05 = 40 (give or
            // take the float clock's edge), the first as the strip starts.
            Assert.That(Step(w, Right), Is.EqualTo(new[] { "step2" }), "the run strip's first frame");
            Assert.That(w.Characters[0].Action, Is.EqualTo(ActionKind.Running));
            for (; steps < 119; steps++)
            {
                cues.AddRange(Step(w, Right));
                Assert.That(w.Characters[0].Action, Is.EqualTo(ActionKind.Running), $"step {steps}");
            }

            Assert.That(cues.Distinct(), Is.EqualTo(new[] { "step2" }));
            Assert.That(cues.Count + 1, Is.InRange(38, 41));
            Assert.That(run.FrameTime, Is.EqualTo(0.05f));
        }

        [Test]
        public void TheRunCueFollowsTheDrawnStripsClockFrameForFrame()
        {
            var s = Settings();
            var w = Landed(s);
            var drawn = new CharacterAnimator(s.Animations.Find("morris"));
            for (var i = 0; i < 90; i++)
            {
                var cues = Step(w, Right);
                var before = (drawn.Clip, drawn.Frame);
                drawn.Select(w.Characters[0]);
                var started = !ReferenceEquals(before.Clip, drawn.Clip);
                var frame = drawn.Frame;
                drawn.Advance(FixedStepper.OriginalElapsedSeconds);
                var shown = started || drawn.Frame != frame;
                Assert.That(cues.Count, Is.EqualTo(shown ? 1 : 0), $"step {i}");
            }
        }

        [Test]
        public void StoppingAndRunningAgainRestartsTheStepsAtTheStripsFirstFrame()
        {
            var w = Landed(Settings());
            Step(w, Right);
            Step(w, Right);
            for (var i = 0; i < 10 && w.Characters[0].Action != ActionKind.Standing; i++) Step(w, Idle);
            Assert.That(w.Characters[0].Action, Is.EqualTo(ActionKind.Standing));
            Assert.That(Step(w, Right), Is.EqualTo(new[] { "step2" }));
        }

        [Test]
        public void TheWallSlideNamesDragOncePerClipNotEveryFrame()
        {
            var s = Settings();
            var w = Shipped(s);
            var c = w.Characters[0];
            w.Characters[1].Reset(new PixelVector(1500, 100));
            c.Reset(new PixelVector(350, 150));
            var ticks = new List<long>();
            var slid = 0;
            for (var i = 0; i < 400; i++)
            {
                var tick = w.Tick;
                var cues = Step(w, Left);
                if (c.Action == ActionKind.WallSliding)
                {
                    slid++;
                    if (cues.Contains("drag")) ticks.Add(tick);
                }
                else if (slid > 0)
                {
                    break;
                }
            }

            Assert.That(slid, Is.GreaterThan(60), "slid down the column-20 wall");
            Assert.That(ticks.Count, Is.GreaterThan(3));
            var gaps = ticks.Zip(ticks.Skip(1), (a, b) => b - a).ToList();
            Assert.That(gaps.Distinct(), Is.EqualTo(new[] { 16L }), "0.26 s at 60 steps a second: the next drag as the last one ends");
            Assert.That(ticks.Count, Is.EqualTo((slid + 15) / 16));
        }

        [Test]
        public void MuteSilencesTheCharactersCuesAndAMutedDragDoesNotHoldBackTheNext()
        {
            var s = Settings();
            var w = Landed(s);
            ((IScriptHost)w).SetMuted(true);
            for (var i = 0; i < 30; i++) Assert.That(Step(w, Right), Is.Empty);
            ((IScriptHost)w).SetMuted(false);
            Assert.That(Enumerable.Range(0, 10).SelectMany(_ => Step(w, Right)), Does.Contain("step2"));

            var cues = new CharacterCues(null, s.Audio, FixedStepper.StepsPerSecond);
            var world = Shipped(s);
            var c = world.Characters[0];
            c.Reset(new PixelVector(350, 150));
            for (var i = 0; i < 60 && c.Action != ActionKind.WallSliding; i++) Step(world, Left);
            Assert.That(c.Action, Is.EqualTo(ActionKind.WallSliding));
            Assert.That(cues.Step(c, 100, FixedStepper.OriginalElapsedSeconds, muted: true), Is.EqualTo("drag"), "named, but the world drops it");
            Assert.That(cues.Step(c, 101, FixedStepper.OriginalElapsedSeconds, muted: false), Is.EqualTo("drag"), "nothing was heard, so it starts now");
            Assert.That(cues.Step(c, 102, FixedStepper.OriginalElapsedSeconds, muted: false), Is.Null);
            Assert.That(cues.Step(c, 101 + cues.WallSlideSteps, FixedStepper.OriginalElapsedSeconds, muted: false), Is.EqualTo("drag"));
        }

        [Test]
        public void TheFrozenWorldMakesNoCharacterSounds()
        {
            var w = Landed(Settings());
            Step(w, Right);
            Step(w, new ToggleMainMenu(P1));
            Assert.That(w.State, Is.EqualTo(GameState.MainMenu));
            for (var i = 0; i < 30; i++) Assert.That(Step(w), Is.Empty);
        }

        [Test]
        public void TheUnplayedCuesAreNeverNamed()
        {
            var s = Settings();
            var w = Landed(s);
            var heard = new HashSet<string>();
            foreach (var command in Enumerable.Repeat(Right, 60).Concat(Enumerable.Repeat<ICommand>(new JumpCharacter(P1), 40)).Concat(Enumerable.Repeat(Left, 120)))
            {
                heard.UnionWith(Step(w, command));
            }

            Assert.That(heard, Does.Not.Contain("music1").And.Not.Contain("footstep_1").And.Not.Contain("footstep_2")
                .And.Not.Contain("step1").And.Not.Contain("jump1"));
            Assert.That(s.Audio.FindCue("jump1").PlayedBy, Is.EqualTo("commented out"));
        }

        [Test]
        public void AWorldWithoutAnimationsNamesNoRunCueButStillDrags()
        {
            var s = Settings();
            var w = new GameWorld(Map.FromSettings(s.Grid), s.Player, s.Characters.Find(s.Characters.DefaultCharacter), s.Names, new Random(5), audio: s.Audio);
            w.Characters[1].Reset(new PixelVector(1500, 100));
            for (var i = 0; i < 300 && w.Characters[0].Action != ActionKind.Standing; i++) Step(w, Idle);
            Assert.That(Enumerable.Range(0, 30).SelectMany(_ => Step(w, Right)), Is.Empty);
        }

        [Test]
        public void SongsStartOnlyFromTheConsoleAndNotWhileMuted()
        {
            var w = Shipped(Settings());
            for (var i = 0; i < 600; i++) w.Advance(Frame);
            Assert.That(w.Music, Is.EqualTo(MusicState.Stopped), "music never starts by itself");

            var host = (IScriptHost)w;
            host.SetMuted(true);
            ConsoleScript.Execute("play music", w.Console, host);
            Assert.That(w.Music, Is.EqualTo(MusicState.Stopped));
            host.SetMuted(false);
            ConsoleScript.Execute("PLAY BATTLE", w.Console, host);
            Assert.That(w.Music, Is.EqualTo(MusicState.Playing));
            ConsoleScript.Execute("pause", w.Console, host);
            Assert.That(w.Music, Is.EqualTo(MusicState.Paused));
            ConsoleScript.Execute("pause", w.Console, host);
            Assert.That(w.Music, Is.EqualTo(MusicState.Playing));

            w.Submit(new EndSong(P1));
            w.Advance(Frame);
            Assert.That(w.Music, Is.EqualTo(MusicState.Stopped));
        }

        [Test]
        public void ARunOrWallSlideCueMissingFromTheTable_IsRejected()
        {
            var source = new MemorySettingsSource(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));
            source.Set(GameSettings.AudioFile, source.Read(GameSettings.AudioFile).Replace("\"wallSlideCue\": \"drag\"", "\"wallSlideCue\": \"drgg\""));
            var e = Assert.Throws<SettingsException>(() => GameSettings.Load(source));
            Assert.That(e.Errors.Single(), Does.Contain("drgg"));
        }

        [Test]
        public void ASongTheConsolePlaysMissingFromTheTable_IsRejected()
        {
            var source = new MemorySettingsSource(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));
            source.Set(GameSettings.AudioFile, source.Read(GameSettings.AudioFile).Replace("\"name\": \"battle\"", "\"name\": \"batle\""));
            var e = Assert.Throws<SettingsException>(() => GameSettings.Load(source));
            Assert.That(e.Errors.Single(), Does.Contain("'battle'"));
        }
    }
}
