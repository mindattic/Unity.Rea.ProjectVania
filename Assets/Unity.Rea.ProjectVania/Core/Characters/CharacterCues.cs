using System;
using Unity.Rea.ProjectVania.Core.Settings;

namespace Unity.Rea.ProjectVania.Core.Characters
{
    /// <summary>
    /// The sounds one character makes (RECONSTRUCTION 4.8, 11): <c>Action_Run</c>'s <c>PlaySfx("step2")</c> and
    /// <c>Action_Wallslide</c>'s <c>PlaySfx("drag")</c>, which the original started every frame the action ran, 60 overlapping
    /// starts a second (RECONSTRUCTION 15.4). Fixed (BIBLE defect 4): the run cue is named once for every frame the run strip
    /// shows, on its own copy of the strip's clock (<see cref="CharacterAnimator"/>, stepped at the fixed rate as XNA's
    /// fixed-step draw stepped it), so a step is heard with each footfall the player sees; the wall-slide cue is named
    /// again only once the last one it named has run its clip's length (audio.json's <c>seconds</c>), so the drag sounds as
    /// one continuous scrape for as long as the slide lasts. Engine-free; the world names what this returns.
    /// </summary>
    public sealed class CharacterCues
    {
        readonly AnimationSheet _sheet;
        CharacterAnimator _runClock;
        readonly string _runCue;
        readonly string _wallSlideCue;
        readonly int _wallSlideSteps;
        long _wallSlideEnds = long.MinValue;

        /// <param name="sheet">The character's strips (animations.json), whose run strip times the run cue; null names no run cue.</param>
        /// <param name="audio">audio.json: the two cue names and the wall-slide cue's clip length.</param>
        /// <param name="stepsPerSecond">The fixed rate the world steps at (60).</param>
        public CharacterCues(AnimationSheet sheet, AudioData audio, int stepsPerSecond)
        {
            if (audio == null) throw new ArgumentNullException(nameof(audio));
            if (stepsPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(stepsPerSecond));
            _sheet = sheet;
            _runCue = audio.RunCue;
            _wallSlideCue = audio.WallSlideCue;
            var seconds = audio.FindCue(_wallSlideCue)?.Seconds ?? 0f;
            // The step on which the clip has finished: 0.26 s is 15.6 steps, so the next drag starts on the 16th.
            _wallSlideSteps = Math.Max(1, (int)Math.Ceiling(seconds * stepsPerSecond - 1e-4));
        }

        /// <summary>How many steps a wall-slide cue lasts before the next one may start.</summary>
        public int WallSlideSteps => _wallSlideSteps;

        /// <summary>
        /// After <paramref name="character"/>'s update on step <paramref name="tick"/> (Play only, as <c>CheckActionState</c>
        /// ran only there), the cue it makes this step, or null. <paramref name="muted"/>: a cue the world will not name
        /// (<c>PlaySfx</c> returned while muted) starts no clip, so the wall slide's next drag is not held back by it.
        /// </summary>
        public string Step(PlayerCharacter character, long tick, float seconds, bool muted)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (character.Action != ActionKind.Running) _runClock = null;
            switch (character.Action)
            {
                case ActionKind.Running:
                    return RunStep(character, seconds);

                case ActionKind.WallSliding:
                    if (string.IsNullOrEmpty(_wallSlideCue) || tick < _wallSlideEnds) return null;
                    if (!muted) _wallSlideEnds = tick + _wallSlideSteps;
                    return _wallSlideCue;

                default:
                    return null;
            }
        }

        // One run cue for each frame the run strip shows: as it starts (frame 0) and every time its clock steps a frame.
        // The clock starts afresh with every run, as the drawn strip restarts at frame 0 when the action turns to Running.
        string RunStep(PlayerCharacter character, float seconds)
        {
            if (_sheet == null || string.IsNullOrEmpty(_runCue)) return null;
            if (_runClock == null) _runClock = new CharacterAnimator(_sheet);
            var before = _runClock.Clip;
            _runClock.Select(character);
            var clip = _runClock.Clip;
            if (clip == null || clip.Name != CharacterAnimator.Run) return null;

            var frame = _runClock.Frame;
            var started = !ReferenceEquals(before, clip);
            _runClock.Advance(seconds);
            return started || _runClock.Frame != frame ? _runCue : null;
        }
    }
}
