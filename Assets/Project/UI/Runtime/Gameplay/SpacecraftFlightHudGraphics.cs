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
        [Header("Boost")]
        [SerializeField] Image boostActiveNotch;
        [SerializeField] TMP_Text boostValueText;
        [SerializeField] Image boostFrameImage;
        [SerializeField] Image boostGlowImage;
        [SerializeField] CanvasGroup boostGlow;

        [Header("Navigation Marker")]
        [SerializeField] RectTransform navigationArrow;
        [SerializeField] Image navigationArrowImage;
        [SerializeField] Image navigationArrowGlowImage;
        [SerializeField] CanvasGroup navigationArrowGroup;
        [SerializeField] CanvasGroup navigationArrowGlow;

        [Header("Control Vector")]
        [SerializeField] RectTransform thrustDot;
        [SerializeField] RectTransform throttleNeedle;
        [Min(1f)]
        [SerializeField] float thrustTravel = 32f;
        [Min(1f)]
        [SerializeField] float throttleTravel = 38f;

        [Header("Landing Vector")]
        [SerializeField] CanvasGroup landingGroup;
        [SerializeField] RectTransform landingDot;
        [Min(1f)]
        [SerializeField] float landingTravel = 28f;
        [Min(0.1f)]
        [SerializeField] float landingSpeedRange = 20f;

        UiTheme theme;
        Vector2 targetThrust;
        float targetThrottle;
        float targetBoostCharge = 1f;
        float displayedBoostCharge = 1f;
        float targetBoostBlend;
        bool boostActive;
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
            ApplySignalColors(0f);

            if (boostValueText != null && theme != null)
            {
                boostValueText.font = theme.InstrumentFont;
                boostValueText.color = theme.SupportingText;
            }

            if (theme == null)
            {
                return;
            }

            SetColor(boostFrameImage, theme.Focus, 0.9f);
            SetColor(boostGlowImage, theme.Focus, 0.28f);
            SetColor(navigationArrowImage, theme.Focus, 0.94f);
            SetColor(navigationArrowGlowImage, theme.Focus, 0.32f);
        }

        public void RefreshTelemetry(SpacecraftMovementTelemetry telemetry)
        {
            targetBoostCharge = telemetry.BoostCharge;
            targetBoostBlend = telemetry.BoostBlend;
            boostActive = telemetry.BoostActive;
            targetThrust = new Vector2(
                telemetry.Command.Translation.x,
                telemetry.Command.Translation.y) * thrustTravel;
            targetThrottle = telemetry.Command.Translation.z * throttleTravel;

            if (boostActiveNotch != null)
            {
                boostActiveNotch.enabled = telemetry.BoostActive;
            }

            if (boostValueText != null)
            {
                boostValueText.SetText("{0:0}%", telemetry.BoostCharge * 100f);
            }

            ApplySignalColors(telemetry.BoostBlend);
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

            if (boostFrameImage != null)
            {
                RectTransform frameFill = boostFrameImage.rectTransform;
                Vector2 anchorMax = frameFill.anchorMax;
                anchorMax.x = displayedBoostCharge;
                frameFill.anchorMax = anchorMax;
            }

            if (boostGlow != null)
            {
                float glowTarget = 0.12f + targetBoostBlend * 0.28f;
                if (boostActive)
                {
                    glowTarget += 0.06f;
                }

                boostGlow.alpha = Mathf.Lerp(
                    boostGlow.alpha,
                    glowTarget,
                    blend);
            }

            MoveTowards(thrustDot, targetThrust, blend);
            MoveTowards(
                throttleNeedle,
                new Vector2(0f, targetThrottle),
                blend);
            MoveTowards(landingDot, targetLanding, blend);
        }

        void OnValidate()
        {
            thrustTravel = Mathf.Max(1f, thrustTravel);
            throttleTravel = Mathf.Max(1f, throttleTravel);
            landingTravel = Mathf.Max(1f, landingTravel);
            landingSpeedRange = Mathf.Max(0.1f, landingSpeedRange);
        }

        void ApplySignalColors(float boostBlend)
        {
            if (theme == null)
            {
                return;
            }

            if (boostFrameImage != null)
            {
                Color color = Color.Lerp(
                    theme.SupportingText,
                    theme.Focus,
                    Mathf.Clamp01(boostBlend));
                color.a = 0.9f;
                boostFrameImage.color = color;
            }

            if (boostActiveNotch != null)
            {
                boostActiveNotch.color = theme.Focus;
            }
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
