using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.EditorTools;
using Unity.Shared.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Unity.Rea.ProjectVania.Tests
{
    /// <summary>The Unity 6 setup of U.VAN-US-A3: bootstrap, start-up load, pixel-perfect camera, URP, Input System, profiles.</summary>
    public class BootstrapTests
    {
        static string SettingsFolder => Path.Combine(Application.dataPath, "StreamingAssets", GameSettings.Folder);

        [TearDown]
        public void TearDown()
        {
            var root = GameObject.Find(Bootstrap.RootName);
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheStreamingSettingsLoadAtStartup()
        {
            var result = Startup.LoadSettings(new DirectorySettingsSource(SettingsFolder));
            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Errors));
            Assert.That(result.Settings.Grid.PixelsPerUnit, Is.GreaterThan(0f));
        }

        [Test]
        public void ABadFileStopsStartupWithEveryErrorListed()
        {
            var source = new MemorySettingsSource(new DirectorySettingsSource(SettingsFolder))
                .Set(GameSettings.PlayerFile, "{ not json")
                .Set(GameSettings.NamesFile, null);
            var result = Startup.LoadSettings(source);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Settings, Is.Null);
            Assert.That(result.Errors.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(string.Join("\n", result.Errors), Does.Contain(GameSettings.PlayerFile).And.Contain(GameSettings.NamesFile));
        }

        [Test]
        public void RunComposesNothingOnBadSettings()
        {
            var source = new MemorySettingsSource(new DirectorySettingsSource(SettingsFolder)).Set(GameSettings.GridFile, "[]");

            // What Startup itself reports for this fixture is also exactly what Stop must log, one error at a
            // time: this is the "stop with every error listed" behaviour the ticket asks for, not just a quiet
            // failure, so the test expects each line rather than swallowing them with ignoreFailingMessages.
            var expected = Startup.LoadSettings(source);
            Assert.That(expected.Succeeded, Is.False, "the fixture should actually be invalid");

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                $"[ProjectVania] The settings in {expected.Source} are invalid ({expected.Errors.Count} problem(s)); stopping.");
            foreach (var error in expected.Errors)
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error, $"[ProjectVania] {error}");
            }

            Assert.That(Bootstrap.Run(source), Is.Null);
            Assert.That(Bootstrap.Current, Is.Null);
            Assert.That(GameObject.Find(Bootstrap.RootName), Is.Null);
        }

        [Test]
        public void RunComposesTheGameInCodeAndConfiguresProfiles()
        {
            var host = Bootstrap.Run(new DirectorySettingsSource(SettingsFolder));
            Assert.That(host, Is.Not.Null);
            Assert.That(Bootstrap.Current, Is.SameAs(host));
            Assert.That(host.World, Is.Not.Null);
            Assert.That(host.World.Map, Is.Not.Null, "the world is built on the bridge level");
            Assert.That(host.World.Map.Columns, Is.EqualTo(118));
            Assert.That(host.World.Map.CellMatchesState(20, 0, Unity.Rea.ProjectVania.Core.World.CellState.Hard), Is.True);
            Assert.That(Profiles.IsConfigured, Is.True);
            Assert.That(Profiles.Current.Settings, Is.Not.Null);

            var pixelPerfect = host.GetComponentInChildren<PixelPerfectCamera>();
            var camera = pixelPerfect.GetComponent<Camera>();
            Assert.That(camera.orthographic, Is.True);
            Assert.That(camera.orthographicSize, Is.EqualTo(PixelPerfect.OrthographicSize(host.Settings.Grid.PixelsPerUnit)));

            var letterbox = host.GetComponentsInChildren<Camera>().Single(c => c != camera);
            Assert.That(letterbox.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(letterbox.backgroundColor, Is.EqualTo(Color.black));
            Assert.That(letterbox.cullingMask, Is.EqualTo(0));
            Assert.That(letterbox.depth, Is.LessThan(camera.depth), "the letterbox must clear before the game camera draws over it");

            var panel = host.GetComponentInChildren<PixelPerfectPanel>();
            var document = panel.GetComponent<UIDocument>();
            Assert.That(document.panelSettings.themeStyleSheet, Is.Not.Null);
            Assert.That(document.panelSettings.scaleMode, Is.EqualTo(PanelScaleMode.ConstantPixelSize));
            Assert.That(document.panelSettings.scale, Is.EqualTo(panel.Viewport.Scale > 0 ? panel.Viewport.Scale : panel.Viewport.Width / (float)PixelPerfect.ReferenceWidth));
            // The inline style values set by PixelPerfectPanel.Apply, not resolvedStyle: nothing has ticked a
            // layout pass in an edit-mode test, so resolvedStyle would still read the pre-layout defaults.
            // The panel's units are screen pixels over its scale, so the root is the 800 x 600 reference itself.
            var style = document.rootVisualElement.style;
            var scale = document.panelSettings.scale;
            Assert.That(style.width.value.value, Is.EqualTo(PixelPerfect.ReferenceWidth).Within(0.5f));
            Assert.That(style.height.value.value, Is.EqualTo(PixelPerfect.ReferenceHeight).Within(0.5f));
            Assert.That(style.left.value.value, Is.EqualTo(panel.Viewport.X / scale).Within(1e-3f));
        }

        [Test]
        public void ThePanelRootIsTheReferenceAtEveryMagnification()
        {
            var host = Bootstrap.Run(new DirectorySettingsSource(SettingsFolder));
            var panel = host.GetComponentInChildren<PixelPerfectPanel>();
            var style = panel.GetComponent<UIDocument>().rootVisualElement.style;

            // 1920 x 1200 holds the reference twice over (1600 x 1200), 160 screen pixels in from the left.
            panel.Apply(1920, 1200);
            Assert.That(panel.Viewport.Scale, Is.EqualTo(2));
            Assert.That(style.left.value.value, Is.EqualTo(80f));
            Assert.That(style.top.value.value, Is.EqualTo(0f));
            Assert.That(style.width.value.value, Is.EqualTo(800f));
            Assert.That(style.height.value.value, Is.EqualTo(600f));
        }

        [Test]
        public void ComposingTwiceLeavesOneGame()
        {
            var settings = Startup.LoadSettings(new DirectorySettingsSource(SettingsFolder)).Settings;
            Bootstrap.Compose(settings);
            Bootstrap.Compose(settings);
            Assert.That(Object.FindObjectsByType<GameHost>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        [Test]
        public void ThePixelPerfectCameraLetterboxesToAWholeScale()
        {
            var go = new GameObject("pp", typeof(Camera), typeof(PixelPerfectCamera));
            try
            {
                var pp = go.GetComponent<PixelPerfectCamera>();
                pp.PixelsPerUnit = 16f;
                pp.Apply(1920, 1080);
                var rect = go.GetComponent<Camera>().rect;
                Assert.That(pp.Viewport.Scale, Is.EqualTo(1));
                Assert.That(rect.width, Is.EqualTo(800f / 1920f).Within(1e-6f));
                Assert.That(rect.height, Is.EqualTo(600f / 1080f).Within(1e-6f));
                Assert.That(rect.x, Is.EqualTo(560f / 1920f).Within(1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TheRuntimeAssemblyDoesNotReferenceTheRenderPipeline()
        {
            var refs = typeof(Bootstrap).Assembly.GetReferencedAssemblies();
            Assert.That(System.Array.Exists(refs, a => a.Name.StartsWith("Unity.RenderPipelines")), Is.False);
        }

        [Test]
        public void TheUrp2DPipelineIsActiveOnEveryQualityLevel()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RenderPipelineBuilder.PipelinePath);
            Assert.That(pipeline, Is.Not.Null, "run ProjectBuilder.Ensure to commit the pipeline asset");
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.SameAs(pipeline));
            var current = QualitySettings.GetQualityLevel();
            try
            {
                var aa = pipeline.msaaSampleCount > 1 ? pipeline.msaaSampleCount : 0;
                for (var level = 0; level < QualitySettings.count; level++)
                {
                    QualitySettings.SetQualityLevel(level, false);
                    Assert.That(QualitySettings.renderPipeline, Is.SameAs(pipeline), QualitySettings.names[level]);
                    Assert.That(QualitySettings.antiAliasing, Is.EqualTo(aa), QualitySettings.names[level]);
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(current, false);
            }
        }

        [Test]
        public void EnsureIsIdempotent()
        {
            var before = File.ReadAllText(RenderPipelineBuilder.PipelinePath);
            ProjectBuilder.Ensure();
            Assert.That(File.ReadAllText(RenderPipelineBuilder.PipelinePath), Is.EqualTo(before));
            // The local game first, then the online scene (U.VAN-T31).
            Assert.That(EditorBuildSettings.scenes.Select(s => s.path), Is.EqualTo(ProjectBuilder.Scenes()));
            Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(ProjectBuilder.ScenePath));
        }

        [Test]
        public void TheInputSystemIsTheActiveBackend()
        {
            Assert.That(UnityEngine.InputSystem.InputSystem.settings, Is.Not.Null);
            var projectSettings = File.ReadAllText("ProjectSettings/ProjectSettings.asset");
            Assert.That(projectSettings, Does.Contain("activeInputHandler: 1"));
        }

        [Test]
        public void TheDisplayIs800By600()
        {
            Assert.That(PlayerSettings.defaultScreenWidth, Is.EqualTo(PixelPerfect.ReferenceWidth));
            Assert.That(PlayerSettings.defaultScreenHeight, Is.EqualTo(PixelPerfect.ReferenceHeight));
        }
    }
}
