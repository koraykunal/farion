using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [System.Serializable]
    public sealed class BiomeSurfaceTextureSet
    {
        [SerializeField] Texture2D baseColor;
        [SerializeField] Texture2D normal;
        [SerializeField] Texture2D roughness;
        [SerializeField] Texture2D ambientOcclusion;
        [SerializeField] Texture2D height;

        [Min(0.001f)]
        [SerializeField] float triplanarScale = 18f;
        [Range(0f, 1f)]
        [SerializeField] float heightStrength = 0.05f;

        public Texture2D BaseColor => baseColor;
        public Texture2D Normal => normal;
        public Texture2D Roughness => roughness;
        public Texture2D AmbientOcclusion => ambientOcclusion;
        public Texture2D Height => height;
        public float TriplanarScale => Mathf.Max(0.001f, triplanarScale);
        public float HeightStrength => Mathf.Clamp01(heightStrength);
        public bool HasBaseMaterial => baseColor != null || normal != null;
        public bool HasMaskData => roughness != null || ambientOcclusion != null || height != null;
    }
}
