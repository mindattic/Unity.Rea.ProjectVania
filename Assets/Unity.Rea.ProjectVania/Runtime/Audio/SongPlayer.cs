using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Settings;
using UnityEngine;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// The original's <c>MediaPlayer</c> (RECONSTRUCTION 11): plays audio.json's two songs, <c>music</c> and <c>battle</c>,
    /// from <c>Resources/Music/&lt;file&gt;</c> (the original's <c>Content\Audio\Music\*.mp3</c>, streamed), only when the
    /// world names one (the console's <c>play music|battle</c>): from the beginning, once, not looping, and nothing starts
    /// by itself. <c>pause</c> pauses and resumes it; <c>mute</c> sets its volume to 0 and back to the table's volume
    /// (<c>MediaPlayer.Volume</c>), the song playing on silently, as the original's did. When a song runs to its end
    /// <see cref="Ended"/> turns true once, which <see cref="GameHost"/> reports to the world as <c>EndSong</c>.
    /// </summary>
    public sealed class SongPlayer : MonoBehaviour
    {
        public const string ResourceFolder = "Music";

        AudioData _audio;
        AudioSource _source;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        bool _heard;

        /// <summary>The song started last (its name in audio.json), or null before the first.</summary>
        public string Current { get; private set; }

        /// <summary>Whether the current song is paused (<c>MediaPlayer.Pause</c>).</summary>
        public bool Paused { get; private set; }

        /// <summary>Whether the song is muted (<c>MediaPlayer.Volume</c> 0).</summary>
        public bool Muted { get; private set; }

        /// <summary>Every song started, in order (for tests and the scripted run).</summary>
        public List<string> Played { get; } = new List<string>();

        /// <summary>The source the songs play through (2D, never looping).</summary>
        public AudioSource Source => _source;

        public void Initialize(AudioData audio)
        {
            _audio = audio;
            if (_source == null)
            {
                // Its own source: the cues' one-shots go through CuePlayer's, and a song is stopped, paused and muted alone.
                _source = gameObject.AddComponent<AudioSource>();
            }

            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            SetMuted(audio != null && audio.Muted);
        }

        /// <summary>The streamed clip of <paramref name="song"/>, or null if audio.json has no such song or it is not imported.</summary>
        public AudioClip Clip(string song)
        {
            var entry = _audio?.FindSong(song);
            if (entry == null)
            {
                return null;
            }

            if (!_clips.TryGetValue(entry.Name, out var clip))
            {
                clip = Resources.Load<AudioClip>($"{ResourceFolder}/{System.IO.Path.GetFileNameWithoutExtension(entry.File)}");
                _clips[entry.Name] = clip;
            }

            return clip;
        }

        /// <summary><c>MediaPlayer.Play(song)</c>: stops whatever was playing and starts <paramref name="song"/> from its start.</summary>
        public bool Play(string song)
        {
            var clip = Clip(song);
            if (clip == null)
            {
                return false;
            }

            _source.Stop();
            _source.clip = clip;
            _source.time = 0f;
            _source.Play();
            Current = song;
            Paused = false;
            _heard = false;
            Played.Add(song);
            return true;
        }

        /// <summary><c>MediaPlayer.Pause</c> (true) or <c>Resume</c> (false).</summary>
        public void SetPaused(bool paused)
        {
            if (Current == null)
            {
                return;
            }

            Paused = paused;
            if (paused)
            {
                _source.Pause();
            }
            else
            {
                _source.UnPause();
            }
        }

        /// <summary><c>MediaPlayer.Volume = Muted ? 0 : Audio.Volume</c>: the song keeps its place, unheard.</summary>
        public void SetMuted(bool muted)
        {
            Muted = muted;
            if (_source != null)
            {
                _source.volume = muted ? 0f : (_audio?.Volume ?? 1f);
            }
        }

        /// <summary>
        /// True once, on the first check after the current song has run to its end (it was heard playing and has stopped
        /// without being paused); the song is then over.
        /// </summary>
        public bool Ended()
        {
            if (Current == null || Paused || _source == null)
            {
                return false;
            }

            if (_source.isPlaying)
            {
                _heard = true;
                return false;
            }

            if (!_heard)
            {
                return false;
            }

            Current = null;
            _heard = false;
            return true;
        }
    }
}
