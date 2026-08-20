using System;
using System.Collections.Generic;
using System.Linq;
using Farion.Editor;
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
using Farion.Rendering.Celestial;
using Farion.Rendering.Lighting;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.UI.Foundation;
using Farion.UI.Gameplay;
using Farion.UI.MainMenu;
using Farion.UI.Navigation;
using FMODUnity;
using FishNet.Component.Observing;
using FishNet.Component.Transforming.Beta;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Object;
using FishNet.Managing.Observing;
using FishNet.Managing.Predicting;
using FishNet.Managing.Server;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Managing.Transporting;
using FishNet.Transporting.Multipass;
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
using Farion.Multiplayer.Spacecraft;

namespace Farion.Editor.Validation
{
    public sealed partial class FarionProjectValidator
    {
        const int MinimumTickRate = 30;
        const int MaximumTickRate = 90;
        const int MinimumStateInterpolation = 3;
        const string CoopScreenPath =
            "Assets/Project/Prefabs/UI/Screens/PF_UI_CoopScreen.prefab";


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
                FindSceneComponents<UiGameplayController>(scene).Length > 0 ||
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
                .FindProperty("starterShuttleFormations");
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
            UiGameplaySceneShellController[] shells =
                FindSceneComponents<UiGameplaySceneShellController>(scene);
            if (shells.Length != 1 ||
                FindSceneComponents<Camera>(scene).Length != 1 ||
                FindSceneComponents<FirstPersonCameraRig>(scene).Length != 1 ||
                FindSceneComponents<SpacecraftCameraRig>(scene).Length != 1 ||
                FindSceneComponents<PlayerControlLock>(scene).Length != 1 ||
                FindSceneComponents<UiGameplayController>(scene).Length != 1 ||
                FindSceneComponents<CelestialLightingRig>(scene).Length != 1 ||
                FindSceneComponents<CelestialLodController>(scene).Length != 1)
            {
                report.AddError(
                    $"{scenePath}: gameplay presentation requires exactly one shell controller, " +
                    "Camera, camera rigs, PlayerControlLock, UiGameplayController, " +
                    "CelestialLightingRig, and CelestialLodController.");
            }
            else if (!shells[0].IsValid ||
                     new SerializedObject(shells[0])
                         .FindProperty("worldSceneName")?.stringValue != "SC_WorldZone")
            {
                report.AddError(
                    $"{scenePath}: gameplay shell bindings or world-scene routing are incomplete.");
            }

            if (FindSceneComponents<UiSpacecraftFlightHudPresenter>(scene).Length != 1 ||
                FindSceneComponents<SpacecraftPostProcessRig>(scene).Length != 1)
            {
                report.AddError(
                    $"{scenePath}: gameplay presentation requires exactly one " +
                    "UiSpacecraftFlightHudPresenter and SpacecraftPostProcessRig.");
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
            const string starterShuttlePath =
                FarionAssetPaths.NetworkStarterShuttlePrefab;
            GameObject session = AssetDatabase.LoadAssetAtPath<GameObject>(sessionPath);
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
            GameObject sessionPlayer =
                AssetDatabase.LoadAssetAtPath<GameObject>(sessionPlayerPath);
            GameObject starterShuttle =
                AssetDatabase.LoadAssetAtPath<GameObject>(starterShuttlePath);
            if (session == null ||
                player == null ||
                sessionPlayer == null ||
                starterShuttle == null)
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
                session.GetComponent<MultiplayerPlayerSpawner>() == null ||
                session.GetComponent<MultiplayerWorldOriginAuthority>() == null ||
                session.GetComponent<ZonePhysicsTickDriver>() == null ||
                session.GetComponent<MultiplayerZoneCoordinator>() == null)
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
            MultiplayerZoneCoordinator zoneCoordinator =
                session.GetComponent<MultiplayerZoneCoordinator>();
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
                int tickRate = timeSerialized.FindProperty("_tickRate").intValue;
                if (tickRate < MinimumTickRate ||
                    tickRate > MaximumTickRate ||
                    timeSerialized.FindProperty("_physicsMode").intValue !=
                    (int)PhysicsMode.TimeManager)
                {
                    report.AddError(
                        $"{sessionPath}: TimeManager requires a tick rate between {MinimumTickRate} and {MaximumTickRate} with TimeManager network physics.");
                }
            }

            ValidateSessionNetworkManagers(session, sessionPath, report);
            ValidateCoopScreen(report);

            if (tugboat != null)
            {
                SerializedObject tugboatSerialized = new(tugboat);
                if (tugboatSerialized.FindProperty("_maximumClients").intValue != 4)
                {
                    report.AddError($"{sessionPath}: Tugboat must allow exactly four clients.");
                }
            }

            ValidateTransportComposition(session, sessionPath, report);

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

            NetworkObject starterShuttleObject =
                starterShuttle.GetComponent<NetworkObject>();
            NetworkStarterShuttle networkStarterShuttle =
                starterShuttle.GetComponent<NetworkStarterShuttle>();
            SpacecraftRig starterShuttleRig =
                starterShuttle.GetComponent<SpacecraftRig>();
            Transform starterShuttleVisual =
                starterShuttle.transform.Find("VisualRoot");
            Transform starterShuttleAnchors =
                starterShuttle.transform.Find("Anchors");
            NetworkTickSmoother visualSmoother = starterShuttleVisual != null
                ? starterShuttleVisual.GetComponent<NetworkTickSmoother>()
                : null;
            NetworkTickSmoother anchorSmoother = starterShuttleAnchors != null
                ? starterShuttleAnchors.GetComponent<NetworkTickSmoother>()
                : null;
            Rigidbody starterShuttleBody = starterShuttle.GetComponent<Rigidbody>();
            if (starterShuttleObject == null ||
                networkStarterShuttle == null ||
                starterShuttleRig == null ||
                starterShuttleVisual == null ||
                starterShuttleAnchors == null ||
                !HasNetworkTickSmoother(
                    starterShuttleObject,
                    visualSmoother,
                    starterShuttle.transform) ||
                anchorSmoother != null ||
                starterShuttleRig.ChaseCameraTarget == null ||
                !starterShuttleRig.ChaseCameraTarget.IsChildOf(starterShuttleVisual) ||
                starterShuttleRig.CockpitCameraTarget == null ||
                !starterShuttleRig.CockpitCameraTarget.IsChildOf(starterShuttleVisual) ||
                starterShuttleRig.InteriorSpawnPoint == null ||
                !starterShuttleRig.InteriorSpawnPoint.IsChildOf(starterShuttleAnchors) ||
                starterShuttleRig.ExteriorExitPoint == null ||
                !starterShuttleRig.ExteriorExitPoint.IsChildOf(starterShuttleAnchors) ||
                starterShuttleObject.NetworkBehaviours == null ||
                !starterShuttleObject.NetworkBehaviours.Contains(
                    networkStarterShuttle) ||
                starterShuttleBody == null ||
                !starterShuttleBody.isKinematic ||
                starterShuttle.GetComponent<SpacecraftMotor>()?.enabled != false ||
                starterShuttle.GetComponent<KeyboardSpacecraftInput>()?.enabled != false)
            {
                report.AddError(
                    $"{starterShuttlePath}: parked network starter ship composition is invalid.");
            }

            MultiplayerPlayerSpawner playerSpawner =
                session.GetComponent<MultiplayerPlayerSpawner>();
            SerializedProperty sessionPlayerReference = playerSpawner == null
                ? null
                : new SerializedObject(playerSpawner)
                    .FindProperty("sessionPlayerPrefab");
            if (sessionPlayerReference?.objectReferenceValue != sessionPlayerObject)
            {
                report.AddError(
                    $"{sessionPath}: session player prefab reference is missing.");
            }

            SerializedProperty starterShuttleReference = playerSpawner == null
                ? null
                : new SerializedObject(playerSpawner)
                    .FindProperty("starterShuttlePrefab");
            if (starterShuttleReference?.objectReferenceValue != starterShuttleObject)
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
                         starterShuttleObject == null ||
                         !registeredPrefabs.Prefabs.Contains(networkObject) ||
                         !registeredPrefabs.Prefabs.Contains(sessionPlayerObject) ||
                         !registeredPrefabs.Prefabs.Contains(starterShuttleObject))
                {
                    report.AddError(
                        $"{sessionPath}: spawnable prefab registry must contain exactly the Farion session player, explorer, and starter ship.");
                }
            }

        }

        static void ValidateTransportComposition(
            GameObject session,
            string sessionPath,
            FarionValidationReport report)
        {
            Multipass multipass = session.GetComponent<Multipass>();
            TransportManager transportManager = session.GetComponent<TransportManager>();
            FishySteamworks.FishySteamworks steam =
                session.GetComponent<FishySteamworks.FishySteamworks>();
            if (multipass == null || transportManager == null || steam == null)
            {
                report.AddError(
                    $"{sessionPath}: co-op requires Multipass, TransportManager, and the Steam transport on the session root.");
                return;
            }

            if (new SerializedObject(transportManager)
                    .FindProperty("Transport").objectReferenceValue != multipass)
            {
                report.AddError(
                    $"{sessionPath}: TransportManager must route through Multipass.");
            }

            SerializedObject serializedMultipass = new(multipass);
            SerializedProperty transports =
                serializedMultipass.FindProperty("_transports");
            if (!serializedMultipass.FindProperty("GlobalServerActions").boolValue)
            {
                report.AddError(
                    $"{sessionPath}: Multipass must use global server actions so the host listens on every transport.");
            }

            if (transports == null ||
                transports.arraySize != 2 ||
                transports.GetArrayElementAtIndex((int)MultiplayerTransportKind.Direct)
                    .objectReferenceValue != session.GetComponent<Tugboat>() ||
                transports.GetArrayElementAtIndex((int)MultiplayerTransportKind.Steam)
                    .objectReferenceValue != steam)
            {
                report.AddError(
                    $"{sessionPath}: Multipass transports must be ordered as Tugboat then Steam to match MultiplayerTransportKind.");
            }

            if (new SerializedObject(steam)
                    .FindProperty("_maximumClients").intValue != 4)
            {
                report.AddError(
                    $"{sessionPath}: the Steam transport must allow exactly four clients.");
            }
        }

        static readonly string[] CoopScreenFields =
        {
            "addressInput",
            "joinButton",
            "backButton",
            "statusText",
            "titleText"
        };

        static void ValidateCoopScreen(FarionValidationReport report)
        {
            GameObject coopScreen =
                AssetDatabase.LoadAssetAtPath<GameObject>(CoopScreenPath);
            if (coopScreen == null)
            {
                report.AddError($"{CoopScreenPath}: the co-op join screen is missing.");
                return;
            }

            UiScreenView screenView = coopScreen.GetComponent<UiScreenView>();
            UiCoopScreenPresenter presenter =
                coopScreen.GetComponent<UiCoopScreenPresenter>();
            if (screenView == null ||
                screenView.ScreenId != UiScreenId.Coop ||
                presenter == null)
            {
                report.AddError(
                    $"{CoopScreenPath}: the co-op screen composition is incomplete.");
                return;
            }

            SerializedObject serializedPresenter = new(presenter);
            foreach (string field in CoopScreenFields)
            {
                if (serializedPresenter.FindProperty(field).objectReferenceValue == null)
                {
                    report.AddError(
                        $"{CoopScreenPath}: the co-op screen must author its {field}.");
                }
            }
        }

        static void ValidateSessionNetworkManagers(
            GameObject session,
            string sessionPath,
            FarionValidationReport report)
        {
            ServerManager server = session.GetComponent<ServerManager>();
            ClientManager client = session.GetComponent<ClientManager>();
            PredictionManager prediction = session.GetComponent<PredictionManager>();
            FarionProtocolAuthenticator authenticator =
                session.GetComponent<FarionProtocolAuthenticator>();
            if (server == null ||
                client == null ||
                prediction == null ||
                authenticator == null ||
                session.GetComponent<MultiplayerStatusReporter>() == null ||
                session.GetComponent<MultiplayerSaveBridge>() == null)
            {
                report.AddError(
                    $"{sessionPath}: the session root must author ServerManager, ClientManager, PredictionManager, the protocol authenticator, the status reporter, and the save bridge.");
                return;
            }

            SerializedObject serializedServer = new(server);
            if (serializedServer.FindProperty("_changeFrameRate").boolValue ||
                new SerializedObject(client)
                    .FindProperty("_changeFrameRate").boolValue)
            {
                report.AddError(
                    $"{sessionPath}: networking must not override the application frame rate.");
            }

            if (serializedServer.FindProperty("_authenticator")
                    .objectReferenceValue != authenticator)
            {
                report.AddError(
                    $"{sessionPath}: ServerManager must use the Farion protocol authenticator.");
            }

            if (new SerializedObject(prediction)
                    .FindProperty("_stateInterpolation").intValue <
                MinimumStateInterpolation)
            {
                report.AddError(
                    $"{sessionPath}: prediction state interpolation must be at least {MinimumStateInterpolation}.");
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
