using Farion.Gameplay.Actors;
using Farion.Gameplay.Character;
using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    public readonly struct PlayerExplorerPlacementContext
    {
        public PlayerExplorerPlacementContext(
            GameObject explorerRoot,
            Rigidbody explorerRigidbody,
            FirstPersonMotor explorerMotor,
            CelestialActorProbe explorerCelestialProbe,
            Transform spacecraftRoot,
            Rigidbody spacecraftRigidbody,
            CelestialActorProbe spacecraftCelestialProbe,
            float exitPoseClearance,
            bool snapToExteriorSurface,
            float exteriorGroundClearance,
            float exteriorSurfaceClearance)
        {
            ExplorerRoot = explorerRoot;
            ExplorerRigidbody = explorerRigidbody;
            ExplorerMotor = explorerMotor;
            ExplorerCelestialProbe = explorerCelestialProbe;
            SpacecraftRoot = spacecraftRoot;
            SpacecraftRigidbody = spacecraftRigidbody;
            SpacecraftCelestialProbe = spacecraftCelestialProbe;
            ExitPoseClearance = exitPoseClearance;
            SnapToExteriorSurface = snapToExteriorSurface;
            ExteriorGroundClearance = exteriorGroundClearance;
            ExteriorSurfaceClearance = exteriorSurfaceClearance;
        }

        public GameObject ExplorerRoot { get; }
        public Rigidbody ExplorerRigidbody { get; }
        public FirstPersonMotor ExplorerMotor { get; }
        public CelestialActorProbe ExplorerCelestialProbe { get; }
        public Transform SpacecraftRoot { get; }
        public Rigidbody SpacecraftRigidbody { get; }
        public CelestialActorProbe SpacecraftCelestialProbe { get; }
        public float ExitPoseClearance { get; }
        public bool SnapToExteriorSurface { get; }
        public float ExteriorGroundClearance { get; }
        public float ExteriorSurfaceClearance { get; }
    }

    public static class PlayerExplorerPlacement
    {
        const float DirectionEpsilon = 0.0001f;

        public static void PlaceAtTransform(
            in PlayerExplorerPlacementContext context,
            Transform targetTransform,
            bool snapToExteriorSurface)
        {
            if (context.ExplorerRoot == null || targetTransform == null)
            {
                return;
            }

            context.ExplorerRoot.SetActive(true);

            Vector3 up = targetTransform.up.sqrMagnitude > DirectionEpsilon
                ? targetTransform.up.normalized
                : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(targetTransform.forward, up);
            if (forward.sqrMagnitude <= DirectionEpsilon &&
                context.SpacecraftRoot != null)
            {
                forward = Vector3.ProjectOnPlane(
                    context.SpacecraftRoot.forward,
                    up);
            }

            if (forward.sqrMagnitude <= DirectionEpsilon)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            }

            if (forward.sqrMagnitude <= DirectionEpsilon)
            {
                forward = Vector3.ProjectOnPlane(Vector3.right, up);
            }

            forward = forward.sqrMagnitude > DirectionEpsilon
                ? forward.normalized
                : Vector3.forward;
            Quaternion rotation = Quaternion.LookRotation(forward, up);
            Vector3 position =
                targetTransform.position + up * context.ExitPoseClearance;

            if (snapToExteriorSurface)
            {
                TryResolveExteriorSurfacePose(
                    context,
                    ref position,
                    ref rotation);
            }

            ApplyPose(context, position, rotation, inheritSpacecraftVelocity: true);
            context.ExplorerMotor?.ResetMotorState();
        }

        public static void SnapToExteriorSurface(
            in PlayerExplorerPlacementContext context)
        {
            if (context.ExplorerRoot == null)
            {
                return;
            }

            Vector3 position = context.ExplorerRigidbody != null
                ? context.ExplorerRigidbody.position
                : context.ExplorerRoot.transform.position;
            Quaternion rotation = context.ExplorerRigidbody != null
                ? context.ExplorerRigidbody.rotation
                : context.ExplorerRoot.transform.rotation;

            if (!TryResolveExteriorSurfacePose(
                    context,
                    ref position,
                    ref rotation))
            {
                return;
            }

            ApplyPose(context, position, rotation, inheritSpacecraftVelocity: true);
            context.ExplorerMotor?.ResetMotorState();
        }

        static bool TryResolveExteriorSurfacePose(
            in PlayerExplorerPlacementContext context,
            ref Vector3 position,
            ref Quaternion rotation)
        {
            if (!context.SnapToExteriorSurface)
            {
                return false;
            }

            Vector3 velocity = context.ExplorerRigidbody != null
                ? context.ExplorerRigidbody.linearVelocity
                : context.SpacecraftRigidbody != null
                    ? context.SpacecraftRigidbody.linearVelocity
                    : Vector3.zero;
            if (!TrySampleExitFrame(
                    context,
                    position,
                    velocity,
                    out CelestialFrameSample frame))
            {
                return false;
            }

            CelestialSurfacePlacementOptions options = new(
                ResolveExplorerGroundCenterOffset(context),
                context.ExteriorSurfaceClearance,
                CelestialSurfacePlacementMode.TerrainSurface);
            if (!CelestialSurfacePlacement.TryResolvePose(
                    frame,
                    position,
                    rotation * Vector3.forward,
                    options,
                    out CelestialSurfacePlacementResult placement))
            {
                return false;
            }

            position = placement.Position;
            rotation = placement.Rotation;
            return true;
        }

        static bool TrySampleExitFrame(
            in PlayerExplorerPlacementContext context,
            Vector3 position,
            Vector3 velocity,
            out CelestialFrameSample frame)
        {
            CelestialFrameProvider frameProvider = ResolveFrameProvider(context);
            if (frameProvider != null)
            {
                return frameProvider.TrySample(position, velocity, out frame);
            }

            if (context.SpacecraftCelestialProbe != null &&
                context.SpacecraftCelestialProbe.HasSample)
            {
                frame = context.SpacecraftCelestialProbe.CurrentSample;
                return frame.HasBody;
            }

            if (context.ExplorerCelestialProbe != null &&
                context.ExplorerCelestialProbe.HasSample)
            {
                frame = context.ExplorerCelestialProbe.CurrentSample;
                return frame.HasBody;
            }

            frame = CelestialFrameSample.Empty(position, velocity);
            return false;
        }

        static CelestialFrameProvider ResolveFrameProvider(
            in PlayerExplorerPlacementContext context)
        {
            if (context.ExplorerCelestialProbe != null &&
                context.ExplorerCelestialProbe.FrameProvider != null)
            {
                return context.ExplorerCelestialProbe.FrameProvider;
            }

            return context.SpacecraftCelestialProbe != null
                ? context.SpacecraftCelestialProbe.FrameProvider
                : null;
        }

        static float ResolveExplorerGroundCenterOffset(
            in PlayerExplorerPlacementContext context)
        {
            if (context.ExplorerMotor != null &&
                context.ExplorerMotor.Capsule != null)
            {
                CapsuleCollider capsule = context.ExplorerMotor.Capsule;
                return Mathf.Max(capsule.height * 0.5f, capsule.radius) +
                       context.ExteriorGroundClearance;
            }

            if (context.ExplorerRoot != null &&
                context.ExplorerRoot.TryGetComponent(
                    out CapsuleCollider rootCapsule))
            {
                return Mathf.Max(rootCapsule.height * 0.5f, rootCapsule.radius) +
                       context.ExteriorGroundClearance;
            }

            return context.ExitPoseClearance +
                   context.ExteriorGroundClearance;
        }

        static void ApplyPose(
            in PlayerExplorerPlacementContext context,
            Vector3 position,
            Quaternion rotation,
            bool inheritSpacecraftVelocity)
        {
            if (context.ExplorerRigidbody != null)
            {
                context.ExplorerRigidbody.position = position;
                context.ExplorerRigidbody.rotation = rotation;
                context.ExplorerRigidbody.linearVelocity =
                    inheritSpacecraftVelocity &&
                    context.SpacecraftRigidbody != null
                        ? context.SpacecraftRigidbody.linearVelocity
                        : Vector3.zero;
                context.ExplorerRigidbody.angularVelocity = Vector3.zero;
                return;
            }

            context.ExplorerRoot.transform.SetPositionAndRotation(
                position,
                rotation);
        }
    }
}
