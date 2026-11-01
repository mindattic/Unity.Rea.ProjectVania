using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using Unity.Rea.ProjectVania.Core.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// Every character's name over it (<c>SceneManager.DrawText</c>, RECONSTRUCTION 6 item 4): characters.json's
    /// <c>nameTag</c> writes, by default red at (X - 1, Y - 100) and then yellow at (X, Y), each centred on its x and
    /// lifted by the original's Top alignment, in an Arial 10 equivalent. UI Toolkit labels in the pixel-perfect
    /// panel, placed each frame where the game camera shows the character, after every character is drawn. The original
    /// drew the names through the camera's matrix, so each label is scaled by the camera's magnification and turned with
    /// it about its anchor, the top-centre of the lifted text. Each name's colour is its write's colour through the world's
    /// effect (<see cref="WorldEffectMaterial.NameColour"/>), as <c>DrawText</c> ran inside the effect's pass.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class NameTags : MonoBehaviour
    {
        public const string LayerName = "name-tags";

        GameWorld _world;
        NameTagData _tag;
        Camera _camera;
        float _pixelsPerUnit;
        readonly List<Label> _labels = new List<Label>();

        /// <summary>One label per character per write, characters in list order, writes in draw order.</summary>
        public IReadOnlyList<Label> Labels => _labels;

        public VisualElement Layer { get; private set; }

        public void Initialize(GameWorld world, NameTagData tag, Camera camera, float pixelsPerUnit, VisualElement root, SpriteEffect effect = null)
        {
            _world = world;
            _tag = tag;
            _camera = camera;
            _pixelsPerUnit = pixelsPerUnit;

            Layer = new VisualElement { name = LayerName, pickingMode = PickingMode.Ignore };
            Layer.style.position = Position.Absolute;
            Layer.style.left = 0;
            Layer.style.top = 0;
            Layer.style.width = PixelPerfect.ReferenceWidth;
            Layer.style.height = PixelPerfect.ReferenceHeight;
            root.Add(Layer);

            // XNA rendered a sprite font's point size at 96 dpi.
            var fontPixels = Mathf.Round(tag.FontPoints * 96f / 72f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var character in world.Characters)
            {
                foreach (var write in tag.Labels)
                {
                    var label = new Label(character.Name ?? string.Empty) { pickingMode = PickingMode.Ignore };
                    label.style.position = Position.Absolute;
                    label.style.fontSize = fontPixels;
                    if (font != null)
                    {
                        label.style.unityFontDefinition = FontDefinition.FromFont(font);
                    }

                    label.style.color = Colour(WorldEffectMaterial.NameColour(effect, write.Colour.ToRgba()));
                    label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
                    label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
                    label.style.unityTextAlign = TextAnchor.UpperCenter;
                    label.style.whiteSpace = WhiteSpace.NoWrap;
                    label.style.translate = new Translate(Length.Percent(-50), 0);
                    label.style.transformOrigin = new TransformOrigin(Length.Percent(50), 0);
                    _labels.Add(label);
                    Layer.Add(label);
                }
            }

            Place();
        }

        void LateUpdate()
        {
            Place();
        }

        /// <summary>Moves every label to its character's current (interpolated) feet plus its write's offset.</summary>
        public void Place()
        {
            if (_world == null || _camera == null)
            {
                return;
            }

            var alpha = (float)_world.Alpha;
            // What GameCamera framed: its orthographic size is the unzoomed one over the magnification, and its turn
            // (with a half turn for a negative magnification) is the original's rotation of the drawn text.
            var magnification = PixelPerfect.OrthographicSize(_pixelsPerUnit) / _camera.orthographicSize;
            var rotate = new Rotate(Angle.Degrees(_camera.transform.eulerAngles.z));
            var scale = new Scale(new Vector2(magnification, magnification));
            var index = 0;
            foreach (var character in _world.Characters)
            {
                var feet = character.PositionAt(alpha);
                foreach (var write in _tag.Labels)
                {
                    // The Top alignment's lift is in the world, before the matrix, as the original's DrawText did it.
                    var anchor = CharacterView.ToUnity(new PixelVector(feet.X + write.Offset.X, feet.Y + write.Offset.Y - _tag.TopAlignLift), _pixelsPerUnit);
                    var viewport = _camera.WorldToViewportPoint(anchor);
                    var label = _labels[index++];
                    label.style.left = viewport.x * PixelPerfect.ReferenceWidth;
                    label.style.top = (1f - viewport.y) * PixelPerfect.ReferenceHeight;
                    label.style.rotate = rotate;
                    label.style.scale = scale;
                }
            }
        }

        /// <summary>The XNA named colours (<see cref="SettingsColours.ToRgba"/>).</summary>
        public static Color Colour(SettingsColour colour) => Colour(colour.ToRgba());

        public static Color Colour(Rgba colour) => new Color32(colour.R, colour.G, colour.B, colour.A);
    }
}
