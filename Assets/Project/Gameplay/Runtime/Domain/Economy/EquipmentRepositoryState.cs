using System;
using System.Collections.Generic;
using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Domain.Economy
{
    public sealed class EquipmentRepositoryState
    {
        readonly Dictionary<PersistentEntityId, EquipmentInstanceState> instances;
        readonly Dictionary<string, PersistentEntityId> instanceIdsBySerial;
        readonly Dictionary<PersistentEntityId, EquipmentLocation> locations;

        public EquipmentRepositoryState(PersistentEntityId repositoryId)
        {
            if (!repositoryId.IsValid)
            {
                throw new ArgumentException(
                    "Equipment repositories require a valid persistent id.",
                    nameof(repositoryId));
            }

            RepositoryId = repositoryId;
            instances = new Dictionary<PersistentEntityId, EquipmentInstanceState>();
            instanceIdsBySerial =
                new Dictionary<string, PersistentEntityId>(StringComparer.OrdinalIgnoreCase);
            locations = new Dictionary<PersistentEntityId, EquipmentLocation>();
        }

        EquipmentRepositoryState(
            PersistentEntityId repositoryId,
            long revision,
            Dictionary<PersistentEntityId, EquipmentInstanceState> instances,
            Dictionary<string, PersistentEntityId> instanceIdsBySerial,
            Dictionary<PersistentEntityId, EquipmentLocation> locations)
        {
            RepositoryId = repositoryId;
            Revision = revision;
            this.instances = instances;
            this.instanceIdsBySerial = instanceIdsBySerial;
            this.locations = locations;
        }

        public PersistentEntityId RepositoryId { get; }
        public int Count => instances.Count;
        public long Revision { get; private set; }
        public IEnumerable<EquipmentInstanceState> Instances
        {
            get
            {
                foreach (EquipmentInstanceState instance in instances.Values)
                {
                    yield return instance;
                }
            }
        }

        public bool Contains(PersistentEntityId instanceId)
        {
            return instanceId.IsValid && instances.ContainsKey(instanceId);
        }

        public bool TryGet(
            PersistentEntityId instanceId,
            out EquipmentInstanceState instance)
        {
            instance = null;
            return instanceId.IsValid && instances.TryGetValue(instanceId, out instance);
        }

        public bool TryGetLocation(
            PersistentEntityId instanceId,
            out EquipmentLocation location)
        {
            location = EquipmentLocation.Unassigned;
            return instanceId.IsValid && locations.TryGetValue(instanceId, out location);
        }

        public bool TryGetBySerialNumber(
            string serialNumber,
            out EquipmentInstanceState instance)
        {
            instance = null;
            string normalized = NormalizeSerialNumber(serialNumber);
            return !string.IsNullOrEmpty(normalized) &&
                   instanceIdsBySerial.TryGetValue(
                       normalized,
                       out PersistentEntityId instanceId) &&
                   instances.TryGetValue(instanceId, out instance);
        }

        public EquipmentRepositoryResult TryRegister(
            EquipmentInstanceState instance,
            long expectedRevision = InventoryContainerState.AnyRevision)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return EquipmentRepositoryResult.StaleRevision;
            }

            if (instance == null ||
                !instance.InstanceId.IsValid ||
                !instance.DefinitionId.IsValid ||
                string.IsNullOrEmpty(NormalizeSerialNumber(instance.SerialNumber)))
            {
                return EquipmentRepositoryResult.InvalidInstance;
            }

            if (instances.ContainsKey(instance.InstanceId))
            {
                return EquipmentRepositoryResult.DuplicateInstance;
            }

            if (instanceIdsBySerial.ContainsKey(instance.SerialNumber))
            {
                return EquipmentRepositoryResult.DuplicateSerialNumber;
            }

            IncrementRevision();
            instances.Add(instance.InstanceId, instance);
            instanceIdsBySerial.Add(instance.SerialNumber, instance.InstanceId);
            locations.Add(instance.InstanceId, EquipmentLocation.Unassigned);
            return EquipmentRepositoryResult.Succeeded;
        }

        public bool MatchesRevision(long expectedRevision)
        {
            return expectedRevision == InventoryContainerState.AnyRevision ||
                   expectedRevision == Revision;
        }

        internal EquipmentRepositoryResult TryMove(
            PersistentEntityId instanceId,
            EquipmentLocation expectedLocation,
            EquipmentLocation destination,
            long expectedRevision = InventoryContainerState.AnyRevision)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return EquipmentRepositoryResult.StaleRevision;
            }

            if (!instanceId.IsValid ||
                !expectedLocation.IsValid ||
                !destination.IsValid ||
                !locations.TryGetValue(instanceId, out EquipmentLocation current))
            {
                return EquipmentRepositoryResult.InvalidLocation;
            }

            if (current != expectedLocation)
            {
                return EquipmentRepositoryResult.OwnershipConflict;
            }

            if (current == destination)
            {
                return EquipmentRepositoryResult.Succeeded;
            }

            IncrementRevision();
            locations[instanceId] = destination;
            return EquipmentRepositoryResult.Succeeded;
        }

        internal EquipmentRepositoryState Clone()
        {
            return new EquipmentRepositoryState(
                RepositoryId,
                Revision,
                new Dictionary<PersistentEntityId, EquipmentInstanceState>(instances),
                new Dictionary<string, PersistentEntityId>(
                    instanceIdsBySerial,
                    StringComparer.OrdinalIgnoreCase),
                new Dictionary<PersistentEntityId, EquipmentLocation>(locations));
        }

        internal void ReplaceWith(EquipmentRepositoryState replacement)
        {
            if (replacement == null || replacement.RepositoryId != RepositoryId)
            {
                throw new InvalidOperationException(
                    "Equipment repository replacement must target the same repository.");
            }

            instances.Clear();
            foreach (KeyValuePair<PersistentEntityId, EquipmentInstanceState> pair
                     in replacement.instances)
            {
                instances.Add(pair.Key, pair.Value);
            }

            instanceIdsBySerial.Clear();
            foreach (KeyValuePair<string, PersistentEntityId> pair
                     in replacement.instanceIdsBySerial)
            {
                instanceIdsBySerial.Add(pair.Key, pair.Value);
            }

            locations.Clear();
            foreach (KeyValuePair<PersistentEntityId, EquipmentLocation> pair
                     in replacement.locations)
            {
                locations.Add(pair.Key, pair.Value);
            }

            Revision = replacement.Revision;
        }

        static string NormalizeSerialNumber(string serialNumber)
        {
            return string.IsNullOrWhiteSpace(serialNumber)
                ? string.Empty
                : serialNumber.Trim();
        }

        void IncrementRevision()
        {
            if (Revision == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "Equipment repository revision capacity was exhausted.");
            }

            Revision++;
        }
    }
}
