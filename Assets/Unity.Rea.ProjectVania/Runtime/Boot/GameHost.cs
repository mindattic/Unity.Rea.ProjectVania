using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Input;
using Unity.Rea.ProjectVania.Core.Net;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using Unity.Rea.ProjectVania.Core.World;
using UnityEngine;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// Owns the running game: the validated settings, the Core world and player one's controls, stepped from real
    /// time at the fixed rate. Created by <see cref="Bootstrap"/>, never placed in a scene by hand.
    /// </summary>
    public sealed class GameHost : MonoBehaviour
    {
        public GameSettings Settings { get; private set; }
        public GameWorld World { get; private set; }

        /// <summary>The keyboard, first pad and mouse (the original's PlayerIndex.One).</summary>
        public InputSystemDevice Device { get; private set; }

        /// <summary>Player one's controls, routed per game state into commands before every step.</summary>
        public PlayerInput Input { get; private set; }

        /// <summary>Plays the cues the world names (the menu's, the console's and the characters' sounds).</summary>
        public CuePlayer Cues { get; private set; }

        /// <summary>Plays the songs the console starts, pauses and mutes (the original's MediaPlayer).</summary>
        public SongPlayer Songs { get; private set; }

        /// <summary>
        /// What the menu's Quit does (<c>Game.Exit()</c>): quits a build, or leaves Play mode in the Editor. Replaceable so
        /// a test can see it called without ending the test run.
        /// </summary>
        public System.Action Exit { get; set; } = ExitProgram;

        /// <summary>
        /// Online as the one authority (U.VAN-T31): the lockstep that takes every seat's frame before each step. Null in
        /// local play, where <see cref="Input"/> drives the world directly.
        /// </summary>
        public LockstepAuthority Authority { get; private set; }

        /// <summary>
        /// Online as a client (U.VAN-T31): the replica that runs the authority's recorded steps. While it is set the world
        /// steps only as the authority's records arrive, and this screen's controls are sampled into
        /// <see cref="TakeOutgoing"/> for the authority instead of driving the world.
        /// </summary>
        public LockstepReplica Replica { get; private set; }

        /// <summary>Whether this screen plays a seat of its own (false on a dedicated server, which only serves).</summary>
        public bool HasLocalSeat { get; private set; } = true;

        /// <summary>Whether the replica is catching up on more steps than online.json's silent backlog (nothing is played then).</summary>
        public bool CatchingUp { get; private set; }

        readonly System.Collections.Generic.List<InputFrame> _outgoing = new System.Collections.Generic.List<InputFrame>();
        readonly FixedStepper _sampleClock = new FixedStepper();

        /// <summary>
        /// Composes the world from <paramref name="settings"/>. Local play passes neither optional argument: player one at
        /// the keyboard and a fresh random roll of names. Online (U.VAN-T31) <paramref name="player"/> is this screen's seat
        /// and <paramref name="seed"/> the authority's, so every peer's world starts the same.
        /// </summary>
        public void Initialize(GameSettings settings, PlayerId? player = null, int? seed = null)
        {
            Settings = settings;
            Authority = null;
            Replica = null;
            HasLocalSeat = true;
            _outgoing.Clear();
            _sampleClock.Reset();
            // characters.json's roster, each with a fresh random name (owner decision, U.VAN-T25: Morris, Shanoa and Leo,
            // where the original spawned two Morrises).
            World = new GameWorld(Map.FromSettings(settings.Grid), settings.Player, settings.Characters.Roster, settings.Names, seed.HasValue ? new System.Random(seed.Value) : new System.Random(), settings.Camera, settings.Menu,
                settings.Console, settings.Audio, settings.Hud, settings.Animations, settings.Editor);
            Device?.Dispose();
            Device = new InputSystemDevice(settings.Input.StickPressPoint);
            Input = new PlayerInput(player ?? PlayerId.One, settings.Input, Device);
            Cues = GetComponent<CuePlayer>();
            if (Cues == null)
            {
                Cues = gameObject.AddComponent<CuePlayer>();
            }

            Cues.Initialize(settings.Audio);
            Songs = GetComponent<SongPlayer>();
            if (Songs == null)
            {
                Songs = gameObject.AddComponent<SongPlayer>();
            }

            Songs.Initialize(settings.Audio);
        }

        /// <summary>
        /// Makes this screen the online game's one authority (U.VAN-T31): from the next step every seat's frame is taken by
        /// <see cref="Authority"/>, this screen's own as its seat unless <paramref name="withLocalSeat"/> is false (a
        /// dedicated server). Call it before the world has stepped, so the history starts with the world.
        /// </summary>
        public LockstepAuthority Serve(bool withLocalSeat)
        {
            HasLocalSeat = withLocalSeat;
            Replica = null;
            Authority = new LockstepAuthority(Settings.Input, Settings.Online, withLocalSeat ? Input.Player.Value : (int?)null, Device);
            return Authority;
        }

        /// <summary>
        /// Makes this screen a client of the online game (U.VAN-T31): its world runs only the steps the authority records
        /// (<see cref="Replica"/>), and its controls go to the authority (<see cref="TakeOutgoing"/>).
        /// </summary>
        public LockstepReplica Follow(int authoritySeat)
        {
            Authority = null;
            Replica = new LockstepReplica(Settings.Input, authoritySeat);
            return Replica;
        }

        /// <summary>A client's frames sampled and not yet taken (<see cref="TakeOutgoing"/>).</summary>
        public int PendingOutgoing => _outgoing.Count;

        /// <summary>A client's controls sampled since the last call, one frame per fixed step, oldest first, for the authority.</summary>
        public System.Collections.Generic.List<InputFrame> TakeOutgoing()
        {
            var taken = new System.Collections.Generic.List<InputFrame>(_outgoing);
            _outgoing.Clear();
            return taken;
        }

        /// <summary>One rendered frame: latch this frame's input, run every step now due, then present its events.</summary>
        public void Tick(double deltaSeconds)
        {
            if (World == null)
            {
                return;
            }

            Device.Poll();
            if (Replica != null)
            {
                FollowTheAuthority(deltaSeconds);
                HoldTheMouse();
                return;
            }

            if (Authority != null)
            {
                if (Songs.Ended())
                {
                    Authority.EndSong();
                }

                World.Advance(deltaSeconds, Authority);
                Present(World.Events);
                HoldTheMouse();
                return;
            }

            // A song that ran to its end is reported before the step, as the original's MediaPlayer.State read Stopped then.
            if (Songs.Ended())
            {
                World.Submit(new EndSong(Input.Player));
            }

            World.Advance(deltaSeconds, Input);
            Present(World.Events);
            HoldTheMouse();
        }

        /// <summary>
        /// A client's frame (U.VAN-T31): its controls sampled once per fixed step, as the authority samples its own, and
        /// queued for the authority; then every authority step received runs, up to online.json's most per frame. A replica
        /// far behind (a late joiner replaying the history) plays none of what those steps produce.
        /// </summary>
        void FollowTheAuthority(double deltaSeconds)
        {
            var steps = _sampleClock.Advance(deltaSeconds);
            for (var i = 0; i < steps; i++)
            {
                var frame = new InputFrame();
                Device.Sample(frame);
                _outgoing.Add(frame);
            }

            CatchingUp = Replica.Ready(World) > Settings.Online.SilentBacklogSteps;
            Replica.Run(World, Settings.Online.CatchUpStepsPerFrame);
            if (!CatchingUp)
            {
                Present(World.Events);
            }
        }

        /// <summary>
        /// The editor's World mode reads the mouse as a joystick and warped it back to the screen centre every frame
        /// (<c>Mouse.SetPosition</c>, RECONSTRUCTION 12.1): the device reads its travel, and while playing the OS cursor is
        /// locked there. The original never showed the OS cursor (<c>IsMouseVisible = false</c>); it drew its own hands.
        /// </summary>
        void HoldTheMouse()
        {
            var editor = World.Editor(Input.Player);
            Device.Joystick = World.State == GameState.MapEditor && editor != null && editor.Mode == EditorCursorMode.World;
            if (!Application.isPlaying)
            {
                return;
            }

            var lockState = Device.Joystick ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != lockState)
            {
                Cursor.lockState = lockState;
            }

            if (Cursor.visible)
            {
                Cursor.visible = false;
            }
        }

        /// <summary>
        /// Plays what the world's steps produced: the window mode, the cues, the songs, pause and mute, the exit. The
        /// script's messages and fade are kept by the world's HUD and drawn from its state (A12).
        /// </summary>
        public void Present(System.Collections.Generic.IReadOnlyList<Core.Commands.IGameEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case FullscreenToggled fullscreen when IsMine(fullscreen.Player):
                        Screen.fullScreen = !Screen.fullScreen;
                        break;
                    case CueRequested cue:
                        Cues.Play(cue.Cue);
                        break;
                    case SongRequested song:
                        Songs.Play(song.Song);
                        break;
                    case MusicPauseChanged pause:
                        Songs.SetPaused(pause.Paused);
                        break;
                    case MuteChanged mute:
                        Songs.SetMuted(mute.Muted);
                        break;
                    case QuitRequested quit when IsMine(quit.Player):
                        Exit?.Invoke();
                        break;
                }
            }
        }

        // Whether a window or exit request is this screen's own: always in local play; online, only its own seat's.
        bool IsMine(PlayerId player) => HasLocalSeat && (Input == null || player == Input.Player);

        static void ExitProgram()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                UnityEditor.EditorApplication.isPlaying = false;
            }
#else
            Application.Quit();
#endif
        }

        void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        void OnDestroy()
        {
            Device?.Dispose();
        }
    }
}
