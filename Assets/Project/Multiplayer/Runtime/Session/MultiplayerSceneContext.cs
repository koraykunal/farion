using Farion.Core.Identity;
using System.Collections.Generic;
using Farion.Gameplay.Actors;
using Farion.Gameplay.Character;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Input;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Resources;
using Farion.Gameplay.Session;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Spawning;
using Farion.Multiplayer.World;
using Farion.Rendering.Celestial;
using Farion.Rendering.Lighting;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.World;
using Farion.UI.Gameplay;
using FishNet.Component.Transforming.Beta;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Multiplayer.Session
{
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class MultiplayerSceneContext : MonoBehaviour
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

        [Header("Local Presentation")]
        [SerializeField] SpacecraftCameraRig spacecraftCameraRig;
        [SerializeField] FirstPersonCameraRig firstPersonCameraRig;
        [SerializeField] Transform viewReference;
        [SerializeField] PlayerControlLock controlLock;

        [Header("Spawn")]
        [SerializeField] Transform[] spawnPoints = new Transform[4];
        [SerializeField] Transform[] starterShipFormations = new Transform[4];
        [Min(0f)]
        [SerializeField] float surfaceClearance = 1.02f;
        [Min(0f)]
        [SerializeField] float starterShipSurfaceClearance = 3f;

        NetworkPlayerSpawner playerSpawner;
        NetworkWorldOriginAuthority originAuthority;
        NetworkExplorerController ownedPlayer;
        GameplayUiController gameplayUi;
        bool multiplayerConfigured;
        NetworkStarterShip ownedSpacecraft;

        public CelestialFrameProvider CelestialFrameProvider =>
            celestialFrameProvider;
        public GravitySimulation GravitySimulation => gravitySimulation;
        public NetworkWorldOriginAuthority OriginAuthority => originAuthority;
        public GeneratedEntityId ZoneId => simulationZoneContext != null
            ? simulationZoneContext.ZoneId
            : GeneratedEntityId.None;
        public int SpawnPointCount => spawnPoints?.Length ?? 0;

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
            if (IsMultiplayerMode)
            {
                ConfigureMultiplayer();
            }
        }

        void Start()
        {
            if (IsMultiplayerMode)
            {
                ApplyLocalPresentationMode();
            }
        }

        bool IsMultiplayerMode =>
            runtimeRoot != null &&
            runtimeRoot.Mode == GameplaySessionMode.Multiplayer;

        void OnDestroy()
        {
            BindOriginRebaser(null);
        }

        public void BindSession(
            NetworkPlayerSpawner spawner,
            NetworkWorldOriginAuthority worldOriginAuthority)
        {
            ConfigureMultiplayer();
            playerSpawner = spawner;
            originAuthority = worldOriginAuthority;
            BindOriginRebaser(originRebaser);
            originAuthority?.BindRebaser(originRebaser);
            playerSpawner?.BindContext(this, originAuthority);
        }

        public void BindPresentation(
            SpacecraftCameraRig spacecraftRig,
            FirstPersonCameraRig firstPersonRig,
            Transform cameraView,
            PlayerControlLock playerControlLock,
            GameplayUiController gameplayUiController,
            CelestialLightingRig lighting,
            CelestialLodController lod)
        {
            spacecraftCameraRig = spacecraftRig;
            firstPersonCameraRig = firstPersonRig;
            viewReference = cameraView;
            controlLock = playerControlLock;
            gameplayUi = gameplayUiController;
            Camera camera = cameraView != null
                ? cameraView.GetComponent<Camera>()
                : null;
            lighting?.SetPrimarySource(FindInScene<CelestialLightSource>());
            if (lod != null)
            {
                lod.SetCamera(camera);
                RegisterZoneVisuals(lod);
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
            if (!TryResolveDryTerrainPose(
                    point.position,
                    point.forward,
                    surfaceClearance,
                    out CelestialSurfacePlacementResult placement))
            {
                return false;
            }

            Vector3 authoredOffset = Vector3.ProjectOnPlane(
                point.position - ResolveSpawnGroupCenter(),
                placement.PlacementUp);
            position = placement.Position + authoredOffset;
            rotation = placement.Rotation;
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

        public bool TryGetStarterShipPose(
            int partySize,
            int shipIndex,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (starterShipFormations == null ||
                partySize < 1 ||
                partySize > starterShipFormations.Length ||
                shipIndex < 0 ||
                shipIndex >= partySize)
            {
                return false;
            }

            Transform formation = starterShipFormations[partySize - 1];
            Transform anchor = formation != null
                ? formation.Find($"Ship_{shipIndex + 1}")
                : null;
            if (anchor == null ||
                !TryResolveDryTerrainPose(
                    anchor.position,
                    anchor.forward,
                    starterShipSurfaceClearance,
                    out CelestialSurfacePlacementResult placement))
            {
                return false;
            }

            position = placement.Position;
            rotation = placement.Rotation;
            return true;
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
            if (player == null ||
                firstPersonCameraRig == null ||
                viewReference == null ||
                controlLock == null)
            {
                return false;
            }

            ownedPlayer = player;
            player.ApplyPossessionState(true);
            player.BindScene(celestialFrameProvider, originAuthority);
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

            return true;
        }

        public bool BindOwnedSpacecraft(NetworkStarterShip ship)
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
            LocalPlayerCameraBinding.FollowSpacecraft(
                firstPersonCameraRig,
                spacecraftCameraRig,
                ship.Rig,
                ship.Motor,
                ship.transform,
                SpacecraftPilotCameraView.Exterior);
            ship.Input?.SetControlLock(controlLock);
            if (ship.Input != null)
            {
                ship.Input.enabled = true;
            }

            SetTrackingTarget(ship.transform);

            return true;
        }

        public void RestoreOwnedExplorer(NetworkStarterShip ship)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (ship != null && ship.Input != null)
            {
                ship.Input.enabled = false;
                ship.Input.SetControlLock(null);
            }

            ownedSpacecraft = null;
            LocalPlayerCameraBinding.Release(null, spacecraftCameraRig);
            if (firstPersonCameraRig != null)
            {
                firstPersonCameraRig.enabled = true;
            }

            if (ownedPlayer != null)
            {
                BindOwnedPlayer(ownedPlayer);
            }
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

            ownedPlayer = null;
            player.Input.SetControlLock(null);
            player.InteractionRaycaster?.SetControlLock(null);
            gameplayUi?.SetInteractionRaycaster(null);
            LocalPlayerCameraBinding.Release(firstPersonCameraRig, null);
            LocalPlayerCameraBinding.SetCursorCaptured(false);
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

            ApplyLocalPresentationMode();

            if (spacecraftRigidbody != null &&
                !spacecraftRigidbody.isKinematic)
            {
                spacecraftRigidbody.linearVelocity = Vector3.zero;
                spacecraftRigidbody.angularVelocity = Vector3.zero;
                spacecraftRigidbody.isKinematic = true;
            }

            gravitySimulation?.SetIntegrationEnabled(false);
            gravitySimulation?.SetExternalTimeSource(true);
            originRebaser?.SetAutomaticRebasing(false);
            for (int i = 0; i < surfacePatchSystems.Count; i++)
            {
                // Multiplayer collision must be identical for every peer. The
                // adaptive local patch remains visual-only; the global planet
                // collider is the shared authoritative collision baseline.
                surfacePatchSystems[i]?.SetCollisionObserverSource(null);
            }
        }
    }
}
