using System.Collections.Generic;
using Farion.Simulation.Celestial;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    internal readonly struct SurfaceFormationPlacementItem
    {
        public SurfaceFormationPlacementItem(
            SurfaceFormationRole role,
            int pieceIndex,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 scale,
            Vector3 localDownhill)
        {
            Role = role;
            PieceIndex = pieceIndex;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            Scale = scale;
            LocalDownhill = localDownhill;
        }

        public SurfaceFormationRole Role { get; }
        public int PieceIndex { get; }
        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }
        public Vector3 Scale { get; }
        public Vector3 LocalDownhill { get; }
    }

    internal static class SurfaceFormationPlacement
    {
        const int ChannelsPerPiece = 10;
        const int RoleChannelStride = 512;

        public static float EvaluateSlopeBreak(
            PlanetSurfaceModel surfaceModel,
            Vector3 direction,
            float surfaceRadius,
            float baseSlopeDegrees,
            SurfaceFormationRule rule)
        {
            if (rule.SlopeBreakBias <= 0f || surfaceRadius <= 0.01f)
            {
                return 1f;
            }

            BuildTangentBasis(direction, out Vector3 tangent, out Vector3 bitangent);
            float angularStep = rule.SlopeBreakSampleMeters / surfaceRadius;
            float maximumDelta = 0f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 offset = i switch
                {
                    0 => tangent,
                    1 => -tangent,
                    2 => bitangent,
                    _ => -bitangent
                };
                Vector3 neighbour = (direction + offset * angularStep).normalized;
                if (!surfaceModel.TrySamplePlanetSurface(neighbour, out PlanetSurfaceSample sample))
                {
                    continue;
                }

                maximumDelta = Mathf.Max(
                    maximumDelta,
                    Mathf.Abs(sample.Surface.SlopeAngleDegrees - baseSlopeDegrees));
            }

            float breakWeight = Mathf.Clamp01(maximumDelta / 12f);
            return Mathf.Lerp(1f, breakWeight, rule.SlopeBreakBias);
        }

        public static float ResolveFeatureScale(
            SurfaceFormationRule rule,
            in CelestialGeologySample geology,
            float referenceHeight)
        {
            float minimum = Mathf.Min(rule.UniformScaleRange.x, rule.UniformScaleRange.y);
            float maximum = Mathf.Max(rule.UniformScaleRange.x, rule.UniformScaleRange.y);
            if (!geology.HasFeature || referenceHeight <= 0.01f)
            {
                return Mathf.Lerp(minimum, maximum, 0.5f);
            }

            float target = geology.FeatureHeight * rule.FeatureHeightRatio;
            return Mathf.Clamp(target / referenceHeight, minimum, maximum);
        }

        public static void BuildComposition(
            SurfaceFormationRule rule,
            PlanetSurfaceModel surfaceModel,
            Transform bodyTransform,
            Vector3 anchorDirection,
            in CelestialGeologySample geology,
            SurfaceScatterCell cell,
            int planetSeed,
            List<SurfaceFormationPlacementItem> results)
        {
            results.Clear();
            if (!surfaceModel.TrySamplePlanetSurface(anchorDirection, out PlanetSurfaceSample anchorSample))
            {
                return;
            }

            float featureScale = ResolveFeatureScale(
                rule,
                geology,
                rule.Kit.AveragePieceHeight(SurfaceFormationRole.Outcrop));

            AppendOutcropLine(
                rule,
                surfaceModel,
                bodyTransform,
                anchorDirection,
                anchorSample,
                geology,
                cell,
                planetSeed,
                featureScale,
                results);
            int outcropCount = results.Count;
            if (outcropCount == 0)
            {
                return;
            }

            AppendButtresses(
                rule,
                surfaceModel,
                bodyTransform,
                geology,
                cell,
                planetSeed,
                featureScale,
                outcropCount,
                results);
            AppendTalus(
                rule,
                surfaceModel,
                bodyTransform,
                geology,
                cell,
                planetSeed,
                featureScale,
                outcropCount,
                results);
            AppendDebris(
                rule,
                surfaceModel,
                bodyTransform,
                geology,
                cell,
                planetSeed,
                featureScale,
                outcropCount,
                results);
        }

        static void AppendOutcropLine(
            SurfaceFormationRule rule,
            PlanetSurfaceModel surfaceModel,
            Transform bodyTransform,
            Vector3 anchorDirection,
            PlanetSurfaceSample anchorSample,
            CelestialGeologySample geology,
            SurfaceScatterCell cell,
            int planetSeed,
            float featureScale,
            List<SurfaceFormationPlacementItem> results)
        {
            int count = ResolveCount(rule, SurfaceFormationRole.Outcrop, cell, planetSeed);
            if (count <= 0)
            {
                return;
            }

            float step = rule.Kit.AverageFootprint(SurfaceFormationRole.Outcrop) *
                featureScale * rule.OutcropSpacingRatio * 2f;
            int forwardSteps = count / 2;
            int backwardSteps = count - 1 - forwardSteps;

            AppendPieceAt(
                rule,
                bodyTransform,
                anchorDirection,
                anchorSample,
                geology,
                SurfaceFormationRole.Outcrop,
                16,
                featureScale,
                cell,
                planetSeed,
                results);
            WalkOutcropRidge(
                rule,
                surfaceModel,
                bodyTransform,
                anchorDirection,
                anchorSample,
                geology,
                cell,
                planetSeed,
                featureScale,
                step,
                1f,
                forwardSteps,
                16 + ChannelsPerPiece,
                results);
            WalkOutcropRidge(
                rule,
                surfaceModel,
                bodyTransform,
                anchorDirection,
                anchorSample,
                geology,
                cell,
                planetSeed,
                featureScale,
                step,
                -1f,
                backwardSteps,
                16 + ChannelsPerPiece * (forwardSteps + 1),
                results);
        }

        static void WalkOutcropRidge(
            SurfaceFormationRule rule,
            PlanetSurfaceModel surfaceModel,
            Transform bodyTransform,
            Vector3 startDirection,
            PlanetSurfaceSample startSample,
            CelestialGeologySample geology,
            SurfaceScatterCell cell,
            int planetSeed,
            float featureScale,
            float step,
            float sign,
            int steps,
            int channelStart,
            List<SurfaceFormationPlacementItem> results)
        {
            Vector3 cursor = startDirection;
            PlanetSurfaceSample cursorSample = startSample;
            for (int i = 0; i < steps; i++)
            {
                int channel = channelStart + i * ChannelsPerPiece;
                Vector3 normalLocal = bodyTransform
                    .InverseTransformDirection(cursorSample.Surface.Normal)
                    .normalized;
                Vector3 up = SurfaceScatterPlacement.ResolvePlacementUp(
                    cursor,
                    normalLocal,
                    rule.SurfaceNormalAlignment);
                Vector3 downhill = ResolveDownhill(cursor, normalLocal, geology, up);
                Vector3 strike = Vector3.Cross(up, downhill);
                if (strike.sqrMagnitude <= 0.000001f)
                {
                    return;
                }

                strike = strike.normalized * sign;
                float wander = (Hash(rule, cell, planetSeed, channel + 7) - 0.5f) * 0.5f;
                Vector3 advance = (strike + downhill * wander).normalized * step;
                cursor = (cursor * cursorSample.SurfaceRadius + advance).normalized;
                if (!surfaceModel.TrySamplePlanetSurface(cursor, out PlanetSurfaceSample sample))
                {
                    return;
                }

                cursorSample = sample;
                AppendPieceAt(
                    rule,
                    bodyTransform,
                    cursor,
                    sample,
                    geology,
                    SurfaceFormationRole.Outcrop,
                    channel,
                    featureScale,
                    cell,
                    planetSeed,
                    results);
            }
        }

        static void AppendButtresses(
            SurfaceFormationRule rule,
            PlanetSurfaceModel surfaceModel,
            Transform bodyTransform,
            CelestialGeologySample geology,
            SurfaceScatterCell cell,
            int planetSeed,
            float featureScale,
            int outcropCount,
            List<SurfaceFormationPlacementItem> results)
        {
            int count = ResolveCount(rule, SurfaceFormationRole.Buttress, cell, planetSeed);
            int roleChannel = (int)SurfaceFormationRole.Buttress * RoleChannelStride;
            for (int i = 0; i < count; i++)
            {
                int channel = roleChannel + 1 + i * ChannelsPerPiece;
                int anchorIndex = Mathf.Clamp(
                    Mathf.FloorToInt(Hash(rule, cell, planetSeed, channel + 5) * outcropCount),
                    0,
                    outcropCount - 1);
                SurfaceFormationPlacementItem anchor = results[anchorIndex];
                Vector3 anchorDirection = anchor.LocalPosition.normalized;
                Vector3 downhill = anchor.LocalDownhill;
                Vector3 strike = Vector3.Cross(anchorDirection, downhill).normalized;
                float reach = rule.Kit.AverageFootprint(SurfaceFormationRole.Buttress) *
                    featureScale * 1.6f;
                float lateral = (Hash(rule, cell, planetSeed, channel) - 0.5f) * 2f * reach;
                float forward = (0.35f + Hash(rule, cell, planetSeed, channel + 1) * 0.8f) * reach;
                if (!TryResolveOffsetSample(
                        surfaceModel,
                        anchorDirection,
                        anchor.LocalPosition.magnitude,
                        strike * lateral + downhill * forward,
                        out Vector3 direction,
                        out PlanetSurfaceSample sample))
                {
                    continue;
                }

                AppendPieceAt(
                    rule,
                    bodyTransform,
                    direction,
                    sample,
                    geology,
                    SurfaceFormationRole.Buttress,
                    channel,
                    featureScale * 0.85f,
                    cell,
                    planetSeed,
                    results);
            }
        }

        static void AppendTalus(
            SurfaceFormationRule rule,
            PlanetSurfaceModel surfaceModel,
            Transform bodyTransform,
            CelestialGeologySample geology,
            SurfaceScatterCell cell,
            int planetSeed,
            float featureScale,
            int outcropCount,
            List<SurfaceFormationPlacementItem> results)
        {
            int perOutcrop = ResolveCount(rule, SurfaceFormationRole.Talus, cell, planetSeed);
            if (perOutcrop <= 0)
            {
                return;
            }

            int roleChannel = (int)SurfaceFormationRole.Talus * RoleChannelStride;
            float outcropHeight = rule.Kit.AveragePieceHeight(SurfaceFormationRole.Outcrop) *
                featureScale;
            float reach = outcropHeight * rule.TalusReach;
            for (int outcrop = 0; outcrop < outcropCount; outcrop++)
            {
                SurfaceFormationPlacementItem anchor = results[outcrop];
                Vector3 anchorDirection = anchor.LocalPosition.normalized;
                float anchorRadius = anchor.LocalPosition.magnitude;
                Vector3 downhill = anchor.LocalDownhill;
                Vector3 strike = Vector3.Cross(anchorDirection, downhill).normalized;
                for (int i = 0; i < perOutcrop; i++)
                {
                    int channel = roleChannel + 1 + (outcrop * perOutcrop + i) * ChannelsPerPiece;
                    float progress = (i + 0.5f + (Hash(rule, cell, planetSeed, channel) - 0.5f) * 0.6f) /
                        perOutcrop;
                    progress = Mathf.Clamp01(progress);
                    float forward = reach * (0.25f + progress * 0.75f);
                    float spread = reach * progress * 0.55f;
                    float lateral = (Hash(rule, cell, planetSeed, channel + 1) - 0.5f) * 2f * spread;
                    if (!TryResolveOffsetSample(
                            surfaceModel,
                            anchorDirection,
                            anchorRadius,
                            downhill * forward + strike * lateral,
                            out Vector3 direction,
                            out PlanetSurfaceSample sample))
                    {
                        continue;
                    }

                    float falloff = Mathf.Lerp(1f, rule.TalusScaleFalloff, progress);
                    AppendPieceAt(
                        rule,
                        bodyTransform,
                        direction,
                        sample,
                        geology,
                        SurfaceFormationRole.Talus,
                        channel,
                        featureScale * falloff,
                        cell,
                        planetSeed,
                        results);
                }
            }
        }

        static void AppendDebris(
            SurfaceFormationRule rule,
            PlanetSurfaceModel surfaceModel,
            Transform bodyTransform,
            CelestialGeologySample geology,
            SurfaceScatterCell cell,
            int planetSeed,
            float featureScale,
            int outcropCount,
            List<SurfaceFormationPlacementItem> results)
        {
            int count = ResolveCount(rule, SurfaceFormationRole.Debris, cell, planetSeed);
            if (count <= 0)
            {
                return;
            }

            int roleChannel = (int)SurfaceFormationRole.Debris * RoleChannelStride;
            float outcropHeight = rule.Kit.AveragePieceHeight(SurfaceFormationRole.Outcrop) *
                featureScale;
            float reach = outcropHeight * rule.TalusReach;
            for (int i = 0; i < count; i++)
            {
                int channel = roleChannel + 1 + i * ChannelsPerPiece;
                int anchorIndex = Mathf.Clamp(
                    Mathf.FloorToInt(Hash(rule, cell, planetSeed, channel + 5) * outcropCount),
                    0,
                    outcropCount - 1);
                SurfaceFormationPlacementItem anchor = results[anchorIndex];
                Vector3 anchorDirection = anchor.LocalPosition.normalized;
                Vector3 downhill = anchor.LocalDownhill;
                Vector3 strike = Vector3.Cross(anchorDirection, downhill).normalized;
                float forward = reach * (0.6f + Hash(rule, cell, planetSeed, channel) * 1.3f);
                float lateral = (Hash(rule, cell, planetSeed, channel + 1) - 0.5f) * 2f *
                    rule.FormationRadius * 0.7f;
                if (!TryResolveOffsetSample(
                        surfaceModel,
                        anchorDirection,
                        anchor.LocalPosition.magnitude,
                        downhill * forward + strike * lateral,
                        out Vector3 direction,
                        out PlanetSurfaceSample sample))
                {
                    continue;
                }

                float scatterScale = Mathf.Lerp(
                    1f,
                    rule.TalusScaleFalloff,
                    Mathf.Clamp01(forward / Mathf.Max(0.01f, reach * 1.9f)));
                AppendPieceAt(
                    rule,
                    bodyTransform,
                    direction,
                    sample,
                    geology,
                    SurfaceFormationRole.Debris,
                    channel,
                    featureScale * scatterScale,
                    cell,
                    planetSeed,
                    results);
            }
        }

        static bool TryResolveOffsetSample(
            PlanetSurfaceModel surfaceModel,
            Vector3 anchorDirection,
            float anchorRadius,
            Vector3 planarOffset,
            out Vector3 direction,
            out PlanetSurfaceSample sample)
        {
            direction = (anchorDirection * Mathf.Max(0.01f, anchorRadius) + planarOffset).normalized;
            return surfaceModel.TrySamplePlanetSurface(direction, out sample);
        }

        static void AppendPieceAt(
            SurfaceFormationRule rule,
            Transform bodyTransform,
            Vector3 direction,
            PlanetSurfaceSample sample,
            CelestialGeologySample geology,
            SurfaceFormationRole role,
            int channel,
            float scale,
            SurfaceScatterCell cell,
            int planetSeed,
            List<SurfaceFormationPlacementItem> results)
        {
            int pieceIndex = rule.Kit.ChoosePiece(
                role,
                Hash(rule, cell, planetSeed, channel + 2));
            if (pieceIndex < 0)
            {
                return;
            }

            SurfaceFormationPiece piece = rule.Kit.Resolve(role)[pieceIndex];
            Vector3 normalLocal = bodyTransform
                .InverseTransformDirection(sample.Surface.Normal)
                .normalized;
            Vector3 up = SurfaceScatterPlacement.ResolvePlacementUp(
                direction,
                normalLocal,
                rule.SurfaceNormalAlignment);
            Vector3 downhill = ResolveDownhill(direction, normalLocal, geology, up);
            float variation = Mathf.Lerp(
                1f - rule.ScaleVariation,
                1f + rule.ScaleVariation,
                Hash(rule, cell, planetSeed, channel + 3));
            float finalScale = scale * variation * piece.BaseScale;

            Quaternion rotation = Quaternion.LookRotation(downhill, up);
            float yaw = (Hash(rule, cell, planetSeed, channel + 4) - 0.5f) * 2f *
                rule.YawJitterDegrees;
            float tiltX = (Hash(rule, cell, planetSeed, channel + 5) - 0.5f) * 2f *
                rule.TiltJitterDegrees;
            float tiltZ = (Hash(rule, cell, planetSeed, channel + 6) - 0.5f) * 2f *
                rule.TiltJitterDegrees;
            rotation = Quaternion.AngleAxis(yaw, up) * rotation;
            rotation *= Quaternion.Euler(tiltX, 0f, tiltZ);

            float stretch = Mathf.Lerp(
                1f - rule.ShapeVariation,
                1f + rule.ShapeVariation,
                Hash(rule, cell, planetSeed, channel + 8));
            float lateral = 1f / Mathf.Sqrt(Mathf.Max(0.05f, stretch));
            Vector3 scaleVector = new(
                finalScale * lateral,
                finalScale * stretch,
                finalScale * lateral);
            float embed = piece.PieceHeight * scaleVector.y * piece.EmbedFraction;
            Vector3 position = direction * sample.SurfaceRadius - up * embed;
            results.Add(new SurfaceFormationPlacementItem(
                role,
                pieceIndex,
                position,
                rotation,
                scaleVector,
                downhill));
        }

        static Vector3 ResolveDownhill(
            Vector3 radialDirection,
            Vector3 surfaceNormal,
            CelestialGeologySample geology,
            Vector3 placementUp)
        {
            Vector3 tangential = surfaceNormal -
                radialDirection * Vector3.Dot(surfaceNormal, radialDirection);
            if (tangential.sqrMagnitude > 0.000001f)
            {
                Vector3 projected = Vector3.ProjectOnPlane(tangential, placementUp);
                if (projected.sqrMagnitude > 0.000001f)
                {
                    return projected.normalized;
                }

                return tangential.normalized;
            }

            if (geology.FeatureDirection.sqrMagnitude > 0.000001f)
            {
                Vector3 fallback = Vector3.ProjectOnPlane(
                    -geology.FeatureDirection,
                    placementUp);
                if (fallback.sqrMagnitude > 0.000001f)
                {
                    return fallback.normalized;
                }
            }

            BuildTangentBasis(placementUp, out Vector3 tangent, out _);
            return tangent;
        }

        static int ResolveCount(
            SurfaceFormationRule rule,
            SurfaceFormationRole role,
            SurfaceScatterCell cell,
            int planetSeed)
        {
            Vector2Int range = rule.ResolveCount(role);
            float random = Hash(rule, cell, planetSeed, (int)role * RoleChannelStride);
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Lerp(range.x, range.y, random)));
        }

        static void BuildTangentBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
        {
            Vector3 reference = Mathf.Abs(normal.y) < 0.95f ? Vector3.up : Vector3.right;
            tangent = Vector3.Cross(reference, normal).normalized;
            bitangent = Vector3.Cross(normal, tangent).normalized;
        }

        static float Hash(
            SurfaceFormationRule rule,
            SurfaceScatterCell cell,
            int planetSeed,
            int channel)
        {
            return SurfaceScatterPlacement.Hash01(planetSeed, rule.StableId, cell, channel);
        }
    }
}
