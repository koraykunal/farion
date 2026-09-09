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
    }
}
