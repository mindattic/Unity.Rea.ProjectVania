using System;
using Unity.Shared.Core;
using UnityEngine;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// How the CLI mute reaches the game (U.VAN-T29): launched with <c>-muted</c> or <c>MINDATTIC_CLI_MUTED=1</c>,
    /// Unity.Shared's bootstrap turns on its session mute before the first scene loads, and the shared profile's
    /// <c>CommonSettings</c> then report music and sound effects muted at volume 0. <see cref="CuePlayer"/> and
    /// <see cref="SongPlayer"/> follow audio.json and the console's <c>mute</c>, not the profile, so this silences the
    /// listener instead: everything they play goes unheard for this run only. <c>AudioListener.volume</c> is never saved
    /// and settings.json is never written. ProjectVania has both songs and cues, so (as Incognito's) the listener is only
    /// silenced when the profile reports BOTH channels muted; the CLI mute forces both. The console's <c>mute</c> is
    /// untouched: it still toggles the world's own mute underneath.
    /// </summary>
    public static class SessionAudio
    {
        /// <summary>True once <see cref="Apply"/> found the profile's music and sound effects both muted.</summary>
        public static bool Muted { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnLoad() => Apply();

        /// <summary>Reads the current profile's audio settings and mutes the listener if they say so. Never unmutes. Returns <see cref="Muted"/>.</summary>
        public static bool Apply()
        {
            Muted = ProfileMutesSound();
            if (Muted)
            {
                AudioListener.volume = 0f;
                Debug.Log("ProjectVania: audio muted for this session (Unity.Shared profile mute; -muted / MINDATTIC_CLI_MUTED=1).");
            }

            return Muted;
        }

        static bool ProfileMutesSound()
        {
            if (!Profiles.IsConfigured)
            {
                return false;
            }

            try
            {
                var common = Profiles.Current.Settings.Common;
                bool musicMuted = common.MuteMusic || common.MusicVolume <= 0f;
                bool sfxMuted = common.MuteSfx || common.SfxVolume <= 0f;
                return musicMuted && sfxMuted;
            }
            catch (Exception e)
            {
                // A profile folder the disk refuses must not stop the game; it just plays with sound.
                Debug.LogWarning($"ProjectVania: could not read the profile's audio settings ({e.Message}); sound stays on.");
                return false;
            }
        }
    }
}
