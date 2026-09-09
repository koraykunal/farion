using Farion.Gameplay.Character;
using FishNet.Object.Prediction;
using UnityEngine;

namespace Farion.Multiplayer.Player
{
    public struct ExplorerReplicateData : IReplicateData
    {
        public Vector3 Movement;
        public Vector3 LookDirection;
        public bool Aim;
        public bool Jump;
        public bool Sprint;
        public bool SwimAscend;
        public bool SwimDescend;
        public uint OriginSequence;

        uint tick;

        public ExplorerReplicateData(ExplorerMotorInput input, uint originSequence)
        {
            Movement = input.Movement;
            LookDirection = input.LookDirection;
            Aim = input.Aim;
            Jump = input.Jump;
            Sprint = input.Sprint;
            SwimAscend = input.SwimAscend;
            SwimDescend = input.SwimDescend;
            OriginSequence = originSequence;
            tick = 0;
        }

        public ExplorerMotorInput ToMotorInput() =>
            new(Movement, LookDirection, Aim, Jump, Sprint, SwimAscend, SwimDescend);

        public void Dispose()
        {
        }

        public uint GetTick() => tick;
        public void SetTick(uint value) => tick = value;
    }

    public struct ExplorerReconcileData : IReconcileData
    {
        public PredictionRigidbody PredictionRigidbody;
        public ExplorerMotorState MotorState;
        public uint OriginSequence;

        uint tick;

        public ExplorerReconcileData(
            PredictionRigidbody predictionRigidbody,
            ExplorerMotorState motorState,
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
