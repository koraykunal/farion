using System.Collections.Generic;
using Farion.Simulation.Physics;
using NUnit.Framework;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class CelestialMotionDeterminismTests
    {
        const float StarRadius = 1200f;
        const float StarSurfaceGravity = 350f;
        const float OrbitRadius = 60000f;

        readonly List<GameObject> created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
        }

        [Test]
        public void OrbitalElementsReproduceTheAuthoredStateAtEpoch()
        {
            Vector3 position = new(OrbitRadius, 0f, 0f);
            Vector3 velocity = new(0f, 0f, CircularSpeed());

            Assert.That(
                OrbitalElements.TryCreate(position, velocity, GravitationalParameter(), out OrbitalElements elements),
                Is.True);
            elements.Evaluate(0d, out Vector3 evaluatedPosition, out Vector3 evaluatedVelocity);

            Assert.That(Vector3.Distance(evaluatedPosition, position), Is.LessThan(0.05f));
            Assert.That(Vector3.Distance(evaluatedVelocity, velocity), Is.LessThan(0.001f));
        }

        [Test]
        public void CircularOrbitReturnsToTheStartAfterOnePeriod()
        {
            Vector3 position = new(OrbitRadius, 0f, 0f);
            Vector3 velocity = new(0f, 0f, CircularSpeed());
            OrbitalElements.TryCreate(position, velocity, GravitationalParameter(), out OrbitalElements elements);

            double period = elements.PeriodSeconds;
            Assert.That(period, Is.GreaterThan(0d));

            elements.Evaluate(period, out Vector3 afterOnePeriod, out _);
            elements.Evaluate(period * 0.5d, out Vector3 halfway, out _);

            Assert.That(Vector3.Distance(afterOnePeriod, position), Is.LessThan(0.05f));
            Assert.That(Vector3.Distance(halfway, position), Is.GreaterThan(OrbitRadius));
        }

        [Test]
        public void EvaluationIsAPureFunctionOfTime()
        {
            Vector3 position = new(OrbitRadius, 0f, 0f);
            Vector3 velocity = new(0f, 0f, CircularSpeed());
            OrbitalElements.TryCreate(position, velocity, GravitationalParameter(), out OrbitalElements elements);

            elements.Evaluate(1234.5d, out Vector3 direct, out Vector3 directVelocity);
            for (double t = 0d; t < 1234.5d; t += 37d)
            {
                elements.Evaluate(t, out _, out _);
            }

            elements.Evaluate(1234.5d, out Vector3 afterWalk, out Vector3 afterWalkVelocity);

            Assert.That(direct, Is.EqualTo(afterWalk));
            Assert.That(directVelocity, Is.EqualTo(afterWalkVelocity));
        }

        [Test]
        public void SimulationPoseDependsOnlyOnSimulationTime()
        {
            GravitySimulation stepped = CreateSystem(out CelestialBody steppedPlanet, out _);
            GravitySimulation jumped = CreateSystem(out CelestialBody jumpedPlanet, out _);

            const double target = 900d;
            for (double t = 0d; t <= target; t += 0.02d)
            {
                stepped.SetSimulationTime(t);
            }

            jumped.SetSimulationTime(target);

            Assert.That(
                Vector3.Distance(steppedPlanet.Rigidbody.position, jumpedPlanet.Rigidbody.position),
                Is.LessThan(0.01f),
                "A late joiner that jumps straight to the shared tick must land on the same pose.");
            Assert.That(
                Quaternion.Angle(steppedPlanet.Rigidbody.rotation, jumpedPlanet.Rigidbody.rotation),
                Is.LessThan(0.01f));
        }

        [Test]
        public void PlanetsSpinAndOrbitWithoutIntegration()
        {
            GravitySimulation simulation = CreateSystem(out CelestialBody planet, out _);
            simulation.SetIntegrationEnabled(false);

            Vector3 startPosition = planet.Rigidbody.position;
            Quaternion startRotation = planet.Rigidbody.rotation;

            simulation.SetSimulationTime(600d);

            Assert.That(
                Vector3.Distance(planet.Rigidbody.position, startPosition),
                Is.GreaterThan(1f),
                "Orbit must advance from the shared clock even when integration is off.");
            Assert.That(
                Quaternion.Angle(planet.Rigidbody.rotation, startRotation),
                Is.GreaterThan(1f),
                "Spin must advance from the shared clock even when integration is off.");
        }

        [Test]
        public void ReferenceBodyStaysPutAndCarriesTheLocalFrame()
        {
            GravitySimulation simulation = CreateSystem(out CelestialBody planet, out CelestialBody star, anchorOnPlanet: true);
            Vector3 planetStart = planet.Rigidbody.position;
            Vector3 starStart = star.Rigidbody.position;

            simulation.SetSimulationTime(500d);

            Assert.That(
                Vector3.Distance(planet.Rigidbody.position, planetStart),
                Is.LessThan(0.01f),
                "The physics reference body anchors local space and must not drift.");
            Assert.That(
                Vector3.Distance(star.Rigidbody.position, starStart),
                Is.GreaterThan(1f),
                "Everything else moves relative to the anchor.");
        }

        static double GravitationalParameter()
        {
            float mass = StarSurfaceGravity * StarRadius * StarRadius /
                GravitySimulation.DefaultGravitationalConstant;
            return (double)GravitySimulation.DefaultGravitationalConstant * mass;
        }

        static float CircularSpeed()
        {
            return (float)System.Math.Sqrt(GravitationalParameter() / OrbitRadius);
        }

        GravitySimulation CreateSystem(out CelestialBody planet, out CelestialBody star)
        {
            return CreateSystem(out planet, out star, anchorOnPlanet: false);
        }

        GravitySimulation CreateSystem(
            out CelestialBody planet,
            out CelestialBody star,
            bool anchorOnPlanet)
        {
            GameObject root = new("Celestial System");
            created.Add(root);

            star = CreateBody(root.transform, "Star", Vector3.zero, StarRadius, StarSurfaceGravity,
                CelestialBodyMotionMode.Static, Vector3.zero, Vector3.zero);
            planet = CreateBody(root.transform, "Planet", new Vector3(OrbitRadius, 0f, 0f), 1600f, 9.81f,
                CelestialBodyMotionMode.KinematicOrbit,
                new Vector3(0f, 0f, CircularSpeed()),
                new Vector3(0f, 0.25f, 0f));
            planet.SetOrbitAttractor(star);

            GravitySimulation simulation = root.AddComponent<GravitySimulation>();
            TestFieldAccess.SetField(simulation, "registeredBodies", new List<CelestialBody> { star, planet });
            if (anchorOnPlanet)
            {
                TestFieldAccess.SetField(simulation, "physicsReferenceBody", planet);
            }

            simulation.RefreshBodies();
            simulation.RebuildAnalyticOrder();
            simulation.SetExternalTimeSource(true);
            return simulation;
        }

        CelestialBody CreateBody(
            Transform parent,
            string bodyName,
            Vector3 position,
            float radius,
            float surfaceGravity,
            CelestialBodyMotionMode motionMode,
            Vector3 initialVelocity,
            Vector3 angularVelocityDegrees)
        {
            GameObject owner = new(bodyName);
            owner.transform.SetParent(parent);
            owner.transform.position = position;
            CelestialBody body = owner.AddComponent<CelestialBody>();
            TestFieldAccess.SetField(body, "bodyName", bodyName);
            TestFieldAccess.SetField(body, "radius", radius);
            TestFieldAccess.SetField(body, "surfaceGravity", surfaceGravity);
            TestFieldAccess.SetField(body, "motionMode", motionMode);
            TestFieldAccess.SetField(body, "initialVelocity", initialVelocity);
            TestFieldAccess.SetField(body, "initialAngularVelocityDegreesPerSecond", angularVelocityDegrees);
            body.RecalculateMass(GravitySimulation.DefaultGravitationalConstant);
            body.ConfigureRigidbody();
            body.ResetSimulationState();
            body.Rigidbody.position = position;
            return body;
        }
    }
}
