using Farion.Simulation.Celestial;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Gameplay.Actors
{
    public static class CelestialSurfacePlacement
    {
        public static bool TryResolvePose(
            CelestialFrameProvider frameProvider,
            Vector3 position,
            Vector3 velocity,
            Vector3 desiredForward,
            CelestialSurfacePlacementOptions options,
            out CelestialSurfacePlacementResult result)
        {
            result = default;
            if (frameProvider == null || !frameProvider.TrySample(position, velocity, out CelestialFrameSample frame))
            {
                return false;
            }

            return TryResolvePose(frame, position, desiredForward, options, out result);
        }

        public static bool TryResolvePose(
            CelestialFrameSample frame,
            Vector3 position,
            Vector3 desiredForward,
            CelestialSurfacePlacementOptions options,
            out CelestialSurfacePlacementResult result)
        {
            result = default;
            if (!frame.HasBody)
            {
                return false;
            }

            Vector3 radialUp = ResolveUp(frame.RadialUp, frame.LocalUp);
            Vector3 terrainPoint = frame.SurfacePoint;
            Vector3 terrainNormal = ResolveUp(frame.SurfaceNormal, radialUp);
            float terrainRadius = ResolveTerrainRadius(frame, terrainPoint);
            bool hasPlanetSurface = false;
            PlanetSurfaceSample planetSurface = default;

            PlanetSurfaceModel surfaceModel = frame.Body.GetComponent<PlanetSurfaceModel>();
            if (surfaceModel != null &&
                surfaceModel.TrySamplePlanetSurface(frame.Body, position, out planetSurface))
            {
                terrainPoint = planetSurface.Surface.Point;
                terrainNormal = ResolveUp(planetSurface.Surface.Normal, terrainNormal);
                terrainRadius = planetSurface.SurfaceRadius;
                hasPlanetSurface = true;
            }

            bool usesOceanSurface = options.AllowsOceanSurface &&
                frame.HasOcean &&
                frame.Environment.OceanRadius > terrainRadius;
            Vector3 placementUp = usesOceanSurface ? radialUp : terrainNormal;
            Vector3 resolvedPosition = usesOceanSurface
                ? frame.BodyPosition + radialUp * frame.Environment.OceanRadius + placementUp * options.OceanSurfaceOffset
                : terrainPoint + placementUp * options.TerrainSurfaceOffset;
            Quaternion resolvedRotation = BuildRotationWithUp(desiredForward, placementUp);

            result = new CelestialSurfacePlacementResult(
                frame,
                resolvedPosition,
                resolvedRotation,
                placementUp,
                terrainRadius,
                usesOceanSurface,
                hasPlanetSurface,
                planetSurface);
            return true;
        }

        static float ResolveTerrainRadius(CelestialFrameSample frame, Vector3 terrainPoint)
        {
            if (terrainPoint.sqrMagnitude > 0.0001f)
            {
                return Vector3.Distance(frame.BodyPosition, terrainPoint);
            }

            return Mathf.Max(0f, frame.CenterDistance - frame.SurfaceAltitude);
        }

        static Vector3 ResolveUp(Vector3 candidate, Vector3 fallback)
        {
            if (candidate.sqrMagnitude > 0.0001f)
            {
                return candidate.normalized;
            }

            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.up;
        }

        static Quaternion BuildRotationWithUp(Vector3 desiredForward, Vector3 up)
        {
            Vector3 normalizedUp = ResolveUp(up, Vector3.up);
            Vector3 forward = Vector3.ProjectOnPlane(desiredForward, normalizedUp);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, normalizedUp);
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.right, normalizedUp);
            }

            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            return Quaternion.LookRotation(forward, normalizedUp);
        }
    }
}
