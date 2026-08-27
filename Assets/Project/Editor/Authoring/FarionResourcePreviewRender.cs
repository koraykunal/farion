using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Farion.Editor.Authoring
{
    static class FarionResourcePreviewRender
    {
        const string DefaultPrefabPath = "Assets/Project/Prefabs/Gameplay/ResourceNodes/PF_IronOreNode.prefab";
        const float SceneSurfaceOffset = 1.96f;
        const int Resolution = 720;

        public static void Run()
        {
            string outputFolder = System.Environment.GetEnvironmentVariable("FARION_PREVIEW_DIR");
            if (string.IsNullOrEmpty(outputFolder))
            {
                Debug.Log("[preview] FARION_PREVIEW_DIR not set");
                EditorApplication.Exit(1);
                return;
            }

            string prefabPath = System.Environment.GetEnvironmentVariable("FARION_PREVIEW_PREFAB");
            if (string.IsNullOrEmpty(prefabPath))
            {
                prefabPath = DefaultPrefabPath;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.Log("[preview] prefab missing at " + prefabPath);
                EditorApplication.Exit(1);
                return;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.30f, 0.34f, 1f);
            RenderSettings.fog = false;

            GameObject instance = Object.Instantiate(prefab);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Renderer renderer = instance.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                Debug.Log("[preview] no renderer");
                EditorApplication.Exit(1);
                return;
            }

            Bounds bounds = renderer.bounds;
            Debug.Log($"[preview] world bounds center={bounds.center} size={bounds.size} " +
                      $"bottomY={bounds.min.y:0.###} groundY={-SceneSurfaceOffset:0.###}");

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.position = new Vector3(0f, -SceneSurfaceOffset, 0f);
            ground.transform.localScale = Vector3.one * 2f;
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            Material groundMaterial = new(Shader.Find("Universal Render Pipeline/Lit"));
            groundMaterial.SetColor("_BaseColor", new Color(0.32f, 0.30f, 0.27f, 1f));
            groundMaterial.SetFloat("_Smoothness", 0.05f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;

            GameObject lightObject = new("Preview Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.9f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(42f, -135f, 0f);

            GameObject cameraObject = new("Preview Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.12f, 0.15f, 1f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;

            Vector3 focus = new(0f, (bounds.max.y - SceneSurfaceOffset) * 0.5f, 0f);
            float radius = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
            camera.orthographicSize = radius * 1.25f + 0.35f;

            Capture(camera, focus, new Vector3(0f, 0.12f, 1f), Path.Combine(outputFolder, "iron_front.png"));
            Capture(camera, focus, new Vector3(1f, 0.12f, 0f), Path.Combine(outputFolder, "iron_side.png"));
            Capture(camera, focus, new Vector3(1f, 0.45f, 1f), Path.Combine(outputFolder, "iron_quarter.png"));

            Debug.Log("[preview] done");
        }

        static void Capture(Camera camera, Vector3 focus, Vector3 direction, string path)
        {
            camera.transform.position = focus + direction.normalized * 20f;
            camera.transform.LookAt(focus, Vector3.up);

            RenderTexture target = new(Resolution, Resolution, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;

            Texture2D shot = new(Resolution, Resolution, TextureFormat.RGB24, mipChain: false);
            shot.ReadPixels(new Rect(0f, 0f, Resolution, Resolution), 0, 0);
            shot.Apply();

            RenderTexture.active = previous;
            camera.targetTexture = null;
            File.WriteAllBytes(path, shot.EncodeToPNG());

            Object.DestroyImmediate(shot);
            target.Release();
            Object.DestroyImmediate(target);
            Debug.Log("[preview] wrote " + path);
        }
    }
}
