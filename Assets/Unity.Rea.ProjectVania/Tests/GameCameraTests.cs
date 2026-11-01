using System.IO;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.World;
using UnityEngine;

namespace Unity.Rea.ProjectVania.Tests
{
    /// <summary>
    /// The camera on screen (U.VAN-US-A9, RECONSTRUCTION 7): the game camera shows exactly what the original's matrix
    /// (scale cubed, turned about the world origin, translated) put on the 800 x 600 screen, zoomed, turned or not, and
    /// the names scale and turn with it, as the original drew them through the same matrix.
    /// </summary>
    public class GameCameraTests
    {
        static string SettingsFolder => Path.Combine(Application.dataPath, "StreamingAssets", GameSettings.Folder);

        GameHost _host;
        Camera _camera;
        GameCamera _game;

        [SetUp]
        public void SetUp()
        {
            _host = Bootstrap.Run(new DirectorySettingsSource(SettingsFolder));
            Assert.That(_host, Is.Not.Null);
            _game = _host.GetComponentInChildren<GameCamera>();
            _camera = _game.GetComponent<Camera>();
        }

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
        public void TheGameCameraShowsPlayerOnesCoreCamera()
        {
            Assert.That(_game.Player, Is.EqualTo(PlayerId.One));
            Assert.That(_host.World.Camera(PlayerId.One), Is.Not.Null);
            Assert.That(_host.World.Camera(PlayerId.One).Mode, Is.EqualTo(CameraMode.LoosePlayer));
        }

        [TestCase(0, 0)]
        [TestCase(20, 0)]
        [TestCase(-30, 0)]
        [TestCase(0, 25)]
        [TestCase(15, -40)]
        [TestCase(-150, 10)]
        public void TheViewIsTheOriginalsMatrix(int zoomSteps, int turnSteps)
        {
            Run(200);
            Nudge(zoomSteps >= 0 ? CameraNudge.ZoomIn : CameraNudge.ZoomOut, Mathf.Abs(zoomSteps));
            Nudge(turnSteps >= 0 ? CameraNudge.RotateRight : CameraNudge.RotateLeft, Mathf.Abs(turnSteps));
            _game.Present();

            // What is drawn is the camera interpolated by the world's Alpha, as the characters are.
            var core = _host.World.Camera(PlayerId.One).ViewAt((float)_host.World.Alpha);
            var ppu = _host.Settings.Grid.PixelsPerUnit;
            var points = new[] { new PixelVector(0f, 0f), new PixelVector(500f, 480f), new PixelVector(820f, 130f), new PixelVector(1500f, 1100f) };
            foreach (var point in points)
            {
                var expected = core.ToScreen(point);
                var viewport = _camera.WorldToViewportPoint(CharacterView.ToUnity(point, ppu));
                var x = viewport.x * 800f;
                var y = (1f - viewport.y) * 600f;
                Assert.That(x, Is.EqualTo(expected.X).Within(0.05f), $"{point} x at zoom {zoomSteps}, turn {turnSteps}");
                Assert.That(y, Is.EqualTo(expected.Y).Within(0.05f), $"{point} y at zoom {zoomSteps}, turn {turnSteps}");
            }
        }

        [Test]
        public void TheNamesScaleAndTurnWithTheCamera()
        {
            Run(200);
            Nudge(CameraNudge.ZoomIn, 10);
            Nudge(CameraNudge.RotateRight, 30);
            _game.Present();
            var tags = _host.GetComponentInChildren<NameTags>();
            tags.Place();

            var core = _host.World.Camera(PlayerId.One).ViewAt((float)_host.World.Alpha);
            var label = tags.Labels[1];
            Assert.That(label.style.scale.value.value.x, Is.EqualTo(core.Magnification).Within(1e-4f));
            Assert.That(label.style.rotate.value.angle.ToDegrees(), Is.EqualTo(core.Rotation * Mathf.Rad2Deg).Within(1e-3f));

            // The anchor is the lifted top-centre of the text, in the world, through the matrix.
            var feet = _host.World.Characters[0].PositionAt((float)_host.World.Alpha);
            var tag = _host.Settings.Characters.NameTag;
            var expected = core.ToScreen(new PixelVector(feet.X, feet.Y - tag.TopAlignLift));
            Assert.That(label.style.left.value.value, Is.EqualTo(expected.X).Within(0.05f));
            Assert.That(label.style.top.value.value, Is.EqualTo(expected.Y).Within(0.05f));
        }

        void Run(int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                _host.World.Advance(1.0 / 60);
            }

            Assert.That(_host.World.Alpha, Is.EqualTo(0.0));
        }

        void Nudge(CameraNudge nudge, int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                _host.World.Submit(new NudgeCamera(PlayerId.One, nudge));
                _host.World.Advance(1.0 / 60);
            }
        }
    }
}
