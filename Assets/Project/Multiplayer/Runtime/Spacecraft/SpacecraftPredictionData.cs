using Farion.Gameplay.Flight;
using FishNet.Object.Prediction;
using UnityEngine;

namespace Farion.Multiplayer.Spacecraft
{
    public struct SpacecraftReplicateData : IReplicateData
    {
        public Vector3 Translation;
        public Vector2 Look;
        public float Roll;
        public bool Boost;
        public bool Brake;
        public bool ToggleFlightAssist;
        public uint OriginSequence;
        uint tick;

        public SpacecraftReplicateData(
            SpacecraftInputState input,
            uint originSequence)
        {
            SpacecraftInputState clamped = new(
                input.Translation,
                input.Look,
                input.Roll,
                input.Boost,
                input.Brake,
                input.ToggleFlightAssist);
            Translation = clamped.Translation;
            Look = clamped.Look;
            Roll = clamped.Roll;
            Boost = clamped.Boost;
            Brake = clamped.Brake;
            ToggleFlightAssist = clamped.ToggleFlightAssist;
            OriginSequence = originSequence;
            tick = 0;
        }

        public SpacecraftInputState Input => new(
            Translation,
            Look,
            Roll,
            Boost,
            Brake,
            ToggleFlightAssist);
        public void Dispose()
        {
        }
        public uint GetTick() => tick;
        public void SetTick(uint value) => tick = value;
    }

    public struct SpacecraftReconcileData : IReconcileData
    {
        public PredictionRigidbody PredictionRigidbody;
        public SpacecraftMotorState MotorState;
        public uint OriginSequence;
        uint tick;

        public SpacecraftReconcileData(
            PredictionRigidbody predictionRigidbody,
            SpacecraftMotorState motorState,
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
