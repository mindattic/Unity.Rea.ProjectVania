using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Simulation;

namespace Unity.Rea.ProjectVania.Core.Tests
{
    public class FixedStepperTests
    {
        [Test]
        public void SixtyFramesOfOneSixtiethAreExactlySixtySteps()
        {
            var s = new FixedStepper();
            int total = 0;
            for (int i = 0; i < 60; i++) total += s.Advance(1.0 / 60);
            Assert.That(total, Is.EqualTo(60));
            Assert.That(s.AccumulatorUnits, Is.EqualTo(0));
        }

        [Test]
        public void ThirtyHertzRenderingRunsTwoStepsPerFrame()
        {
            Assert.That(new FixedStepper().Advance(1.0 / 30), Is.EqualTo(2));
        }

        [Test]
        public void ASlowFrameCarriesItsRemainderAndAlphaInterpolates()
        {
            var s = new FixedStepper();
            Assert.That(s.Advance(0.025), Is.EqualTo(1));
            Assert.That(s.Alpha, Is.EqualTo(0.5).Within(1e-6));
            Assert.That(s.Advance(0.0083333333), Is.EqualTo(1));
        }

        [Test]
        public void AStallIsCappedAtHalfASecond()
        {
            Assert.That(new FixedStepper().Advance(30), Is.EqualTo(30));
        }

        [TestCase(0.0)]
        [TestCase(-1.0)]
        [TestCase(double.NaN)]
        public void NonPositiveTimeStepsNothing(double dt)
        {
            Assert.That(new FixedStepper().Advance(dt), Is.EqualTo(0));
        }
    }
}
