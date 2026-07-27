using System.Linq;
using Farion.Gameplay.Input;
using Farion.Gameplay.Interaction;
using NUnit.Framework;

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
        public void InputSchemaContainsRequiredActionMaps()
        {
            Assert.That(FarionInputActions.Asset.FindActionMap("OnFoot"), Is.Not.Null);
            Assert.That(FarionInputActions.Asset.FindActionMap("Flight"), Is.Not.Null);
            Assert.That(FarionInputActions.Asset.FindActionMap("Vehicle"), Is.Not.Null);
            Assert.That(FarionInputActions.Asset.FindActionMap("UI"), Is.Not.Null);
            Assert.That(FarionInputActions.FlightToggleAssist, Is.Not.Null);
            Assert.That(FarionInputActions.FlightToggleLandingGear, Is.Not.Null);
            Assert.That(FarionInputActions.UiPause, Is.Not.Null);
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
        }
    }
}
