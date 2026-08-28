using Farion.Core.Identity;
using System.Collections.Generic;
using Farion.Gameplay.Actors;
using Farion.Gameplay.Character;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Input;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Presentation.Flight;
using Farion.Gameplay.ResourceNodes;
using Farion.Gameplay.Session;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Spawning;
using Farion.Multiplayer.World;
using Farion.Rendering.Celestial;
using Farion.Rendering.Lighting;
using Farion.Rendering.Space;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.World;
using Farion.UI.Gameplay;
using FishNet.Component.Transforming.Beta;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Farion.Multiplayer.Spacecraft;

namespace Farion.Multiplayer.Session
{
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class MultiplayerSceneContext : MonoBehaviour, ILocalPilotContext
    {
        const int DryLandSampleCount = 128;
        const float GoldenAngleRadians = 2.39996323f;

        [Header("Mode")]
        [SerializeField] GameplayRuntimeRoot runtimeRoot;

        [Header("Offline Owners")]
        [SerializeField] GameObject offlineExplorer;
        [SerializeField] PlayerPossessionController possession;
        [SerializeField] SpacecraftMotor spacecraftMotor;
        [SerializeField] KeyboardSpacecraftInput spacecraftInput;
        [SerializeField] Rigidbody spacecraftRigidbody;

        [Header("Shared Simulation")]
        [SerializeField] GravitySimulation gravitySimulation;
        [SerializeField] CelestialFrameProvider celestialFrameProvider;
        [SerializeField] WorldOriginRebaser originRebaser;
        [SerializeField] SimulationZoneContext simulationZoneContext;
        [SerializeField] List<ResourceDepositRuntimeSpawner> resourceStreamers = new();
        [SerializeField] List<CelestialSurfacePatchSystem> surfacePatchSystems = new();
        [SerializeField] List<SurfaceFormationSpawner> formationSpawners = new();

        [Header("Local Presentation")]
        [SerializeField] SpacecraftCameraRig spacecraftCameraRig;
        [SerializeField] FirstPersonCameraRig firstPersonCameraRig;
        [SerializeField] Transform viewReference;
        [SerializeField] PlayerControlLock controlLock;
        [SerializeField] SpacecraftPilotCameraView pilotCameraView =
            SpacecraftPilotCameraView.Exterior;

        [Header("Spawn")]
        [SerializeField] Transform[] spawnPoints = new Transform[4];
        [SerializeField] Transform starterShuttleFormation;
        [Min(0f)]
        [SerializeField] float surfaceClearance = 1.02f;

        MultiplayerPlayerSpawner playerSpawner;
        MultiplayerWorldOriginAuthority originAuthority;
        ZoneOriginState zoneOrigin;
        CelestialLodController lodController;
        NetworkExplorerController ownedPlayer;
        UiGameplayController gameplayUi;
        UiSpacecraftFlightHudPresenter flightHud;
        SpacecraftPostProcessRig postProcessRig;
        MultiplayerSurfaceCollisionObserverSource surfaceCollisionObservers;
        bool multiplayerConfigured;
        bool presentationSuppressed;
        NetworkStarterShuttle ownedSpacecraft;

        public static MultiplayerSceneContext Active { get; private set; }

        public event Action<PlayerPossessionMode> ModeChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetActive()
        {
            Active = null;
        }

        public PlayerPossessionMode CurrentMode => ownedSpacecraft != null
            ? PlayerPossessionMode.Spacecraft
            : NetworkSessionPlayer.Local != null
                ? NetworkSessionPlayer.Local.PossessionMode
                : PlayerPossessionMode.OnFoot;
        public SpacecraftPilotCameraView CurrentPilotCameraView => pilotCameraView;
        public SpacecraftMotor PilotedSpacecraftMotor =>
            ownedSpacecraft != null ? ownedSpacecraft.Motor : null;

        public CelestialFrameProvider CelestialFrameProvider =>
            celestialFrameProvider;
        public GravitySimulation GravitySimulation => gravitySimulation;
        public GameplayRuntimeRoot RuntimeRoot => runtimeRoot;
        public ZoneOriginState ZoneOrigin => zoneOrigin;
        public GeneratedEntityId ZoneId => simulationZoneContext != null
            ? simulationZoneContext.ZoneId
            : GeneratedEntityId.None;

        public void RegisterFormationObserver(Transform observer)
        {
            for (int i = 0; i < formationSpawners.Count; i++)
            {
                formationSpawners[i]?.AddObserver(observer);
            }
        }

        public void UnregisterFormationObserver(Transform observer)
        {
            for (int i = 0; i < formationSpawners.Count; i++)
            {
                formationSpawners[i]?.RemoveObserver(observer);
            }
        }

        public void ApplyNetworkSimulationTime(double seconds)
        {
            gravitySimulation?.SetSimulationTime(seconds);
        }

        public void AttachToShiftedWorld(Transform target)
        {
            if (target != null)
            {
                Transform shiftedWorldRoot = transform.parent;
                target.SetParent(
                    shiftedWorldRoot != null ? shiftedWorldRoot : transform,
                    true);
            }
        }

        void Awake()
        {
            if (!IsMultiplayerMode)
            {
                return;
            }

            ConfigureMultiplayer();
        }

        void Start()
        {
            if (IsMultiplayerMode)
            {
                ApplyLocalPresentationMode();
            }
        }

        bool IsMultiplayerMode =>
            runtimeRoot == null ||
            runtimeRoot.Mode == GameplaySessionMode.Multiplayer;

        void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
            }

            if (zoneOrigin != null && zoneOrigin.Rebaser == originRebaser)
            {
                zoneOrigin.Unbind();
            }

            BindOriginRebaser(null);
            if (flightHud != null)
            {
                flightHud.SetPilotContext(null);
            }

            if (postProcessRig != null)
            {
                postProcessRig.SetMotor(null);
            }
        }

        public void BindSession(
            MultiplayerPlayerSpawner spawner,
            MultiplayerWorldOriginAuthority worldOriginAuthority,
            GeneratedEntityId zoneId)
        {
            ConfigureMultiplayer();
            playerSpawner = spawner;
            originAuthority = worldOriginAuthority;
            BindOriginRebaser(originRebaser);
            zoneOrigin = originAuthority != null
                ? originAuthority.BindZone(
                    zoneId,
                    gameObject.scene,
                    originRebaser,
                    gravitySimulation,
                    surfaceCollisionObservers)
                : null;
            playerSpawner?.BindContext(this, originAuthority);
        }

        public void BindPresentation(UiGameplaySceneShellController bindings)
        {
            if (bindings == null)
            {
                return;
            }

            spacecraftCameraRig = bindings.SpacecraftCameraRig;
            firstPersonCameraRig = bindings.FirstPersonCameraRig;
            viewReference = bindings.ViewReference;
            controlLock = bindings.ControlLock;
            gameplayUi = bindings.GameplayUi;
            flightHud = bindings.FlightHud;
            postProcessRig = bindings.PostProcessRig;
            flightHud?.SetPilotContext(this);
            bindings.LightingRig?.SetPrimarySource(FindInScene<CelestialLightSource>());
            bindings.OrbitLines?.SetSimulation(gravitySimulation);
            bindings.BindWorldOrigin(originRebaser);
            for (int i = 0; i < surfacePatchSystems.Count; i++)
            {
                surfacePatchSystems[i]?.SetCamera(bindings.Camera);
            }

            if (bindings.LodController != null)
            {
                lodController = bindings.LodController;
                bindings.LodController.SetCamera(bindings.Camera);
                RegisterZoneVisuals(bindings.LodController);
            }
        }

        public void UnbindPresentation()
        {
            for (int i = 0; i < surfacePatchSystems.Count; i++)
            {
                surfacePatchSystems[i]?.SetCamera(null);
            }

            if (lodController != null)
            {
                UnregisterZoneVisuals(lodController);
                lodController = null;
            }

            flightHud?.SetPilotContext(null);
            postProcessRig?.SetMotor(null);
        }

        public void SetPresentationActive(bool active)
        {
            if (active != presentationSuppressed)
            {
                return;
            }

            presentationSuppressed = !active;
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                SetBehavioursEnabled<CelestialLightSource>(roots[i], active);
                SetBehavioursEnabled<NebulaVolume>(roots[i], active);
                SetBehavioursEnabled<CelestialScaledSpaceVisual>(roots[i], active);
                SetBehavioursEnabled<CelestialStarVisual>(roots[i], active);
                SetBehavioursEnabled<TerrestrialPlanetVisual>(roots[i], active);
                SetBehavioursEnabled<SurfaceDecorationRenderer>(roots[i], active);
            }
        }

        static void SetBehavioursEnabled<T>(GameObject root, bool enabled)
            where T : Behaviour
        {
            T[] behaviours = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                behaviours[i].enabled = enabled;
            }
        }

        public bool TryGetSpawnPose(
            int slot,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (spawnPoints == null ||
                slot < 0 ||
                slot >= spawnPoints.Length ||
                spawnPoints[slot] == null)
            {
                return false;
            }

            Transform point = spawnPoints[slot];
            return TryResolveFormationPose(
                ResolveSpawnGroupCenter(),
                point.parent != null ? point.parent.rotation : Quaternion.identity,
                point,
                surfaceClearance,
                out position,
                out rotation);
        }

        bool TryResolveFormationPose(
            Vector3 groupPosition,
            Quaternion groupRotation,
            Transform anchor,
            float clearance,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (!TryResolveDryTerrainPose(
                    groupPosition,
                    groupRotation * Vector3.forward,
                    clearance,
                    out CelestialSurfacePlacementResult groupPlacement))
            {
                return false;
            }

            Quaternion groupToPlacement =
                groupPlacement.Rotation * Quaternion.Inverse(groupRotation);
            position = groupPlacement.Position +
                groupToPlacement * (anchor.position - groupPosition);
            rotation = groupToPlacement * anchor.rotation;
            CelestialSurfacePlacementOptions options = new(
                clearance,
                clearance,
                CelestialSurfacePlacementMode.TerrainSurface);
            if (CelestialSurfacePlacement.TryResolvePose(
                    celestialFrameProvider,
                    position,
                    Vector3.zero,
                    rotation * Vector3.forward,
                    options,
                    out CelestialSurfacePlacementResult anchorPlacement) &&
                IsDryLand(anchorPlacement))
            {
                position = anchorPlacement.Position;
                rotation = anchorPlacement.Rotation;
            }

            return true;
        }

        bool TryResolveDryTerrainPose(
            Vector3 anchor,
            Vector3 forward,
            float clearance,
            out CelestialSurfacePlacementResult placement)
        {
            CelestialSurfacePlacementOptions options = new(
                clearance,
                clearance,
                CelestialSurfacePlacementMode.TerrainSurface);
            if (!CelestialSurfacePlacement.TryResolvePose(
                celestialFrameProvider,
                anchor,
                Vector3.zero,
                forward,
                options,
                out placement))
            {
                return false;
            }

            if (IsDryLand(placement))
            {
                return true;
            }

            CelestialBody body = placement.Frame.Body;
            Vector3 preferredUp = placement.Frame.RadialUp;
            float sampleDistance = Mathf.Max(
                body.Radius * 2f,
                placement.Frame.CenterDistance);
            bool found = false;
            float bestAlignment = -2f;
            for (int i = 0; i < DryLandSampleCount; i++)
            {
                Vector3 direction = body.transform.TransformDirection(
                    FibonacciSphereDirection(i, DryLandSampleCount));
                Vector3 candidatePosition =
                    body.Position + direction * sampleDistance;
                if (!CelestialSurfacePlacement.TryResolvePose(
                        celestialFrameProvider,
                        candidatePosition,
                        Vector3.zero,
                        forward,
                        options,
                        out CelestialSurfacePlacementResult candidate) ||
                    candidate.Frame.Body != body ||
                    !IsDryLand(candidate))
                {
                    continue;
                }

                float alignment = Vector3.Dot(
                    preferredUp,
                    candidate.PlacementUp);
                if (alignment > bestAlignment)
                {
                    placement = candidate;
                    bestAlignment = alignment;
                    found = true;
                }
            }

            return found;
        }

        public bool TryGetStarterShuttlePose(
            int slot,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            Transform anchor = starterShuttleFormation != null && slot >= 0
                ? starterShuttleFormation.Find($"Ship_{slot + 1}")
                : null;
            if (anchor == null)
            {
                return false;
            }

            return TryResolveFormationPose(
                starterShuttleFormation.position,
                starterShuttleFormation.rotation,
                anchor,
                0f,
                out position,
                out rotation);
        }

        bool IsDryLand(CelestialSurfacePlacementResult placement)
        {
            return !placement.Frame.HasOcean ||
                placement.TerrainRadius >=
                placement.Frame.Environment.OceanRadius + surfaceClearance;
        }

        Vector3 ResolveSpawnGroupCenter()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null)
                {
                    sum += spawnPoints[i].position;
                    count++;
                }
            }

            return count > 0 ? sum / count : Vector3.zero;
        }

        static Vector3 FibonacciSphereDirection(int index, int count)
        {
            float y = 1f - 2f * (index + 0.5f) / count;
            float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float angle = index * GoldenAngleRadians;
            return new Vector3(
                Mathf.Cos(angle) * radius,
                y,
                Mathf.Sin(angle) * radius);
        }

        public bool BindOwnedPlayer(NetworkExplorerController player)
        {
            if (player == null)
            {
                return false;
            }

            MultiplayerSessionController.Active?.NotifyLocalZone(this);
            if (firstPersonCameraRig == null ||
                viewReference == null ||
                controlLock == null)
            {
                return false;
            }

            Active = this;
            ownedPlayer = player;
            player.ApplyPossessionState(true);
            player.BindScene(celestialFrameProvider, zoneOrigin);
            player.Motor.SetViewReference(viewReference);
            player.Input.SetControlLock(controlLock);
            player.InteractionRaycaster?.SetViewReference(viewReference);
            player.InteractionRaycaster?.SetControlLock(controlLock);
            gameplayUi?.SetInteractionRaycaster(player.InteractionRaycaster);
            LocalPlayerCameraBinding.FollowExplorer(
                firstPersonCameraRig,
                spacecraftCameraRig,
                player.Motor,
                player.Input);
            LocalPlayerCameraBinding.SetCursorCaptured(true);
            SetTrackingTarget(player.transform);
            ModeChanged?.Invoke(CurrentMode);

            NetworkGameplayCommands commands = NetworkSessionPlayer.Local != null
                ? NetworkSessionPlayer.Local.GetComponent<NetworkGameplayCommands>()
                : null;
            commands?.BindOwnerExplorer(
                player,
                runtimeRoot != null ? runtimeRoot.Bindings : null);
            if (commands != null)
            {
                gameplayUi?.BindCommandEvents(commands);
            }

            return true;
        }

        public bool BindOwnedSpacecraft(NetworkStarterShuttle ship)
        {
            if (ship == null ||
                ownedPlayer == null ||
                spacecraftCameraRig == null ||
                firstPersonCameraRig == null)
            {
                return false;
            }

            ownedPlayer.ApplyPossessionState(false);
            ownedSpacecraft = ship;
            ApplyPilotCameraView();
            ship.Input?.SetControlLock(controlLock);
            ship.BoardingInput?.SetControlLock(controlLock);
            if (ship.Input != null)
            {
                ship.Input.enabled = true;
            }

            postProcessRig?.SetMotor(ship.Motor);
            LocalPilotContextBinding.Apply(ship.transform, this);
            SetTrackingTarget(ship.transform);
            ModeChanged?.Invoke(CurrentMode);

            return true;
        }

        public void TogglePilotCameraView()
        {
            SetPilotCameraView(pilotCameraView == SpacecraftPilotCameraView.Exterior
                ? SpacecraftPilotCameraView.Cockpit
                : SpacecraftPilotCameraView.Exterior);
        }

        public void SetPilotCameraView(SpacecraftPilotCameraView view)
        {
            pilotCameraView = view;
            if (ownedSpacecraft != null)
            {
                ApplyPilotCameraView();
            }
            else if (spacecraftCameraRig != null)
            {
                spacecraftCameraRig.SetView(pilotCameraView);
            }
        }

        void ApplyPilotCameraView()
        {
            LocalPlayerCameraBinding.FollowSpacecraft(
                firstPersonCameraRig,
                spacecraftCameraRig,
                ownedSpacecraft.Rig,
                ownedSpacecraft.Motor,
                ownedSpacecraft.transform,
                pilotCameraView);
        }

        public void RestoreOwnedExplorer(NetworkStarterShuttle ship)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (ship != null)
            {
                if (ship.Input != null)
                {
                    ship.Input.enabled = false;
                    ship.Input.SetControlLock(null);
                }

                ship.BoardingInput?.SetControlLock(null);

                LocalPilotContextBinding.Apply(ship.transform, null);
            }

            ownedSpacecraft = null;
            postProcessRig?.SetMotor(null);
            LocalPlayerCameraBinding.Release(null, spacecraftCameraRig);
            if (firstPersonCameraRig != null)
            {
                firstPersonCameraRig.enabled = true;
            }

            if (ownedPlayer != null)
            {
                BindOwnedPlayer(ownedPlayer);
            }

            ModeChanged?.Invoke(CurrentMode);
        }

        void SetTrackingTarget(Transform target)
        {
            WorldFocusTracking.Apply(originRebaser, resourceStreamers, target);
        }

        void BindOriginRebaser(WorldOriginRebaser rebaser)
        {
            if (originRebaser != null)
            {
                originRebaser.Rebased -= OnWorldOriginRebased;
            }

            originRebaser = rebaser;
            if (originRebaser != null)
            {
                originRebaser.Rebased += OnWorldOriginRebased;
            }
        }

        void OnWorldOriginRebased(Vector3 _)
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                NetworkTickSmoother[] smoothers =
                    roots[i].GetComponentsInChildren<NetworkTickSmoother>(true);
                for (int j = 0; j < smoothers.Length; j++)
                {
                    smoothers[j]?.SmootherController?.UniversalSmoother?.Teleport();
                }
            }
        }

        public void UnbindOwnedPlayer(NetworkExplorerController player)
        {
            if (ownedPlayer != player)
            {
                return;
            }

            if (Active == this)
            {
                Active = null;
            }

            ownedPlayer = null;
            player.Input.SetControlLock(null);
            player.InteractionRaycaster?.SetControlLock(null);
            gameplayUi?.SetInteractionRaycaster(null);
            LocalPlayerCameraBinding.Release(firstPersonCameraRig, null);
            LocalPlayerCameraBinding.SetCursorCaptured(false);
            ModeChanged?.Invoke(CurrentMode);
        }

        public static MultiplayerSceneContext FindIn(Scene scene)
        {
            if (!scene.IsValid())
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                MultiplayerSceneContext context =
                    roots[i].GetComponentInChildren<MultiplayerSceneContext>(true);
                if (context != null)
                {
                    return context;
                }
            }

            return null;
        }

        T FindInScene<T>() where T : Component
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        void RegisterZoneVisuals(CelestialLodController lod)
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                CelestialBodyVisual[] visuals =
                    roots[i].GetComponentsInChildren<CelestialBodyVisual>(true);
                for (int j = 0; j < visuals.Length; j++)
                {
                    lod.RegisterVisual(visuals[j]);
                }
            }

            lod.ApplyLods();
        }

        void UnregisterZoneVisuals(CelestialLodController lod)
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                CelestialBodyVisual[] visuals =
                    roots[i].GetComponentsInChildren<CelestialBodyVisual>(true);
                for (int j = 0; j < visuals.Length; j++)
                {
                    lod.UnregisterVisual(visuals[j]);
                }
            }
        }

        void ApplyLocalPresentationMode()
        {
            if (spacecraftMotor != null)
            {
                spacecraftMotor.enabled = false;
            }

            if (spacecraftInput != null)
            {
                spacecraftInput.enabled = false;
            }

            if (ownedSpacecraft == null)
            {
                LocalPlayerCameraBinding.Release(null, spacecraftCameraRig);
                if (firstPersonCameraRig != null)
                {
                    firstPersonCameraRig.enabled = true;
                }
            }
        }

        void ConfigureMultiplayer()
        {
            ApplyZoneAuthority();
            if (multiplayerConfigured)
            {
                return;
            }

            multiplayerConfigured = true;
            if (offlineExplorer != null)
            {
                offlineExplorer.SetActive(false);
            }

            if (possession != null)
            {
                possession.enabled = false;
            }

            if (spacecraftMotor != null)
            {
                spacecraftMotor.gameObject.SetActive(false);
            }

            ApplyLocalPresentationMode();

            if (spacecraftRigidbody != null &&
                !spacecraftRigidbody.isKinematic)
            {
                spacecraftRigidbody.linearVelocity = Vector3.zero;
                spacecraftRigidbody.angularVelocity = Vector3.zero;
                spacecraftRigidbody.isKinematic = true;
            }
        }

        void ApplyZoneAuthority()
        {
            gravitySimulation?.SetIntegrationEnabled(false);
            gravitySimulation?.SetExternalTimeSource(true);
            originRebaser?.SetAutomaticRebasing(false);
            surfaceCollisionObservers =
                GetComponent<MultiplayerSurfaceCollisionObserverSource>();
            if (surfaceCollisionObservers == null)
            {
                surfaceCollisionObservers =
                    gameObject.AddComponent<MultiplayerSurfaceCollisionObserverSource>();
            }

            gravitySimulation?.SetPhysicsReferenceObserverSource(null);

            for (int i = 0; i < surfacePatchSystems.Count; i++)
            {
                surfacePatchSystems[i]?.SetCollisionObserverSource(
                    surfaceCollisionObservers);
            }
        }
    }
}
