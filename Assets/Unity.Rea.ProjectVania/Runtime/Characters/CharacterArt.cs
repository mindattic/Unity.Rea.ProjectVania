using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Settings;
using UnityEngine;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// The characters' sprite strips (RECONSTRUCTION 4.8, 13): <c>Resources/Characters/&lt;sheet&gt;/&lt;animation&gt;.png</c>,
    /// cut into animations.json's square frames, frame n at x = n * frameSize as the original's AnimationPlayer cut
    /// them, each pivoted at the bottom centre (the original's origin (frameSize / 2, frameSize)) so a character's
    /// position is its feet. Frames are made once per strip and shared by every character of that sheet.
    /// </summary>
    public sealed class CharacterArt
    {
        public const string ResourceFolder = "Characters";

        readonly AnimationData _animations;
        readonly float _pixelsPerUnit;
        readonly Dictionary<AnimationClip, Sprite[]> _frames = new Dictionary<AnimationClip, Sprite[]>();

        public CharacterArt(AnimationData animations, float pixelsPerUnit)
        {
            _animations = animations;
            _pixelsPerUnit = pixelsPerUnit;
        }

        /// <summary>The Resources path of one strip.</summary>
        public static string PathOf(string sheet, string animation) => $"{ResourceFolder}/{sheet}/{animation}";

        /// <summary>The frames of <paramref name="clip"/> from <paramref name="sheet"/>, or null (logged) if its strip is missing or too small.</summary>
        public Sprite[] Frames(string sheet, AnimationClip clip)
        {
            if (clip == null)
            {
                return null;
            }

            if (_frames.TryGetValue(clip, out var cached))
            {
                return cached;
            }

            // A borrowed clip (U.VAN-T25) cuts its frames from another strip of the sheet, at the frames it lists.
            var path = PathOf(sheet, clip.Strip);
            var texture = Resources.Load<Texture2D>(path);
            Sprite[] frames = null;
            var size = _animations.FrameSize;
            var needed = 0;
            for (var i = 0; i < clip.Frames; i++) needed = Mathf.Max(needed, clip.SourceFrame(i) + 1);
            if (texture == null)
            {
                Debug.LogError($"[ProjectVania] The character strip {path} is missing.");
            }
            else if (texture.height != size || texture.width < needed * size)
            {
                Debug.LogError($"[ProjectVania] {path} is {texture.width} x {texture.height}; animations.json expects {needed} frames of {size} x {size}.");
            }
            else
            {
                frames = new Sprite[clip.Frames];
                for (var i = 0; i < clip.Frames; i++)
                {
                    var sprite = Sprite.Create(texture, new Rect(clip.SourceFrame(i) * size, 0, size, size), new Vector2(0.5f, 0f), _pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprite.name = $"{sheet}_{clip.Name}_{i}";
                    frames[i] = sprite;
                }
            }

            _frames[clip] = frames;
            return frames;
        }
    }
}
