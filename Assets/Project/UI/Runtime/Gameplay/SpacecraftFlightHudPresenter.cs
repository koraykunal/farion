using System.Globalization;
using System.Text;
using Farion.Gameplay.Actors;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Interaction;
using TMPro;
using UnityEngine;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class SpacecraftFlightHudPresenter : MonoBehaviour
    {
        static readonly Color NominalColor = new(0.52f, 0.94f, 0.9f, 0.96f);
        static readonly Color CautionColor = new(1f, 0.72f, 0.24f, 0.98f);
        static readonly Color CriticalColor = new(1f, 0.26f, 0.2f, 1f);
        static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        [Header("Sources")]
        [SerializeField] PlayerPossessionController possessionController;

        [Header("Views")]
        [SerializeField] TMP_Text flightText;
        [SerializeField] TMP_Text navigationText;
        [SerializeField] TMP_Text advisoryText;

        [Header("Refresh")]
        [Min(1f)]
        [SerializeField] float refreshRate = 20f;

        readonly StringBuilder flightBuilder = new(128);
        readonly StringBuilder navigationBuilder = new(128);
        SpacecraftMotor motor;
        CelestialActorProbe celestialProbe;
        SpacecraftLandingComputer landingComputer;
        SpacecraftLandingGuidanceComputer guidanceComputer;
        SpacecraftLandingGearAnimator landingGear;
        float nextRefreshTime;
        bool visible;

        void Awake()
        {
            ResolveSources();
            SetVisible(false);
        }

        void OnValidate()
        {
            refreshRate = Mathf.Max(1f, refreshRate);
        }

        void Update()
        {
            ResolveSources();
            bool shouldShow = possessionController != null &&
                possessionController.IsPilotingSpacecraft &&
                motor != null;
            SetVisible(shouldShow);
            if (!shouldShow || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 1f / refreshRate;
            RefreshText();
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
            if (celestialProbe != null && celestialProbe.HasSample)
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
                navigationBuilder.Append("NO SURFACE FRAME");
            }

            navigationBuilder
                .AppendLine()
                .Append("GEAR  ")
                .Append(ResolveGearLabel());
            SetText(navigationText, navigationBuilder);

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
                SpacecraftLandingGuidanceLevel.Critical => CriticalColor,
                SpacecraftLandingGuidanceLevel.Warning => CautionColor,
                SpacecraftLandingGuidanceLevel.Caution => CautionColor,
                _ => NominalColor
            };
        }

        void SetVisible(bool shouldShow)
        {
            if (visible == shouldShow)
            {
                return;
            }

            visible = shouldShow;
            SetActive(flightText, visible);
            SetActive(navigationText, visible);
            SetActive(advisoryText, visible);
            if (visible)
            {
                nextRefreshTime = 0f;
            }
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
