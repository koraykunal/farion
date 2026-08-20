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
    public sealed class UiSpacecraftFlightHudPresenter : MonoBehaviour
    {
        static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
        const float VelocityVectorProjectionDistance = 1000f;

        [Header("Design")]
        [SerializeField] UiTheme theme;

        [Header("Sources")]
        [SerializeField] MonoBehaviour pilotContextSource;
        [SerializeField] FlightNavigationTarget navigationTarget;
        [SerializeField] Camera worldCamera;

        [Header("Views")]
        [SerializeField] TMP_Text navigationText;
        [SerializeField] TMP_Text phaseText;
        [SerializeField] TMP_Text advisoryText;
        [SerializeField] RectTransform navigationMarker;
        [SerializeField] TMP_Text navigationMarkerText;
        [SerializeField] RectTransform velocityVectorMarker;
        [SerializeField] UiSpacecraftFlightHudGraphics graphics;
        [Min(0f)]
        [SerializeField] float navigationMarkerEdgePadding = 48f;
        [Min(0f)]
        [SerializeField] float velocityVectorMinimumSpeed = 2f;

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
        SpacecraftOrbitComputer orbitComputer;
        ILocalPilotContext pilotContext;
        float nextRefreshTime;
        bool visible;

        UiTheme Theme => theme = UiTheme.Resolve(theme);

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
            velocityVectorMinimumSpeed = Mathf.Max(0f, velocityVectorMinimumSpeed);
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
            RefreshVelocityVector();
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
            orbitComputer = motor != null ? motor.GetComponent<SpacecraftOrbitComputer>() : null;
        }

        void RefreshText()
        {
            SpacecraftMovementTelemetry telemetry = motor.Telemetry;
            SpacecraftFlightProfile profile = motor.FlightProfile;
            graphics?.RefreshTelemetry(
                telemetry,
                motor.CurrentLocalTranslationInput.z,
                profile != null ? profile.MaxForwardSpeed : 180f,
                profile != null ? profile.MaxReverseSpeed : 70f);

            if (phaseText != null)
            {
                phaseText.text = ResolvePhaseLabel();
                phaseText.color = ResolvePhaseColor();
            }

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
                    .Append("ALTITUDE    ");
                AppendDistance(navigationBuilder, frame.SurfaceAltitude);
                navigationBuilder
                    .Append("  V/S ")
                    .Append(frame.SurfaceNormalVelocity.ToString("+0.0;-0.0;0.0", InvariantCulture))
                    .Append(" m/s")
                    .AppendLine()
                    .Append("LATERAL     ")
                    .Append(frame.SurfaceTangentialSpeed.ToString("0.0", InvariantCulture))
                    .Append(" m/s  GEAR ")
                    .Append(ResolveGearLabel())
                    .AppendLine();
                graphics?.RefreshLanding(
                    frame.SurfaceNormalVelocity,
                    frame.SurfaceTangentialSpeed,
                    landingAssessment.VerticalSpeedLimit,
                    landingAssessment.TangentialSpeedLimit,
                    hasFrame: true);
            }
            else
            {
                graphics?.RefreshLanding(0f, 0f, 0f, 0f, hasFrame: false);
                navigationBuilder
                    .Append("GEAR        ")
                    .Append(ResolveGearLabel())
                    .AppendLine();
            }

            AppendOrbit();
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

        void AppendOrbit()
        {
            SpacecraftOrbitSample orbit = orbitComputer != null
                ? orbitComputer.CurrentOrbit
                : SpacecraftOrbitSample.NoFrame;
            if (!orbit.HasFrame ||
                orbit.Regime == SpacecraftOrbitRegime.SurfaceProximity)
            {
                return;
            }

            navigationBuilder.Append("APOAPSIS    ");
            if (orbit.IsBound)
            {
                AppendDistance(navigationBuilder, orbit.ApoapsisAltitude);
            }
            else
            {
                navigationBuilder.Append("ESCAPE");
            }

            navigationBuilder
                .Append("  PE ");
            AppendDistance(navigationBuilder, orbit.PeriapsisAltitude);
            navigationBuilder
                .AppendLine()
                .Append("ECC         ")
                .Append(orbit.Eccentricity.ToString("0.000", InvariantCulture));
            if (orbit.IsBound && orbit.OrbitalPeriod > 0f)
            {
                navigationBuilder.Append("  PERIOD ");
                AppendDuration(navigationBuilder, orbit.OrbitalPeriod);
            }
        }

        static void AppendDuration(StringBuilder builder, float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            int hours = total / 3600;
            int minutes = total % 3600 / 60;
            if (hours > 0)
            {
                builder
                    .Append(hours.ToString(InvariantCulture))
                    .Append('h')
                    .Append(minutes.ToString("00", InvariantCulture));
                return;
            }

            builder
                .Append(minutes.ToString(InvariantCulture))
                .Append(':')
                .Append((total % 60).ToString("00", InvariantCulture));
        }

        Color ResolvePhaseColor()
        {
            SpacecraftApproachPhase phase = landingComputer != null
                ? landingComputer.Phase
                : SpacecraftApproachPhase.NoFrame;
            return phase switch
            {
                SpacecraftApproachPhase.UnsafeTouchdown => ResolveCriticalColor(),
                SpacecraftApproachPhase.Submerged => ResolveCriticalColor(),
                SpacecraftApproachPhase.TouchdownWindow => ResolveNominalColor(),
                SpacecraftApproachPhase.LowApproach => ResolveCautionColor(),
                SpacecraftApproachPhase.HighDescent => ResolveCautionColor(),
                SpacecraftApproachPhase.AtmosphericDescent => ResolveCautionColor(),
                _ => Theme.SupportingText
            };
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

        void RefreshVelocityVector()
        {
            bool shouldShow =
                visible &&
                motor != null &&
                worldCamera != null &&
                velocityVectorMarker != null &&
                velocityVectorMarker.parent is RectTransform;
            if (!shouldShow)
            {
                SetVelocityVectorActive(false);
                graphics?.RefreshVelocityVector(false);
                return;
            }

            Vector3 velocity = motor.Telemetry.WorldRelativeVelocity;
            if (velocity.magnitude < velocityVectorMinimumSpeed)
            {
                SetVelocityVectorActive(false);
                graphics?.RefreshVelocityVector(false);
                return;
            }

            Vector3 projected = worldCamera.transform.position +
                velocity.normalized * VelocityVectorProjectionDistance;
            Vector3 viewport = worldCamera.WorldToViewportPoint(projected);
            bool onScreen =
                viewport.z > 0f &&
                viewport.x > 0.03f && viewport.x < 0.97f &&
                viewport.y > 0.03f && viewport.y < 0.97f;

            SetVelocityVectorActive(true);
            if (onScreen)
            {
                RectTransform bounds = (RectTransform)velocityVectorMarker.parent;
                velocityVectorMarker.anchoredPosition = new Vector2(
                    (viewport.x - 0.5f) * bounds.rect.width,
                    (viewport.y - 0.5f) * bounds.rect.height);
            }

            graphics?.RefreshVelocityVector(onScreen);
        }

        void SetVelocityVectorActive(bool active)
        {
            if (velocityVectorMarker != null &&
                velocityVectorMarker.gameObject.activeSelf != active)
            {
                velocityVectorMarker.gameObject.SetActive(active);
            }
        }

        string ResolvePhaseLabel()
        {
            SpacecraftApproachPhase phase = landingComputer != null
                ? landingComputer.Phase
                : SpacecraftApproachPhase.NoFrame;
            return phase switch
            {
                SpacecraftApproachPhase.BodyProximity => "PROXIMITY",
                SpacecraftApproachPhase.Orbit => "ORBIT",
                SpacecraftApproachPhase.Deorbiting => "DEORBIT",
                SpacecraftApproachPhase.AtmosphericFlight => "ATMOSPHERIC",
                SpacecraftApproachPhase.AtmosphericDescent => "ATMO DESCENT",
                SpacecraftApproachPhase.HighDescent => "HIGH DESCENT",
                SpacecraftApproachPhase.LowApproach => "LOW APPROACH",
                SpacecraftApproachPhase.TouchdownWindow => "TOUCHDOWN WINDOW",
                SpacecraftApproachPhase.UnsafeTouchdown => "UNSAFE APPROACH",
                SpacecraftApproachPhase.Submerged => "SUBMERGED",
                _ => "DEEP SPACE"
            };
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
            SetActive(phaseText, visible);
            SetActive(advisoryText, visible);
            graphics?.SetVisible(visible);
            SetMarkerActive(visible && navigationTarget != null);
            if (!visible)
            {
                SetVelocityVectorActive(false);
            }
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
            theme = UiTheme.Resolve(root != null ? root.Theme : null);
        }

        void ApplyTheme()
        {
            if (navigationText != null)
            {
                navigationText.color = Theme.PrimaryText;
            }

            if (navigationMarkerText != null)
            {
                navigationMarkerText.color = Theme.Focus;
            }

            graphics?.ApplyTheme(Theme);

            if (Application.isPlaying)
            {
                ApplyTypography();
            }
        }

        void ApplyTypography()
        {

            ApplyFont(navigationText, theme.InstrumentFont, 2f, 2f);
            ApplyFont(phaseText, theme.InstrumentFont, 2f, 0f);
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
            return Theme.Nominal;
        }

        Color ResolveCautionColor()
        {
            return Theme.Caution;
        }

        Color ResolveCriticalColor()
        {
            return Theme.Critical;
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
