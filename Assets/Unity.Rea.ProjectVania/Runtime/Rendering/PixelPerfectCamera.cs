using Unity.Rea.ProjectVania.Core.Display;
using UnityEngine;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// Draws the 800x600 reference pixel-perfect: an orthographic camera showing exactly 600 rows at the grid's
    /// pixels per unit, with its viewport set to the largest whole magnification the window holds, centred
    /// (<see cref="PixelPerfect"/>). It touches only <see cref="Camera"/>, so it is the same under any render
    /// pipeline; the pipeline itself is chosen by project assets, not by this code.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PixelPerfectCamera : MonoBehaviour
    {
        int _width, _height;
        float _ppu;

        /// <summary>The pixels per unit the camera frames the reference at.</summary>
        public float PixelsPerUnit
        {
            get => _ppu;
            set
            {
                _ppu = value;
                Apply(Screen.width, Screen.height);
            }
        }

        /// <summary>
        /// How much the game camera magnifies the reference (the original's scale cubed, A9): the orthographic size is
        /// divided by it, so 1 is the unzoomed pixel-perfect view. Positive; the caller turns a negative one into a half turn.
        /// </summary>
        public float Magnification
        {
            get => _magnification;
            set
            {
                _magnification = value > 0f ? value : 1f;
                if (_ppu > 0f)
                {
                    GetComponent<Camera>().orthographicSize = PixelPerfect.OrthographicSize(_ppu) / _magnification;
                }
            }
        }

        float _magnification = 1f;

        /// <summary>The viewport last applied.</summary>
        public PixelViewport Viewport { get; private set; }

        void LateUpdate()
        {
            if (Screen.width != _width || Screen.height != _height)
            {
                Apply(Screen.width, Screen.height);
            }
        }

        /// <summary>Frames the camera for a window of the given size.</summary>
        public void Apply(int screenWidth, int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0 || !(_ppu > 0f))
            {
                return;
            }

            _width = screenWidth;
            _height = screenHeight;
            var view = PixelPerfect.Fit(screenWidth, screenHeight);
            Viewport = view;
            var cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = PixelPerfect.OrthographicSize(_ppu) / _magnification;
            // The viewport is always the reference's 4:3, even when a tiny window rounds it to a pixel or two.
            cam.aspect = PixelPerfect.ReferenceWidth / (float)PixelPerfect.ReferenceHeight;
            cam.rect = new Rect(view.X / (float)screenWidth, view.Y / (float)screenHeight, view.Width / (float)screenWidth, view.Height / (float)screenHeight);
        }
    }
}
