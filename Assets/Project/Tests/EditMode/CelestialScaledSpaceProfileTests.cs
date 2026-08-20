using Farion.Rendering.Celestial;
using NUnit.Framework;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class CelestialScaledSpaceProfileTests
    {
        [Test]
        public void DistanceMapping_IsContinuousMonotonicAndCompressed()
        {
            CelestialScaledSpaceProfile profile = ScriptableObject.CreateInstance<CelestialScaledSpaceProfile>();
            try
            {
                float transition = profile.TransitionDistance;
                float atTransition = profile.ResolveDisplayDistance(null, transition);
                float farther = profile.ResolveDisplayDistance(null, transition * 2f);

                Assert.That(atTransition, Is.EqualTo(transition).Within(0.001f));
                Assert.That(farther, Is.GreaterThan(atTransition));
                Assert.That(farther, Is.LessThan(transition * 2f));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
