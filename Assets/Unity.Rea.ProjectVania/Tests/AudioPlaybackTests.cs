using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Characters;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.World;
using UnityEngine;

namespace Unity.Rea.ProjectVania.Tests
{
    /// <summary>
    /// The original's sounds heard (U.VAN-US-A13, RECONSTRUCTION 11): every cue's wave but music1's imported at the table's
    /// length, the run's step2 and the wall slide's drag played through the cue player, and the console's two songs
    /// started, paused, resumed and muted through the song player, which reports a finished song back to the world.
    /// </summary>
    public class AudioPlaybackTests
    {
        static string SettingsFolder => Path.Combine(Application.dataPath, "StreamingAssets", GameSettings.Folder);
        static readonly PlayerId P1 = PlayerId.One;

        GameHost _host;

        [SetUp]
        public void SetUp()
        {
            _host = Bootstrap.Run(new DirectorySettingsSource(SettingsFolder));
            Assert.That(_host, Is.Not.Null);
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
        }

        void Run(string line)
        {
            _host.World.Console.Append(line);
            Step(new EditConsole(P1, ConsoleInput.Submit));
        }

        [Test]
        public void EveryCuesWaveButMusic1IsImportedAtTheTablesLength()
        {
            foreach (var cue in _host.Settings.Audio.Cues)
            {
                var clip = _host.Cues.Clip(cue.Cue);
                if (cue.Cue == "music1")
                {
                    Assert.That(clip, Is.Null, "kept in the table, its 11 MB wave left to the asset sweep (A17)");
                    continue;
                }

                Assert.That(clip, Is.Not.Null, $"Resources/Audio/{cue.Wave}.wav");
                Assert.That(clip.length, Is.EqualTo(cue.Seconds).Within(0.02f), cue.Cue);
            }
        }

        [Test]
        public void TheSongsAreImportedStreamed()
        {
            foreach (var song in _host.Settings.Audio.Songs)
            {
                var clip = _host.Songs.Clip(song.Name);
                Assert.That(clip, Is.Not.Null, $"Resources/Music/{song.File}");
                Assert.That(clip.loadType, Is.EqualTo(AudioClipLoadType.Streaming), song.Name);
                Assert.That(clip.length, Is.GreaterThan(60f), song.Name);
            }
        }

        [Test]
        public void RunningAndWallSlidingPlayStepAndDrag()
        {
            var world = _host.World;
            var c = world.Characters[0];
            world.Characters[1].Reset(new PixelVector(1500, 100));
            for (var i = 0; i < 300 && c.Action != ActionKind.Standing; i++) Step(new StandCharacter(P1));
            Assert.That(c.Action, Is.EqualTo(ActionKind.Standing));
            for (var i = 0; i < 30; i++) Step(new MoveCharacter(P1, 1));
            var steps = _host.Cues.Played.Count(p => p == "step2");
            Assert.That(steps, Is.InRange(9, 11), "half a second of running: one step per 0.05 s strip frame");

            c.Reset(new PixelVector(350, 150));
            for (var i = 0; i < 60 && c.Action != ActionKind.WallSliding; i++) Step(new MoveCharacter(P1, -1));
            Assert.That(c.Action, Is.EqualTo(ActionKind.WallSliding));
            for (var i = 0; i < 31; i++) Step(new MoveCharacter(P1, -1));
            Assert.That(_host.Cues.Played.Count(p => p == "drag"), Is.EqualTo(2), "32 steps of sliding: a drag as each 0.26 s clip ends");
        }

        [Test]
        public void TheConsoleStartsPausesAndMutesTheSongs()
        {
            Assert.That(_host.Songs.Current, Is.Null, "no song starts by itself");
            Run("play music");
            Assert.That(_host.Songs.Played, Is.EqualTo(new[] { "music" }));
            Assert.That(_host.Songs.Source.clip, Is.SameAs(_host.Songs.Clip("music")));
            Assert.That(_host.Songs.Source.loop, Is.False);
            Assert.That(_host.Songs.Source.volume, Is.EqualTo(1f));

            Run("pause");
            Assert.That(_host.Songs.Paused, Is.True);
            Run("pause");
            Assert.That(_host.Songs.Paused, Is.False);

            Run("mute");
            Assert.That(_host.Songs.Source.volume, Is.EqualTo(0f), "MediaPlayer.Volume 0: the song plays on unheard");
            Assert.That(_host.Songs.Current, Is.EqualTo("music"));
            Run("play battle");
            Assert.That(_host.Songs.Played, Is.EqualTo(new[] { "music" }), "PlayMusic does nothing while muted");
            Run("mute");
            Assert.That(_host.Songs.Source.volume, Is.EqualTo(1f));
            Run("play battle");
            Assert.That(_host.Songs.Played, Is.EqualTo(new[] { "music", "battle" }));
            Assert.That(_host.Songs.Source.clip, Is.SameAs(_host.Songs.Clip("battle")));
        }

        [Test]
        public void ASongNeverHeardPlayingIsNotTakenForFinished()
        {
            Run("play battle");
            Assert.That(_host.World.Music, Is.EqualTo(MusicState.Playing));
            _host.Songs.Source.Stop();
            // Stopped before it was ever heard playing (as a source is with no audio output): not an end to report, so
            // the world's pause keeps reading Playing, as MediaPlayer.State did until the song really finished.
            Assert.That(_host.Songs.Ended(), Is.EqualTo(false));
            Assert.That(_host.Songs.Current, Is.EqualTo("battle"));
        }
    }
}
