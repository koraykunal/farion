using System;
using System.Collections.Generic;
using Farion.Gameplay.Domain.Identity;
using UnityEngine;

namespace Farion.Gameplay.Research
{
    [Serializable]
    public sealed class FleetKnowledgeSnapshot
    {
        [SerializeField] List<string> capabilities = new();
        [SerializeField] List<string> blueprints = new();
        [SerializeField] List<string> discoveries = new();

        public FleetKnowledgeSnapshot(
            IReadOnlyList<string> capabilities,
            IReadOnlyList<string> blueprints,
            IReadOnlyList<string> discoveries)
        {
            this.capabilities = Copy(capabilities);
            this.blueprints = Copy(blueprints);
            this.discoveries = Copy(discoveries);
        }

        public IReadOnlyList<string> Capabilities => capabilities;
        public IReadOnlyList<string> Blueprints => blueprints;
        public IReadOnlyList<string> Discoveries => discoveries;
        public bool IsValid =>
            AllIdsAreValid(capabilities) &&
            AllIdsAreValid(blueprints) &&
            AllIdsAreValid(discoveries);

        static List<string> Copy(IReadOnlyList<string> source)
        {
            List<string> copy = new(source?.Count ?? 0);
            if (source == null)
            {
                return copy;
            }

            for (int i = 0; i < source.Count; i++)
            {
                string value = string.IsNullOrWhiteSpace(source[i])
                    ? string.Empty
                    : source[i].Trim();
                if (!string.IsNullOrEmpty(value))
                {
                    copy.Add(value);
                }
            }

            return copy;
        }

        static bool AllIdsAreValid(IReadOnlyList<string> ids)
        {
            if (ids == null)
            {
                return true;
            }

            HashSet<DefinitionId> uniqueIds = new();
            for (int i = 0; i < ids.Count; i++)
            {
                if (!DefinitionId.TryCreate(
                        ids[i],
                        out DefinitionId id) ||
                    !uniqueIds.Add(id))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
