using Farion.Gameplay.Character;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Input;
using Farion.Gameplay.Presentation.Flight;
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
        [SerializeField] SpacecraftCameraRig spacecraftCameraRig;
        [SerializeField] ExplorerCameraRig explorerCameraRig;
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
        public ExplorerCameraRig ExplorerCameraRig => explorerCameraRig;
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
                    explorerCameraRig != null &&
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

        public void BindWorldOrigin(WorldOriginRebaser rebaser)
        {
            if (boundOriginRebaser == rebaser)
            {
                return;
            }

            if (boundOriginRebaser != null)
            {
                boundOriginRebaser.Rebased -= OnWorldOriginRebased;
                boundOriginRebaser.UnregisterShiftRoot(ShiftRootOf(spacecraftCameraRig));
                boundOriginRebaser.UnregisterShiftRoot(ShiftRootOf(explorerCameraRig));
            }

            boundOriginRebaser = rebaser;
            if (boundOriginRebaser == null)
            {
                return;
            }

            boundOriginRebaser.RegisterShiftRoot(ShiftRootOf(spacecraftCameraRig));
            boundOriginRebaser.RegisterShiftRoot(ShiftRootOf(explorerCameraRig));
            boundOriginRebaser.Rebased += OnWorldOriginRebased;
        }

        void OnWorldOriginRebased(Vector3 originOffset)
        {
            if (spacecraftCameraRig != null)
            {
                spacecraftCameraRig.NotifyOriginShift(originOffset);
            }

            if (postProcessRig != null)
            {
                postProcessRig.ResetCameraHistory(gameplayCamera);
            }
        }

        void ResolvePresentation()
        {
            Scene scene = gameObject.scene;
            ResolveInScene(ref spacecraftCameraRig, scene);
            ResolveInScene(ref explorerCameraRig, scene);
            ResolveInScene(ref gameplayCamera, scene);
            ResolveInScene(ref controlLock, scene);
            ResolveInScene(ref gameplayUi, scene);
            ResolveInScene(ref flightHud, scene);
            ResolveInScene(ref postProcessRig, scene);
            ResolveInScene(ref lightingRig, scene);
            ResolveInScene(ref lodController, scene);
            ResolveInScene(ref orbitLines, scene);
        }

        static Transform ShiftRootOf(Component component)
        {
            return component != null ? component.transform : null;
        }

        static void ResolveInScene<T>(ref T component, Scene scene) where T : Component
        {
            if (component == null)
            {
                component = FindInScene<T>(scene);
            }
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
    }
}
