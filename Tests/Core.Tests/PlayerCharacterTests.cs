using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Characters;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    /// <summary>
    /// The character's physics and per-frame state machine (U.VAN-US-A6, RECONSTRUCTION 4.2-4.7, 4.9). Each test replays
    /// a traced frame sequence of the original's <c>Player.Update</c> through the world: the step's commands (what the
    /// original's InputManager called that frame), then the update, and checks the state frame by frame. Expected
    /// figures are the original's arithmetic in single precision with XNA's 0.0166667 s frame.
    /// </summary>
    public class PlayerCharacterTests
    {
        const double Frame = 1.0 / 60;
        static readonly float Dt = FixedStepper.OriginalElapsedSeconds;
        static readonly PlayerId P1 = PlayerId.One;

        static GameSettings Settings() => GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));

        /// <summary>The shipped world: the bridge level and a Morris at each spawn position, (500, 100), (550, 100) and (450, 100).</summary>
        static GameWorld Bridge()
        {
            var s = Settings();
            return new GameWorld(Map.FromSettings(s.Grid), s.Player, s.Characters.Find("Morris").UnitSize);
        }

        static GameWorld World(Map map)
        {
            var s = Settings();
            return new GameWorld(map, s.Player, s.Characters.Find("Morris").UnitSize);
        }

        static readonly GridCell Hard = new GridCell(CellState.Hard, 255, new TileRect(16, 0, 16, 16));

        /// <summary>An open map with only the row-30 floor (feet rest at y = 480).</summary>
        static Map FloorOnly()
        {
            var map = new Map(1900, 1200, 16, new GridCell(CellState.Empty, 64, new TileRect(0, 0, 16, 16)));
            for (int x = 0; x < map.Columns; x++) map.Set(x, 30, Hard);
            return map;
        }

        /// <summary>One step: submit the commands (in the order the original's handler issued them), then advance 1/60 s.</summary>
        static void Step(GameWorld w, params ICommand[] commands)
        {
            foreach (var c in commands) w.Submit(c);
            Assert.That(w.Advance(Frame), Is.EqualTo(1));
        }

        static ICommand Left => new MoveCharacter(P1, -1);
        static ICommand Right => new MoveCharacter(P1, 1);
        static ICommand Idle => new StandCharacter(P1);
        static ICommand Down => new KneelCharacter(P1);
        static ICommand Jump => new JumpCharacter(P1);

        /// <summary>Steps with no direction held until the character is on the ground and still.</summary>
        static int Settle(GameWorld w, int max = 200)
        {
            var c = w.Characters[0];
            for (int i = 1; i <= max; i++)
            {
                Step(w, Idle);
                if (c.Terrain == TerrainState.OnGround && c.Velocity.Y == 0 && c.Action != ActionKind.Falling) return i;
            }

            throw new AssertionException("never landed");
        }

        // ---- Spawning, falling and the terminal-speed landing ----

        [Test]
        public void ACharacterSpawnsFallingInTheAirAtEachSpawnPosition()
        {
            var w = Bridge();
            Assert.That(w.Characters.Count, Is.EqualTo(3), "the original's two and Leo's (U.VAN-T25)");
            Assert.That(w.Characters[0].Position.X, Is.EqualTo(500f));
            Assert.That(w.Characters[0].Position.Y, Is.EqualTo(100f));
            Assert.That(w.Characters[1].Position.X, Is.EqualTo(550f));
            Assert.That(w.Characters[2].Position.X, Is.EqualTo(450f));
            foreach (var c in w.Characters)
            {
                Assert.That(c.Action, Is.EqualTo(ActionKind.Falling));
                Assert.That(c.Terrain, Is.EqualTo(TerrainState.InAir));
                Assert.That(c.Facing, Is.EqualTo(0));
                Assert.That(c.UnitHeight, Is.EqualTo(3));
            }
        }

        [Test]
        public void Fall_Adds10PerFrameAfterMovingAndClampsAt500OnFrame50()
        {
            var w = Bridge();
            var c = w.Characters[0];
            float y = 100f, vy = 0f;
            for (int frame = 1; frame <= 60; frame++)
            {
                Step(w, Idle);

                // The position moves by the speed the frame started with; gravity is added after (4.3).
                y += vy * Dt;
                vy = Math.Min(vy + 10f, 500f);
                Assert.That(c.Position.Y, Is.EqualTo(y), $"frame {frame}");
                Assert.That(c.Velocity.Y, Is.EqualTo(vy), $"frame {frame}");
                Assert.That(c.Action, Is.EqualTo(ActionKind.Falling));
                Assert.That(c.Velocity.Y == 500f, Is.EqualTo(frame >= 50), $"frame {frame}");
            }

            Assert.That(c.FallTime, Is.EqualTo(60 * Dt).Within(1e-4f));
        }

        [Test]
        public void TerminalSpeedLanding_KneelsOnTheFloorThenStandsOnRelease()
        {
            var w = Bridge();
            var c = w.Characters[0];
            int frame = 0;
            while (c.Terrain == TerrainState.InAir)
            {
                Step(w, Idle);
                frame++;
                Assert.That(frame, Is.LessThan(200));
            }

            // 380 px from y = 100: at 500 px/s since frame 50, so the landing is a terminal-speed one.
            Assert.That(c.Action, Is.EqualTo(ActionKind.Kneeling));
            Assert.That(c.Terrain, Is.EqualTo(TerrainState.OnGround));
            Assert.That(c.Velocity.Y, Is.EqualTo(0f));
            Assert.That(c.Position.Y, Is.GreaterThanOrEqualTo(480f).And.LessThan(481f));

            // Fixed (defect 3, owner decision 2026-10-23): with nothing held, the next idle step stands him up.
            Step(w, Idle);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Standing));
            for (int i = 0; i < 29; i++) Step(w, Idle);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Standing));
            Assert.That(c.Position.Y, Is.EqualTo(480f));

            // The idle twin lands and stands up the same way, without being driven.
            Assert.That(w.Characters[1].Action, Is.EqualTo(ActionKind.Standing));
        }

        [Test]
        public void SlowLanding_Stands()
        {
            var w = Bridge();
            var c = w.Characters[0];
            w.Submit(new ResetGame(P1));
            Settle(w);
            c.Reset(new PixelVector(500, 440));
            Settle(w);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Standing));
            Assert.That(c.Position.Y, Is.EqualTo(480f));
        }

        [Test]
        public void TerminalSpeedLandingWhileMoving_StaysFallingOnTheGroundForOneFrame()
        {
            // Defect 7, kept: Initiate_Kneel neither kneels (moving) nor slides (at 500), so the action stays Falling.
            var w = World(FloorOnly());
            var c = w.Characters[0];
            var trace = new List<(ActionKind action, TerrainState terrain)>();
            while (trace.Count < 200 && c.Action != ActionKind.Running)
            {
                Step(w, Right);
                trace.Add((c.Action, c.Terrain));
            }

            int landed = trace.FindIndex(t => t.terrain == TerrainState.OnGround);
            Assert.That(trace[landed], Is.EqualTo((ActionKind.Falling, TerrainState.OnGround)));
            Assert.That(trace[landed + 1], Is.EqualTo((ActionKind.Running, TerrainState.OnGround)));
            Assert.That(trace.Count, Is.EqualTo(landed + 2));
        }

        // ---- Running ----

        [Test]
        public void Run_IsASteady116PxPerSecondOnTheGround()
        {
            var w = World(FloorOnly());
            var c = w.Characters[0];
            Settle(w);
            float x = c.Position.X;
            for (int frame = 1; frame <= 30; frame++)
            {
                Step(w, Right);

                // +233.3 clamps to the 200 cap, then the 0.58 ground drag: 116 from the first frame.
                float vx = 200f * 0.58f;
                x += vx * Dt;
                Assert.That(c.Action, Is.EqualTo(ActionKind.Running));
                Assert.That(c.Velocity.X, Is.EqualTo(vx), $"frame {frame}");
                Assert.That(c.Position.X, Is.EqualTo(x), $"frame {frame}");
                Assert.That(c.Facing, Is.EqualTo(1));
            }

            Assert.That(c.Velocity.X, Is.EqualTo(116f).Within(1e-3f));

            // Letting go stands at once (Command_Stand, then the idle test), and drag stops him.
            Step(w, Idle);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Standing));
            Assert.That(c.Velocity.X, Is.EqualTo(116f * 0.58f).Within(1e-3f));
        }

        [Test]
        public void Run_IsASteady130PxPerSecondInTheAir()
        {
            var w = World(FloorOnly());
            var c = w.Characters[0];
            for (int frame = 1; frame <= 20; frame++)
            {
                Step(w, Left);
                Assert.That(c.Terrain, Is.EqualTo(TerrainState.InAir));
                Assert.That(c.Velocity.X, Is.EqualTo(-200f * 0.65f), $"frame {frame}");
            }
        }

        // ---- Jumping ----

        static List<float> JumpTrace(bool holdJump)
        {
            var w = World(FloorOnly());
            var c = w.Characters[0];
            Settle(w);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Kneeling));
            Step(w, Right); // up from the kneel
            Step(w, Idle);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Standing));

            var ys = new List<float>();
            Step(w, Idle, Jump);
            ys.Add(c.Position.Y);
            while (c.Action != ActionKind.Standing)
            {
                if (holdJump) Step(w, Idle, Jump);
                else Step(w, Idle);
                ys.Add(c.Position.Y);
                Assert.That(ys.Count, Is.LessThan(300));
            }

            return ys;
        }

        [Test]
        public void Jump_FollowsTheFixedPowerCurveFor35FramesFallsOnThe36thAndRisesAbout76Px()
        {
            var w = World(FloorOnly());
            var c = w.Characters[0];
            Settle(w);
            Step(w, Right);
            Step(w, Idle);
            Assert.That(c.Position.Y, Is.EqualTo(480f));

            Step(w, Idle, Jump);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Jumping));
            Assert.That(c.IsJumping, Is.True);

            // A ground jump stays OnGround for its whole rise; the curve is applied after the move, so it moves a frame later.
            float t = 0f, y = 480f, vy = 0f;
            int rise = 0;
            for (int frame = 1; frame < 200; frame++)
            {
                y += vy * Dt;
                t += Dt;
                if (t > 0.6f) break;
                vy = -1100f * (1f - (float)Math.Pow(t / 0.6f, 0.14f));
                rise++;
                Assert.That(c.Terrain, Is.EqualTo(TerrainState.OnGround), $"frame {frame}");
                Assert.That(c.JumpTime, Is.EqualTo(t), $"frame {frame}");
                Assert.That(c.Velocity.Y, Is.EqualTo(vy), $"frame {frame}");
                Assert.That(c.Position.Y, Is.EqualTo(y).Within(1e-3f), $"frame {frame}");
                Step(w, Idle);
            }

            // 35 frames of curve (the press frame is the first); on the 36th the jump time, 36 x 0.0166667, is past 0.6.
            Assert.That(rise, Is.EqualTo(35));

            // Past 0.6 s: Falling in the air, and the apex reset the jump time so the next jump can start.
            Assert.That(c.Action, Is.EqualTo(ActionKind.Falling));
            Assert.That(c.Terrain, Is.EqualTo(TerrainState.InAir));
            Assert.That(c.JumpTime, Is.EqualTo(0f));
            Assert.That(480f - c.Position.Y, Is.EqualTo(76f).Within(1f));
        }

        [Test]
        public void Jump_IsNotShortenedByReleasingTheKeyAndLandsStanding()
        {
            var tapped = JumpTrace(holdJump: false);
            var held = JumpTrace(holdJump: true);
            Assert.That(tapped, Is.EqualTo(held));

            float apex = float.MaxValue;
            foreach (var y in tapped) apex = Math.Min(apex, y);
            Assert.That(480f - apex, Is.EqualTo(76f).Within(1f));
            Assert.That(tapped[tapped.Count - 1], Is.EqualTo(480f));
        }

        [Test]
        public void Jump_InTheAirIsCancelledByTheFallInTheSameFrame()
        {
            // Initiate_Jump accepts it (not Jumping, jumpTime 0), but the InAir branch re-runs Initiate_Fall before the
            // jump's curve is ever applied.
            var w = World(FloorOnly());
            var c = w.Characters[0];
            Step(w, Idle, Jump);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Falling));
            Assert.That(c.IsJumping, Is.False);
            Assert.That(c.JumpTime, Is.EqualTo(0f));
            Assert.That(c.Velocity.Y, Is.EqualTo(10f));
        }

        [Test]
        public void Jump_HitsACeilingFourCellsAboveAndFalls()
        {
            var map = FloorOnly();
            for (int x = 0; x < map.Columns; x++) map.Set(x, 24, Hard);
            var w = World(map);
            var c = w.Characters[0];
            c.Reset(new PixelVector(500, 470));
            Settle(w);
            Step(w, Idle, Jump);
            int frames = 0;
            while (c.Action == ActionKind.Jumping)
            {
                Step(w, Idle);
                Assert.That(++frames, Is.LessThan(40));
            }

            // The cell (y - 48) / 16 reached row 24: snapped to (24 + 4) * 16 = 448 with the rise stopped.
            Assert.That(c.Action, Is.EqualTo(ActionKind.Falling));
            Assert.That(c.Position.Y, Is.EqualTo(448f));
            Assert.That(c.Velocity.Y, Is.EqualTo(10f));
        }

        // ---- Kneeling and sliding ----

        [Test]
        public void Slide_OutOfARunHoldsAt81PxPerSecondShrinksToOneCellAndStandsOnRelease()
        {
            var w = World(FloorOnly());
            var c = w.Characters[0];
            Settle(w);
            for (int i = 0; i < 10; i++) Step(w, Right);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Running));

            // Down alone keeps movement (Command_Kneel does not reset it): a moving kneel is a slide.
            for (int frame = 1; frame <= 20; frame++)
            {
                Step(w, Down);
                Assert.That(c.Action, Is.EqualTo(ActionKind.Sliding));
                Assert.That(c.Movement, Is.EqualTo(1));
                Assert.That(c.Velocity.X, Is.EqualTo(200f * 0.58f * 0.7f), $"frame {frame}");
                Assert.That(c.UnitWidth, Is.EqualTo(3));
                Assert.That(c.UnitHeight, Is.EqualTo(1));
            }

            // Released: movement 0, and the idle test stands him (a slide is not a kneel). Defect 2 fixed: back to 1 x 3.
            Step(w, Idle);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Standing));
            Assert.That(c.UnitWidth, Is.EqualTo(1));
            Assert.That(c.UnitHeight, Is.EqualTo(3));
        }

        [Test]
        public void Kneel_WhenStillKneelsAndStandsAsSoonAsSIsReleased()
        {
            // Fixed (defect 3, owner decision 2026-10-23, player.json kneel.releaseStandsUp): the original held the
            // kneel until a move or a jump; the rebuild now stands him up the next idle step instead.
            var w = World(FloorOnly());
            var c = w.Characters[0];
            Settle(w);
            Step(w, Right);
            Step(w, Idle);
            Step(w, Down);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Kneeling));
            Step(w, Idle);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Standing));
            Step(w, Left);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Running));
            Assert.That(c.Facing, Is.EqualTo(-1));
        }

        [Test]
        public void KneelingJump_LaunchesTheSlideWithoutTrappingTheCharacter()
        {
            // Defect 1 fixed: the slide jump leaves IsJumping clear, so the ground checks apply and he can stand and run.
            var w = World(FloorOnly());
            var c = w.Characters[0];
            Settle(w);
            Step(w, Right);
            Step(w, Idle);
            Step(w, Down);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Kneeling));
            float x = c.Position.X;

            Step(w, Idle, Jump);
            Assert.That(c.IsJumping, Is.False);
            Assert.That(c.JumpTime, Is.EqualTo(0f));
            Assert.That(c.Position.X, Is.GreaterThan(x));
            Assert.That(c.Action, Is.EqualTo(ActionKind.Standing));

            Step(w, Right);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Running));
            Step(w, Idle);
            Step(w, Idle, Jump);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Jumping));
        }

        // ---- Walls ----

        /// <summary>A character holding left in the air just right of the bridge's column-20 wall (its right face is x = 336).</summary>
        static (GameWorld w, PlayerCharacter c) OnLeftWall(float y)
        {
            var w = Bridge();
            var c = w.Characters[0];
            c.Reset(new PixelVector(350, y));
            int frames = 0;
            while (c.Action != ActionKind.WallSliding)
            {
                Step(w, Left);
                Assert.That(++frames, Is.LessThan(60), "never reached the wall");
            }

            return (w, c);
        }

        [Test]
        public void WallSlide_StartsInTheAirHoldingTowardTheWallAndCapsTheFallAt83PxPerSecond()
        {
            var (w, c) = OnLeftWall(150);
            Assert.That(c.Terrain, Is.EqualTo(TerrainState.OnWall));
            Assert.That(c.WallContact, Is.EqualTo(3));
            Assert.That(c.Velocity.X, Is.EqualTo(0f));

            float cap = 500f / 6f;
            for (int frame = 1; frame <= 30; frame++)
            {
                float before = c.Velocity.Y;
                float x = c.Position.X;
                Step(w, Left);
                float expected = before > cap ? before * 0.97f : cap;
                Assert.That(c.Velocity.Y, Is.EqualTo(expected), $"frame {frame}");
                Assert.That(c.Position.X, Is.EqualTo(x), $"frame {frame}");
                Assert.That(c.Action, Is.EqualTo(ActionKind.WallSliding));
            }

            Assert.That(c.Velocity.Y, Is.EqualTo(83.3333f).Within(1e-3f));

            // Sliding down to the floor lands him standing (Action_Stand clears movement that frame), then running again.
            for (int i = 0; i < 400 && c.Action == ActionKind.WallSliding; i++) Step(w, Left);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Standing));
            Assert.That(c.Position.Y, Is.EqualTo(480f));
            Step(w, Left);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Running));
            Assert.That(c.Position.Y, Is.EqualTo(480f));
        }

        [Test]
        public void WallSlide_FromAFastFallDecaysByThreePercentPerFrame()
        {
            var w = Bridge();
            var c = w.Characters[0];
            c.Reset(new PixelVector(350, 0));
            for (int i = 0; i < 40; i++) Step(w, Idle);
            Assert.That(c.Velocity.Y, Is.EqualTo(400f));
            for (int i = 0; i < 10 && c.Action != ActionKind.WallSliding; i++) Step(w, Left);
            Assert.That(c.Action, Is.EqualTo(ActionKind.WallSliding));
            float v = c.Velocity.Y;
            Assert.That(v, Is.GreaterThan(83.4f));
            Step(w, Left);
            Assert.That(c.Velocity.Y, Is.EqualTo(v * 0.97f));
        }

        [Test]
        public void WallSlide_DropsAfterOneIdleSecondOnTheWall()
        {
            var (w, c) = OnLeftWall(100);

            // The timer counts while idle on the wall and falls once it is no longer under 1.0 s.
            float t = 0f;
            int counting = 0;
            while (t < 1.0f)
            {
                t += Dt;
                counting++;
            }

            for (int frame = 1; frame <= counting; frame++)
            {
                Step(w, Idle);
                Assert.That(c.Action, Is.EqualTo(ActionKind.WallSliding), $"frame {frame}");
                Assert.That(c.WallSlideTime, Is.EqualTo(frame * Dt).Within(1e-4f));
            }

            Step(w, Idle);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Falling));
            Assert.That(c.Terrain, Is.EqualTo(TerrainState.InAir));
            Assert.That(c.WallSlideTime, Is.EqualTo(0f));
            Assert.That(c.Facing, Is.EqualTo(-1));
            Assert.That(counting, Is.EqualTo(60).Or.EqualTo(61));

            // Idle in the air beside the wall does not reattach.
            Step(w, Idle);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Falling));
        }

        [Test]
        public void WallSlide_TurningAwayFlipsFacingBackAndFalls()
        {
            var (w, c) = OnLeftWall(100);
            Step(w, Right);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Falling));
            Assert.That(c.Terrain, Is.EqualTo(TerrainState.InAir));
            Assert.That(c.Facing, Is.EqualTo(-1));
        }

        [Test]
        public void WallJump_PushesOffTheWallAtOnceThenKeepsTheOriginalJumpCurve()
        {
            // Defect 6 fixed (BIBLE, U.VAN-T24, player.json's wallJump.pushOff/pushSpeed): the original held the
            // horizontal speed at 0 for as long as a full-height wall was touched, so against a wall this tall a wall
            // jump only ever climbed - contact never broke on its own. Now it kicks away at once and keeps doing so
            // every frame contact lasts, while the vertical jump curve (RECONSTRUCTION 4.6/4.9) is untouched.
            var (w, c) = OnLeftWall(300);
            float x0 = c.Position.X;
            float y0 = c.Position.Y;
            Assert.That(c.Facing, Is.EqualTo(-1));

            Step(w, Left, Jump);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Jumping));
            Assert.That(c.Terrain, Is.EqualTo(TerrainState.OnWall));
            Assert.That(c.IsWallJumping, Is.True);

            // Still full contact this first frame (the position has not moved yet), so the push applies at once
            // instead of the original's zero. It goes through the same air drag (0.65) as any other horizontal
            // speed, so player.json's 200 px/s pushSpeed settles at 130: the same speed a run settles to in the air
            // (U.VAN-T9).
            Assert.That(c.WallContact, Is.EqualTo(3));
            Assert.That(c.Velocity.X, Is.EqualTo(200f * 0.65f).Within(1e-3f));
            Assert.That(c.Position.X, Is.GreaterThan(x0));

            int frames = 1;
            float apex = c.Position.Y;
            bool sawContactBreak = false;
            while (c.Action == ActionKind.Jumping)
            {
                Step(w, Left);
                frames++;
                apex = Math.Min(apex, c.Position.Y);

                // The push-off is not a drop off the wall: Facing never flips mid-jump, and the jump keeps rising
                // (via ActionJump, which keeps setting IsWallJumping while Terrain reads OnWall) even once contact is
                // actually lost, ending only when ActionJump's own timer calls Fall(), exactly as before the fix.
                Assert.That(c.Facing, Is.EqualTo(-1), $"frame {frames}");
                if (c.WallContact == 0) sawContactBreak = true;
                if (c.Action == ActionKind.Jumping) Assert.That(c.IsWallJumping, Is.True);
            }

            Assert.That(frames, Is.EqualTo(36));
            Assert.That(y0 - apex, Is.EqualTo(76f).Within(2f));
            Assert.That(c.Action, Is.EqualTo(ActionKind.Falling));
            Assert.That(c.IsWallJumping, Is.False);

            // Unlike the original (defect 6), the character actually left the wall during the climb...
            Assert.That(sawContactBreak, Is.True);
            Assert.That(c.Position.X, Is.GreaterThan(x0 + 10f));

            // ...so holding back toward the wall afterward does not reattach at once: he is out past it now.
            Step(w, Left);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Falling));
        }

        [Test]
        public void WallJump_WithPushOffDisabledStillClimbsStraightUpAsTheOriginalDid()
        {
            // player.json's wallJump.pushOff is a data slot (U.VAN-T24): with it off, the original's defect-6
            // behaviour (held at 0 for as long as a full-height wall is touched, so the wall jump only ever climbs)
            // is unchanged.
            var shipped = new DirectorySettingsSource(GameSettingsTests.ShippedFolder);
            var patched = new MemorySettingsSource(shipped).Set(GameSettings.PlayerFile,
                shipped.Read(GameSettings.PlayerFile).Replace("\"pushOff\": true", "\"pushOff\": false"));
            var s = GameSettings.Load(patched);
            var w = new GameWorld(Map.FromSettings(s.Grid), s.Player, s.Characters.Find("Morris").UnitSize);
            var c = w.Characters[0];
            c.Reset(new PixelVector(350, 300));
            int frames = 0;
            while (c.Action != ActionKind.WallSliding)
            {
                Step(w, Left);
                Assert.That(++frames, Is.LessThan(60), "never reached the wall");
            }

            float x = c.Position.X;
            Step(w, Left, Jump);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Jumping));
            Assert.That(c.Velocity.X, Is.EqualTo(0f));

            while (c.Action == ActionKind.Jumping)
            {
                Step(w, Left);
                Assert.That(c.Position.X, Is.EqualTo(x));
            }

            Assert.That(c.Action, Is.EqualTo(ActionKind.Falling));
        }

        [Test]
        public void GroundJump_DoesNotCatchAWallWhileRising()
        {
            // The rise of a ground jump stays OnGround, so the InAir wall test is not reached until the apex.
            var w = Bridge();
            var c = w.Characters[0];
            c.Reset(new PixelVector(350, 470));
            Settle(w);
            Step(w, Left, Jump);
            for (int i = 0; i < 20; i++)
            {
                Step(w, Left);
                Assert.That(c.Action, Is.EqualTo(ActionKind.Jumping));
            }
        }

        // ---- Bounds and reset ----

        [Test]
        public void Bounds_ClampToTheMapAndZeroTheClampedAxis()
        {
            var w = World(FloorOnly());
            var c = w.Characters[0];
            c.Reset(new PixelVector(1, 300));

            // CheckBounds runs before the move (4.3), so a frame can end up to one move past the edge; the next frame
            // clamps it back to 0 with the speed zeroed, and so on.
            Step(w, Left);
            Assert.That(c.Position.X, Is.EqualTo(1f - 130f * Dt));
            Step(w, Left);
            Assert.That(c.Position.X, Is.EqualTo(0f));
            Assert.That(c.Velocity.X, Is.EqualTo(0f));
            Step(w, Left);
            Assert.That(c.Position.X, Is.EqualTo(-130f * Dt));
            Step(w, Left);
            Assert.That(c.Position.X, Is.EqualTo(0f));
        }

        [Test]
        public void Reset_PutsEveryCharacterAt500By100FallingAndKeepsTheirMovement()
        {
            var w = World(FloorOnly());
            Settle(w);
            for (int i = 0; i < 10; i++) Step(w, Right);
            Step(w, Right, new ResetGame(P1));
            foreach (var c in w.Characters)
            {
                Assert.That(c.Action, Is.EqualTo(ActionKind.Falling));
                Assert.That(c.Terrain, Is.EqualTo(TerrainState.InAir));
            }

            // The reset happens before the step's update, so the driven one has moved one air frame from (500, 100).
            var p = w.Characters[0];
            Assert.That(p.Position.Y, Is.EqualTo(100f));
            Assert.That(p.Position.X, Is.EqualTo(500f + 200f * 0.65f * Dt));
            Assert.That(p.Velocity.X, Is.EqualTo(200f * 0.65f));
            Assert.That(p.Movement, Is.EqualTo(1));
            Assert.That(w.Characters[1].Position.X, Is.EqualTo(500f));
        }

        // ---- The world's part ----

        [Test]
        public void CharactersFreezeOutsidePlayAndOnlyTheDrivenOneTakesCommands()
        {
            var w = Bridge();
            Step(w, new SetGameState(P1, GameState.MainMenu));
            Assert.That(w.Characters[0].Position.Y, Is.EqualTo(100f));
            Step(w);
            Assert.That(w.Characters[0].Position.Y, Is.EqualTo(100f));
            Step(w, new SetGameState(P1, GameState.Play), Right);
            Assert.That(w.Characters[0].Movement, Is.EqualTo(1));
            Assert.That(w.Characters[1].Movement, Is.EqualTo(0));
            Assert.That(w.Driven(new PlayerId(1)), Is.SameAs(w.Characters[1]));
            Assert.That(w.Driven(new PlayerId(5)), Is.Null);
        }

        [Test]
        public void AnActionChangeIsAnEvent()
        {
            var w = World(FloorOnly());
            Settle(w);
            Step(w, Right);
            Assert.That(w.Events, Has.Some.Matches<IGameEvent>(e =>
                e is CharacterActionChanged a && a.Character == 0 && a.From == ActionKind.Kneeling && a.To == ActionKind.Running));
            Step(w, Right);
            Assert.That(w.Events, Has.None.InstanceOf<CharacterActionChanged>());
        }

        [Test]
        public void OriginalElapsedIsXnasDefaultFrame()
        {
            Assert.That(FixedStepper.OriginalElapsedSeconds, Is.EqualTo(0.0166667f));
        }
    }
}
