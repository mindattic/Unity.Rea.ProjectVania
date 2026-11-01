using System;
using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Input;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;

namespace Unity.Rea.ProjectVania.Core.Net
{
    /// <summary>
    /// Routes one step's <see cref="StepRecord"/> into a world (U.VAN-T31): the authority's song end first, then each
    /// seat's frame through that seat's own edge state and <see cref="InputRouter"/>, exactly as <see cref="PlayerInput"/>
    /// routes a local driver's. The authority and every replica route through one of these, so the same records make the
    /// same commands in the same order, and so the same game.
    /// </summary>
    public sealed class SeatRouter
    {
        readonly InputData _bindings;
        readonly PlayerId _songOwner;
        readonly SortedDictionary<int, InputState> _states = new SortedDictionary<int, InputState>();
        readonly List<ICommand> _commands = new List<ICommand>();

        /// <param name="bindings">input.json's bindings.</param>
        /// <param name="songOwner">Whose <see cref="EndSong"/> a record's song end is (the authority's own seat).</param>
        public SeatRouter(InputData bindings, PlayerId songOwner)
        {
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            if (!songOwner.IsValid) throw new ArgumentException("The song's end must name a player.", nameof(songOwner));
            _songOwner = songOwner;
        }

        /// <summary>Submits the commands <paramref name="record"/> makes to <paramref name="world"/>, as the step it starts reads it.</summary>
        public void Route(StepRecord record, GameWorld world)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (record.SongEnded) world.Submit(new EndSong(_songOwner));
            foreach (var seat in record.Seats)
            {
                if (!_states.TryGetValue(seat.Seat, out var state))
                {
                    state = new InputState();
                    _states.Add(seat.Seat, state);
                }

                state.Shift();
                state.Current.CopyFrom(seat.Frame);
                _commands.Clear();
                InputRouter.Route(world.State, state, _bindings, new PlayerId(seat.Seat), _commands);
                foreach (var command in _commands) world.Submit(command);
            }
        }
    }

    /// <summary>
    /// The one authority of an online game (U.VAN-T31): it steps the world, and before every step takes one input frame
    /// from each seat (the host's own from its device; a remote seat's the oldest it has been sent, or, with none waiting,
    /// its last one held with the wheel at rest, so a held key stays held and nothing is pressed twice), records them as
    /// a <see cref="StepRecord"/> and routes them (<see cref="SeatRouter"/>). The records are what the clients' replicas
    /// run (<see cref="TakeNew"/>) and the session's history, kept encoded in online.json's chunks of steps
    /// (<see cref="HistoryFrom"/>) for a player who joins late or missed some.
    /// </summary>
    public sealed class LockstepAuthority : IStepInput
    {
        readonly SeatRouter _router;
        readonly int _queueLimit;
        readonly int? _localSeat;
        readonly IInputDevice _localDevice;
        readonly InputFrame _sample = new InputFrame();
        readonly SortedDictionary<int, Seat> _seats = new SortedDictionary<int, Seat>();
        readonly List<byte[]> _chunks = new List<byte[]>();
        readonly List<StepRecord> _open = new List<StepRecord>();
        readonly List<StepRecord> _unsent = new List<StepRecord>();
        readonly int _chunkSteps;
        long _start;
        bool _songEnded;

        sealed class Seat
        {
            public readonly Queue<InputFrame> Waiting = new Queue<InputFrame>();
            public InputFrame Last = new InputFrame();
        }

        /// <param name="bindings">input.json's bindings.</param>
        /// <param name="online">online.json's figures.</param>
        /// <param name="localSeat">The host's own seat, played from <paramref name="localDevice"/>; null on a dedicated server.</param>
        /// <param name="localDevice">The host's own controls (required with <paramref name="localSeat"/>).</param>
        public LockstepAuthority(InputData bindings, OnlineData online, int? localSeat, IInputDevice localDevice)
        {
            if (online == null) throw new ArgumentNullException(nameof(online));
            if (localSeat.HasValue && localDevice == null) throw new ArgumentNullException(nameof(localDevice));
            _queueLimit = online.InputQueueLimit;
            _chunkSteps = online.HistoryChunkSteps;
            _localSeat = localSeat;
            _localDevice = localDevice;
            _router = new SeatRouter(bindings, new PlayerId(localSeat ?? 0));
            if (localSeat.HasValue) _seats.Add(localSeat.Value, new Seat());
        }

        /// <summary>Steps recorded since the session began.</summary>
        public long Recorded { get; private set; }

        /// <summary>
        /// The recorded steps from <paramref name="tick"/> on (from the start of the chunk that holds it), as
        /// <see cref="StepCodec.EncodeSteps"/> messages, oldest first: a joining player's catch-up, or a resend.
        /// </summary>
        public List<byte[]> HistoryFrom(long tick)
        {
            var messages = new List<byte[]>();
            var first = (int)Math.Max(0, Math.Min((tick - _start) / _chunkSteps, _chunks.Count));
            for (var i = first; i < _chunks.Count; i++) messages.Add(_chunks[i]);
            if (_open.Count > 0) messages.Add(StepCodec.EncodeSteps(_open));
            return messages;
        }

        /// <summary>The seats whose frames each step takes, in order.</summary>
        public IEnumerable<int> Seats => _seats.Keys;

        /// <summary>Seats <paramref name="seat"/> from the next step on (a player joined); seating one already seated does nothing.</summary>
        public void AddSeat(int seat)
        {
            if (seat < 0 || seat > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(seat));
            if (!_seats.ContainsKey(seat)) _seats.Add(seat, new Seat());
        }

        /// <summary>
        /// Unseats <paramref name="seat"/> from the next step on (its player left); its character stands where it was,
        /// still in the game. The host's own seat is never unseated.
        /// </summary>
        public bool RemoveSeat(int seat) => seat != _localSeat && _seats.Remove(seat);

        /// <summary>Queues frames a seat's player sent, oldest first; past online.json's limit the oldest are dropped.</summary>
        public void Receive(int seat, IReadOnlyList<InputFrame> frames)
        {
            if (frames == null || !_seats.TryGetValue(seat, out var state) || seat == _localSeat) return;
            foreach (var frame in frames)
            {
                state.Waiting.Enqueue(StepCodec.Normalise(frame));
                while (state.Waiting.Count > _queueLimit) state.Waiting.Dequeue();
            }
        }

        /// <summary>The host's song ran to its end: the next step starts with its <c>EndSong</c>, recorded for every replica.</summary>
        public void EndSong() => _songEnded = true;

        /// <summary>The records made since the last call, oldest first (what the clients are sent next).</summary>
        public List<StepRecord> TakeNew()
        {
            var taken = new List<StepRecord>(_unsent);
            _unsent.Clear();
            return taken;
        }

        public void BeforeStep(GameWorld world)
        {
            if (Recorded == 0) _start = world.Tick;
            var record = new StepRecord(world.Tick, _songEnded);
            _songEnded = false;
            foreach (var entry in _seats)
            {
                InputFrame frame;
                if (entry.Key == _localSeat)
                {
                    _sample.Clear();
                    _localDevice.Sample(_sample);
                    frame = StepCodec.Normalise(_sample);
                }
                else if (entry.Value.Waiting.Count > 0)
                {
                    frame = entry.Value.Waiting.Dequeue();
                }
                else
                {
                    frame = new InputFrame();
                    frame.CopyFrom(entry.Value.Last);
                    frame.Wheel = 0;
                }

                entry.Value.Last = frame;
                record.Add(entry.Key, frame);
            }

            Recorded++;
            _unsent.Add(record);
            _open.Add(record);
            if (_open.Count == _chunkSteps)
            {
                _chunks.Add(StepCodec.EncodeSteps(_open));
                _open.Clear();
            }

            _router.Route(record, world);
        }
    }

    /// <summary>
    /// A client's copy of the authority's world (U.VAN-T31): built from the authority's seed, it runs only the steps the
    /// authority has recorded, each through the same <see cref="SeatRouter"/>, so it holds the authority's game step for
    /// step. Records may arrive in any order and more than once (the history and the live steps come separately); each
    /// runs once, in tick order, when every step before it has.
    /// </summary>
    public sealed class LockstepReplica : IStepInput
    {
        readonly SeatRouter _router;
        readonly SortedDictionary<long, StepRecord> _waiting = new SortedDictionary<long, StepRecord>();
        StepRecord _next;

        /// <param name="bindings">input.json's bindings.</param>
        /// <param name="authoritySeat">The authority's own seat, whose song end the records carry.</param>
        public LockstepReplica(InputData bindings, int authoritySeat)
        {
            _router = new SeatRouter(bindings, new PlayerId(authoritySeat));
        }

        /// <summary>Records received and not yet run.</summary>
        public int Waiting => _waiting.Count;

        /// <summary>Keeps <paramref name="records"/> to run; any the world has already run are ignored.</summary>
        public void Receive(IEnumerable<StepRecord> records, GameWorld world)
        {
            if (records == null) return;
            foreach (var record in records)
            {
                if (record != null && record.Tick >= world.Tick) _waiting[record.Tick] = record;
            }
        }

        /// <summary>How many steps <paramref name="world"/> can run now: the received records that follow on from its tick.</summary>
        public int Ready(GameWorld world)
        {
            var count = 0;
            while (_waiting.ContainsKey(world.Tick + count)) count++;
            return count;
        }

        /// <summary>Runs up to <paramref name="most"/> of the ready steps and returns how many ran.</summary>
        public int Run(GameWorld world, int most)
        {
            var steps = Math.Min(Ready(world), Math.Max(0, most));
            return world.Run(steps, this);
        }

        public void BeforeStep(GameWorld world)
        {
            if (!_waiting.TryGetValue(world.Tick, out _next)) throw new InvalidOperationException("No record for step " + world.Tick + ".");
            _waiting.Remove(world.Tick);
            _router.Route(_next, world);
        }
    }
}
