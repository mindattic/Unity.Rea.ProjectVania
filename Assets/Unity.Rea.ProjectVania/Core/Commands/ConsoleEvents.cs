using Unity.Rea.ProjectVania.Core.Display;

namespace Unity.Rea.ProjectVania.Core.Commands
{
    // What the console's script asks of the rest of the game (ScriptManager.ExecuteScript, RECONSTRUCTION 10.3). The
    // world keeps the state each one changes (Fade, Muted, Music) and names the change as an event; the message stack and
    // the fade lens are drawn by the HUD story (A12) and the songs played by the audio story (A13).

    /// <summary>The original's <c>g.FadeState</c>: the full-screen fade lens (RECONSTRUCTION 9.1).</summary>
    public enum FadeState
    {
        Off,
        In,
        Out,
    }

    /// <summary>Whether a song is playing (the original's <c>MediaPlayer.State</c>, which <c>pause</c> reads).</summary>
    public enum MusicState
    {
        Stopped,
        Playing,
        Paused,
    }

    /// <summary>A line for the message stack (<c>Gui.MessageHandler.AddMessage(text, colour)</c>).</summary>
    public readonly struct MessageRequested : IGameEvent
    {
        public long Tick { get; }
        public string Text { get; }
        public Rgba Colour { get; }
        public MessageRequested(long tick, string text, Rgba colour) { Tick = tick; Text = text; Colour = colour; }
    }

    /// <summary><c>fade in|out|off</c> set the lens' fade (<c>Gui.Lenses.Fade</c>).</summary>
    public readonly struct FadeRequested : IGameEvent
    {
        public long Tick { get; }
        public FadeState Fade { get; }
        public FadeRequested(long tick, FadeState fade) { Tick = tick; Fade = fade; }
    }

    /// <summary><c>play music|battle</c>: start the named song from its beginning (<c>MediaPlayer.Play</c>), not looping.</summary>
    public readonly struct SongRequested : IGameEvent
    {
        public long Tick { get; }
        public string Song { get; }
        public SongRequested(long tick, string song) { Tick = tick; Song = song; }
    }

    /// <summary><c>pause</c>: the playing song paused (true) or resumed (false) (<c>MediaPlayer.Pause</c> / <c>Resume</c>).</summary>
    public readonly struct MusicPauseChanged : IGameEvent
    {
        public long Tick { get; }
        public bool Paused { get; }
        public MusicPauseChanged(long tick, bool paused) { Tick = tick; Paused = paused; }
    }

    /// <summary><c>mute</c>: the game's sound muted or not (<c>Audio.Muted</c>; a song keeps playing at volume 0).</summary>
    public readonly struct MuteChanged : IGameEvent
    {
        public long Tick { get; }
        public bool Muted { get; }
        public MuteChanged(long tick, bool muted) { Tick = tick; Muted = muted; }
    }

    /// <summary>The song presentation was playing ran to its end (MediaPlayer's state back to Stopped).</summary>
    public readonly struct EndSong : ICommand
    {
        public PlayerId Player { get; }
        public EndSong(PlayerId player) { Player = player; }
    }
}
