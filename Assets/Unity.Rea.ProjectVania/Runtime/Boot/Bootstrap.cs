using System;
using Unity.Rea.ProjectVania.Core.Characters;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// The code-built bootstrap (RECONSTRUCTION 0): the project's one scene is empty, and this composes everything
    /// at start-up instead of Inspector wiring. It points the shared profiles at the player's data folder, loads the
    /// settings registry (stopping with every error listed if a file is bad), then builds the pixel-perfect camera,
    /// the game host, the map with the editor's world cursor, a sprite per character, and the UI Toolkit root (with the characters' names, the HUD, the main menu, the editor's palette and cursors, the console and the HUD's text) at the 800x600 reference, and the
    /// cue player the menu's and the console's sounds play through.
    /// </summary>
    public static class Bootstrap
    {
        public const string RootName = "ProjectVania";

        /// <summary>
        /// The online scene (U.VAN-T31), second in the build after the local game's Main: it composes the game itself once a
        /// session starts, so the bootstrap leaves it alone.
        /// </summary>
        public const string OnlineScene = "Online";

        /// <summary>The launch argument that opens the online scene instead of local play (play.ps1 -Online).</summary>
        public const string OnlineArgument = "-online";

        /// <summary>The composed game, or null before the bootstrap ran or after it stopped on bad settings.</summary>
        public static GameHost Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnLoad()
        {
            // Local play is exactly as before; online play is its own scene, which composes the game per session.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == OnlineScene)
            {
                return;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), OnlineArgument) >= 0 && Application.CanStreamedLevelBeLoaded(OnlineScene))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(OnlineScene);
                return;
            }

            Run(Startup.StreamingSource());
        }

        /// <summary>Loads the settings from <paramref name="source"/> and composes the game, or logs every error and stops.</summary>
        public static GameHost Run(ISettingsSource source)
        {
            Current = null;
            Unity.Shared.ProfilesBootstrap.Initialize();

            var result = Startup.LoadSettings(source);
            if (!result.Succeeded)
            {
                Stop(result);
                return null;
            }

            Current = Compose(result.Settings);
            return Current;
        }

        /// <summary>
        /// Builds the camera, host and UI for valid settings. Replaces a previous composition. Online (U.VAN-T31)
        /// <paramref name="player"/> is this screen's seat (its camera, editor and controls) and <paramref name="seed"/> the
        /// authority's, so every peer's world starts the same; local play passes neither.
        /// </summary>
        public static GameHost Compose(GameSettings settings, PlayerId? player = null, int? seed = null)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Remove(existing);
            }

            var root = new GameObject(RootName);
            var host = root.AddComponent<GameHost>();
            host.Initialize(settings, player, seed);

            // Letterbox bars are outside every camera's viewport rect, so nothing clears them on their own; this
            // camera draws nothing (cullingMask 0) and runs before the game camera (lower depth) only to paint
            // them black, full-screen, under whatever the game camera draws over it.
            var letterboxObject = new GameObject("LetterboxCamera");
            letterboxObject.transform.SetParent(root.transform, false);
            var letterbox = letterboxObject.AddComponent<Camera>();
            letterbox.clearFlags = CameraClearFlags.SolidColor;
            letterbox.backgroundColor = Color.black;
            letterbox.cullingMask = 0;
            letterbox.depth = -1;

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = MapView.ToColor(settings.Grid.Draw.ClearColour); // XNA CornflowerBlue, SceneManager.Draw's clear colour
            cameraObject.AddComponent<AudioListener>(); // hears the cues (CuePlayer's sources are 2D)
            var pixelPerfect = cameraObject.AddComponent<PixelPerfectCamera>();
            pixelPerfect.PixelsPerUnit = settings.Grid.PixelsPerUnit;
            cameraObject.AddComponent<GameCamera>().Initialize(host.World, host.Input.Player, settings.Grid.PixelsPerUnit);

            // Every world sprite is drawn through effects.json's preset (SceneManager's "filter"), each in its own texture
            // coordinates (RECONSTRUCTION 6.1); the GUI after it is not.
            var effect = SpriteEffect.World(settings.Effects);
            var effectMaterial = WorldEffectMaterial.World(settings.Effects);
            BuildMap(root.transform, host, settings, effect, effectMaterial);
            BuildCharacters(root.transform, host, settings, effectMaterial);
            var ui = BuildUi(root.transform);
            ui.gameObject.AddComponent<NameTags>().Initialize(host.World, settings.Characters.NameTag, camera, settings.Grid.PixelsPerUnit, ui.rootVisualElement, effect);
            // The GUI draws after the scene: first the HUD's pictures and the fade lens (RECONSTRUCTION 9.1), so the fade
            // darkens the world, its names and the HUD ...
            ui.gameObject.AddComponent<HudView>().Initialize(host.World, ui.rootVisualElement);
            // ... then the menu, which covers the world and its names (RECONSTRUCTION 9) ...
            ui.gameObject.AddComponent<MainMenuView>().Initialize(host.World, settings.Menu, ui.rootVisualElement);
            // ... the state's own GUI: the hand over the menu, or the editor's palette and Screen cursor (RECONSTRUCTION 8, 12) ...
            ui.gameObject.AddComponent<EditorGuiView>().Initialize(host.World, host.Input.Player, settings.Editor, settings.Grid.TilesetAsset, ui.rootVisualElement);
            // ... the console after the menu, as GuiManager.Draw drew it (RECONSTRUCTION 9, 10.1) ...
            ui.gameObject.AddComponent<ConsoleView>().Initialize(host.World, ui.rootVisualElement);
            // ... and last the debug readout and the message stack.
            ui.gameObject.AddComponent<HudTextView>().Initialize(host.World, host.Input.Player, ui.rootVisualElement);
            Current = host;
            return host;
        }

        /// <summary>Removes the composed game, if any (an online session ended, U.VAN-T31).</summary>
        public static void Dispose()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Remove(existing);
            }

            Current = null;
        }

        /// <summary>The map under the characters: outline, layers, cells, then the editor's cursor (RECONSTRUCTION 5, 6).</summary>
        static void BuildMap(Transform parent, GameHost host, GameSettings settings, SpriteEffect effect, Material effectMaterial)
        {
            var map = new GameObject("Map");
            map.transform.SetParent(parent, false);
            map.AddComponent<MapView>().Initialize(host.World, settings.Grid, effect, effectMaterial);

            // MapEditor.DrawToWorld: the World cursor, after the cells and before the characters.
            var cursor = new GameObject("EditorCursor");
            cursor.transform.SetParent(map.transform, false);
            cursor.AddComponent<EditorCursorView>().Initialize(host.World, host.Input.Player, settings.Editor, settings.Grid.PixelsPerUnit, effectMaterial);
        }

        /// <summary>One sprite per Core character, drawn from its sheet's strips (RECONSTRUCTION 4.8).</summary>
        static void BuildCharacters(Transform parent, GameHost host, GameSettings settings, Material effectMaterial)
        {
            var group = new GameObject("Characters");
            group.transform.SetParent(parent, false);
            var art = new CharacterArt(settings.Animations, settings.Grid.PixelsPerUnit);
            var characters = host.World.Characters;
            for (var i = 0; i < characters.Count; i++)
            {
                var definition = characters[i].Definition ?? settings.Characters.Find(settings.Characters.DefaultCharacter);
                var view = new GameObject($"Character {i} ({characters[i].Name})", typeof(SpriteRenderer)).AddComponent<CharacterView>();
                view.transform.SetParent(group.transform, false);
                var animator = new CharacterAnimator(settings.Animations.Find(definition.Sheet));
                view.Initialize(host.World, i, animator, art, settings.Grid.PixelsPerUnit, effectMaterial);
            }
        }

        static UIDocument BuildUi(Transform parent)
        {
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "ProjectVaniaPanel";
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");

            var uiObject = new GameObject("UI");
            uiObject.transform.SetParent(parent, false);
            var document = uiObject.AddComponent<UIDocument>();
            document.panelSettings = panel;

            // Fits the panel to the same viewport PixelPerfectCamera gave the game camera (both read the current
            // Screen size through PixelPerfect.Fit), applied immediately rather than waiting for the first
            // LateUpdate so the UI is never shown unscaled for a frame, and in edit-mode tests that run no frames.
            var pixelPerfectUi = uiObject.AddComponent<PixelPerfectPanel>();
            pixelPerfectUi.Apply(Screen.width, Screen.height);
            return document;
        }

        /// <summary>Logs every error of the failed load and stops the game (quits a build, leaves Play mode in the Editor).</summary>
        static void Stop(StartupResult result)
        {
            Debug.LogError($"[ProjectVania] The settings in {result.Source} are invalid ({result.Errors.Count} problem(s)); stopping.");
            foreach (var error in result.Errors)
            {
                Debug.LogError($"[ProjectVania] {error}");
            }

            if (!Application.isPlaying)
            {
                return;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(1);
#endif
        }

        static void Remove(GameObject target)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
