using Farion.Gameplay.Presentation.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class LightFixtureProfileTests
    {
        [Test]
        public void SteadyProfileHoldsFullPulseOutput()
        {
            LightFixtureProfile profile = CreateProfile();
            try
            {
                for (float time = 0f; time < 2f; time += 0.05f)
                {
                    Assert.That(profile.EvaluatePulse(time), Is.EqualTo(1f).Within(0.0001f));
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void BeaconPulseSweepsBetweenFloorAndFullOutput()
        {
            LightFixtureProfile profile = CreateProfile(
                ("pulseHz", 1.6f),
                ("pulseFloor", 0.08f),
                ("pulseSharpness", 2.5f));
            try
            {
                float minimum = float.MaxValue;
                float maximum = float.MinValue;
                for (float time = 0f; time < 2f; time += 0.002f)
                {
                    float value = profile.EvaluatePulse(time);
                    Assert.That(value, Is.InRange(0.08f - 0.0001f, 1f + 0.0001f));
                    minimum = Mathf.Min(minimum, value);
                    maximum = Mathf.Max(maximum, value);
                }

                Assert.That(minimum, Is.LessThan(0.2f));
                Assert.That(maximum, Is.GreaterThan(0.9f));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void FaultEnvelopeDecaysMonotonicallyToZero()
        {
            LightFixtureProfile profile = CreateProfile(("faultDecay", 6f));
            try
            {
                float previous = profile.EvaluateFaultEnvelope(1f, 0f);
                Assert.That(previous, Is.EqualTo(1f).Within(0.0001f));

                for (float time = 0.05f; time <= 2f; time += 0.05f)
                {
                    float current = profile.EvaluateFaultEnvelope(1f, time);
                    Assert.That(current, Is.LessThan(previous));
                    previous = current;
                }

                Assert.That(previous, Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void FaultMultiplierStaysWithinDepthAndVaries()
        {
            LightFixtureProfile profile = CreateProfile(
                ("faultFrequency", 22f),
                ("faultDepth", 0.85f));
            try
            {
                float minimum = float.MaxValue;
                float maximum = float.MinValue;
                for (float time = 0f; time < 5f; time += 0.01f)
                {
                    float value = profile.EvaluateFault(1f, time, 0.31f);
                    Assert.That(value, Is.InRange(0.15f - 0.0001f, 1f + 0.0001f));
                    minimum = Mathf.Min(minimum, value);
                    maximum = Mathf.Max(maximum, value);
                }

                Assert.That(maximum - minimum, Is.GreaterThan(0.3f));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void SettledFaultLeavesOutputUntouched()
        {
            LightFixtureProfile profile = CreateProfile();
            try
            {
                Assert.That(profile.EvaluateFault(0f, 1.23f, 0.5f), Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        static LightFixtureProfile CreateProfile(params (string Field, float Value)[] overrides)
        {
            LightFixtureProfile profile = ScriptableObject.CreateInstance<LightFixtureProfile>();
            if (overrides.Length == 0)
            {
                return profile;
            }

            SerializedObject serialized = new(profile);
            for (int i = 0; i < overrides.Length; i++)
            {
                SerializedProperty property = serialized.FindProperty(overrides[i].Field);
                Assert.That(property, Is.Not.Null, $"Missing serialized field {overrides[i].Field}.");
                property.floatValue = overrides[i].Value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }
    }
}
