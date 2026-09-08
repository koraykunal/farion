using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Farion.Core.Numerics;
using Farion.Gameplay.Character;
using Farion.Gameplay.Input;
using Farion.Simulation.Planetary;
using Farion.UI.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class ExplorerFeelTests
    {
        [Test]
        public void SpringConvergesToTarget()
        {
            float position = 0f;
            float velocity = 0f;
            for (int i = 0; i < 600; i++)
            {
                FarionMath.Spring(ref position, ref velocity, 1f, 180f, 12f, 1f / 60f);
            }

            Assert.That(position, Is.EqualTo(1f).Within(0.01f));
            Assert.That(velocity, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void SpringImpulseDecaysBackToRest()
        {
            float position = 0f;
            float velocity = -3f;
            float peakDip = 0f;
            for (int i = 0; i < 600; i++)
            {
                FarionMath.Spring(ref position, ref velocity, 0f, 180f, 12f, 1f / 60f);
                peakDip = Mathf.Min(peakDip, position);
            }

            Assert.That(peakDip, Is.LessThan(-0.05f));
            Assert.That(position, Is.EqualTo(0f).Within(0.005f));
            Assert.That(velocity, Is.EqualTo(0f).Within(0.005f));
        }

        [Test]
        public void SpringIgnoresNonPositiveDeltaTime()
        {
            float position = 0.5f;
            float velocity = 2f;
            FarionMath.Spring(ref position, ref velocity, 1f, 180f, 12f, 0f);
            FarionMath.Spring(ref position, ref velocity, 1f, 180f, 12f, -0.1f);

            Assert.That(position, Is.EqualTo(0.5f));
            Assert.That(velocity, Is.EqualTo(2f));
        }

        [Test]
        public void LandingFiresAfterRealFall()
        {
            bool landed = ExplorerLocomotionSignals.EvaluateLanding(
                wasGrounded: false,
                isGrounded: true,
                underwater: false,
                airborneSeconds: 0.6f,
                minimumAirborneSeconds: 0.15f,
                secondsSinceLastLanding: 5f,
                refireGuardSeconds: 0.2f);

            Assert.That(landed, Is.True);
        }

        [Test]
        public void LandingIgnoresStairPop()
        {
            bool landed = ExplorerLocomotionSignals.EvaluateLanding(
                wasGrounded: false,
                isGrounded: true,
                underwater: false,
                airborneSeconds: 0.05f,
                minimumAirborneSeconds: 0.15f,
                secondsSinceLastLanding: 5f,
                refireGuardSeconds: 0.2f);

            Assert.That(landed, Is.False);
        }

        [Test]
        public void LandingIgnoresRefireInsideGuard()
        {
            bool landed = ExplorerLocomotionSignals.EvaluateLanding(
                wasGrounded: false,
                isGrounded: true,
                underwater: false,
                airborneSeconds: 0.6f,
                minimumAirborneSeconds: 0.15f,
                secondsSinceLastLanding: 0.05f,
                refireGuardSeconds: 0.2f);

            Assert.That(landed, Is.False);
        }

        [Test]
        public void LandingIgnoresUnderwaterGrounding()
        {
            bool landed = ExplorerLocomotionSignals.EvaluateLanding(
                wasGrounded: false,
                isGrounded: true,
                underwater: true,
                airborneSeconds: 0.6f,
                minimumAirborneSeconds: 0.15f,
                secondsSinceLastLanding: 5f,
                refireGuardSeconds: 0.2f);

            Assert.That(landed, Is.False);
        }

        [Test]
        public void StrideEmitsOneStepPerStrideLength()
        {
            const float StrideLength = 0.8f;
            const float Speed = 4f;
            const float Duration = 8f;
            const float DeltaTime = 1f / 60f;

            float cycleMeters = 0f;
            int steps = 0;
            for (float elapsed = 0f; elapsed < Duration; elapsed += DeltaTime)
            {
                steps += ExplorerLocomotionSignals.AdvanceStride(
                    ref cycleMeters,
                    Speed,
                    0.5f,
                    StrideLength,
                    DeltaTime);
            }

            int expected = Mathf.RoundToInt(Speed * Duration / StrideLength);
            Assert.That(steps, Is.EqualTo(expected).Within(1));
        }

        [Test]
        public void StridePausesBelowMinimumSpeed()
        {
            float cycleMeters = 0.5f;
            int steps = ExplorerLocomotionSignals.AdvanceStride(
                ref cycleMeters,
                0.2f,
                0.5f,
                0.8f,
                1f / 60f);

            Assert.That(steps, Is.EqualTo(0));
            Assert.That(cycleMeters, Is.EqualTo(0.5f));
        }

        [Test]
        public void StridePhaseWrapsInsideFullCycle()
        {
            float cycleMeters = 0f;
            for (int i = 0; i < 1000; i++)
            {
                ExplorerLocomotionSignals.AdvanceStride(
                    ref cycleMeters,
                    6f,
                    0.5f,
                    0.8f,
                    1f / 60f);
            }

            Assert.That(cycleMeters, Is.GreaterThanOrEqualTo(0f));
            Assert.That(cycleMeters, Is.LessThan(1.6f));
        }

        [Test]
        public void ReducedMotionKeyMatchesUiPreferencesService()
        {
            string gameplayKey = ReadPrivateConst(
                typeof(PlayerViewPreferences),
                "ReducedMotionKey");
            string uiKey = ReadPrivateConst(
                typeof(UiPreferencesService),
                "ReducedMotionKey");

            Assert.That(gameplayKey, Is.Not.Null.And.Not.Empty);
            Assert.That(gameplayKey, Is.EqualTo(uiKey));
        }

        static string ReadPrivateConst(System.Type type, string fieldName)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Static);
            return field?.GetRawConstantValue() as string;
        }

        [Test]
        public void FootstepSurfaceEnumMatchesFmodSurfaceLabels()
        {
            string presetFolder = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "FMODProject/FarionAudio/Metadata/ParameterPreset"));
            string[] labels = null;
            foreach (string file in Directory.GetFiles(presetFolder, "*.xml"))
            {
                XDocument document = XDocument.Load(file);
                bool isSurface = document.Descendants("object")
                    .Where(o => (string)o.Attribute("class") == "ParameterPreset")
                    .SelectMany(o => o.Elements("property"))
                    .Any(p => (string)p.Attribute("name") == "name" &&
                        (string)p.Element("value") == "Surface");
                if (!isSurface)
                {
                    continue;
                }

                labels = document.Descendants("property")
                    .Where(p => (string)p.Attribute("name") == "enumerationLabels")
                    .SelectMany(p => p.Elements("value"))
                    .Select(v => v.Value)
                    .ToArray();
                break;
            }

            Assert.That(labels, Is.Not.Null, "FMOD Surface parameter preset not found.");
            Assert.That(labels, Is.EqualTo(System.Enum.GetNames(typeof(FootstepSurface))));
        }
    }
}
