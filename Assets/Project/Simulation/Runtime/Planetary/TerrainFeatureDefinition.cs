using UnityEngine;

namespace Farion.Simulation.Planetary
{
    [CreateAssetMenu(menuName = "Farion/Simulation/Planetary/Terrain Feature Definition", fileName = "SO_TerrainFeature")]
    public sealed class TerrainFeatureDefinition : ScriptableObject
    {
        [SerializeField] string featureId = "feature.crater_field";
        [SerializeField] string displayName = "Crater Field";
        [SerializeField] Color previewColor = new(0.55f, 0.5f, 0.44f, 1f);

        [Header("Sculpt")]
        [Tooltip("Relief the shape profile carves wherever this feature's noise window is active, so the label and the geometry agree.")]
        [SerializeField] TerrainSculptStyle sculptStyle = TerrainSculptStyle.None;
        [Tooltip("Metres of relief: crater depth or mesa terrace step.")]
        [SerializeField, Min(0f)] float sculptAmplitudeMeters = 12f;
        [Tooltip("Element footprint in metres: crater diameter. Ignored by mesas.")]
        [SerializeField, Min(1f)] float sculptFootprintMeters = 200f;

        public string FeatureId => string.IsNullOrWhiteSpace(featureId) ? name : featureId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? FeatureId : displayName.Trim();
        public Color PreviewColor => previewColor;
        public TerrainSculptStyle SculptStyle => sculptStyle;
        public float SculptAmplitudeMeters => Mathf.Max(0f, sculptAmplitudeMeters);
        public float SculptFootprintMeters => Mathf.Max(1f, sculptFootprintMeters);

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
