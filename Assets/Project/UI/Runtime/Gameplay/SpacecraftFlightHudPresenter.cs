using System.Globalization;
using System.Text;
using Farion.Gameplay.Actors;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Navigation;
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

        [Header("Design")]
        [SerializeField] UiTheme theme;

        [Header("Sources")]
        [SerializeField] PlayerPossessionController possessionController;
        [SerializeField] FlightNavigationTarget navigationTarget;
        [SerializeField] Camera worldCamera;

        [Header("Views")]
        [SerializeField] TMP_Text flightText;
        [SerializeField] TMP_Text navigationText;
        [SerializeField] TMP_Text advisoryText;
        [SerializeField] RectTransform navigationMarker;
        [SerializeField] TMP_Text navigationMarkerText;
        [Min(0f)]
        [SerializeField] float navigationMarkerEdgePadding = 48f;

        [Header("Refresh")]
        [Min(1f)]
        [SerializeField] float refreshRate = 20f;

        readonly StringBuilder flightBuilder = new(128);
        readonly StringBuilder navigationBuilder = new(128);
        readonly StringBuilder markerBuilder = new(64);
        SpacecraftMotor motor;
        CelestialActorProbe celestialProbe;
        SpacecraftLandingComputer landingComputer;
        SpacecraftLandingGuidanceComputer guidanceComputer;
        SpacecraftLandingGearAnimator landingGear;
        NavigationMarkerDirection markerDirection;
        float nextRefreshTime;
        bool visible;

        void Awake()
        {
            ResolveTheme();
            ResolveSources();
            ApplyTheme();
            SetVisible(false);
        }

        void OnEnable()
        {
            ResolveSources();
            if (possessionController != null)
            {
                possessionController.ModeChanged -= HandlePossessionModeChanged;
                possessionController.ModeChanged += HandlePossessionModeChanged;
            }

            RefreshVisibility();
        }

        void OnDisable()
        {
            if (possessionController != null)
            {
                possessionController.ModeChanged -= HandlePossessionModeChanged;
            }

            SetVisible(false);
        }

        void OnValidate()
        {
            refreshRate = Mathf.Max(1f, refreshRate);
            navigationMarkerEdgePadding =
                Mathf.Max(0f, navigationMarkerEdgePadding);
            ResolveTheme();
            ApplyTheme();
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
            SpacecraftMotor nextMotor = possessionController != null
                ? possessionController.SpacecraftMotor
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
            flightBuilder.Clear();
            flightBuilder
                .Append(telemetry.FlightAssistEnabled ? "FLIGHT ASSIST  ON" : "FLIGHT ASSIST  OFF")
                .AppendLine()
                .Append("THR  ")
                .AppendSignedPercent(telemetry.Command.Translation.z)
                .AppendLine()
                .Append("VEL  ")
                .Append(telemetry.RelativeSpeed.ToString("0.0", InvariantCulture))
                .Append(" m/s")
                .AppendLine()
                .Append("BST  ")
                .Append(Mathf.RoundToInt(telemetry.BoostCharge * 100f))
                .Append('%');
            SetText(flightText, flightBuilder);

            navigationBuilder.Clear();
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
                    .Append("ALT  ")
                    .Append(frame.SurfaceAltitude.ToString("0.0", InvariantCulture))
                    .Append(" m")
                    .AppendLine()
                    .Append("V/S  ")
                    .Append(frame.SurfaceNormalVelocity.ToString("+0.0;-0.0;0.0", InvariantCulture))
                    .Append(" m/s")
                    .AppendLine()
                    .Append("LAT  ")
                    .Append(frame.SurfaceTangentialSpeed.ToString("0.0", InvariantCulture))
                    .Append(" m/s");
            }
            else
            {
                if (navigationTarget == null)
                {
                    navigationBuilder.Append("NAV  CRUISE");
                }
            }

            navigationBuilder
                .AppendLine()
                .Append("GEAR  ")
                .Append(ResolveGearLabel());
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
                .Append("TGT  ")
                .Append(navigationTarget.DisplayName)
                .Append("  ");
            AppendDistance(navigationBuilder, distance);
            navigationBuilder
                .AppendLine()
                .Append("CLS  ")
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
            markerDirection = ResolveMarkerDirection(direction, onScreen);
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
                .Append(ResolveMarkerPrefix(markerDirection))
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
            SetActive(flightText, visible);
            SetActive(navigationText, visible);
            SetActive(advisoryText, visible);
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
                possessionController != null &&
                possessionController.IsPilotingSpacecraft &&
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
            Color nominal = ResolveNominalColor();
            if (flightText != null)
            {
                flightText.color = nominal;
            }

            if (navigationText != null)
            {
                navigationText.color = nominal;
            }

            if (navigationMarkerText != null)
            {
                navigationMarkerText.color = nominal;
            }
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

        static NavigationMarkerDirection ResolveMarkerDirection(
            Vector2 direction,
            bool onScreen)
        {
            if (onScreen)
            {
                return NavigationMarkerDirection.OnScreen;
            }

            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            {
                return direction.x >= 0f
                    ? NavigationMarkerDirection.Right
                    : NavigationMarkerDirection.Left;
            }

            return direction.y >= 0f
                ? NavigationMarkerDirection.Up
                : NavigationMarkerDirection.Down;
        }

        static string ResolveMarkerPrefix(
            NavigationMarkerDirection direction)
        {
            return direction switch
            {
                NavigationMarkerDirection.Left => "< ",
                NavigationMarkerDirection.Right => "> ",
                NavigationMarkerDirection.Up => "^ ",
                NavigationMarkerDirection.Down => "v ",
                _ => string.Empty
            };
        }

        enum NavigationMarkerDirection
        {
            OnScreen = 0,
            Left = 1,
            Right = 2,
            Up = 3,
            Down = 4
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
