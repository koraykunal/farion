using System;
using System.Collections.Generic;
using System.Linq;
using Farion.Editor;
using Farion.App.Flow;
using Farion.Audio;
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
using Farion.Gameplay.Resources;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Session;
using Farion.Multiplayer.Spawning;
using Farion.Multiplayer.World;
using Farion.Rendering.Celestial;
using Farion.Rendering.Lighting;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.UI.Gameplay;
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
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Farion.Editor.Validation
{
    public sealed partial class FarionProjectValidator
    {
        static void ValidateSimulationZoneScene(
            Scene scene,
            string scenePath,
            SimulationZoneContext[] zones,
            FarionValidationReport report)
        {
            if (zones.Length != 1)
            {
                report.AddError(
                    $"{scenePath}: expected one SimulationZoneContext, found {zones.Length}.");
            }

            if (FindSceneComponents<AudioDirector>(scene).Length > 0 ||
                FindSceneComponents<AudioListener>(scene).Length > 0 ||
                FindSceneComponents<StudioListener>(scene).Length > 0)
            {
                report.AddError(
                    $"{scenePath}: a simulation zone must not duplicate global audio ownership.");
            }

            if (FindSceneComponents<Light>(scene)
                    .Any(light => light.type == LightType.Directional) ||
                FindSceneComponents<CelestialLightingRig>(scene).Length > 0)
            {
                report.AddError(
                    $"{scenePath}: a simulation zone must not duplicate global lighting ownership.");
            }

            if (FindSceneComponents<Camera>(scene).Length > 0 ||
                FindSceneComponents<GameplayUiController>(scene).Length > 0 ||
                FindSceneComponents<PlayerControlLock>(scene).Length > 0 ||
                FindSceneComponents<EventSystem>(scene).Length > 0 ||
                FindSceneComponents<Volume>(scene).Length > 0)
            {
                report.AddError(
                    $"{scenePath}: a simulation zone must not duplicate gameplay presentation ownership.");
            }

            GameplayRuntimeRoot[] runtimeRoots =
                FindSceneComponents<GameplayRuntimeRoot>(scene);
            if (runtimeRoots.Length != 1)
            {
                report.AddError(
                    $"{scenePath}: expected one GameplayRuntimeRoot, found {runtimeRoots.Length}.");
            }

            MultiplayerSceneContext[] contexts =
                FindSceneComponents<MultiplayerSceneContext>(scene);
            if (contexts.Length != 1)
            {
                report.AddError(
                    $"{scenePath}: expected one MultiplayerSceneContext, found {contexts.Length}.");
                return;
            }

            SerializedProperty formations = new SerializedObject(contexts[0])
                .FindProperty("starterShipFormations");
            if (formations == null || formations.arraySize != 4)
            {
                report.AddError(
                    $"{scenePath}: starter ship formations must contain Count_1 through Count_4.");
                return;
            }

            for (int i = 0; i < formations.arraySize; i++)
            {
                Transform formation = formations.GetArrayElementAtIndex(i)
                    .objectReferenceValue as Transform;
                if (formation == null || formation.name != $"Count_{i + 1}")
                {
                    report.AddError(
                        $"{scenePath}: starter ship formation {i + 1} is missing or misordered.");
                    continue;
                }

                for (int shipIndex = 0; shipIndex <= i; shipIndex++)
                {
                    if (formation.Find($"Ship_{shipIndex + 1}") == null)
                    {
                        report.AddError(
                            $"{scenePath}: {formation.name}/Ship_{shipIndex + 1} is missing.");
                    }
                }
            }
        }

        static void ValidateMultiplayerPresentationScene(
            Scene scene,
            string scenePath,
            FarionValidationReport report)
        {
            GameplaySceneShellController[] shells =
                FindSceneComponents<GameplaySceneShellController>(scene);
            if (shells.Length != 1 ||
                FindSceneComponents<Camera>(scene).Length != 1 ||
                FindSceneComponents<FirstPersonCameraRig>(scene).Length != 1 ||
                FindSceneComponents<SpacecraftCameraRig>(scene).Length != 1 ||
                FindSceneComponents<PlayerControlLock>(scene).Length != 1 ||
                FindSceneComponents<GameplayUiController>(scene).Length != 1 ||
                FindSceneComponents<CelestialLightingRig>(scene).Length != 1 ||
                FindSceneComponents<CelestialLodController>(scene).Length != 1)
            {
                report.AddError(
                    $"{scenePath}: gameplay presentation requires exactly one shell controller, " +
                    "Camera, camera rigs, PlayerControlLock, GameplayUiController, " +
                    "CelestialLightingRig, and CelestialLodController.");
            }
            else if (!shells[0].IsValid ||
                     new SerializedObject(shells[0])
                         .FindProperty("worldSceneName")?.stringValue != "SC_WorldZone")
            {
                report.AddError(
                    $"{scenePath}: gameplay shell bindings or world-scene routing are incomplete.");
            }

            if (FindSceneComponents<SpacecraftFlightHudPresenter>(scene).Length != 1 ||
                FindSceneComponents<FarionPostProcessRig>(scene).Length != 1)
            {
                report.AddError(
                    $"{scenePath}: gameplay presentation requires exactly one " +
                    "SpacecraftFlightHudPresenter and FarionPostProcessRig.");
            }

            if (FindSceneComponents<CelestialBody>(scene).Length > 0 ||
                FindSceneComponents<GravitySimulation>(scene).Length > 0 ||
                FindSceneComponents<GameplayRuntimeRoot>(scene).Length > 0)
            {
                report.AddError(
                    $"{scenePath}: gameplay presentation must not own world simulation or offline gameplay state.");
            }
        }

        static void ValidateMultiplayerAssets(FarionValidationReport report)
        {
            const string sessionPath =
                FarionAssetPaths.NetworkSessionRootPrefab;
            const string playerPath =
                FarionAssetPaths.NetworkExplorerPrefab;
            const string sessionPlayerPath =
                FarionAssetPaths.NetworkSessionPlayerPrefab;
            const string starterShipPath =
                FarionAssetPaths.NetworkStarterShuttlePrefab;
            GameObject session = AssetDatabase.LoadAssetAtPath<GameObject>(sessionPath);
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
            GameObject sessionPlayer =
                AssetDatabase.LoadAssetAtPath<GameObject>(sessionPlayerPath);
            GameObject starterShip =
                AssetDatabase.LoadAssetAtPath<GameObject>(starterShipPath);
            if (session == null ||
                player == null ||
                sessionPlayer == null ||
                starterShip == null)
            {
                report.AddError(
                    "Multiplayer session, player, or starter ship prefab is missing.");
                return;
            }

            NetworkManager manager = session.GetComponent<NetworkManager>();
            TimeManager timeManager = session.GetComponent<TimeManager>();
            Tugboat tugboat = session.GetComponent<Tugboat>();
            ObserverManager observerManager = session.GetComponent<ObserverManager>();
            if (manager == null ||
                timeManager == null ||
                tugboat == null ||
                observerManager == null ||
                session.GetComponent<MultiplayerSessionController>() == null ||
                session.GetComponent<NetworkPlayerSpawner>() == null ||
                session.GetComponent<NetworkWorldOriginAuthority>() == null ||
                session.GetComponent<ZonePhysicsTickDriver>() == null ||
                session.GetComponent<NetworkZoneCoordinator>() == null)
            {
                report.AddError($"{sessionPath}: incomplete networking composition.");
            }

            if (observerManager != null)
            {
                SerializedProperty conditions =
                    new SerializedObject(observerManager)
                        .FindProperty("_defaultConditions");
                bool hasSceneCondition = false;
                for (int i = 0; conditions != null && i < conditions.arraySize; i++)
                {
                    if (conditions.GetArrayElementAtIndex(i).objectReferenceValue
                        is SceneCondition)
                    {
                        hasSceneCondition = true;
                        break;
                    }
                }

                if (!hasSceneCondition)
                {
                    report.AddError(
                        $"{sessionPath}: ObserverManager must include FishNet SceneCondition.");
                }
            }

            ValidateRequiredBuildScene(MultiplayerPresentationScenePath, report);
            ValidateRequiredBuildScene(WorldZoneScenePath, report);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldZoneScenePath) == null)
            {
                return;
            }

            MultiplayerSessionController sessionController =
                session.GetComponent<MultiplayerSessionController>();
            NetworkZoneCoordinator zoneCoordinator =
                session.GetComponent<NetworkZoneCoordinator>();
            if (sessionController != null)
            {
                SerializedObject controllerSerialized =
                    new(sessionController);
                if (controllerSerialized.FindProperty("zoneCoordinator")
                        ?.objectReferenceValue != zoneCoordinator ||
                    controllerSerialized.FindProperty("presentationSceneName")
                        ?.stringValue != "SC_GameplayShell" ||
                    controllerSerialized.FindProperty("startingZoneSceneName")
                        ?.stringValue != "SC_WorldZone")
                {
                    report.AddError(
                        $"{sessionPath}: multiplayer shell or starting-zone routing is incomplete.");
                }
            }

            if (timeManager != null)
            {
                SerializedObject timeSerialized = new(timeManager);
                if (timeSerialized.FindProperty("_tickRate").intValue <= 0 ||
                    timeSerialized.FindProperty("_physicsMode").intValue !=
                    (int)PhysicsMode.TimeManager)
                {
                    report.AddError(
                        $"{sessionPath}: TimeManager requires a positive tick rate and TimeManager network physics.");
                }
            }

            if (tugboat != null)
            {
                SerializedObject tugboatSerialized = new(tugboat);
                if (tugboatSerialized.FindProperty("_maximumClients").intValue != 4)
                {
                    report.AddError($"{sessionPath}: Tugboat must allow exactly four clients.");
                }
            }

            NetworkObject networkObject = player.GetComponent<NetworkObject>();
            NetworkExplorerController networkExplorer =
                player.GetComponent<NetworkExplorerController>();
            Transform playerVisual = player.transform.Find("VisualRoot");
            NetworkTickSmoother networkSmoother = playerVisual != null
                ? playerVisual.GetComponent<NetworkTickSmoother>()
                : null;
            if (networkObject == null ||
                !networkObject.EnablePrediction ||
                networkExplorer == null ||
                playerVisual == null ||
                !HasNetworkTickSmoother(
                    networkObject,
                    networkSmoother,
                    player.transform) ||
                networkObject.NetworkBehaviours == null ||
                !networkObject.NetworkBehaviours.Contains(networkExplorer))
            {
                report.AddError($"{playerPath}: predicted network explorer composition is invalid.");
            }

            if (player.GetComponent<PersistentObjectId>() == null ||
                player.GetComponentInChildren<PlayerInventory>(true) == null)
            {
                report.AddError(
                    $"{playerPath}: network explorer prefab must carry PersistentObjectId and PlayerInventory.");
            }

            NetworkObject sessionPlayerObject =
                sessionPlayer.GetComponent<NetworkObject>();
            NetworkSessionPlayer networkSessionPlayer =
                sessionPlayer.GetComponent<NetworkSessionPlayer>();
            if (sessionPlayerObject == null ||
                !sessionPlayerObject.IsGlobal ||
                networkSessionPlayer == null ||
                sessionPlayerObject.NetworkBehaviours == null ||
                !sessionPlayerObject.NetworkBehaviours.Contains(
                    networkSessionPlayer))
            {
                report.AddError(
                    $"{sessionPlayerPath}: global session identity composition is invalid.");
            }

            NetworkGameplayCommands networkGameplayCommands =
                sessionPlayer.GetComponent<NetworkGameplayCommands>();
            if (networkGameplayCommands == null ||
                sessionPlayerObject == null ||
                sessionPlayerObject.NetworkBehaviours == null ||
                !sessionPlayerObject.NetworkBehaviours.Contains(
                    networkGameplayCommands))
            {
                report.AddError(
                    $"{sessionPlayerPath}: NetworkGameplayCommands component is missing.");
            }
            else if (new SerializedObject(networkGameplayCommands)
                         .FindProperty("maximumHarvestDistance")
                         ?.floatValue <= 0f)
            {
                report.AddError(
                    $"{sessionPlayerPath}: network harvest distance must be positive.");
            }

            NetworkObject starterShipObject =
                starterShip.GetComponent<NetworkObject>();
            NetworkStarterShip networkStarterShip =
                starterShip.GetComponent<NetworkStarterShip>();
            SpacecraftRig starterShipRig =
                starterShip.GetComponent<SpacecraftRig>();
            Transform starterShipVisual =
                starterShip.transform.Find("VisualRoot");
            Transform starterShipAnchors =
                starterShip.transform.Find("Anchors");
            NetworkTickSmoother visualSmoother = starterShipVisual != null
                ? starterShipVisual.GetComponent<NetworkTickSmoother>()
                : null;
            NetworkTickSmoother anchorSmoother = starterShipAnchors != null
                ? starterShipAnchors.GetComponent<NetworkTickSmoother>()
                : null;
            Rigidbody starterShipBody = starterShip.GetComponent<Rigidbody>();
            if (starterShipObject == null ||
                networkStarterShip == null ||
                starterShipRig == null ||
                starterShipVisual == null ||
                starterShipAnchors == null ||
                !HasNetworkTickSmoother(
                    starterShipObject,
                    visualSmoother,
                    starterShip.transform) ||
                anchorSmoother != null ||
                starterShipRig.ChaseCameraTarget == null ||
                !starterShipRig.ChaseCameraTarget.IsChildOf(starterShipVisual) ||
                starterShipRig.CockpitCameraTarget == null ||
                !starterShipRig.CockpitCameraTarget.IsChildOf(starterShipVisual) ||
                starterShipRig.InteriorSpawnPoint == null ||
                !starterShipRig.InteriorSpawnPoint.IsChildOf(starterShipAnchors) ||
                starterShipRig.ExteriorExitPoint == null ||
                !starterShipRig.ExteriorExitPoint.IsChildOf(starterShipAnchors) ||
                starterShipObject.NetworkBehaviours == null ||
                !starterShipObject.NetworkBehaviours.Contains(
                    networkStarterShip) ||
                starterShipBody == null ||
                !starterShipBody.isKinematic ||
                starterShip.GetComponent<SpacecraftMotor>()?.enabled != false ||
                starterShip.GetComponent<KeyboardSpacecraftInput>()?.enabled != false)
            {
                report.AddError(
                    $"{starterShipPath}: parked network starter ship composition is invalid.");
            }

            NetworkPlayerSpawner playerSpawner =
                session.GetComponent<NetworkPlayerSpawner>();
            SerializedProperty sessionPlayerReference = playerSpawner == null
                ? null
                : new SerializedObject(playerSpawner)
                    .FindProperty("sessionPlayerPrefab");
            if (sessionPlayerReference?.objectReferenceValue != sessionPlayerObject)
            {
                report.AddError(
                    $"{sessionPath}: session player prefab reference is missing.");
            }

            SerializedProperty starterShipReference = playerSpawner == null
                ? null
                : new SerializedObject(playerSpawner)
                    .FindProperty("starterShipPrefab");
            if (starterShipReference?.objectReferenceValue != starterShipObject)
            {
                report.AddError(
                    $"{sessionPath}: starter ship prefab reference is missing.");
            }

            Rigidbody playerBody = player.GetComponent<Rigidbody>();
            if (playerBody == null ||
                (playerBody.constraints & RigidbodyConstraints.FreezeRotation) !=
                    RigidbodyConstraints.FreezeRotation)
            {
                report.AddError(
                    $"{playerPath}: explorer Rigidbody must freeze physics rotation.");
            }

            if (manager != null)
            {
                if (manager.SpawnablePrefabs is not DefaultPrefabObjects registeredPrefabs)
                {
                    report.AddError(
                        $"{sessionPath}: NetworkManager must use the Farion default prefab registry.");
                }
                else if (registeredPrefabs.Prefabs.Count != 3 ||
                         networkObject == null ||
                         sessionPlayerObject == null ||
                         starterShipObject == null ||
                         !registeredPrefabs.Prefabs.Contains(networkObject) ||
                         !registeredPrefabs.Prefabs.Contains(sessionPlayerObject) ||
                         !registeredPrefabs.Prefabs.Contains(starterShipObject))
                {
                    report.AddError(
                        $"{sessionPath}: spawnable prefab registry must contain exactly the Farion session player, explorer, and starter ship.");
                }
            }

        }

        static void ValidateRequiredBuildScene(
            string scenePath,
            FarionValidationReport report)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                report.AddError($"{scenePath}: required multiplayer scene is missing.");
                return;
            }

            if (!Array.Exists(
                    EditorBuildSettings.scenes,
                    scene => scene.enabled && scene.path == scenePath))
            {
                report.AddError(
                    $"{scenePath}: required multiplayer scene must be enabled in Build Profiles.");
            }
        }

        static bool HasNetworkTickSmoother(
            NetworkObject networkObject,
            NetworkTickSmoother smoother,
            Transform target)
        {
            if (networkObject == null ||
                smoother == null ||
                target == null ||
                networkObject.NetworkBehaviours == null ||
                !networkObject.NetworkBehaviours.Contains(smoother))
            {
                return false;
            }

            SerializedProperty initialization = new SerializedObject(smoother)
                .FindProperty("_initializationSettings");
            return initialization != null &&
                initialization.FindPropertyRelative("TargetTransform")
                    .objectReferenceValue == target &&
                initialization.FindPropertyRelative("DetachOnStart").boolValue &&
                initialization.FindPropertyRelative("AttachOnStop").boolValue;
        }
    }
}
