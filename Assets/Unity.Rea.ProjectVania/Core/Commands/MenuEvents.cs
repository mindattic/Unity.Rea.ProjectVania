namespace Unity.Rea.ProjectVania.Core.Commands
{
    /// <summary>
    /// A sound cue to play (<c>Audio.PlayAmbience</c> / <c>PlaySfx</c>): presentation looks <see cref="Cue"/> up in
    /// audio.json's cue table and plays it. The world names a cue only while the game is not muted (<see cref="Simulation.GameWorld.Muted"/>).
    /// </summary>
    public readonly struct CueRequested : IGameEvent
    {
        public long Tick { get; }
        public string Cue { get; }
        public CueRequested(long tick, string cue) { Tick = tick; Cue = cue; }
    }

    /// <summary>
    /// The menu's Quit, or the console's <c>exit</c>, was chosen (<c>Game.Exit()</c>): presentation ends the program.
    /// <see cref="Player"/> is who chose it: online (U.VAN-T31) only that player's program ends.
    /// </summary>
    public readonly struct QuitRequested : IGameEvent
    {
        public long Tick { get; }
        public PlayerId Player { get; }
        public QuitRequested(long tick, PlayerId player) { Tick = tick; Player = player; }
    }
}
