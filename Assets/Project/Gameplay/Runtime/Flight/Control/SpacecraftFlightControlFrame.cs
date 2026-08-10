using UnityEngine;

namespace Farion.Gameplay.Flight
{
    readonly struct SpacecraftFlightControlFrame
    {
        public SpacecraftFlightControlFrame(
            SpacecraftPilotCommand command,
            Vector3 localRelativeVelocity,
            Vector3 localAngularVelocity,
            Vector3 localGravityAcceleration,
            bool flightAssistEnabled,
            float boostAuthority,
            float boostSurge)
        {
            Command = command;
            LocalRelativeVelocity = localRelativeVelocity;
            LocalAngularVelocity = localAngularVelocity;
            LocalGravityAcceleration = localGravityAcceleration;
            FlightAssistEnabled = flightAssistEnabled;
            BoostAuthority = Mathf.Clamp01(boostAuthority);
            BoostSurge = Mathf.Clamp01(boostSurge);
        }

        public SpacecraftPilotCommand Command { get; }
        public Vector3 LocalRelativeVelocity { get; }
        public Vector3 LocalAngularVelocity { get; }
        public Vector3 LocalGravityAcceleration { get; }
        public bool FlightAssistEnabled { get; }
        public float BoostAuthority { get; }
        public float BoostSurge { get; }
    }
}
