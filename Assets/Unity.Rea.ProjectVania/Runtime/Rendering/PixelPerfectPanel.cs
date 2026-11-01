using Unity.Rea.ProjectVania.Core.Display;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// Keeps a UI Toolkit panel pixel-perfect with <see cref="PixelPerfectCamera"/>: the same <see cref="PixelPerfect"/>
    /// viewport fit is applied to the panel's scale and its root is clipped to that viewport's rectangle, so the UI
    /// is magnified by the same whole number as the game image and never spills into the letterbox bars. Touches
    /// only <see cref="UIDocument"/> and <see cref="PanelSettings"/>, so it is the same under any render pipeline.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PixelPerfectPanel : MonoBehaviour
    {
        int _width, _height;
        UIDocument _document;

        /// <summary>The viewport last applied.</summary>
        public PixelViewport Viewport { get; private set; }

        // Resolved lazily rather than in Awake: Bootstrap calls Apply right after AddComponent, in the same call,
        // before Unity has run this component's Awake (edit-mode scripts only get it at the next editor tick).
        UIDocument Document => _document != null ? _document : (_document = GetComponent<UIDocument>());

        void LateUpdate()
        {
            if (Screen.width != _width || Screen.height != _height)
            {
                Apply(Screen.width, Screen.height);
            }
        }

        /// <summary>Fits the panel's scale and clips its root to the pixel-perfect viewport of a window this size.</summary>
        public void Apply(int screenWidth, int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return;
            }

            _width = screenWidth;
            _height = screenHeight;
            var view = PixelPerfect.Fit(screenWidth, screenHeight);
            Viewport = view;

            // Below one whole magnification the reference itself was shrunk to fit (PixelPerfect.Fit), so the
            // panel follows with the same fractional scale instead of a meaningless scale of zero.
            var scale = view.Scale > 0 ? view.Scale : view.Width / (float)PixelPerfect.ReferenceWidth;
            var settings = Document.panelSettings;
            if (settings != null)
            {
                settings.scaleMode = PanelScaleMode.ConstantPixelSize;
                settings.scale = scale;
            }

            var root = Document.rootVisualElement;
            if (root == null)
            {
                return;
            }

            // The panel's own units are screen pixels divided by its scale, so the root is placed and sized in those:
            // it is then exactly the 800 x 600 reference, and a child placed at reference pixel (x, y) lands on the
            // game image's pixel (x, y) at any magnification. UI Toolkit's screen space is top-down; PixelViewport's
            // Y is bottom-left (RECONSTRUCTION 0's back buffer convention), so the top edge is the distance from the
            // window's top to the viewport's top.
            root.style.position = Position.Absolute;
            root.style.left = view.X / scale;
            root.style.top = (screenHeight - view.Y - view.Height) / scale;
            root.style.width = view.Width / scale;
            root.style.height = view.Height / scale;
            root.style.overflow = Overflow.Hidden;
        }
    }
}
