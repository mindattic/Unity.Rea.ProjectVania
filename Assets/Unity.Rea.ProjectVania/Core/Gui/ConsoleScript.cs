using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Settings;

namespace Unity.Rea.ProjectVania.Core.Gui
{
    /// <summary>What the script reaches outside the console: the message stack, the fade lens, the songs, the program.</summary>
    public interface IScriptHost
    {
        bool Muted { get; }
        MusicState Music { get; }
        void Message(string text, SettingsColour colour);
        void Fade(FadeState fade);
        void PlaySong(string song);
        void PauseMusic();
        void ResumeMusic();
        void SetMuted(bool muted);
        void Quit();
    }

    /// <summary>
    /// "Ryan's Scripting Language", <c>ScriptManager.ExecuteScript</c> (RECONSTRUCTION 10.3), in the original's order:
    /// the line is lower-cased; the exact lines <c>play music</c>, <c>play battle</c>, <c>fade in|out|off</c> and
    /// <c>msg hi</c>; then the words (split on every space, so two spaces make an empty word) for <c>say</c> and
    /// <c>yell</c>; then a switch on the first word by the number of words. Anything it does not know does nothing.
    /// <para>
    /// Fixed (BIBLE defect 11): <c>console text|history|bg</c> and <c>commander x</c> with two words read a third word
    /// that is not there, and the original crashed with an <c>IndexOutOfRangeException</c>; here they write console.json's
    /// usage line to the history instead, and nothing the script is given can throw.
    /// </para>
    /// </summary>
    public static class ConsoleScript
    {
        /// <summary>The songs <c>play music</c> and <c>play battle</c> start (audio.json's <c>songs</c>).</summary>
        public const string MusicSong = "music";
        public const string BattleSong = "battle";

        public static void Execute(string line, GameConsole console, IScriptHost host)
        {
            var data = console.Data.Script;
            var script = (line ?? string.Empty).ToLowerInvariant();

            if (script == "play music") host.PlaySong(MusicSong);
            if (script == "play battle") host.PlaySong(BattleSong);

            if (script == "fade in") host.Fade(FadeState.In);
            if (script == "fade out") host.Fade(FadeState.Out);
            if (script == "fade off") host.Fade(FadeState.Off);

            if (script == "msg hi") host.Message(data.Hello, data.HelloColour);

            var words = script.Split(' ');

            if (words[0] == "say")
            {
                host.Message(data.Quote + Rest(words) + data.Quote, data.SayColour);
            }

            if (words[0] == "yell")
            {
                host.Message((data.Quote + Rest(words) + data.YellEnd + data.Quote).ToUpperInvariant(), data.YellColour);
            }

            switch (words.Length)
            {
                case 1:
                    Command(words[0], console, host);
                    break;
                case 2:
                    if (words[0] == "console") ConsoleObject(words, console);
                    else if (words[0] == "commander") Commander(words, console);
                    break; // run / execute: "Execute a predefined script", never written
                case 3:
                    if (words[0] == "console") ConsoleObject(words, console);
                    break;
                case 4:
                    if (words[0] == "commander") Commander(words, console);
                    break;
                case 5:
                    if (words[0] == "unit") UnitCollection(words, console);
                    break;
            }
        }

        /// <summary>The words after the first, joined by single spaces as they were split.</summary>
        static string Rest(string[] words) => words.Length > 1 ? string.Join(" ", words, 1, words.Length - 1) : string.Empty;

        // 1D expressions: 'Command'.
        static void Command(string command, GameConsole console, IScriptHost host)
        {
            var data = console.Data.Script;
            switch (command)
            {
                case "exit":
                case "quit":
                    host.Quit();
                    break;

                case "version":
                case "ver":
                    console.SetHistory(0, console.Data.Version);
                    break;

                // MediaPlayer.State == Playing ? Pause : Resume; the message says which, whether or not a song was there.
                case "pause":
                case "unpause":
                    if (host.Music == MusicState.Playing)
                    {
                        host.PauseMusic();
                        host.Message(data.MusicPaused, data.NoticeColour);
                    }
                    else
                    {
                        host.ResumeMusic();
                        host.Message(data.MusicUnpaused, data.NoticeColour);
                    }

                    break;

                case "mute":
                case "unmute":
                    host.SetMuted(!host.Muted);
                    host.Message(data.MutePrefix + (host.Muted ? data.On : data.Off), data.NoticeColour);
                    break;

                case "lee":
                case "buzzer":
                    for (var i = 0; i < data.Banner.Count; i++)
                    {
                        console.SetHistory(i, data.Banner[i]);
                    }

                    break;
            }
        }

        // 2D and 3D expressions on the console: 'console' 'Value' and 'console' 'Attribute' 'Value'.
        static void ConsoleObject(string[] words, GameConsole console)
        {
            var data = console.Data.Script;
            switch (words[1])
            {
                case "clear":
                    console.ClearHistory();
                    break;
                case "text":
                    if (words.Length < 3) console.Push(data.UsageConsoleText);
                    else TextColour(words[2], console);
                    break;
                case "history":
                    if (words.Length < 3) console.Push(data.UsageConsoleHistory);
                    else HistoryColour(words[2], console);
                    break;
                case "bg":
                    if (words.Length < 3) console.Push(data.UsageConsoleBg);
                    else Background(words[2], console);
                    break;
                case "full":
                    console.Full();
                    break;
            }
        }

        // 5D expressions: 'Collection' 'Object' 'Attribute' 'Parameter' 'Value'; unit reaches the console's parameters
        // with its second word (colUnit), and they read the third.
        static void UnitCollection(string[] words, GameConsole console)
        {
            switch (words[1])
            {
                case "text":
                    TextColour(words[2], console);
                    break;
                case "history":
                    HistoryColour(words[2], console);
                    break;
                case "bg":
                    Background(words[2], console);
                    break;
                case "full":
                    console.Full();
                    break;
            }
        }

        // objCommander: the x and y handlers are empty; with two words it read a third (the crash, fixed).
        static void Commander(string[] words, GameConsole console)
        {
            if (words.Length < 3)
            {
                console.Push(console.Data.Script.UsageCommander);
            }
        }

        static void TextColour(string word, GameConsole console)
        {
            if (console.Data.TryColour(word, out var colour)) console.SetTextColour(colour);
        }

        static void HistoryColour(string word, GameConsole console)
        {
            if (console.Data.TryColour(word, out var colour)) console.SetHistoryColour(colour);
        }

        static void Background(string word, GameConsole console)
        {
            var texture = console.Data.FindBackground(word);
            if (texture != null) console.SetTexture(texture);
        }
    }
}
