using Farion.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using Farion.App.Flow;
using Farion.Audio.Direction;
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
    public sealed partial class FarionProjectValidator : IPreprocessBuildWithReport
    {
        const string MultiplayerPresentationScenePath =
            FarionAssetPaths.GameplayShellScene;
        const string WorldZoneScenePath =
            FarionAssetPaths.WorldZoneScene;

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
            ValidatePhysicsLayers(report);
            ValidateShaders(report);
            ValidateBuildScenes(report);
            ValidateDefinitionRegistries(report);
            ValidateMultiplayerAssets(report);
            ValidateCharacterAnimation(report);
            FarionUiValidator.ValidateProject(report);
            FarionCelestialVisualProjectValidator.ValidateProjectAssets(report);
            return report;
        }

        static void ValidatePhysicsLayers(FarionValidationReport report)
        {
            (int layer, string name)[] expectedNames =
            {
                (FarionLayers.CelestialSurface, "CelestialSurface"),
                (FarionLayers.SpacecraftExterior, "SpacecraftExterior"),
                (FarionLayers.SpacecraftInterior, "SpacecraftInterior"),
                (FarionLayers.Explorer, "Explorer"),
                (FarionLayers.ExplorerInterior, "ExplorerInterior"),
                (FarionLayers.Interactable, "Interactable")
            };

            foreach ((int layer, string name) in expectedNames)
            {
                string actual = LayerMask.LayerToName(layer);
                if (!string.Equals(actual, name, StringComparison.Ordinal))
                {
                    report.AddError(
                        $"Physics layer {layer} must be named '{name}' but is '{actual}'. Fix ProjectSettings/TagManager.asset.");
                }
            }

            (int a, int b)[] mustIgnore =
            {
                (FarionLayers.ExplorerInterior, FarionLayers.SpacecraftExterior),
                (FarionLayers.Interactable, FarionLayers.CelestialSurface),
                (FarionLayers.Interactable, FarionLayers.Interactable),
                (FarionLayers.CelestialSurface, FarionLayers.CelestialSurface),
                (FarionLayers.SpacecraftInterior, FarionLayers.CelestialSurface),
                (FarionLayers.SpacecraftInterior, FarionLayers.SpacecraftExterior),
                (FarionLayers.SpacecraftInterior, FarionLayers.SpacecraftInterior)
            };

            foreach ((int a, int b) in mustIgnore)
            {
                if (!UnityEngine.Physics.GetIgnoreLayerCollision(a, b))
                {
                    report.AddError(
                        $"Layers '{LayerMask.LayerToName(a)}' and '{LayerMask.LayerToName(b)}' must not collide. Fix the collision matrix in ProjectSettings/DynamicsManager.asset.");
                }
            }

            (int a, int b)[] mustCollide =
            {
                (FarionLayers.Explorer, FarionLayers.CelestialSurface),
                (FarionLayers.Explorer, FarionLayers.SpacecraftExterior),
                (FarionLayers.Explorer, FarionLayers.Interactable),
                (FarionLayers.ExplorerInterior, FarionLayers.SpacecraftInterior),
                (FarionLayers.ExplorerInterior, FarionLayers.Interactable),
                (FarionLayers.ExplorerInterior, FarionLayers.CelestialSurface),
                (FarionLayers.SpacecraftExterior, FarionLayers.CelestialSurface),
                (FarionLayers.SpacecraftExterior, FarionLayers.Interactable)
            };

            foreach ((int a, int b) in mustCollide)
            {
                if (UnityEngine.Physics.GetIgnoreLayerCollision(a, b))
                {
                    report.AddError(
                        $"Layers '{LayerMask.LayerToName(a)}' and '{LayerMask.LayerToName(b)}' must collide. Fix the collision matrix in ProjectSettings/DynamicsManager.asset.");
                }
            }
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
                SimulationZoneContext[] zones =
                    FindSceneComponents<SimulationZoneContext>(scene);
                bool isSimulationZone = zones.Length > 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    ValidateHierarchy(
                        root,
                        path,
                        persistentIds,
                        containerIds,
                        isSimulationZone,
                        report);
                }

                if (isSimulationZone)
                {
                    ValidateSimulationZoneScene(scene, path, zones, report);
                    ValidateCelestialSystem(scene, path, report);
                }
                else
                {
                    ValidateAudioComposition(scene, path, report);
                    if (path == MultiplayerPresentationScenePath)
                    {
                        ValidateMultiplayerPresentationScene(
                            scene,
                            path,
                            report);
                    }
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
            bool isSimulationZone,
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
                    isSimulationZone,
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

                if (gameObject.TryGetComponent(out CelestialFrameProvider frameProvider))
                {
                    ValidateRequiredReference(scenePath, frameProvider, "simulation", report);
                }

                if (gameObject.TryGetComponent(out GameplaySaveCoordinator saveCoordinator))
                {
                    ValidateSaveCoordinator(scenePath, saveCoordinator, report);
                }

                if (gameObject.TryGetComponent(out GameplayRuntimeRoot runtimeRoot))
                {
                    ValidateGameplayRuntimeRoot(scenePath, runtimeRoot, report);
                }

                if (gameObject.TryGetComponent(
                        out MultiplayerSceneContext multiplayerContext))
                {
                    if (isSimulationZone)
                    {
                        ValidateRequiredReference(
                            scenePath,
                            multiplayerContext,
                            "gravitySimulation",
                            report);
                        ValidateRequiredReference(
                            scenePath,
                            multiplayerContext,
                            "celestialFrameProvider",
                            report);
                        ValidateRequiredReference(
                            scenePath,
                            multiplayerContext,
                            "originRebaser",
                            report);
                        ValidateRequiredReference(
                            scenePath,
                            multiplayerContext,
                            "simulationZoneContext",
                            report);
                    }
                    else
                    {
                        ValidateRequiredReference(
                            scenePath,
                            multiplayerContext,
                            "runtimeRoot",
                            report);
                        ValidateRequiredReference(
                            scenePath,
                            multiplayerContext,
                            "spacecraftCameraRig",
                            report);
                        ValidateRequiredReference(
                            scenePath,
                            multiplayerContext,
                            "firstPersonCameraRig",
                            report);
                        ValidateRequiredReference(
                            scenePath,
                            multiplayerContext,
                            "viewReference",
                            report);
                        ValidateRequiredReference(
                            scenePath,
                            multiplayerContext,
                            "controlLock",
                            report);
                    }
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
            if (!PersistentObjectId.IsValid(persistentObjectId.Id))
            {
                string reason = string.IsNullOrEmpty(persistentObjectId.Id)
                    ? "is empty"
                    : "contains whitespace or control characters";
                report.AddError(
                    $"{scenePath}: {GetHierarchyPath(target)} has an invalid persistent id that {reason}.");
                return;
            }

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
            if (scenePath != WorldZoneScenePath)
            {
                ValidateRequiredReference(scenePath, controller, "controlLock", report);
            }

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

        static void ValidateAudioComposition(
            Scene scene,
            string scenePath,
            FarionValidationReport report)
        {
            AudioDirector[] directors = FindSceneComponents<AudioDirector>(scene);
            AudioSceneContext[] contexts = FindSceneComponents<AudioSceneContext>(scene);
            StudioListener[] studioListeners = FindSceneComponents<StudioListener>(scene);
            AudioListener[] unityListeners = FindSceneComponents<AudioListener>(scene);
            AudioSource[] unitySources = FindSceneComponents<AudioSource>(scene);

            if (directors.Length != 1)
            {
                report.AddError($"{scenePath}: expected one AudioDirector, found {directors.Length}.");
            }

            if (contexts.Length != 1)
            {
                report.AddError($"{scenePath}: expected one AudioSceneContext, found {contexts.Length}.");
            }

            if (studioListeners.Length != 1)
            {
                report.AddError($"{scenePath}: expected one FMOD StudioListener, found {studioListeners.Length}.");
            }
            else if (directors.Length == 1 &&
                     studioListeners[0].gameObject != directors[0].gameObject)
            {
                report.AddError(
                    $"{scenePath}: FMOD StudioListener must be owned by the persistent AudioDirector.");
            }

            if (unityListeners.Length > 0 || unitySources.Length > 0)
            {
                report.AddError(
                    $"{scenePath}: production audio is FMOD-only; found " +
                    $"{unityListeners.Length} AudioListener and {unitySources.Length} AudioSource component(s).");
            }
        }

        static T[] FindSceneComponents<T>(Scene scene) where T : Component
        {
            List<T> components = new();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                components.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return components.ToArray();
        }

        static void ValidateGameplayRuntimeRoot(
            string scenePath,
            GameplayRuntimeRoot runtimeRoot,
            FarionValidationReport report)
        {
            ValidateRequiredReference(scenePath, runtimeRoot, "definitions", report);
            ValidateRequiredReference(scenePath, runtimeRoot, "gravitySimulation", report);
            ValidateRequiredReference(scenePath, runtimeRoot, "celestialFrameProvider", report);
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

                if (definition is InventoryItemDefinition inventoryItem && !inventoryItem.HasIcon)
                {
                    report.AddError($"{registryPath}: inventory item '{inventoryItem.ItemId}' has no icon.");
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
}
