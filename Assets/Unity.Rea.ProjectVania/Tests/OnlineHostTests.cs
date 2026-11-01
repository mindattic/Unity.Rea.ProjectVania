using System.IO;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Net;
using Unity.Rea.ProjectVania.Core.Settings;
using UnityEngine;

namespace Unity.Rea.ProjectVania.Tests
{
    /// <summary>
    /// U.VAN-T31: the game host's online modes without a network. An authority host and a client host built from the
    /// same seed, the client's frames carried to the authority and the authority's steps carried back as the kit would,
    /// end on the same game; and only a screen's own seat quits or flips its window.
    /// </summary>
    public class OnlineHostTests
    {
        const int Seed = 99;
        GameObject _authorityObject;
        GameObject _clientObject;

        static GameSettings Settings() =>
            GameSettings.Load(new DirectorySettingsSource(Path.Combine(Application.dataPath, "StreamingAssets", GameSettings.Folder)));

        [TearDown]
        public void TearDown()
        {
            if (_authorityObject != null) Object.DestroyImmediate(_authorityObject);
            if (_clientObject != null) Object.DestroyImmediate(_clientObject);
        }

        GameHost Host(ref GameObject holder, string name, GameSettings settings, int seat)
        {
            holder = new GameObject(name);
            var host = holder.AddComponent<GameHost>();
            host.Initialize(settings, new PlayerId(seat), Seed);
            return host;
        }

        [Test]
        public void AClientFollowingTheAuthorityHoldsTheSameGame()
        {
            var settings = Settings();
            var authorityHost = Host(ref _authorityObject, "Authority", settings, 0);
            var clientHost = Host(ref _clientObject, "Client", settings, 1);
            var authority = authorityHost.Serve(withLocalSeat: true);
            authority.AddSeat(1);
            clientHost.Follow(0);

            Assert.That(clientHost.World.Characters[2].Name, Is.EqualTo(authorityHost.World.Characters[2].Name), "the seed names everyone alike");
            for (var frame = 0; frame < 120; frame++)
            {
                clientHost.Tick(1.0 / 60);
                authority.Receive(1, StepCodec.DecodeFrames(StepCodec.EncodeFrames(clientHost.TakeOutgoing())));
                authorityHost.Tick(1.0 / 60);
                clientHost.Replica.Receive(StepCodec.DecodeSteps(StepCodec.EncodeSteps(authority.TakeNew())), clientHost.World);
            }

            clientHost.Tick(1.0 / 60);
            Assert.That(clientHost.World.Tick, Is.EqualTo(authorityHost.World.Tick).Within(1), "the client runs the authority's steps");
            Assert.That(clientHost.World.Tick, Is.GreaterThan(100));
            Assert.That(clientHost.Input.Player, Is.EqualTo(new PlayerId(1)), "the client's screen is its own seat");
            while (clientHost.World.Tick < authorityHost.World.Tick)
            {
                clientHost.Tick(1.0 / 60);
            }

            for (var i = 0; i < authorityHost.World.Characters.Count; i++)
            {
                Assert.That(clientHost.World.Characters[i].Position.Y, Is.EqualTo(authorityHost.World.Characters[i].Position.Y), "character " + i);
            }
        }

        [Test]
        public void OnlyThisScreensOwnSeatQuitsOrFlipsTheWindow()
        {
            var settings = Settings();
            var host = Host(ref _authorityObject, "Authority", settings, 0);
            host.Serve(withLocalSeat: true);
            var exits = 0;
            host.Exit = () => exits++;

            host.Present(new IGameEvent[] { new QuitRequested(0, new PlayerId(1)) });
            Assert.That(exits, Is.EqualTo(0), "another seat's Quit leaves this program running");
            host.Present(new IGameEvent[] { new QuitRequested(0, new PlayerId(0)) });
            Assert.That(exits, Is.EqualTo(1));

            var server = Host(ref _clientObject, "Server", settings, 0);
            server.Serve(withLocalSeat: false);
            server.Exit = () => exits++;
            server.Present(new IGameEvent[] { new QuitRequested(0, new PlayerId(0)) });
            Assert.That(exits, Is.EqualTo(1), "a dedicated server plays no seat, so no Quit ends it");
        }
    }
}
