using System;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Inventory;

namespace Farion.Gameplay.Persistence
{
    public static class GameplaySaveMigration
    {
        public static bool TryMigrateToCurrent(
            GameplaySaveData source,
            GameplaySaveContext context,
            out GameplaySaveData migrated)
        {
            migrated = null;
            if (source == null || !source.IsSupported)
            {
                return false;
            }

            if (IsValidCurrentPayload(source))
            {
                migrated = source;
                return true;
            }

            if (source.SchemaVersion == GameplaySaveData.CurrentSchemaVersion)
            {
                return false;
            }

            InventoryContainerComponent shuttleCargo = context.ShuttleCargo;
            FleetStorageInventory fleetStorage = context.FleetStorage;
            if (shuttleCargo == null ||
                fleetStorage == null ||
                !shuttleCargo.ContainerId.IsValid ||
                !fleetStorage.ContainerId.IsValid)
            {
                return false;
            }

            InventoryContainerSnapshot cargoDefault = EmptySnapshot(
                shuttleCargo);
            InventoryContainerSnapshot fleetStorageDefault = EmptySnapshot(
                fleetStorage);
            return TryMigrateToCurrent(
                source,
                cargoDefault,
                fleetStorageDefault,
                out migrated);
        }

        public static bool TryMigrateToCurrent(
            GameplaySaveData source,
            InventoryContainerSnapshot legacyCargoDefault,
            InventoryContainerSnapshot fleetStorageDefault,
            out GameplaySaveData migrated)
        {
            migrated = null;
            if (source == null || !source.IsSupported)
            {
                return false;
            }

            if (IsValidCurrentPayload(source))
            {
                migrated = source;
                return true;
            }

            if (source.SchemaVersion == GameplaySaveData.CurrentSchemaVersion ||
                legacyCargoDefault == null ||
                fleetStorageDefault == null ||
                !legacyCargoDefault.HasValidContainerId ||
                !legacyCargoDefault.HasContainerId ||
                !fleetStorageDefault.HasValidContainerId ||
                !fleetStorageDefault.HasContainerId)
            {
                return false;
            }

            InventoryContainerSnapshot shuttleCargo;
            FleetKnowledgeSnapshot fleetKnowledge;
            InventoryContainerSnapshot fleetStorage = fleetStorageDefault;
            if (source.SchemaVersion == 3 ||
                source.SchemaVersion == 4)
            {
                shuttleCargo = legacyCargoDefault;
                fleetKnowledge = EmptyKnowledge();
            }
            else if (source.SchemaVersion == 5 &&
                     source.PayloadRevision ==
                     GameplaySaveData.CurrentPayloadRevision &&
                     source.ShuttleCargo != null &&
                     source.FleetKnowledge != null)
            {
                shuttleCargo = source.ShuttleCargo;
                fleetKnowledge = source.FleetKnowledge;
            }
            else if (source.SchemaVersion == 6 &&
                     source.PayloadRevision ==
                     GameplaySaveData.CurrentPayloadRevision &&
                     source.ShuttleCargo != null &&
                     source.FleetStorage != null &&
                     source.FleetKnowledge != null)
            {
                shuttleCargo = source.ShuttleCargo;
                fleetKnowledge = source.FleetKnowledge;
                fleetStorage = source.FleetStorage;
            }
            else
            {
                return false;
            }

            migrated = GameplaySaveData.CreateMigrated(
                source,
                shuttleCargo,
                fleetStorage,
                fleetKnowledge);
            return true;
        }

        static bool IsValidCurrentPayload(GameplaySaveData source)
        {
            return source.SchemaVersion ==
                   GameplaySaveData.CurrentSchemaVersion &&
                   source.PayloadRevision ==
                   GameplaySaveData.CurrentPayloadRevision &&
                   source.ShuttleCargo != null &&
                   source.FleetStorage != null &&
                   source.FleetKnowledge != null;
        }

        static InventoryContainerSnapshot EmptySnapshot(
            InventoryContainerComponent container)
        {
            return new InventoryContainerSnapshot(
                container.ContainerId.Value,
                container.SlotCapacity,
                Array.Empty<InventoryStackSnapshot>());
        }

        static FleetKnowledgeSnapshot EmptyKnowledge()
        {
            return new FleetKnowledgeSnapshot(
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }
    }
}
