using System.IO;
using NUnit.Framework;
using Unity.Shared.Core;
using UnityEngine;

namespace Unity.Rea.ProjectVania.Tests
{
    /// <summary>
    /// The CLI mute (U.VAN-T29): <c>-muted</c> / <c>MINDATTIC_CLI_MUTED=1</c> turns on Unity.Shared's session mute, the
    /// profile's CommonSettings report music and sound effects muted, and <see cref="SessionAudio"/> silences the
    /// listener without writing the player's settings.json.
    /// </summary>
    public class SessionAudioTests
    {
        string _root;
        ProfileStore _priorStore;
        bool _priorSessionMuted;
        float _priorVolume;

        [SetUp]
        public void SetUp()
        {
            _priorStore = Profiles.IsConfigured ? Profiles.Store : null;
            _priorSessionMuted = Profiles.SessionMuted;
            _priorVolume = AudioListener.volume;
            _root = Path.Combine(Path.GetTempPath(), "ProjectVaniaSessionAudio-" + System.Guid.NewGuid().ToString("N"));
            Profiles.UseRoot(_root);
            Profiles.SessionMuted = false;
            AudioListener.volume = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Profiles.SessionMuted = _priorSessionMuted;
            if (_priorStore != null)
            {
                Profiles.Use(_priorStore);
            }
            else
            {
                Profiles.Reset();
            }

            AudioListener.volume = _priorVolume;
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void TheSessionMuteSilencesTheListenerWithoutSavingSettings()
        {
            var profile = Profiles.Current;
            profile.SaveSettings();
            var saved = File.ReadAllText(profile.SettingsPath);
            Profiles.SessionMuted = true;

            Assert.That(SessionAudio.Apply(), Is.True);
            Assert.That(SessionAudio.Muted, Is.True);
            Assert.That(AudioListener.volume, Is.EqualTo(0f));
            Assert.That(File.ReadAllText(profile.SettingsPath), Is.EqualTo(saved), "settings.json was written.");
        }

        [Test]
        public void AnUnmutedProfileLeavesTheListenerAlone()
        {
            Assert.That(SessionAudio.Apply(), Is.False);
            Assert.That(AudioListener.volume, Is.EqualTo(1f));
        }

        [Test]
        public void OneChannelMutedByThePlayerKeepsTheOtherAudible()
        {
            Profiles.Current.Settings.Common.MuteSfx = true;

            Assert.That(SessionAudio.Apply(), Is.False);
            Assert.That(AudioListener.volume, Is.EqualTo(1f));
        }
    }
}
