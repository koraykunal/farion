using Farion.Rendering.Celestial;
using Farion.Rendering.Lighting;
using NUnit.Framework;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class CelestialAtmosphericAmbientTests
    {
        const float SurfaceRadius = 1000f;
        const float AtmosphereRadius = 1250f;

        CelestialAtmosphereProfile atmosphereProfile;
        CelestialLightingProfile lightingProfile;

        [SetUp]
        public void SetUp()
        {
            atmosphereProfile = ScriptableObject.CreateInstance<CelestialAtmosphereProfile>();
            lightingProfile = ScriptableObject.CreateInstance<CelestialLightingProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(atmosphereProfile);
            Object.DestroyImmediate(lightingProfile);
        }

        CelestialAtmosphereEffectData CreateAtmosphere()
        {
            return new CelestialAtmosphereEffectData(
                Vector3.zero,
                SurfaceRadius,
                SurfaceRadius,
                AtmosphereRadius,
                atmosphereProfile);
        }

        bool Sample(Vector3 focus, Vector3 directionToStar, out AtmosphericAmbientSample sample)
        {
            return CelestialAtmosphericAmbient.TrySample(
                CreateAtmosphere(),
                focus,
                directionToStar,
                Color.white,
                1.1f,
                lightingProfile,
                out sample);
        }

        static float Luminance(Color color)
        {
            return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        }

        [Test]
        public void NoonOnSurface_SkyIsLitBlueAndBrighterThanGround()
        {
            bool result = Sample(new Vector3(0f, SurfaceRadius, 0f), Vector3.up, out AtmosphericAmbientSample sample);

            Assert.That(result, Is.True);
            Assert.That(sample.DensityFactor, Is.GreaterThan(0.9f));
            Assert.That(Luminance(sample.Sky), Is.GreaterThan(0.05f));
            Assert.That(sample.Sky.b, Is.GreaterThan(sample.Sky.r));
            Assert.That(Luminance(sample.Ground), Is.LessThan(Luminance(sample.Sky)));
            Assert.That(sample.Up, Is.EqualTo(Vector3.up));
        }

        [Test]
        public void MidnightOnSurface_SkyIsDark()
        {
            bool result = Sample(new Vector3(0f, SurfaceRadius, 0f), Vector3.down, out AtmosphericAmbientSample sample);

            Assert.That(result, Is.True);
            Assert.That(Luminance(sample.Sky), Is.LessThan(0.01f));
            Assert.That(Luminance(sample.Ground), Is.LessThan(0.01f));
        }

        [Test]
        public void DuskOnSurface_SkyShiftsWarm()
        {
            Sample(new Vector3(0f, SurfaceRadius, 0f), Vector3.up, out AtmosphericAmbientSample noon);
            Sample(new Vector3(0f, SurfaceRadius, 0f), Vector3.forward, out AtmosphericAmbientSample dusk);

            Assert.That(noon.Sky.b, Is.GreaterThan(noon.Sky.r));
            Assert.That(dusk.Sky.r, Is.GreaterThan(dusk.Sky.b));
        }

        [Test]
        public void AboveAtmosphere_SampleIsRejected()
        {
            bool result = Sample(new Vector3(0f, AtmosphereRadius + 100f, 0f), Vector3.up, out _);

            Assert.That(result, Is.False);
        }
    }
}
