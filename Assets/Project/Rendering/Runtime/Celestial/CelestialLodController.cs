using System.Collections.Generic;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CelestialLodController : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] Camera targetCamera;

        [Header("Targets")]
        [SerializeField] List<CelestialBodyVisual> visuals = new();

        [Header("Runtime")]
        [SerializeField] bool updateInEditMode = true;
        [SerializeField] bool updateEveryFrame = true;

        void OnEnable()
        {
            RemoveMissingTargets();
            ApplyLods();
        }

        void OnValidate()
        {
            RemoveMissingTargets();
            if (!Application.isPlaying && updateInEditMode)
            {
                ApplyLods();
            }
        }

        void LateUpdate()
        {
            if (!updateEveryFrame)
            {
                return;
            }

            if (!Application.isPlaying && !updateInEditMode)
            {
                return;
            }

            ApplyLods();
        }

        public void SetCamera(Camera camera)
        {
            targetCamera = camera;
        }

        public void RegisterVisual(CelestialBodyVisual visual)
        {
            if (visual != null && !visuals.Contains(visual))
            {
                visuals.Add(visual);
            }
        }

        public void UnregisterVisual(CelestialBodyVisual visual)
        {
            if (visual != null)
            {
                visuals.Remove(visual);
            }
        }

        [ContextMenu("Apply LODs Now")]
        public void ApplyLods()
        {
            Camera camera = ResolveCamera();
            if (camera == null)
            {
                return;
            }

            for (int i = visuals.Count - 1; i >= 0; i--)
            {
                CelestialBodyVisual visual = visuals[i];
                if (visual == null)
                {
                    visuals.RemoveAt(i);
                    continue;
                }

                float screenHeight = CalculateScreenHeight(visual, camera);
                visual.ApplyLodForScreenHeight(screenHeight);
            }
        }

        void RemoveMissingTargets()
        {
            visuals.RemoveAll(visual => visual == null);
        }

        Camera ResolveCamera()
        {
            return targetCamera;
        }

        static float CalculateScreenHeight(CelestialBodyVisual visual, Camera camera)
        {
            Vector3 center = visual.transform.position;
            float radius = visual.HasRenderRadiusRange ? visual.RenderRadiusMinMax.y : visual.Body.Radius;
            float distanceToCenter = Vector3.Distance(camera.transform.position, center);
            if (distanceToCenter <= radius * 2.5f)
            {
                return 1f;
            }

            Vector3 up = camera.transform.up * Mathf.Max(0.01f, radius);
            Vector3 viewportA = camera.WorldToViewportPoint(center - up);
            Vector3 viewportB = camera.WorldToViewportPoint(center + up);

            if (viewportA.z <= 0f && viewportB.z <= 0f)
            {
                return 0f;
            }

            return Mathf.Abs(viewportA.y - viewportB.y);
        }

#if UNITY_EDITOR
        [ContextMenu("Collect Scene Visuals")]
        void CollectSceneVisuals()
        {
            visuals.Clear();
            CelestialBodyVisual[] discovered = FindObjectsByType<CelestialBodyVisual>(FindObjectsInactive.Exclude);
            visuals.AddRange(discovered);

            if (targetCamera == null)
            {
                targetCamera = Camera.main != null
                    ? Camera.main
                    : FindAnyObjectByType<Camera>(FindObjectsInactive.Exclude);
            }
        }
#endif
    }
}
