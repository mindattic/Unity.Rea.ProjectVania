namespace Unity.Rea.ProjectVania.Core.Commands
{
    /// <summary>The game states of the original (RECONSTRUCTION 2).</summary>
    public enum GameState { Play, MainMenu, Console, MapEditor }

    /// <summary>Asks the rules to move to a game state. The first command, proving the command and event path.</summary>
    public readonly struct SetGameState : ICommand
    {
        public PlayerId Player { get; }
        public GameState State { get; }
        public SetGameState(PlayerId player, GameState state) { Player = player; State = state; }
    }

    public readonly struct GameStateChanged : IGameEvent
    {
        public long Tick { get; }
        public PlayerId Player { get; }
        public GameState From { get; }
        public GameState To { get; }
        public GameStateChanged(long tick, PlayerId player, GameState from, GameState to)
        { Tick = tick; Player = player; From = from; To = to; }
    }
}
