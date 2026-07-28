using UnityEngine;
using UnityEngine.Serialization;

namespace Farion.Rendering.Celestial
{
    [System.Serializable]
    public sealed class SurfaceTextureSet
    {
        [SerializeField] Texture2D baseColor;
        [SerializeField] Texture2D normal;
        [SerializeField] Texture2D roughness;
        [SerializeField] Texture2D ambientOcclusion;
        [SerializeField] Texture2D height;
        [SerializeField] Texture2D emission;

        [Min(0.001f)]
        [FormerlySerializedAs("triplanarScale")]
        [SerializeField] float worldTileSize = 18f;
        [Range(0f, 1f)]
        [SerializeField] float heightStrength = 0.05f;
        [ColorUsage(false, true)]
        [SerializeField] Color emissionTint = Color.white;
        [Min(0f)]
        [SerializeField] float emissionStrength;

        public Texture2D BaseColor => baseColor;
        public Texture2D Normal => normal;
        public Texture2D Roughness => roughness;
        public Texture2D AmbientOcclusion => ambientOcclusion;
        public Texture2D Height => height;
        public Texture2D Emission => emission;
        public float WorldTileSize => Mathf.Max(0.001f, worldTileSize);
        public float HeightStrength => Mathf.Clamp01(heightStrength);
        public Color EmissionTint => emissionTint;
        public float EmissionStrength => Mathf.Max(0f, emissionStrength);
        public bool HasBaseMaterial => baseColor != null || normal != null;
        public bool HasMaskData => roughness != null || ambientOcclusion != null || height != null;
        public bool HasSurfaceTextures =>
            baseColor != null
            && normal != null
            && roughness != null;
        public bool HasAmbientOcclusion => ambientOcclusion != null;
        public bool HasHeight => height != null && HeightStrength > 0f;
        public bool HasEmission => emission != null && EmissionStrength > 0f;
    }
}
