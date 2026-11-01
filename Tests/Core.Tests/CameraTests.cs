using System;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using Unity.Rea.ProjectVania.Core.Viewing;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    /// <summary>
    /// The camera (U.VAN-US-A9, RECONSTRUCTION 7): CameraManager's modes, its 0.02 pull and 0.7 damping replayed frame
    /// for frame against a literal copy of the original's arithmetic, the clamp of its centre to the map, the scroll,
    /// zoom (scale cubed) and rotate steps, the original's matrix, and the world applying the camera commands.
    /// </summary>
    public class CameraTests
    {
        const double Frame = 1.0 / 60;
        static readonly PlayerId P1 = PlayerId.One;

        static GameSettings Settings() => GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));

        static CameraData Data() => Settings().Camera;

        static Map ShippedMap() => Map.FromSettings(Settings().Grid);

        static GameWorld World(int seed = 1)
        {
            var s = Settings();
            return new GameWorld(Map.FromSettings(s.Grid), s.Player, s.Characters.Find(s.Characters.DefaultCharacter), s.Names, new Random(seed), s.Camera);
        }

        static void Steps(GameWorld world, int steps)
        {
            for (var i = 0; i < steps; i++) world.Advance(Frame);
        }

        /// <summary>CameraManager.cs's Update, MoveToLocation and CheckBounds, copied literally (floats, the 0.01 double literal).</summary>
        sealed class Original
        {
            public float PX = 1, PY = 1, VX, VY;
            public int Mode = 2; // 0 Free, 1 LockPlayer, 2 LoosePlayer
            public float MapWidth = 1900, MapHeight = 1200;

            public void Update(float tx, float ty)
            {
                if (Mode == 1) { PX = -tx; PY = -ty; }
                else if (Mode == 2)
                {
                    float run = -(Math.Abs(tx) - Math.Abs(PX));
                    float rise = -(Math.Abs(ty) - Math.Abs(PY));
                    VX += 0.02f * run;
                    VY += 0.02f * rise;
                }

                if (Math.Abs(VX) < 0.01) VX = 0;
                if (Math.Abs(VY) < 0.01) VY = 0;
                PX += VX;
                PY += VY;
                VX *= .7f;
                VY *= .7f;
                if (-(PX) < 0) { PX = 0; VX = 0.0f; }
                if (-(PY) < 0) { PY = 0; VY = 0.0f; }
                if (-(PX) > MapWidth) { PX = -MapWidth; VX = 0.0f; }
                if (-(PY) > MapHeight) { PY = -MapHeight; VY = 0.0f; }
            }
        }

        // ---- the camera alone ----

        [Test]
        public void StartsAsCameraManagerInitialize()
        {
            var camera = new WorldCamera(Data());
            Assert.That(camera.Position.X, Is.EqualTo(1f));
            Assert.That(camera.Position.Y, Is.EqualTo(1f));
            Assert.That(camera.Rotation, Is.EqualTo(0f));
            Assert.That(camera.Scale, Is.EqualTo(1f));
            Assert.That(camera.Mode, Is.EqualTo(CameraMode.LoosePlayer));
            Assert.That(camera.Velocity.X, Is.EqualTo(0f));
        }

        [Test]
        public void LooseModeEasesExactlyAsTheOriginalFrameForFrame()
        {
            var camera = new WorldCamera(Data());
            var original = new Original();
            var map = ShippedMap();
            var targets = new[] { (500f, 100f), (500f, 580f), (1200f, 480f), (30f, 20f), (1850f, 1150f) };
            var frame = 0;
            foreach (var (tx, ty) in targets)
            {
                for (var i = 0; i < 240; i++, frame++)
                {
                    camera.BeginStep();
                    camera.Step(new PixelVector(tx, ty), map);
                    original.Update(tx, ty);
                    Assert.That(camera.Position.X, Is.EqualTo(original.PX), $"frame {frame} x");
                    Assert.That(camera.Position.Y, Is.EqualTo(original.PY), $"frame {frame} y");
                    Assert.That(camera.Velocity.X, Is.EqualTo(original.VX), $"frame {frame} vx");
                    Assert.That(camera.Velocity.Y, Is.EqualTo(original.VY), $"frame {frame} vy");
                }

                Assert.That(camera.Centre.X, Is.EqualTo(tx).Within(0.5f), "settles on the target");
                Assert.That(camera.Centre.Y, Is.EqualTo(ty).Within(0.5f));
            }
        }

        [Test]
        public void LooseModeIsADampedLagNotASnap()
        {
            var camera = new WorldCamera(Data());
            var map = ShippedMap();
            camera.BeginStep();
            camera.Step(new PixelVector(500f, 100f), map);
            // One frame: velocity 0.02 * 499 = 9.98 moves the centre to about 11, nowhere near 500.
            Assert.That(camera.Centre.X, Is.EqualTo(-1f + 9.98f).Within(1e-3f));
            Assert.That(camera.Velocity.X, Is.EqualTo(-9.98f * 0.7f).Within(1e-3f));
        }

        [Test]
        public void LockModeSitsOnTheTargetEveryStep()
        {
            var camera = new WorldCamera(Data());
            camera.SetMode(CameraMode.LockPlayer);
            camera.BeginStep();
            camera.Step(new PixelVector(500f, 100f), ShippedMap());
            Assert.That(camera.Position.X, Is.EqualTo(-500f));
            Assert.That(camera.Position.Y, Is.EqualTo(-100f));
        }

        [Test]
        public void FreeModeIgnoresTheTargetAndScrollIsAnAccelerationTheDampingSettles()
        {
            var camera = new WorldCamera(Data());
            var map = ShippedMap();
            camera.SetMode(CameraMode.Free);
            // Start the centre inside the map, then hold Delete (scroll left) for one step, then let go.
            camera.Nudge(CameraNudge.ScrollRight);
            for (var i = 0; i < 100; i++)
            {
                camera.Nudge(CameraNudge.ScrollRight);
                camera.BeginStep();
                camera.Step(new PixelVector(900f, 900f), map);
            }

            var before = camera.Centre;
            Assert.That(before.Y, Is.EqualTo(0f), "Free never follows the target");
            camera.Nudge(CameraNudge.ScrollLeft);
            camera.BeginStep();
            camera.Step(null, map);
            // ScrollRight held: velocity settles at -1 / 0.3; the left press adds +1.
            var settled = -1f / (1f - 0.7f);
            Assert.That(camera.Centre.X - before.X, Is.EqualTo(-(settled * 0.7f + 1f)).Within(1e-2f));
        }

        [Test]
        public void TheCentreIsClampedToTheMapAndTheVelocityZeroedOnAClampedAxis()
        {
            var camera = new WorldCamera(Data());
            var map = ShippedMap();
            camera.SetMode(CameraMode.LockPlayer);
            camera.BeginStep();
            camera.Step(new PixelVector(5000f, -40f), map);
            Assert.That(camera.Centre.X, Is.EqualTo(1900f));
            Assert.That(camera.Centre.Y, Is.EqualTo(0f));

            camera.SetMode(CameraMode.Free);
            for (var i = 0; i < 30; i++) camera.Nudge(CameraNudge.ScrollRight);
            camera.BeginStep();
            camera.Step(null, map);
            Assert.That(camera.Centre.X, Is.EqualTo(1900f));
            Assert.That(camera.Velocity.X, Is.EqualTo(0f));
        }

        [TestCase(CameraNudge.ScrollLeft, 1f, 0f)]
        [TestCase(CameraNudge.ScrollRight, -1f, 0f)]
        [TestCase(CameraNudge.ScrollUp, 0f, 1f)]
        [TestCase(CameraNudge.ScrollDown, 0f, -1f)]
        public void EachScrollKeyAddsOneToTheVelocityWithTheOriginalsSign(CameraNudge nudge, float vx, float vy)
        {
            var camera = new WorldCamera(Data());
            camera.Nudge(nudge);
            Assert.That(camera.Velocity.X, Is.EqualTo(vx));
            Assert.That(camera.Velocity.Y, Is.EqualTo(vy));
        }

        [Test]
        public void ZoomStepsTheScaleByAHundredthAndTheViewMagnifiesByItsCube()
        {
            var camera = new WorldCamera(Data());
            for (var i = 0; i < 10; i++) camera.Nudge(CameraNudge.ZoomIn);
            var s = 1f;
            for (var i = 0; i < 10; i++) s += 0.01f;
            Assert.That(camera.Scale, Is.EqualTo(s));
            Assert.That(camera.Magnification, Is.EqualTo(s * s * s));

            for (var i = 0; i < 120; i++) camera.Nudge(CameraNudge.ZoomOut);
            Assert.That(camera.Scale, Is.LessThan(0f), "no clamp: the original's zoom out goes through zero");
            Assert.That(camera.Magnification, Is.LessThan(0f), "a negative scale's cube stays negative, turning the view round");
        }

        [Test]
        public void RotateLeftTurnsMinusAHundredthAndRotateRightPlus()
        {
            var camera = new WorldCamera(Data());
            camera.Nudge(CameraNudge.RotateLeft);
            Assert.That(camera.Rotation, Is.EqualTo(-0.01f));
            camera.Nudge(CameraNudge.RotateRight);
            camera.Nudge(CameraNudge.RotateRight);
            Assert.That(camera.Rotation, Is.EqualTo(0.01f).Within(1e-7f));
        }

        [Test]
        public void TheViewIsTheOriginalsMatrixAboutTheWorldOrigin()
        {
            var position = new PixelVector(-500f, -100f);
            // Unzoomed and unturned, the centred world point is at the screen's centre.
            var at = WorldCamera.ToScreen(new PixelVector(500f, 100f), position, 0f, 1f);
            Assert.That(at.X, Is.EqualTo(400f));
            Assert.That(at.Y, Is.EqualTo(300f));

            // Scale(k) then RotationZ then Translation: the origin stays put, and a quarter turn takes +x to +y (down,
            // clockwise on screen).
            var origin = WorldCamera.ToScreen(new PixelVector(0f, 0f), position, 0.3f, 2f);
            Assert.That(origin.X, Is.EqualTo(400f - 500f).Within(1e-3f));
            Assert.That(origin.Y, Is.EqualTo(300f - 100f).Within(1e-3f));
            var turned = WorldCamera.ToScreen(new PixelVector(10f, 0f), position, (float)(Math.PI / 2), 2f);
            Assert.That(turned.X, Is.EqualTo(400f - 500f).Within(1e-3f));
            Assert.That(turned.Y, Is.EqualTo(300f - 100f + 20f).Within(1e-3f));
        }

        [TestCase(0f, 1f)]
        [TestCase(0.4f, 1f)]
        [TestCase(-1.2f, 1.3f)]
        [TestCase(0.25f, 0.7f)]
        [TestCase(2f, -0.5f)]
        public void TheViewCentreIsTheWorldPointTheMatrixPutsAtTheScreenCentre(float rotation, float scale)
        {
            var camera = new WorldCamera(Data());
            var map = ShippedMap();
            camera.SetMode(CameraMode.LockPlayer);
            camera.BeginStep();
            camera.Step(new PixelVector(700f, 450f), map);
            while (camera.Rotation < rotation - 0.005f) camera.Nudge(CameraNudge.RotateRight);
            while (camera.Rotation > rotation + 0.005f) camera.Nudge(CameraNudge.RotateLeft);
            while (camera.Scale < scale - 0.005f) camera.Nudge(CameraNudge.ZoomIn);
            while (camera.Scale > scale + 0.005f) camera.Nudge(CameraNudge.ZoomOut);

            var view = camera.ViewAt(1f);
            var centre = view.ToScreen(view.Centre);
            Assert.That(centre.X, Is.EqualTo(400f).Within(1e-2f));
            Assert.That(centre.Y, Is.EqualTo(300f).Within(1e-2f));
            Assert.That(view.Magnification, Is.EqualTo(camera.Magnification));
            Assert.That(view.Rotation, Is.EqualTo(camera.Rotation));
        }

        [Test]
        public void TheViewInterpolatesBetweenSteps()
        {
            var camera = new WorldCamera(Data());
            camera.SetMode(CameraMode.LockPlayer);
            camera.BeginStep();
            camera.Step(new PixelVector(500f, 100f), ShippedMap());
            camera.BeginStep();
            camera.Nudge(CameraNudge.ZoomIn);
            camera.Step(new PixelVector(520f, 100f), ShippedMap());
            var half = camera.ViewAt(0.5f);
            Assert.That(half.Position.X, Is.EqualTo(-510f).Within(1e-3f));
            Assert.That(half.Magnification, Is.EqualTo(1.005f * 1.005f * 1.005f).Within(1e-5f), "a zoom eases across the step too");
        }

        // ---- the world applying the camera commands ----

        [Test]
        public void TheWorldGivesEachCharactersPlayerACameraAndNoneWithoutCameraData()
        {
            var world = World();
            Assert.That(world.Camera(P1), Is.Not.Null);
            Assert.That(world.Camera(new PlayerId(1)), Is.Not.Null);
            Assert.That(world.Camera(new PlayerId(1)), Is.Not.SameAs(world.Camera(P1)));
            Assert.That(world.Camera(new PlayerId(2)), Is.Not.Null, "three spawn positions, three characters (U.VAN-T25)");
            Assert.That(world.Camera(new PlayerId(3)), Is.Null);
            Assert.That(world.Camera(default), Is.Null);

            var s = Settings();
            var bare = new GameWorld(Map.FromSettings(s.Grid), s.Player, s.Characters.Find(s.Characters.DefaultCharacter), s.Names, new Random(1));
            Assert.That(bare.Camera(P1), Is.Null);
            bare.Submit(new CycleCameraMode(P1));
            Assert.DoesNotThrow(() => bare.Advance(Frame));
        }

        [Test]
        public void TheCameraEasesOntoTheDrivenCharacterAfterItMoves()
        {
            var world = World();
            var original = new Original();
            for (var i = 0; i < 400; i++)
            {
                world.Advance(Frame);
                var feet = world.Driven(P1).Position;
                original.Update(feet.X, feet.Y); // the camera reads the position the characters were just moved to
                Assert.That(world.Camera(P1).Position.X, Is.EqualTo(original.PX), $"step {i}");
                Assert.That(world.Camera(P1).Position.Y, Is.EqualTo(original.PY), $"step {i}");
            }

            var centre = world.Camera(P1).Centre;
            Assert.That(centre.X, Is.EqualTo(world.Driven(P1).Position.X).Within(0.5f));
            Assert.That(centre.Y, Is.EqualTo(world.Driven(P1).Position.Y).Within(0.5f));

            world.Submit(new CycleCharacter(P1, 1));
            Steps(world, 400);
            Assert.That(world.Camera(P1).Centre.X, Is.EqualTo(world.Characters[1].Position.X).Within(0.5f), "[ / ] move the camera to the other character");
        }

        [Test]
        public void CCyclesFreeLockLooseCursorAndRoundAgain()
        {
            var world = World();
            var seen = new CameraMode[5];
            for (var i = 0; i < seen.Length; i++)
            {
                seen[i] = world.Camera(P1).Mode;
                world.Submit(new CycleCameraMode(P1));
                world.Advance(Frame);
            }

            Assert.That(seen, Is.EqualTo(new[] { CameraMode.LoosePlayer, CameraMode.LooseCursor, CameraMode.Free, CameraMode.LockPlayer, CameraMode.LoosePlayer }));
            Assert.That(world.Camera(new PlayerId(1)).Mode, Is.EqualTo(CameraMode.LoosePlayer), "only the issuing player's camera cycles");
        }

        [Test]
        public void LockModeKeepsTheDrivenCharacterAtTheCentre()
        {
            var world = World();
            world.Submit(new CycleCameraMode(P1));
            world.Submit(new CycleCameraMode(P1));
            world.Submit(new CycleCameraMode(P1));
            world.Advance(Frame);
            Assert.That(world.Camera(P1).Mode, Is.EqualTo(CameraMode.LockPlayer));
            for (var i = 0; i < 60; i++)
            {
                world.Submit(new MoveCharacter(P1, 1));
                world.Advance(Frame);
                var feet = world.Driven(P1).Position;
                Assert.That(world.Camera(P1).Centre.X, Is.EqualTo(feet.X).Within(1e-3f));
                Assert.That(world.Camera(P1).Centre.Y, Is.EqualTo(feet.Y).Within(1e-3f));
            }
        }

        [Test]
        public void TheEditorFreesTheCameraWithItsCursorOnThePlayerAndLeavingItLoosensItAgain()
        {
            var world = World();
            Steps(world, 120);
            var feet = world.Driven(P1).Position;
            world.Submit(new ToggleMapEditor(P1));
            world.Advance(Frame);
            Assert.That(world.State, Is.EqualTo(GameState.MapEditor));
            Assert.That(world.Camera(P1).Mode, Is.EqualTo(CameraMode.Free));
            Assert.That(world.EditorCursor(P1).X, Is.EqualTo(feet.X));
            Assert.That(world.EditorCursor(P1).Y, Is.EqualTo(feet.Y));

            world.Submit(new ToggleMapEditor(P1));
            world.Advance(Frame);
            Assert.That(world.State, Is.EqualTo(GameState.Play));
            Assert.That(world.Camera(P1).Mode, Is.EqualTo(CameraMode.LoosePlayer));
        }

        [Test]
        public void LooseCursorEasesTowardTheEditorCursor()
        {
            var world = World();
            Assert.That(world.EditorCursor(P1).X, Is.EqualTo(0f), "the cursor starts at (0, 0), as Cursor's constructor put it");
            Steps(world, 300);
            world.Submit(new CycleCameraMode(P1)); // LoosePlayer -> LooseCursor
            Steps(world, 400);
            Assert.That(world.Camera(P1).Mode, Is.EqualTo(CameraMode.LooseCursor));
            Assert.That(world.Camera(P1).Centre.X, Is.EqualTo(0f).Within(0.5f));
            Assert.That(world.Camera(P1).Centre.Y, Is.EqualTo(0f).Within(0.5f));
        }

        [Test]
        public void RPutsEveryCameraBackOnLoosePlayer()
        {
            var world = World();
            world.Submit(new CycleCameraMode(P1));
            world.Submit(new CycleCameraMode(new PlayerId(1)));
            world.Submit(new CycleCameraMode(new PlayerId(1)));
            world.Advance(Frame);
            world.Submit(new ResetGame(P1));
            world.Advance(Frame);
            Assert.That(world.Camera(P1).Mode, Is.EqualTo(CameraMode.LoosePlayer));
            Assert.That(world.Camera(new PlayerId(1)).Mode, Is.EqualTo(CameraMode.LoosePlayer));
        }

        [Test]
        public void TheCameraKeepsRunningWhileTheWorldIsFrozen()
        {
            var world = World();
            world.Submit(new ToggleMainMenu(P1));
            world.Advance(Frame);
            Assert.That(world.State, Is.EqualTo(GameState.MainMenu));
            var before = world.Camera(P1).Centre;
            Steps(world, 30);
            Assert.That(world.Camera(P1).Centre.X, Is.GreaterThan(before.X + 100f), "the camera still eases toward the frozen player");
        }

        [Test]
        public void TheCameraCommandsScrollZoomAndTurnThePlayersCamera()
        {
            var world = World();
            world.Submit(new NudgeCamera(P1, CameraNudge.ZoomIn));
            world.Submit(new NudgeCamera(P1, CameraNudge.RotateRight));
            world.Advance(Frame);
            Assert.That(world.Camera(P1).Scale, Is.EqualTo(1.01f));
            Assert.That(world.Camera(P1).Rotation, Is.EqualTo(0.01f));
            Assert.That(world.Camera(new PlayerId(1)).Scale, Is.EqualTo(1f));
        }

        [Test]
        public void AnEditorStepScrollsTheCameraByFourUnlessShift()
        {
            var world = World();
            world.Submit(new ToggleMapEditor(P1));
            Steps(world, 200);
            world.Submit(new StepEditor(P1, 1, 0, selectTile: true, moveCell: false));
            world.Advance(Frame);
            Assert.That(world.Camera(P1).Velocity.X, Is.EqualTo(0f), "Shift held: no scroll");

            var before = world.Camera(P1).Centre.X;
            world.Submit(new StepEditor(P1, 1, 0, selectTile: true, moveCell: true));
            world.Advance(Frame);
            Assert.That(world.Camera(P1).Centre.X - before, Is.EqualTo(4f).Within(1e-4f), "MoveCellRight: ScrollRight(4)");
            Assert.That(world.Camera(P1).Velocity.X, Is.EqualTo(-4f * 0.7f).Within(1e-4f));
        }
    }
}
