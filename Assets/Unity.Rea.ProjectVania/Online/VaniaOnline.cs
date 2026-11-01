using System.Collections.Generic;
using Unity.Multiplayer;      // NetBootstrap, NetSession, HeadlessBootstrap
using Unity.Multiplayer.Core; // Disconnection
using Unity.Multiplayer.UI;   // NetFrontEnd
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Net;
using Unity.Rea.ProjectVania.Core.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Rea.ProjectVania.Online
{
    /// <summary>
    /// The online scene's one game component (U.VAN-T31). The menus are the Unity.Multiplayer kit's drop-in front end
    /// (Host, Join by IP, Join by code, the lobby with its join code and roster, reconnect); this adds <c>Play alone</c>,
    /// which leaves for the local game exactly as it plays without the kit. When the game starts (the host presses Start,
    /// or a dedicated server is running) the server composes the game as the one authority (<see cref="GameHost.Serve"/>):
    /// the host's own seat plays from its keyboard and pad, every other seat from what its <see cref="VaniaNetPlayer"/>
    /// sends, and <see cref="VaniaWorldSync"/> sends the steps to every client's replica. The game's own Esc menu stays the
    /// game's (the kit's in-game menu is off); its Quit, and the console's <c>exit</c>, end only that player's program.
    /// </summary>
    [DefaultExecutionOrder(-800)] // before NetBootstrap.Start, which may start a headless server at once
    public sealed class VaniaOnline : MonoBehaviour
    {
        /// <summary>The local game's scene (the build's first), which <see cref="PlayAlone"/> opens.</summary>
        public const string LocalScene = "Main";

        /// <summary>The main menu's button back to local play.</summary>
        public const string PlayAloneLabel = "Play alone";

        /// <summary>The host's own seat (the kit seats the host first).</summary>
        public const int HostSeat = 0;

        static readonly List<VaniaNetPlayer> Waiting = new List<VaniaNetPlayer>();
        static GameSettings _settings;

        [Tooltip("The VaniaWorldSync prefab (a NetworkObject, a NetKey and VaniaWorldSync) the server spawns for each session.")]
        [SerializeField] private GameObject worldSyncPrefab;

        public GameObject WorldSyncPrefab
        {
            get => worldSyncPrefab;
            set => worldSyncPrefab = value;
        }

        /// <summary>Server: the running game's lockstep, or null before the game starts (and on a client).</summary>
        public static LockstepAuthority Authority { get; private set; }

        /// <summary>Server: the seed every peer's world was built from.</summary>
        public static int Seed { get; private set; }

        NetFrontEnd _frontEnd;

        /// <summary>The settings registry, loaded once (null, with every error logged, if a file is bad).</summary>
        public static GameSettings LoadSettings()
        {
            if (_settings != null)
            {
                return _settings;
            }

            var result = Startup.LoadSettings(Startup.StreamingSource());
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    Debug.LogError("[ProjectVania] " + error);
                }

                return null;
            }

            _settings = result.Settings;
            return _settings;
        }

        /// <summary>Server: seats a joined player in the running game, or keeps it until the game starts.</summary>
        internal static void Seat(VaniaNetPlayer player)
        {
            if (player.IsOwner)
            {
                return; // the host's own player: the authority's local seat
            }

            if (Authority == null)
            {
                if (!Waiting.Contains(player)) Waiting.Add(player);
                return;
            }

            Authority.AddSeat(player.Seat);
            player.Welcome(Seed, HostSeat, Authority);
        }

        /// <summary>Server: a player left; their character stays where it stands.</summary>
        internal static void Unseat(VaniaNetPlayer player)
        {
            Waiting.Remove(player);
            if (Authority != null && player.IsSeated && !player.IsOwner)
            {
                Authority.RemoveSeat(player.Seat);
            }
        }

        void Start()
        {
            // A host or a client keeps its connection running in the background; the local scene keeps the project's own setting.
            Application.runInBackground = true;
            var settings = LoadSettings();
            var bootstrap = NetBootstrap.Instance;
            if (bootstrap == null || bootstrap.Session == null)
            {
                Debug.LogError("[ProjectVania] The online scene has no NetBootstrap; run MindAttic/ProjectVania/Rebuild Online Scene.", this);
                return;
            }

            if (!bootstrap.Session.IsActive && settings != null)
            {
                bootstrap.Configure(c => c.MaxPlayers = settings.Online.MaxPlayers);
            }

            _frontEnd = NetFrontEnd.Instance;
            if (_frontEnd == null)
            {
                return;
            }

            _frontEnd.AddMainMenuItems(s => s.AddButton(PlayAloneLabel, PlayAlone));
            _frontEnd.SessionStarted += OnSessionStarted;
            _frontEnd.GameStarted += OnGameStarted;
            _frontEnd.Left += OnLeft;
        }

        void OnDestroy()
        {
            if (_frontEnd != null)
            {
                _frontEnd.SessionStarted -= OnSessionStarted;
                _frontEnd.GameStarted -= OnGameStarted;
                _frontEnd.Left -= OnLeft;
            }

            EndGame();
        }

        void OnSessionStarted(NetSession session)
        {
            // A headless server (-mode server) is paced to the tick rate once it serves.
            if (session.IsServer && HeadlessBootstrap.IsHeadless && NetBootstrap.Instance != null)
            {
                HeadlessBootstrap.Apply(NetBootstrap.Instance.BuildConfig().TickRate);
            }
        }

        /// <summary>Server: composes the game as the authority and seats everyone already here.</summary>
        void OnGameStarted()
        {
            var session = _frontEnd != null ? _frontEnd.Session : null;
            if (session == null || !session.IsServer || Authority != null)
            {
                return;
            }

            var settings = LoadSettings();
            if (settings == null)
            {
                return;
            }

            Seed = Random.Range(int.MinValue, int.MaxValue);
            var host = Bootstrap.Compose(settings, new PlayerId(HostSeat), Seed);
            Authority = host.Serve(withLocalSeat: !HeadlessBootstrap.IsHeadless);
            Debug.Log("[ProjectVania] Serving the online game (seed " + Seed + (HeadlessBootstrap.IsHeadless ? ", dedicated" : ", host plays seat " + HostSeat) + ").");
            if (worldSyncPrefab != null)
            {
                VaniaWorldSync.Serve(session, worldSyncPrefab);
            }

            var waiting = new List<VaniaNetPlayer>(Waiting);
            Waiting.Clear();
            foreach (var player in waiting)
            {
                if (player != null && player.IsSpawned)
                {
                    Seat(player);
                }
            }
        }

        void OnLeft(Disconnection why) => EndGame();

        static void EndGame()
        {
            Authority = null;
            Waiting.Clear();
            Bootstrap.Dispose();
        }

        /// <summary>Leaves the online scene for the local game, which runs without any of the kit, as it always has.</summary>
        public static void PlayAlone()
        {
            var bootstrap = NetBootstrap.Instance;
            if (bootstrap != null)
            {
                bootstrap.Leave();
                Destroy(bootstrap.gameObject);
            }

            EndGame();
            Application.runInBackground = false;
            // The bootstrap composes local play once, as the first scene loads; coming back from the online scene it is asked again.
            SceneManager.sceneLoaded += ComposeLocalPlay;
            SceneManager.LoadScene(LocalScene, LoadSceneMode.Single);
        }

        static void ComposeLocalPlay(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= ComposeLocalPlay;
            Bootstrap.Run(Startup.StreamingSource());
        }
    }
}
