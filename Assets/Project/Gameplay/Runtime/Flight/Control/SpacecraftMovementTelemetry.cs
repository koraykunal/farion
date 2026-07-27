using UnityEngine;

namespace Farion.Gameplay.Flight
{
    public readonly struct SpacecraftMovementTelemetry
    {
        public SpacecraftMovementTelemetry(
            SpacecraftFlightAssistMode assistMode,
            SpacecraftPilotCommand command,
            SpacecraftThrusterCommand thrusters,
            Vector3 localRelativeVelocity,
            Vector3 localAngularVelocity,
            Vector3 localLinearAcceleration,
            Vector3 localAngularAcceleration,
            Vector3 worldRelativeVelocity,
            bool boostActive,
            float boostBlend,
            float boostCharge)
        {
            AssistMode = assistMode;
            Command = command;
            Thrusters = thrusters;
            LocalRelativeVelocity = localRelativeVelocity;
            LocalAngularVelocity = localAngularVelocity;
            LocalLinearAcceleration = localLinearAcceleration;
            LocalAngularAcceleration = localAngularAcceleration;
            WorldRelativeVelocity = worldRelativeVelocity;
            BoostActive = boostActive;
            BoostBlend = Mathf.Clamp01(boostBlend);
            BoostCharge = Mathf.Clamp01(boostCharge);
        }

        public SpacecraftFlightAssistMode AssistMode { get; }
        public SpacecraftPilotCommand Command { get; }
        public SpacecraftThrusterCommand Thrusters { get; }
        public Vector3 LocalRelativeVelocity { get; }
        public Vector3 LocalAngularVelocity { get; }
        public Vector3 LocalLinearAcceleration { get; }
        public Vector3 LocalAngularAcceleration { get; }
        public Vector3 WorldRelativeVelocity { get; }
        public bool BoostActive { get; }
        public float BoostBlend { get; }
        public float BoostCharge { get; }

        public float RelativeSpeed => WorldRelativeVelocity.magnitude;
        public bool FlightAssistEnabled => AssistMode == SpacecraftFlightAssistMode.Assisted;

        public static SpacecraftMovementTelemetry Empty => new(
            SpacecraftFlightAssistMode.Assisted,
            SpacecraftPilotCommand.None,
            SpacecraftThrusterCommand.None,
            Vector3.zero,
            Vector3.zero,
            Vector3.zero,
            Vector3.zero,
            Vector3.zero,
            boostActive: false,
            boostBlend: 0f,
            boostCharge: 1f);
    }
}
