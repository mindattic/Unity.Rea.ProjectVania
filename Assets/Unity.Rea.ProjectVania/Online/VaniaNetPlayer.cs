using System;
using Unity.Multiplayer;      // NetEntity
using Unity.Multiplayer.Core; // [ToServer], [ToOwner], [Synced]
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Input;
using Unity.Rea.ProjectVania.Core.Net;
using UnityEngine;

namespace Unity.Rea.ProjectVania.Online
{
    /// <summary>
    /// One online player (U.VAN-T31): the NetworkManager's Player Prefab, so every connection gets one, carrying its seat,
    /// which is the <see cref="PlayerId"/> its input plays as in the one authority's world (seat n starts on character n,
    /// with camera n). On the server it seats its player in the authority's lockstep (<see cref="VaniaOnline.Seat"/>),
    /// sends the owner the world's seed and the session's history, and queues every input frame the owner sends for the
    /// authority's next steps. On the owning client it composes that screen's game as a replica of the authority's
    /// (<see cref="GameHost.Follow"/>) and sends its controls, one frame per fixed step. The host's own player is the
    /// authority's local seat and sends nothing.
    /// </summary>
    public partial class VaniaNetPlayer : NetEntity
    {
        /// <summary>The seat before the server has given one.</summary>
        public const int NoSeat = -1;

        /// <summary>Frame messages a second the server takes from one player: the owner sends every second step.</summary>
        public const int FrameRate = 60;

        /// <summary>Steps a client's frames are held for before they are sent (two: thirty messages a second).</summary>
        public const int FramesPerMessage = 2;

        /// <summary>Seconds a replica waits on a gap in the steps it was sent before it asks for them again.</summary>
        public const float GapSeconds = 1f;

        // client -> server: the owner's input frames (StepCodec.EncodeFrames), oldest first.
        [ToServer(RateLimit = FrameRate, Burst = FrameRate * 2)]
        public partial void SendFrames(byte[] frames);

        // client -> server: the steps from a tick on again (a replica that missed some).
        [ToServer(RateLimit = 2, Burst = 4)]
        public partial void AskFrom(long tick);

        // server -> owner: the authority's seed, this player's seat and the authority's own seat; the owner composes its replica.
        [ToOwner]
        public partial void Begin(int seed, int seat, int authoritySeat);

        // server -> owner: recorded steps (StepCodec.EncodeSteps), the history or a resend.
        [ToOwner]
        public partial void History(byte[] steps);

        [Synced] private int _seat = NoSeat;

        float _gap;
        GameHost _screen;

        /// <summary>The player id this seat plays as (P-1 until seated).</summary>
        public PlayerId PlayerId => new PlayerId(Seat);

        public bool IsSeated => Seat != NoSeat;

        /// <summary>Owning client: whether its screen is a running replica of the authority's world.</summary>
        public bool Following => _screen != null && _screen.Replica != null;

        /// <summary>Owning client: its screen's game (null before <see cref="Begin"/>).</summary>
        public GameHost Screen => _screen;

        /// <summary>
        /// Owning client: changes each frame before it is sent (a bot's controls, <see cref="VaniaBot"/>). Null sends the
        /// device's frames as they are.
        /// </summary>
        public Action<InputFrame> Script { get; set; }

        /// <summary>Server: frame messages received that did not decode, and were ignored.</summary>
        public int Refused { get; private set; }

        protected override void OnSpawned()
        {
            if (IsServer)
            {
                Seat = Owner.HasSeat ? Owner.SeatIndex : 0;
                VaniaOnline.Seat(this);
            }
        }

        protected override void OnDespawned()
        {
            if (IsServer)
            {
                VaniaOnline.Unseat(this);
            }

            if (IsOwner && !IsServer && _screen != null)
            {
                Bootstrap.Dispose();
            }

            _screen = null;
        }

        /// <summary>Server: tells the owner how to build its replica, then sends it every step recorded so far.</summary>
        internal void Welcome(int seed, int authoritySeat, LockstepAuthority authority)
        {
            Begin(seed, Seat, authoritySeat);
            foreach (var message in authority.HistoryFrom(0))
            {
                History(message);
            }
        }

        private void OnBegin(int seed, int seat, int authoritySeat)
        {
            if (IsServer)
            {
                return;
            }

            var settings = VaniaOnline.LoadSettings();
            if (settings == null)
            {
                return;
            }

            _screen = Bootstrap.Compose(settings, new PlayerId(seat), seed);
            _screen.Follow(authoritySeat);
            _gap = 0f;
        }

        private void OnHistory(byte[] steps)
        {
            if (IsServer || !Following)
            {
                return;
            }

            _screen.Replica.Receive(StepCodec.DecodeSteps(steps), _screen.World);
        }

        private void OnSendFrames(byte[] frames)
        {
            var decoded = StepCodec.DecodeFrames(frames);
            var authority = VaniaOnline.Authority;
            if (decoded == null)
            {
                Refused++;
                return;
            }

            if (authority != null && IsSeated)
            {
                authority.Receive(Seat, decoded);
            }
        }

        private void OnAskFrom(long tick)
        {
            var authority = VaniaOnline.Authority;
            if (authority == null)
            {
                return;
            }

            foreach (var message in authority.HistoryFrom(tick))
            {
                History(message);
            }
        }

        // After GameHost.Update has sampled this frame's steps (VaniaWorldSync runs later still).
        private void LateUpdate()
        {
            if (!IsSpawned || !IsOwner || IsServer || !Following)
            {
                return;
            }

            if (_screen.Replica.Waiting > 0 && _screen.Replica.Ready(_screen.World) == 0)
            {
                _gap += Time.unscaledDeltaTime;
                if (_gap >= GapSeconds)
                {
                    _gap = 0f;
                    AskFrom(_screen.World.Tick);
                }
            }
            else
            {
                _gap = 0f;
            }

            if (_screen.PendingOutgoing < FramesPerMessage)
            {
                return;
            }

            var frames = _screen.TakeOutgoing();
            if (Script != null)
            {
                foreach (var frame in frames)
                {
                    Script(frame);
                }
            }

            SendFrames(StepCodec.EncodeFrames(frames));
        }
    }
}
