using UnityEngine;

namespace Farion.Gameplay.Flight
{
    readonly struct SpacecraftFlightControlOutput
    {
        public SpacecraftFlightControlOutput(
            Vector3 localLinearAcceleration,
            Vector3 localAngularAcceleration,
            Vector3 localAssistAcceleration,
            Vector3 localGravityCompensation)
        {
            LocalLinearAcceleration = localLinearAcceleration;
            LocalAngularAcceleration = localAngularAcceleration;
            LocalAssistAcceleration = localAssistAcceleration;
            LocalGravityCompensation = localGravityCompensation;
        }

        public Vector3 LocalLinearAcceleration { get; }
        public Vector3 LocalAngularAcceleration { get; }
        public Vector3 LocalAssistAcceleration { get; }
        public Vector3 LocalGravityCompensation { get; }
    }
}
