using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Input;
using Unity.Rea.ProjectVania.Core.Net;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    /// <summary>
    /// U.VAN-T31: online play is a lockstep the one authority orders. The authority records every seat's input frame per
    /// step; a replica built from the same seed that runs those records holds the same game, whether it received them live
    /// or as a late joiner's history, and the wire form loses nothing the router reads.
    /// </summary>
    public class LockstepTests
    {
        const double Frame = 1.0 / 60;
        const int Seed = 1234;

        static GameSettings Settings() => GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));

        // GameHost.Initialize's world, with the authority's seed.
        static GameWorld World(GameSettings s) =>
            new GameWorld(Map.FromSettings(s.Grid), s.Player, s.Characters.Roster, s.Names, new Random(Seed),
                s.Camera, s.Menu, s.Console, s.Audio, s.Hud, s.Animations, s.Editor);

        sealed class Script : IInputDevice
        {
            readonly Func<int, InputFrame, InputFrame> _frame;
            int _step;
            public Script(Func<int, InputFrame, InputFrame> frame) { _frame = frame; }
            public void Sample(InputFrame frame) => _frame(_step++, frame);
        }

        // The host (seat 0): runs right, jumps, then opens the console and types into it.
        static InputFrame Host(int step, InputFrame f)
        {
            if (step < 90) f.Set(Key.D);
            if (step == 100) f.Set(Key.Space);
            if (step == 200) f.Set(Key.OemTilde);
            if (step == 260) f.Set(Key.H);
            if (step == 262) f.Set(Key.I);
            return f;
        }

        // A remote seat's frames: runs left, kneels, moves the mouse and turns the wheel.
        static List<InputFrame> Remote(int from, int count)
        {
            var frames = new List<InputFrame>();
            for (var step = from; step < from + count; step++)
            {
                var f = new InputFrame();
                if (step < 60) f.Set(Key.A);
                if (step >= 120 && step < 150) f.Set(Key.S);
                if (step == 170) f.Wheel = 3;
                f.SetMouse(400 + step % 7, 300 - step % 5);
                frames.Add(f);
            }

            return frames;
        }

        static void AssertSameGame(GameWorld expected, GameWorld actual)
        {
            Assert.That(actual.Tick, Is.EqualTo(expected.Tick), "tick");
            Assert.That(actual.State, Is.EqualTo(expected.State), "state");
            Assert.That(actual.Map.Revision, Is.EqualTo(expected.Map.Revision), "map");
            Assert.That(actual.Console.Text, Is.EqualTo(expected.Console.Text), "console line");
            for (var i = 0; i < expected.Characters.Count; i++)
            {
                var e = expected.Characters[i];
                var a = actual.Characters[i];
                Assert.That(a.Name, Is.EqualTo(e.Name), "name " + i);
                Assert.That(a.Position.X, Is.EqualTo(e.Position.X), "x " + i);
                Assert.That(a.Position.Y, Is.EqualTo(e.Position.Y), "y " + i);
                Assert.That(a.Velocity.X, Is.EqualTo(e.Velocity.X), "vx " + i);
                Assert.That(a.Velocity.Y, Is.EqualTo(e.Velocity.Y), "vy " + i);
                Assert.That(a.Action, Is.EqualTo(e.Action), "action " + i);
            }

            for (var seat = 0; seat < 3; seat++)
            {
                var p = new PlayerId(seat);
                Assert.That(actual.DrivenIndex(p), Is.EqualTo(expected.DrivenIndex(p)), "driven " + seat);
                Assert.That(actual.Camera(p).Position.X, Is.EqualTo(expected.Camera(p).Position.X), "camera " + seat);
            }
        }

        [Test]
        public void FramesRoundTripAndRepeatsCostOneByte()
        {
            var a = new InputFrame().Set(Key.D).Set(PadButton.A).Set(MouseButton.Right).SetMouse(12.5f, 600f);
            a.PadConnected = true;
            a.Wheel = -4;
            var b = new InputFrame().Set(Key.OemTilde);
            b.WindowActive = false;
            var frames = new List<InputFrame> { a, a, a, b };
            var bytes = StepCodec.EncodeFrames(frames);
            var decoded = StepCodec.DecodeFrames(bytes);

            Assert.That(decoded, Has.Count.EqualTo(4));
            Assert.That(decoded[0].SameAs(a), Is.True);
            Assert.That(decoded[0].Wheel, Is.EqualTo(-1), "the wheel travels as its sign, all the router reads");
            Assert.That(decoded[2].SameAs(a), Is.True);
            Assert.That(decoded[3].SameAs(b), Is.True);
            Assert.That(decoded[3].WindowActive, Is.False);
            Assert.That(StepCodec.EncodeFrames(new List<InputFrame> { a, a, a, a, a, a }).Length,
                Is.EqualTo(StepCodec.EncodeFrames(new List<InputFrame> { a }).Length + 5), "a repeated frame is one byte");
            Assert.That(StepCodec.DecodeFrames(bytes.Take(bytes.Length - 3).ToArray()), Is.Null, "a cut message decodes to nothing");
            Assert.That(StepCodec.DecodeSteps(new byte[] { 9, 9, 9 }), Is.Null);
        }

        [Test]
        public void AReplicaRunningTheAuthoritysRecordsHoldsTheSameGame()
        {
            var s = Settings();
            var authorityWorld = World(s);
            var authority = new LockstepAuthority(s.Input, s.Online, 0, new Script(Host));
            authority.AddSeat(1);
            var replicaWorld = World(s);
            var replica = new LockstepReplica(s.Input, 0);

            var sent = 0;
            for (var frame = 0; frame < 300; frame++)
            {
                // The remote seat's frames arrive in uneven bursts, as they would over a network.
                if (frame % 3 == 0)
                {
                    authority.Receive(1, StepCodec.DecodeFrames(StepCodec.EncodeFrames(Remote(sent, 3))));
                    sent += 3;
                }

                if (frame == 150) authority.EndSong();
                authorityWorld.Advance(Frame, authority);
                var message = StepCodec.EncodeSteps(authority.TakeNew());
                replica.Receive(StepCodec.DecodeSteps(message), replicaWorld);
                replica.Run(replicaWorld, int.MaxValue);
            }

            Assert.That(authorityWorld.Tick, Is.GreaterThan(250));
            Assert.That(authorityWorld.State, Is.EqualTo(GameState.Console), "the host's tilde opened the console");
            Assert.That(authorityWorld.Console.Text, Is.EqualTo("hi"), "and the host typed into it");
            Assert.That(authorityWorld.Characters[1].Position.X, Is.LessThan(s.Player.SpawnPositions[1].X), "the remote seat ran its own character left");
            AssertSameGame(authorityWorld, replicaWorld);
        }

        [Test]
        public void ALateJoinerCatchesUpFromTheHistoryInAnyOrder()
        {
            var s = Settings();
            var authorityWorld = World(s);
            var authority = new LockstepAuthority(s.Input, s.Online, 0, new Script(Host));
            authority.AddSeat(2);
            for (var frame = 0; frame < 400; frame++)
            {
                if (frame % 2 == 0) authority.Receive(2, Remote(frame, 2));
                authorityWorld.Advance(Frame, authority);
            }

            var history = authority.HistoryFrom(0);
            Assert.That(history.Count, Is.GreaterThan(1), "the history is kept in online.json's chunks");
            var lateWorld = World(s);
            var late = new LockstepReplica(s.Input, 0);
            for (var i = history.Count - 1; i >= 0; i--)
            {
                late.Receive(StepCodec.DecodeSteps(history[i]), lateWorld);
            }

            // A live message it already had from the history changes nothing.
            late.Receive(StepCodec.DecodeSteps(StepCodec.EncodeSteps(authority.TakeNew())), lateWorld);
            while (late.Run(lateWorld, s.Online.CatchUpStepsPerFrame) > 0)
            {
            }

            AssertSameGame(authorityWorld, lateWorld);

            // A resend from where a replica stands starts at the chunk that holds that step.
            var resend = authority.HistoryFrom(authorityWorld.Tick - 1).SelectMany(StepCodec.DecodeSteps).ToList();
            Assert.That(resend.First().Tick, Is.LessThanOrEqualTo(authorityWorld.Tick - 1));
            Assert.That(resend.Last().Tick, Is.EqualTo(authorityWorld.Tick - 1));
        }

        [Test]
        public void ASeatThatSentNothingHoldsItsControlsWithoutPressingAgain()
        {
            var s = Settings();
            var world = World(s);
            var authority = new LockstepAuthority(s.Input, s.Online, null, null);
            authority.AddSeat(1);
            var jump = new InputFrame().Set(Key.D).Set(Key.Space);
            jump.Wheel = 1;
            authority.Receive(1, new List<InputFrame> { jump });
            world.Run(5, authority);
            var steps = authority.TakeNew();

            Assert.That(steps, Has.Count.EqualTo(5));
            Assert.That(steps.All(r => r.Seats.Single().Frame.IsDown(Key.D) && r.Seats.Single().Frame.IsDown(Key.Space)), Is.True, "held");
            Assert.That(steps.Skip(1).All(r => r.Seats.Single().Frame.Wheel == 0), Is.True, "the wheel turns once");
            Assert.That(authority.Seats, Is.EqualTo(new[] { 1 }), "a dedicated server seats no one of its own");
            Assert.That(authority.RemoveSeat(1), Is.True);
            world.Run(1, authority);
            Assert.That(authority.TakeNew().Single().Seats, Is.Empty);
        }

        [Test]
        public void WindowAndExitRequestsNameTheSeatThatAsked()
        {
            var s = Settings();
            var world = World(s);
            var authority = new LockstepAuthority(s.Input, s.Online, 0, new Script((step, f) => f));
            authority.AddSeat(1);
            authority.Receive(1, new List<InputFrame> { new InputFrame().Set(Key.F12) });
            world.Run(1, authority);

            Assert.That(world.Events.OfType<FullscreenToggled>().Single().Player, Is.EqualTo(new PlayerId(1)));
        }

        [Test]
        public void OnlineSettingsLoadAndASeatNeedsACharacter()
        {
            var s = Settings();
            Assert.That(s.Online.MaxPlayers, Is.EqualTo(s.Characters.Roster.Count), "one seat per playable character");

            var tooMany = GameSettingsTests.Patched(GameSettings.OnlineFile, json => json.Replace("\"maxPlayers\": 3", "\"maxPlayers\": 4"));
            var e = Assert.Throws<SettingsException>(() => GameSettings.Load(tooMany));
            Assert.That(e.Errors.Single(), Does.Contain("online.json").And.Contain("maxPlayers"));
        }
    }
}
