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

        public FleetOperationResult TryCompleteResearch(
            DefinitionId researchId,
            IReadOnlyList<DefinitionId> blueprintIds,
            IReadOnlyList<DefinitionId> capabilityIds,
            long expectedRevision = -1L)
        {
            if (expectedRevision >= 0L && expectedRevision != Revision)
            {
                return FleetOperationResult.StaleRevision;
            }

            if (!researchId.IsValid ||
                !AllIdsAreValid(blueprintIds) ||
                !AllIdsAreValid(capabilityIds))
            {
                return FleetOperationResult.InvalidIdentifier;
            }

            if (discoveries.Contains(researchId))
            {
                return FleetOperationResult.AlreadyExists;
            }

            int mutationCount = 1 +
                                CountMissing(blueprints, blueprintIds) +
                                CountMissing(capabilities, capabilityIds);
            if (Revision > long.MaxValue - mutationCount)
            {
                return FleetOperationResult.CapacityExceeded;
            }

            discoveries.Add(researchId);
            AddRange(blueprints, blueprintIds);
            AddRange(capabilities, capabilityIds);
            Revision += mutationCount;
            return FleetOperationResult.Succeeded;
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

        static bool AllIdsAreValid(IReadOnlyList<DefinitionId> ids)
        {
            if (ids == null)
            {
                return true;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                if (!ids[i].IsValid)
                {
                    return false;
                }
            }

            return true;
        }

        static int CountMissing(
            HashSet<DefinitionId> target,
            IReadOnlyList<DefinitionId> ids)
        {
            if (ids == null)
            {
                return 0;
            }

            HashSet<DefinitionId> unique = new();
            for (int i = 0; i < ids.Count; i++)
            {
                if (!target.Contains(ids[i]))
                {
                    unique.Add(ids[i]);
                }
            }

            return unique.Count;
        }

        static void AddRange(
            HashSet<DefinitionId> target,
            IReadOnlyList<DefinitionId> ids)
        {
            if (ids == null)
            {
                return;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                target.Add(ids[i]);
            }
        }
    }
}
