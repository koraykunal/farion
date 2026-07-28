using UnityEngine;

namespace Farion.Simulation.Planetary
{
    public enum SurfaceMaterialCategory
    {
        Rock,
        Regolith,
        Soil,
        Ice
    }

    [CreateAssetMenu(
        menuName = "Farion/Simulation/Planetary/Surface Material Definition",
        fileName = "SO_SurfaceMaterial")]
    public sealed class SurfaceMaterialDefinition : ScriptableObject
    {
        [SerializeField] string materialId = "surface.rock";
        [SerializeField] string displayName = "Rock";
        [SerializeField] SurfaceMaterialCategory category = SurfaceMaterialCategory.Rock;
        [SerializeField] Color previewColor = new(0.4f, 0.38f, 0.34f, 1f);

        public string MaterialId => string.IsNullOrWhiteSpace(materialId) ? name : materialId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? MaterialId : displayName.Trim();
        public SurfaceMaterialCategory Category => category;
        public Color PreviewColor => previewColor;

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(materialId))
            {
                materialId = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = materialId;
            }
        }
    }
}
