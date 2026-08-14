using Farion.Audio.Spacecraft;
using Farion.Gameplay.Actors;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Input;
using Farion.Gameplay.Presentation.Flight;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using NUnit.Framework;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class SpacecraftFlightControlTests
    {
        [Test]
        public void AssistedControlCompensatesGravityAtZeroVelocity()
        {
            SpacecraftFlightControlOutput output = SpacecraftFlightControlLaw.Evaluate(
                Frame(
                    SpacecraftPilotCommand.None,
                    Vector3.zero,
                    Vector3.zero,
                    new Vector3(0f, -9.81f, 0f),
                    assisted: true),
                Settings());

            Assert.That(output.LocalLinearAcceleration.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(output.LocalLinearAcceleration.y, Is.EqualTo(9.81f).Within(0.0001f));
            Assert.That(output.LocalLinearAcceleration.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void ManualControlDoesNotHideGravityWithNoPilotInput()
        {
            SpacecraftFlightControlOutput output = SpacecraftFlightControlLaw.Evaluate(
                Frame(
                    SpacecraftPilotCommand.None,
                    Vector3.zero,
                    Vector3.zero,
                    new Vector3(0f, -9.81f, 0f),
                    assisted: false),
                Settings());

            Assert.That(output.LocalLinearAcceleration, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ManualEnvelopeStopsAddingSpeedAtEveryAxisLimit()
        {
            SpacecraftPilotCommand command = new(
                Vector3.one,
                Vector3.one,
                boost: false,
                brake: false,
                toggleFlightAssist: false);
            SpacecraftFlightControlOutput output = SpacecraftFlightControlLaw.Evaluate(
                Frame(
                    command,
                    new Vector3(45f, 40f, 180f),
                    new Vector3(65f, 42f, 95f) * Mathf.Deg2Rad,
                    Vector3.zero,
                    assisted: false),
                Settings());

            Assert.That(output.LocalLinearAcceleration, Is.EqualTo(Vector3.zero));
            Assert.That(output.LocalAngularAcceleration, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ManualEnvelopeKeepsFullCounterThrustAuthority()
        {
            SpacecraftPilotCommand command = new(
                -Vector3.one,
                -Vector3.one,
                boost: false,
                brake: false,
                toggleFlightAssist: false);
            SpacecraftFlightControlOutput output = SpacecraftFlightControlLaw.Evaluate(
                Frame(
                    command,
                    new Vector3(45f, 40f, 180f),
                    new Vector3(65f, 42f, 95f) * Mathf.Deg2Rad,
                    Vector3.zero,
                    assisted: false),
                Settings());

            Assert.That(output.LocalLinearAcceleration, Is.EqualTo(new Vector3(-12f, -16f, -18f)));
            Assert.That(
                output.LocalAngularAcceleration,
                Is.EqualTo(new Vector3(-180f, -140f, -240f) * Mathf.Deg2Rad));
        }

        [Test]
        public void AssistedReverseCommandUsesConfiguredReverseAuthority()
        {
            SpacecraftPilotCommand command = new(
                new Vector3(0f, 0f, -1f),
                Vector3.zero,
                boost: false,
                brake: false,
                toggleFlightAssist: false);
            SpacecraftFlightControlOutput output = SpacecraftFlightControlLaw.Evaluate(
                Frame(command, Vector3.zero, Vector3.zero, Vector3.zero, assisted: true),
                Settings());

            Assert.That(output.LocalLinearAcceleration.z, Is.EqualTo(-18f));
        }

        [Test]
        public void AssistedBrakeAppliesMaximumCounterThrust()
        {
            SpacecraftPilotCommand command = new(
                Vector3.zero,
                Vector3.zero,
                boost: false,
                brake: true,
                toggleFlightAssist: false);
            SpacecraftFlightControlOutput output = SpacecraftFlightControlLaw.Evaluate(
                Frame(command, new Vector3(0f, 0f, 100f), Vector3.zero, Vector3.zero, assisted: true),
                Settings());

            Assert.That(output.LocalLinearAcceleration.z, Is.EqualTo(-18f));
        }

        [Test]
        public void SimultaneousAxesAreNotNormalizedAgainstEachOther()
        {
            SpacecraftInputState input = new(
                new Vector3(1f, 1f, 1f),
                Vector2.zero,
                roll: 0f,
                boost: false);

            Assert.That(input.Translation, Is.EqualTo(Vector3.one));

            SpacecraftThrusterVfxFrame vfxFrame = new(
                throttle: 1f,
                boost: 0f,
                heat: 0f,
                atmosphereDensity: 0f,
                relativeSpeed: 0f,
                localTranslation: Vector3.one,
                localRotation: Vector3.one,
                localLinearAcceleration: Vector3.zero,
                localAngularAcceleration: Vector3.zero,
                thrusters: SpacecraftThrusterCommand.None);
            Assert.That(vfxFrame.LocalTranslation, Is.EqualTo(Vector3.one));
            Assert.That(vfxFrame.LocalRotation, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void GroundProximityRisesOnlyNearAMeasuredSurface()
        {
            Assert.That(
                SpacecraftThrusterVfxController.CalculateGroundProximity(0f, 40f),
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                SpacecraftThrusterVfxController.CalculateGroundProximity(20f, 40f),
                Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(
                SpacecraftThrusterVfxController.CalculateGroundProximity(400f, 40f),
                Is.Zero);
            Assert.That(
                SpacecraftThrusterVfxController.CalculateGroundProximity(float.PositiveInfinity, 40f),
                Is.Zero);
            Assert.That(
                SpacecraftThrusterVfxController.CalculateGroundProximity(float.NaN, 40f),
                Is.Zero);
        }

        [Test]
        public void IgnitionFlareSpikesOnThrottleRiseAndDecaysAfterwards()
        {
            float flare = SpacecraftThrusterNozzleVfx.AdvanceIgnitionFlare(
                currentFlare: 0f,
                loadDelta: 0.4f,
                deltaTime: 0.02f,
                gain: 0.32f,
                decay: 4.5f);
            Assert.That(flare, Is.GreaterThan(0.5f));

            float steady = SpacecraftThrusterNozzleVfx.AdvanceIgnitionFlare(
                currentFlare: flare,
                loadDelta: 0f,
                deltaTime: 0.02f,
                gain: 0.32f,
                decay: 4.5f);
            Assert.That(steady, Is.LessThan(flare));

            float released = SpacecraftThrusterNozzleVfx.AdvanceIgnitionFlare(
                currentFlare: 0.05f,
                loadDelta: -0.4f,
                deltaTime: 0.02f,
                gain: 0.32f,
                decay: 4.5f);
            Assert.That(released, Is.Zero);
        }

        [Test]
        public void RearMainVfxIgnoresTranslationThatDoesNotProduceForwardExhaust()
        {
            SpacecraftThrusterCommand reverse = new(
                0f, 1f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f, 0f);
            SpacecraftThrusterCommand strafe = new(
                0f, 0f, 1f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f, 0f);
            SpacecraftThrusterCommand vertical = new(
                0f, 0f, 0f, 0f, 1f, 0f,
                0f, 0f, 0f, 0f, 0f, 0f);

            Assert.That(SpacecraftThrusterVfxController.CalculateMainThrustDemand(reverse), Is.Zero);
            Assert.That(SpacecraftThrusterVfxController.CalculateMainThrustDemand(strafe), Is.Zero);
            Assert.That(SpacecraftThrusterVfxController.CalculateMainThrustDemand(vertical), Is.Zero);
        }

        [Test]
        public void RearMainVfxUsesForwardThrustAndRestrainedAngularStabilization()
        {
            SpacecraftThrusterCommand forward = new(
                1f, 0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f, 0f);
            SpacecraftThrusterCommand yaw = new(
                0f, 0f, 0f, 0f, 0f, 0f,
                0f, 0f, 1f, 0f, 0f, 0f);

            Assert.That(
                SpacecraftThrusterVfxController.CalculateMainThrustDemand(forward),
                Is.EqualTo(1f));
            Assert.That(
                SpacecraftThrusterVfxController.CalculateMainThrustDemand(yaw),
                Is.EqualTo(0.18f).Within(0.0001f));
        }

        [Test]
        public void RearMainVfxAccelerationLoadUsesOnlyPositiveForwardAxis()
        {
            Assert.That(
                SpacecraftThrusterVfxController.CalculateForwardAccelerationLoad(
                    new Vector3(30f, 30f, 0f),
                    20f),
                Is.Zero);
            Assert.That(
                SpacecraftThrusterVfxController.CalculateForwardAccelerationLoad(
                    new Vector3(0f, 0f, -30f),
                    20f),
                Is.Zero);
            Assert.That(
                SpacecraftThrusterVfxController.CalculateForwardAccelerationLoad(
                    new Vector3(0f, 0f, 10f),
                    20f),
                Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void AtmosphereInteractionProducesMonotonicLoadsAndSuppressesSubmergedDrag()
        {
            GameObject bodyObject = new("Atmosphere Test Body");
            CelestialBody body = bodyObject.AddComponent<CelestialBody>();
            SpacecraftAtmosphereInteractionProfile profile =
                ScriptableObject.CreateInstance<SpacecraftAtmosphereInteractionProfile>();

            try
            {
                CelestialFrameSample upperFrame = AtmosphereFrame(
                    body,
                    centerDistance: 55f,
                    speed: 200f,
                    oceanRadius: 0f);
                CelestialFrameSample surfaceFrame = AtmosphereFrame(
                    body,
                    centerDistance: 50f,
                    speed: 200f,
                    oceanRadius: 0f);
                CelestialFrameSample submergedFrame = AtmosphereFrame(
                    body,
                    centerDistance: 50f,
                    speed: 200f,
                    oceanRadius: 51f);

                SpacecraftAtmosphereInteractionSample upper = profile.Evaluate(upperFrame);
                SpacecraftAtmosphereInteractionSample surface = profile.Evaluate(surfaceFrame);
                SpacecraftAtmosphereInteractionSample submerged = profile.Evaluate(submergedFrame);

                Assert.That(surface.AtmosphereDensity, Is.GreaterThan(upper.AtmosphereDensity));
                Assert.That(surface.DynamicPressure, Is.GreaterThan(upper.DynamicPressure));
                Assert.That(surface.HeatingRate, Is.GreaterThan(upper.HeatingRate));
                Assert.That(
                    Vector3.Dot(
                        surface.DragAcceleration,
                        surfaceFrame.SurfaceRelativeVelocity),
                    Is.LessThan(0f));
                Assert.That(surface.DragAcceleration.magnitude, Is.LessThanOrEqualTo(60f));
                Assert.That(submerged.IsInsideAtmosphere, Is.False);
                Assert.That(submerged.DragAcceleration.sqrMagnitude, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(bodyObject);
            }
        }

        [Test]
        public void BoostAudioTransitionFiresOnlyOnStateEdges()
        {
            Assert.That(
                ShipAudioController.EvaluateBoostTransition(false, false),
                Is.EqualTo(ShipAudioController.BoostTransition.None));
            Assert.That(
                ShipAudioController.EvaluateBoostTransition(false, true),
                Is.EqualTo(ShipAudioController.BoostTransition.Ignition));
            Assert.That(
                ShipAudioController.EvaluateBoostTransition(true, true),
                Is.EqualTo(ShipAudioController.BoostTransition.None));
            Assert.That(
                ShipAudioController.EvaluateBoostTransition(true, false),
                Is.EqualTo(ShipAudioController.BoostTransition.Shutdown));
        }

        [Test]
        public void GamepadLookScalingDoesNotDependOnFrameDelta()
        {
            Vector2 first = FarionInputActions.ScaleGamepadLook(Vector2.one, 120f);
            Vector2 second = FarionInputActions.ScaleGamepadLook(Vector2.one, 120f);

            Assert.That(first, Is.EqualTo(Vector2.one));
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void MouseLookScalingPreservesAccumulatedPhysicalMotionAcrossFrameRates()
        {
            Vector2 physicalMotion = new(120f, -60f);
            Vector2 expected = physicalMotion * 1.5f;

            foreach (int frameCount in new[] { 30, 60, 120 })
            {
                Vector2 accumulated = Vector2.zero;
                Vector2 frameMotion = physicalMotion / frameCount;
                for (int frame = 0; frame < frameCount; frame++)
                {
                    accumulated += FarionInputActions.ScaleMouseLook(frameMotion, 1.5f);
                }

                Assert.That(Vector2.Distance(accumulated, expected), Is.LessThan(0.001f));
            }
        }

        [Test]
        public void BoostRequiresReleaseAfterDepletionAndThenRecharges()
        {
            SpacecraftBoostController controller = new();
            controller.Step(
                requested: true,
                hasForwardThrottle: true,
                deltaTime: 1f,
                spoolRate: 10f,
                drainPerSecond: 1f,
                rechargePerSecond: 1f,
                rechargeDelay: 0f);

            Assert.That(controller.Charge, Is.EqualTo(0f));
            Assert.That(controller.IsLocked, Is.True);

            controller.Step(true, true, 1f, 10f, 1f, 1f, 0f);
            Assert.That(controller.Charge, Is.EqualTo(1f));
            Assert.That(controller.IsLocked, Is.True);

            controller.Step(false, true, 0f, 10f, 1f, 1f, 0f);
            controller.Step(true, true, 0.1f, 10f, 0f, 0f, 0f);
            Assert.That(controller.IsLocked, Is.False);
            Assert.That(controller.IsActive, Is.True);
        }

        [Test]
        public void TouchdownRequiresContactWithinEverySafetyLimit()
        {
            Assert.That(
                SpacecraftTouchdownEvaluator.IsSafe(
                    SpacecraftSurfaceContactSample.Empty,
                    0f,
                    2f,
                    3f,
                    8f),
                Is.False);

            GameObject bodyObject = new("Touchdown Test Body");
            try
            {
                CelestialBody body = bodyObject.AddComponent<CelestialBody>();
                SpacecraftSurfaceContactSample safeContact = new(
                    body,
                    Vector3.zero,
                    Vector3.up,
                    new Vector3(1f, -1.5f, 0f),
                    0f,
                    2,
                    1f);

                Assert.That(
                    SpacecraftTouchdownEvaluator.IsSafe(safeContact, 5f, 2f, 3f, 8f),
                    Is.True);
                Assert.That(
                    SpacecraftTouchdownEvaluator.IsSafe(safeContact, 9f, 2f, 3f, 8f),
                    Is.False);
                Assert.That(
                    SpacecraftTouchdownEvaluator.IsSafe(safeContact, 5f, 1f, 3f, 8f),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(bodyObject);
            }
        }

        [Test]
        public void ActorProbeUsesOnlyExplicitCelestialFrameProvider()
        {
            GameObject providerObject = new("Frame Provider");
            GameObject actorObject = new("Actor");
            try
            {
                CelestialFrameProvider provider =
                    providerObject.AddComponent<CelestialFrameProvider>();
                CelestialActorProbe probe = actorObject.AddComponent<CelestialActorProbe>();

                Assert.That(probe.FrameProvider, Is.Null);
                probe.SetFrameProvider(provider);
                Assert.That(probe.FrameProvider, Is.SameAs(provider));
            }
            finally
            {
                Object.DestroyImmediate(actorObject);
                Object.DestroyImmediate(providerObject);
            }
        }

        [Test]
        public void CelestialBodyPreservesInertialOrbitInsideStationaryPhysicsFrame()
        {
            GameObject bodyObject = new("Reference Frame Body");
            CelestialBodyDefinition definition =
                ScriptableObject.CreateInstance<CelestialBodyDefinition>();
            try
            {
                JsonUtility.FromJsonOverwrite(
                    "{\"bodyName\":\"Reference Frame Body\"," +
                    "\"initialVelocity\":{\"x\":0,\"y\":0,\"z\":28.46}," +
                    "\"participatesInNBody\":true,\"motionMode\":1}",
                    definition);

                CelestialBody body = bodyObject.AddComponent<CelestialBody>();
                body.ApplyDefinition(definition);
                body.ResetSimulationState();
                body.IntegratePosition(
                    0.01f,
                    new Vector3(0f, 0f, 28.46f));

                Assert.That(body.InertialVelocity.z, Is.EqualTo(28.46f).Within(0.0001f));
                Assert.That(body.Velocity, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                Object.DestroyImmediate(bodyObject);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void MechanicalPartPoseBuildsLinkedHierarchyWithoutChangingDeployedPose()
        {
            GameObject rootObject = new("Visual Root");
            GameObject parentObject = new("Parent Part");
            GameObject childObject = new("Child Part");
            try
            {
                parentObject.transform.SetParent(rootObject.transform, false);
                childObject.transform.SetParent(rootObject.transform, false);
                childObject.transform.localPosition = new Vector3(0f, -1f, 1f);
                Vector3 deployedPosition = childObject.transform.position;

                SpacecraftMechanicalPartPose parentPose =
                    JsonUtility.FromJson<SpacecraftMechanicalPartPose>(
                        "{\"partName\":\"Parent Part\",\"targetLocalEulerOffset\":{\"x\":-90}}");
                SpacecraftMechanicalPartPose childPose =
                    JsonUtility.FromJson<SpacecraftMechanicalPartPose>(
                        "{\"partName\":\"Child Part\",\"parentPartName\":\"Parent Part\"}");

                Assert.That(parentPose.PrepareDrivenTransform(rootObject.transform), Is.True);
                Assert.That(childPose.PrepareDrivenTransform(rootObject.transform), Is.True);
                Assert.That(childPose.AttachTo(parentPose), Is.True);
                parentPose.CaptureInitialPose(rootObject.transform);
                childPose.CaptureInitialPose(rootObject.transform);

                Assert.That(childObject.transform.parent, Is.SameAs(parentObject.transform));
                Assert.That(childObject.transform.position, Is.EqualTo(deployedPosition));

                parentPose.Apply(1f);
                childPose.Apply(1f);

                Assert.That(
                    Vector3.Distance(childObject.transform.position, deployedPosition),
                    Is.GreaterThan(0.5f));
                Assert.That(
                    Vector3.Distance(childObject.transform.position, parentObject.transform.position),
                    Is.EqualTo(Mathf.Sqrt(2f)).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void MechanicalPartPoseCanDriveMeshFromAuthoredVirtualPivot()
        {
            GameObject rootObject = new("Visual Root");
            GameObject partObject = new("Unrigged Part");
            try
            {
                partObject.transform.SetParent(rootObject.transform, false);
                partObject.transform.localPosition = new Vector3(2f, 0f, 0f);
                Vector3 deployedPosition = partObject.transform.position;
                SpacecraftMechanicalPartPose pose =
                    JsonUtility.FromJson<SpacecraftMechanicalPartPose>(
                        "{\"partName\":\"Unrigged Part\",\"createVirtualPivot\":true," +
                        "\"virtualPivotLocalPosition\":{\"x\":0,\"y\":1,\"z\":0}}");

                Assert.That(pose.PrepareDrivenTransform(rootObject.transform), Is.True);

                Assert.That(pose.DrivenTransform, Is.Not.SameAs(partObject.transform));
                Assert.That(partObject.transform.parent, Is.SameAs(pose.DrivenTransform));
                Assert.That(pose.DrivenTransform.position, Is.EqualTo(new Vector3(2f, 1f, 0f)));
                Assert.That(partObject.transform.position, Is.EqualTo(deployedPosition));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        static SpacecraftFlightControlFrame Frame(
            SpacecraftPilotCommand command,
            Vector3 localVelocity,
            Vector3 localAngularVelocity,
            Vector3 localGravity,
            bool assisted)
        {
            return new SpacecraftFlightControlFrame(
                command,
                localVelocity,
                localAngularVelocity,
                localGravity,
                assisted,
                boostAuthority: 0f,
                boostSurge: 0f);
        }

        static CelestialFrameSample AtmosphereFrame(
            CelestialBody body,
            float centerDistance,
            float speed,
            float oceanRadius)
        {
            Vector3 position = Vector3.up * centerDistance;
            CelestialEnvironmentSample environment = new(
                body,
                oceanRadius > 0f,
                oceanRadius,
                hasAtmosphere: true,
                atmosphereRadius: 60f,
                terrainRadiusMinMax: new Vector2(body.Radius, body.Radius),
                atmosphereBaseRadius: oceanRadius > 0f ? oceanRadius : body.Radius);
            return new CelestialFrameSample(
                body,
                position,
                Vector3.right * speed,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                Vector3.up * body.Radius,
                Vector3.up,
                Vector3.down * body.SurfaceGravity,
                centerDistance,
                centerDistance - body.Radius,
                0f,
                environment);
        }

        static SpacecraftFlightControlSettings Settings()
        {
            return new SpacecraftFlightControlSettings(
                new Vector3(45f, 40f, 180f),
                new Vector3(45f, 40f, 260f),
                new Vector3(45f, 40f, 70f),
                new Vector3(12f, 16f, 20f),
                new Vector3(12f, 16f, 34f),
                new Vector3(12f, 16f, 18f),
                new Vector3(65f, 42f, 95f) * Mathf.Deg2Rad,
                new Vector3(180f, 140f, 240f) * Mathf.Deg2Rad,
                new Vector3(2.8f, 2.8f, 2.2f),
                new Vector3(7f, 7f, 9f),
                3.2f,
                compensateGravity: true,
                limitManualFlightEnvelope: true,
                manualEnvelopeStart: 0.85f,
                boostSurgeStrength: 0f);
        }
    }
}
