using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Simulation;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    public class GameWorldTests
    {
        const double Frame = 1.0 / 60;

        [Test]
        public void StartsInPlayAtTickZero()
        {
            var w = new GameWorld();
            Assert.That(w.State, Is.EqualTo(GameState.Play));
            Assert.That(w.Tick, Is.EqualTo(0));
        }

        [Test]
        public void StateChangesOnlyWhenAStepAppliesTheCommand()
        {
            var w = new GameWorld();
            w.Submit(new SetGameState(PlayerId.One, GameState.MainMenu));
            Assert.That(w.State, Is.EqualTo(GameState.Play));
            w.Advance(Frame);
            Assert.That(w.State, Is.EqualTo(GameState.MainMenu));
            Assert.That(w.Tick, Is.EqualTo(1));
        }

        [Test]
        public void ACommandProducesAnEventNamingItsPlayerAndTick()
        {
            var w = new GameWorld();
            w.Advance(Frame);
            w.Submit(new SetGameState(new PlayerId(1), GameState.MapEditor));
            w.Advance(Frame);
            var e = (GameStateChanged)w.Events[0];
            Assert.That(e.Player, Is.EqualTo(new PlayerId(1)));
            Assert.That(e.From, Is.EqualTo(GameState.Play));
            Assert.That(e.To, Is.EqualTo(GameState.MapEditor));
            Assert.That(e.Tick, Is.EqualTo(1));
        }

        [Test]
        public void ReenteringTheSameStateMakesNoEventAndEventsClearEachAdvance()
        {
            var w = new GameWorld();
            w.Submit(new SetGameState(PlayerId.One, GameState.Play));
            w.Advance(Frame);
            Assert.That(w.Events, Is.Empty);
            w.Submit(new SetGameState(PlayerId.One, GameState.Console));
            w.Advance(Frame);
            Assert.That(w.Events, Has.Count.EqualTo(1));
            w.Advance(Frame);
            Assert.That(w.Events, Is.Empty);
        }

        [Test]
        public void CommandsApplyInSubmissionOrder()
        {
            var w = new GameWorld();
            w.Submit(new SetGameState(PlayerId.One, GameState.MainMenu));
            w.Submit(new SetGameState(PlayerId.One, GameState.Play));
            w.Advance(Frame);
            Assert.That(w.State, Is.EqualTo(GameState.Play));
            Assert.That(w.Events, Has.Count.EqualTo(2));
        }

        [Test]
        public void WithoutAStepTheCommandWaits()
        {
            var w = new GameWorld();
            w.Submit(new SetGameState(PlayerId.One, GameState.MainMenu));
            w.Advance(0.001);
            Assert.That(w.PendingCommands, Is.EqualTo(1));
            Assert.That(w.State, Is.EqualTo(GameState.Play));
        }

        [Test]
        public void NullCommandIsRejected()
        {
            Assert.Throws<System.ArgumentNullException>(() => new GameWorld().Submit(null));
        }

        [Test]
        public void ACommandWithNoPlayerIsRejected()
        {
            Assert.Throws<System.ArgumentException>(() => new GameWorld().Submit(default(SetGameState)));
            Assert.That(default(PlayerId).IsValid, Is.False);
            Assert.That(PlayerId.One.Value, Is.EqualTo(0));
        }
    }
}
