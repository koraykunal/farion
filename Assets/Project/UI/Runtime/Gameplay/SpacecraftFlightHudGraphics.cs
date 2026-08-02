using Farion.Gameplay.Flight;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class SpacecraftFlightHudGraphics : MonoBehaviour
    {
        [Header("Propulsion")]
        [SerializeField] TMP_Text speedValueText;
        [SerializeField] TMP_Text speedUnitText;
        [SerializeField] TMP_Text assistValueText;
        [SerializeField] Image speedRailFillImage;
        [SerializeField] Image assistFrameImage;

        [Header("Boost Arc")]
        [SerializeField] TMP_Text boostValueText;
        [SerializeField] Image boostArcFillImage;
        [SerializeField] Image boostArcGlowImage;
        [SerializeField] Image boostFrameImage;
        [SerializeField] Image boostIndicatorImage;
        [SerializeField] Image panelFrameImage;

        [Header("Navigation Marker")]
        [SerializeField] RectTransform navigationArrow;
        [SerializeField] Image navigationArrowImage;
        [SerializeField] Image navigationArrowGlowImage;
        [SerializeField] CanvasGroup navigationArrowGroup;
        [SerializeField] CanvasGroup navigationArrowGlow;

        [Header("Landing Vector")]
        [SerializeField] CanvasGroup landingGroup;
        [SerializeField] RectTransform landingDot;
        [Min(1f)]
        [SerializeField] float landingTravel = 28f;
        [Min(0.1f)]
        [SerializeField] float landingSpeedRange = 20f;

        UiTheme theme;
        float targetBoostCharge = 1f;
        float displayedBoostCharge = 1f;
        float targetBoostBlend;
        bool boostActive;
        float targetSpeedRatio;
        float displayedSpeedRatio;
        Vector2 targetLanding;

        public void SetVisible(bool shouldShow)
        {
            if (gameObject.activeSelf != shouldShow)
            {
                gameObject.SetActive(shouldShow);
            }
        }

        public void ApplyTheme(UiTheme value)
        {
            theme = value;
            if (theme == null)
            {
                return;
            }

            SetFont(speedValueText, theme.InstrumentFont);
            SetFont(speedUnitText, theme.InstrumentFont);
            SetFont(assistValueText, theme.InstrumentFont);
            SetFont(boostValueText, theme.InstrumentFont);
            SetTextColor(speedValueText, theme.PrimaryText);
            SetTextColor(speedUnitText, theme.SupportingText);
            SetTextColor(boostValueText, theme.PrimaryText);

            SetColor(panelFrameImage, Color.white, 0.9f);
            SetColor(boostFrameImage, theme.Focus, 0.68f);
            SetColor(boostIndicatorImage, theme.Focus, 0.42f);
            SetColor(boostArcFillImage, theme.Focus, 0.88f);
            SetColor(boostArcGlowImage, theme.Focus, 0.14f);
            SetColor(speedRailFillImage, theme.Focus, 0.76f);
            SetColor(assistFrameImage, theme.Focus, 0.72f);
            SetColor(navigationArrowImage, theme.Focus, 0.94f);
            SetColor(navigationArrowGlowImage, theme.Focus, 0.32f);
            ApplySignalColors();
        }

        public void RefreshTelemetry(
            SpacecraftMovementTelemetry telemetry,
            float speedScale)
        {
            targetBoostCharge = telemetry.BoostCharge;
            targetBoostBlend = telemetry.BoostBlend;
            boostActive = telemetry.BoostActive;
            targetSpeedRatio = Mathf.Clamp01(
                telemetry.RelativeSpeed / Mathf.Max(1f, speedScale));

            if (speedValueText != null)
            {
                speedValueText.SetText("{0:0}", telemetry.RelativeSpeed);
            }

            if (assistValueText != null)
            {
                assistValueText.SetText(
                    telemetry.FlightAssistEnabled ? "ON" : "OFF");
            }

            if (boostValueText != null)
            {
                boostValueText.SetText(
                    "{0:0}%",
                    telemetry.BoostCharge * 100f);
            }

            ApplySignalColors();
        }

        public void RefreshNavigationMarker(Vector2 direction, bool onScreen)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.up;
            }

            float angle = Vector2.SignedAngle(
                Vector2.up,
                direction.normalized);

            if (navigationArrow != null)
            {
                navigationArrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (navigationArrowGroup != null)
            {
                navigationArrowGroup.alpha = onScreen ? 0.42f : 1f;
            }

            if (navigationArrowGlow != null)
            {
                navigationArrowGlow.alpha = onScreen ? 0.04f : 0.16f;
            }
        }

        public void RefreshLanding(
            float verticalSpeed,
            float lateralSpeed,
            bool hasFrame)
        {
            if (landingGroup != null)
            {
                landingGroup.alpha = hasFrame ? 1f : 0.18f;
            }

            targetLanding = hasFrame
                ? new Vector2(
                    Mathf.Clamp(lateralSpeed / landingSpeedRange, -1f, 1f),
                    Mathf.Clamp(verticalSpeed / landingSpeedRange, -1f, 1f)) *
                  landingTravel
                : Vector2.zero;
        }

        void Update()
        {
            float blend = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            displayedBoostCharge = Mathf.Lerp(
                displayedBoostCharge,
                targetBoostCharge,
                blend);
            displayedSpeedRatio = Mathf.Lerp(
                displayedSpeedRatio,
                targetSpeedRatio,
                blend);

            if (boostArcFillImage != null)
            {
                boostArcFillImage.fillAmount = displayedBoostCharge;
            }

            if (boostArcGlowImage != null)
            {
                boostArcGlowImage.fillAmount = displayedBoostCharge;
                Color glow = boostArcGlowImage.color;
                glow.a = 0.08f + targetBoostBlend * 0.16f +
                    (boostActive ? 0.08f : 0f);
                boostArcGlowImage.color = glow;
            }

            if (speedRailFillImage != null)
            {
                speedRailFillImage.fillAmount = displayedSpeedRatio;
            }

            MoveTowards(landingDot, targetLanding, blend);
        }

        void OnValidate()
        {
            landingTravel = Mathf.Max(1f, landingTravel);
            landingSpeedRange = Mathf.Max(0.1f, landingSpeedRange);
        }

        void ApplySignalColors()
        {
            if (theme == null)
            {
                return;
            }

            Color signal = Color.Lerp(
                theme.SupportingText,
                theme.Focus,
                targetBoostBlend);
            signal.a = boostActive ? 1f : 0.88f;
            SetTextColor(boostValueText, signal);
            SetColor(boostArcFillImage, signal, signal.a);

            Color assist = assistValueText != null &&
                assistValueText.text == "ON"
                    ? theme.Nominal
                    : theme.SupportingText;
            SetTextColor(assistValueText, assist);
        }

        static void MoveTowards(
            RectTransform target,
            Vector2 position,
            float blend)
        {
            if (target != null)
            {
                target.anchoredPosition = Vector2.Lerp(
                    target.anchoredPosition,
                    position,
                    blend);
            }
        }

        static void SetFont(TMP_Text target, TMP_FontAsset font)
        {
            if (target != null && font != null)
            {
                target.font = font;
            }
        }

        static void SetTextColor(TMP_Text target, Color color)
        {
            if (target != null)
            {
                target.color = color;
            }
        }

        static void SetColor(Image target, Color color, float alpha)
        {
            if (target == null)
            {
                return;
            }

            color.a = alpha;
            target.color = color;
        }
    }
}
