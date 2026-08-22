using System.Collections.Generic;
using Farion.Simulation.Physics;
using NUnit.Framework;
using UnityEngine;
using Farion.Tests.Support;

namespace Farion.Tests.EditMode
{
    public sealed class GravitySimulationTests
    {
        GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Gravity Simulation Test");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void KinematicOrbitUsesOnlyItsAssignedAttractor()
        {
            CelestialBody star = CreateBody("Star", Vector3.zero, 1200f, 350f, CelestialBodyMotionMode.Static);
            CelestialBody planet = CreateBody(
                "Planet",
                Vector3.right * 60000f,
                1600f,
                9.81f,
                CelestialBodyMotionMode.KinematicOrbit);
            CelestialBody perturbingBody = CreateBody(
                "Perturber",
                Vector3.right * 65000f,
                800f,
                5f,
                CelestialBodyMotionMode.Static);
            planet.SetOrbitAttractor(star);

            GravitySimulation simulation = CreateSimulation(null, star, planet, perturbingBody);
            Vector3 expected = simulation.CalculateAccelerationFromBody(planet.Position, star);
            Vector3 fullNBody = expected +
                simulation.CalculateAccelerationFromBody(planet.Position, perturbingBody);
            Vector3 actual = simulation.CalculateOrbitalAcceleration(planet);

            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.00001f));
            Assert.That(Vector3.Distance(actual, fullNBody), Is.GreaterThan(0.001f));
        }

        [Test]
        public void ReferenceFrameVelocityMatchesAssignedPhysicsBody()
        {
            CelestialBody star = CreateBody("Star", Vector3.zero, 1200f, 350f, CelestialBodyMotionMode.Static);
            Vector3 orbitVelocity = new(0f, 0f, 30f);
            CelestialBody planet = CreateBody(
                "Planet",
                Vector3.right * 60000f,
                1600f,
                9.81f,
                CelestialBodyMotionMode.KinematicOrbit,
                orbitVelocity);

            GravitySimulation simulation = CreateSimulation(planet, star, planet);

            Assert.That(simulation.HasPhysicsReferenceFrame, Is.True);
            Assert.That(Vector3.Distance(simulation.ReferenceFrameVelocity, orbitVelocity), Is.LessThan(0.0001f));
            Assert.That(planet.Velocity.magnitude, Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(star.Velocity, -orbitVelocity), Is.LessThan(0.0001f));
        }

        [Test]
        public void ChangingReferenceFramePreservesDynamicBodyInertialVelocity()
        {
            CelestialBody planet = CreateBody(
                "Planet",
                Vector3.zero,
                1600f,
                9.81f,
                CelestialBodyMotionMode.KinematicOrbit,
                new Vector3(0f, 0f, 30f));
            CelestialBody moon = CreateBody(
                "Moon",
                Vector3.right * 13000f,
                120f,
                1.62f,
                CelestialBodyMotionMode.KinematicOrbit,
                new Vector3(0f, 0f, 45f));
            moon.SetOrbitAttractor(planet);
            GravitySimulation simulation = CreateSimulation(planet, planet, moon);

            GameObject actorObject = new("Actor");
            actorObject.transform.SetParent(root.transform);
            Rigidbody actor = actorObject.AddComponent<Rigidbody>();
            actor.useGravity = false;
            actor.linearVelocity = new Vector3(0f, 0f, 15f);
            actor.position = moon.Position + Vector3.up * (moon.Radius + 1f);
            GravityReferenceObserverStub observer =
                actorObject.AddComponent<GravityReferenceObserverStub>();
            observer.Body = actor;

            simulation.SetPhysicsReferenceObserverSource(observer);

            Assert.That(simulation.RefreshPhysicsReferenceBody(), Is.True);
            Assert.That(simulation.PhysicsReferenceBody, Is.EqualTo(moon));
            Assert.That(Vector3.Distance(simulation.ReferenceFrameVelocity, moon.InertialVelocity), Is.LessThan(0.0001f));
            Assert.That(actor.linearVelocity.magnitude, Is.LessThan(0.0001f));
            Assert.That(moon.Velocity.magnitude, Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(planet.Velocity, new Vector3(0f, 0f, -15f)), Is.LessThan(0.0001f));
        }

        [Test]
        public void ReferenceFrameAccelerationRemovesFrameAcceleration()
        {
            CelestialBody star = CreateBody("Star", Vector3.zero, 1200f, 350f, CelestialBodyMotionMode.Static);
            CelestialBody planet = CreateBody(
                "Planet",
                Vector3.right * 60000f,
                1600f,
                9.81f,
                CelestialBodyMotionMode.KinematicOrbit);

            GravitySimulation simulation = CreateSimulation(planet, star, planet);

            Vector3 point = planet.Position + Vector3.up * 5000f;
            Vector3 absolute = simulation.CalculateAcceleration(point);
            Vector3 frameRelative = simulation.CalculateReferenceFrameAcceleration(point);

            Assert.That(
                Vector3.Distance(frameRelative, absolute - simulation.ReferenceFrameAcceleration),
                Is.LessThan(0.0001f));
            Assert.That(simulation.ReferenceFrameAcceleration.magnitude, Is.GreaterThan(0f));
        }

        [Test]
        public void DominantBodyIsTheStrongestAttractorAtThePoint()
        {
            CelestialBody star = CreateBody("Star", Vector3.zero, 1200f, 350f, CelestialBodyMotionMode.Static);
            CelestialBody planet = CreateBody(
                "Planet",
                Vector3.right * 60000f,
                1600f,
                9.81f,
                CelestialBodyMotionMode.KinematicOrbit);

            GravitySimulation simulation = CreateSimulation(null, star, planet);

            GravitySample nearPlanet = simulation.FindDominantBody(planet.Position + Vector3.up * 2000f);
            GravitySample nearStar = simulation.FindDominantBody(star.Position + Vector3.up * 2000f);

            Assert.That(nearPlanet.Body, Is.EqualTo(planet));
            Assert.That(nearStar.Body, Is.EqualTo(star));
            Assert.That(nearPlanet.SurfaceDistance, Is.EqualTo(nearPlanet.CenterDistance - planet.Radius).Within(0.01f));
            Assert.That(Vector3.Dot(nearPlanet.SurfaceNormal, Vector3.up), Is.GreaterThan(0.99f));
        }

        [Test]
        public void SnapshotRoundTripRestoresPositionAndVelocity()
        {
            CelestialBody star = CreateBody("Star", Vector3.zero, 1200f, 350f, CelestialBodyMotionMode.Static);
            CelestialBody planet = CreateBody(
                "Planet",
                Vector3.right * 60000f,
                1600f,
                9.81f,
                CelestialBodyMotionMode.KinematicOrbit,
                new Vector3(0f, 0f, 30f));

            GravitySimulation simulation = CreateSimulation(null, star, planet);

            List<CelestialBodySnapshot> snapshots = new();
            simulation.CaptureSnapshots(snapshots);
            Assert.That(snapshots.Count, Is.EqualTo(2));

            Vector3 originalPosition = planet.Position;
            Vector3 originalVelocity = planet.InertialVelocity;
            planet.Rigidbody.position = originalPosition + Vector3.one * 1234f;
            planet.IntegrateVelocity(Vector3.one * 7f, 1f);

            Assert.That(simulation.CanApplySnapshots(snapshots), Is.True);
            Assert.That(simulation.ApplySnapshots(snapshots), Is.True);
            Assert.That(Vector3.Distance(planet.Position, originalPosition), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(planet.InertialVelocity, originalVelocity), Is.LessThan(0.001f));
        }

        [Test]
        public void SnapshotsFromAnUnknownBodyAreRejected()
        {
            CelestialBody star = CreateBody("Star", Vector3.zero, 1200f, 350f, CelestialBodyMotionMode.Static);
            GravitySimulation simulation = CreateSimulation(null, star);

            List<CelestialBodySnapshot> snapshots = new();
            simulation.CaptureSnapshots(snapshots);

            GameObject otherRoot = new("Other Simulation");
            try
            {
                CelestialBody stranger = CreateBody(
                    "Stranger",
                    Vector3.zero,
                    100f,
                    1f,
                    CelestialBodyMotionMode.Static,
                    parent: otherRoot.transform);
                GravitySimulation other = CreateSimulation(null, new[] { stranger }, otherRoot);

                Assert.That(other.CanApplySnapshots(snapshots), Is.False);
                Assert.That(other.ApplySnapshots(snapshots), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(otherRoot);
            }
        }

        [Test]
        public void KinematicBodiesSupportNonConvexSurfaceColliders()
        {
            CelestialBody planet = CreateBody(
                "Planet",
                Vector3.zero,
                1600f,
                9.81f,
                CelestialBodyMotionMode.KinematicOrbit);
            CelestialBody star = CreateBody("Star", Vector3.right * 5f, 1200f, 350f, CelestialBodyMotionMode.Static);

            Assert.That(planet.SupportsNonConvexSurfaceCollider, Is.True);
            Assert.That(star.SupportsNonConvexSurfaceCollider, Is.True);
            Assert.That(planet.Rigidbody.isKinematic, Is.True);
            Assert.That(star.IntegratesOrbit, Is.False);
        }

        GravitySimulation CreateSimulation(
            CelestialBody referenceBody,
            params CelestialBody[] bodies)
        {
            return CreateSimulation(referenceBody, bodies, root);
        }

        static GravitySimulation CreateSimulation(
            CelestialBody referenceBody,
            IReadOnlyList<CelestialBody> bodies,
            GameObject owner)
        {
            GravitySimulation simulation = owner.AddComponent<GravitySimulation>();
            TestFieldAccess.SetField(simulation, "registeredBodies", new List<CelestialBody>(bodies));
            TestFieldAccess.SetField(simulation, "physicsReferenceBody", referenceBody);
            simulation.RefreshBodies();
            foreach (CelestialBody body in bodies)
            {
                body.RecalculateMass(GravitySimulation.DefaultGravitationalConstant);
                body.ResetSimulationState();
            }

            simulation.SetIntegrationEnabled(true);
            return simulation;
        }

        CelestialBody CreateBody(
            string bodyName,
            Vector3 position,
            float radius,
            float surfaceGravity,
            CelestialBodyMotionMode motionMode,
            Vector3 initialVelocity = default,
            Transform parent = null)
        {
            GameObject owner = new(bodyName);
            owner.transform.SetParent(parent != null ? parent : root.transform);
            owner.transform.position = position;
            CelestialBody body = owner.AddComponent<CelestialBody>();
            TestFieldAccess.SetField(body, "bodyName", bodyName);
            TestFieldAccess.SetField(body, "radius", radius);
            TestFieldAccess.SetField(body, "surfaceGravity", surfaceGravity);
            TestFieldAccess.SetField(body, "motionMode", motionMode);
            TestFieldAccess.SetField(body, "initialVelocity", initialVelocity);
            body.RecalculateMass(GravitySimulation.DefaultGravitationalConstant);
            body.ConfigureRigidbody();
            body.ResetSimulationState();
            body.Rigidbody.position = position;
            return body;
        }
    }

    sealed class GravityReferenceObserverStub :
        MonoBehaviour,
        ICelestialSurfaceCollisionObserver
    {
        public Rigidbody Body { get; set; }

        public bool TryGetSurfaceCollisionObserver(
            out CelestialSurfaceCollisionObserverState observer)
        {
            observer = new CelestialSurfaceCollisionObserverState(Body);
            return observer.IsValid;
        }
    }
}
