namespace Unity.Rea.ProjectVania.Core.Commands
{
    /// <summary>The only way game state changes. Always carries who issued it.</summary>
    public interface ICommand
    {
        PlayerId Player { get; }
    }

    /// <summary>A fact about something that happened in a step; presentation plays these.</summary>
    public interface IGameEvent
    {
        /// <summary>The step (tick) in which it happened.</summary>
        long Tick { get; }
    }
}
