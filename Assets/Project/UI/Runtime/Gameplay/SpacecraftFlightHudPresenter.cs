using System.Globalization;
using System.Text;
using Farion.Gameplay.Actors;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Navigation;
using Farion.Gameplay.Session;
using Farion.UI.Foundation;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;

namespace Farion.UI.Gameplay
{
    [DefaultExecutionOrder(400)]
    [DisallowMultipleComponent]
    public sealed class SpacecraftFlightHudPresenter : MonoBehaviour
    {
        static readonly Color FallbackNominalColor = new(0.52f, 0.94f, 0.9f, 0.96f);
        static readonly Color FallbackCautionColor = new(1f, 0.72f, 0.24f, 0.98f);
        static readonly Color FallbackCriticalColor = new(1f, 0.26f, 0.2f, 1f);
        static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
        const string SectionLabelOpen = "<size=72%><alpha=#96>";
        const string SectionLabelClose = "</alpha></size>";

        [Header("Design")]
        [SerializeField] UiTheme theme;

        [Header("Sources")]
        [SerializeField] MonoBehaviour pilotContextSource;
        [SerializeField] FlightNavigationTarget navigationTarget;
        [SerializeField] Camera worldCamera;

        [Header("Views")]
        [SerializeField] TMP_Text navigationText;
        [SerializeField] TMP_Text advisoryText;
        [SerializeField] RectTransform navigationMarker;
        [SerializeField] TMP_Text navigationMarkerText;
        [SerializeField] SpacecraftFlightHudGraphics graphics;
        [Min(0f)]
        [SerializeField] float navigationMarkerEdgePadding = 48f;

        [Header("Refresh")]
        [Min(1f)]
        [SerializeField] float refreshRate = 20f;

        readonly StringBuilder navigationBuilder = new(128);
        readonly StringBuilder markerBuilder = new(64);
        SpacecraftMotor motor;
        CelestialActorProbe celestialProbe;
        SpacecraftLandingComputer landingComputer;
        SpacecraftLandingGuidanceComputer guidanceComputer;
        SpacecraftLandingGearAnimator landingGear;
        ILocalPilotContext pilotContext;
        float nextRefreshTime;
        bool visible;

        public void SetPilotContext(ILocalPilotContext context)
        {
            if (pilotContext == context)
            {
                return;
            }

            UnsubscribePilotContext();
            pilotContext = context;
            if (isActiveAndEnabled)
            {
                SubscribePilotContext();
            }

            ResolveSources();
            RefreshVisibility();
        }

        void Awake()
        {
            ResolveTheme();
            ResolvePilotContext();
            ResolveSources();
            ApplyTheme();
            SetVisible(false);
        }

        void OnEnable()
        {
            ResolvePilotContext();
            ResolveSources();
            SubscribePilotContext();
            RefreshVisibility();
        }

        void OnDisable()
        {
            UnsubscribePilotContext();
            SetVisible(false);
        }

        void OnValidate()
        {
            refreshRate = Mathf.Max(1f, refreshRate);
            navigationMarkerEdgePadding =
                Mathf.Max(0f, navigationMarkerEdgePadding);
            if (pilotContextSource != null && pilotContextSource is not ILocalPilotContext)
            {
                pilotContextSource = null;
            }

            ResolveTheme();
            ApplyTheme();
        }

        void ResolvePilotContext()
        {
            pilotContext ??= pilotContextSource as ILocalPilotContext;
        }

        void SubscribePilotContext()
        {
            if (pilotContext != null)
            {
                pilotContext.ModeChanged -= HandlePossessionModeChanged;
                pilotContext.ModeChanged += HandlePossessionModeChanged;
            }
        }

        void UnsubscribePilotContext()
        {
            if (pilotContext != null)
            {
                pilotContext.ModeChanged -= HandlePossessionModeChanged;
            }
        }

        void Update()
        {
            ResolveSources();
            RefreshVisibility();
            if (!visible || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 1f / refreshRate;
            RefreshText();
        }

        void LateUpdate()
        {
            RefreshNavigationMarker();
        }

        void ResolveSources()
        {
            SpacecraftMotor nextMotor = pilotContext != null
                ? pilotContext.PilotedSpacecraftMotor
                : null;
            if (nextMotor == motor)
            {
                return;
            }

            motor = nextMotor;
            celestialProbe = motor != null ? motor.GetComponent<CelestialActorProbe>() : null;
            landingComputer = motor != null ? motor.GetComponent<SpacecraftLandingComputer>() : null;
            guidanceComputer = motor != null ? motor.GetComponent<SpacecraftLandingGuidanceComputer>() : null;
            landingGear = motor != null ? motor.GetComponent<SpacecraftLandingGearAnimator>() : null;
        }

        void RefreshText()
        {
            SpacecraftMovementTelemetry telemetry = motor.Telemetry;
            float speedScale = motor.FlightProfile != null
                ? motor.FlightProfile.MaxBoostForwardSpeed
                : 260f;
            graphics?.RefreshTelemetry(telemetry, speedScale);

            navigationBuilder.Clear();
            navigationBuilder
                .Append(SectionLabelOpen)
                .Append("NAVIGATION / LANDING")
                .Append(SectionLabelClose)
                .AppendLine();
            float targetDistance = AppendNavigationTarget(telemetry);
            SpacecraftLandingAssessment landingAssessment =
                landingComputer != null
                    ? landingComputer.CurrentAssessment
                    : default;
            if (celestialProbe != null &&
                celestialProbe.HasSample &&
                landingAssessment.HasFrame)
            {
                var frame = celestialProbe.CurrentSample;
                navigationBuilder
                    .Append("ALTITUDE    ")
                    .Append(frame.SurfaceAltitude.ToString("0.0", InvariantCulture))
                    .Append(" m  V/S ")
                    .Append(frame.SurfaceNormalVelocity.ToString("+0.0;-0.0;0.0", InvariantCulture))
                    .Append(" m/s")
                    .AppendLine()
                    .Append("LATERAL     ")
                    .Append(frame.SurfaceTangentialSpeed.ToString("0.0", InvariantCulture))
                    .Append(" m/s  GEAR ")
                    .Append(ResolveGearLabel());
                graphics?.RefreshLanding(
                    frame.SurfaceNormalVelocity,
                    frame.SurfaceTangentialSpeed,
                    hasFrame: true);
            }
            else
            {
                graphics?.RefreshLanding(0f, 0f, hasFrame: false);
                if (navigationTarget == null)
                {
                    navigationBuilder.Append("MODE        CRUISE");
                }

                navigationBuilder
                    .AppendLine()
                    .Append("GEAR        ")
                    .Append(ResolveGearLabel());
            }
            SetText(navigationText, navigationBuilder);
            RefreshNavigationMarkerText(targetDistance);

            SpacecraftLandingGuidanceSample guidance = guidanceComputer != null
                ? guidanceComputer.CurrentGuidance
                : SpacecraftLandingGuidanceSample.Offline;
            string advisory = guidance.Command != SpacecraftLandingGuidanceCommand.None
                ? guidance.Advisory
                : landingComputer != null && landingComputer.TouchdownConfirmed
                    ? "TOUCHDOWN"
                    : string.Empty;
            if (advisoryText != null)
            {
                advisoryText.text = advisory;
                advisoryText.color = ResolveAdvisoryColor();
            }
        }

        float AppendNavigationTarget(SpacecraftMovementTelemetry telemetry)
        {
            if (navigationTarget == null || motor == null)
            {
                return -1f;
            }

            Vector3 targetOffset =
                navigationTarget.Position - motor.transform.position;
            float distance = targetOffset.magnitude;
            float closingSpeed = distance > 0.001f
                ? Vector3.Dot(
                    telemetry.WorldRelativeVelocity,
                    targetOffset / distance)
                : 0f;

            navigationBuilder
                .Append("TARGET      ")
                .Append(navigationTarget.DisplayName)
                .AppendLine()
                .Append("RANGE       ");
            AppendDistance(navigationBuilder, distance);
            navigationBuilder
                .Append("  RATE ")
                .Append(closingSpeed.ToString(
                    "+0.0;-0.0;0.0",
                    InvariantCulture))
                .Append(" m/s")
                .AppendLine();
            return distance;
        }

        void RefreshNavigationMarker()
        {
            bool shouldShow =
                visible &&
                navigationTarget != null &&
                worldCamera != null &&
                navigationMarker != null &&
                navigationMarker.parent is RectTransform;
            SetMarkerActive(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            RectTransform bounds = (RectTransform)navigationMarker.parent;
            Vector3 viewport =
                worldCamera.WorldToViewportPoint(navigationTarget.Position);
            bool behindCamera = viewport.z <= 0f;
            Vector2 direction = new(
                viewport.x - 0.5f,
                viewport.y - 0.5f);
            if (behindCamera)
            {
                direction = -direction;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.up;
            }

            Vector2 halfSize = bounds.rect.size * 0.5f;
            Vector2 markerHalfSize = navigationMarker.rect.size * 0.5f;
            Vector2 limits = new(
                Mathf.Max(
                    0f,
                    halfSize.x - markerHalfSize.x -
                    navigationMarkerEdgePadding),
                Mathf.Max(
                    0f,
                    halfSize.y - markerHalfSize.y -
                    navigationMarkerEdgePadding));
            bool onScreen =
                !behindCamera &&
                viewport.x >= 0f &&
                viewport.x <= 1f &&
                viewport.y >= 0f &&
                viewport.y <= 1f;

            Vector2 position = new(
                direction.x * bounds.rect.width,
                direction.y * bounds.rect.height);
            if (onScreen)
            {
                position.x = Mathf.Clamp(position.x, -limits.x, limits.x);
                position.y = Mathf.Clamp(position.y, -limits.y, limits.y);
            }
            else
            {
                float horizontalScale = Mathf.Abs(position.x) > 0.001f
                    ? limits.x / Mathf.Abs(position.x)
                    : float.PositiveInfinity;
                float verticalScale = Mathf.Abs(position.y) > 0.001f
                    ? limits.y / Mathf.Abs(position.y)
                    : float.PositiveInfinity;
                position *= Mathf.Min(horizontalScale, verticalScale);
            }

            navigationMarker.anchoredPosition = position;
            graphics?.RefreshNavigationMarker(direction, onScreen);
        }

        void RefreshNavigationMarkerText(float distance)
        {
            if (navigationMarkerText == null ||
                navigationTarget == null ||
                distance < 0f)
            {
                return;
            }

            markerBuilder.Clear();
            markerBuilder
                .Append(navigationTarget.DisplayName)
                .AppendLine();
            if (distance <= navigationTarget.ArrivalRadius)
            {
                markerBuilder.Append("ARRIVED");
            }
            else
            {
                AppendDistance(markerBuilder, distance);
            }

            navigationMarkerText.SetText(markerBuilder);
        }

        string ResolveGearLabel()
        {
            if (landingGear == null)
            {
                return "N/A";
            }

            if (landingGear.IsTransitioning)
            {
                return landingGear.IsCommandedDeployed ? "DEPLOYING" : "RETRACTING";
            }

            return landingGear.IsDeployed ? "DOWN" : "UP";
        }

        Color ResolveAdvisoryColor()
        {
            SpacecraftLandingGuidanceLevel level = guidanceComputer != null
                ? guidanceComputer.CurrentGuidance.Level
                : SpacecraftLandingGuidanceLevel.Offline;
            return level switch
            {
                SpacecraftLandingGuidanceLevel.Critical => ResolveCriticalColor(),
                SpacecraftLandingGuidanceLevel.Warning => ResolveCautionColor(),
                SpacecraftLandingGuidanceLevel.Caution => ResolveCautionColor(),
                _ => ResolveNominalColor()
            };
        }

        void SetVisible(bool shouldShow)
        {
            bool changed = visible != shouldShow;
            visible = shouldShow;
            SetActive(navigationText, visible);
            SetActive(advisoryText, visible);
            graphics?.SetVisible(visible);
            SetMarkerActive(visible && navigationTarget != null);
            if (changed && visible)
            {
                nextRefreshTime = 0f;
            }
        }

        void HandlePossessionModeChanged(PlayerPossessionMode _)
        {
            RefreshVisibility();
        }

        void RefreshVisibility()
        {
            SetVisible(
                pilotContext != null &&
                pilotContext.CurrentMode == PlayerPossessionMode.Spacecraft &&
                motor != null);
        }

        static void SetText(TMP_Text target, StringBuilder builder)
        {
            if (target != null)
            {
                target.SetText(builder);
            }
        }

        static void SetActive(TMP_Text target, bool active)
        {
            if (target != null && target.gameObject.activeSelf != active)
            {
                target.gameObject.SetActive(active);
            }
        }

        void ResolveTheme()
        {
            if (theme != null)
            {
                return;
            }

            UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
            theme = root != null ? root.Theme : null;
        }

        void ApplyTheme()
        {
            if (navigationText != null)
            {
                navigationText.color = theme != null
                    ? theme.PrimaryText
                    : ResolveNominalColor();
            }

            if (navigationMarkerText != null)
            {
                navigationMarkerText.color = theme != null
                    ? theme.Focus
                    : ResolveNominalColor();
            }

            graphics?.ApplyTheme(theme);

            if (Application.isPlaying)
            {
                ApplyTypography();
            }
        }

        void ApplyTypography()
        {
            if (theme == null)
            {
                return;
            }

            ApplyFont(navigationText, theme.InstrumentFont, 2f, 2f);
            ApplyFont(navigationMarkerText, theme.InstrumentFont, 2f, 0f);
            ApplyFont(advisoryText, theme.InterfaceMediumFont, 5f, 0f);
        }

        static void ApplyFont(
            TMP_Text target,
            TMP_FontAsset font,
            float characterSpacing,
            float lineSpacing)
        {
            if (target == null || font == null)
            {
                return;
            }

            target.font = font;
            target.characterSpacing = characterSpacing;
            target.lineSpacing = lineSpacing;
            target.outlineColor = new Color32(2, 6, 9, 220);
            target.outlineWidth = 0.08f;
        }

        Color ResolveNominalColor()
        {
            return theme != null ? theme.Nominal : FallbackNominalColor;
        }

        Color ResolveCautionColor()
        {
            return theme != null ? theme.Caution : FallbackCautionColor;
        }

        Color ResolveCriticalColor()
        {
            return theme != null ? theme.Critical : FallbackCriticalColor;
        }

        void SetMarkerActive(bool active)
        {
            if (navigationMarker != null &&
                navigationMarker.gameObject.activeSelf != active)
            {
                navigationMarker.gameObject.SetActive(active);
            }
        }

        static void AppendDistance(StringBuilder builder, float distance)
        {
            if (distance >= 1000f)
            {
                builder
                    .Append((distance / 1000f).ToString(
                        "0.0",
                        InvariantCulture))
                    .Append(" km");
                return;
            }

            builder
                .Append(distance.ToString("0", InvariantCulture))
                .Append(" m");
        }

    }

    static class SpacecraftFlightHudStringBuilderExtensions
    {
        public static StringBuilder AppendSignedPercent(this StringBuilder builder, float value)
        {
            int percent = Mathf.RoundToInt(Mathf.Clamp(value, -1f, 1f) * 100f);
            return builder.Append(percent.ToString("+0;-0;0", CultureInfo.InvariantCulture)).Append('%');
        }
    }
}
