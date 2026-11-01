using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Settings;
using UnityEngine;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// Plays the cues the world names (<c>AudioManager.PlayAmbience</c> / <c>PlaySfx</c>, RECONSTRUCTION 11) from
    /// audio.json's cue table: the cue's wave is <c>Resources/Audio/&lt;wave&gt;.wav</c>, imported as the original's PCM,
    /// started as a new one-shot instance each time (XACT allowed 255 per category) at the cue's XACT volume, in
    /// hundredths of a dB, times the table's volume. Mute is the world's: it names no cue while muted (audio.json's
    /// muted at start, then the console's mute), as the original's play methods returned while <c>Muted</c>, and a cue
    /// already started plays on through a mute, as XACT's did. Every wave the table's cues name is imported but
    /// <c>music1</c>'s (an 11 MB PCM loop no code ever started; the asset sweep, A17, brings it in): the four menu cues,
    /// the console's two, the run's <c>step2</c> and the wall slide's <c>drag</c>, and the unplayed <c>footstep_1</c>,
    /// <c>footstep_2</c>, <c>step1</c> and <c>jump1</c>.
    /// </summary>
    public sealed class CuePlayer : MonoBehaviour
    {
        public const string ResourceFolder = "Audio";

        AudioData _audio;
        AudioSource _source;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

        /// <summary>Every cue started, in order (for tests and the scripted run).</summary>
        public List<string> Played { get; } = new List<string>();

        public void Initialize(AudioData audio)
        {
            _audio = audio;
            if (_source == null)
            {
                // Its own source: SongPlayer's, on the same object, is the songs'.
                _source = gameObject.AddComponent<AudioSource>();
            }

            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
        }

        /// <summary>XACT's volume in hundredths of a dB as a linear gain (0 dB is 1, -1200 is about 0.25).</summary>
        public static float Gain(int hundredthsOfDecibel) => Mathf.Pow(10f, hundredthsOfDecibel / 100f / 20f);

        /// <summary>The clip of <paramref name="cue"/>'s wave, or null if the cue is not in the table or its wave is not imported.</summary>
        public AudioClip Clip(string cue)
        {
            var entry = _audio?.FindCue(cue);
            if (entry == null)
            {
                return null;
            }

            if (!_clips.TryGetValue(entry.Wave, out var clip))
            {
                clip = Resources.Load<AudioClip>($"{ResourceFolder}/{entry.Wave}");
                _clips[entry.Wave] = clip;
            }

            return clip;
        }

        /// <summary>Starts <paramref name="cue"/>; returns whether it was started.</summary>
        public bool Play(string cue)
        {
            if (_audio == null)
            {
                return false;
            }

            var clip = Clip(cue);
            if (clip == null)
            {
                return false;
            }

            _source.PlayOneShot(clip, _audio.Volume * Gain(_audio.FindCue(cue).Volume));
            Played.Add(cue);
            return true;
        }
    }
}
