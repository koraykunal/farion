using System.Collections.Generic;
using Farion.Gameplay.Actors;
using Farion.Gameplay.Character;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Input;
using Farion.Gameplay.Resources;
using Farion.Gameplay.Session;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Spawning;
using Farion.Multiplayer.World;
using Farion.Multiplayer.World.Zones;
using Farion.Rendering.Celestial;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.World;
using Farion.Simulation.World.Identity;
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
        [SerializeField] float surfaceClearance = 1.08f;
        [Min(0f)]
        [SerializeField] float starterShipSurfaceClearance = 3f;

        NetworkPlayerSpawner playerSpawner;
        NetworkWorldOriginAuthority originAuthority;
        NetworkExplorerController ownedPlayer;

        public CelestialFrameProvider CelestialFrameProvider =>
            celestialFrameProvider;
        public NetworkWorldOriginAuthority OriginAuthority => originAuthority;
        public GeneratedEntityId ZoneId => simulationZoneContext != null
            ? simulationZoneContext.ZoneId
            : GeneratedEntityId.None;
        public int SpawnPointCount => spawnPoints?.Length ?? 0;

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
            if (runtimeRoot != null &&
                runtimeRoot.Mode == GameplaySessionMode.Multiplayer)
            {
                ConfigureMultiplayer();
            }
        }

        void Start()
        {
            if (runtimeRoot != null &&
                runtimeRoot.Mode == GameplaySessionMode.Multiplayer)
            {
                ConfigureMultiplayer();
            }
        }

        public void BindSession(
            NetworkPlayerSpawner spawner,
            NetworkWorldOriginAuthority worldOriginAuthority)
        {
            ConfigureMultiplayer();
            playerSpawner = spawner;
            originAuthority = worldOriginAuthority;
            originAuthority?.BindRebaser(originRebaser);
            playerSpawner?.BindContext(this, originAuthority);
        }

        public void BindPresentation(
            SpacecraftCameraRig spacecraftRig,
            FirstPersonCameraRig firstPersonRig,
            Transform cameraView,
            PlayerControlLock playerControlLock)
        {
            spacecraftCameraRig = spacecraftRig;
            firstPersonCameraRig = firstPersonRig;
            viewReference = cameraView;
            controlLock = playerControlLock;
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

            if (ownedPlayer == player)
            {
                return true;
            }

            ownedPlayer = player;
            player.BindScene(celestialFrameProvider, originAuthority);
            player.Motor.SetViewReference(viewReference);
            player.Input.SetControlLock(controlLock);
            firstPersonCameraRig?.SetTarget(player.Motor);
            firstPersonCameraRig?.SetInputSource(player.Input);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            originRebaser?.SetTrackingTarget(player.transform);
            for (int i = 0; i < resourceStreamers.Count; i++)
            {
                resourceStreamers[i]?.SetTrackingTarget(player.transform);
            }

            for (int i = 0; i < surfacePatchSystems.Count; i++)
            {
                surfacePatchSystems[i]?.SetCollisionObserverSource(player);
            }

            return true;
        }

        public void UnbindOwnedPlayer(NetworkExplorerController player)
        {
            if (ownedPlayer != player)
            {
                return;
            }

            ownedPlayer = null;
            player.Input.SetControlLock(null);
            firstPersonCameraRig?.SetTarget(null);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            for (int i = 0; i < surfacePatchSystems.Count; i++)
            {
                surfacePatchSystems[i]?.SetCollisionObserverSource(possession);
            }
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

        void ConfigureMultiplayer()
        {
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
                spacecraftMotor.enabled = false;
            }

            if (spacecraftInput != null)
            {
                spacecraftInput.enabled = false;
            }

            if (spacecraftCameraRig != null)
            {
                spacecraftCameraRig.enabled = false;
            }

            if (firstPersonCameraRig != null)
            {
                firstPersonCameraRig.enabled = true;
            }

            if (spacecraftRigidbody != null &&
                !spacecraftRigidbody.isKinematic)
            {
                spacecraftRigidbody.linearVelocity = Vector3.zero;
                spacecraftRigidbody.angularVelocity = Vector3.zero;
                spacecraftRigidbody.isKinematic = true;
            }

            gravitySimulation?.SetIntegrationEnabled(false);
            originRebaser?.SetAutomaticRebasing(false);
        }
    }
}
