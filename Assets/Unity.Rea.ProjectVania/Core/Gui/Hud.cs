using System;
using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Settings;

namespace Unity.Rea.ProjectVania.Core.Gui
{
    /// <summary>
    /// The original's always-on HUD state (RECONSTRUCTION 9.1): the quick bar's slide, the fade lens and the message
    /// stack, every figure hud.json's. The portrait never changes and the debug readout is read from the world
    /// (<see cref="HudReadout"/>), so neither keeps state here. The world changes it only while applying commands and in
    /// its GUI update; presentation reads it.
    /// </summary>
    public sealed class Hud
    {
        public Hud(HudData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            QuickBar = new QuickBar(data.QuickBar, (int)data.Screen.Y);
            Lens = new FadeLens(data.Lens);
            Messages = new MessageStack(data.Messages, (int)data.Screen.X, (int)data.Screen.Y);
            foreach (var line in data.Messages.Startup)
            {
                Messages.Add(line.Text, line.Colour.ToRgba());
            }
        }

        public HudData Data { get; }
        public QuickBar QuickBar { get; }
        public FadeLens Lens { get; }
        public MessageStack Messages { get; }

        /// <summary>
        /// <c>GuiManager.Update</c>'s HUD part, every frame in every state, in its order: the quick bar, then the lenses,
        /// then the messages.
        /// </summary>
        public void Update()
        {
            QuickBar.Update();
            Lens.Update();
            Messages.Update();
        }
    }

    /// <summary>
    /// <c>QuickBar</c>: its top edge on screen and its slide. U toggles it unless a slide is under way; it slides 3 px a
    /// frame down until its top reaches the screen's bottom edge, or up until it is back at H - 105.
    /// </summary>
    public sealed class QuickBar
    {
        readonly HudQuickBarData _data;
        readonly int _screenHeight;

        public QuickBar(HudQuickBarData data, int screenHeight)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _screenHeight = screenHeight;
            Visible = data.StartsVisible;
            Y = Visible ? Shown : screenHeight;
        }

        /// <summary>The bar's top edge (<c>glass.Rectangle.Y</c>): H - 105 shown, H hidden.</summary>
        public int Y { get; private set; }

        public int X => _data.X;

        /// <summary>Shown, or sliding up (<c>Visible</c>).</summary>
        public bool Visible { get; private set; }

        public bool Sliding { get; private set; }

        int Shown => _screenHeight - _data.FromBottom;

        /// <summary><c>ToggleQuickBar</c>: starts a slide the other way, unless one is under way.</summary>
        public void Toggle()
        {
            if (Sliding)
            {
                return;
            }

            Sliding = true;
            Visible = !Visible;
        }

        /// <summary><c>QuickBar.Update</c>: the slide up while showing, then (a separate test, as the original's) the slide down while hiding.</summary>
        public void Update()
        {
            if (Sliding && Visible)
            {
                if (Y > Shown)
                {
                    Y -= _data.SlideStep;
                }
                else
                {
                    Sliding = false;
                    Visible = true;
                    Y = Shown;
                }
            }

            if (Sliding && !Visible)
            {
                if (Y < _screenHeight)
                {
                    Y += _data.SlideStep;
                }
                else
                {
                    Sliding = false;
                    Visible = false;
                    Y = _screenHeight;
                }
            }
        }
    }

    /// <summary>
    /// <c>Lenses</c>' <c>fade</c> lens: the screen tinted black at <see cref="Alpha"/>. Every time its timer passes 10 ms
    /// (every frame at 60 Hz) <c>CheckFade</c> runs: Out darkens by 1 to 255, In clears by 1 to 0 and then goes Off, Off
    /// keeps it clear. BIBLE defect 13 fixed: a finished fade out holds black (the original went Off and flashed clear).
    /// </summary>
    public sealed class FadeLens
    {
        readonly HudLensData _data;
        float _timer;

        public FadeLens(HudLensData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary><c>Lenses.Fade</c>, which <c>fade in|out|off</c> set.</summary>
        public FadeState State { get; private set; }

        /// <summary>The lens' alpha (<c>lens["fade"].Color.A</c>), 0 clear .. 255 black.</summary>
        public byte Alpha { get; private set; }

        /// <summary>The lens' tint, [r, g, b] at <see cref="Alpha"/>.</summary>
        public Rgba Colour => new Rgba(_data.R, _data.G, _data.B, Alpha);

        public void Set(FadeState state) => State = state;

        /// <summary><c>Lenses.UpdateTimer</c>: the frame's 16.6667 ms onto the float timer; past the step, <c>CheckFade</c> and restart it.</summary>
        public void Update()
        {
            _timer += GameConsole.FrameMilliseconds;
            if (_timer > _data.StepMilliseconds)
            {
                CheckFade();
                _timer = 0f;
            }
        }

        void CheckFade()
        {
            switch (State)
            {
                case FadeState.Out:
                    if (Alpha < 255)
                    {
                        Alpha = (byte)Math.Min(255, Alpha + _data.AlphaStep);
                    }
                    else if (!_data.HoldBlackAfterFadeOut)
                    {
                        State = FadeState.Off;
                    }

                    break;
                case FadeState.In:
                    if (Alpha > 0)
                    {
                        Alpha = (byte)Math.Max(0, Alpha - _data.AlphaStep);
                    }
                    else
                    {
                        State = FadeState.Off;
                    }

                    break;
                default:
                    Alpha = 0;
                    break;
            }
        }
    }

    /// <summary>One line of the message stack (<c>Message</c>): its text, where it is written and its colour, whose alpha fades.</summary>
    public sealed class HudMessage
    {
        internal HudMessage(string text, float x, float y, Rgba colour)
        {
            Text = text;
            X = x;
            Y = y;
            Colour = colour;
        }

        public string Text { get; internal set; }
        public float X { get; }
        public float Y { get; internal set; }
        public Rgba Colour { get; internal set; }
    }

    /// <summary>
    /// <c>MessageHandler</c>: messages queue at (W / 2, H / 2 - 16); every time the timer passes 50 ms (every third
    /// frame) each message in order falls 1 px and fades by 1 if it is the first or is 16 px behind the one that last
    /// moved, and is removed at alpha 0, so they drift down as a falling, fading stack. A message is written only once it
    /// is below H / 2 + 16. Defect fixed: the original kept the index of the last message moved across ticks and threw
    /// when that message had been the last one removed (possible once a pad's notice had restored the front message's
    /// alpha); here the index is held inside the stack.
    /// </summary>
    public sealed class MessageStack
    {
        readonly HudMessageData _data;
        readonly List<HudMessage> _messages = new List<HudMessage>();
        readonly float _startX;
        readonly float _startY;
        readonly float _shownBelow;
        float _timer;
        int _last;

        public MessageStack(HudMessageData data, int screenWidth, int screenHeight)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _startX = screenWidth / 2;
            _startY = screenHeight / 2 - data.Margin;
            _shownBelow = screenHeight / 2 + data.Margin;
        }

        /// <summary>The queued messages, oldest (front) first.</summary>
        public IReadOnlyList<HudMessage> Messages => _messages;

        /// <summary>Whether <paramref name="message"/> is written (<c>Position.Y &gt; H / 2 + margin</c>).</summary>
        public bool IsShown(HudMessage message) => message != null && message.Y > _shownBelow;

        /// <summary><c>AddMessage</c>: queues a line at the start position.</summary>
        public void Add(string text, Rgba colour) => _messages.Add(new HudMessage(text ?? string.Empty, _startX, _startY, colour));

        /// <summary>
        /// <c>InvokeMessage</c>: rewrites the front message's text and colour (so it is opaque again). With nothing queued
        /// the original did nothing; with hud.json's noticeWhenEmpty the line is queued instead.
        /// </summary>
        public void Invoke(string text, Rgba colour)
        {
            if (_messages.Count > 0)
            {
                _messages[0].Text = text ?? string.Empty;
                _messages[0].Colour = colour;
            }
            else if (_data.NoticeWhenEmpty)
            {
                Add(text, colour);
            }
        }

        /// <summary><c>MessageHandler.Update</c>: only while a message is queued, the frame's 16.6667 ms onto the float timer; past 50 ms, one tick.</summary>
        public void Update()
        {
            if (_messages.Count == 0)
            {
                return;
            }

            _timer += GameConsole.FrameMilliseconds;
            if (!(_timer > _data.StepMilliseconds))
            {
                return;
            }

            for (var m = 0; m <= _messages.Count - 1; m++)
            {
                if (_last >= _messages.Count)
                {
                    _last = _messages.Count - 1;
                }

                var message = _messages[m];
                if (message.Y + _data.Margin < _messages[_last].Y || m == 0)
                {
                    message.Y += _data.Fall;
                    var c = message.Colour;
                    message.Colour = new Rgba(c.R, c.G, c.B, (byte)Math.Max(0, c.A - _data.FadeStep));
                    if (message.Colour.A == 0)
                    {
                        _messages.RemoveAt(m);
                    }

                    _last = _messages.Count > 0 ? m : 0;
                }
            }

            _timer = 0f;
        }
    }
}
