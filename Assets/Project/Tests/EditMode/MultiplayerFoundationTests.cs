using Farion.Gameplay.Character;
using Farion.Gameplay.Session;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Session;
using Farion.Multiplayer.Spawning;
using Farion.Multiplayer.World;
using NUnit.Framework;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class MultiplayerFoundationTests
    {
        [TearDown]
        public void TearDown()
        {
            GameplaySessionModeRequest.Cancel();
        }

        [Test]
        public void SessionModeSurvivesRepeatedSceneComposition()
        {
            GameplaySessionModeRequest.Request(
                GameplaySessionMode.Multiplayer);

            Assert.That(
                GameplaySessionModeRequest.RequestedOrDefault,
                Is.EqualTo(GameplaySessionMode.Multiplayer));
            Assert.That(
                GameplaySessionModeRequest.RequestedOrDefault,
                Is.EqualTo(GameplaySessionMode.Multiplayer));
        }

        [Test]
        public void SessionModeRequestCanBeCancelledBeforeSceneComposition()
        {
            GameplaySessionModeRequest.Request(
                GameplaySessionMode.Multiplayer);

            GameplaySessionModeRequest.Cancel();

            Assert.That(
                GameplaySessionModeRequest.RequestedOrDefault,
                Is.EqualTo(GameplaySessionMode.Offline));
        }

        [Test]
        public void MotorInputClampsUntrustedMovementAndYaw()
        {
            FirstPersonMotorInput input = new(
                new Vector2(10f, -10f),
                float.PositiveInfinity,
                true,
                true);

            Assert.That(input.Movement.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(input.YawDegrees, Is.Zero);
            Assert.That(input.Jump, Is.True);
            Assert.That(input.Sprint, Is.True);
        }

        [Test]
        public void MotorStateRoundTripsForReconcile()
        {
            GameObject gameObject = new("Motor State Test");
            try
            {
                gameObject.AddComponent<Rigidbody>();
                gameObject.AddComponent<CapsuleCollider>();
                gameObject.AddComponent<Farion.Gameplay.Actors.CelestialActorProbe>();
                FirstPersonMotor motor = gameObject.AddComponent<FirstPersonMotor>();
                FirstPersonMotorState expected = new()
                {
                    SimulationTime = 12.5f,
                    LastJumpTime = 4f,
                    Grounded = true,
                    WalkableGround = true,
                    LocalUp = Vector3.up,
                    GroundNormal = Vector3.forward,
                    SmoothedGroundNormal = Vector3.right,
                    HasSmoothedGroundNormal = true
                };

                motor.RestoreState(expected);
                FirstPersonMotorState actual = motor.CaptureState();

                Assert.That(actual.SimulationTime, Is.EqualTo(expected.SimulationTime));
                Assert.That(actual.LastJumpTime, Is.EqualTo(expected.LastJumpTime));
                Assert.That(actual.Grounded, Is.True);
                Assert.That(actual.WalkableGround, Is.True);
                Assert.That(actual.GroundNormal, Is.EqualTo(expected.GroundNormal));
                Assert.That(actual.SmoothedGroundNormal, Is.EqualTo(expected.SmoothedGroundNormal));
                Assert.That(actual.HasSmoothedGroundNormal, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void OriginSequenceRejectsDuplicateAndStaleMessages()
        {
            WorldOriginSequenceState state = new();

            Assert.That(
                state.TryAccept(1, new Vector3(100f, 0f, 0f), out Vector3 first),
                Is.True);
            Assert.That(first, Is.EqualTo(new Vector3(100f, 0f, 0f)));
            Assert.That(
                state.TryAccept(1, new Vector3(200f, 0f, 0f), out _),
                Is.False);
            Assert.That(
                state.TryAccept(0, Vector3.zero, out _),
                Is.False);
            Assert.That(state.AccumulatedOrigin, Is.EqualTo(new Vector3(100f, 0f, 0f)));
        }

        [Test]
        public void LateJoinOriginUsesAccumulatedDelta()
        {
            WorldOriginSequenceState state = new();

            Assert.That(
                state.TryAccept(3, new Vector3(450f, -20f, 8f), out Vector3 delta),
                Is.True);
            Assert.That(delta, Is.EqualTo(new Vector3(450f, -20f, 8f)));
            Assert.That(state.Sequence, Is.EqualTo(3));
        }

        [Test]
        public void FourSpawnSlotsAreUniqueAndFifthIsRejected()
        {
            SpawnSlotAllocator allocator = new(4);

            for (int connection = 0; connection < 4; connection++)
            {
                Assert.That(allocator.TryReserve(connection, out int slot), Is.True);
                Assert.That(slot, Is.EqualTo(connection));
            }

            Assert.That(allocator.TryReserve(4, out int rejectedSlot), Is.False);
            Assert.That(rejectedSlot, Is.EqualTo(-1));
        }

        [Test]
        public void ReleasedSpawnSlotCanBeReused()
        {
            SpawnSlotAllocator allocator = new(4);
            allocator.TryReserve(10, out int firstSlot);
            allocator.Release(10);

            Assert.That(allocator.TryReserve(11, out int reusedSlot), Is.True);
            Assert.That(reusedSlot, Is.EqualTo(firstSlot));
        }

        [Test]
        public void LaunchArgumentsParseHostAndClientAddress()
        {
            Assert.That(
                MultiplayerCommandLine.TryParse(
                    new[] { "game.exe", "-farion-net=client", "-farion-address=10.0.0.5" },
                    out MultiplayerLaunchRequest request),
                Is.True);
            Assert.That(request.Mode, Is.EqualTo(MultiplayerLaunchMode.Client));
            Assert.That(request.Endpoint.Address, Is.EqualTo("10.0.0.5"));
            Assert.That(
                request.Endpoint.Port,
                Is.EqualTo(MultiplayerEndpoint.DefaultPort));
        }

        [TestCase("10.0.0.5", "10.0.0.5", MultiplayerEndpoint.DefaultPort)]
        [TestCase("10.0.0.5:7000", "10.0.0.5", (ushort)7000)]
        [TestCase("localhost", MultiplayerEndpoint.LoopbackAddress, MultiplayerEndpoint.DefaultPort)]
        [TestCase("[::1]:7000", "::1", (ushort)7000)]
        [TestCase("::1", "::1", MultiplayerEndpoint.DefaultPort)]
        public void EndpointParsesAddressAndPort(
            string value,
            string expectedAddress,
            ushort expectedPort)
        {
            Assert.That(
                MultiplayerEndpoint.TryParse(value, out MultiplayerEndpoint endpoint),
                Is.True);
            Assert.That(endpoint.Address, Is.EqualTo(expectedAddress));
            Assert.That(endpoint.Port, Is.EqualTo(expectedPort));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("10.0.0.5:0")]
        [TestCase("10.0.0.5:70000")]
        [TestCase("http://10.0.0.5")]
        public void EndpointRejectsMalformedValues(string value)
        {
            Assert.That(
                MultiplayerEndpoint.TryParse(value, out _),
                Is.False);
        }

        [TestCase(1UL, "explorer.net.1")]
        [TestCase(42UL, "explorer.net.42")]
        public void NetworkExplorerIdentityIsUniquePerSessionPlayer(
            ulong sessionPlayerId,
            string expected)
        {
            Assert.That(
                NetworkExplorerController.BuildPersistentId(sessionPlayerId),
                Is.EqualTo(expected));
        }

        [Test]
        public void HarvestDistanceIncludesBoundaryAndRejectsBeyondIt()
        {
            Assert.That(
                NetworkGameplayCommands.IsWithinHarvestDistance(
                    Vector3.zero,
                    Vector3.forward * 6f,
                    6f),
                Is.True);
            Assert.That(
                NetworkGameplayCommands.IsWithinHarvestDistance(
                    Vector3.zero,
                    Vector3.forward * 6.01f,
                    6f),
                Is.False);
        }
    }
}
