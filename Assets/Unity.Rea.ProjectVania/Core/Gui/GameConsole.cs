using System;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;

namespace Unity.Rea.ProjectVania.Core.Gui
{
    /// <summary>
    /// The original's console window (<c>Gui\Console\Console.cs</c>, RECONSTRUCTION 10.1): the panel's top edge and
    /// height, the slide, the typed line, the 100-line history and the recall index, the two text colours, the panel's
    /// texture and the prompt's blink, every figure console.json's. The world changes it only while applying commands
    /// and in its GUI update; presentation reads it.
    /// </summary>
    public sealed class GameConsole
    {
        /// <summary>
        /// <c>(float)gameTime.ElapsedGameTime.TotalMilliseconds</c> with XNA's fixed step (16.6667), what the blink timer adds
        /// every frame.
        /// </summary>
        public static readonly float FrameMilliseconds = (float)(FixedStepper.XnaTargetElapsedTicks / 10_000.0);

        readonly ConsoleData _data;
        readonly string[] _history;
        float _timer;

        public GameConsole(ConsoleData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _history = new string[data.HistoryLines];
            for (var i = 0; i < _history.Length; i++)
            {
                _history[i] = string.Empty;
            }

            Height = data.Height;
            Y = data.StartY;
            Texture = data.Texture;
            TextColour = data.TextColour;
            HistoryColour = data.HistoryColour;
        }

        public ConsoleData Data => _data;

        /// <summary>The panel's top edge on screen (<c>Background.Rectangle.Y</c>): -200 hidden, 0 down.</summary>
        public int Y { get; private set; }

        /// <summary>The panel's height (<c>Background.Rectangle.Height</c>): 200, or the screen's after <c>console full</c>.</summary>
        public int Height { get; private set; }

        /// <summary>The panel's texture (<c>Background.Asset</c>): console1, or what <c>console bg</c> chose.</summary>
        public string Texture { get; private set; }

        /// <summary>Whether a slide is under way (<c>Sliding</c>); a toggle is ignored until it ends.</summary>
        public bool Sliding { get; private set; }

        /// <summary>Down, or sliding down (<c>Visible</c>); false hidden or sliding up.</summary>
        public bool Visible { get; private set; }

        /// <summary>The line being typed (<c>Text</c>).</summary>
        public string Text { get; private set; } = string.Empty;

        /// <summary>The history, newest first (<c>History</c>): line h is drawn h lines above the typed one.</summary>
        public System.Collections.Generic.IReadOnlyList<string> History => _history;

        /// <summary>The history line Up recalls next (<c>HistoryIndex</c>).</summary>
        public int HistoryIndex { get; private set; }

        /// <summary><c>Colors["ConsoleText"]</c>, the typed line's colour.</summary>
        public SettingsColour TextColour { get; private set; }

        /// <summary><c>Colors["ConsoleHistory"]</c>, the history's colour.</summary>
        public SettingsColour HistoryColour { get; private set; }

        /// <summary>The prompt's blink (<c>blink</c>): on, the caret is drawn and the prompt sits 2 px further right.</summary>
        public bool Blink { get; private set; }

        /// <summary>The cue <c>Command_ToggleConsole</c> plays: the long one once the panel is taller than its 200 px.</summary>
        public string ToggleCue => Height > _data.Height ? _data.LongCue : _data.ShortCue;

        /// <summary>Where the prompt is written: (6, height - 20) with the blink on, (4, height - 20) off.</summary>
        public int PromptX => Blink ? _data.PromptX : _data.PromptBlinkX;

        public int PromptY => Height - _data.PromptFromBottom;

        public int TextY => Height - _data.TextFromBottom;

        /// <summary>What is written after the prompt: the typed line, with the caret while the blink is on.</summary>
        public string Line => Blink ? Text + _data.Caret : Text;

        /// <summary>Where history line <paramref name="index"/> is written: (2, height - 34 - 14 * index).</summary>
        public int HistoryY(int index) => Height - _data.HistoryOffset - _data.HistoryLineHeight * index;

        /// <summary><c>ToggleConsole</c>: starts a slide the other way, unless one is under way.</summary>
        public void Toggle()
        {
            if (Sliding)
            {
                return;
            }

            Sliding = true;
            Visible = !Visible;
        }

        /// <summary>
        /// <c>Console.Update</c>, every frame in every state: the slide, then the blink timer. Returns the game state the
        /// slide sets: <see cref="SlideResult.Play"/> on every frame of a slide up, <see cref="SlideResult.Console"/> on the
        /// frame a slide down ends, else <see cref="SlideResult.None"/>.
        /// </summary>
        public SlideResult Update()
        {
            var result = SlideResult.None;
            if (Sliding && Visible)
            {
                result = SlideDown();
            }
            else if (Sliding)
            {
                result = SlideUp();
            }

            _timer += FrameMilliseconds;
            if (_timer > _data.BlinkMilliseconds)
            {
                Blink = !Blink;
                _timer = 0f;
            }

            return result;
        }

        // Down: Y += (int)(1 + 1 * (1 - Y / 16)) in integer arithmetic (14 px a frame at the top, easing to 2), until Y is
        // no longer negative; the next frame snaps it to 0, ends the slide and enters the Console state.
        SlideResult SlideDown()
        {
            if (Y < 0)
            {
                Y += _data.SlideDownStep + _data.SlideDownEase * (1 - Y / _data.SlideEaseDivisor);
                return SlideResult.None;
            }

            Sliding = false;
            Visible = true;
            Y = 0;
            return SlideResult.Console;
        }

        // Up: Play on every frame; 12 px a frame while above -height, then the slide ends hidden at -height.
        SlideResult SlideUp()
        {
            if (Y > -Height)
            {
                Y -= _data.SlideUpStep;
            }
            else
            {
                Sliding = false;
                Visible = false;
                Y = -Height;
            }

            return SlideResult.Play;
        }

        /// <summary>
        /// Enter (<c>CheckInput_Console</c>): the typed line goes to the top of the history (<c>AddConsoleTextToHistory</c>),
        /// the line is cleared and recall starts again from the newest. Returns the line, for the script to run before the
        /// line is cleared, as the original ran it.
        /// </summary>
        public string Submit()
        {
            var line = Text;
            Push(line);
            return line;
        }

        /// <summary>After the script ran: clear the line and restart recall from the newest (<c>Text = ""; HistoryIndex = 0</c>).</summary>
        public void FinishSubmit()
        {
            Text = string.Empty;
            HistoryIndex = 0;
        }

        /// <summary>Delete: clear the typed line.</summary>
        public void Clear() => Text = string.Empty;

        /// <summary>Backspace: remove the last character, if any.</summary>
        public void Backspace()
        {
            if (Text.Length > 0)
            {
                Text = Text.Substring(0, Text.Length - 1);
            }
        }

        /// <summary>Up: the line becomes history line <see cref="HistoryIndex"/>, and the index moves one older (to the last line at most).</summary>
        public void Recall()
        {
            Text = _history[HistoryIndex];
            if (HistoryIndex < _history.Length - 1)
            {
                HistoryIndex++;
            }
        }

        public void Append(string text)
        {
            Text += text ?? string.Empty;
        }

        /// <summary>Moves every history line one older (the oldest drops off) and writes <paramref name="line"/> as the newest.</summary>
        public void Push(string line)
        {
            for (var h = _history.Length - 1; h >= 1; h--)
            {
                _history[h] = _history[h - 1];
            }

            _history[0] = line ?? string.Empty;
        }

        /// <summary>Writes history line <paramref name="index"/> (<c>History[i] = ...</c>); an index past the history is ignored.</summary>
        public void SetHistory(int index, string line)
        {
            if (index >= 0 && index < _history.Length)
            {
                _history[index] = line ?? string.Empty;
            }
        }

        /// <summary><c>ClearHistory</c>: blanks every line but the newest (the echoed <c>console clear</c>; BIBLE defect 11b, kept).</summary>
        public void ClearHistory()
        {
            for (var h = _history.Length - 1; h >= 1; h--)
            {
                _history[h] = string.Empty;
            }
        }

        /// <summary><c>console full</c>: the panel as tall as the screen (<c>Viewport.Height</c>, console.json's fullHeight).</summary>
        public void Full() => Height = _data.FullHeight;

        public void SetTexture(string texture) => Texture = texture;

        public void SetTextColour(SettingsColour colour) => TextColour = colour;

        public void SetHistoryColour(SettingsColour colour) => HistoryColour = colour;
    }

    /// <summary>The game state a frame of the console's slide sets.</summary>
    public enum SlideResult
    {
        None,
        Play,
        Console,
    }
}
