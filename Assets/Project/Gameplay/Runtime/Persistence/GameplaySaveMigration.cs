using System;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Research;

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

            if (source.SchemaVersion == GameplaySaveData.CurrentSchemaVersion)
            {
                if (source.PayloadRevision != GameplaySaveData.CurrentPayloadRevision ||
                    source.PersonalShipCargo == null ||
                    source.FleetKnowledge == null)
                {
                    return false;
                }

                migrated = source;
                return true;
            }

            if (source.SchemaVersion != 3 && source.SchemaVersion != 4)
            {
                return false;
            }

            InventoryContainerComponent cargo = context.PersonalShipCargo;
            if (cargo == null || !cargo.ContainerId.IsValid)
            {
                return false;
            }

            InventoryContainerSnapshot legacyCargoDefault = new(
                cargo.ContainerId.Value,
                cargo.SlotCapacity,
                Array.Empty<InventoryStackSnapshot>());
            return TryMigrateToCurrent(
                source,
                legacyCargoDefault,
                out migrated);
        }

        public static bool TryMigrateToCurrent(
            GameplaySaveData source,
            InventoryContainerSnapshot legacyCargoDefault,
            out GameplaySaveData migrated)
        {
            migrated = null;
            if (source == null || !source.IsSupported)
            {
                return false;
            }

            if (source.SchemaVersion == GameplaySaveData.CurrentSchemaVersion)
            {
                if (source.PayloadRevision != GameplaySaveData.CurrentPayloadRevision ||
                    source.PersonalShipCargo == null ||
                    source.FleetKnowledge == null)
                {
                    return false;
                }

                migrated = source;
                return true;
            }

            if ((source.SchemaVersion != 3 &&
                 source.SchemaVersion != 4) ||
                legacyCargoDefault == null ||
                !legacyCargoDefault.HasValidContainerId ||
                !legacyCargoDefault.HasContainerId)
            {
                return false;
            }

            FleetKnowledgeSnapshot emptyKnowledge = new(
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());

            migrated = GameplaySaveData.CreateMigrated(
                source,
                legacyCargoDefault,
                emptyKnowledge);
            return true;
        }
    }
}
