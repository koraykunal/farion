using System.Collections;
using Farion.App.Flow;
using Farion.Gameplay.Character;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Input;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Presentation.Flight;
using Farion.Gameplay.Session;
using Farion.Rendering.Celestial;
using Farion.Rendering.Lighting;
using Farion.Simulation.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.UI.Gameplay
{
    [DefaultExecutionOrder(-1100)]
    [DisallowMultipleComponent]
    public sealed class UiGameplaySceneShellController : MonoBehaviour
    {
        const string DefaultWorldScene = "SC_WorldZone";

        [SerializeField] string worldSceneName = DefaultWorldScene;
        [SerializeField] SpacecraftCameraRig spacecraftCameraRig;
        [SerializeField] FirstPersonCameraRig firstPersonCameraRig;
        [SerializeField] Camera gameplayCamera;
        [SerializeField] PlayerControlLock controlLock;
        [SerializeField] UiGameplayController gameplayUi;
        [SerializeField] UiSpacecraftFlightHudPresenter flightHud;
        [SerializeField] SpacecraftPostProcessRig postProcessRig;
        [SerializeField] CelestialLightingRig lightingRig;
        [SerializeField] CelestialLodController lodController;
        [SerializeField] CelestialOrbitLineRenderer orbitLines;

        WorldOriginRebaser boundOriginRebaser;

        public SpacecraftCameraRig SpacecraftCameraRig => spacecraftCameraRig;
        public FirstPersonCameraRig FirstPersonCameraRig => firstPersonCameraRig;
        public Camera Camera => gameplayCamera;
        public Transform ViewReference => gameplayCamera != null
            ? gameplayCamera.transform
            : null;
        public PlayerControlLock ControlLock => controlLock;
        public UiGameplayController GameplayUi => gameplayUi;
        public UiSpacecraftFlightHudPresenter FlightHud => flightHud;
        public SpacecraftPostProcessRig PostProcessRig => postProcessRig;
        public CelestialLightingRig LightingRig => lightingRig;
        public CelestialLodController LodController => lodController;
        public CelestialOrbitLineRenderer OrbitLines => orbitLines;
        public bool IsValid
        {
            get
            {
                ResolvePresentation();
                return spacecraftCameraRig != null &&
                    firstPersonCameraRig != null &&
                    gameplayCamera != null &&
                    controlLock != null &&
                    gameplayUi != null &&
                    flightHud != null &&
                    postProcessRig != null &&
                    lightingRig != null &&
                    lodController != null;
            }
        }

        void Awake()
        {
            ResolvePresentation();
        }

        void OnDestroy()
        {
            BindWorldOrigin(null);
        }

        IEnumerator Start()
        {
            if (GameplaySessionModeRequest.RequestedOrDefault ==
                GameplaySessionMode.Multiplayer)
            {
                yield break;
            }

            Scene world = SceneManager.GetSceneByName(worldSceneName);
            if (!world.isLoaded)
            {
                yield return SceneManager.LoadSceneAsync(
                    worldSceneName,
                    LoadSceneMode.Additive);
                world = SceneManager.GetSceneByName(worldSceneName);
            }

            if (!world.isLoaded || !BindOfflineWorld(world))
            {
                Debug.LogError(
                    $"Gameplay shell could not bind world scene '{worldSceneName}'.",
                    this);
                yield break;
            }

            SceneManager.SetActiveScene(world);
        }

        bool BindOfflineWorld(Scene world)
        {
            GameplayRuntimeRoot runtimeRoot = FindInScene<GameplayRuntimeRoot>(world);
            GameplaySessionController sessionController =
                FindInScene<GameplaySessionController>(world);
            PlayerPossessionController possession =
                runtimeRoot != null ? runtimeRoot.Bindings?.Possession : null;
            if (!IsValid || runtimeRoot == null || sessionController == null || possession == null)
            {
                Debug.LogError(
                    $"Gameplay shell bindings are incomplete: presentation={IsValid}, " +
                    $"runtime={runtimeRoot != null}, session={sessionController != null}, " +
                    $"possession={possession != null}, spacecraftCamera={spacecraftCameraRig != null}, " +
                    $"firstPersonCamera={firstPersonCameraRig != null}, camera={gameplayCamera != null}, " +
                    $"controlLock={controlLock != null}, ui={gameplayUi != null}, " +
                    $"hud={flightHud != null}, postProcess={postProcessRig != null}, " +
                    $"lighting={lightingRig != null}, lod={lodController != null}.",
                    this);
                return false;
            }

            possession.BindPresentation(
                spacecraftCameraRig,
                firstPersonCameraRig,
                controlLock);
            gameplayUi.BindSession(
                sessionController,
                possession.ExplorerInteractionRaycaster);
            flightHud.SetPilotContext(possession);
            postProcessRig.SetMotor(possession.SpacecraftMotor);
            lightingRig.SetPrimarySource(FindInScene<CelestialLightSource>(world));
            orbitLines?.SetSimulation(runtimeRoot.Bindings.GravitySimulation);
            runtimeRoot.Bindings.GravitySimulation.SetPhysicsReferenceObserverSource(possession);
            BindWorldOrigin(runtimeRoot.Bindings.OriginRebaser);

            foreach (CelestialSurfacePatchSystem patch in
                     FindAllInScene<CelestialSurfacePatchSystem>(world))
            {
                patch.SetCamera(gameplayCamera);
                patch.SetCollisionObserverSource(possession);
            }

            lodController.SetCamera(gameplayCamera);
            foreach (CelestialBodyVisual visual in
                     FindAllInScene<CelestialBodyVisual>(world))
            {
                lodController.RegisterVisual(visual);
            }

            lodController.ApplyLods();
            return true;
        }

        public void BindWorldOrigin(WorldOriginRebaser rebaser)
        {
            if (boundOriginRebaser == rebaser)
            {
                return;
            }

            if (boundOriginRebaser != null)
            {
                boundOriginRebaser.Rebased -= OnWorldOriginRebased;
                boundOriginRebaser.UnregisterShiftRoot(spacecraftCameraRig?.transform);
                boundOriginRebaser.UnregisterShiftRoot(firstPersonCameraRig?.transform);
            }

            boundOriginRebaser = rebaser;
            if (boundOriginRebaser == null)
            {
                return;
            }

            boundOriginRebaser.RegisterShiftRoot(spacecraftCameraRig?.transform);
            boundOriginRebaser.RegisterShiftRoot(firstPersonCameraRig?.transform);
            boundOriginRebaser.Rebased += OnWorldOriginRebased;
        }

        void OnWorldOriginRebased(Vector3 _)
        {
            spacecraftCameraRig?.SnapToTarget();
            firstPersonCameraRig?.SnapToTarget();
            postProcessRig?.ResetCameraHistory(gameplayCamera);
        }

        void ResolvePresentation()
        {
            Scene scene = gameObject.scene;
            spacecraftCameraRig ??= FindInScene<SpacecraftCameraRig>(scene);
            firstPersonCameraRig ??= FindInScene<FirstPersonCameraRig>(scene);
            gameplayCamera ??= FindInScene<Camera>(scene);
            controlLock ??= FindInScene<PlayerControlLock>(scene);
            gameplayUi ??= FindInScene<UiGameplayController>(scene);
            flightHud ??= FindInScene<UiSpacecraftFlightHudPresenter>(scene);
            postProcessRig ??= FindInScene<SpacecraftPostProcessRig>(scene);
            lightingRig ??= FindInScene<CelestialLightingRig>(scene);
            lodController ??= FindInScene<CelestialLodController>(scene);
            orbitLines ??= FindInScene<CelestialOrbitLineRenderer>(scene);
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        static T[] FindAllInScene<T>(Scene scene) where T : Component
        {
            var results = new System.Collections.Generic.List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                results.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return results.ToArray();
        }
    }
}
