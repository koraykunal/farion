using System;
using System.Collections.Generic;
using System.Linq;
using Farion.Editor;
using Farion.App.Flow;
using Farion.Core.Identity;
using Farion.Core.Persistence;
using Farion.Core.Physics;
using Farion.Gameplay.Character;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Input;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Persistence;
using Farion.Gameplay.Presentation.Flight;
using Farion.Gameplay.ResourceNodes;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Session;
using Farion.Multiplayer.Spawning;
using Farion.Multiplayer.World;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using FMODUnity;
using FishNet.Component.Observing;
using FishNet.Component.Transforming.Beta;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Observing;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using Object = UnityEngine.Object;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Editor.Validation
{
    public sealed partial class FarionProjectValidator
    {
        static void ValidateSpacecraft(
            string scenePath,
            SpacecraftMotor motor,
            FarionValidationReport report)
        {
            ValidateRequiredReference(scenePath, motor, "inputSource", report);
            ValidateRequiredReference(scenePath, motor, "flightProfile", report);
            ValidateRequiredReference(scenePath, motor, "celestialProbe", report);
            ValidateRequiredReference(scenePath, motor, "surfaceContactProbe", report);
            ValidateShuttleBinding(scenePath, motor, report);

            if (motor.GetComponent<SpacecraftHull>() == null)
            {
                report.AddWarning(
                    $"{scenePath}: {motor.name} has no {nameof(SpacecraftHull)}, so impacts never damage it.");
            }

            SpacecraftFlightProfile profile = motor.FlightProfile;
            if (profile != null)
            {
                if (profile.MaxBoostForwardSpeed < profile.MaxForwardSpeed)
                {
                    report.AddError(
                        $"{scenePath}: {motor.name} boost speed is lower than normal forward speed.");
                }

                if (profile.VerticalAcceleration <= 0f ||
                    profile.StrafeAcceleration <= 0f ||
                    profile.ForwardAcceleration <= 0f)
                {
                    report.AddError(
                        $"{scenePath}: {motor.name} flight profile has a non-positive thrust axis.");
                }

                if (!profile.CompensateGravityInAssistedMode)
                {
                    report.AddWarning(
                        $"{scenePath}: {motor.name} assisted mode does not compensate gravity.");
                }

                if (!profile.LimitManualFlightEnvelope)
                {
                    report.AddWarning(
                        $"{scenePath}: {motor.name} manual flight has no configured speed envelope.");
                }

                if (profile.FuelCapacity <= 0f)
                {
                    report.AddWarning(
                        $"{scenePath}: {motor.name} flight profile has no fuel capacity, so the drive never runs dry.");
                }
                else if (profile.FuelPerAccelerationUnit <= 0f)
                {
                    report.AddWarning(
                        $"{scenePath}: {motor.name} flight profile carries fuel but never burns it.");
                }

                if (profile.HullIntegrity <= 0f)
                {
                    report.AddWarning(
                        $"{scenePath}: {motor.name} flight profile has no hull integrity, so impacts never damage it.");
                }
                else if (profile.ImpactDamagePerSpeedUnit <= 0f)
                {
                    report.AddWarning(
                        $"{scenePath}: {motor.name} flight profile has a hull but takes no impact damage.");
                }
            }

            Rigidbody body = motor.GetComponent<Rigidbody>();
            if (body == null || body.isKinematic)
            {
                report.AddError($"{scenePath}: {motor.name} requires a dynamic Rigidbody.");
            }

            RequireSpacecraftComponent<SpacecraftSurfaceContactProbe>(scenePath, motor, report);
            RequireSpacecraftComponent<SpacecraftSurfaceContactStabilizer>(scenePath, motor, report);
            RequireSpacecraftComponent<SpacecraftLandingComputer>(scenePath, motor, report);
            RequireSpacecraftComponent<SpacecraftLandingGuidanceComputer>(scenePath, motor, report);
            SpacecraftOceanInteractor oceanInteractor =
                RequireSpacecraftComponent<SpacecraftOceanInteractor>(scenePath, motor, report);
            if (oceanInteractor != null)
            {
                ValidateRequiredReference(scenePath, oceanInteractor, "profile", report);
                ValidateRequiredReference(scenePath, oceanInteractor, "celestialProbe", report);
            }

            SpacecraftAtmosphereInteractor atmosphereInteractor =
                RequireSpacecraftComponent<SpacecraftAtmosphereInteractor>(scenePath, motor, report);
            if (atmosphereInteractor != null)
            {
                ValidateRequiredReference(scenePath, atmosphereInteractor, "profile", report);
                ValidateRequiredReference(scenePath, atmosphereInteractor, "celestialProbe", report);
            }

            SpacecraftLandingGearAnimator landingGear =
                RequireSpacecraftComponent<SpacecraftLandingGearAnimator>(scenePath, motor, report);
            if (landingGear != null)
            {
                SerializedObject serializedLandingGear = new(landingGear);
                ValidateLandingGearRig(scenePath, motor, serializedLandingGear, report);
                SerializedProperty colliders =
                    serializedLandingGear.FindProperty("landingGearColliders");
                if (colliders == null || !colliders.isArray || colliders.arraySize == 0)
                {
                    report.AddError(
                        $"{scenePath}: {motor.name} landing gear has no physical contact collider.");
                }
                else
                {
                    for (int i = 0; i < colliders.arraySize; i++)
                    {
                        if (colliders.GetArrayElementAtIndex(i).objectReferenceValue == null)
                        {
                            report.AddError(
                                $"{scenePath}: {motor.name} landing gear collider {i} is missing.");
                        }
                    }

                    if (colliders.arraySize < 3)
                    {
                        report.AddWarning(
                            $"{scenePath}: {motor.name} still uses a temporary landing footprint; " +
                            "author three physical gear-contact colliders for the production rig.");
                    }
                }
            }

            SpacecraftLandingComputer landingComputer = motor.GetComponent<SpacecraftLandingComputer>();
            if (landingComputer != null)
            {
                ValidateRequiredReference(scenePath, landingComputer, "profile", report);
            }

            SpacecraftRig rig = motor.GetComponent<SpacecraftRig>();
            if (rig == null)
            {
                report.AddError($"{scenePath}: {motor.name} has no {nameof(SpacecraftRig)}.");
                return;
            }

            ValidateRequiredReference(scenePath, rig, "visualRoot", report);
            ValidateRequiredReference(scenePath, rig, "pilotSeatPoint", report);
            ValidateRequiredReference(scenePath, rig, "chaseCameraTarget", report);
            ValidateRequiredReference(scenePath, rig, "cockpitCameraTarget", report);

            SpacecraftThrusterVfxController vfxController =
                motor.GetComponentInChildren<SpacecraftThrusterVfxController>(true);
            if (vfxController != null)
            {
                ValidateThrusterVfxController(scenePath, motor, vfxController, report);
                return;
            }

            report.AddWarning($"{scenePath}: {motor.name} has no spacecraft VFX Graph thruster controller.");
        }

        static void ValidateShuttleBinding(
            string scenePath,
            SpacecraftMotor motor,
            FarionValidationReport report)
        {
            ShuttleRuntimeBinding binding =
                motor.GetComponent<ShuttleRuntimeBinding>();
            if (binding == null)
            {
                report.AddError(
                    $"{scenePath}: {motor.name} has no ShuttleRuntimeBinding.");
                return;
            }

            if (!binding.HasValidAuthoring)
            {
                report.AddError(
                    $"{scenePath}: {motor.name} has invalid shuttle authoring.");
            }

            if (binding.Motor != motor)
            {
                report.AddError(
                    $"{scenePath}: {motor.name} shuttle binding targets another motor.");
            }

            ShuttleCargoInventory cargo = binding.Cargo;
            if (cargo == null || cargo.gameObject != motor.gameObject)
            {
                report.AddError(
                    $"{scenePath}: {motor.name} requires one root-owned ShuttleCargoInventory.");
                return;
            }

            if (!PersistentEntityId.TryCreate(
                    $"ship_cargo.{binding.ShipId.Value}",
                    out PersistentEntityId expectedCargoId) ||
                cargo.ContainerId != expectedCargoId)
            {
                report.AddError(
                    $"{scenePath}: {motor.name} cargo id does not derive from its ship identity.");
            }
        }

        static void ValidateLandingGearRig(
            string scenePath,
            SpacecraftMotor motor,
            SerializedObject serializedLandingGear,
            FarionValidationReport report)
        {
            SerializedProperty parts = serializedLandingGear.FindProperty("landingGearParts");
            if (parts == null || !parts.isArray || parts.arraySize == 0)
            {
                report.AddError($"{scenePath}: {motor.name} landing gear has no animated parts.");
                return;
            }

            HashSet<string> partNames = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> parentByPart = new(StringComparer.OrdinalIgnoreCase);
            int linkedPartCount = 0;
            for (int i = 0; i < parts.arraySize; i++)
            {
                SerializedProperty part = parts.GetArrayElementAtIndex(i);
                string partName = part.FindPropertyRelative("partName")?.stringValue?.Trim();
                string parentName = part.FindPropertyRelative("parentPartName")?.stringValue?.Trim();
                if (string.IsNullOrEmpty(partName))
                {
                    report.AddError(
                        $"{scenePath}: {motor.name} landing gear part {i} has no source name.");
                    continue;
                }

                if (!partNames.Add(partName))
                {
                    report.AddError(
                        $"{scenePath}: {motor.name} landing gear part '{partName}' is duplicated.");
                }

                parentByPart[partName] = parentName;
                if (!string.IsNullOrEmpty(parentName))
                {
                    linkedPartCount++;
                }
            }

            foreach (KeyValuePair<string, string> pair in parentByPart)
            {
                if (string.IsNullOrEmpty(pair.Value))
                {
                    continue;
                }

                if (string.Equals(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase))
                {
                    report.AddError(
                        $"{scenePath}: {motor.name} landing gear part '{pair.Key}' parents itself.");
                }
                else if (!partNames.Contains(pair.Value))
                {
                    report.AddError(
                        $"{scenePath}: {motor.name} landing gear part '{pair.Key}' references " +
                        $"missing parent '{pair.Value}'.");
                }
                else if (HasLandingGearParentCycle(pair.Key, parentByPart))
                {
                    report.AddError(
                        $"{scenePath}: {motor.name} landing gear hierarchy contains a cycle at " +
                        $"'{pair.Key}'.");
                }
            }

            if (parts.arraySize > 1 && linkedPartCount == 0)
            {
                report.AddWarning(
                    $"{scenePath}: {motor.name} landing gear parts are all independent; " +
                    "articulate multi-part legs with parentPartName links.");
            }
        }

        static bool HasLandingGearParentCycle(
            string startPart,
            IReadOnlyDictionary<string, string> parentByPart)
        {
            HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
            string current = startPart;
            while (!string.IsNullOrEmpty(current) &&
                   parentByPart.TryGetValue(current, out string parent))
            {
                if (!visited.Add(current))
                {
                    return true;
                }

                current = parent;
            }

            return false;
        }

        static void ValidateThrusterVfxController(
            string scenePath,
            SpacecraftMotor motor,
            SpacecraftThrusterVfxController controller,
            FarionValidationReport report)
        {
            SpacecraftThrusterNozzleVfx[] nozzles =
                controller.GetComponentsInChildren<SpacecraftThrusterNozzleVfx>(true);
            if (nozzles == null || nozzles.Length == 0)
            {
                report.AddWarning($"{scenePath}: {motor.name} has no authored thruster VFX nozzles.");
                return;
            }

            bool hasLeft = false;
            bool hasRight = false;
            SerializedObject serializedController = new(controller);
            SerializedProperty authoredNozzles =
                serializedController.FindProperty("nozzles");
            if (authoredNozzles == null ||
                !authoredNozzles.isArray ||
                authoredNozzles.arraySize != nozzles.Length)
            {
                report.AddError(
                    $"{scenePath}: {motor.name} thruster controller must explicitly reference every authored nozzle.");
            }

            for (int i = 0; i < nozzles.Length; i++)
            {
                SpacecraftThrusterNozzleVfx nozzle = nozzles[i];
                if (nozzle == null)
                {
                    continue;
                }

                SerializedObject serializedNozzle = new(nozzle);
                SerializedProperty sideSign = serializedNozzle.FindProperty("sideSign");
                if (sideSign != null)
                {
                    hasLeft |= sideSign.floatValue < -0.01f;
                    hasRight |= sideSign.floatValue > 0.01f;
                }

                RequireObjectReference(
                    serializedNozzle,
                    "steeringRoot",
                    scenePath,
                    motor,
                    nozzle,
                    report);
                RequireObjectReference(
                    serializedNozzle,
                    "coreGlow",
                    scenePath,
                    motor,
                    nozzle,
                    report);
                RequireObjectReference(
                    serializedNozzle,
                    "innerPlasma",
                    scenePath,
                    motor,
                    nozzle,
                    report);
                RequireObjectReference(
                    serializedNozzle,
                    "outerPlasma",
                    scenePath,
                    motor,
                    nozzle,
                    report);
                RequireObjectReference(
                    serializedNozzle,
                    "shockDiamonds",
                    scenePath,
                    motor,
                    nozzle,
                    report);
                RequireObjectReference(
                    serializedNozzle,
                    "distortion",
                    scenePath,
                    motor,
                    nozzle,
                    report);
                RequireObjectReference(
                    serializedNozzle,
                    "sparksGraph",
                    scenePath,
                    motor,
                    nozzle,
                    report);
                RequireObjectReference(
                    serializedNozzle,
                    "smokeGraph",
                    scenePath,
                    motor,
                    nozzle,
                    report);
                RequireObjectReference(
                    serializedNozzle,
                    "thrusterLight",
                    scenePath,
                    motor,
                    nozzle,
                    report);

                SpacecraftThrusterMeshLayer[] meshLayers =
                    nozzle.GetComponentsInChildren<SpacecraftThrusterMeshLayer>(true);
                HashSet<SpacecraftThrusterMeshLayerKind> layerKinds = new();
                for (int layerIndex = 0; layerIndex < meshLayers.Length; layerIndex++)
                {
                    SpacecraftThrusterMeshLayer layer = meshLayers[layerIndex];
                    if (layer == null)
                    {
                        continue;
                    }

                    layerKinds.Add(layer.LayerKind);
                    if (!layer.HasValidAuthoring)
                    {
                        report.AddError(
                            $"{scenePath}: {motor.name}/{nozzle.name}/{layer.name} has incomplete " +
                            "thruster mesh authoring (mesh components or material missing).");
                    }
                }

                if (layerKinds.Count != 5)
                {
                    report.AddError(
                        $"{scenePath}: {motor.name}/{nozzle.name} requires exactly one authored " +
                        $"core, inner plasma, outer plasma, shock diamond, and distortion layer.");
                }

            }

            if (!hasLeft || !hasRight)
            {
                report.AddError(
                    $"{scenePath}: {motor.name} thruster VFX requires one left and one right main nozzle " +
                    $"(left={hasLeft}, right={hasRight}).");
            }
        }
    }
}
