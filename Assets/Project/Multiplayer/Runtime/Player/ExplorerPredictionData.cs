using Farion.Gameplay.Character;
using FishNet.Object.Prediction;
using UnityEngine;

namespace Farion.Multiplayer.Player
{
    public struct ExplorerReplicateData : IReplicateData
    {
        public Vector2 Movement;
        public float YawDegrees;
        public bool Jump;
        public bool Sprint;
        public uint OriginSequence;

        uint tick;

        public ExplorerReplicateData(
            Vector2 movement,
            float yawDegrees,
            bool jump,
            bool sprint,
            uint originSequence)
        {
            FirstPersonMotorInput input = new(
                movement,
                yawDegrees,
                jump,
                sprint);
            Movement = input.Movement;
            YawDegrees = input.YawDegrees;
            Jump = input.Jump;
            Sprint = input.Sprint;
            OriginSequence = originSequence;
            tick = 0;
        }

        public void Dispose()
        {
        }

        public uint GetTick() => tick;
        public void SetTick(uint value) => tick = value;
    }

    public struct ExplorerReconcileData : IReconcileData
    {
        public PredictionRigidbody PredictionRigidbody;
        public FirstPersonMotorState MotorState;
        public uint OriginSequence;

        uint tick;

        public ExplorerReconcileData(
            PredictionRigidbody predictionRigidbody,
            FirstPersonMotorState motorState,
            uint originSequence)
        {
            PredictionRigidbody = predictionRigidbody;
            MotorState = motorState;
            OriginSequence = originSequence;
            tick = 0;
        }

        public void Dispose()
        {
        }

        public uint GetTick() => tick;
        public void SetTick(uint value) => tick = value;
    }
}
