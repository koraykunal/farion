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

    public enum FootstepSurface
    {
        Rock,
        Gravel,
        Sand,
        Snow,
        Ice,
        Dirt,
        Grass,
        Mud,
        Metal
    }

    [CreateAssetMenu(
        menuName = "Farion/Simulation/Planetary/Surface Material Definition",
        fileName = "SO_SurfaceMaterial")]
    public sealed class SurfaceMaterialDefinition : ScriptableObject
    {
        [SerializeField] string materialId = "surface.rock";
        [SerializeField] string displayName = "Rock";
        [SerializeField] SurfaceMaterialCategory category = SurfaceMaterialCategory.Rock;
        [Tooltip("Drives the FMOD Surface parameter for footstep, jump and land events; label order matches the FMOD enumeration.")]
        [SerializeField] FootstepSurface footstepSurface = FootstepSurface.Rock;
        [SerializeField] Color previewColor = new(0.4f, 0.38f, 0.34f, 1f);

        public string MaterialId => string.IsNullOrWhiteSpace(materialId) ? name : materialId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? MaterialId : displayName.Trim();
        public SurfaceMaterialCategory Category => category;
        public FootstepSurface FootstepSurface => footstepSurface;
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
