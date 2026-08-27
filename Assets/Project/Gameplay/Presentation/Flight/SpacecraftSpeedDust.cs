using Farion.Gameplay.Flight;
using UnityEngine;
using UnityEngine.Rendering;

namespace Farion.Gameplay.Presentation.Flight
{
    [DefaultExecutionOrder(340)]
    [DisallowMultipleComponent]
    public sealed class SpacecraftSpeedDust : MonoBehaviour
    {
        const int MaxStreaks = 512;

        [Header("Source")]
        [SerializeField] SpacecraftMotor motor;
        [SerializeField] Transform viewReference;

        [Header("Field")]
        [Min(16)]
        [SerializeField] int streakCount = 220;
        [Min(1f)]
        [SerializeField] float fieldRadius = 55f;
        [Min(0f)]
        [SerializeField] float nearClearance = 4f;

        [Header("Response")]
        [Min(0f)]
        [SerializeField] float fadeInSpeed = 8f;
        [Min(1f)]
        [SerializeField] float fullOpacitySpeed = 90f;
        [Range(0f, 1f)]
        [SerializeField] float maximumOpacity = 0.55f;
        [Min(0.001f)]
        [SerializeField] float streakWidth = 0.05f;
        [Min(0f)]
        [SerializeField] float streakLengthPerSpeed = 0.06f;
        [Min(0.01f)]
        [SerializeField] float minimumStreakLength = 0.25f;
        [Min(0.01f)]
        [SerializeField] float maximumStreakLength = 9f;

        [Header("Presentation")]
        [SerializeField] Material dustMaterial;

        static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        readonly Vector3[] points = new Vector3[MaxStreaks];
        readonly Matrix4x4[] matrices = new Matrix4x4[MaxStreaks];
        Mesh quadMesh;
        MaterialPropertyBlock propertyBlock;
        RenderParams renderParams;
        bool seeded;
        Vector3 lastCenter;

        void OnValidate()
        {
            streakCount = Mathf.Clamp(streakCount, 16, MaxStreaks);
            maximumStreakLength = Mathf.Max(minimumStreakLength, maximumStreakLength);
            motor ??= GetComponentInParent<SpacecraftMotor>();
        }

        void OnEnable()
        {
            motor ??= GetComponentInParent<SpacecraftMotor>();
            seeded = false;
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || motor == null || dustMaterial == null)
            {
                return;
            }

            Transform view = ResolveView();
            if (view == null)
            {
                return;
            }

            Vector3 velocity = motor.Telemetry.WorldRelativeVelocity;
            float speed = velocity.magnitude;
            float opacity = fullOpacitySpeed > fadeInSpeed
                ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeInSpeed, fullOpacitySpeed, speed))
                : 0f;
            opacity *= maximumOpacity;
            if (opacity <= 0.001f)
            {
                seeded = false;
                return;
            }

            Vector3 center = view.position;
            if (seeded)
            {
                Vector3 centerDelta = center - lastCenter;
                if (centerDelta.sqrMagnitude > fieldRadius * fieldRadius)
                {
                    for (int i = 0; i < MaxStreaks; i++)
                    {
                        points[i] += centerDelta;
                    }
                }
            }

            lastCenter = center;
            EnsureSeeded(center);
            Vector3 direction = velocity / Mathf.Max(speed, 0.0001f);
            float length = Mathf.Clamp(
                speed * streakLengthPerSpeed,
                minimumStreakLength,
                maximumStreakLength);
            Quaternion orientation = Quaternion.LookRotation(
                Vector3.Cross(direction, view.right).sqrMagnitude > 0.0001f
                    ? Vector3.Cross(Vector3.Cross(direction, view.forward), direction).normalized
                    : view.up,
                direction);

            int count = Mathf.Clamp(streakCount, 16, MaxStreaks);
            Vector3 step = velocity * Time.deltaTime;
            for (int i = 0; i < count; i++)
            {
                Vector3 point = points[i] - step;
                Vector3 offset = point - center;
                if (offset.sqrMagnitude > fieldRadius * fieldRadius)
                {
                    point = center - offset.normalized * fieldRadius + RandomJitter(fieldRadius * 0.35f);
                    offset = point - center;
                }

                float clearance = Mathf.Max(nearClearance, 0.01f);
                if (offset.sqrMagnitude < clearance * clearance)
                {
                    point = center + offset.normalized * clearance;
                }

                points[i] = point;
                matrices[i] = Matrix4x4.TRS(
                    point,
                    orientation,
                    new Vector3(streakWidth, length, 1f));
            }

            EnsureRenderResources();
            propertyBlock.SetFloat(AlphaId, opacity);
            renderParams.matProps = propertyBlock;
            renderParams.worldBounds = new Bounds(center, Vector3.one * (fieldRadius * 2.5f));
            Graphics.RenderMeshInstanced(renderParams, quadMesh, 0, matrices, count);
        }

        Transform ResolveView()
        {
            if (viewReference != null)
            {
                return viewReference;
            }

            Camera camera = Camera.main;
            return camera != null ? camera.transform : null;
        }

        void EnsureSeeded(Vector3 center)
        {
            if (seeded)
            {
                return;
            }

            for (int i = 0; i < MaxStreaks; i++)
            {
                points[i] = center + RandomJitter(fieldRadius);
            }

            seeded = true;
        }

        void EnsureRenderResources()
        {
            if (quadMesh == null)
            {
                quadMesh = BuildQuad();
            }

            propertyBlock ??= new MaterialPropertyBlock();
            if (renderParams.material != dustMaterial)
            {
                renderParams = new RenderParams(dustMaterial)
                {
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    lightProbeUsage = LightProbeUsage.Off,
                    reflectionProbeUsage = ReflectionProbeUsage.Off,
                    layer = gameObject.layer
                };
            }
        }

        static Vector3 RandomJitter(float radius)
        {
            return Random.insideUnitSphere * radius;
        }

        static Mesh BuildQuad()
        {
            Mesh mesh = new()
            {
                name = "Farion Space Dust Streak",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 }
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
