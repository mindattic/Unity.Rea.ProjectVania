using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Gui;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    /// <summary>
    /// U.VAN-US-A12: the original's always-on HUD (RECONSTRUCTION 9.1): the quick bar U slides, the falling and fading
    /// message stack with its six start-up messages and the gamepad notice, the fade lens the fade commands drive, and the
    /// debug readout's text. The stack is replayed frame for frame against a literal copy of <c>MessageHandler.Update</c>.
    /// </summary>
    public class HudTests
    {
        static readonly PlayerId P1 = PlayerId.One;

        static GameSettings Shipped() => GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));

        static GameSettings Patched(Func<string, string> change)
        {
            var shipped = new DirectorySettingsSource(GameSettingsTests.ShippedFolder);
            return GameSettings.Load(new MemorySettingsSource(shipped).Set(GameSettings.HudFile, change(shipped.Read(GameSettings.HudFile))));
        }

        static GameWorld World(GameSettings s = null, int seed = 5)
        {
            s ??= Shipped();
            return new GameWorld(Map.FromSettings(s.Grid), s.Player, s.Characters.Find(s.Characters.DefaultCharacter), s.Names, new Random(seed), s.Camera, s.Menu,
                s.Console, s.Audio, s.Hud, s.Animations, s.Editor);
        }

        static void Steps(GameWorld world, int count)
        {
            for (var i = 0; i < count; i++)
            {
                world.Advance(1.0 / 60);
            }
        }

        static void Run(GameWorld world, string line)
        {
            world.Submit(new EditConsole(P1, ConsoleInput.Clear));
            world.Submit(new EditConsole(P1, ConsoleInput.Append, line));
            world.Submit(new EditConsole(P1, ConsoleInput.Submit));
            world.Advance(1.0 / 60);
        }

        /// <summary><c>MessageHandler</c> as the original wrote it (XNA's Vector2 and byte alpha as floats and an int).</summary>
        sealed class OriginalMessageHandler
        {
            public sealed class Message
            {
                public string Text;
                public float X;
                public float Y;
                public int A;
            }

            public readonly List<Message> messages = new List<Message>();
            float Timer;
            int lastMessage;
            const int margin = 16;
            const int speed = 50;

            public void AddMessage(string text) => messages.Add(new Message { Text = text, X = 800 / 2, Y = 600 / 2 - margin, A = 255 });

            public void InvokeMessage(string text)
            {
                if (messages.Count > 0)
                {
                    messages[0].Text = text;
                    messages[0].A = 255;
                }
            }

            public void Update()
            {
                if (messages.Count > 0)
                {
                    Timer += (float)TimeSpan.FromTicks(166667).TotalMilliseconds;
                    if (Timer > speed)
                    {
                        for (int m = 0; m <= messages.Count - 1; m++)
                        {
                            if (messages[m].Y + margin < messages[lastMessage].Y || m == 0)
                            {
                                messages[m].Y += 1;
                                messages[m].A -= 1;
                                if (messages[m].A == 0) { messages.RemoveAt(m); }
                                if (messages.Count > 0) { lastMessage = m; } else { lastMessage = 0; }
                            }
                        }

                        Timer = 0.0f;
                    }
                }
            }
        }

        // ---- settings ----

        [Test]
        public void HudJsonHoldsTheOriginalsFigures()
        {
            var hud = Shipped().Hud;
            Assert.That((hud.Screen.X, hud.Screen.Y), Is.EqualTo((800f, 600f)));
            Assert.That((hud.Portrait.X, hud.Portrait.FromBottom, hud.Portrait.Width, hud.Portrait.Height), Is.EqualTo((0, 110, 99, 110)));
            Assert.That(hud.Portrait.Layers.Select(l => (l.Asset, l.Alpha)), Is.EqualTo(new[] { ("portrait_glass", 0.4f), ("portrait_1", 1f), ("portrait_frame", 1f) }));
            Assert.That((hud.QuickBar.X, hud.QuickBar.FromBottom, hud.QuickBar.Width, hud.QuickBar.Height, hud.QuickBar.SlideStep), Is.EqualTo((103, 105, 261, 105, 3)));
            Assert.That(hud.QuickBar.Layers.Select(l => l.Asset), Is.EqualTo(new[] { "quick_bar_glass", "quick_bar_frame" }));
            Assert.That(hud.Messages.Startup.Select(l => l.Text), Is.EqualTo(new[] { "test1", "test2", "test3", "test4", "test5", "test6" }));
            Assert.That(hud.Messages.Startup.All(l => l.Colour == SettingsColour.Wheat));
            Assert.That(hud.Messages.GamepadNotice.Text, Is.EqualTo("XBox 360 Gamepad Connected"));
            Assert.That(hud.Messages.GamepadNotice.Colour.ToRgba(), Is.EqualTo(new Rgba(205, 133, 63, 255)), "XNA Color.Peru");
        }

        [Test]
        public void AHudScreenOtherThanTheReferenceIsRejected()
        {
            var e = Assert.Throws<SettingsException>(() => Patched(t => t.Replace("\"screen\": [800, 600]", "\"screen\": [640, 480]")));
            Assert.That(e.Errors.Single(), Does.Contain("hud.json").And.Contain("referenceScreen"));
        }

        // ---- the message stack ----

        [Test]
        public void SixStartUpMessagesQueueAtTheCentreInWheat()
        {
            var stack = World().Hud.Messages;
            Assert.That(stack.Messages.Select(m => m.Text), Is.EqualTo(new[] { "test1", "test2", "test3", "test4", "test5", "test6" }));
            Assert.That(stack.Messages.All(m => m.X == 400f && m.Y == 284f && m.Colour == new Rgba(245, 222, 179, 255)));
            Assert.That(stack.Messages.Any(stack.IsShown), Is.False, "written only below H / 2 + 16");
        }

        [Test]
        public void TheStackFallsAndFadesFrameForFrameAsTheOriginal()
        {
            var world = World();
            var original = new OriginalMessageHandler();
            for (var i = 1; i <= 6; i++) original.AddMessage("test" + i);

            for (var frame = 1; frame <= 3000; frame++)
            {
                if (frame == 600)
                {
                    // A message added mid-way (msg hi, run by the input handler before the GUI updates) queues behind the others.
                    Run(world, "msg hi");
                    original.AddMessage("Hello!");
                }
                else
                {
                    world.Advance(1.0 / 60);
                }

                original.Update();

                var ours = world.Hud.Messages.Messages;
                Assert.That(ours.Count, Is.EqualTo(original.messages.Count), $"frame {frame}");
                for (var m = 0; m < ours.Count; m++)
                {
                    Assert.That((ours[m].Text, ours[m].Y, (int)ours[m].Colour.A), Is.EqualTo((original.messages[m].Text, original.messages[m].Y, original.messages[m].A)),
                        $"frame {frame}, message {m}");
                }
            }

            Assert.That(world.Hud.Messages.Messages, Is.Empty, "all gone long before frame 3000");
        }

        [Test]
        public void AMessageMovesEveryThirdFrameAndShowsAfter33Moves()
        {
            var world = World();
            var first = world.Hud.Messages.Messages[0];
            Steps(world, 2);
            Assert.That(first.Y, Is.EqualTo(284f), "16.6667 ms twice is not past 50");
            Steps(world, 1);
            Assert.That((first.Y, (int)first.Colour.A), Is.EqualTo((285f, 254)));
            Assert.That(world.Hud.Messages.Messages[1].Y, Is.EqualTo(284f), "the next waits until the first is 16 px ahead");

            Steps(world, 3 * 32 - 3);
            Assert.That(world.Hud.Messages.IsShown(first), Is.False);
            Steps(world, 3);
            Assert.That(first.Y, Is.EqualTo(317f));
            Assert.That(world.Hud.Messages.IsShown(first), Is.True, "after 33 moves (1.65 s)");

            Steps(world, 3 * 254 - 99);
            Assert.That(world.Hud.Messages.Messages[0], Is.SameAs(first));
            Steps(world, 3);
            Assert.That(world.Hud.Messages.Messages[0], Is.Not.SameAs(first), "removed at alpha 0, after 255 moves (12.75 s)");
        }

        [Test]
        public void TheScriptsMessagesJoinTheStack()
        {
            var world = World();
            Steps(world, 2000);
            Assert.That(world.Hud.Messages.Messages, Is.Empty);
            Run(world, "say hello there");
            Run(world, "yell run");
            Assert.That(world.Hud.Messages.Messages.Select(m => (m.Text, m.Colour)), Is.EqualTo(new[]
            {
                ("''hello there''", new Rgba(255, 255, 0, 255)),
                ("''RUN!''", new Rgba(255, 0, 0, 255)),
            }));
        }

        [Test]
        public void AConnectedPadRewritesTheFrontMessageInPeru()
        {
            var world = World();
            Steps(world, 30);
            var front = world.Hud.Messages.Messages[0];
            var y = front.Y;
            world.Submit(new AnnounceGamepad(P1));
            Steps(world, 1);
            Assert.That(front.Text, Is.EqualTo("XBox 360 Gamepad Connected"));
            Assert.That(front.Colour, Is.EqualTo(new Rgba(205, 133, 63, 255)), "opaque again: InvokeMessage sets the whole colour");
            Assert.That(front.Y, Is.EqualTo(y), "it keeps its place");
            Assert.That(world.Hud.Messages.Messages, Has.Count.EqualTo(6));
        }

        [Test]
        public void ANoticeThatRevivesTheFrontMessageOutlivesTheRestWithoutACrash()
        {
            // The original threw here: the front message, opaque again, outlives the others, and once the last of them was
            // removed its stale index pointed past the list.
            var original = new OriginalMessageHandler();
            for (var i = 1; i <= 6; i++) original.AddMessage("test" + i);
            for (var frame = 0; frame < 300; frame++) original.Update();
            original.InvokeMessage("XBox 360 Gamepad Connected");
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                for (var frame = 0; frame < 3000; frame++) original.Update();
            }, "the original's crash");

            var world = World();
            Steps(world, 300);
            world.Submit(new AnnounceGamepad(P1));
            Assert.DoesNotThrow(() => Steps(world, 3000));
            Assert.That(world.Hud.Messages.Messages, Is.Empty);
        }

        [Test]
        public void WithNothingQueuedTheNoticeIsQueuedUnlessTheOriginalIsAskedFor()
        {
            var world = World();
            Steps(world, 2000);
            world.Submit(new AnnounceGamepad(P1));
            Steps(world, 1);
            Assert.That(world.Hud.Messages.Messages.Single().Text, Is.EqualTo("XBox 360 Gamepad Connected"));

            var original = World(Patched(t => t.Replace("\"noticeWhenEmpty\": true", "\"noticeWhenEmpty\": false")));
            Steps(original, 2000);
            original.Submit(new AnnounceGamepad(P1));
            Steps(original, 1);
            Assert.That(original.Hud.Messages.Messages, Is.Empty, "InvokeMessage did nothing with no message");
        }

        [Test]
        public void TheStackKeepsMovingWhileTheMenuFreezesTheWorld()
        {
            var world = World();
            world.Submit(new ToggleMainMenu(P1));
            Steps(world, 30);
            Assert.That(world.State, Is.EqualTo(GameState.MainMenu));
            Assert.That(world.Hud.Messages.Messages[0].Y, Is.EqualTo(294f));
        }

        // ---- the fade lens ----

        [Test]
        public void FadeOutDarkensOneStepAFrameAndHoldsBlack()
        {
            var world = World();
            Run(world, "fade out");
            Assert.That((world.Fade, world.Hud.Lens.Alpha), Is.EqualTo((FadeState.Out, (byte)1)), "the lens updates in the step the line ran");
            Steps(world, 254);
            Assert.That(world.Hud.Lens.Alpha, Is.EqualTo(255));
            Assert.That(world.Hud.Lens.Colour, Is.EqualTo(new Rgba(0, 0, 0, 255)));
            Steps(world, 100);
            Assert.That((world.Fade, world.Hud.Lens.Alpha), Is.EqualTo((FadeState.Out, (byte)255)), "BIBLE defect 13 fixed: it holds black");

            Run(world, "fade in");
            Assert.That(world.Hud.Lens.Alpha, Is.EqualTo(254));
            Steps(world, 254);
            Assert.That((world.Fade, world.Hud.Lens.Alpha), Is.EqualTo((FadeState.In, (byte)0)));
            Steps(world, 1);
            Assert.That(world.Fade, Is.EqualTo(FadeState.Off), "In goes Off once clear");
        }

        [Test]
        public void TheOriginalsFadeOutFlashesClearWhenItFinishes()
        {
            var world = World(Patched(t => t.Replace("\"holdBlackAfterFadeOut\": true", "\"holdBlackAfterFadeOut\": false")));
            Run(world, "fade out");
            Steps(world, 254);
            Assert.That(world.Hud.Lens.Alpha, Is.EqualTo(255));
            Steps(world, 1);
            Assert.That((world.Fade, world.Hud.Lens.Alpha), Is.EqualTo((FadeState.Off, (byte)255)));
            Steps(world, 1);
            Assert.That(world.Hud.Lens.Alpha, Is.EqualTo(0), "Off resets the tint the next frame");
        }

        [Test]
        public void FadeOffClearsAtOnceAndFadeInFromClearDoesNothingVisible()
        {
            var world = World();
            Run(world, "fade out");
            Steps(world, 100);
            Run(world, "fade off");
            Assert.That((world.Fade, world.Hud.Lens.Alpha), Is.EqualTo((FadeState.Off, (byte)0)));
            Run(world, "fade in");
            Assert.That((world.Fade, world.Hud.Lens.Alpha), Is.EqualTo((FadeState.Off, (byte)0)));
        }

        // ---- the quick bar ----

        [Test]
        public void USlidesTheQuickBarOffTheBottomAndBack()
        {
            var world = World();
            var bar = world.Hud.QuickBar;
            Assert.That((bar.X, bar.Y, bar.Visible, bar.Sliding), Is.EqualTo((103, 495, true, false)));

            world.Submit(new ToggleQuickBar(P1));
            Steps(world, 1);
            Assert.That((bar.Y, bar.Visible, bar.Sliding), Is.EqualTo((498, false, true)));
            world.Submit(new ToggleQuickBar(P1));
            Steps(world, 34);
            Assert.That((bar.Y, bar.Sliding), Is.EqualTo((600, true)), "a toggle mid-slide is ignored");
            Steps(world, 1);
            Assert.That((bar.Y, bar.Sliding, bar.Visible), Is.EqualTo((600, false, false)));

            world.Submit(new ToggleQuickBar(P1));
            Steps(world, 35);
            Assert.That((bar.Y, bar.Sliding), Is.EqualTo((495, true)));
            Steps(world, 1);
            Assert.That((bar.Y, bar.Sliding, bar.Visible), Is.EqualTo((495, false, true)));
        }

        // ---- the debug readout ----

        [Test]
        public void TheReadoutWritesThePlayerCameraAndCellAsTheOriginalConcatenatedThem()
        {
            var world = World();
            var data = world.Hud.Data.Readout;
            var name = world.Driven(P1).Name;
            Assert.That(HudReadout.Player(world, P1), Is.EqualTo(name + "\n500, 100\n31, 6"));
            Assert.That(HudReadout.Camera(world, P1, data), Is.EqualTo("Mode: LoosePlayer\n1, 1"));
            Assert.That(HudReadout.Cell(world, P1, data), Is.EqualTo("Coords:0, 0"), "the editor's selected cell before it ever opened");

            // Once the camera has eased onto the character, the editor opens: its first step puts the cursor on the view's
            // centre plus half the hand (6, 9), and the readout writes the cell under it.
            Steps(world, 180);
            world.Submit(new ToggleMapEditor(P1));
            Steps(world, 1);
            var camera = world.Camera(P1).Position;
            var under = world.Map.GetGridCoordinates(new PixelVector(-camera.X + 6, -camera.Y + 9));
            Assert.That(under.X, Is.GreaterThan(20), "the view is on the character, between the walls");
            Assert.That(HudReadout.Cell(world, P1, data), Is.EqualTo("Coords:" + under.X + ", " + under.Y), "the cell under the editor's cursor");
            Assert.That(HudReadout.Camera(world, P1, data), Does.StartWith("Mode: Free\n"));

            world.Submit(new SetEditorCursor(P1, EditorCursorMode.Screen));
            world.Submit(new PointEditor(P1, 100f, 100f, false, false));
            Steps(world, 1);
            Assert.That(HudReadout.Cell(world, P1, data), Is.EqualTo("Coords:" + under.X + ", " + under.Y), "Screen mode leaves the selected cell as it was");
        }

        [TestCase(500f, "500")]
        [TestCase(523.33333f, "523.3333")]
        [TestCase(-0f, "0")]
        [TestCase(-1.5f, "-1.5")]
        [TestCase(0.1f, "0.1")]
        public void NumbersAreWrittenAsDotNet35WroteAFloat(float value, string text)
        {
            Assert.That(HudReadout.Number(value), Is.EqualTo(text));
        }

        [Test]
        public void TheOriginalsFrameRateAlwaysRead59Point99988()
        {
            Assert.That(HudReadout.FrameRate(HudReadout.OriginalFrameRate), Is.EqualTo("59.99988"));
        }

        [TestCase(-5f, 700f, 0f, 582f)]
        [TestCase(900f, 10f, 787f, 10f)]
        [TestCase(120f, 45f, 120f, 45f)]
        public void TheGuiCursorStaysInsideTheScreen(float mx, float my, float x, float y)
        {
            Assert.That(HudReadout.ClampCursor(mx, my, Shipped().Hud), Is.EqualTo((x, y)));
            Assert.That(HudReadout.Cursor(x, y), Is.EqualTo(HudReadout.Number(x) + ", " + HudReadout.Number(y)));
        }

        [Test]
        public void AWorldWithoutAHudStillNamesMessagesAndFades()
        {
            var world = new GameWorld(null, Shipped().Console);
            Assert.That(world.Hud, Is.Null);
            Run(world, "fade out");
            Assert.That(world.Fade, Is.EqualTo(FadeState.Out));
            world.Submit(new ToggleQuickBar(P1));
            world.Submit(new AnnounceGamepad(P1));
            Assert.DoesNotThrow(() => world.Advance(1.0 / 60));
        }
    }
}
