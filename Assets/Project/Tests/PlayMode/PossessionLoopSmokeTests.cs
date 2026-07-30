using System.Collections;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Farion.Tests.PlayMode
{
    public sealed class PossessionLoopSmokeTests
    {
        [UnityTest]
        public IEnumerator PhysicsSandboxSupportsExitAndReenterPilotLoop()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "SC_PhysicsSandbox",
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;

            PlayerPossessionController controller =
                Object.FindAnyObjectByType<PlayerPossessionController>();
            Assert.That(controller, Is.Not.Null);

            SpacecraftMotor motor = controller.SpacecraftMotor;
            Assert.That(motor, Is.Not.Null);
            Assert.That(motor.FlightProfile, Is.Not.Null);
            Assert.That(
                motor.Rigidbody.mass,
                Is.EqualTo(motor.FlightProfile.RigidbodyMass).Within(0.01f));
            Assert.That(motor.Telemetry.BoostCharge, Is.InRange(0f, 1f));

            SpacecraftLandingGearAnimator landingGear =
                motor.GetComponent<SpacecraftLandingGearAnimator>();
            Assert.That(landingGear, Is.Not.Null);
            Assert.That(landingGear.IsDeployed, Is.True);

            AssertLandingContact(motor, "COL_Landing_Front");
            AssertLandingContact(motor, "COL_Landing_Left");
            AssertLandingContact(motor, "COL_Landing_Right");

            SpacecraftCameraRig cameraRig = Object.FindAnyObjectByType<SpacecraftCameraRig>();
            Assert.That(cameraRig, Is.Not.Null);
            Assert.That(GameObject.Find("SpacecraftFlightHud"), Is.Not.Null);

            PilotSeatInteractable pilotSeat =
                Object.FindAnyObjectByType<PilotSeatInteractable>(
                    FindObjectsInactive.Include);
            VehicleBoardingPoint boardingPoint =
                Object.FindAnyObjectByType<VehicleBoardingPoint>(
                    FindObjectsInactive.Include);
            Assert.That(pilotSeat, Is.Not.Null);
            Assert.That(boardingPoint, Is.Not.Null);
            Assert.That(pilotSeat.IsBound, Is.True);
            Assert.That(boardingPoint.IsBound, Is.True);

            Assert.That(controller.IsPilotingSpacecraft, Is.True);
            Assert.That(controller.ExitPilotSeat(), Is.True);
            Assert.That(controller.IsInShipInterior, Is.True);
            Assert.That(controller.EnterPilotSeat(), Is.True);
            Assert.That(controller.IsPilotingSpacecraft, Is.True);
        }

        static Collider FindCollider(SpacecraftMotor motor, string colliderName)
        {
            Collider[] colliders = motor.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider candidate = colliders[i];
                if (candidate != null && candidate.name == colliderName)
                {
                    return candidate;
                }
            }

            return null;
        }

        static void AssertLandingContact(
            SpacecraftMotor motor,
            string colliderName)
        {
            Collider contact = FindCollider(motor, colliderName);
            Assert.That(contact, Is.Not.Null, $"{colliderName} is missing.");
            Assert.That(contact.enabled, Is.True, $"{colliderName} is disabled.");
        }
    }
}
