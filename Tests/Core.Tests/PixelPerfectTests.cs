using System;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Display;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    public class PixelPerfectTests
    {
        [Test]
        public void TheReferenceIs800By600()
        {
            Assert.That(PixelPerfect.ReferenceWidth, Is.EqualTo(800));
            Assert.That(PixelPerfect.ReferenceHeight, Is.EqualTo(600));
        }

        [Test]
        public void ExactlyTheReferenceFillsTheWindowAtScaleOne()
        {
            var v = PixelPerfect.Fit(800, 600);
            Assert.That((v.X, v.Y, v.Width, v.Height, v.Scale), Is.EqualTo((0, 0, 800, 600, 1)));
        }

        [Test]
        public void A1920By1080WindowMagnifiesByOneAndLetterboxes()
        {
            var v = PixelPerfect.Fit(1920, 1080);
            Assert.That(v.Scale, Is.EqualTo(1));
            Assert.That((v.X, v.Y, v.Width, v.Height), Is.EqualTo((560, 240, 800, 600)));
        }

        [Test]
        public void ALargeWindowTakesTheLargestWholeScaleOfTheTighterAxis()
        {
            var v = PixelPerfect.Fit(2400, 1300);
            Assert.That(v.Scale, Is.EqualTo(2));
            Assert.That((v.Width, v.Height), Is.EqualTo((1600, 1200)));
            Assert.That((v.X, v.Y), Is.EqualTo((400, 50)));
        }

        [Test]
        public void AWindowSmallerThanTheReferenceShrinksToFitKeepingFourByThree()
        {
            var v = PixelPerfect.Fit(400, 400);
            Assert.That(v.Scale, Is.EqualTo(0));
            Assert.That((v.Width, v.Height), Is.EqualTo((400, 300)));
            Assert.That((v.X, v.Y), Is.EqualTo((0, 50)));
        }

        [TestCase(0, 600)]
        [TestCase(800, 0)]
        [TestCase(-1, -1)]
        public void ANonPositiveWindowIsRejected(int w, int h)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PixelPerfect.Fit(w, h));
        }

        [Test]
        public void TheOrthographicSizeShowsSixHundredRows()
        {
            Assert.That(PixelPerfect.OrthographicSize(16f), Is.EqualTo(18.75f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PixelPerfect.OrthographicSize(0f));
        }
    }
}
