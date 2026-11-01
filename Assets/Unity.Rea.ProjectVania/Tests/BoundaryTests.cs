using System.Linq;
using NUnit.Framework;

namespace Unity.Rea.ProjectVania.Tests
{
    /// <summary>Core must stay engine-free: it may not reference UnityEngine or the other asmdefs.</summary>
    public class BoundaryTests
    {
        [Test]
        public void CoreReferencesNoUnityAssembly()
        {
            var core = typeof(Unity.Rea.ProjectVania.Core.Simulation.GameWorld).Assembly;
            var refs = core.GetReferencedAssemblies().Select(a => a.Name).ToList();
            Assert.That(refs.Any(n => n.StartsWith("UnityEngine") || n.StartsWith("UnityEditor")), Is.False);
            Assert.That(refs.Any(n => n.StartsWith("Unity.Rea.ProjectVania")), Is.False);
        }
    }
}
