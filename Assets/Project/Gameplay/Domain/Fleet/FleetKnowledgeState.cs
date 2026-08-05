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
        public IEnumerable<DefinitionId> Capabilities => capabilities;
        public IEnumerable<DefinitionId> Blueprints => blueprints;
        public IEnumerable<DefinitionId> Discoveries => discoveries;

        public bool HasCapability(DefinitionId id)
        {
            return id.IsValid && capabilities.Contains(id);
        }

        public bool HasBlueprint(DefinitionId id)
        {
            return id.IsValid && blueprints.Contains(id);
        }

        public bool HasDiscovery(DefinitionId id)
        {
            return id.IsValid && discoveries.Contains(id);
        }

        public bool TryUnlockCapability(DefinitionId id)
        {
            return TryAdd(capabilities, id);
        }

        public bool TryUnlockBlueprint(DefinitionId id)
        {
            return TryAdd(blueprints, id);
        }

        public bool TryRecordDiscovery(DefinitionId id)
        {
            return TryAdd(discoveries, id);
        }

        bool TryAdd(HashSet<DefinitionId> target, DefinitionId id)
        {
            if (!id.IsValid || target.Contains(id))
            {
                return false;
            }

            if (Revision == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "Fleet knowledge revision capacity was exhausted.");
            }

            target.Add(id);
            Revision++;
            return true;
        }
    }
}
