using Farion.Gameplay.Flight;
using NUnit.Framework;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class SpacecraftSurfaceGuardTests
    {
        static readonly SpacecraftSurfaceGuardSettings Settings = new(clearanceMeters: 1.5f, toleranceMeters: 3f);

        [Test]
        public void StaysPassiveWhileClearOfTerrain()
        {
            SpacecraftSurfaceGuardOutput output = Evaluate(
                currentAltitude: 50f,
                predictedAltitude: 40f,
                velocity: new Vector3(200f, -600f, 0f));

            Assert.That(output.Engaged, Is.False);
            Assert.That(output.CorrectsVelocity, Is.False);
            Assert.That(output.CorrectsPosition, Is.False);
            Assert.That(output.SurfaceRelativeVelocity, Is.EqualTo(new Vector3(200f, -600f, 0f)));
        }

        [Test]
        public void EngagesOnPredictedPenetrationAndKeepsTangentialSpeed()
        {
            SpacecraftSurfaceGuardOutput output = Evaluate(
                currentAltitude: 5f,
                predictedAltitude: -5f,
                velocity: new Vector3(100f, -30f, 0f));

            Assert.That(output.Engaged, Is.True);
            Assert.That(output.SurfaceRelativeVelocity.x, Is.EqualTo(100f).Within(0.0001f));
            Assert.That(output.SurfaceRelativeVelocity.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(output.ImpactSpeed, Is.EqualTo(30f).Within(0.0001f));
            Assert.That(output.CorrectsPosition, Is.False);
        }

        [Test]
        public void LiftsBuriedHullBackToClearance()
        {
            SpacecraftSurfaceGuardOutput output = Evaluate(
                currentAltitude: -20f,
                predictedAltitude: -20f,
                velocity: new Vector3(0f, -5f, 0f));

            Assert.That(output.Engaged, Is.True);
            Assert.That(output.PositionCorrection.y, Is.EqualTo(21.5f).Within(0.0001f));
            Assert.That(output.SurfaceRelativeVelocity.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(output.ImpactSpeed, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void IgnoresCollisionMeshDipsWithinTolerance()
        {
            SpacecraftSurfaceGuardOutput output = Evaluate(
                currentAltitude: -1f,
                predictedAltitude: -1f,
                velocity: Vector3.zero);

            Assert.That(output.Engaged, Is.False);
        }

        [Test]
        public void HoldsEngagementUntilReleaseAltitudeWithoutRepeatingImpact()
        {
            SpacecraftSurfaceGuardOutput held = Evaluate(
                currentAltitude: 3f,
                predictedAltitude: 3f,
                velocity: new Vector3(0f, -2f, 0f),
                engaged: true);
            SpacecraftSurfaceGuardOutput released = Evaluate(
                currentAltitude: 5f,
                predictedAltitude: 6f,
                velocity: new Vector3(0f, 4f, 0f),
                engaged: true);

            Assert.That(held.Engaged, Is.True);
            Assert.That(held.ImpactSpeed, Is.EqualTo(0f));
            Assert.That(held.SurfaceRelativeVelocity.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(released.Engaged, Is.False);
        }

        [Test]
        public void LeavesReceedingVelocityAlone()
        {
            SpacecraftSurfaceGuardOutput output = Evaluate(
                currentAltitude: -10f,
                predictedAltitude: -8f,
                velocity: new Vector3(0f, 12f, 0f));

            Assert.That(output.Engaged, Is.True);
            Assert.That(output.CorrectsVelocity, Is.False);
            Assert.That(output.SurfaceRelativeVelocity.y, Is.EqualTo(12f).Within(0.0001f));
            Assert.That(output.ImpactSpeed, Is.EqualTo(0f));
        }

        static SpacecraftSurfaceGuardOutput Evaluate(
            float currentAltitude,
            float predictedAltitude,
            Vector3 velocity,
            bool engaged = false)
        {
            return SpacecraftSurfaceGuardLaw.Evaluate(
                new SpacecraftSurfaceGuardFrame(
                    currentAltitude,
                    predictedAltitude,
                    Vector3.up,
                    Vector3.up,
                    velocity,
                    engaged),
                Settings);
        }
    }
}
