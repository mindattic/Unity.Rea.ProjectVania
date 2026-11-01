using System;
using Unity.Rea.ProjectVania.Core.Settings;

namespace Unity.Rea.ProjectVania.Core.Characters
{
    /// <summary>
    /// One character's sprite animation: the original's <c>AnimationPlayer</c> struct and the <c>sprite.PlayAnimation</c>
    /// calls of <c>Player.cs</c>'s <c>Action_*</c> methods (RECONSTRUCTION 4.8), engine-free. <see cref="Select"/> picks
    /// the strip the character's action plays, as the original's update did every frame; <see cref="Advance"/> moves the
    /// clock and the frame index, as the original's draw did. Presentation reads <see cref="Clip"/> and
    /// <see cref="Frame"/> and never changes the character.
    /// </summary>
    public sealed class CharacterAnimator
    {
        // The strip each Action_* method played. Hanging is declared and never used, so it plays nothing new.
        public const string Idle = "idle";
        public const string Run = "run";
        public const string Jump = "jump";
        public const string Fall = "fall";
        public const string WallSlide = "wallslide";
        public const string Kneel = "kneel";
        public const string Slide = "slide";

        float _time;

        public CharacterAnimator(AnimationSheet sheet)
        {
            Sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        }

        /// <summary>The character's strips (animations.json).</summary>
        public AnimationSheet Sheet { get; }

        /// <summary>The strip playing, or null before the first <see cref="Select"/>.</summary>
        public AnimationClip Clip { get; private set; }

        /// <summary>The frame of <see cref="Clip"/> to draw: the source rectangle is this times the frame size along the strip.</summary>
        public int Frame { get; private set; }

        /// <summary>
        /// The strip name <paramref name="character"/>'s action plays this frame, or null to keep the current one: the
        /// jump strip plays only while the jump is live (<c>Action_Jump</c>'s <c>if (isJumping)</c>), and Hanging plays nothing.
        /// </summary>
        public static string ClipFor(PlayerCharacter character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (character.Action == ActionKind.Jumping && !character.IsJumping) return null;
            return ClipFor(character.Action);
        }

        /// <summary>The strip <paramref name="action"/> plays (idle, run, jump, ...), or null for Hanging, which plays nothing.</summary>
        public static string ClipFor(ActionKind action)
        {
            switch (action)
            {
                case ActionKind.Standing: return Idle;
                case ActionKind.Running: return Run;
                case ActionKind.Jumping: return Jump;
                case ActionKind.Falling: return Fall;
                case ActionKind.WallSliding: return WallSlide;
                case ActionKind.Kneeling: return Kneel;
                case ActionKind.Sliding: return Slide;
                default: return null;
            }
        }

        /// <summary>
        /// Plays the strip <paramref name="character"/>'s action calls for. A sheet without that strip plays idle: the
        /// original threw <c>KeyNotFoundException</c> there and crashed, which the rebuild does not carry over (BIBLE,
        /// characters). The shipped sheets have every strip their character's move set plays (U.VAN-T25: Shanoa's and
        /// Leo's missing ones borrow frames of their own idle and run strips, animations.json).
        /// </summary>
        public void Select(PlayerCharacter character)
        {
            var name = ClipFor(character);
            if (name == null) return;
            Play(Sheet.Find(name) ?? Sheet.Find(Idle));
        }

        /// <summary><c>AnimationPlayer.PlayAnimation</c>: restarts at frame 0 only if <paramref name="clip"/> is not the one already playing.</summary>
        public void Play(AnimationClip clip)
        {
            if (clip == null || ReferenceEquals(clip, Clip)) return;
            Clip = clip;
            Frame = 0;
            _time = 0f;
        }

        /// <summary>
        /// <c>AnimationPlayer.Draw</c>'s clock: adds <paramref name="seconds"/> and steps a frame for every whole frame
        /// time passed, wrapping a looping strip and holding a one-shot strip on its last frame.
        /// </summary>
        public void Advance(float seconds)
        {
            if (Clip == null) return;
            _time += seconds;
            while (_time > Clip.FrameTime)
            {
                _time -= Clip.FrameTime;
                Frame = Clip.Loop ? (Frame + 1) % Clip.Frames : Math.Min(Frame + 1, Clip.Frames - 1);
            }
        }
    }
}
