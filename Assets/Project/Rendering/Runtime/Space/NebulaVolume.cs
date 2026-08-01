using UnityEngine;

namespace Farion.Rendering.Space
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class NebulaVolume : MonoBehaviour
    {
        public static NebulaVolume Active { get; private set; }

        [Header("Volume")]
        [Min(1f)]
        [SerializeField] float radius = 8000f;
        [Range(0.01f, 0.5f)]
        [SerializeField] float edgeFade = 0.5f;

        [Header("Color")]
        [ColorUsage(false, true)]
        [SerializeField] Color deepColor = new(0.03f, 0.07f, 0.23529412f, 1f);
        [ColorUsage(false, true)]
        [SerializeField] Color midColor = new(0.110728174f, 0.13298237f, 0.40138966f, 1f);
        [ColorUsage(false, true)]
        [SerializeField] Color highlightColor = new(1.3977199f, 0.23770505f, 0f, 1f);

        [Header("Structure")]
        [Min(0.1f)]
        [SerializeField] float structureScale = 11.27f;
        [Range(0f, 3f)]
        [SerializeField] float density = 0.4f;
        [Min(0f)]
        [SerializeField] float brightness = 1.42f;
        [Range(0f, 2f)]
        [Tooltip("How quickly distant scene geometry is obscured inside the nebula. Lower values preserve visibility.")]
        [SerializeField] float extinction;
        [InspectorName("Max Step Count")]
        [Tooltip("Upper raymarch budget. Larger structure scales need more samples and cost more GPU time.")]
        [Range(16, 192)]
        [SerializeField] int stepCount = 144;
        [SerializeField] float seed = 333f;
        [SerializeField] Texture2D noiseTexture;

        [Header("Motion")]
        [SerializeField] Vector3 driftDirection = new(0.7f, 0.15f, 0.45f);
        [Min(0f)]
        [SerializeField] float driftSpeed = 0.018f;

        public float Radius => Mathf.Max(1f, radius);
        public float EdgeFade => Mathf.Clamp(edgeFade, 0.01f, 0.5f);
        public Color DeepColor => deepColor;
        public Color MidColor => midColor;
        public Color HighlightColor => highlightColor;
        public float StructureScale => Mathf.Max(0.1f, structureScale);
        public float Density => Mathf.Max(0f, density);
        public float Brightness => Mathf.Max(0f, brightness);
        public float Extinction => Mathf.Max(0f, extinction);
        public int StepCount => Mathf.Clamp(stepCount, 16, 192);
        public float Seed => seed;
        public Texture2D NoiseTexture => noiseTexture;
        public Vector3 DriftDirection => driftDirection.sqrMagnitude > 0.0001f
            ? driftDirection.normalized
            : Vector3.right;
        public float DriftSpeed => Mathf.Max(0f, driftSpeed);

        void OnEnable()
        {
            Active = this;
        }

        void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        void OnValidate()
        {
            radius = Mathf.Max(1f, radius);
            structureScale = Mathf.Max(0.1f, structureScale);
            density = Mathf.Max(0f, density);
            brightness = Mathf.Max(0f, brightness);
            extinction = Mathf.Max(0f, extinction);
            stepCount = Mathf.Clamp(stepCount, 16, 192);
            driftSpeed = Mathf.Max(0f, driftSpeed);

            if (isActiveAndEnabled)
            {
                Active = this;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.45f, 0.3f, 0.85f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
    }
}
