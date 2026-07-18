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
        [SerializeField] bool autoFindMainCamera = true;

        [Header("Targets")]
        [SerializeField] bool autoDiscoverVisuals = true;
        [SerializeField] List<CelestialBodyVisual> visuals = new();

        [Header("Runtime")]
        [SerializeField] bool updateInEditMode = true;
        [SerializeField] bool updateEveryFrame = true;
        [Min(0f)]
        [SerializeField] float refreshInterval = 0.25f;

        float nextRefreshTime;

        void OnEnable()
        {
            RefreshTargets();
            ApplyLods();
        }

        void OnValidate()
        {
            refreshInterval = Mathf.Max(0f, refreshInterval);
            if (!Application.isPlaying && updateInEditMode)
            {
                RefreshTargets();
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

            if (ShouldRefreshTargets())
            {
                RefreshTargets();
            }

            ApplyLods();
        }

        [ContextMenu("Refresh LOD Targets")]
        public void RefreshTargets()
        {
            ResolveCamera();

            if (!autoDiscoverVisuals)
            {
                visuals.RemoveAll(visual => visual == null);
                return;
            }

            visuals.Clear();
            CelestialBodyVisual[] discovered = FindObjectsByType<CelestialBodyVisual>(FindObjectsInactive.Exclude);
            visuals.AddRange(discovered);
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

        bool ShouldRefreshTargets()
        {
            if (!autoDiscoverVisuals)
            {
                return false;
            }

            if (!Application.isPlaying)
            {
                return false;
            }

            if (refreshInterval <= 0f)
            {
                return true;
            }

            if (Time.unscaledTime < nextRefreshTime)
            {
                return false;
            }

            nextRefreshTime = Time.unscaledTime + refreshInterval;
            return true;
        }

        Camera ResolveCamera()
        {
            if (targetCamera != null || !autoFindMainCamera)
            {
                return targetCamera;
            }

            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindAnyObjectByType<Camera>(FindObjectsInactive.Exclude);
            }

            return targetCamera;
        }

        static float CalculateScreenHeight(CelestialBodyVisual visual, Camera camera)
        {
            Vector3 center = visual.transform.position;
            float radius = visual.HasRenderRadiusRange ? visual.RenderRadiusMinMax.y : visual.Body.Radius;
            Vector3 up = camera.transform.up * Mathf.Max(0.01f, radius);
            Vector3 viewportA = camera.WorldToViewportPoint(center - up);
            Vector3 viewportB = camera.WorldToViewportPoint(center + up);

            if (viewportA.z <= 0f && viewportB.z <= 0f)
            {
                return 0f;
            }

            return Mathf.Abs(viewportA.y - viewportB.y);
        }
    }
}
