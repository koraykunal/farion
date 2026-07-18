using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Celestial LOD Profile", fileName = "SO_CelestialLodProfile")]
    public sealed class CelestialLodProfile : ScriptableObject
    {
        const int MaxResolution = 128;

        [Header("Screen Height Thresholds")]
        [Tooltip("Viewport height fraction above which the highest detail mesh is used.")]
        [Range(0f, 1f)]
        [SerializeField] float lod0ScreenHeight = 0.5f;
        [Tooltip("Viewport height fraction above which the medium detail mesh is used.")]
        [Range(0f, 1f)]
        [SerializeField] float lod1ScreenHeight = 0.2f;

        [Header("Mesh Resolutions")]
        [Range(0, MaxResolution)]
        [SerializeField] int lod0Resolution = 64;
        [Range(0, MaxResolution)]
        [SerializeField] int lod1Resolution = 32;
        [Range(0, MaxResolution)]
        [SerializeField] int lod2Resolution = 16;

        public int LodCount => 3;

        public int GetResolution(int lodLevel)
        {
            return lodLevel switch
            {
                0 => lod0Resolution,
                1 => lod1Resolution,
                _ => lod2Resolution
            };
        }

        public int SelectLod(float screenHeight)
        {
            if (screenHeight > lod0ScreenHeight)
            {
                return 0;
            }

            return screenHeight > lod1ScreenHeight ? 1 : 2;
        }

        void OnValidate()
        {
            lod0ScreenHeight = Mathf.Clamp01(lod0ScreenHeight);
            lod1ScreenHeight = Mathf.Clamp(lod1ScreenHeight, 0f, lod0ScreenHeight);
            lod0Resolution = Mathf.Clamp(lod0Resolution, 0, MaxResolution);
            lod1Resolution = Mathf.Clamp(lod1Resolution, 0, lod0Resolution);
            lod2Resolution = Mathf.Clamp(lod2Resolution, 0, lod1Resolution);
        }
    }
}
