using System.Collections.Generic;
using Farion.Gameplay.Domain.Fleet;
using Farion.Gameplay.Domain.Identity;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Farion.Gameplay.Fleet
{
    [MovedFrom(
        true,
        "Farion.Gameplay.Research",
        "Farion.Gameplay.Runtime",
        "FleetProgressionRuntime")]
    [DisallowMultipleComponent]
    public sealed class FleetKnowledgeRuntime : MonoBehaviour
    {
        FleetKnowledgeState knowledge = new();

        public FleetKnowledgeState Knowledge =>
            knowledge ??= new FleetKnowledgeState();

        public FleetKnowledgeSnapshot CaptureSnapshot()
        {
            return new FleetKnowledgeSnapshot(
                ToStrings(Knowledge.Capabilities),
                ToStrings(Knowledge.Blueprints),
                ToStrings(Knowledge.Discoveries));
        }

        public bool CanApplySnapshot(FleetKnowledgeSnapshot snapshot)
        {
            return snapshot != null && snapshot.IsValid;
        }

        public bool ApplySnapshot(FleetKnowledgeSnapshot snapshot)
        {
            if (!CanApplySnapshot(snapshot))
            {
                return false;
            }

            FleetKnowledgeState restored = new();
            if (!ApplyIds(
                    snapshot.Capabilities,
                    restored.TryUnlockCapability) ||
                !ApplyIds(
                    snapshot.Blueprints,
                    restored.TryUnlockBlueprint) ||
                !ApplyIds(
                    snapshot.Discoveries,
                    restored.TryRecordDiscovery))
            {
                return false;
            }

            knowledge = restored;
            return true;
        }

        static List<string> ToStrings(IEnumerable<DefinitionId> ids)
        {
            List<string> values = new();
            foreach (DefinitionId id in ids)
            {
                if (id.IsValid)
                {
                    values.Add(id.Value);
                }
            }

            values.Sort(System.StringComparer.Ordinal);
            return values;
        }

        static bool ApplyIds(
            IReadOnlyList<string> ids,
            System.Func<DefinitionId, bool> apply)
        {
            if (ids == null)
            {
                return true;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                if (!DefinitionId.TryCreate(ids[i], out DefinitionId id) ||
                    !apply(id))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
