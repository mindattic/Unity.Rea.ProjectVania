using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using Unity.Rea.ProjectVania.Core.World;
using UnityEngine;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// The map editor's World cursor (<c>MapEditor.DrawToWorld</c>, RECONSTRUCTION 6, 8): the 13 x 18 hand, tinted by the
    /// buttons held, drawn in the world after the map's cells and before the characters, with its top-left on the
    /// editor's cursor, only while the game is in the editor and Space is not held. It is placed on the interpolated
    /// camera's centre plus half the hand, as Core puts the cursor on the stepped camera's, so it stays still on screen
    /// while the view scrolls. Drawn through the world's effect, as <c>DrawToWorld</c> ran inside <c>DrawMap</c>'s pass.
    /// It only reads the world.
    /// </summary>
    [DefaultExecutionOrder(45)]
    public sealed class EditorCursorView : MonoBehaviour
    {
        /// <summary>After the cells (<see cref="MapView.CellsOrder"/>), under the characters (from 0).</summary>
        public const int SortingOrder = -50;

        GameWorld _world;
        PlayerId _player;
        float _pixelsPerUnit;

        public SpriteRenderer Renderer { get; private set; }

        public void Initialize(GameWorld world, PlayerId player, EditorData data, float pixelsPerUnit, Material effect = null)
        {
            _world = world;
            _player = player;
            _pixelsPerUnit = pixelsPerUnit;
            Renderer = gameObject.AddComponent<SpriteRenderer>();
            Renderer.sortingOrder = SortingOrder;
            if (effect != null)
            {
                Renderer.sharedMaterial = effect;
            }
            var path = MainMenuView.PathOf(data.Cursor.Asset);
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogError($"[ProjectVania] The editor cursor {path} is missing.");
            }
            else
            {
                // The hand's top-left is the cursor's position (SpriteBatch.Draw(texture, position, colour)).
                Renderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0f, 1f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
                Renderer.sprite.name = data.Cursor.Asset;
            }

            Present((float)world.Alpha);
        }

        void LateUpdate()
        {
            if (_world != null)
            {
                Present((float)_world.Alpha);
            }
        }

        /// <summary>Shows the hand where the editor's World cursor is, <paramref name="alpha"/> of the way through the step.</summary>
        public void Present(float alpha)
        {
            var editor = _world.Editor(_player);
            var shown = _world.State == GameState.MapEditor && editor != null && editor.Mode == EditorCursorMode.World;
            Renderer.enabled = shown;
            if (!shown)
            {
                return;
            }

            var at = editor.Cursor;
            var camera = _world.Camera(_player);
            if (camera != null && editor.Data != null)
            {
                var view = camera.ViewAt(alpha);
                at = new PixelVector(-view.Position.X + editor.CursorOffset.X, -view.Position.Y + editor.CursorOffset.Y);
            }

            transform.localPosition = new Vector3(at.X / _pixelsPerUnit, -at.Y / _pixelsPerUnit, 0f);
            Renderer.color = MapView.ToColor(editor.Colour);
        }
    }
}
