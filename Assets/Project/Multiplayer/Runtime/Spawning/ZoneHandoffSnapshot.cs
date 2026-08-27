using Farion.Core.Identity;
using Farion.Gameplay.Interaction;
using UnityEngine;

namespace Farion.Multiplayer.Spawning
{
    public readonly struct ZoneHandoffSnapshot
    {
        public ZoneHandoffSnapshot(
            Vector3 explorerSystemPosition,
            Vector3 explorerSystemVelocity,
            Quaternion explorerSystemRotation,
            PlayerPossessionMode possessionMode,
            GeneratedEntityId shipId,
            int shipSlot,
            Vector3 shipSystemPosition,
            Vector3 shipSystemVelocity,
            Quaternion shipSystemRotation)
        {
            ExplorerSystemPosition = explorerSystemPosition;
            ExplorerSystemVelocity = explorerSystemVelocity;
            ExplorerSystemRotation = explorerSystemRotation;
            PossessionMode = possessionMode;
            ShipId = shipId;
            ShipSlot = shipSlot;
            ShipSystemPosition = shipSystemPosition;
            ShipSystemVelocity = shipSystemVelocity;
            ShipSystemRotation = shipSystemRotation;
        }

        public Vector3 ExplorerSystemPosition { get; }
        public Vector3 ExplorerSystemVelocity { get; }
        public Quaternion ExplorerSystemRotation { get; }
        public PlayerPossessionMode PossessionMode { get; }
        public GeneratedEntityId ShipId { get; }
        public int ShipSlot { get; }
        public Vector3 ShipSystemPosition { get; }
        public Vector3 ShipSystemVelocity { get; }
        public Quaternion ShipSystemRotation { get; }
        public bool HasShip => ShipId.IsValid && ShipSlot >= 0;
    }
}
