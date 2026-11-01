using System;
using System.IO;
using System.Reflection;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.ProjectVania.EditorTools
{
    /// <summary>
    /// Renders the composed game's camera to PNGs at the 800 x 600 reference, so a change can be seen working from the
    /// command line (<c>unity.ps1 capture</c>, which runs with graphics). It boots the game as the player would, lets the
    /// characters fall and land and the Loose camera settle on player one, then captures the followed view, the view
    /// zoomed in and turned by the camera keys (the original's matrix: scale cubed, about the world origin), the map's
    /// top-left corner, and that corner with the editor open. The main menu's pictures (menu, menu-video), the console's (console-sliding, console, console-full-red), the HUD's (play-ui, hud-quickbar-away, hud-fade-half) and the map editor's (editor-paint, editor-palette) draw the UI Toolkit overlay over the camera's picture; the others are the camera alone, without the names or the HUD.
    /// Last, each of the five effects (RECONSTRUCTION 6.1) draws the tileset as a sprite (effect-*.png) and every pixel is
    /// checked against Core's <see cref="SpriteEffect"/>, so the GPU's shader is known to be the cells' arithmetic.
    /// </summary>
    public static class SceneCapture
    {
        public const string DefaultOutput = "Logs/Capture";

        public static void Capture()
        {
            try
            {
                var output = ArgumentValue("-captureOutput") ?? DefaultOutput;
                Directory.CreateDirectory(output);
                var host = Bootstrap.Run(Startup.StreamingSource());
                if (host == null)
                {
                    throw new InvalidOperationException("The settings did not load.");
                }

                Steps(host, 300);

                Present(host);
                var camera = GameObject.Find("Camera").GetComponent<Camera>();
                Save(camera, Path.Combine(output, "warmup.png")); // the pipeline's first render in a fresh editor is incomplete
                File.Delete(Path.Combine(output, "warmup.png"));
                Save(camera, Path.Combine(output, "play.png"));

                // The main menu (U.VAN-US-A10) over the frozen world: the root, then the Video level, then closed again.
                SaveWithUi(camera, host, Path.Combine(output, "play-ui.png"));
                host.World.Submit(new ToggleMainMenu(PlayerId.One));
                host.World.Advance(1.0 / 60);
                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "menu.png"));
                host.World.Submit(new NavigateMenu(PlayerId.One, MenuInput.Down));
                host.World.Submit(new NavigateMenu(PlayerId.One, MenuInput.Select));
                host.World.Advance(1.0 / 60);
                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "menu-video.png"));
                host.World.Submit(new NavigateMenu(PlayerId.One, MenuInput.Back));
                host.World.Submit(new NavigateMenu(PlayerId.One, MenuInput.Back));
                host.World.Advance(1.0 / 60);
                Present(host);

                // The console (U.VAN-US-A11): half-way down, then down with a few lines run, then full height on red.
                host.World.Submit(new ToggleConsole(PlayerId.One));
                Steps(host, 6);
                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "console-sliding.png"));
                while (host.World.Console.Sliding)
                {
                    Steps(host, 1);
                }

                foreach (var line in new[] { "lee", "say hello", "console history yellow", "console text", "ver", "console text green", "type here" })
                {
                    Type(host, line, submit: line != "type here");
                }

                while (!host.World.Console.Blink)
                {
                    Steps(host, 1);
                }

                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "console.png"));
                Type(host, "console full", submit: true);
                Type(host, "console bg red", submit: true);
                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "console-full-red.png"));
                host.World.Submit(new ToggleConsole(PlayerId.One));
                Steps(host, 1);
                while (host.World.Console.Sliding)
                {
                    Steps(host, 1);
                }

                Present(host);

                // The HUD (U.VAN-US-A12): a message and the readout over the quick bar slid off the bottom, then the screen
                // half faded out under the text (the lens is drawn before the menu, the console and the text).
                Type(host, "say the quick bar is away", submit: true);
                host.World.Submit(new ToggleQuickBar(PlayerId.One));
                Steps(host, 120);
                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "hud-quickbar-away.png"));
                host.World.Submit(new ToggleQuickBar(PlayerId.One));
                Steps(host, 40);
                Type(host, "fade out", submit: true);
                Steps(host, 127);
                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "hud-fade-half.png"));
                Type(host, "fade off", submit: true);
                Present(host);

                // = held 20 steps (scale 1.2, magnified 1.728 about the world origin), then PageUp 20 (0.2 rad clockwise).
                Nudge(host, CameraNudge.ZoomIn, 20);
                Present(host);
                Save(camera, Path.Combine(output, "zoomed.png"));
                Nudge(host, CameraNudge.RotateRight, 20);
                Present(host);
                Save(camera, Path.Combine(output, "turned.png"));
                Nudge(host, CameraNudge.RotateLeft, 20);
                Nudge(host, CameraNudge.ZoomOut, 20);
                Present(host);

                // The map editor (U.VAN-US-A14): the third tile of the second row picked with the arrows, the mouse
                // scrolls the view up off the floor, left held paints a row of it while the mouse scrolls right, the cursor
                // rests over the row's end (highlighted yellow), then Space picks another tile under the Screen cursor.
                host.World.Submit(new ToggleMapEditor(PlayerId.One));
                host.World.Submit(new StepEditor(PlayerId.One, 1, 0, true, false));
                host.World.Submit(new StepEditor(PlayerId.One, 0, 1, true, false));
                Edit(host, 400f, 300f, 1);
                host.World.Submit(new StepEditor(PlayerId.One, 1, 0, true, false));
                Edit(host, 400f, 260f, 8);
                Edit(host, 400f, 300f, 40);
                Edit(host, 430f, 300f, 30, left: true);
                Edit(host, 400f, 300f, 40);
                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "editor-paint.png"));
                Edit(host, 16f + 16 * 6, 300f + 16 * 3, 1, screen: true);
                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "editor-palette.png"));
                Edit(host, 400f, 300f, 1);
                host.World.Submit(new ToggleMapEditor(PlayerId.One));
                Steps(host, 1);
                Present(host);

                var follow = camera.GetComponent<GameCamera>();
                follow.enabled = false;
                camera.GetComponent<PixelPerfectCamera>().Magnification = 1f;
                camera.transform.SetPositionAndRotation(new Vector3(300f / 16f, -200f / 16f, camera.transform.position.z), Quaternion.identity);
                Save(camera, Path.Combine(output, "corner.png"));

                host.World.Submit(new ToggleMapEditor(PlayerId.One));
                host.World.Advance(1.0 / 60);
                Present(host);
                Save(camera, Path.Combine(output, "editor.png"));

                // U.VAN-US-A18 (U.VAN-T21): the rest of RECONSTRUCTION's mechanics EndToEndPlaythroughTests plays through
                // Core alone, seen here with Unity's graphics. Placed last, after every other capture's own choreography
                // (which assumes the spawn-area camera framing the earlier sections settled onto), so moving the driven
                // character and cycling the camera's mode here disturbs nothing captured above. Back to Play first (the
                // editor left it in MapEditor, where only the editor itself steps, not the characters) with the camera
                // tracking again (disabled for corner.png/editor.png above).
                host.World.Submit(new ToggleMapEditor(PlayerId.One));
                host.World.Advance(1.0 / 60);
                follow.enabled = true;

                // The editor section above painted a row of Hard cells near row 27 (editor-paint.png's own row, not
                // grid.json's structures) around the spawn column, which a fall from there would now land on well
                // above the floor. Column 100 (x 1600) is far outside every camera view any capture above showed, so
                // it is still the clean row-30 floor (RECONSTRUCTION 5, "a full floor on row 30", the whole grid
                // width); the driven character falls there from spawn height instead.
                var driven = host.World.Characters[0];
                driven.Reset(new PixelVector(1600f, 100f));
                Steps(host, 120); // falls, lands, and LoosePlayer re-settles onto it before the shots below
                Present(host);

                // The camera's four modes (U.VAN-US-A9, RECONSTRUCTION 7): C cycles Free -> LockPlayer -> LoosePlayer ->
                // LooseCursor -> Free; one shot per mode, named from the mode actually landed on. Four cycles return to
                // the mode this block started from (LoosePlayer, set by leaving the editor above).
                for (var i = 0; i < 4; i++)
                {
                    host.World.Submit(new CycleCameraMode(PlayerId.One));
                    Steps(host, 30);
                    Present(host);
                    var mode = host.World.Camera(PlayerId.One).Mode.ToString().ToLowerInvariant();
                    Save(camera, Path.Combine(output, $"camera-{mode}.png"));
                }

                // Runs on the open floor (RECONSTRUCTION 4.2), kneels and releases (BIBLE defect 3, U.VAN-T23, settled),
                // slides (4.5), then is placed beside the bridge's left wall the way the Core test and
                // PlayerCharacterTests' OnLeftWall do, to wall slide (4.6) and wall jump with the push-off (defect 6,
                // U.VAN-T24, settled).
                for (var i = 0; i < 15; i++)
                {
                    host.World.Submit(new MoveCharacter(PlayerId.One, -1));
                    host.World.Advance(1.0 / 60);
                }

                host.World.Submit(new StandCharacter(PlayerId.One));
                host.World.Advance(1.0 / 60);
                host.World.Submit(new KneelCharacter(PlayerId.One));
                host.World.Advance(1.0 / 60);
                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "kneel.png"));
                host.World.Submit(new StandCharacter(PlayerId.One)); // releasing S: U.VAN-T23 stands him up at once
                host.World.Advance(1.0 / 60);

                for (var i = 0; i < 10; i++)
                {
                    host.World.Submit(new MoveCharacter(PlayerId.One, -1));
                    host.World.Advance(1.0 / 60);
                }

                for (var i = 0; i < 8; i++)
                {
                    host.World.Submit(new KneelCharacter(PlayerId.One));
                    host.World.Advance(1.0 / 60);
                }

                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "slide.png"));
                host.World.Submit(new StandCharacter(PlayerId.One));
                host.World.Advance(1.0 / 60);

                driven.Reset(new PixelVector(350f, 300f)); // beside the left wall (column 20, right face x = 336)
                for (var i = 0; i < 20; i++)
                {
                    host.World.Submit(new MoveCharacter(PlayerId.One, -1));
                    host.World.Advance(1.0 / 60);
                }

                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "wallslide.png"));
                host.World.Submit(new MoveCharacter(PlayerId.One, -1));
                host.World.Submit(new JumpCharacter(PlayerId.One));
                host.World.Advance(1.0 / 60);
                for (var i = 0; i < 5; i++)
                {
                    host.World.Submit(new MoveCharacter(PlayerId.One, -1));
                    host.World.Advance(1.0 / 60);
                }

                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "walljump.png")); // pushed off the wall, not pinned to it (defect 6)

                CheckEffects(host, output);

                UnityEngine.Object.DestroyImmediate(GameObject.Find(Bootstrap.RootName));
                Debug.Log($"[ProjectVania] Captured to {Path.GetFullPath(output)}");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProjectVania] Capture failed: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Draws the tileset unscaled through each preset's material over black and compares every pixel with Core's shade
        /// of the same texel drawn over black; throws if any channel is off by more than <paramref name="tolerance"/>.
        /// </summary>
        static void CheckEffects(GameHost host, string output, int tolerance = 1)
        {
            var texture = Resources.Load<Texture2D>(MapView.PathOf(host.Settings.Grid.TilesetAsset));
            var source = MapView.Download(texture);
            var origin = new Vector3(1000f, 1000f, 0f);
            var cameraObject = new GameObject("EffectCheckCamera");
            var spriteObject = new GameObject("EffectCheckSprite", typeof(SpriteRenderer));
            var report = new System.Text.StringBuilder();
            try
            {
                const int layer = 31;
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = origin + new Vector3(0f, 0f, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << layer;
                camera.orthographic = true;
                camera.orthographicSize = 600f / 2f / 16f;
                camera.aspect = 800f / 600f;

                // The sprite's top-left on the view's top-left pixel, one texel a pixel.
                var renderer = spriteObject.GetComponent<SpriteRenderer>();
                spriteObject.layer = layer;
                renderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0f, 1f), 16f, 0, SpriteMeshType.FullRect);
                spriteObject.transform.position = origin + new Vector3(-400f / 16f, 300f / 16f, 0f);

                var black = new Rgba(0, 0, 0, 255);
                foreach (EffectPreset preset in Enum.GetValues(typeof(EffectPreset)))
                {
                    var material = WorldEffectMaterial.Create(preset, host.Settings.Effects);
                    renderer.sharedMaterial = material;
                    var effect = new SpriteEffect(preset, host.Settings.Effects);
                    var target = new RenderTexture(800, 600, 24, RenderTextureFormat.ARGB32);
                    var previousActive = RenderTexture.active;
                    var image = new Texture2D(800, 600, TextureFormat.RGB24, false);
                    try
                    {
                        camera.targetTexture = target;
                        camera.Render();
                        camera.Render(); // the pipeline's first render of a new camera can be incomplete
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 800, 600), 0, 0);
                        image.Apply();
                        File.WriteAllBytes(Path.Combine(output, $"effect-{preset.ToString().ToLowerInvariant()}.png"), image.EncodeToPNG());
                    }
                    finally
                    {
                        camera.targetTexture = null;
                        RenderTexture.active = previousActive;
                        UnityEngine.Object.DestroyImmediate(target);
                        UnityEngine.Object.DestroyImmediate(material);
                    }

                    var worst = 0;
                    var worstAt = (0, 0);
                    for (var y = 0; y < source.Height; y++)
                    {
                        for (var x = 0; x < source.Width; x++)
                        {
                            var expected = Rgba.Over(effect.ShadeTexel(source, x, y, Rgba.White), black);
                            Color32 shown = image.GetPixel(x, 599 - y);
                            var off = Math.Max(Math.Abs(shown.r - expected.R), Math.Max(Math.Abs(shown.g - expected.G), Math.Abs(shown.b - expected.B)));
                            if (off > worst)
                            {
                                worst = off;
                                worstAt = (x, y);
                            }
                        }
                    }

                    UnityEngine.Object.DestroyImmediate(image);
                    report.AppendLine($"{preset}: largest difference from Core {worst} at {worstAt}");
                    if (worst > tolerance)
                    {
                        throw new InvalidOperationException($"The {preset} shader differs from Core's SpriteEffect by {worst} at texel {worstAt}.");
                    }
                }
            }
            finally
            {
                File.WriteAllText(Path.Combine(output, "effects.txt"), report.ToString());
                UnityEngine.Object.DestroyImmediate(spriteObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        static void Present(GameHost host)
        {
            foreach (var view in host.GetComponentsInChildren<CharacterView>())
            {
                view.Present(0f);
            }

            host.GetComponentInChildren<MapView>().Present();
            host.GetComponentInChildren<NameTags>()?.Place();
            host.GetComponentInChildren<HudView>()?.Refresh();
            host.GetComponentInChildren<MainMenuView>()?.Refresh();
            host.GetComponentInChildren<EditorCursorView>()?.Present(1f);
            host.GetComponentInChildren<EditorGuiView>()?.Refresh();
            host.GetComponentInChildren<ConsoleView>()?.Refresh();
            host.GetComponentInChildren<HudTextView>()?.Refresh();
            var follow = host.GetComponentInChildren<GameCamera>();
            if (follow != null && follow.enabled)
            {
                follow.Present();
            }
        }

        /// <summary>Clears the console's line, types <paramref name="line"/> and, if asked, presses Enter, in one step.</summary>
        static void Type(GameHost host, string line, bool submit)
        {
            host.World.Submit(new EditConsole(PlayerId.One, ConsoleInput.Clear));
            host.World.Submit(new EditConsole(PlayerId.One, ConsoleInput.Append, line));
            if (submit)
            {
                host.World.Submit(new EditConsole(PlayerId.One, ConsoleInput.Submit));
            }

            Steps(host, 1);
        }

        /// <summary><paramref name="steps"/> editor steps with the mouse at (x, y), left held or not, Space held for <paramref name="screen"/>.</summary>
        static void Edit(GameHost host, float x, float y, int steps, bool left = false, bool screen = false)
        {
            for (var i = 0; i < steps; i++)
            {
                host.World.Submit(new SetEditorCursor(PlayerId.One, screen ? EditorCursorMode.Screen : EditorCursorMode.World));
                host.World.Submit(new PointEditor(PlayerId.One, x, y, left, false));
                host.World.Advance(1.0 / 60);
            }
        }

        static void Steps(GameHost host, int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                host.World.Advance(1.0 / 60);
            }
        }

        static void Nudge(GameHost host, CameraNudge nudge, int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                host.World.Submit(new NudgeCamera(PlayerId.One, nudge));
                host.World.Advance(1.0 / 60);
            }
        }

        static void Save(Camera camera, string path) => Save(camera, path, null);

        /// <summary>
        /// The camera's picture with the UI Toolkit panel drawn over it: the panel is pointed at the same render texture
        /// (without clearing it) at a scale of 1 and repainted by UI Toolkit's own runtime loop, called directly because an
        /// -executeMethod run ticks no player loop.
        /// </summary>
        static void SaveWithUi(Camera camera, GameHost host, string path)
        {
            var document = host.GetComponentInChildren<UIDocument>();
            var panel = document.GetComponent<PixelPerfectPanel>();
            var settings = document.panelSettings;
            Save(camera, path, target =>
            {
                var previous = settings.targetTexture;
                try
                {
                    settings.clearColor = false;
                    settings.targetTexture = target;
                    panel.Apply(800, 600);
                    host.GetComponentInChildren<NameTags>()?.Place();
                    // Lay out first, then draw exactly once: the texture is not cleared, so a second repaint would draw
                    // the translucent menu over itself.
                    PumpPanels("UpdatePanels");
                    PumpPanels("UpdatePanels", "RepaintPanels", "RenderOffscreenPanels");
                }
                finally
                {
                    settings.targetTexture = previous;
                    panel.Apply(Screen.width, Screen.height);
                }
            });
        }

        static void PumpPanels(params string[] steps)
        {
            var utility = typeof(UIDocument).Assembly.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
            if (utility == null)
            {
                throw new InvalidOperationException("UIElementsRuntimeUtility not found; the UI cannot be captured.");
            }

            const BindingFlags any = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var name in steps)
            {
                var method = utility.GetMethod(name, any);
                if (method == null)
                {
                    throw new InvalidOperationException($"UIElementsRuntimeUtility.{name} not found; the UI cannot be captured.");
                }

                var parameters = method.GetParameters();
                var arguments = new object[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    // RepaintPanels(bool onlyOffscreen): the panel now draws into the capture's texture, which is offscreen.
                    arguments[i] = parameters[i].ParameterType == typeof(bool) ? (object)true : null;
                }

                method.Invoke(null, arguments);
            }
        }

        static void Save(Camera camera, string path, Action<RenderTexture> overlay)
        {
            var target = new RenderTexture(800, 600, 24, RenderTextureFormat.ARGB32);
            var previousRect = camera.rect;
            var previousActive = RenderTexture.active;
            try
            {
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.orthographic = true;
                // 600 rows at 16 px per unit over the camera's zoom, as PixelPerfectCamera frames it
                camera.orthographicSize = 600f / 2f / 16f / camera.GetComponent<PixelPerfectCamera>().Magnification;
                camera.aspect = 800f / 600f;
                camera.targetTexture = target;
                camera.Render();
                overlay?.Invoke(target);
                RenderTexture.active = target;
                var image = new Texture2D(800, 600, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 800, 600), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = null;
                camera.rect = previousRect;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        static string ArgumentValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
