using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Simulation;
using Unity.Rea.ProjectVania.Core.Viewing;
using UnityEngine;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// Shows a player's Core camera (<see cref="WorldCamera"/>, the original's <c>CameraManager</c>, RECONSTRUCTION 7)
    /// through the game camera each frame, interpolated by the world's <c>Alpha</c>. The original's matrix scales by the
    /// scale cubed and turns about the world origin, then translates; for an orthographic camera that is the same
    /// picture as one centred on the world point the matrix puts at the screen centre, turned by the rotation (a
    /// negative magnification adding a half turn) and with its orthographic size divided by the magnification. It only
    /// reads the camera; the camera changes only through commands.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(Camera), typeof(PixelPerfectCamera))]
    public sealed class GameCamera : MonoBehaviour
    {
        GameWorld _world;
        PlayerId _player;
        float _pixelsPerUnit;

        /// <summary>The player whose camera this shows.</summary>
        public PlayerId Player => _player;

        public void Initialize(GameWorld world, PlayerId player, float pixelsPerUnit)
        {
            _world = world;
            _player = player;
            _pixelsPerUnit = pixelsPerUnit;
            Present();
        }

        void LateUpdate()
        {
            Present();
        }

        /// <summary>Frames the game camera on the player's camera as of the world's current interpolation.</summary>
        public void Present()
        {
            var camera = _world?.Camera(_player);
            if (camera == null)
            {
                return;
            }

            Apply(camera.ViewAt((float)_world.Alpha));
        }

        /// <summary>Frames the game camera on <paramref name="view"/>.</summary>
        public void Apply(CameraView view)
        {
            var centre = CharacterView.ToUnity(view.Centre, _pixelsPerUnit);
            transform.position = new Vector3(centre.x, centre.y, transform.position.z);
            // XNA's positive rotation turns the y-down world clockwise on screen; turning a y-up camera
            // anticlockwise by the same angle does that.
            var degrees = view.Rotation * Mathf.Rad2Deg + (view.Magnification < 0f ? 180f : 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, degrees);
            GetComponent<PixelPerfectCamera>().Magnification = Mathf.Abs(view.Magnification);
        }
    }
}
