using UnityEngine;

namespace Farion.Simulation.Planetary
{
    [CreateAssetMenu(menuName = "Farion/Simulation/Planetary/Terrain Feature Definition", fileName = "SO_TerrainFeature")]
    public sealed class TerrainFeatureDefinition : ScriptableObject
    {
        [SerializeField] string featureId = "feature.crater_field";
        [SerializeField] string displayName = "Crater Field";
        [SerializeField] Color previewColor = new(0.55f, 0.5f, 0.44f, 1f);

        public string FeatureId => string.IsNullOrWhiteSpace(featureId) ? name : featureId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? FeatureId : displayName.Trim();
        public Color PreviewColor => previewColor;

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(featureId))
            {
                featureId = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = featureId;
            }
        }
    }
}
