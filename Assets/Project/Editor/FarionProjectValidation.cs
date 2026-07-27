using System;
using System.Collections.Generic;
using Farion.App.Flow;
using Farion.Core.Persistence;
using Farion.Core.Physics;
using Farion.Gameplay.Crafting;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Persistence;
using Farion.Gameplay.Research;
using Farion.Gameplay.Resources;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Farion.Editor.Validation
{
    [InitializeOnLoad]
    static class FarionStartupValidation
    {
        const string SessionKey = "Farion.ProjectValidation.Completed.SpacecraftV4";

        static FarionStartupValidation()
        {
            EditorApplication.delayCall += ValidateOncePerSession;
        }

        static void ValidateOncePerSession()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += ValidateOncePerSession;
                return;
            }

            SessionState.SetBool(SessionKey, true);
            FarionProjectValidator.ValidateProject().Log();
        }
    }

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
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    ValidateHierarchy(root, path, persistentIds, report);
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

                if (gameObject.TryGetComponent(out CelestialBody celestialBody) &&
                    string.IsNullOrEmpty(celestialBody.PersistentId))
                {
                    report.AddError(
                        $"{scenePath}: celestial body {GetHierarchyPath(item)} has no persistent id.");
                }

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
                    ValidateRequiredReference(scenePath, sessionController, "saveCoordinator", report);
                }

                if (gameObject.TryGetComponent(out GameplaySaveCoordinator saveCoordinator))
                {
                    ValidateSaveCoordinator(scenePath, saveCoordinator, report);
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
            if (!persistentObjectId.HasId)
            {
                report.AddError($"{scenePath}: {GetHierarchyPath(target)} has an empty persistent id.");
                return;
            }

            if (ids.TryGetValue(persistentObjectId.Id, out PersistentObjectId existing))
            {
                report.AddError(
                    $"{scenePath}: duplicate persistent id '{persistentObjectId.Id}' on " +
                    $"{GetHierarchyPath(existing.transform)} and {GetHierarchyPath(target)}.");
                return;
            }

            ids.Add(persistentObjectId.Id, persistentObjectId);
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
            ValidateRequiredReference(scenePath, coordinator, "definitions", report);
            ValidateRequiredReference(scenePath, coordinator, "gravitySimulation", report);
            ValidateRequiredReference(scenePath, coordinator, "originRebaser", report);
            ValidateRequiredReference(scenePath, coordinator, "playerInventory", report);
            ValidateRequiredReference(scenePath, coordinator, "possessionController", report);
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

            SpacecraftEngineMotionAnimator engineMotion =
                motor.GetComponent<SpacecraftEngineMotionAnimator>();
            SerializedProperty motionParts = engineMotion != null
                ? new SerializedObject(engineMotion).FindProperty("engineMotionParts")
                : null;
            if (motionParts == null || !motionParts.isArray || motionParts.arraySize < 2)
            {
                report.AddWarning(
                    $"{scenePath}: {motor.name} does not yet expose independent left/right engine gimbals.");
            }

            SpacecraftThrusterEffects effects =
                motor.GetComponentInChildren<SpacecraftThrusterEffects>(true);
            if (effects == null)
            {
                report.AddWarning($"{scenePath}: {motor.name} has no spacecraft thruster effects.");
            }
            else
            {
                ValidateThrusterEffectCoverage(scenePath, motor, effects, report);
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

        static void ValidateThrusterEffectCoverage(
            string scenePath,
            SpacecraftMotor motor,
            SpacecraftThrusterEffects effects,
            FarionValidationReport report)
        {
            SerializedProperty emitters = new SerializedObject(effects).FindProperty("emitters");
            if (emitters == null || !emitters.isArray || emitters.arraySize == 0)
            {
                report.AddWarning($"{scenePath}: {motor.name} has no authored thruster emitters.");
                return;
            }

            HashSet<int> roles = new();
            for (int i = 0; i < emitters.arraySize; i++)
            {
                SerializedProperty role = emitters.GetArrayElementAtIndex(i).FindPropertyRelative("role");
                if (role != null)
                {
                    roles.Add(role.enumValueIndex);
                }
            }

            bool hasMain = roles.Contains((int)SpacecraftThrusterEffects.ThrusterRole.MainForward);
            bool hasReverse = roles.Contains((int)SpacecraftThrusterEffects.ThrusterRole.Reverse);
            bool hasTranslationRcs =
                roles.Contains((int)SpacecraftThrusterEffects.ThrusterRole.StrafeLeft) &&
                roles.Contains((int)SpacecraftThrusterEffects.ThrusterRole.StrafeRight) &&
                roles.Contains((int)SpacecraftThrusterEffects.ThrusterRole.Ascend) &&
                roles.Contains((int)SpacecraftThrusterEffects.ThrusterRole.Descend);
            bool hasAngularRcs =
                roles.Contains((int)SpacecraftThrusterEffects.ThrusterRole.PitchUp) &&
                roles.Contains((int)SpacecraftThrusterEffects.ThrusterRole.PitchDown) &&
                roles.Contains((int)SpacecraftThrusterEffects.ThrusterRole.YawLeft) &&
                roles.Contains((int)SpacecraftThrusterEffects.ThrusterRole.YawRight) &&
                roles.Contains((int)SpacecraftThrusterEffects.ThrusterRole.RollLeft) &&
                roles.Contains((int)SpacecraftThrusterEffects.ThrusterRole.RollRight);

            if (!hasMain || !hasReverse || !hasTranslationRcs || !hasAngularRcs)
            {
                report.AddWarning(
                    $"{scenePath}: {motor.name} thruster VFX coverage is incomplete " +
                    $"(main={hasMain}, reverse={hasReverse}, translationRcs={hasTranslationRcs}, " +
                    $"angularRcs={hasAngularRcs}).");
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
                ValidateDefinitionList<InventoryItemDefinition>(
                    serialized.FindProperty("inventoryItems"), item => item.ItemId, path, report);
                ValidateDefinitionList<ResourceNodeDefinition>(
                    serialized.FindProperty("resourceNodes"), item => item.NodeId, path, report);
                ValidateDefinitionList<RecipeDefinition>(
                    serialized.FindProperty("recipes"), item => item.RecipeId, path, report);
                ValidateDefinitionList<ResearchDefinition>(
                    serialized.FindProperty("research"), item => item.ResearchId, path, report);
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

            HashSet<string> ids = new(StringComparer.Ordinal);
            for (int i = 0; i < list.arraySize; i++)
            {
                T definition = list.GetArrayElementAtIndex(i).objectReferenceValue as T;
                string id = definition != null ? getId(definition)?.Trim() : string.Empty;
                if (definition == null || string.IsNullOrEmpty(id))
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
