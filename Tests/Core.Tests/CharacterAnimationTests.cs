using System;
using System.Collections.Generic;
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
    /// The characters' animation, names and the two players (U.VAN-US-A7, RECONSTRUCTION 4.1, 4.8): the original's
    /// AnimationPlayer clock, the strip each action plays, the draw flip, the random names and [ / ] choosing the driven one.
    /// </summary>
    public class CharacterAnimationTests
    {
        const double Frame = 1.0 / 60;
        static readonly PlayerId P1 = PlayerId.One;

        static GameSettings Settings() => GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));

        static GameWorld Shipped(int seed = 1)
        {
            var s = Settings();
            return new GameWorld(Map.FromSettings(s.Grid), s.Player, s.Characters.Find(s.Characters.DefaultCharacter), s.Names, new Random(seed));
        }

        static AnimationSheet Sheet(string name) => Settings().Animations.Find(name);

        // ---- the animation table (4.8) ----

        [Test]
        public void TheShippedSheetsAreTheOriginalsStripsAndFrameTimes()
        {
            var morris = Sheet("morris");
            var expected = new (string Name, int Frames, float Time, bool Loop)[]
            {
                ("idle", 4, 1.0f, true), ("run", 15, 0.05f, true), ("jump", 3, 0.2f, false), ("fall", 1, 1.0f, true),
                ("wallslide", 1, 0.07f, true), ("kneel", 4, 0.07f, false), ("slide", 4, 0.07f, false),
            };
            Assert.That(morris.Animations.Select(a => (a.Name, a.Frames, a.FrameTime, a.Loop)), Is.EqualTo(expected));

            Assert.That(morris.Animations.All(a => !a.Borrowed && a.Strip == a.Name));

            // The original's own Shanoa and Leo strips; U.VAN-T25's other clips borrow their run frames.
            Assert.That(Sheet("shanoa").Animations.Where(a => !a.Borrowed).Select(a => (a.Name, a.Frames, a.FrameTime, a.Loop)),
                Is.EqualTo(new[] { ("idle", 10, 0.1f, true), ("run", 10, 0.1f, true) }));
            Assert.That(Sheet("leo").Animations.Where(a => !a.Borrowed).Select(a => (a.Name, a.Frames, a.FrameTime, a.Loop)),
                Is.EqualTo(new[] { ("idle", 4, 0.1f, true), ("run", 4, 0.1f, true) }));
        }

        [Test]
        public void ShanoaAndLeoBorrowRunFramesForTheStripsTheOriginalLacks()
        {
            var shanoa = Sheet("shanoa");
            Assert.That(shanoa.Animations.Where(a => a.Borrowed).Select(a => (a.Name, a.Strip, string.Join(",", a.FrameIndices), a.FrameTime, a.Loop)),
                Is.EqualTo(new[]
                {
                    ("jump", "run", "2,3", 0.2f, false), ("fall", "run", "3", 1.0f, true), ("wallslide", "run", "9", 0.07f, true),
                    ("kneel", "run", "4", 0.07f, false), ("slide", "run", "5", 0.07f, false),
                }));
            var leo = Sheet("leo");
            Assert.That(leo.Animations.Where(a => a.Borrowed).Select(a => (a.Name, a.Strip, string.Join(",", a.FrameIndices), a.FrameTime, a.Loop)),
                Is.EqualTo(new[] { ("jump", "run", "0,2", 0.1f, false), ("fall", "run", "2", 1.0f, true) }));

            // A borrowed clip plays its own frame count; its frames name the strip's.
            var animator = new CharacterAnimator(leo);
            animator.Play(leo.Find("jump"));
            animator.Advance(0.5f);
            Assert.That(animator.Frame, Is.EqualTo(1), "a one-shot holds its last frame");
            Assert.That(animator.Clip.SourceFrame(animator.Frame), Is.EqualTo(2));
        }

        [Test]
        public void ABorrowedClipMustNameAStripOfItsSheetAndFramesItHas()
        {
            Assert.That(GameSettingsTests.LoadFails(GameSettingsTests.Patched(GameSettings.AnimationsFile,
                t => t.Replace("\"strip\": \"run\", \"frameIndices\": [0, 2]", "\"strip\": \"run\", \"frameIndices\": [0, 4]"))).Errors.Single(),
                Does.Contain("borrows frame 4 of 'run', which has 4"));
            Assert.That(GameSettingsTests.LoadFails(GameSettingsTests.Patched(GameSettings.AnimationsFile,
                t => t.Replace("\"name\": \"fall\", \"strip\": \"run\", \"frameIndices\": [2]", "\"name\": \"fall\", \"strip\": \"jump\", \"frameIndices\": [1]"))).Errors.Single(),
                Does.Contain("not a strip of its own"));
        }

        [Test]
        public void ALoopingStripStepsAFramePerFrameTimeAndWraps()
        {
            var animator = new CharacterAnimator(Sheet("morris"));
            animator.Play(animator.Sheet.Find("run"));
            Assert.That(animator.Frame, Is.EqualTo(0));

            // The clock steps only once it is past a whole frame time (while time > frameTime).
            animator.Advance(0.03f);
            Assert.That(animator.Frame, Is.EqualTo(0), "0.03 s is not yet past 0.05 s");
            animator.Advance(0.03f);
            Assert.That(animator.Frame, Is.EqualTo(1));

            animator.Advance(0.05f * 14);
            Assert.That(animator.Frame, Is.EqualTo(0), "15 frames wrap to the first");
        }

        [Test]
        public void AOneShotStripHoldsItsLastFrame()
        {
            var animator = new CharacterAnimator(Sheet("morris"));
            animator.Play(animator.Sheet.Find("jump"));
            animator.Advance(10f);
            Assert.That(animator.Frame, Is.EqualTo(2));
        }

        [Test]
        public void PlayingTheSameStripDoesNotRestartIt()
        {
            var animator = new CharacterAnimator(Sheet("morris"));
            var run = animator.Sheet.Find("run");
            animator.Play(run);
            animator.Advance(0.12f);
            Assert.That(animator.Frame, Is.EqualTo(2));
            animator.Play(run);
            Assert.That(animator.Frame, Is.EqualTo(2));
            animator.Play(animator.Sheet.Find("idle"));
            Assert.That(animator.Frame, Is.EqualTo(0));
            Assert.That(animator.Clip.Name, Is.EqualTo("idle"));
        }

        [Test]
        public void EachActionPlaysItsOwnStrip()
        {
            var w = Shipped();
            var morris = w.Characters[0];
            var animator = new CharacterAnimator(Sheet("morris"));

            animator.Select(morris);
            Assert.That(animator.Clip.Name, Is.EqualTo("fall"), "spawned falling");

            AdvanceUntil(w, () => morris.Action == ActionKind.Standing);
            animator.Select(morris);
            Assert.That(animator.Clip.Name, Is.EqualTo("idle"));

            w.Submit(new MoveCharacter(P1, 1));
            w.Advance(Frame);
            animator.Select(morris);
            Assert.That(animator.Clip.Name, Is.EqualTo("run"));

            w.Submit(new KneelCharacter(P1));
            w.Advance(Frame);
            Assert.That(morris.Action, Is.EqualTo(ActionKind.Sliding));
            animator.Select(morris);
            Assert.That(animator.Clip.Name, Is.EqualTo("slide"));

            AdvanceUntil(w, () => morris.Action == ActionKind.Standing);
            w.Submit(new KneelCharacter(P1));
            w.Advance(Frame);
            Assert.That(morris.Action, Is.EqualTo(ActionKind.Kneeling));
            animator.Select(morris);
            Assert.That(animator.Clip.Name, Is.EqualTo("kneel"));

            AdvanceUntil(w, () => morris.Action == ActionKind.Standing);
            w.Submit(new JumpCharacter(P1));
            w.Advance(Frame);
            Assert.That(morris.Action, Is.EqualTo(ActionKind.Jumping));
            animator.Select(morris);
            Assert.That(animator.Clip.Name, Is.EqualTo("jump"));
        }

        [Test]
        public void AWallSlidePlaysTheWallSlideStrip()
        {
            var w = Shipped();
            var morris = w.Characters[0];
            var animator = new CharacterAnimator(Sheet("morris"));

            // The bridge's wall is column 40 (x 640..656); run right at it from the floor, jump, keep holding right.
            AdvanceUntil(w, () => morris.Action == ActionKind.Standing);
            for (int i = 0; i < 600 && morris.Action != ActionKind.WallSliding; i++)
            {
                w.Submit(new MoveCharacter(P1, 1));
                if (morris.WallContact > 0) w.Submit(new JumpCharacter(P1));
                w.Advance(Frame);
            }

            Assert.That(morris.Action, Is.EqualTo(ActionKind.WallSliding));
            animator.Select(morris);
            Assert.That(animator.Clip.Name, Is.EqualTo("wallslide"));
        }

        [Test]
        public void ASheetWithoutTheStripPlaysIdleInsteadOfCrashing()
        {
            // Leo's sheet has no kneel strip (his move set has none, U.VAN-T25); a Morris kneeling drawn from it plays idle.
            var w = Shipped();
            var leo = new CharacterAnimator(Sheet("leo"));
            leo.Select(w.Characters[0]);
            Assert.That(w.Characters[0].Action, Is.EqualTo(ActionKind.Falling));
            Assert.That(leo.Clip.Name, Is.EqualTo("fall"));

            AdvanceUntil(w, () => w.Characters[0].Action == ActionKind.Kneeling);
            leo.Select(w.Characters[0]);
            Assert.That(leo.Clip.Name, Is.EqualTo("idle"));

            w.Submit(new MoveCharacter(P1, 1));
            AdvanceUntil(w, () => w.Characters[0].Action == ActionKind.Running, keepMoving: true);
            leo.Select(w.Characters[0]);
            Assert.That(leo.Clip.Name, Is.EqualTo("run"));
        }

        // ---- the draw (4.8) ----

        [Test]
        public void TheLeftFacingArtFlipsWhenFacingRightAndAWallJumpInvertsIt()
        {
            var w = Shipped();
            var morris = w.Characters[0];
            Assert.That(morris.Facing, Is.EqualTo(0));
            Assert.That(morris.Flipped, Is.False, "facing neither way does not flip");

            w.Submit(new MoveCharacter(P1, 1));
            w.Advance(Frame);
            Assert.That(morris.Flipped, Is.True);

            w.Submit(new MoveCharacter(P1, -1));
            w.Advance(Frame);
            Assert.That(morris.Flipped, Is.False);

            // Up against the bridge's right wall, then jump off it: the wall jump inverts the flip.
            AdvanceUntil(w, () => morris.Action == ActionKind.Standing);
            for (int i = 0; i < 600 && morris.Action != ActionKind.WallSliding; i++)
            {
                w.Submit(new MoveCharacter(P1, 1));
                if (morris.WallContact > 0) w.Submit(new JumpCharacter(P1));
                w.Advance(Frame);
            }

            w.Submit(new MoveCharacter(P1, 1));
            w.Submit(new JumpCharacter(P1));
            w.Advance(Frame);
            w.Submit(new MoveCharacter(P1, 1));
            w.Advance(Frame);
            Assert.That(morris.IsWallJumping, Is.True);
            Assert.That(morris.Facing, Is.EqualTo(1));
            Assert.That(morris.Flipped, Is.False, "facing right but wall jumping");
        }

        [Test]
        public void PreviousPositionIsWhereTheStepStartedAndHoldsWhileFrozen()
        {
            var w = Shipped();
            var morris = w.Characters[0];
            Assert.That(morris.PreviousPosition.Y, Is.EqualTo(100f));
            w.Advance(Frame);
            w.Advance(Frame);
            var before = morris.Position;
            w.Advance(Frame);
            Assert.That(morris.PreviousPosition.Y, Is.EqualTo(before.Y));
            Assert.That(morris.Position.Y, Is.GreaterThan(before.Y));

            w.Submit(new SetGameState(P1, GameState.MainMenu));
            w.Advance(Frame);
            Assert.That(morris.PreviousPosition.Y, Is.EqualTo(morris.Position.Y), "a frozen world does not move");
        }

        // ---- names (4.1) ----

        [Test]
        public void EveryCharacterIsGivenANameFromItsGendersTable()
        {
            var s = Settings();
            var w = Shipped(seed: 7);
            Assert.That(w.Characters, Has.Count.EqualTo(3));
            foreach (var c in w.Characters)
            {
                Assert.That(c.Definition.Name, Is.EqualTo("Morris"));
                Assert.That(s.Names.Male, Does.Contain(c.Name));
            }

            // The shipped roster (U.VAN-T25): Shanoa's name comes from the female table.
            var roster = new GameWorld(Map.FromSettings(s.Grid), s.Player, s.Characters.Roster, s.Names, new Random(7));
            Assert.That(s.Names.Male, Does.Contain(roster.Characters[0].Name));
            Assert.That(s.Names.Female, Does.Contain(roster.Characters[1].Name));
            Assert.That(s.Names.Male, Does.Contain(roster.Characters[2].Name));
        }

        [Test]
        public void TheSameSeedNamesTheSameAndAnotherSeedVaries()
        {
            Assert.That(Shipped(3).Characters.Select(c => c.Name), Is.EqualTo(Shipped(3).Characters.Select(c => c.Name)));
            var seen = new HashSet<string>(Enumerable.Range(0, 20).Select(seed => Shipped(seed).Characters[0].Name));
            Assert.That(seen.Count, Is.GreaterThan(1));
        }

        [Test]
        public void EveryNameCanBePickedIncludingTheLast()
        {
            // Defect 8 fixed: the original's Next(Length - 1) never returned the last name.
            var names = new[] { "Aaron", "Zane" };
            var random = new Random(0);
            var picked = new HashSet<string>(Enumerable.Range(0, 200).Select(_ => RandomName.Pick(names, random)));
            Assert.That(picked, Is.EquivalentTo(names));
            Assert.Throws<ArgumentException>(() => RandomName.Pick(new string[0], random));
        }

        [Test]
        public void TheNameTagIsTheOriginalsRedThenYellowWrite()
        {
            var tag = Settings().Characters.NameTag;
            Assert.That(tag.Font, Is.EqualTo("Arial"));
            Assert.That(tag.FontPoints, Is.EqualTo(10f));
            Assert.That(tag.TopAlignLift, Is.EqualTo(64f).Within(1e-4f));
            Assert.That(tag.Labels.Select(l => (l.Colour, l.Offset.X, l.Offset.Y)),
                Is.EqualTo(new[] { (SettingsColour.Red, -1f, -100f), (SettingsColour.Yellow, 0f, 0f) }));
        }

        // ---- two players, [ and ] (4) ----

        [Test]
        public void PlayerOneStartsOnTheFirstCharacterAndTheBracketsWrapBothWays()
        {
            var w = Shipped();
            Assert.That(w.DrivenIndex(P1), Is.EqualTo(0));

            w.Submit(new CycleCharacter(P1, 1));
            w.Advance(Frame);
            Assert.That(w.DrivenIndex(P1), Is.EqualTo(1));
            var e = w.Events.OfType<DrivenCharacterChanged>().Single();
            Assert.That((e.Player, e.From, e.To), Is.EqualTo((P1, 0, 1)));

            w.Submit(new CycleCharacter(P1, 1));
            w.Advance(Frame);
            Assert.That(w.DrivenIndex(P1), Is.EqualTo(2));

            w.Submit(new CycleCharacter(P1, 1));
            w.Advance(Frame);
            Assert.That(w.DrivenIndex(P1), Is.EqualTo(0), "right from the last wraps to the first");

            w.Submit(new CycleCharacter(P1, -1));
            w.Advance(Frame);
            Assert.That(w.DrivenIndex(P1), Is.EqualTo(2), "left from the first wraps to the last");
            Assert.That(w.Driven(P1), Is.SameAs(w.Characters[2]));
        }

        [Test]
        public void TheControlsMoveOnlyTheDrivenCharacter()
        {
            var w = Shipped();
            AdvanceUntil(w, () => w.Characters.All(c => c.Action == ActionKind.Standing));

            w.Submit(new CycleCharacter(P1, 1));
            w.Advance(Frame);
            for (int i = 0; i < 30; i++)
            {
                w.Submit(new MoveCharacter(P1, -1));
                w.Advance(Frame);
            }

            Assert.That(w.Characters[0].Position.X, Is.EqualTo(500f), "the twin stands still");
            Assert.That(w.Characters[1].Position.X, Is.LessThan(550f));
            Assert.That(w.Characters[1].Action, Is.EqualTo(ActionKind.Running));
        }

        [Test]
        public void TheCharacterSwitchedAwayFromLetsGoOfItsDirection()
        {
            // Defect 16 fixed: the original left the old character's movement held, so it ran on undriven.
            var w = Shipped();
            AdvanceUntil(w, () => w.Characters.All(c => c.Action == ActionKind.Standing));
            w.Submit(new MoveCharacter(P1, 1));
            w.Advance(Frame);
            w.Submit(new MoveCharacter(P1, 1));
            w.Submit(new CycleCharacter(P1, 1));
            w.Advance(Frame);
            Assert.That(w.Characters[0].Movement, Is.EqualTo(0));

            // The drag settles what speed it had; after that it stays put rather than running on.
            for (int i = 0; i < 120; i++) w.Advance(Frame);
            Assert.That(w.Characters[0].Action, Is.EqualTo(ActionKind.Standing));
            var x = w.Characters[0].Position.X;
            for (int i = 0; i < 60; i++) w.Advance(Frame);
            Assert.That(w.Characters[0].Position.X, Is.EqualTo(x).Within(0.01f));
        }

        [Test]
        public void ASecondPlayerCyclesOnItsOwn()
        {
            var w = Shipped();
            var p2 = new PlayerId(1);
            Assert.That(w.DrivenIndex(p2), Is.EqualTo(1));
            w.Submit(new CycleCharacter(p2, 1));
            w.Advance(Frame);
            Assert.That(w.DrivenIndex(p2), Is.EqualTo(2));
            w.Submit(new CycleCharacter(p2, 1));
            w.Advance(Frame);
            Assert.That(w.DrivenIndex(p2), Is.EqualTo(0));
            Assert.That(w.DrivenIndex(P1), Is.EqualTo(0), "player one is untouched");
            Assert.That(w.DrivenIndex(new PlayerId(5)), Is.EqualTo(-1));
            Assert.That(w.Driven(new PlayerId(5)), Is.Null);
        }

        static void AdvanceUntil(GameWorld w, Func<bool> done, bool keepMoving = false)
        {
            for (int i = 0; i < 600 && !done(); i++)
            {
                // As the router does: a direction held, or Command_Stand when none is.
                w.Submit(keepMoving ? (ICommand)new MoveCharacter(P1, 1) : new StandCharacter(P1));
                w.Advance(Frame);
            }

            Assert.That(done(), Is.True, "the condition was never reached");
        }
    }
}
