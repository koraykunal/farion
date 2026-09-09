using System;
using UnityEngine;

namespace Farion.Gameplay.Character
{
    [Serializable]
    public struct ExplorerMotorState
    {
        public float SimulationTime;
        public float LastJumpTime;
        public float LastJumpRequestTime;
        public float LastWalkableGroundTime;
        public bool JumpQueued;
        public bool Grounded;
        public bool WalkableGround;
        public float GroundSlopeAngle;
        public float SurfaceSpeed;
        public Vector3 SurfaceVelocity;
        public float VerticalSpeed;
        public bool TouchingWater;
        public bool Underwater;
        public bool Swimming;
        public float WaterDepth;
        public float WaterSubmergedFraction;
        public Vector3 LocalUp;
        public Vector3 GroundNormal;
        public Vector3 SmoothedGroundNormal;
        public bool HasSmoothedGroundNormal;
    }
}
