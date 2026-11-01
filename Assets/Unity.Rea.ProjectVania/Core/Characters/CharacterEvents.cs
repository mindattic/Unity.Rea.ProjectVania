using Unity.Rea.ProjectVania.Core.Settings;

namespace Unity.Rea.ProjectVania.Core.Characters
{
    /// <summary>
    /// A character's action changed over a step (commands and update together), so presentation can switch its
    /// animation and cues. <see cref="Character"/> is the index into <c>GameWorld.Characters</c>.
    /// </summary>
    public readonly struct CharacterActionChanged : Commands.IGameEvent
    {
        public long Tick { get; }
        public int Character { get; }
        public ActionKind From { get; }
        public ActionKind To { get; }

        public CharacterActionChanged(long tick, int character, ActionKind from, ActionKind to)
        {
            Tick = tick;
            Character = character;
            From = from;
            To = to;
        }
    }

    /// <summary>
    /// [ or ] moved <see cref="Player"/> from driving character <see cref="From"/> to character <see cref="To"/> (indices
    /// into <c>GameWorld.Characters</c>), so the camera can follow the new one.
    /// </summary>
    public readonly struct DrivenCharacterChanged : Commands.IGameEvent
    {
        public long Tick { get; }
        public Commands.PlayerId Player { get; }
        public int From { get; }
        public int To { get; }

        public DrivenCharacterChanged(long tick, Commands.PlayerId player, int from, int to)
        {
            Tick = tick;
            Player = player;
            From = from;
            To = to;
        }
    }
}
