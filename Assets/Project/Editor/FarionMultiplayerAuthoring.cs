using System;
using System.Collections.Generic;
using Farion.Core.Persistence;
using Farion.Gameplay.Actors;
using Farion.Gameplay.Character;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Input;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Persistence;
using Farion.Gameplay.Resources;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Session;
using Farion.Multiplayer.Spawning;
using Farion.Multiplayer.World;
using Farion.Multiplayer.World.Zones;
using Farion.Rendering.Celestial;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.World;
using Farion.UI.MainMenu;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Observing;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Observing;
using FishNet.Component.Transforming.Beta;
using FishNet.Editing;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FishNetSceneManager = FishNet.Managing.Scened.SceneManager;
using Object = UnityEngine.Object;

namespace Farion.EditorTools
{
    public static class FarionMultiplayerAuthoring
    {
        const string ScenePath =
            "Assets/Project/Scenes/SC_Expedition.unity";
        const string MainMenuScenePath =
            "Assets/Project/Scenes/SC_MainMenu.unity";
        const string MenuButtonPrefabPath =
            "Assets/Project/Prefabs/UI/Common/UI_MenuButton.prefab";
        const string ProfilePath =
            "Assets/Project/Design/Gameplay/Character/SO_DefaultFirstPersonMotorProfile.asset";
        const string PrefabFolder =
            "Assets/Project/Prefabs/Gameplay/Character";
        const string CorePrefabPath =
            PrefabFolder + "/PF_PlayerExplorerCore.prefab";
        const string OfflinePrefabPath =
            PrefabFolder + "/PF_PlayerExplorerOffline.prefab";
        const string NetworkPrefabPath =
            PrefabFolder + "/PF_PlayerExplorerNetwork.prefab";
        const string MultiplayerPrefabFolder =
            "Assets/Project/Prefabs/Multiplayer";
        const string SessionPlayerPrefabPath =
            MultiplayerPrefabFolder + "/PF_NetworkSessionPlayer.prefab";
        const string StarterShuttleSourcePath =
            "Assets/Project/Prefabs/Gameplay/Spacecraft/PF_PlayerStarterShuttle.prefab";
        const string NetworkStarterShuttlePath =
            MultiplayerPrefabFolder + "/PF_StarterShuttleNetwork.prefab";
        const string DefaultPrefabObjectsPath =
            "Assets/DefaultPrefabObjects.asset";
        const string ResourceFolder =
            "Assets/Project/Resources/Multiplayer";
        const string SessionPrefabPath =
            ResourceFolder + "/PF_NetworkSessionRoot.prefab";
        const string WorldZoneScenePath =
            "Assets/Project/Scenes/SC_WorldZone.unity";
        const string SceneConditionPath =
            "Assets/FishNet/Runtime/Observing/Conditions/ScriptableObjects/SceneCondition.asset";

        [MenuItem("Farion/Multiplayer/Install Zone Foundation")]
        public static void InstallZoneFoundation()
        {
            CreateWorldZoneScene();
            InstallZoneSessionComponents();
            EnsureBuildScene(WorldZoneScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Farion zone foundation authored.");
        }

        [MenuItem("Farion/Multiplayer/Install Session Player Foundation")]
        public static void InstallSessionPlayerFoundation()
        {
            EnsureFolder("Assets/Project/Prefabs", "Multiplayer");
            GameObject sessionPlayerPrefab = CreateSessionPlayerPrefab();
            InstallSessionPlayerReference(sessionPlayerPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Farion session player foundation authored.");
        }

        [MenuItem("Farion/Multiplayer/Install Starter Ship Foundation")]
        public static void InstallStarterShipFoundation()
        {
            EnsureFolder("Assets/Project/Prefabs", "Multiplayer");
            GameObject starterShipPrefab = CreateNetworkStarterShipPrefab();
            RegisterNetworkPrefabs(starterShipPrefab);
            InstallStarterShipReference(starterShipPrefab);
            InstallStarterShipSceneReferences();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshDefaultPrefabsMenu.RebuildDefaultPrefabs();
            AssetDatabase.SaveAssets();
            Debug.Log("Farion starter ship foundation authored.");
        }

        static GameObject CreateCorePrefab(MeshRenderer sourceRenderer)
        {
            GameObject root = new("PF_PlayerExplorerCore");
            Rigidbody rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = 1f;
            rigidbody.useGravity = false;
            rigidbody.freezeRotation = true;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;

            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 0.5f;
            capsule.height = 2f;

            root.AddComponent<CelestialActorProbe>();
            FirstPersonMotor motor = root.AddComponent<FirstPersonMotor>();

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "VisualRoot";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            if (sourceRenderer != null)
            {
                visual.GetComponent<MeshRenderer>().sharedMaterials =
                    sourceRenderer.sharedMaterials;
            }

            GameObject cameraAnchor = new("CameraAnchor");
            cameraAnchor.transform.SetParent(root.transform, false);
            cameraAnchor.transform.localPosition = Vector3.up * 1.65f;

            SerializedObject motorObject = new(motor);
            motorObject.FindProperty("profile").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<FirstPersonMotorProfile>(ProfilePath);
            motorObject.FindProperty("viewReference").objectReferenceValue =
                cameraAnchor.transform;
            motorObject.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                root,
                CorePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject CreateOfflinePrefab(GameObject corePrefab)
        {
            GameObject instance =
                (GameObject)PrefabUtility.InstantiatePrefab(corePrefab);
            instance.name = "PF_PlayerExplorerOffline";
            KeyboardFirstPersonInput input =
                instance.AddComponent<KeyboardFirstPersonInput>();
            PlayerInteractionRaycaster raycaster =
                instance.AddComponent<PlayerInteractionRaycaster>();
            instance.AddComponent<PlayerInventory>();
            PersistentObjectId persistentId =
                instance.GetComponent<PersistentObjectId>();
            persistentId.SetId("player.explorer");

            FirstPersonMotor motor = instance.GetComponent<FirstPersonMotor>();
            SetObjectReference(motor, "inputSource", input);
            SetObjectReference(raycaster, "inputSource", input);
            SetObjectReference(
                raycaster,
                "viewReference",
                instance.transform.Find("CameraAnchor"));
            instance.transform.Find("VisualRoot")
                .GetComponent<Renderer>().enabled = false;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                instance,
                OfflinePrefabPath);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        static GameObject CreateNetworkPrefab(GameObject corePrefab)
        {
            GameObject instance =
                (GameObject)PrefabUtility.InstantiatePrefab(corePrefab);
            instance.name = "PF_PlayerExplorerNetwork";
            KeyboardFirstPersonInput input =
                instance.AddComponent<KeyboardFirstPersonInput>();
            NetworkObject networkObject = instance.AddComponent<NetworkObject>();
            NetworkExplorerController controller =
                instance.AddComponent<NetworkExplorerController>();
            Renderer visual = instance.transform.Find("VisualRoot")
                .GetComponent<Renderer>();
            visual.enabled = true;
            NetworkTickSmoother smoother =
                visual.gameObject.AddComponent<NetworkTickSmoother>();

            SetObjectReference(controller, "motor", instance.GetComponent<FirstPersonMotor>());
            SetObjectReference(controller, "input", input);
            SetObjectReferenceArray(
                controller,
                "ownerHiddenRenderers",
                new Object[] { visual });

            SerializedObject networkObjectSerialized = new(networkObject);
            networkObjectSerialized.FindProperty("_enablePrediction").boolValue = true;
            networkObjectSerialized.FindProperty("_predictionType").enumValueIndex = 1;
            networkObjectSerialized.FindProperty("_graphicalObject").objectReferenceValue =
                null;
            networkObjectSerialized.FindProperty("_enableStateForwarding").boolValue = true;
            networkObjectSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject smootherSerialized = new(smoother);
            SerializedProperty initialization =
                smootherSerialized.FindProperty("_initializationSettings");
            initialization.FindPropertyRelative("TargetTransform")
                .objectReferenceValue = instance.transform;
            initialization.FindPropertyRelative("DetachOnStart").boolValue = true;
            initialization.FindPropertyRelative("AttachOnStop").boolValue = true;
            smootherSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                instance,
                NetworkPrefabPath);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        static GameObject InstallOfflineExplorer(
            Scene scene,
            GameObject offlinePrefab)
        {
            GameObject current = FindNamedObject(scene, "Player Explorer");
            if (current == null)
            {
                current = FindNamedObject(scene, "PF_PlayerExplorerOffline");
            }

            if (current != null &&
                PrefabUtility.GetCorrespondingObjectFromSource(current) == offlinePrefab)
            {
                return current;
            }

            Transform parent = current != null ? current.transform.parent : null;
            Vector3 localPosition = current != null
                ? current.transform.localPosition
                : Vector3.zero;
            Quaternion localRotation = current != null
                ? current.transform.localRotation
                : Quaternion.identity;
            Vector3 localScale = current != null
                ? current.transform.localScale
                : Vector3.one;
            int siblingIndex = current != null
                ? current.transform.GetSiblingIndex()
                : 0;

            GameObject next = (GameObject)PrefabUtility.InstantiatePrefab(
                offlinePrefab,
                scene);
            next.name = "Player Explorer";
            next.transform.SetParent(parent, false);
            next.transform.localPosition = localPosition;
            next.transform.localRotation = localRotation;
            next.transform.localScale = localScale;
            next.transform.SetSiblingIndex(siblingIndex);

            if (current != null)
            {
                CopyComponent(current, next, typeof(Rigidbody));
                CopyComponent(current, next, typeof(CapsuleCollider));
                CopyComponent(current, next, typeof(FirstPersonMotor));
                CopyComponent(current, next, typeof(KeyboardFirstPersonInput));
                CopyComponent(current, next, typeof(CelestialActorProbe));
                CopyComponent(current, next, typeof(PlayerInteractionRaycaster));
                CopyComponent(current, next, typeof(PlayerInventory));
                CopyComponent(current, next, typeof(PersistentObjectId));
            }

            FirstPersonMotor motor = next.GetComponent<FirstPersonMotor>();
            KeyboardFirstPersonInput input =
                next.GetComponent<KeyboardFirstPersonInput>();
            PlayerInteractionRaycaster raycaster =
                next.GetComponent<PlayerInteractionRaycaster>();
            SetObjectReference(motor, "inputSource", input);
            SetObjectReference(raycaster, "inputSource", input);

            RewireSceneReferences(scene, current, next);
            if (current != null)
            {
                Object.DestroyImmediate(current);
            }

            return next;
        }

        static void RewireSceneReferences(
            Scene scene,
            GameObject previous,
            GameObject next)
        {
            FirstPersonMotor nextMotor = next.GetComponent<FirstPersonMotor>();
            KeyboardFirstPersonInput nextInput =
                next.GetComponent<KeyboardFirstPersonInput>();
            PlayerInteractionRaycaster nextRaycaster =
                next.GetComponent<PlayerInteractionRaycaster>();
            PlayerInventory nextInventory = next.GetComponent<PlayerInventory>();
            Rigidbody nextRigidbody = next.GetComponent<Rigidbody>();

            GameplayRuntimeRoot runtimeRoot = FindComponent<GameplayRuntimeRoot>(scene);
            SetObjectReference(runtimeRoot, "localPlayerInventory", nextInventory);

            PlayerPossessionController possession =
                FindComponent<PlayerPossessionController>(scene);
            SetObjectReference(possession, "explorerRoot", next);
            SetObjectReference(possession, "explorerRigidbody", nextRigidbody);
            SetObjectReference(possession, "explorerMotor", nextMotor);
            SetObjectReference(possession, "explorerInput", nextInput);
            SetObjectReference(
                possession,
                "explorerInteractionRaycaster",
                nextRaycaster);
            FirstPersonCameraRig cameraRig =
                FindComponent<FirstPersonCameraRig>(scene);
            SetObjectReference(cameraRig, "target", nextMotor);
            SetObjectReference(cameraRig, "inputSource", nextInput);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                MonoBehaviour[] behaviours =
                    root.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    ReplaceReference(
                        behaviours[i],
                        "interactionRaycaster",
                        previous != null
                            ? previous.GetComponent<PlayerInteractionRaycaster>()
                            : null,
                        nextRaycaster);
                }
            }
        }

        static void InstallSceneContext(Scene scene, GameObject offlineExplorer)
        {
            GameObject root = FindNamedObject(scene, "Multiplayer");
            if (root == null)
            {
                root = new GameObject("Multiplayer");
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            GameObject actorsRoot = FindNamedObject(scene, "Actors");
            if (actorsRoot == null)
            {
                throw new InvalidOperationException(
                    "SC_Expedition requires the authored Actors root.");
            }

            root.transform.SetParent(actorsRoot.transform, true);

            MultiplayerSceneContext context =
                root.GetComponent<MultiplayerSceneContext>() ??
                root.AddComponent<MultiplayerSceneContext>();
            GameplayRuntimeRoot runtimeRoot = FindComponent<GameplayRuntimeRoot>(scene);
            PlayerPossessionController possession =
                FindComponent<PlayerPossessionController>(scene);
            GravitySimulation gravity = FindComponent<GravitySimulation>(scene);
            CelestialFrameProvider frameProvider =
                FindComponent<CelestialFrameProvider>(scene);
            WorldOriginRebaser origin = FindComponent<WorldOriginRebaser>(scene);
            FirstPersonCameraRig cameraRig =
                FindComponent<FirstPersonCameraRig>(scene);
            SpacecraftCameraRig spacecraftCameraRig =
                FindComponent<SpacecraftCameraRig>(scene);
            PlayerControlLock controlLock =
                FindComponent<PlayerControlLock>(scene);
            SpacecraftMotor spacecraftMotor = FindComponent<SpacecraftMotor>(scene);
            KeyboardSpacecraftInput spacecraftInput =
                FindComponent<KeyboardSpacecraftInput>(scene);
            Rigidbody spacecraftRigidbody = spacecraftMotor != null
                ? spacecraftMotor.GetComponent<Rigidbody>()
                : null;

            Transform[] points = new Transform[4];
            Vector3 basePosition = offlineExplorer != null
                ? offlineExplorer.transform.position
                : Vector3.zero;
            Vector3 right = offlineExplorer != null
                ? offlineExplorer.transform.right
                : Vector3.right;
            Vector3 forward = offlineExplorer != null
                ? offlineExplorer.transform.forward
                : Vector3.forward;
            Vector3[] offsets =
            {
                right * -2f + forward * -2f,
                right * 2f + forward * -2f,
                right * -2f + forward * 2f,
                right * 2f + forward * 2f
            };
            for (int i = 0; i < points.Length; i++)
            {
                Transform point = root.transform.Find($"Spawn_{i + 1}");
                if (point == null)
                {
                    point = new GameObject($"Spawn_{i + 1}").transform;
                    point.SetParent(root.transform);
                }

                point.SetPositionAndRotation(
                    basePosition + offsets[i],
                    offlineExplorer != null
                        ? offlineExplorer.transform.rotation
                        : Quaternion.identity);
                points[i] = point;
            }

            SetObjectReference(context, "runtimeRoot", runtimeRoot);
            SetObjectReference(context, "offlineExplorer", offlineExplorer);
            SetObjectReference(context, "possession", possession);
            SetObjectReference(context, "spacecraftMotor", spacecraftMotor);
            SetObjectReference(context, "spacecraftInput", spacecraftInput);
            SetObjectReference(context, "spacecraftRigidbody", spacecraftRigidbody);
            SetObjectReference(context, "gravitySimulation", gravity);
            SetObjectReference(context, "celestialFrameProvider", frameProvider);
            SetObjectReference(context, "originRebaser", origin);
            SetObjectReferenceArray(
                context,
                "resourceStreamers",
                Array.ConvertAll(
                    FindComponents<ResourceDepositRuntimeSpawner>(scene),
                    item => (Object)item));
            SetObjectReferenceArray(
                context,
                "surfacePatchSystems",
                Array.ConvertAll(
                    FindComponents<CelestialSurfacePatchSystem>(scene),
                    item => (Object)item));
            SetObjectReference(
                context,
                "spacecraftCameraRig",
                spacecraftCameraRig);
            SetObjectReference(context, "firstPersonCameraRig", cameraRig);
            SetObjectReference(context, "controlLock", controlLock);
            SetObjectReference(
                context,
                "viewReference",
                cameraRig != null ? cameraRig.transform : null);
            SetObjectReferenceArray(
                context,
                "spawnPoints",
                Array.ConvertAll(points, item => (Object)item));
        }

        static GameObject CreateSessionPlayerPrefab()
        {
            GameObject root = new("PF_NetworkSessionPlayer");
            NetworkObject networkObject = root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkSessionPlayer>();
            SetBool(networkObject, "_isGlobal", true);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                root,
                SessionPlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject CreateNetworkStarterShipPrefab()
        {
            GameObject source =
                AssetDatabase.LoadAssetAtPath<GameObject>(StarterShuttleSourcePath);
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"Starter shuttle source is missing at {StarterShuttleSourcePath}.");
            }

            GameObject instance =
                (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = "PF_StarterShuttleNetwork";
            if (instance.GetComponent<NetworkObject>() == null)
            {
                instance.AddComponent<NetworkObject>();
            }

            if (instance.GetComponent<NetworkStarterShip>() == null)
            {
                instance.AddComponent<NetworkStarterShip>();
            }

            Rigidbody body = instance.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.None;
            }

            KeyboardSpacecraftInput input =
                instance.GetComponent<KeyboardSpacecraftInput>();
            if (input != null)
            {
                input.enabled = false;
            }

            SpacecraftMotor motor = instance.GetComponent<SpacecraftMotor>();
            if (motor != null)
            {
                motor.enabled = false;
            }

            instance.GetComponent<PersistentObjectId>()
                ?.SetId("ship.network_template");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                instance,
                NetworkStarterShuttlePath);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        static void RegisterNetworkPrefabs(GameObject starterShipPrefab)
        {
            DefaultPrefabObjects registry =
                AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(
                    DefaultPrefabObjectsPath);
            GameObject sessionPlayer =
                AssetDatabase.LoadAssetAtPath<GameObject>(SessionPlayerPrefabPath);
            GameObject networkPlayer =
                AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPrefabPath);
            if (registry == null || sessionPlayer == null || networkPlayer == null)
            {
                throw new InvalidOperationException(
                    "Farion network prefab registry foundation is incomplete.");
            }

            registry.Clear();
            registry.AddObjects(
                new[]
                {
                    sessionPlayer.GetComponent<NetworkObject>(),
                    networkPlayer.GetComponent<NetworkObject>(),
                    starterShipPrefab.GetComponent<NetworkObject>()
                },
                checkForDuplicates: true,
                initializeAdded: false);
            EditorUtility.SetDirty(registry);
        }

        static void InstallStarterShipReference(GameObject starterShipPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(SessionPrefabPath);
            try
            {
                SetObjectReference(
                    root.GetComponent<NetworkPlayerSpawner>(),
                    "starterShipPrefab",
                    starterShipPrefab.GetComponent<NetworkObject>());
                PrefabUtility.SaveAsPrefabAsset(root, SessionPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void InstallStarterShipSceneReferences()
        {
            Scene scene = SceneManager.GetSceneByPath(WorldZoneScenePath);
            bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (!alreadyLoaded)
            {
                scene = EditorSceneManager.OpenScene(
                    WorldZoneScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                MultiplayerSceneContext context =
                    FindComponent<MultiplayerSceneContext>(scene);
                SimulationZoneContext zone =
                    FindComponent<SimulationZoneContext>(scene);
                GameObject formations =
                    FindNamedObject(scene, "StarterShipFormations");
                if (context == null || zone == null || formations == null)
                {
                    throw new InvalidOperationException(
                        "SC_WorldZone starter ship hierarchy is incomplete.");
                }

                Object[] formationRoots = new Object[4];
                for (int i = 0; i < formationRoots.Length; i++)
                {
                    Transform formation =
                        formations.transform.Find($"Count_{i + 1}");
                    if (formation == null)
                    {
                        throw new InvalidOperationException(
                            $"Starter ship formation Count_{i + 1} is missing.");
                    }

                    formationRoots[i] = formation;
                }

                SetObjectReference(
                    context,
                    "simulationZoneContext",
                    zone);
                SetObjectReferenceArray(
                    context,
                    "starterShipFormations",
                    formationRoots);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (!alreadyLoaded && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        static void InstallSessionPlayerReference(GameObject sessionPlayerPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(SessionPrefabPath);
            try
            {
                NetworkPlayerSpawner spawner =
                    root.GetComponent<NetworkPlayerSpawner>();
                SetObjectReference(
                    spawner,
                    "sessionPlayerPrefab",
                    sessionPlayerPrefab.GetComponent<NetworkObject>());
                PrefabUtility.SaveAsPrefabAsset(root, SessionPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void CreateSessionPrefab(
            GameObject networkPlayerPrefab,
            GameObject sessionPlayerPrefab)
        {
            GameObject root = new("PF_NetworkSessionRoot");
            NetworkManager networkManager = root.AddComponent<NetworkManager>();
            TimeManager timeManager = GetOrAdd<TimeManager>(root);
            GetOrAdd<FishNetSceneManager>(root);
            Tugboat tugboat = GetOrAdd<Tugboat>(root);
            MultiplayerSessionController controller =
                root.AddComponent<MultiplayerSessionController>();
            NetworkPlayerSpawner spawner = root.AddComponent<NetworkPlayerSpawner>();
            NetworkWorldOriginAuthority origin =
                root.AddComponent<NetworkWorldOriginAuthority>();
            ObserverManager observerManager = GetOrAdd<ObserverManager>(root);
            ZonePhysicsTickDriver physicsTickDriver =
                root.AddComponent<ZonePhysicsTickDriver>();
            NetworkZoneCoordinator zoneCoordinator =
                root.AddComponent<NetworkZoneCoordinator>();

            networkManager.SpawnablePrefabs =
                AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(
                    "Assets/DefaultPrefabObjects.asset");
            SetBool(networkManager, "_dontDestroyOnLoad", true);
            SetInt(timeManager, "_tickRate", 100);
            SetInt(timeManager, "_physicsMode", (int)PhysicsMode.TimeManager);
            SetInt(tugboat, "_maximumClients", 4);
            SetObjectReference(controller, "networkManager", networkManager);
            SetObjectReference(controller, "playerSpawner", spawner);
            SetObjectReference(controller, "worldOriginAuthority", origin);
            SetObjectReference(controller, "zoneCoordinator", zoneCoordinator);
            SetObjectReference(spawner, "networkManager", networkManager);
            SetObjectReference(
                spawner,
                "sessionPlayerPrefab",
                sessionPlayerPrefab.GetComponent<NetworkObject>());
            SetObjectReference(
                spawner,
                "playerPrefab",
                networkPlayerPrefab.GetComponent<NetworkObject>());
            SetObjectReference(origin, "networkManager", networkManager);
            SetObjectReference(physicsTickDriver, "networkManager", networkManager);
            SetObjectReference(zoneCoordinator, "networkManager", networkManager);
            SetObjectReference(
                zoneCoordinator,
                "physicsTickDriver",
                physicsTickDriver);
            SetObjectReferenceArray(
                observerManager,
                "_defaultConditions",
                new Object[]
                {
                    AssetDatabase.LoadAssetAtPath<ObserverCondition>(
                        SceneConditionPath)
                });

            PrefabUtility.SaveAsPrefabAsset(root, SessionPrefabPath);
            Object.DestroyImmediate(root);
        }

        static void CreateWorldZoneScene()
        {
            Scene scene;
            bool alreadyLoaded = false;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldZoneScenePath) != null)
            {
                scene = SceneManager.GetSceneByPath(WorldZoneScenePath);
                alreadyLoaded = scene.IsValid() && scene.isLoaded;
                if (!alreadyLoaded)
                {
                    scene = EditorSceneManager.OpenScene(
                        WorldZoneScenePath,
                        OpenSceneMode.Additive);
                }
            }
            else
            {
                scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive);
            }

            if (FindComponent<SimulationZoneContext>(scene) == null)
            {
                GameObject zoneRoot = new("WorldZone");
                SceneManager.MoveGameObjectToScene(zoneRoot, scene);
                zoneRoot.AddComponent<SimulationZoneContext>();
                EditorSceneManager.MarkSceneDirty(scene);
            }

            EditorSceneManager.SaveScene(scene, WorldZoneScenePath);
            if (!alreadyLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void InstallZoneSessionComponents()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(SessionPrefabPath);
            try
            {
                NetworkManager networkManager = GetOrAdd<NetworkManager>(root);
                MultiplayerSessionController controller =
                    GetOrAdd<MultiplayerSessionController>(root);
                ObserverManager observerManager = GetOrAdd<ObserverManager>(root);
                ZonePhysicsTickDriver physicsTickDriver =
                    GetOrAdd<ZonePhysicsTickDriver>(root);
                NetworkZoneCoordinator zoneCoordinator =
                    GetOrAdd<NetworkZoneCoordinator>(root);
                ObserverCondition sceneCondition =
                    AssetDatabase.LoadAssetAtPath<ObserverCondition>(
                        SceneConditionPath);
                if (sceneCondition == null)
                {
                    throw new InvalidOperationException(
                        $"FishNet scene observer condition is missing at {SceneConditionPath}.");
                }

                SetObjectReference(
                    physicsTickDriver,
                    "networkManager",
                    networkManager);
                SetObjectReference(
                    zoneCoordinator,
                    "networkManager",
                    networkManager);
                SetObjectReference(
                    zoneCoordinator,
                    "physicsTickDriver",
                    physicsTickDriver);
                SetObjectReference(
                    controller,
                    "zoneCoordinator",
                    zoneCoordinator);
                SetObjectReferenceArray(
                    observerManager,
                    "_defaultConditions",
                    new Object[] { sceneCondition });
                PrefabUtility.SaveAsPrefabAsset(root, SessionPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void EnsureBuildScene(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes =
                new(EditorBuildSettings.scenes);
            if (scenes.Exists(scene => scene.path == scenePath))
            {
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void InstallMainMenuEntryPoints()
        {
            Scene scene = SceneManager.GetSceneByPath(MainMenuScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(
                    MainMenuScenePath,
                    OpenSceneMode.Additive);
            }

            MainMenuController controller = FindComponent<MainMenuController>(scene);
            GameObject buttonList = FindNamedObject(scene, "ButtonList");
            GameObject buttonPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(MenuButtonPrefabPath);
            if (controller == null || buttonList == null || buttonPrefab == null)
            {
                throw new InvalidOperationException(
                    "Main menu multiplayer entry points could not be authored.");
            }

            MultiplayerMainMenuBridge bridge =
                GetOrAdd<MultiplayerMainMenuBridge>(controller.gameObject);
            SetObjectReference(bridge, "mainMenu", controller);

            EnsureMainMenuButton(
                scene,
                buttonList.transform,
                buttonPrefab,
                controller,
                "HostGameButton",
                MainMenuAction.HostGame,
                "Host game",
                "Start a local co-op expedition.",
                3);
            EnsureMainMenuButton(
                scene,
                buttonList.transform,
                buttonPrefab,
                controller,
                "JoinLocalhostButton",
                MainMenuAction.JoinLocalhost,
                "Join localhost",
                "Connect to a host on this computer.",
                4);

            RectTransform listRect = buttonList.GetComponent<RectTransform>();
            if (listRect != null)
            {
                Vector2 size = listRect.sizeDelta;
                size.y = 520f;
                listRect.sizeDelta = size;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void EnsureMainMenuButton(
            Scene scene,
            Transform parent,
            GameObject prefab,
            MainMenuController controller,
            string objectName,
            MainMenuAction action,
            string title,
            string subtitle,
            int siblingIndex)
        {
            GameObject buttonObject = FindNamedObject(scene, objectName);
            if (buttonObject == null)
            {
                buttonObject = (GameObject)PrefabUtility.InstantiatePrefab(
                    prefab,
                    parent);
                buttonObject.name = objectName;
            }

            buttonObject.transform.SetSiblingIndex(siblingIndex);
            MainMenuButton button = GetOrAdd<MainMenuButton>(buttonObject);
            SetInt(button, "action", (int)action);
            SetObjectReference(button, "controller", controller);
            button.ConfigureContent(title, subtitle, null);
        }

        static Scene GetOrOpenScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            return scene.IsValid() && scene.isLoaded
                ? scene
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            return gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();
        }

        static void CopyComponent(GameObject source, GameObject destination, Type type)
        {
            Component sourceComponent = source.GetComponent(type);
            Component destinationComponent = destination.GetComponent(type);
            if (sourceComponent != null && destinationComponent != null)
            {
                EditorUtility.CopySerialized(sourceComponent, destinationComponent);
            }
        }

        static GameObject FindNamedObject(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].name == objectName)
                    {
                        return transforms[i].gameObject;
                    }
                }
            }

            return null;
        }

        static T FindComponent<T>(Scene scene) where T : Component
        {
            T[] components = FindComponents<T>(scene);
            return components.Length > 0 ? components[0] : null;
        }

        static T[] FindComponents<T>(Scene scene) where T : Component
        {
            List<T> components = new();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                components.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return components.ToArray();
        }

        static void SetObjectReference(
            Object target,
            string propertyName,
            Object value)
        {
            if (target == null)
            {
                return;
            }

            SerializedObject serialized = new(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name}.{propertyName} was not found.");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetObjectReferenceArray(
            Object target,
            string propertyName,
            Object[] values)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetBool(Object target, string propertyName, bool value)
        {
            SerializedObject serialized = new(target);
            serialized.FindProperty(propertyName).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetInt(Object target, string propertyName, int value)
        {
            SerializedObject serialized = new(target);
            serialized.FindProperty(propertyName).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ReplaceReference(
            Object target,
            string propertyName,
            Object previous,
            Object next)
        {
            if (target == null)
            {
                return;
            }

            SerializedObject serialized = new(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null ||
                property.propertyType != SerializedPropertyType.ObjectReference ||
                property.objectReferenceValue != previous)
            {
                return;
            }

            property.objectReferenceValue = next;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
