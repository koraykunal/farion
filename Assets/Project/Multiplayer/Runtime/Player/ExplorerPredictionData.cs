using Farion.Gameplay.Character;
using FishNet.Object.Prediction;
using UnityEngine;

namespace Farion.Multiplayer.Player
{
    public struct ExplorerReplicateData : IReplicateData
    {
        public Vector2 Movement;
        public float YawDegrees;
        public float PitchDegrees;
        public bool Jump;
        public bool Sprint;
        public bool SwimAscend;
        public uint OriginSequence;

        uint tick;

        public ExplorerReplicateData(
            Vector2 movement,
            float yawDegrees,
            float pitchDegrees,
            bool jump,
            bool sprint,
            bool swimAscend,
            uint originSequence)
        {
            FirstPersonMotorInput input = new(
                movement,
                yawDegrees,
                jump,
                sprint,
                swimAscend);
            Movement = input.Movement;
            YawDegrees = input.YawDegrees;
            PitchDegrees = Mathf.Clamp(
                float.IsFinite(pitchDegrees) ? pitchDegrees : 0f,
                -FirstPersonMotor.MaximumViewPitchDegrees,
                FirstPersonMotor.MaximumViewPitchDegrees);
            Jump = input.Jump;
            Sprint = input.Sprint;
            SwimAscend = input.SwimAscend;
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
