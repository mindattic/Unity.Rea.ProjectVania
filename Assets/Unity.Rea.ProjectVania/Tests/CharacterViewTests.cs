using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Characters;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.ProjectVania.Tests
{
    /// <summary>
    /// The characters on screen (U.VAN-US-A7, RECONSTRUCTION 4.1, 4.8): every strip of every sheet loads and is cut into
    /// animations.json's frames, each character is a sprite at its feet flipped by its facing, its name is written over
    /// it in red and yellow, and [ / ] move the camera to the other character.
    /// </summary>
    public class CharacterViewTests
    {
        static string SettingsFolder => Path.Combine(Application.dataPath, "StreamingAssets", GameSettings.Folder);

        GameHost _host;

        [SetUp]
        public void SetUp()
        {
            _host = Bootstrap.Run(new DirectorySettingsSource(SettingsFolder));
            Assert.That(_host, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            var root = GameObject.Find(Bootstrap.RootName);
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EveryStripOfEverySheetLoadsAsItsFrames()
        {
            var settings = _host.Settings;
            var art = new CharacterArt(settings.Animations, settings.Grid.PixelsPerUnit);
            foreach (var sheet in settings.Animations.Sheets)
            {
                foreach (var clip in sheet.Animations)
                {
                    var frames = art.Frames(sheet.Sheet, clip);
                    Assert.That(frames, Is.Not.Null, $"{sheet.Sheet}/{clip.Name}");
                    Assert.That(frames.Length, Is.EqualTo(clip.Frames), $"{sheet.Sheet}/{clip.Name}");
                    Assert.That(frames[0].texture.width, Is.EqualTo(sheet.Find(clip.Strip).Width), $"{sheet.Sheet}/{clip.Strip} is imported unscaled");
                    Assert.That(frames[0].texture.filterMode, Is.EqualTo(FilterMode.Point));
                    for (var i = 0; i < frames.Length; i++)
                    {
                        Assert.That(frames[i].rect, Is.EqualTo(new Rect(clip.SourceFrame(i) * 64, 0, 64, 64)), "a borrowed clip cuts the frames it lists");
                        Assert.That(frames[i].pivot, Is.EqualTo(new Vector2(32, 0)), "bottom centre");
                        Assert.That(frames[i].pixelsPerUnit, Is.EqualTo(settings.Grid.PixelsPerUnit));
                    }
                }
            }
        }

        [Test]
        public void EachCharacterIsASpriteAtItsFeetInListOrder()
        {
            var views = _host.GetComponentsInChildren<CharacterView>();
            Assert.That(views.Length, Is.EqualTo(_host.World.Characters.Count));
            Assert.That(views.Length, Is.EqualTo(3), "Morris, Shanoa and Leo (U.VAN-T25)");
            var ppu = _host.Settings.Grid.PixelsPerUnit;
            for (var i = 0; i < views.Length; i++)
            {
                var view = views[i];
                Assert.That(view.Index, Is.EqualTo(i));
                Assert.That(view.Renderer.sortingOrder, Is.EqualTo(i), "a later character draws over an earlier one");
                Assert.That(view.Renderer.sprite, Is.Not.Null);
                Assert.That(view.Animator.Clip.Name, Is.EqualTo(CharacterAnimator.Fall), "spawned falling");
                var feet = view.Character.Position;
                Assert.That(view.transform.position.x, Is.EqualTo(feet.X / ppu).Within(1e-4f));
                Assert.That(view.transform.position.y, Is.EqualTo(-feet.Y / ppu).Within(1e-4f));
            }
        }

        [Test]
        public void TheSpriteFollowsTheActionAndFacing()
        {
            var view = _host.GetComponentsInChildren<CharacterView>()[0];
            Step(120);
            view.Present(0f);
            Assert.That(view.Animator.Clip.Name, Is.EqualTo(CharacterAnimator.Idle));
            Assert.That(view.Renderer.flipX, Is.False, "the art faces left; facing neither way does not flip");

            for (var i = 0; i < 5; i++)
            {
                _host.World.Submit(new MoveCharacter(PlayerId.One, 1));
                Step(1);
            }

            view.Present(0f);
            Assert.That(view.Animator.Clip.Name, Is.EqualTo(CharacterAnimator.Run));
            Assert.That(view.Renderer.flipX, Is.True, "facing right flips the left-facing art");

            view.Present(0.06f);
            Assert.That(view.Animator.Frame, Is.EqualTo(1));
            Assert.That(view.Renderer.sprite.rect.x, Is.EqualTo(64f));
        }

        [Test]
        public void ShanoaAndLeoDrawTheirOwnSheetsAndLeosJumpShowsHisRunStripsCrouchThenStride()
        {
            var views = _host.GetComponentsInChildren<CharacterView>();
            Assert.That(views.Select(v => v.Animator.Sheet.Sheet), Is.EqualTo(new[] { "morris", "shanoa", "leo" }));
            Step(120);

            // ] ] drives Leo; he jumps: the borrowed jump clip cuts run frame 0, then run frame 2.
            _host.World.Submit(new CycleCharacter(PlayerId.One, 1));
            _host.World.Submit(new CycleCharacter(PlayerId.One, 1));
            Step(1);
            _host.World.Submit(new JumpCharacter(PlayerId.One));
            Step(1);
            var leo = views[2];
            leo.Present(0f);
            Assert.That(leo.Animator.Clip.Name, Is.EqualTo(CharacterAnimator.Jump));
            Assert.That(leo.Renderer.sprite.texture.name, Is.EqualTo("run"));
            Assert.That(leo.Renderer.sprite.rect.x, Is.EqualTo(0f));
            leo.Present(0.11f);
            Assert.That(leo.Renderer.sprite.rect.x, Is.EqualTo(128f));

            // [ steps back to Shanoa, who kneels with her own (borrowed) kneel frame.
            _host.World.Submit(new CycleCharacter(PlayerId.One, -1));
            Step(1);
            Assert.That(_host.World.DrivenIndex(PlayerId.One), Is.EqualTo(1));
            _host.World.Submit(new KneelCharacter(PlayerId.One));
            Step(1);
            var shanoa = views[1];
            shanoa.Present(0f);
            Assert.That(shanoa.Animator.Clip.Name, Is.EqualTo(CharacterAnimator.Kneel));
            Assert.That(shanoa.Renderer.sprite.rect.x, Is.EqualTo(4 * 64f));
        }

        [Test]
        public void EveryNameIsWrittenRedThenYellowOverItsCharacter()
        {
            var tags = _host.GetComponentInChildren<NameTags>();
            var characters = _host.World.Characters;
            Assert.That(tags.Labels.Count, Is.EqualTo(characters.Count * 2));
            Assert.That(tags.Layer.parent, Is.Not.Null);

            var tag = _host.Settings.Characters.NameTag;
            for (var i = 0; i < characters.Count; i++)
            {
                var red = tags.Labels[i * 2];
                var yellow = tags.Labels[i * 2 + 1];
                Assert.That(characters[i].Name, Is.Not.Empty);
                Assert.That(_host.Settings.Names.For(characters[i].Definition.Gender), Does.Contain(characters[i].Name));
                Assert.That(red.text, Is.EqualTo(characters[i].Name));
                Assert.That(yellow.text, Is.EqualTo(characters[i].Name));
                Assert.That(red.style.color.value, Is.EqualTo(new Color(1f, 0f, 0f, 1f)));
                Assert.That(yellow.style.color.value, Is.EqualTo(new Color(1f, 1f, 0f, 1f)));
                Assert.That(yellow.style.fontSize.value.value, Is.EqualTo(13f), "Arial 10 at 96 dpi");

                // Red at (X - 1, Y - 100), yellow at (X, Y), both lifted 64 px by the Top alignment.
                Assert.That(red.style.left.value.value, Is.EqualTo(yellow.style.left.value.value - 1f).Within(1e-3f));
                Assert.That(red.style.top.value.value, Is.EqualTo(yellow.style.top.value.value - 100f).Within(1e-3f));
            }

            // Once the Loose camera has settled on player one's character, its yellow name is centred on x 400 with its
            // top 64 px above the feet, and the twin's 50 px to the right.
            Step(600);
            _host.GetComponentInChildren<GameCamera>().Present();
            tags.Place();
            var feet = characters[0].Position;
            var centre = _host.World.Camera(PlayerId.One).Centre;
            Assert.That(tags.Labels[1].style.left.value.value, Is.EqualTo(400f + feet.X - centre.X).Within(0.05f));
            Assert.That(tags.Labels[1].style.top.value.value, Is.EqualTo(300f + feet.Y - centre.Y - tag.TopAlignLift).Within(0.05f));
            Assert.That(feet.X - centre.X, Is.EqualTo(0f).Within(0.5f));
            Assert.That(tags.Labels[3].style.left.value.value - tags.Labels[1].style.left.value.value, Is.EqualTo(50f).Within(0.05f), "the twin 50 px to the right");
        }

        [Test]
        public void TheBracketsMoveTheCameraToTheOtherCharacter()
        {
            var follow = _host.GetComponentInChildren<GameCamera>();
            var ppu = _host.Settings.Grid.PixelsPerUnit;
            Step(600);
            follow.Present();
            Assert.That(follow.transform.position.x, Is.EqualTo(500f / ppu).Within(0.5f / ppu));

            _host.World.Submit(new CycleCharacter(PlayerId.One, 1));
            Step(600);
            follow.Present();
            Assert.That(_host.World.DrivenIndex(PlayerId.One), Is.EqualTo(1));
            Assert.That(follow.transform.position.x, Is.EqualTo(550f / ppu).Within(0.5f / ppu));
            Assert.That(follow.transform.position.z, Is.EqualTo(-10f));
        }

        void Step(int steps)
        {
            // Whole steps with nothing left in the accumulator, so the interpolated position is the stepped one.
            for (var i = 0; i < steps; i++)
            {
                _host.World.Advance(1.0 / 60);
            }

            Assert.That(_host.World.Alpha, Is.EqualTo(0.0));
        }
    }
}
