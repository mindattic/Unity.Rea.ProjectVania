using System;

namespace Unity.Rea.ProjectVania.Core.Commands
{
    /// <summary>
    /// Who issued a command. The original had one global current player; the rebuild seam is that
    /// every command names its player and nothing reads a current-player singleton.
    /// </summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        // Stored one-based so default(PlayerId) is the invalid, unset player.
        readonly int _stored;

        /// <summary>The zero-based player index (PlayerIndex in the original).</summary>
        public int Value => _stored - 1;

        /// <summary>False for default(PlayerId): a command that never named its player.</summary>
        public bool IsValid => _stored > 0;

        public PlayerId(int value) { _stored = value + 1; }

        /// <summary>The first spawned player (PlayerIndex 0 in the original).</summary>
        public static PlayerId One => new PlayerId(0);

        public bool Equals(PlayerId other) => _stored == other._stored;
        public override bool Equals(object obj) => obj is PlayerId p && Equals(p);
        public override int GetHashCode() => _stored;
        public override string ToString() => "Player " + Value;
        public static bool operator ==(PlayerId a, PlayerId b) => a._stored == b._stored;
        public static bool operator !=(PlayerId a, PlayerId b) => a._stored != b._stored;
    }
}
