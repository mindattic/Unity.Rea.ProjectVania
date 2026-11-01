using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Gui;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// Draws the pictures of the always-on HUD (RECONSTRUCTION 9.1) in <c>GuiManager.Draw</c>'s order, every frame in
    /// every state: the portrait (glass at 0.4, the photo stretched to the frame, the frame) at (0, H - 110), the quick bar
    /// (glass at 0.4, frame) at (103, its slide's Y), then the fade lens, the whole screen tinted black at the lens' alpha.
    /// UI Toolkit elements in the pixel-perfect panel after the names and before the main menu and the console, so the
    /// fade darkens the world and the HUD but not the menu, the console or the text written after them
    /// (<see cref="HudTextView"/>). Every figure is hud.json's; it only reads the world.
    /// </summary>
    [DefaultExecutionOrder(105)]
    public sealed class HudView : MonoBehaviour
    {
        public const string LayerName = "hud";

        GameWorld _world;
        readonly List<VisualElement> _portrait = new List<VisualElement>();
        readonly List<VisualElement> _quickBar = new List<VisualElement>();

        public VisualElement Layer { get; private set; }
        public VisualElement Portrait { get; private set; }
        public VisualElement QuickBar { get; private set; }
        public VisualElement Lens { get; private set; }

        /// <summary>The portrait's pictures in draw order: glass, photo, frame.</summary>
        public IReadOnlyList<VisualElement> PortraitLayers => _portrait;

        /// <summary>The quick bar's pictures in draw order: glass, frame.</summary>
        public IReadOnlyList<VisualElement> QuickBarLayers => _quickBar;

        public void Initialize(GameWorld world, VisualElement root)
        {
            _world = world;
            Layer = new VisualElement { name = LayerName, pickingMode = PickingMode.Ignore };
            Absolute(Layer, 0, 0, PixelPerfect.ReferenceWidth, PixelPerfect.ReferenceHeight);
            Layer.style.overflow = Overflow.Hidden;
            root.Add(Layer);

            var hud = world.Hud;
            if (hud == null)
            {
                Layer.style.display = DisplayStyle.None;
                return;
            }

            var data = hud.Data;
            var screenHeight = (int)data.Screen.Y;

            Portrait = Group("hud-portrait", data.Portrait.X, screenHeight - data.Portrait.FromBottom, data.Portrait.Width, data.Portrait.Height);
            foreach (var picture in data.Portrait.Layers)
            {
                _portrait.Add(Picture(Portrait, picture));
            }

            QuickBar = Group("hud-quick-bar", data.QuickBar.X, hud.QuickBar.Y, data.QuickBar.Width, data.QuickBar.Height);
            foreach (var picture in data.QuickBar.Layers)
            {
                _quickBar.Add(Picture(QuickBar, picture));
            }

            // Lenses: GuiItem("white", (0, 0, 0, 0), (0, 0, W, H)), the 1 x 1 white texture stretched and tinted, which is a
            // plain fill of the tint.
            Lens = new VisualElement { name = "hud-lens", pickingMode = PickingMode.Ignore };
            Absolute(Lens, 0, 0, data.Screen.X, data.Screen.Y);
            Layer.Add(Lens);

            Refresh();
        }

        void LateUpdate()
        {
            Refresh();
        }

        /// <summary>Places the quick bar where its slide has it and tints the lens.</summary>
        public void Refresh()
        {
            var hud = _world?.Hud;
            if (hud == null)
            {
                return;
            }

            QuickBar.style.top = hud.QuickBar.Y;
            Lens.style.backgroundColor = NameTags.Colour(hud.Lens.Colour);
        }

        VisualElement Group(string name, float left, float top, float width, float height)
        {
            var group = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            Absolute(group, left, top, width, height);
            Layer.Add(group);
            return group;
        }

        /// <summary>One <c>spriteBatch.Draw(texture, rectangle, g.Alpha(a))</c>: the picture stretched over its group, tinted white at its alpha.</summary>
        static VisualElement Picture(VisualElement group, HudPicture picture)
        {
            var element = new VisualElement { name = picture.Asset, pickingMode = PickingMode.Ignore };
            Absolute(element, 0, 0, group.style.width.value.value, group.style.height.value.value);
            element.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>(MainMenuView.PathOf(picture.Asset)));
            element.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            element.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, picture.Alpha);
            group.Add(element);
            return element;
        }

        static void Absolute(VisualElement element, float left, float top, float width, float height)
        {
            element.style.position = Position.Absolute;
            element.style.left = left;
            element.style.top = top;
            element.style.width = width;
            element.style.height = height;
        }
    }
}
