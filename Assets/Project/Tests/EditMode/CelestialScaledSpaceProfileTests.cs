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

        [Test]
        public void ScaledSpaceActivation_UsesSurfaceClearanceInsteadOfBodyCenterDistance()
        {
            CelestialScaledSpaceProfile profile = ScriptableObject.CreateInstance<CelestialScaledSpaceProfile>();
            try
            {
                const float Radius = 16000f;
                float transition = profile.TransitionDistance;

                Assert.That(
                    profile.ShouldUseScaledSpace(Radius, Radius, false),
                    Is.False);
                Assert.That(
                    profile.ShouldUseScaledSpace(Radius + transition + 1f, Radius, false),
                    Is.True);
                Assert.That(
                    profile.ResolveDisplayDistance(null, Radius + transition, Radius),
                    Is.EqualTo(Radius + transition).Within(0.001f));
                Assert.That(
                    profile.ResolveDisplayDistance(null, Radius + transition * 2f, Radius),
                    Is.LessThan(Radius + transition * 2f));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
