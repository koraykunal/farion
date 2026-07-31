using System;
using System.Collections.Generic;
using Farion.App.Flow;
using Farion.Core.Persistence;
using Farion.Core.Physics;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Persistence;
using Farion.Gameplay.Resources;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Farion.Editor.Validation
{
    public sealed class FarionProjectValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        [MenuItem("Farion/Validation/Validate Project")]
        public static void ValidateFromMenu()
        {
            FarionValidationReport report = ValidateProject();
            report.Log();
            if (report.HasErrors)
            {
                throw new BuildFailedException(
                    $"Farion project validation failed with {report.Errors.Count} error(s).");
            }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            FarionValidationReport validation = ValidateProject();
            if (validation.HasErrors)
            {
                validation.Log();
                throw new BuildFailedException(
                    $"Build blocked by {validation.Errors.Count} Farion validation error(s).");
            }
        }

        public static FarionValidationReport ValidateProject()
        {
            FarionValidationReport report = new();
            ValidateBuildScenes(report);
            ValidateDefinitionRegistries(report);
            FarionUiProjectValidator.ValidateProject(report);
            FarionCelestialVisualProjectValidator.ValidateProjectAssets(report);
            return report;
        }

        static void ValidateBuildScenes(FarionValidationReport report)
        {
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            int enabledSceneCount = 0;
            foreach (EditorBuildSettingsScene buildScene in buildScenes)
            {
                if (!buildScene.enabled)
                {
                    continue;
                }

                enabledSceneCount++;
                if (string.IsNullOrEmpty(buildScene.path) ||
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(buildScene.path) == null)
                {
                    report.AddError($"Build scene is missing: {buildScene.path}");
                    continue;
                }

                ValidateScene(buildScene.path, report);
            }

            if (enabledSceneCount == 0)
            {
                report.AddError("No enabled scenes exist in the active build profile.");
            }
        }

        static void ValidateScene(string path, FarionValidationReport report)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            try
            {
                Dictionary<string, PersistentObjectId> persistentIds =
                    new(StringComparer.Ordinal);
                Dictionary<PersistentEntityId, InventoryContainerComponent> containerIds =
                    new();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    ValidateHierarchy(
                        root,
                        path,
                        persistentIds,
                        containerIds,
                        report);
                }
            }
            finally
            {
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }

        static void ValidateHierarchy(
            GameObject root,
            string scenePath,
            Dictionary<string, PersistentObjectId> persistentIds,
            Dictionary<PersistentEntityId, InventoryContainerComponent> containerIds,
            FarionValidationReport report)
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject gameObject = item.gameObject;
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) > 0)
                {
                    report.AddError($"{scenePath}: {GetHierarchyPath(item)} has a missing script.");
                }

                if (gameObject.TryGetComponent(out PersistentObjectId persistentObjectId))
                {
                    ValidatePersistentId(scenePath, item, persistentObjectId, persistentIds, report);
                }

                if (gameObject.TryGetComponent(
                        out InventoryContainerComponent inventoryContainer))
                {
                    ValidateInventoryContainer(
                        scenePath,
                        item,
                        inventoryContainer,
                        containerIds,
                        report);
                }

                if (gameObject.TryGetComponent(out CelestialBody celestialBody) &&
                    string.IsNullOrEmpty(celestialBody.PersistentId))
                {
                    report.AddError(
                        $"{scenePath}: celestial body {GetHierarchyPath(item)} has no persistent id.");
                }

                FarionCelestialVisualProjectValidator.ValidateSceneObject(
                    gameObject,
                    scenePath,
                    report);

                if (gameObject.TryGetComponent(out PlayerPossessionController possessionController))
                {
                    ValidatePossessionController(scenePath, possessionController, report);
                }

                if (gameObject.TryGetComponent(out SpacecraftMotor spacecraftMotor))
                {
                    ValidateSpacecraft(scenePath, spacecraftMotor, report);
                }

                if (gameObject.TryGetComponent(out GameplaySessionController sessionController))
                {
                    ValidateRequiredReference(scenePath, sessionController, "flowSettings", report);
                    ValidateRequiredReference(scenePath, sessionController, "runtimeRoot", report);
                    ValidateGameplaySession(scenePath, sessionController, report);
                }

                if (gameObject.TryGetComponent(out GameplaySaveCoordinator saveCoordinator))
                {
                    ValidateSaveCoordinator(scenePath, saveCoordinator, report);
                }

                if (gameObject.TryGetComponent(out GameplayRuntimeRoot runtimeRoot))
                {
                    ValidateGameplayRuntimeRoot(scenePath, runtimeRoot, report);
                }

                if (gameObject.TryGetComponent(out FleetRuntime fleet))
                {
                    ValidateFleetRuntime(scenePath, fleet, report);
                }
            }
        }

        static void ValidatePersistentId(
            string scenePath,
            Transform target,
            PersistentObjectId persistentObjectId,
            Dictionary<string, PersistentObjectId> ids,
            FarionValidationReport report)
        {
            if (!PersistentEntityId.TryCreate(
                    persistentObjectId.Id,
                    out PersistentEntityId persistentId))
            {
                report.AddError(
                    $"{scenePath}: {GetHierarchyPath(target)} has an invalid persistent id.");
                return;
            }

            if (ids.TryGetValue(persistentId.Value, out PersistentObjectId existing))
            {
                report.AddError(
                    $"{scenePath}: duplicate persistent id '{persistentId}' on " +
                    $"{GetHierarchyPath(existing.transform)} and {GetHierarchyPath(target)}.");
                return;
            }

            ids.Add(persistentId.Value, persistentObjectId);
        }

        static void ValidateInventoryContainer(
            string scenePath,
            Transform target,
            InventoryContainerComponent container,
            Dictionary<PersistentEntityId, InventoryContainerComponent> containerIds,
            FarionValidationReport report)
        {
            if (!target.TryGetComponent(out PersistentObjectId ownerId) ||
                !PersistentEntityId.TryCreate(ownerId.Id, out _))
            {
                report.AddError(
                    $"{scenePath}: inventory container {GetHierarchyPath(target)} requires a valid PersistentObjectId.");
                return;
            }

            PersistentEntityId containerId = container.ContainerId;
            if (!containerId.IsValid)
            {
                report.AddError(
                    $"{scenePath}: inventory container {GetHierarchyPath(target)} has an invalid container id.");
                return;
            }

            if (containerIds.TryGetValue(
                    containerId,
                    out InventoryContainerComponent existing))
            {
                report.AddError(
                    $"{scenePath}: duplicate inventory container id '{containerId}' on " +
                    $"{GetHierarchyPath(existing.transform)} and {GetHierarchyPath(target)}.");
                return;
            }

            containerIds.Add(containerId, container);
        }

        static void ValidatePossessionController(
            string scenePath,
            PlayerPossessionController controller,
            FarionValidationReport report)
        {
            SerializedObject serialized = new(controller);
            Transform spacecraftRoot =
                serialized.FindProperty("spacecraftRoot")?.objectReferenceValue as Transform;
            if (spacecraftRoot == null)
            {
                report.AddError($"{scenePath}: {controller.name} has no spacecraft root.");
                return;
            }

            if (spacecraftRoot.GetComponentInChildren<PilotSeatInteractable>(true) == null)
            {
                report.AddError($"{scenePath}: {spacecraftRoot.name} has no pilot-seat interactable.");
            }

            if (spacecraftRoot.GetComponentInChildren<VehicleBoardingPoint>(true) == null)
            {
                report.AddError($"{scenePath}: {spacecraftRoot.name} has no vehicle boarding point.");
            }
        }

        static void ValidateSaveCoordinator(
            string scenePath,
            GameplaySaveCoordinator coordinator,
            FarionValidationReport report)
        {
            ValidateRequiredReference(scenePath, coordinator, "runtimeRoot", report);
        }

        static void ValidateGameplayRuntimeRoot(
            string scenePath,
            GameplayRuntimeRoot runtimeRoot,
            FarionValidationReport report)
        {
            ValidateRequiredReference(scenePath, runtimeRoot, "definitions", report);
            ValidateRequiredReference(scenePath, runtimeRoot, "gravitySimulation", report);
            ValidateRequiredReference(scenePath, runtimeRoot, "originRebaser", report);
            ValidateRequiredReference(scenePath, runtimeRoot, "localPlayerInventory", report);
            ValidateRequiredReference(scenePath, runtimeRoot, "possession", report);
            ValidateRequiredReference(scenePath, runtimeRoot, "assignedShuttle", report);
            ValidateRequiredReference(scenePath, runtimeRoot, "fleet", report);

            if (!runtimeRoot.HasValidAuthoring)
            {
                report.AddError(
                    $"{scenePath}: {runtimeRoot.name} cannot compose valid gameplay runtime bindings.");
            }
        }

        static void ValidateGameplaySession(
            string scenePath,
            GameplaySessionController controller,
            FarionValidationReport report)
        {
            SerializedProperty localPlayerId =
                new SerializedObject(controller).FindProperty("localPlayerId");
            if (localPlayerId == null ||
                !PersistentEntityId.TryCreate(
                    localPlayerId.stringValue,
                    out _))
            {
                report.AddError(
                    $"{scenePath}: {controller.name} has an invalid local player id.");
                return;
            }

            if (!controller.TryGetRuntime(out _))
            {
                report.AddError(
                    $"{scenePath}: {controller.name} cannot compose its local gameplay session.");
            }
        }

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

            SpacecraftReentryVfxController reentryVfx =
                RequireSpacecraftComponent<SpacecraftReentryVfxController>(
                    scenePath,
                    motor,
                    report);
            if (reentryVfx != null)
            {
                ValidateRequiredReference(scenePath, reentryVfx, "atmosphereInteractor", report);
                ValidateRequiredReference(scenePath, reentryVfx, "material", report);
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

        static void ValidateFleetRuntime(
            string scenePath,
            FleetRuntime fleet,
            FarionValidationReport report)
        {
            ValidateRequiredReference(scenePath, fleet, "persistentId", report);
            ValidateRequiredReference(scenePath, fleet, "knowledge", report);
            ValidateRequiredReference(scenePath, fleet, "storage", report);
            if (!fleet.HasValidAuthoring)
            {
                report.AddError(
                    $"{scenePath}: {fleet.name} has invalid Fleet runtime authoring.");
            }
        }

        static void RequireObjectReference(
            SerializedObject serializedNozzle,
            string propertyName,
            string scenePath,
            SpacecraftMotor motor,
            SpacecraftThrusterNozzleVfx nozzle,
            FarionValidationReport report)
        {
            SerializedProperty property =
                serializedNozzle.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null)
            {
                report.AddError(
                    $"{scenePath}: {motor.name}/{nozzle.name} is missing required thruster VFX reference '{propertyName}'.");
            }
        }

        static T RequireSpacecraftComponent<T>(
            string scenePath,
            SpacecraftMotor motor,
            FarionValidationReport report)
            where T : Component
        {
            T component = motor.GetComponent<T>();
            if (component == null)
            {
                report.AddError($"{scenePath}: {motor.name} has no {typeof(T).Name}.");
            }

            return component;
        }

        static void ValidateRequiredReference(
            string scenePath,
            Object owner,
            string propertyName,
            FarionValidationReport report)
        {
            SerializedProperty property = new SerializedObject(owner).FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null)
            {
                report.AddError(
                    $"{scenePath}: {owner.name} ({owner.GetType().Name}) is missing '{propertyName}'.");
            }
        }

        static void ValidateDefinitionRegistries(FarionValidationReport report)
        {
            string[] registryGuids = AssetDatabase.FindAssets("t:GameplayDefinitionRegistry");
            if (registryGuids.Length == 0)
            {
                report.AddError("No GameplayDefinitionRegistry asset exists.");
                return;
            }

            foreach (string guid in registryGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameplayDefinitionRegistry registry =
                    AssetDatabase.LoadAssetAtPath<GameplayDefinitionRegistry>(path);
                SerializedObject serialized = new(registry);
                SerializedProperty inventoryItems =
                    serialized.FindProperty("inventoryItems");
                ValidateDefinitionList<InventoryItemDefinition>(
                    inventoryItems, item => item.ItemId, path, report);
                ValidateDefinitionList<ResourceNodeDefinition>(
                    serialized.FindProperty("resourceNodes"), item => item.NodeId, path, report);
            }
        }

        static void ValidateDefinitionList<T>(
            SerializedProperty list,
            Func<T, string> getId,
            string registryPath,
            FarionValidationReport report)
            where T : Object
        {
            if (list == null || !list.isArray)
            {
                report.AddError($"{registryPath}: missing definition list for {typeof(T).Name}.");
                return;
            }

            HashSet<DefinitionId> ids = new();
            for (int i = 0; i < list.arraySize; i++)
            {
                T definition = list.GetArrayElementAtIndex(i).objectReferenceValue as T;
                string rawId = definition != null ? getId(definition) : string.Empty;
                if (definition == null ||
                    !DefinitionId.TryCreate(rawId, out DefinitionId id))
                {
                    report.AddError($"{registryPath}: invalid {typeof(T).Name} at index {i}.");
                }
                else if (!ids.Add(id))
                {
                    report.AddError($"{registryPath}: duplicate {typeof(T).Name} id '{id}'.");
                }
            }
        }

        static string GetHierarchyPath(Transform target)
        {
            string path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = $"{target.name}/{path}";
            }

            return path;
        }
    }

    public sealed class FarionValidationReport
    {
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        public bool HasErrors => Errors.Count > 0;

        public void AddError(string message)
        {
            Errors.Add(message);
        }

        public void AddWarning(string message)
        {
            Warnings.Add(message);
        }

        public void Log()
        {
            foreach (string error in Errors)
            {
                Debug.LogError($"[Farion Validation] {error}");
            }

            foreach (string warning in Warnings)
            {
                Debug.LogWarning($"[Farion Validation] {warning}");
            }

            if (!HasErrors)
            {
                Debug.Log($"[Farion Validation] Passed with {Warnings.Count} warning(s).");
            }
        }
    }
}
