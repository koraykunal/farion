using System;
using System.Collections.Generic;
using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Domain.Fleet
{
    public sealed class FleetKnowledgeState
    {
        readonly HashSet<DefinitionId> capabilities = new();
        readonly HashSet<DefinitionId> blueprints = new();
        readonly HashSet<DefinitionId> discoveries = new();

        public long Revision { get; private set; }
        public IEnumerable<DefinitionId> Capabilities => Enumerate(capabilities);
        public IEnumerable<DefinitionId> Blueprints => Enumerate(blueprints);
        public IEnumerable<DefinitionId> Discoveries => Enumerate(discoveries);

        public bool HasCapability(DefinitionId capabilityId)
        {
            return capabilityId.IsValid && capabilities.Contains(capabilityId);
        }

        public bool HasBlueprint(DefinitionId blueprintId)
        {
            return blueprintId.IsValid && blueprints.Contains(blueprintId);
        }

        public bool HasDiscovery(DefinitionId discoveryId)
        {
            return discoveryId.IsValid && discoveries.Contains(discoveryId);
        }

        public FleetOperationResult TryUnlockCapability(
            DefinitionId capabilityId,
            long expectedRevision = -1L)
        {
            return TryUnlock(capabilities, capabilityId, expectedRevision);
        }

        public FleetOperationResult TryUnlockBlueprint(
            DefinitionId blueprintId,
            long expectedRevision = -1L)
        {
            return TryUnlock(blueprints, blueprintId, expectedRevision);
        }

        public FleetOperationResult TryRecordDiscovery(
            DefinitionId discoveryId,
            long expectedRevision = -1L)
        {
            return TryUnlock(discoveries, discoveryId, expectedRevision);
        }

        FleetOperationResult TryUnlock(
            HashSet<DefinitionId> target,
            DefinitionId definitionId,
            long expectedRevision)
        {
            if (expectedRevision >= 0L && expectedRevision != Revision)
            {
                return FleetOperationResult.StaleRevision;
            }

            if (!definitionId.IsValid)
            {
                return FleetOperationResult.InvalidIdentifier;
            }

            if (target.Contains(definitionId))
            {
                return FleetOperationResult.AlreadyExists;
            }

            if (Revision == long.MaxValue)
            {
                throw new InvalidOperationException("Fleet knowledge revision capacity was exhausted.");
            }

            Revision++;
            target.Add(definitionId);
            return FleetOperationResult.Succeeded;
        }

        static IEnumerable<DefinitionId> Enumerate(HashSet<DefinitionId> source)
        {
            foreach (DefinitionId definitionId in source)
            {
                yield return definitionId;
            }
        }
    }
}
