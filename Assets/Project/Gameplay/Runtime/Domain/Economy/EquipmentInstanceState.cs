using System;
using System.Collections.Generic;
using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Domain.Economy
{
    public enum EquipmentOperationResult
    {
        Succeeded = 0,
        InvalidDefinition = 1,
        InvalidUpgrade = 2,
        InvalidCondition = 3,
        DuplicateUpgrade = 4,
        MissingUpgrade = 5,
        StaleRevision = 6
    }

    public sealed class EquipmentInstanceState
    {
        readonly HashSet<DefinitionId> installedUpgrades = new();

        public EquipmentInstanceState(
            PersistentEntityId instanceId,
            DefinitionId definitionId,
            string serialNumber,
            float condition = 1f)
        {
            if (!instanceId.IsValid)
            {
                throw new ArgumentException("Equipment requires a persistent instance id.", nameof(instanceId));
            }

            if (!definitionId.IsValid)
            {
                throw new ArgumentException("Equipment requires a valid definition id.", nameof(definitionId));
            }

            if (!TryNormalizeSerialNumber(serialNumber, out string normalizedSerial))
            {
                throw new ArgumentException("Equipment serial numbers must contain 1-32 visible characters.", nameof(serialNumber));
            }

            if (!IsFinite(condition))
            {
                throw new ArgumentOutOfRangeException(nameof(condition));
            }

            InstanceId = instanceId;
            DefinitionId = definitionId;
            SerialNumber = normalizedSerial;
            Condition = Clamp01(condition);
        }

        public PersistentEntityId InstanceId { get; }
        public DefinitionId DefinitionId { get; }
        public string SerialNumber { get; }
        public float Condition { get; private set; }
        public long Revision { get; private set; }
        public IEnumerable<DefinitionId> InstalledUpgrades
        {
            get
            {
                foreach (DefinitionId upgradeId in installedUpgrades)
                {
                    yield return upgradeId;
                }
            }
        }

        public EquipmentOperationResult TrySetCondition(
            float condition,
            long expectedRevision = InventoryContainerState.AnyRevision)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return EquipmentOperationResult.StaleRevision;
            }

            if (!IsFinite(condition))
            {
                return EquipmentOperationResult.InvalidCondition;
            }

            float nextCondition = Clamp01(condition);
            if (Condition.Equals(nextCondition))
            {
                return EquipmentOperationResult.Succeeded;
            }

            IncrementRevision();
            Condition = nextCondition;
            return EquipmentOperationResult.Succeeded;
        }

        public EquipmentOperationResult TryInstallUpgrade(
            DefinitionId upgradeId,
            long expectedRevision = InventoryContainerState.AnyRevision)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return EquipmentOperationResult.StaleRevision;
            }

            if (!upgradeId.IsValid)
            {
                return EquipmentOperationResult.InvalidUpgrade;
            }

            if (installedUpgrades.Contains(upgradeId))
            {
                return EquipmentOperationResult.DuplicateUpgrade;
            }

            IncrementRevision();
            installedUpgrades.Add(upgradeId);
            return EquipmentOperationResult.Succeeded;
        }

        public EquipmentOperationResult TryRemoveUpgrade(
            DefinitionId upgradeId,
            long expectedRevision = InventoryContainerState.AnyRevision)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return EquipmentOperationResult.StaleRevision;
            }

            if (!upgradeId.IsValid)
            {
                return EquipmentOperationResult.InvalidUpgrade;
            }

            if (!installedUpgrades.Contains(upgradeId))
            {
                return EquipmentOperationResult.MissingUpgrade;
            }

            IncrementRevision();
            installedUpgrades.Remove(upgradeId);
            return EquipmentOperationResult.Succeeded;
        }

        public bool HasUpgrade(DefinitionId upgradeId)
        {
            return upgradeId.IsValid && installedUpgrades.Contains(upgradeId);
        }

        bool MatchesRevision(long expectedRevision)
        {
            return expectedRevision == InventoryContainerState.AnyRevision ||
                   expectedRevision == Revision;
        }

        void IncrementRevision()
        {
            if (Revision == long.MaxValue)
            {
                throw new InvalidOperationException("Equipment revision capacity was exhausted.");
            }

            Revision++;
        }

        static bool TryNormalizeSerialNumber(string serialNumber, out string normalized)
        {
            normalized = string.IsNullOrWhiteSpace(serialNumber)
                ? string.Empty
                : serialNumber.Trim();
            if (normalized.Length < 1 || normalized.Length > 32)
            {
                return false;
            }

            for (int i = 0; i < normalized.Length; i++)
            {
                if (char.IsControl(normalized[i]))
                {
                    return false;
                }
            }

            return true;
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }
    }
}
