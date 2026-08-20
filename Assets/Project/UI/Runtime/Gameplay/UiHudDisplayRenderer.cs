using UnityEngine;
using UnityEngine.UI;

namespace Farion.UI.Gameplay
{
    /// <summary>
    /// Moves the flight HUD onto an offscreen canvas, renders it into a texture and
    /// draws that texture back on the original canvas through a display material. Every
    /// widget and label goes through the same shader, which a per-image material
    /// cannot do because TextMeshPro draws with its own distance field shader.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class UiHudDisplayRenderer : MonoBehaviour
    {
        // Far enough from the world that the offscreen camera can only ever see the
        // HUD canvas it is paired with.
        static readonly Vector3 OffscreenOrigin = new(0f, -100000f, 0f);

        [Header("Design")]
        [SerializeField] Material displayMaterial;

        [Tooltip("Render texture size relative to the screen. Lower values coarsen " +
            "the HUD pixels, which reads as an older display.")]
        [Range(0.25f, 1f)]
        [SerializeField] float resolutionScale = 1f;

        static readonly int TexelSizeId = Shader.PropertyToID("_HudTexelSize");

        RectTransform screenParent;
        int screenSiblingIndex;
        Canvas hudCanvas;
        Camera hudCamera;
        RawImage output;
        Material runtimeMaterial;
        RenderTexture texture;

        void OnEnable()
        {
            if (displayMaterial == null)
            {
                // Without a material the plain HUD is still the correct output.
                return;
            }

            if (hudCamera == null)
            {
                Build();
            }

            if (hudCamera == null)
            {
                return;
            }

            RefreshTexture();
            SetRigEnabled(true);
        }

        void OnDisable()
        {
            SetRigEnabled(false);
        }

        void OnDestroy()
        {
            Teardown();
        }

        void LateUpdate()
        {
            if (hudCamera != null)
            {
                RefreshTexture();
            }
        }

        void SetRigEnabled(bool value)
        {
            if (hudCamera != null)
            {
                hudCamera.enabled = value;
            }

            if (output != null)
            {
                output.enabled = value;
            }
        }

        void OnValidate()
        {
            resolutionScale = Mathf.Clamp(resolutionScale, 0.25f, 1f);
        }

        void Build()
        {
            Canvas sourceCanvas = GetComponentInParent<Canvas>();
            if (sourceCanvas == null)
            {
                return;
            }

            screenParent = transform.parent as RectTransform;
            screenSiblingIndex = transform.GetSiblingIndex();

            GameObject cameraObject = new("UI_HudRenderCamera", typeof(Camera))
            {
                hideFlags = HideFlags.DontSave,
            };
            cameraObject.transform.position = OffscreenOrigin;
            hudCamera = cameraObject.GetComponent<Camera>();
            hudCamera.orthographic = true;
            hudCamera.clearFlags = CameraClearFlags.SolidColor;
            hudCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            hudCamera.cullingMask = 1 << gameObject.layer;
            hudCamera.nearClipPlane = 0.1f;
            hudCamera.farClipPlane = 100f;
            hudCamera.depth = -100f;
            hudCamera.useOcclusionCulling = false;
            hudCamera.allowHDR = false;
            hudCamera.allowMSAA = false;

            GameObject canvasObject = new(
                "UI_HudRenderCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler))
            {
                hideFlags = HideFlags.DontSave,
                layer = gameObject.layer,
            };
            hudCanvas = canvasObject.GetComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            hudCanvas.worldCamera = hudCamera;
            hudCanvas.planeDistance = 10f;
            hudCanvas.sortingOrder = sourceCanvas.sortingOrder;
            hudCanvas.additionalShaderChannels = sourceCanvas.additionalShaderChannels;
            CopyScaler(
                sourceCanvas.GetComponent<CanvasScaler>(),
                canvasObject.GetComponent<CanvasScaler>());

            GameObject outputObject = new(
                "UI_HudDisplayOutput",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage))
            {
                hideFlags = HideFlags.DontSave,
                layer = gameObject.layer,
            };
            RectTransform outputRect = (RectTransform)outputObject.transform;
            outputRect.SetParent(screenParent, false);
            outputRect.SetSiblingIndex(screenSiblingIndex);
            outputRect.anchorMin = Vector2.zero;
            outputRect.anchorMax = Vector2.one;
            outputRect.offsetMin = Vector2.zero;
            outputRect.offsetMax = Vector2.zero;
            runtimeMaterial = new Material(displayMaterial)
            {
                name = displayMaterial.name + " (Runtime)",
                hideFlags = HideFlags.DontSave,
            };
            output = outputObject.GetComponent<RawImage>();
            output.material = runtimeMaterial;
            output.raycastTarget = false;

            transform.SetParent(hudCanvas.transform, false);
        }

        static void CopyScaler(CanvasScaler source, CanvasScaler target)
        {
            if (source == null)
            {
                return;
            }

            target.uiScaleMode = source.uiScaleMode;
            target.referenceResolution = source.referenceResolution;
            target.screenMatchMode = source.screenMatchMode;
            target.matchWidthOrHeight = source.matchWidthOrHeight;
            target.referencePixelsPerUnit = source.referencePixelsPerUnit;
            target.scaleFactor = source.scaleFactor;
        }

        void RefreshTexture()
        {
            int width = Mathf.Max(
                1,
                Mathf.RoundToInt(Screen.width * resolutionScale));
            int height = Mathf.Max(
                1,
                Mathf.RoundToInt(Screen.height * resolutionScale));
            if (texture != null &&
                texture.width == width &&
                texture.height == height)
            {
                return;
            }

            ReleaseTexture();
            texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "RT_HudDisplay",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                antiAliasing = 1,
                hideFlags = HideFlags.DontSave,
            };
            texture.Create();

            hudCamera.targetTexture = texture;
            if (output != null)
            {
                output.texture = texture;
            }

            if (runtimeMaterial != null)
            {
                runtimeMaterial.SetVector(
                    TexelSizeId,
                    new Vector4(1f / width, 1f / height, width, height));
            }
        }

        void Teardown()
        {
            if (hudCanvas != null && screenParent != null)
            {
                transform.SetParent(screenParent, false);
                transform.SetSiblingIndex(screenSiblingIndex);
            }

            if (hudCamera != null)
            {
                hudCamera.targetTexture = null;
                Destroy(hudCamera.gameObject);
                hudCamera = null;
            }

            if (hudCanvas != null)
            {
                Destroy(hudCanvas.gameObject);
                hudCanvas = null;
            }

            if (output != null)
            {
                Destroy(output.gameObject);
                output = null;
            }

            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
                runtimeMaterial = null;
            }

            ReleaseTexture();
        }

        void ReleaseTexture()
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            Destroy(texture);
            texture = null;
        }
    }
}
