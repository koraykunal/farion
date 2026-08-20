using System;
using Farion.Gameplay.Flight;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class UiSpacecraftFlightHudGraphics : MonoBehaviour
    {
        const float CautionFuelLevel = 0.3f;
        const float CriticalFuelLevel = 0.1f;

        [Serializable]
        sealed class ThrottleGauge
        {
            [SerializeField] RectTransform track;
            [SerializeField] RectTransform fill;
            [SerializeField] RectTransform marker;
            [SerializeField] Image fillImage;
            [SerializeField] Image trackImage;
            [SerializeField] Image centerImage;
            [SerializeField] Image markerImage;

            float targetSetting;
            float targetResponse;
            float displayedSetting;
            float displayedResponse;

            public void SetTargets(float setting, float response)
            {
                targetSetting = Mathf.Clamp(setting, -1f, 1f);
                targetResponse = Mathf.Clamp(response, -1f, 1f);
            }

            public void ApplyTheme(UiTheme theme)
            {
                SetColor(fillImage, theme.Focus, 0.5f);
                SetColor(trackImage, theme.Focus, 0.12f);
                SetColor(centerImage, theme.Focus, 0.34f);
                SetColor(markerImage, theme.PrimaryText, 0.95f);
            }

            public void Tick(float blend)
            {
                displayedSetting = Mathf.Lerp(displayedSetting, targetSetting, blend);
                displayedResponse = Mathf.Lerp(displayedResponse, targetResponse, blend);
                if (track == null)
                {
                    return;
                }

                float half = track.rect.width * 0.5f;
                if (fill != null)
                {
                    fill.anchorMin = new Vector2(0.5f, 0f);
                    fill.anchorMax = new Vector2(0.5f, 1f);
                    fill.pivot = new Vector2(displayedResponse >= 0f ? 0f : 1f, 0.5f);
                    fill.anchoredPosition = Vector2.zero;
                    fill.sizeDelta = new Vector2(
                        Mathf.Abs(displayedResponse) * half,
                        -6f);
                }

                if (marker != null)
                {
                    marker.anchorMin = new Vector2(0.5f, 0.5f);
                    marker.anchorMax = new Vector2(0.5f, 0.5f);
                    marker.pivot = new Vector2(0.5f, 0.5f);
                    marker.anchoredPosition = new Vector2(
                        displayedSetting * half,
                        0f);
                }
            }
        }

        [Header("Propulsion")]
        [SerializeField] TMP_Text speedValueText;
        [SerializeField] TMP_Text speedUnitText;
        [SerializeField] TMP_Text assistValueText;
        [SerializeField] Image assistFrameImage;

        [Header("Throttle")]
        [SerializeField] TMP_Text throttleValueText;
        [SerializeField] ThrottleGauge throttle = new();

        [Header("Fuel Arc")]
        [FormerlySerializedAs("boostValueText")]
        [SerializeField] TMP_Text fuelValueText;
        [FormerlySerializedAs("boostArcFillImage")]
        [SerializeField] Image fuelArcFillImage;
        [FormerlySerializedAs("boostArcGlowImage")]
        [SerializeField] Image fuelArcGlowImage;
        [SerializeField] Image boostFrameImage;
        [SerializeField] Image boostIndicatorImage;
        [SerializeField] Image panelFrameImage;

        [Header("Navigation Marker")]
        [SerializeField] RectTransform navigationArrow;
        [SerializeField] Image navigationArrowImage;
        [SerializeField] Image navigationArrowGlowImage;
        [SerializeField] CanvasGroup navigationArrowGroup;
        [SerializeField] CanvasGroup navigationArrowGlow;

        [Header("Velocity Vector")]
        [SerializeField] CanvasGroup velocityVectorGroup;
        [SerializeField] Image velocityVectorImage;

        [Header("Landing Vector")]
        [SerializeField] CanvasGroup landingGroup;
        [SerializeField] RectTransform landingDot;
        [SerializeField] Image landingDotImage;
        [Min(1f)]
        [SerializeField] float landingTravel = 28f;
        [Min(0.1f)]
        [SerializeField] float landingSpeedRange = 20f;

        UiTheme theme;

        UiTheme Theme => theme = UiTheme.Resolve(theme);
        float targetFuel = 1f;
        float displayedFuel = 1f;
        float targetBoostBlend;
        bool boostActive;
        Vector2 targetLanding;
        float targetVelocityVectorAlpha;

        public void SetVisible(bool shouldShow)
        {
            if (gameObject.activeSelf != shouldShow)
            {
                gameObject.SetActive(shouldShow);
            }
        }

        public void ApplyTheme(UiTheme value)
        {
            theme = UiTheme.Resolve(value);

            SetFont(speedValueText, Theme.InstrumentFont);
            SetFont(speedUnitText, Theme.InstrumentFont);
            SetFont(assistValueText, Theme.InstrumentFont);
            SetFont(fuelValueText, Theme.InstrumentFont);
            SetTextColor(speedValueText, Theme.PrimaryText);
            SetTextColor(speedUnitText, Theme.SupportingText);
            SetTextColor(fuelValueText, Theme.PrimaryText);

            SetColor(panelFrameImage, Color.white, 0.9f);
            SetColor(boostFrameImage, Theme.Focus, 0.68f);
            SetColor(boostIndicatorImage, Theme.Focus, 0.42f);
            SetColor(fuelArcFillImage, Theme.Focus, 0.88f);
            SetColor(fuelArcGlowImage, Theme.Focus, 0.14f);
            SetColor(assistFrameImage, Theme.Focus, 0.72f);
            SetColor(navigationArrowImage, Theme.Focus, 0.94f);
            SetColor(navigationArrowGlowImage, Theme.Focus, 0.32f);
            SetColor(velocityVectorImage, Theme.Focus, 0.86f);
            SetFont(throttleValueText, Theme.InstrumentFont);
            SetTextColor(throttleValueText, Theme.SupportingText);
            throttle.ApplyTheme(Theme);
            ApplySignalColors();
        }

        public void RefreshTelemetry(
            SpacecraftMovementTelemetry telemetry,
            float throttleSetting,
            float forwardSpeedScale,
            float reverseSpeedScale)
        {
            targetFuel = telemetry.FuelNormalized;
            targetBoostBlend = telemetry.BoostBlend;
            boostActive = telemetry.BoostActive;

            float forwardVelocity = telemetry.LocalRelativeVelocity.z;
            float response = forwardVelocity >= 0f
                ? forwardVelocity / Mathf.Max(1f, forwardSpeedScale)
                : forwardVelocity / Mathf.Max(1f, reverseSpeedScale);
            throttle.SetTargets(throttleSetting, response);

            if (throttleValueText != null)
            {
                throttleValueText.SetText("{0:0}%", throttleSetting * 100f);
            }

            if (speedValueText != null)
            {
                speedValueText.SetText("{0:0}", telemetry.RelativeSpeed);
            }

            if (assistValueText != null)
            {
                assistValueText.SetText(
                    telemetry.FlightAssistEnabled ? "ON" : "OFF");
            }

            if (fuelValueText != null)
            {
                fuelValueText.SetText(
                    "{0:0}%",
                    telemetry.FuelNormalized * 100f);
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
            float verticalLimit,
            float lateralLimit,
            bool hasFrame)
        {
            if (landingGroup != null)
            {
                landingGroup.alpha = hasFrame ? 1f : 0.18f;
            }

            float verticalRange = verticalLimit > 0.01f
                ? verticalLimit
                : landingSpeedRange;
            float lateralRange = lateralLimit > 0.01f
                ? lateralLimit
                : landingSpeedRange;
            float normalizedVertical = verticalSpeed / verticalRange;
            float normalizedLateral = lateralSpeed / lateralRange;

            bool withinLimits =
                !hasFrame ||
                IsWithinLandingLimits(normalizedVertical, normalizedLateral);

            targetLanding = hasFrame
                ? new Vector2(
                    Mathf.Clamp(normalizedLateral, -1f, 1f),
                    Mathf.Clamp(normalizedVertical, -1f, 1f)) * landingTravel
                : Vector2.zero;

            if (landingDotImage != null)
            {
                Color dot = withinLimits ? Theme.Nominal : Theme.Critical;
                dot.a = hasFrame ? 1f : 0.4f;
                landingDotImage.color = dot;
            }
        }

        internal static bool IsWithinLandingLimits(
            float normalizedVertical,
            float normalizedLateral)
        {
            return normalizedVertical >= -1f &&
                Mathf.Abs(normalizedLateral) <= 1f;
        }

        public void RefreshVelocityVector(bool visible)
        {
            targetVelocityVectorAlpha = visible ? 1f : 0f;
        }

        void Update()
        {
            float blend = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            displayedFuel = Mathf.Lerp(
                displayedFuel,
                targetFuel,
                blend);
            throttle.Tick(blend);

            if (velocityVectorGroup != null)
            {
                velocityVectorGroup.alpha = Mathf.Lerp(
                    velocityVectorGroup.alpha,
                    targetVelocityVectorAlpha,
                    blend);
            }

            if (fuelArcFillImage != null)
            {
                fuelArcFillImage.fillAmount = displayedFuel;
            }

            if (fuelArcGlowImage != null)
            {
                fuelArcGlowImage.fillAmount = displayedFuel;
                Color glow = fuelArcGlowImage.color;
                glow.a = 0.08f + targetBoostBlend * 0.16f +
                    (boostActive ? 0.08f : 0f);
                fuelArcGlowImage.color = glow;
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
            Color signal = ResolveFuelColor();
            signal.a = boostActive ? 1f : 0.88f;
            SetTextColor(fuelValueText, signal);
            SetColor(fuelArcFillImage, signal, signal.a);

            Color assist = assistValueText != null &&
                assistValueText.text == "ON"
                    ? Theme.Nominal
                    : Theme.SupportingText;
            SetTextColor(assistValueText, assist);
        }

        Color ResolveFuelColor()
        {
            if (targetFuel <= CriticalFuelLevel)
            {
                return Theme.Critical;
            }

            if (targetFuel <= CautionFuelLevel)
            {
                return Theme.Caution;
            }

            return Color.Lerp(Theme.SupportingText, Theme.Focus, targetBoostBlend);
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
