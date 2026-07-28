using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Domain.Fleet
{
    public sealed class PersonalShipState
    {
        readonly Dictionary<DefinitionId, PersistentEntityId> installedModules = new();
        readonly ReadOnlyDictionary<DefinitionId, PersistentEntityId> readOnlyInstalledModules;
        readonly BoundedResourceState hull;
        readonly BoundedResourceState fuel;

        public PersonalShipState(
            PersistentEntityId shipId,
            PersistentEntityId ownerPlayerId,
            PersistentEntityId cargoContainerId,
            string displayName,
            double maximumHull,
            double currentHull,
            double maximumFuel,
            double currentFuel)
        {
            if (!shipId.IsValid || !ownerPlayerId.IsValid || !cargoContainerId.IsValid)
            {
                throw new ArgumentException("Personal ships require valid ship, owner, and cargo ids.");
            }

            if (!ShipNamePolicy.TryNormalize(displayName, out string normalizedName))
            {
                throw new ArgumentException("Personal ship name is invalid.", nameof(displayName));
            }

            ShipId = shipId;
            OwnerPlayerId = ownerPlayerId;
            CargoContainerId = cargoContainerId;
            DisplayName = normalizedName;
            hull = new BoundedResourceState(maximumHull, currentHull);
            fuel = new BoundedResourceState(maximumFuel, currentFuel);
            readOnlyInstalledModules =
                new ReadOnlyDictionary<DefinitionId, PersistentEntityId>(installedModules);
        }

        public PersistentEntityId ShipId { get; }
        public PersistentEntityId OwnerPlayerId { get; }
        public PersistentEntityId CargoContainerId { get; }
        public string DisplayName { get; private set; }
        public double HullCurrent => hull.Current;
        public double HullMaximum => hull.Maximum;
        public double HullNormalized => hull.Normalized;
        public double FuelCurrent => fuel.Current;
        public double FuelMaximum => fuel.Maximum;
        public double FuelNormalized => fuel.Normalized;
        public bool IsDisabled => hull.IsEmpty;
        public long Revision { get; private set; }
        public IReadOnlyDictionary<DefinitionId, PersistentEntityId> InstalledModules =>
            readOnlyInstalledModules;

        public bool TryGetInstalledModule(
            DefinitionId slotId,
            out PersistentEntityId equipmentInstanceId)
        {
            equipmentInstanceId = PersistentEntityId.None;
            return slotId.IsValid &&
                   installedModules.TryGetValue(slotId, out equipmentInstanceId);
        }

        public bool IsEquipmentInstalled(PersistentEntityId equipmentInstanceId)
        {
            if (!equipmentInstanceId.IsValid)
            {
                return false;
            }

            foreach (PersistentEntityId installedInstanceId in installedModules.Values)
            {
                if (installedInstanceId == equipmentInstanceId)
                {
                    return true;
                }
            }

            return false;
        }

        public FleetOperationResult TryRename(
            string displayName,
            long expectedRevision = -1L)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return FleetOperationResult.StaleRevision;
            }

            if (!ShipNamePolicy.TryNormalize(displayName, out string normalizedName))
            {
                return FleetOperationResult.InvalidName;
            }

            if (string.Equals(DisplayName, normalizedName, StringComparison.Ordinal))
            {
                return FleetOperationResult.Succeeded;
            }

            IncrementRevision();
            DisplayName = normalizedName;
            return FleetOperationResult.Succeeded;
        }

        public FleetOperationResult TryConsumeFuel(
            double amount,
            long expectedRevision = -1L)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return FleetOperationResult.StaleRevision;
            }

            if (!IsPositiveFinite(amount))
            {
                return FleetOperationResult.InvalidAmount;
            }

            if (IsDisabled)
            {
                return FleetOperationResult.ShipDisabled;
            }

            if (fuel.Current < amount)
            {
                return FleetOperationResult.InsufficientResource;
            }

            IncrementRevision();
            fuel.TryConsume(amount);
            return FleetOperationResult.Succeeded;
        }

        public FleetOperationResult TryRefuel(
            double amount,
            out double accepted,
            long expectedRevision = -1L)
        {
            accepted = 0d;
            if (!MatchesRevision(expectedRevision))
            {
                return FleetOperationResult.StaleRevision;
            }

            if (!IsPositiveFinite(amount))
            {
                return FleetOperationResult.InvalidAmount;
            }

            accepted = Math.Min(amount, fuel.Maximum - fuel.Current);
            if (accepted > 0d)
            {
                IncrementRevision();
                fuel.Restore(accepted);
            }

            return FleetOperationResult.Succeeded;
        }

        public FleetOperationResult TryApplyDamage(
            double amount,
            out double applied,
            long expectedRevision = -1L)
        {
            applied = 0d;
            if (!MatchesRevision(expectedRevision))
            {
                return FleetOperationResult.StaleRevision;
            }

            if (!IsPositiveFinite(amount))
            {
                return FleetOperationResult.InvalidAmount;
            }

            applied = Math.Min(amount, hull.Current);
            if (applied > 0d)
            {
                IncrementRevision();
                hull.Deplete(applied);
            }

            return FleetOperationResult.Succeeded;
        }

        public FleetOperationResult TryRepair(
            double amount,
            out double accepted,
            long expectedRevision = -1L)
        {
            accepted = 0d;
            if (!MatchesRevision(expectedRevision))
            {
                return FleetOperationResult.StaleRevision;
            }

            if (!IsPositiveFinite(amount))
            {
                return FleetOperationResult.InvalidAmount;
            }

            accepted = Math.Min(amount, hull.Maximum - hull.Current);
            if (accepted > 0d)
            {
                IncrementRevision();
                hull.Restore(accepted);
            }

            return FleetOperationResult.Succeeded;
        }

        internal FleetOperationResult TryInstallModule(
            DefinitionId slotId,
            PersistentEntityId equipmentInstanceId,
            out PersistentEntityId displacedInstanceId,
            long expectedRevision = -1L)
        {
            displacedInstanceId = PersistentEntityId.None;
            if (!MatchesRevision(expectedRevision))
            {
                return FleetOperationResult.StaleRevision;
            }

            if (!slotId.IsValid || !equipmentInstanceId.IsValid)
            {
                return FleetOperationResult.InvalidIdentifier;
            }

            foreach (KeyValuePair<DefinitionId, PersistentEntityId> pair in installedModules)
            {
                if (pair.Value == equipmentInstanceId && pair.Key != slotId)
                {
                    return FleetOperationResult.Conflict;
                }
            }

            if (installedModules.TryGetValue(slotId, out PersistentEntityId existing))
            {
                if (existing == equipmentInstanceId)
                {
                    return FleetOperationResult.Succeeded;
                }

                displacedInstanceId = existing;
            }

            IncrementRevision();
            installedModules[slotId] = equipmentInstanceId;
            return FleetOperationResult.Succeeded;
        }

        internal FleetOperationResult TryUninstallModule(
            DefinitionId slotId,
            out PersistentEntityId removedInstanceId,
            long expectedRevision = -1L)
        {
            removedInstanceId = PersistentEntityId.None;
            if (!MatchesRevision(expectedRevision))
            {
                return FleetOperationResult.StaleRevision;
            }

            if (!slotId.IsValid)
            {
                return FleetOperationResult.InvalidIdentifier;
            }

            if (!installedModules.TryGetValue(slotId, out removedInstanceId))
            {
                return FleetOperationResult.NotFound;
            }

            IncrementRevision();
            installedModules.Remove(slotId);
            return FleetOperationResult.Succeeded;
        }

        public bool MatchesRevision(long expectedRevision)
        {
            return expectedRevision < 0L || expectedRevision == Revision;
        }

        internal PersonalShipState Clone()
        {
            PersonalShipState clone = new(
                ShipId,
                OwnerPlayerId,
                CargoContainerId,
                DisplayName,
                hull.Maximum,
                hull.Current,
                fuel.Maximum,
                fuel.Current);
            foreach (KeyValuePair<DefinitionId, PersistentEntityId> pair in installedModules)
            {
                clone.installedModules.Add(pair.Key, pair.Value);
            }

            clone.Revision = Revision;
            return clone;
        }

        internal void ReplaceWith(PersonalShipState replacement)
        {
            if (replacement == null ||
                replacement.ShipId != ShipId ||
                replacement.OwnerPlayerId != OwnerPlayerId ||
                replacement.CargoContainerId != CargoContainerId)
            {
                throw new InvalidOperationException(
                    "Personal ship state replacement must target the same ship.");
            }

            DisplayName = replacement.DisplayName;
            hull.ReplaceWith(replacement.hull);
            fuel.ReplaceWith(replacement.fuel);
            installedModules.Clear();
            foreach (KeyValuePair<DefinitionId, PersistentEntityId> pair
                     in replacement.installedModules)
            {
                installedModules.Add(pair.Key, pair.Value);
            }

            Revision = replacement.Revision;
        }

        void IncrementRevision()
        {
            if (Revision == long.MaxValue)
            {
                throw new InvalidOperationException("Personal ship revision capacity was exhausted.");
            }

            Revision++;
        }

        static bool IsPositiveFinite(double value)
        {
            return value > 0d && !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
