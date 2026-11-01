namespace Unity.Rea.ProjectVania.Core.Settings
{
    /// <summary>
    /// online.json: the online session's figures (U.VAN-T31). The original had no online play; these size the shared
    /// Unity.Multiplayer kit's session around the rebuild's lockstep (BIBLE <b>Online</b>), and change nothing in local play.
    /// </summary>
    public sealed class OnlineData
    {
        /// <summary>Seats in a session: one per playable character, so each seat starts on its own character and camera.</summary>
        public int MaxPlayers { get; private set; }

        /// <summary>
        /// Input frames the authority keeps waiting for one seat before it drops the oldest (a client that sent faster
        /// than the authority steps, after a stall).
        /// </summary>
        public int InputQueueLimit { get; private set; }

        /// <summary>Steps per message when the authority sends a joining player the session's history.</summary>
        public int HistoryChunkSteps { get; private set; }

        /// <summary>Most steps a replica runs in one rendered frame while it catches up with the authority.</summary>
        public int CatchUpStepsPerFrame { get; private set; }

        /// <summary>
        /// A replica further behind the authority than this many steps runs them without playing their sounds or window
        /// changes (a late joiner replaying the session's history hears none of it).
        /// </summary>
        public int SilentBacklogSteps { get; private set; }

        internal static OnlineData Read(SettingsReader r) => new OnlineData
        {
            MaxPlayers = r.Integer("maxPlayers", 1, 64),
            InputQueueLimit = r.Integer("inputQueueLimit", 1, 6000),
            HistoryChunkSteps = r.Integer("historyChunkSteps", 1, 60000),
            CatchUpStepsPerFrame = r.Integer("catchUpStepsPerFrame", 1, 600000),
            SilentBacklogSteps = r.Integer("silentBacklogSteps", 0, 600000),
        };
    }
}
