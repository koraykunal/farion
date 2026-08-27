using Farion.Gameplay.Flight;
using Farion.Rendering.Celestial;
using Farion.Rendering.Lighting;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using NUnit.Framework;
using UnityEngine;
using Farion.Tests.Support;

namespace Farion.Tests.EditMode
{
    public sealed class OceanPhysicsTests
    {
        [Test]
        public void SubmergedFraction_MatchesSphericalCapVolume()
        {
            const float radius = 2f;

            Assert.AreEqual(0f, SpacecraftOceanInteractionProfile.CalculateSphereSubmergedFraction(0f, radius), 1e-5f);
            Assert.AreEqual(0.5f, SpacecraftOceanInteractionProfile.CalculateSphereSubmergedFraction(radius, radius), 1e-5f);
            Assert.AreEqual(1f, SpacecraftOceanInteractionProfile.CalculateSphereSubmergedFraction(radius * 2f, radius), 1e-5f);
        }

        [Test]
        public void SubmergedFraction_IsMonotonic()
        {
            const float radius = 1.5f;
            float previous = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float depth = radius * 2f * i / 20f;
                float fraction = SpacecraftOceanInteractionProfile.CalculateSphereSubmergedFraction(depth, radius);
                Assert.GreaterOrEqual(fraction, previous);
                previous = fraction;
            }
        }

        [Test]
        public void WaveHeight_StaysWithinAmplitude()
        {
            const float amplitude = 0.4f;
            Vector3 phases = OceanWaveField.GetPhases(123.456, 0.7f);

            for (int i = 0; i < 200; i++)
            {
                Vector3 position = new(i * 3.7f, i * -1.9f, i * 5.3f);
                float height = OceanWaveField.SampleHeight(position, 26f, amplitude, phases);
                Assert.LessOrEqual(Mathf.Abs(height), amplitude + 1e-4f);
            }
        }

        [Test]
        public void WaveHeight_IsZeroWithoutAmplitude()
        {
            Vector3 phases = OceanWaveField.GetPhases(10.0, 1f);
            Assert.AreEqual(0f, OceanWaveField.SampleHeight(new Vector3(4f, 5f, 6f), 26f, 0f, phases), 1e-6f);
        }

        [Test]
        public void WavePhases_StayWrapped()
        {
            Vector3 phases = OceanWaveField.GetPhases(1_000_000.0, 0.7f);
            for (int i = 0; i < OceanWaveField.WaveCount; i++)
            {
                Assert.LessOrEqual(Mathf.Abs(phases[i]), Mathf.PI * 2f + 1e-3f);
            }
        }

        [Test]
        public void WaveLength_UsesCrestToCrestScale()
        {
            const float waveLength = 26f;
            const float amplitude = 0.4f;
            Vector3 position = new(6.5f, -2.3f, 4.1f);
            Vector3 phases = new(0.2f, 1.1f, 2.4f);
            Vector3[] directions =
            {
                new Vector3(1f, 0f, 0.35f).normalized,
                new Vector3(-0.4f, 0.9f, 0.2f).normalized,
                new Vector3(0.3f, -0.5f, 0.8f).normalized
            };
            float[] frequencies = { 1f, 2.13f, 4.31f };
            float[] weights = { 1f, 0.55f, 0.3f };
            float expected = 0f;
            for (int i = 0; i < directions.Length; i++)
            {
                float phase = Vector3.Dot(position, directions[i]) *
                    (Mathf.PI * 2f / waveLength) * frequencies[i] + phases[i];
                expected += weights[i] * Mathf.Sin(phase);
            }

            expected *= amplitude / 1.85f;
            Assert.AreEqual(
                expected,
                OceanWaveField.SampleHeight(position, waveLength, amplitude, phases),
                1e-5f);
        }

        [Test]
        public void WaveVelocity_MatchesHeightDerivative()
        {
            const double time = 32.5;
            const double step = 0.001;
            const float speed = 0.7f;
            const float waveLength = 26f;
            const float amplitude = 0.35f;
            Vector3 position = new(8f, -3f, 11f);

            float before = OceanWaveField.SampleHeight(
                position,
                waveLength,
                amplitude,
                OceanWaveField.GetPhases(time - step, speed));
            float after = OceanWaveField.SampleHeight(
                position,
                waveLength,
                amplitude,
                OceanWaveField.GetPhases(time + step, speed));
            float finiteDifference = (float)((after - before) / (step * 2d));
            float velocity = OceanWaveField.SampleVerticalVelocity(
                position,
                waveLength,
                amplitude,
                speed,
                OceanWaveField.GetPhases(time, speed));

            Assert.AreEqual(finiteDifference, velocity, 1e-3f);
        }

        [Test]
        public void UnderwaterVisibilityDistance_LeavesFivePercentBlueLightAndAbsorbsWarmerLightFaster()
        {
            CelestialOceanProfile profile = ScriptableObject.CreateInstance<CelestialOceanProfile>();
            try
            {
                TestFieldAccess.SetField(profile, "underwaterVisibilityDistance", 50f);
                Vector3 extinction = profile.UnderwaterExtinctionCoefficients;
                Vector3 transmittance = new(
                    Mathf.Exp(-50f * extinction.x),
                    Mathf.Exp(-50f * extinction.y),
                    Mathf.Exp(-50f * extinction.z));

                Assert.AreEqual(0.05f, transmittance.z, 1e-4f);
                Assert.Less(transmittance.x, transmittance.y);
                Assert.Less(transmittance.y, transmittance.z);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void OceanEffectData_UsesDisplacedSurfaceForUnderwaterState()
        {
            CelestialOceanEffectData data = new(
                Vector3.zero,
                9f,
                10f,
                0.35f,
                26f,
                new Vector3(0.2f, 1.1f, 2.4f),
                Matrix4x4.identity,
                null);
            Vector3 direction = new Vector3(0.7f, 0.2f, -0.4f).normalized;
            float surfaceRadius = data.GetSurfaceRadiusAt(direction * 10f);
            for (int i = 0; i < 8; i++)
            {
                surfaceRadius = data.GetSurfaceRadiusAt(direction * surfaceRadius);
            }

            Assert.AreEqual(0f, data.GetSignedSurfaceDistance(direction * surfaceRadius), 1e-4f);
            Assert.Less(data.GetSignedSurfaceDistance(direction * (surfaceRadius - 0.01f)), 0f);
            Assert.Greater(data.GetSignedSurfaceDistance(direction * (surfaceRadius + 0.01f)), 0f);
            Assert.That(data.IsPointUnderwater(direction * (surfaceRadius - 0.01f)), Is.True);
            Assert.That(data.IsPointUnderwater(direction * (surfaceRadius + 0.01f)), Is.False);
        }

        [Test]
        public void UnderwaterSpotLight_OnlyExposesAnActiveSubmergedSpot()
        {
            GameObject gameObject = new("Underwater spot light");
            Light light = gameObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.intensity = 1800f;
            light.range = 25f;
            light.spotAngle = 40f;
            light.innerSpotAngle = 20f;
            UnderwaterSpotLight source = gameObject.AddComponent<UnderwaterSpotLight>();
            CelestialOceanProfile profile = ScriptableObject.CreateInstance<CelestialOceanProfile>();
            CelestialOceanEffectData ocean = new(
                Vector3.zero,
                9f,
                10f,
                0f,
                26f,
                Vector3.zero,
                Matrix4x4.identity,
                profile);

            try
            {
                gameObject.transform.position = Vector3.forward * 9f;
                Assert.That(source.TryBuildShaderData(ocean, Vector3.forward * 8f, out var data), Is.True);
                Assert.That(data.PositionRange.w, Is.EqualTo(25f));
                Assert.That(data.InnerConeCos, Is.GreaterThan(data.DirectionOuterCos.w));

                gameObject.transform.position = Vector3.forward * 11f;
                Assert.That(source.TryBuildShaderData(ocean, Vector3.forward * 8f, out _), Is.False);

                gameObject.transform.position = Vector3.forward * 9f;
                light.enabled = false;
                Assert.That(source.TryBuildShaderData(ocean, Vector3.forward * 8f, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AtmosphereEffectData_CarriesTheOceanSurfaceUsedForMediaTransitions()
        {
            CelestialOceanEffectData ocean = new(
                Vector3.zero,
                9f,
                10f,
                0.35f,
                26f,
                new Vector3(0.2f, 1.1f, 2.4f),
                Matrix4x4.identity,
                null);
            CelestialAtmosphereEffectData atmosphere = new(
                Vector3.zero,
                9f,
                10f,
                12f,
                null,
                ocean);

            Assert.That(atmosphere.HasOceanSurface, Is.True);
            Assert.That(atmosphere.OceanSurface.OceanRadius, Is.EqualTo(ocean.OceanRadius));
            Assert.That(atmosphere.OceanSurface.WavePhases, Is.EqualTo(ocean.WavePhases));
            Assert.That(atmosphere.OceanSurface.WorldToLocalRotation, Is.EqualTo(ocean.WorldToLocalRotation));
        }

        [Test]
        public void EnvironmentWaveField_CoRotatesWithBody()
        {
            GameObject gameObject = new("Ocean body");
            CelestialBody body = gameObject.AddComponent<CelestialBody>();
            try
            {
                Quaternion bodyRotation = Quaternion.Euler(17f, 63f, -9f);
                TestFieldAccess.SetField(
                    body,
                    "initialAngularVelocityDegreesPerSecond",
                    new Vector3(0f, 0.3f, 0f));
                body.transform.rotation = bodyRotation;
                body.Rigidbody.rotation = bodyRotation;
                body.ResetSimulationState();
                body.CaptureAnalyticReference(Vector3.zero, Vector3.zero, 0f, 0d);
                CelestialEnvironmentSample environment = new(
                    body,
                    true,
                    10f,
                    false,
                    0f,
                    new Vector2(9f, 11f),
                    10f,
                    0.35f,
                    26f,
                    0.7f,
                    18d);
                Vector3 localPosition = new(4f, 8f, -3f);
                Vector3 worldRelativePosition = bodyRotation * localPosition;
                float expected = 10f + OceanWaveField.SampleHeight(
                    localPosition,
                    environment.WaveLength,
                    environment.WaveAmplitude,
                    environment.WavePhases);

                Assert.AreEqual(expected, environment.GetOceanRadiusAt(worldRelativePosition), 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PointContact_DrivesInteractionWhenHullCenterIsAboveWater()
        {
            GameObject gameObject = new("Ocean body");
            CelestialBody body = gameObject.AddComponent<CelestialBody>();
            SpacecraftOceanInteractionProfile profile = ScriptableObject.CreateInstance<SpacecraftOceanInteractionProfile>();
            try
            {
                CelestialEnvironmentSample environment = new(
                    body,
                    true,
                    10f,
                    false,
                    0f,
                    new Vector2(9f, 11f),
                    10f);
                CelestialFrameSample frame = new(
                    body,
                    new Vector3(0f, 11f, 0f),
                    Vector3.zero,
                    Vector3.zero,
                    Vector3.zero,
                    Vector3.zero,
                    new Vector3(0f, 10f, 0f),
                    Vector3.up,
                    Vector3.down * 9.81f,
                    11f,
                    1f,
                    0f,
                    environment);
                Vector3 buoyancy = profile.EvaluateBuoyancyPoint(
                    frame,
                    new Vector3(0f, 9.5f, 0f),
                    out float pointSubmergedFraction);
                SpacecraftOceanInteractionSample interaction = profile.Evaluate(
                    frame,
                    pointSubmergedFraction,
                    buoyancy);

                Assert.That(frame.IsBelowOceanLevel, Is.False);
                Assert.That(interaction.IsTouchingWater, Is.True);
                Assert.That(interaction.BuoyancyAcceleration, Is.EqualTo(buoyancy));
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
