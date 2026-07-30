using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Farion.Gameplay.Research
{
    [DisallowMultipleComponent]
    public sealed class ResearchTerminalRuntime : MonoBehaviour
    {
        [SerializeField] List<ResearchDefinition> availableResearch = new();

        ReadOnlyCollection<ResearchDefinition> readOnlyResearch;

        public IReadOnlyList<ResearchDefinition> AvailableResearch =>
            readOnlyResearch ??= availableResearch.AsReadOnly();

        void OnValidate()
        {
            availableResearch ??= new List<ResearchDefinition>();
            availableResearch.RemoveAll(research => research == null);
            readOnlyResearch = null;
        }

        public bool CanOffer(ResearchDefinition research)
        {
            return research != null &&
                   !string.IsNullOrWhiteSpace(research.ResearchId) &&
                   availableResearch.Contains(research);
        }

        public bool TryGetResearch(
            string researchId,
            out ResearchDefinition research)
        {
            research = null;
            if (string.IsNullOrWhiteSpace(researchId))
            {
                return false;
            }

            string normalized = researchId.Trim();
            for (int i = 0; i < availableResearch.Count; i++)
            {
                ResearchDefinition candidate = availableResearch[i];
                if (candidate != null &&
                    string.Equals(
                        candidate.ResearchId,
                        normalized,
                        System.StringComparison.Ordinal))
                {
                    research = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
