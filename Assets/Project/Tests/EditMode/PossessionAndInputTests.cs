using System.Linq;
using Farion.Gameplay.Character;
using Farion.Gameplay.Input;
using Farion.Gameplay.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace Farion.Tests.EditMode
{
    public sealed class PossessionAndInputTests
    {
        [Test]
        public void PossessionPolicyAllowsTheCompleteBoardingLoop()
        {
            Assert.That(
                PlayerPossessionTransitionPolicy.CanTransition(
                    PlayerPossessionMode.Spacecraft,
                    PlayerPossessionMode.ShipInterior,
                    PlayerPossessionTransitionRequest.ExitPilotSeat),
                Is.True);
            Assert.That(
                PlayerPossessionTransitionPolicy.CanTransition(
                    PlayerPossessionMode.ShipInterior,
                    PlayerPossessionMode.OnFoot,
                    PlayerPossessionTransitionRequest.ExitShipInterior),
                Is.True);
            Assert.That(
                PlayerPossessionTransitionPolicy.CanTransition(
                    PlayerPossessionMode.OnFoot,
                    PlayerPossessionMode.ShipInterior,
                    PlayerPossessionTransitionRequest.EnterShipInterior),
                Is.True);
            Assert.That(
                PlayerPossessionTransitionPolicy.CanTransition(
                    PlayerPossessionMode.ShipInterior,
                    PlayerPossessionMode.Spacecraft,
                    PlayerPossessionTransitionRequest.EnterPilotSeat),
                Is.True);
        }

        [Test]
        public void PossessionPolicyRejectsInvalidDirectTransitions()
        {
            Assert.That(
                PlayerPossessionTransitionPolicy.CanTransition(
                    PlayerPossessionMode.OnFoot,
                    PlayerPossessionMode.Spacecraft,
                    PlayerPossessionTransitionRequest.EnterPilotSeat),
                Is.False);
            Assert.That(
                PlayerPossessionTransitionPolicy.CanTransition(
                    PlayerPossessionMode.OnFoot,
                    PlayerPossessionMode.Spacecraft,
                    PlayerPossessionTransitionRequest.ExitPilotSeat),
                Is.False);
            Assert.That(
                PlayerPossessionTransitionPolicy.CanTransition(
                    PlayerPossessionMode.Spacecraft,
                    PlayerPossessionMode.OnFoot,
                    PlayerPossessionTransitionRequest.EnterShipInterior),
                Is.False);
        }

        [Test]
        public void ArtificialGravityFollowsVolumeUpAndForcesTriggerCollider()
        {
            GameObject owner = new("ArtificialGravityVolumeTest");
            try
            {
                owner.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                ArtificialGravityVolume volume = owner.AddComponent<ArtificialGravityVolume>();

                Assert.That(owner.GetComponent<BoxCollider>().isTrigger, Is.True);
                Assert.That(volume.Up, Is.EqualTo(owner.transform.up).Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(
                    volume.GravityAcceleration,
                    Is.EqualTo(-owner.transform.up * 9.81f)
                        .Using(Vector3ComparerWithEqualsOperator.Instance));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void InputSchemaContainsRequiredActionMaps()
        {
            Assert.That(FarionInputActions.Asset.FindActionMap("OnFoot"), Is.Not.Null);
            Assert.That(FarionInputActions.Asset.FindActionMap("Flight"), Is.Not.Null);
            Assert.That(FarionInputActions.Asset.FindActionMap("Vehicle"), Is.Not.Null);
            Assert.That(FarionInputActions.Asset.FindActionMap("UI"), Is.Not.Null);
            Assert.That(FarionInputActions.FlightToggleAssist, Is.Not.Null);
            Assert.That(FarionInputActions.FlightToggleLandingGear, Is.Not.Null);
            Assert.That(FarionInputActions.UiPause, Is.Not.Null);
            Assert.That(FarionInputActions.UiInventory, Is.Not.Null);
            Assert.That(FarionInputActions.UiNavigate, Is.Not.Null);
            Assert.That(FarionInputActions.UiSubmit, Is.Not.Null);
            Assert.That(FarionInputActions.UiCancel, Is.Not.Null);
            Assert.That(FarionInputActions.UiPoint, Is.Not.Null);
            Assert.That(FarionInputActions.UiLeftClick, Is.Not.Null);
            Assert.That(FarionInputActions.UiMiddleClick, Is.Not.Null);
            Assert.That(FarionInputActions.UiRightClick, Is.Not.Null);
            Assert.That(FarionInputActions.UiScrollWheel, Is.Not.Null);
            Assert.That(
                FarionInputActions.FlightTranslate.bindings.Any(binding =>
                    binding.path == "<Gamepad>/leftStick" &&
                    binding.processors.Contains("stickDeadzone")),
                Is.True);
            Assert.That(
                FarionInputActions.FlightLook.bindings.Any(binding =>
                    binding.path == "<Gamepad>/rightStick" &&
                    binding.processors.Contains("stickDeadzone")),
                Is.True);
            Assert.That(
                FarionInputActions.UiNavigate.bindings.Any(binding =>
                    binding.path == "<Gamepad>/dpad"),
                Is.True);
            Assert.That(
                FarionInputActions.UiSubmit.bindings.Any(binding =>
                    binding.path == "<Gamepad>/buttonSouth"),
                Is.True);
            Assert.That(
                FarionInputActions.UiCancel.bindings.Any(binding =>
                    binding.path == "<Gamepad>/buttonEast"),
                Is.True);
        }
    }
}
