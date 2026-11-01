using System;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Characters;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    /// <summary>
    /// Owner decision 2026-10-23 (U.VAN-T25): Morris, Shanoa and Leo are all spawned and all playable, [ / ] (L1 / R1)
    /// cycle between the three, Shanoa has Morris's whole move set, Leo stands, runs, jumps and falls only, and grid.json's
    /// block structures can be climbed by him from the floor to the top ledge with nothing but runs and jumps.
    /// </summary>
    public class PlayableCharactersTests
    {
        const double Frame = 1.0 / 60;
        static readonly PlayerId P1 = PlayerId.One;

        static GameSettings Settings() => GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));

        static GameWorld Shipped(int seed = 7)
        {
            var s = Settings();
            return new GameWorld(Map.FromSettings(s.Grid), s.Player, s.Characters.Roster, s.Names, new Random(seed), s.Camera,
                audio: s.Audio, animations: s.Animations);
        }

        static void Step(GameWorld w, params ICommand[] commands)
        {
            foreach (var c in commands) w.Submit(c);
            Assert.That(w.Advance(Frame), Is.EqualTo(1));
        }

        static ICommand Move(int direction) => new MoveCharacter(P1, direction);
        static ICommand Idle => new StandCharacter(P1);
        static ICommand Down => new KneelCharacter(P1);
        static ICommand Jump => new JumpCharacter(P1);

        /// <summary>Everyone falls and lands on the floor with nothing held.</summary>
        static GameWorld Landed()
        {
            var w = Shipped();
            for (var i = 0; i < 200 && w.Characters.Any(c => c.Action != ActionKind.Standing); i++) Step(w, Idle);
            Assert.That(w.Characters.All(c => c.Action == ActionKind.Standing), "everyone lands and stands");
            return w;
        }

        /// <summary>Drives the character <paramref name="index"/> (from Morris, with ] presses).</summary>
        static PlayerCharacter Drive(GameWorld w, int index)
        {
            for (var i = 0; i < index; i++) Step(w, new CycleCharacter(P1, 1));
            Assert.That(w.DrivenIndex(P1), Is.EqualTo(index));
            return w.Driven(P1);
        }

        [Test]
        public void TheRosterIsMorrisShanoaAndLeoEachAtItsSpawnPosition()
        {
            var w = Shipped();
            Assert.That(w.Characters.Select(c => c.Definition.Name), Is.EqualTo(new[] { "Morris", "Shanoa", "Leo" }));
            Assert.That(w.Characters.Select(c => c.Position.X), Is.EqualTo(new[] { 500f, 550f, 450f }));
            Assert.That(w.Characters.Select(c => c.UnitHeight), Is.EqualTo(new[] { 3, 3, 2 }));
            Assert.That(w.Camera(new PlayerId(2)), Is.Not.Null, "each character's player has a camera");

            // Every one falls straight down to the floor: the structures leave their columns clear.
            w = Landed();
            Assert.That(w.Characters.Select(c => c.Position.Y), Is.EqualTo(new[] { 480f, 480f, 480f }));
            Assert.That(w.Characters.Select(c => c.Position.X), Is.EqualTo(new[] { 500f, 550f, 450f }));
        }

        [Test]
        public void TheBracketsCycleThroughAllThreeAndWrapBothWays()
        {
            var w = Landed();
            Assert.That(w.DrivenIndex(P1), Is.EqualTo(0));
            foreach (var expected in new[] { 1, 2, 0 })
            {
                Step(w, new CycleCharacter(P1, 1));
                Assert.That(w.DrivenIndex(P1), Is.EqualTo(expected), "] goes right and wraps to Morris");
            }

            foreach (var expected in new[] { 2, 1, 0 })
            {
                Step(w, new CycleCharacter(P1, -1));
                Assert.That(w.DrivenIndex(P1), Is.EqualTo(expected), "[ wraps to Leo and goes left");
            }

            Assert.That(w.Events.OfType<DrivenCharacterChanged>().Single().To, Is.EqualTo(0));
        }

        [Test]
        public void TheControlsMoveOnlyTheDrivenCharacterAndOneLeftMidRunStops()
        {
            var w = Landed();
            var leo = Drive(w, 2);
            for (var i = 0; i < 20; i++) Step(w, Move(-1));
            Assert.That(leo.Position.X, Is.LessThan(440f));
            Assert.That(w.Characters[0].Position.X, Is.EqualTo(500f), "Morris stays where he stood");
            Assert.That(w.Characters[1].Position.X, Is.EqualTo(550f), "Shanoa stays where she stood");

            // Switched away from mid-run, Leo lets go and stops; Morris (] wraps) now runs.
            Step(w, new CycleCharacter(P1, 1));
            for (var i = 0; i < 20; i++) Step(w, Move(1));
            Assert.That(leo.Movement, Is.EqualTo(0));
            Assert.That(leo.Action, Is.EqualTo(ActionKind.Standing));
            Assert.That(w.Characters[0].Position.X, Is.GreaterThan(510f));
        }

        [Test]
        public void ShanoaHasMorrissWholeMoveSet()
        {
            var w = Landed();
            var shanoa = Drive(w, 1);
            Assert.That(Enum.GetValues(typeof(ActionKind)).Cast<ActionKind>().Where(shanoa.Can),
                Is.EquivalentTo(Enum.GetValues(typeof(ActionKind)).Cast<ActionKind>().Where(w.Characters[0].Can)));

            Step(w, Down);
            Assert.That(shanoa.Action, Is.EqualTo(ActionKind.Kneeling));
            for (var i = 0; i < 10; i++) Step(w, Idle);
            Assert.That(shanoa.Action, Is.EqualTo(ActionKind.Standing), "releasing down stands her up");

            Step(w, Move(1));
            Assert.That(shanoa.Action, Is.EqualTo(ActionKind.Running));
            Step(w, Move(1), Down);
            Assert.That(shanoa.Action, Is.EqualTo(ActionKind.Sliding), "down while running slides");
            for (var i = 0; i < 10; i++) Step(w, Idle);

            Step(w, Jump);
            Assert.That(shanoa.Action, Is.EqualTo(ActionKind.Jumping));
            var top = shanoa.Position.Y;
            for (var i = 0; i < 40; i++)
            {
                Step(w, Idle);
                top = Math.Min(top, shanoa.Position.Y);
            }

            Assert.That(480f - top, Is.EqualTo(76f).Within(2f), "the full jump");
            for (var i = 0; i < 60 && shanoa.Action != ActionKind.Standing; i++) Step(w, Idle);

            // Run left at the column-20 wall and jump along it: she wall slides down it.
            var slid = false;
            for (var i = 0; i < 200 && !slid; i++)
            {
                Step(w, Move(-1));
                if (shanoa.WallContact > 0 && shanoa.Action == ActionKind.Running) Step(w, Move(-1), Jump);
                slid = shanoa.Action == ActionKind.WallSliding;
            }

            Assert.That(slid, "she wall slides");
        }

        [Test]
        public void LeoStandsRunsJumpsAndFallsAndNeverKneelsSlidesOrWallSlides()
        {
            var w = Landed();
            var leo = Drive(w, 2);
            Assert.That(Enum.GetValues(typeof(ActionKind)).Cast<ActionKind>().Where(leo.Can),
                Is.EquivalentTo(new[] { ActionKind.Standing, ActionKind.Running, ActionKind.Jumping, ActionKind.Falling }));

            Step(w, Down);
            Assert.That(leo.Action, Is.EqualTo(ActionKind.Standing), "down does not kneel him");
            Step(w, Move(-1));
            Step(w, Move(-1), Down);
            Assert.That(leo.Action, Is.EqualTo(ActionKind.Running), "down does not slide him");

            Step(w, Move(-1), Jump);
            Assert.That(leo.Action, Is.EqualTo(ActionKind.Jumping));

            // Into the column-20 wall and down it: he falls past it and lands standing, never wall sliding.
            for (var i = 0; i < 300 && !(leo.WallContact > 0 && leo.Action == ActionKind.Standing); i++)
            {
                Step(w, Move(-1));
                Assert.That(leo.Action, Is.Not.EqualTo(ActionKind.WallSliding));
                if (leo.Action == ActionKind.Running && leo.WallContact > 0) Step(w, Move(-1), Jump);
            }

            Assert.That(leo.Position.Y, Is.EqualTo(480f));
            for (var i = 0; i < 3; i++) Step(w, Move(-1));
            Assert.That(leo.Action, Is.EqualTo(ActionKind.Running).Or.EqualTo(ActionKind.Standing), "pressed against the wall on the floor");
        }

        [Test]
        public void LeoClimbsTheStructuresFromTheFloorToTheTopLedge()
        {
            var w = Landed();
            var leo = Drive(w, 2);
            var grid = Settings().Grid;
            Assert.That(grid.Blocks.Select(b => b.Name), Is.EqualTo(new[] { "low step", "high step", "ledge 1", "ledge 2", "ledge 3", "ledge 4" }));
            foreach (var block in grid.Blocks)
            {
                Hop(w, leo, block);
            }

            Assert.That(leo.Position.Y, Is.EqualTo(12 * 16f), "on the top ledge, 18 cells above the floor");
        }

        /// <summary>
        /// From where <paramref name="c"/> stands, runs to the edge of its footing toward <paramref name="target"/> (or
        /// until a wall stops it), then jumps toward it, letting go once over it, and checks it lands on its top.
        /// </summary>
        static void Hop(GameWorld w, PlayerCharacter c, BlockData target)
        {
            var map = w.Map;
            float left = target.From.X * 16f, right = (target.To.X + 1) * 16f;
            int dir = Math.Sign((left + right) / 2f - c.Position.X);

            // Run toward the target while the ground ahead holds and nothing blocks the way.
            for (var i = 0; i < 400; i++)
            {
                var ahead = new PixelVector(c.Position.X + dir * 10f, c.Position.Y);
                if (c.WallContact > 0 || !map.CheckFloorCollision(ref ahead)) break;
                Step(w, Move(dir));
            }

            for (var i = 0; i < 10; i++) Step(w, Idle);
            Assert.That(c.Action, Is.EqualTo(ActionKind.Standing), $"ready to jump for {target.Name}");

            Step(w, Move(dir), Jump);
            for (var i = 0; i < 300; i++)
            {
                var over = c.Position.X > left + 3f && c.Position.X < right - 3f;
                Step(w, over ? Idle : Move(dir));
                if (c.Action == ActionKind.Standing) break;
            }

            Assert.That(c.Action, Is.EqualTo(ActionKind.Standing), $"landed after jumping for {target.Name}");
            Assert.That(c.Position.Y, Is.EqualTo(target.From.Y * 16f), $"on top of {target.Name}");
            Assert.That(c.Position.X, Is.InRange(left, right), $"on {target.Name}");
        }
    }
}
