using Farion.Gameplay.Actors;
using Farion.Simulation.Celestial;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Farion.Gameplay.Flight
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpacecraftLandingGuidanceComputer))]
    public sealed class SpacecraftLandingDebugHud : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] bool showHud;
        [SerializeField] bool showOnlyInPlayMode = true;
        [SerializeField] Key inputSystemToggleKey = Key.F9;
        [SerializeField] Vector2 panelPosition = new(24f, 24f);
        [SerializeField, Min(240f)] float panelWidth = 360f;
        [SerializeField, Min(160f)] float panelHeight = 680f;

        [Header("Source")]
        [SerializeField] SpacecraftLandingGuidanceComputer guidanceComputer;
        [SerializeField] SpacecraftLandingComputer landingComputer;
        [SerializeField] SpacecraftOrbitComputer orbitComputer;
        [SerializeField] SpacecraftEntryCorridorComputer entryCorridorComputer;
        [SerializeField] CelestialActorProbe celestialProbe;
        [SerializeField] SpacecraftSurfaceContactProbe surfaceContactProbe;
        [SerializeField] SpacecraftOceanInteractor oceanInteractor;

        GUIStyle titleStyle;
        GUIStyle labelStyle;
        GUIStyle valueStyle;

        void Awake()
        {
            ResolveComponents();
        }

        void OnValidate()
        {
            ResolveComponents();
            panelWidth = Mathf.Max(240f, panelWidth);
            panelHeight = Mathf.Max(160f, panelHeight);
        }

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && WasPressedThisFrame(keyboard, inputSystemToggleKey))
            {
                showHud = !showHud;
            }
#endif
        }

        void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!showHud || (showOnlyInPlayMode && !Application.isPlaying))
            {
                return;
            }

            ResolveComponents();
            EnsureStyles();

            Rect rect = new(panelPosition.x, panelPosition.y, panelWidth, panelHeight);
            GUILayout.BeginArea(rect, GUI.skin.box);
            DrawHud();
            GUILayout.EndArea();
#endif
        }

        void ResolveComponents()
        {
            if (guidanceComputer == null)
            {
                guidanceComputer = GetComponent<SpacecraftLandingGuidanceComputer>();
            }

            if (landingComputer == null)
            {
                landingComputer = GetComponent<SpacecraftLandingComputer>();
            }

            if (orbitComputer == null)
            {
                orbitComputer = GetComponent<SpacecraftOrbitComputer>();
            }

            if (entryCorridorComputer == null)
            {
                entryCorridorComputer = GetComponent<SpacecraftEntryCorridorComputer>();
            }

            if (celestialProbe == null)
            {
                celestialProbe = GetComponent<CelestialActorProbe>();
            }

            if (surfaceContactProbe == null)
            {
                surfaceContactProbe = GetComponent<SpacecraftSurfaceContactProbe>();
            }

            if (oceanInteractor == null)
            {
                oceanInteractor = GetComponent<SpacecraftOceanInteractor>();
            }
        }

        void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Normal
            };
            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
        }

        void DrawHud()
        {
            SpacecraftLandingGuidanceSample guidance = guidanceComputer != null
                ? guidanceComputer.CurrentGuidance
                : SpacecraftLandingGuidanceSample.Offline;
            SpacecraftLandingAssessment assessment = landingComputer != null
                ? landingComputer.CurrentAssessment
                : default;
            SpacecraftOrbitSample orbit = orbitComputer != null
                ? orbitComputer.CurrentOrbit
                : SpacecraftOrbitSample.NoFrame;
            SpacecraftEntryCorridorSample entry = entryCorridorComputer != null
                ? entryCorridorComputer.CurrentCorridor
                : SpacecraftEntryCorridorSample.NoFrame;
            CelestialFrameSample frame = celestialProbe != null
                ? celestialProbe.CurrentSample
                : default;
            SpacecraftSurfaceContactSample contact = surfaceContactProbe != null
                ? surfaceContactProbe.CurrentContact
                : SpacecraftSurfaceContactSample.Empty;
            SpacecraftOceanInteractionSample ocean = oceanInteractor != null
                ? oceanInteractor.CurrentInteraction
                : SpacecraftOceanInteractionSample.Empty(frame);

            Color previousColor = GUI.contentColor;
            GUI.contentColor = ColorForLevel(guidance.Level);
            GUILayout.Label(guidance.Advisory, titleStyle);
            GUI.contentColor = previousColor;

            DrawRow("Level", guidance.Level.ToString());
            DrawRow("Command", guidance.Command.ToString());
            DrawRow("Body", frame.HasBody ? frame.Body.BodyName : "NO FRAME");
            DrawRow("Phase", assessment.HasFrame ? assessment.Phase.ToString() : "NoFrame");
            DrawRow("Altitude", assessment.HasFrame ? FormatMeters(frame.SurfaceAltitude) : "--");
            DrawRow("Surface Slope", assessment.HasFrame ? FormatDegrees(frame.SurfaceSlopeAngleDegrees) : "--");
            DrawRow("Ocean Altitude", frame.HasOcean ? FormatMeters(frame.OceanAltitude) : "--");
            DrawRow("Water Depth", frame.IsBelowOceanLevel ? FormatMeters(frame.WaterDepth) : "--");
            DrawRow("Water Contact", ocean.HasOcean ? ocean.IsTouchingWater.ToString() : "--");
            DrawRow("Submerged", ocean.HasOcean ? FormatRatio(ocean.SubmergedFraction) : "--");
            DrawRow("Vertical Speed", assessment.HasFrame ? FormatSpeed(frame.SurfaceNormalVelocity) : "--");
            DrawRow("Lateral Speed", assessment.HasFrame ? FormatSpeed(frame.SurfaceTangentialSpeed) : "--");
            DrawRow("Stress", FormatRatio(guidance.Stress));
            DrawRow("Vertical Limit", assessment.HasFrame ? FormatSpeed(assessment.VerticalSpeedLimit) : "--");
            DrawRow("Lateral Limit", assessment.HasFrame ? FormatSpeed(assessment.TangentialSpeedLimit) : "--");
            DrawRow("Slope Limit", assessment.HasFrame ? FormatDegrees(assessment.SurfaceSlopeLimit) : "--");
            DrawRow("Risks", assessment.HasFrame ? assessment.Risks.ToString() : "NoFrame");
            GUILayout.Space(6f);
            DrawRow("Orbit", orbit.HasFrame ? orbit.Regime.ToString() : "NoFrame");
            DrawRow("Orbit Radial", orbit.HasFrame ? FormatSpeed(orbit.RadialVelocity) : "--");
            DrawRow("Circular Speed", orbit.HasFrame ? FormatSpeed(orbit.CircularVelocity) : "--");
            DrawRow("Escape Speed", orbit.HasFrame ? FormatSpeed(orbit.EscapeVelocity) : "--");
            DrawRow("Periapsis", orbit.HasFrame ? FormatOptionalMeters(orbit.PeriapsisAltitude) : "--");
            DrawRow("Apoapsis", orbit.HasFrame ? FormatOptionalMeters(orbit.ApoapsisAltitude) : "--");
            DrawRow("Flight Path", orbit.HasFrame ? FormatDegrees(orbit.FlightPathAngleDegrees) : "--");
            GUILayout.Space(6f);
            DrawRow("Entry", entry.HasFrame ? entry.State.ToString() : "NoFrame");
            DrawRow("Entry Advisory", entry.HasFrame ? entry.Advisory : "--");
            DrawRow("Entry Angle", entry.HasFrame ? FormatDegrees(entry.EntryAngleDegrees) : "--");
            DrawRow("Entry Risk", FormatRatio(entry.NormalizedRisk));
            GUILayout.Space(6f);
            DrawRow("Buoyancy", ocean.IsTouchingWater ? FormatAccel(ocean.BuoyancyAcceleration.magnitude) : "--");
            DrawRow("Water Drag", ocean.IsTouchingWater ? FormatAccel(ocean.DragAcceleration.magnitude) : "--");
            DrawRow("Water Entry", ocean.IsTouchingWater ? FormatSpeed(ocean.WaterEntrySpeed) : "--");
            DrawRow("Pressure", ocean.HasOcean ? FormatRatio(ocean.PressureStress) : "--");
            GUILayout.Space(6f);
            DrawRow("Contact", contact.HasContact ? contact.Body.BodyName : "None");
            DrawRow("Touchdown", landingComputer != null && landingComputer.TouchdownConfirmed ? "Confirmed" : "Pending");
        }

        void DrawRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(panelWidth * 0.42f));
            GUILayout.Label(value, valueStyle);
            GUILayout.EndHorizontal();
        }

        static Color ColorForLevel(SpacecraftLandingGuidanceLevel guidanceLevel)
        {
            return guidanceLevel switch
            {
                SpacecraftLandingGuidanceLevel.Offline => new Color(0.55f, 0.55f, 0.55f),
                SpacecraftLandingGuidanceLevel.Nominal => new Color(0.72f, 0.95f, 0.78f),
                SpacecraftLandingGuidanceLevel.Advisory => new Color(0.62f, 0.82f, 1f),
                SpacecraftLandingGuidanceLevel.Caution => new Color(1f, 0.82f, 0.34f),
                SpacecraftLandingGuidanceLevel.Warning => new Color(1f, 0.55f, 0.24f),
                SpacecraftLandingGuidanceLevel.Critical => new Color(1f, 0.25f, 0.22f),
                _ => Color.white
            };
        }

        static string FormatMeters(float value)
        {
            return $"{value:0.0} m";
        }

        static string FormatOptionalMeters(float value)
        {
            return float.IsInfinity(value) ? "Infinity" : FormatMeters(value);
        }

        static string FormatSpeed(float value)
        {
            return $"{value:0.00} m/s";
        }

        static string FormatAccel(float value)
        {
            return $"{value:0.00} m/s2";
        }

        static string FormatDegrees(float value)
        {
            return $"{value:0.0} deg";
        }

        static string FormatRatio(float value)
        {
            return $"{Mathf.Clamp01(value) * 100f:0}%";
        }

        static bool WasPressedThisFrame(Keyboard keyboard, Key key)
        {
            KeyControl control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }
    }
}
