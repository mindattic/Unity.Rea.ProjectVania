using System;

namespace Unity.Rea.ProjectVania.Core.Simulation
{
    /// <summary>
    /// The 60 Hz clock (RECONSTRUCTION 0). The original ran the XNA fixed timestep, so every constant is
    /// per frame, and Core must step at exactly 1/60 s. Frame time is turned into whole sub-units
    /// (one step = 1,000,000 units, one second = 60,000,000) so the accumulator never drifts: sixty
    /// frames of 1/60 s are exactly sixty steps.
    /// </summary>
    public sealed class FixedStepper
    {
        public const int StepsPerSecond = 60;
        public const double StepSeconds = 1.0 / StepsPerSecond;
        public const long UnitsPerStep = 1_000_000;
        public const long UnitsPerSecond = UnitsPerStep * StepsPerSecond;

        /// <summary>
        /// XNA's default <c>Game.TargetElapsedTime</c>, <c>TimeSpan.FromTicks(166667)</c>: with a fixed timestep every
        /// <c>ElapsedGameTime</c> the original's per-frame code read was exactly this, not 1/60.
        /// </summary>
        public const long XnaTargetElapsedTicks = 166667;

        /// <summary>
        /// The <c>elapsed</c> the original's <c>Player.Update</c> computed each frame:
        /// <c>(float)gameTime.ElapsedGameTime.TotalSeconds</c> of <see cref="XnaTargetElapsedTicks"/> (0.0166667).
        /// The character's per-step arithmetic uses it so jump, wall and timer frame counts match the original.
        /// </summary>
        public static readonly float OriginalElapsedSeconds = (float)(XnaTargetElapsedTicks * (1.0 / 10_000_000));

        /// <summary>
        /// Longest frame the clock honours (a stall is dropped rather than replayed as a burst of
        /// steps). XNA capped a frame at half a second; the same cap applies here.
        /// </summary>
        public const double MaxFrameSeconds = 0.5;

        long _accumulator;

        /// <summary>Time not yet stepped, in units (always under one step after Advance).</summary>
        public long AccumulatorUnits => _accumulator;

        /// <summary>How far through the next step the clock is, 0 to under 1: the interpolation factor for rendering.</summary>
        public double Alpha => (double)_accumulator / UnitsPerStep;

        /// <summary>Adds frame time and returns how many fixed steps are now due.</summary>
        public int Advance(double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds) || deltaSeconds <= 0) return 0;
            if (deltaSeconds > MaxFrameSeconds) deltaSeconds = MaxFrameSeconds;
            _accumulator += (long)Math.Round(deltaSeconds * UnitsPerSecond);
            int steps = (int)(_accumulator / UnitsPerStep);
            _accumulator -= steps * UnitsPerStep;
            return steps;
        }

        public void Reset() => _accumulator = 0;
    }
}
