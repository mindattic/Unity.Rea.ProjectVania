using Unity.Rea.ProjectVania.Core.Characters;
using Unity.Rea.ProjectVania.Core.Simulation;
using Unity.Rea.ProjectVania.Core.World;
using UnityEngine;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// Draws one Core character (<c>Player.Draw</c>, RECONSTRUCTION 4.8): picks the strip its action plays, advances the
    /// animation clock by the frame's time as the original's draw did, and shows that frame at the character's feet,
    /// mirrored by <see cref="PlayerCharacter.Flipped"/>, through the world's effect in the strip's own texture
    /// coordinates (RECONSTRUCTION 6.1). It only reads the character.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CharacterView : MonoBehaviour
    {
        GameWorld _world;
        CharacterArt _art;
        float _pixelsPerUnit;
        SpriteRenderer _renderer;

        public int Index { get; private set; }
        public CharacterAnimator Animator { get; private set; }
        public PlayerCharacter Character => _world.Characters[Index];
        public SpriteRenderer Renderer => _renderer;

        public void Initialize(GameWorld world, int index, CharacterAnimator animator, CharacterArt art, float pixelsPerUnit, Material effect = null)
        {
            _world = world;
            Index = index;
            Animator = animator;
            _art = art;
            _pixelsPerUnit = pixelsPerUnit;
            _renderer = GetComponent<SpriteRenderer>();
            if (effect != null)
            {
                _renderer.sharedMaterial = effect;
            }

            // Drawn in list order, as the original's foreach did: a later character over an earlier one.
            _renderer.sortingOrder = index;
            Present(0f);
        }

        void LateUpdate()
        {
            Present(Time.unscaledDeltaTime);
        }

        /// <summary>One draw: choose the strip, advance its clock by <paramref name="seconds"/>, place and show the frame.</summary>
        public void Present(float seconds)
        {
            if (_world == null)
            {
                return;
            }

            var character = Character;
            Animator.Select(character);
            Animator.Advance(seconds);

            var frames = _art.Frames(Animator.Sheet.Sheet, Animator.Clip);
            _renderer.sprite = frames != null && Animator.Frame < frames.Length ? frames[Animator.Frame] : null;
            _renderer.flipX = character.Flipped;

            transform.localPosition = ToUnity(character.PositionAt((float)_world.Alpha), _pixelsPerUnit);
        }

        /// <summary>The original's pixels (y down) as Unity units (y up), one grid cell per unit.</summary>
        public static Vector3 ToUnity(PixelVector pixels, float pixelsPerUnit) =>
            new Vector3(pixels.X / pixelsPerUnit, -pixels.Y / pixelsPerUnit, 0f);
    }
}
