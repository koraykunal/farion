using Farion.Gameplay.Character;
using Farion.Gameplay.Flight;
using Farion.Rendering.Celestial;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Editor.Validation
{
    public sealed partial class FarionProjectValidator
    {
        const float MinimumInfluenceRadiusRatio = 4f;
        const float MaximumReliefRatio = 0.05f;
        const float MinimumSurfaceOrbitalPeriodSeconds = 180f;
        const float MinimumLiftOffMarginRatio = 1.3f;

        static void ValidateCelestialSystem(
            Scene scene,
            string scenePath,
            FarionValidationReport report)
        {
            CelestialBody[] bodies = FindSceneComponents<CelestialBody>(scene);
            if (bodies.Length == 0)
            {
                return;
            }

            SpacecraftMotor[] motors = FindSceneComponents<SpacecraftMotor>(scene);
            ExplorerMotor[] explorers = FindSceneComponents<ExplorerMotor>(scene);

            for (int i = 0; i < bodies.Length; i++)
            {
                ValidateCelestialBodyContract(
                    scenePath,
                    bodies[i],
                    motors,
                    explorers,
                    report);
            }
        }

        static void ValidateCelestialBodyContract(
            string scenePath,
            CelestialBody body,
            SpacecraftMotor[] motors,
            ExplorerMotor[] explorers,
            FarionValidationReport report)
        {
            if (body == null || body.Radius <= 0f || body.SurfaceGravity <= 0f)
            {
                return;
            }

            string label = string.IsNullOrEmpty(body.BodyName) ? body.name : body.BodyName;
            float radius = body.Radius;
            float gravity = body.SurfaceGravity;

            ValidateInfluenceHeadroom(scenePath, body, label, radius, report);
            ValidateRelief(scenePath, body, label, radius, report);
            ValidateRotationPeriod(scenePath, body, label, radius, gravity, report);
            ValidateSpin(scenePath, body, label, report);

            if (IsLandable(body.BodyType))
            {
                ValidateLiftOff(scenePath, label, gravity, motors, report);
                ValidateJumpContainment(scenePath, label, radius, gravity, explorers, report);
            }
        }

        static void ValidateInfluenceHeadroom(
            string scenePath,
            CelestialBody body,
            string label,
            float radius,
            FarionValidationReport report)
        {
            CelestialBody attractor = body.OrbitAttractor;
            if (attractor == null || attractor.Mass <= 0f || body.Mass <= 0f)
            {
                return;
            }

            float orbitRadius = Vector3.Distance(
                body.transform.position,
                attractor.transform.position);
            if (orbitRadius <= 0f)
            {
                return;
            }

            float influenceRadius = orbitRadius * Mathf.Pow(body.Mass / attractor.Mass, 0.4f);
            float ratio = influenceRadius / radius;
            if (ratio >= MinimumInfluenceRadiusRatio)
            {
                return;
            }

            report.AddError(
                $"{scenePath}: celestial body '{label}' has a sphere of influence only " +
                $"{ratio:0.0} times its radius ({influenceRadius:0} m over a {radius:0} m body). " +
                $"Approach and landing need at least {MinimumInfluenceRadiusRatio:0}. " +
                "Move it further from its attractor or increase its mass.");
        }

        static void ValidateRelief(
            string scenePath,
            CelestialBody body,
            string label,
            float radius,
            FarionValidationReport report)
        {
            if (!body.TryGetComponent(out CelestialBodyVisual visual))
            {
                return;
            }

            SerializedProperty shapeProperty =
                new SerializedObject(visual).FindProperty("shapeProfile");
            if (shapeProperty?.objectReferenceValue is not CelestialShapeProfile shape)
            {
                return;
            }

            float relief = Mathf.Max(
                Mathf.Abs(shape.EstimatePeakElevationMeters()),
                Mathf.Abs(shape.EstimateTroughElevationMeters()));
            if (relief <= 0f)
            {
                return;
            }

            float ratio = relief / radius;
            if (ratio <= MaximumReliefRatio)
            {
                return;
            }

            report.AddWarning(
                $"{scenePath}: celestial body '{label}' carries {relief:0} m of relief on a " +
                $"{radius:0} m radius ({ratio:P1} of the radius, budget {MaximumReliefRatio:P0}). " +
                "It reads as a lump rather than a sphere from orbit; lower the shape profile's " +
                "elevation scale.");
        }

        static void ValidateRotationPeriod(
            string scenePath,
            CelestialBody body,
            string label,
            float radius,
            float gravity,
            FarionValidationReport report)
        {
            if (body.BodyType != CelestialBodyType.Planet)
            {
                return;
            }

            float surfaceOrbitalPeriod = 2f * Mathf.PI * Mathf.Sqrt(radius / gravity);
            if (surfaceOrbitalPeriod >= MinimumSurfaceOrbitalPeriodSeconds)
            {
                return;
            }

            report.AddWarning(
                $"{scenePath}: celestial body '{label}' completes a surface orbit in " +
                $"{surfaceOrbitalPeriod:0} s. Below {MinimumSurfaceOrbitalPeriodSeconds:0} s the " +
                "body is too small to fly around or navigate by; raise its radius.");
        }

        static void ValidateSpin(
            string scenePath,
            CelestialBody body,
            string label,
            FarionValidationReport report)
        {
            if (body.BodyType == CelestialBodyType.Station ||
                body.InitialAngularVelocityDegreesPerSecond.sqrMagnitude > 0f)
            {
                return;
            }

            report.AddWarning(
                $"{scenePath}: celestial body '{label}' has no spin, so it never turns relative " +
                "to its own frame. Shadows, wind reference and tidal locking all read as static.");
        }

        static void ValidateLiftOff(
            string scenePath,
            string label,
            float gravity,
            SpacecraftMotor[] motors,
            FarionValidationReport report)
        {
            for (int i = 0; i < motors.Length; i++)
            {
                SpacecraftFlightProfile profile = motors[i] != null
                    ? motors[i].FlightProfile
                    : null;
                if (profile == null)
                {
                    continue;
                }

                float required = gravity * MinimumLiftOffMarginRatio;
                if (profile.VerticalAcceleration >= required)
                {
                    continue;
                }

                report.AddError(
                    $"{scenePath}: {motors[i].name} has {profile.VerticalAcceleration:0.0} m/s² of " +
                    $"vertical thrust but '{label}' pulls {gravity:0.0} m/s². Lift-off needs at " +
                    $"least {required:0.0} m/s² to leave the surface at a usable rate.");
            }
        }

        static void ValidateJumpContainment(
            string scenePath,
            string label,
            float radius,
            float gravity,
            ExplorerMotor[] explorers,
            FarionValidationReport report)
        {
            float escapeSpeed = Mathf.Sqrt(2f * gravity * radius);
            for (int i = 0; i < explorers.Length; i++)
            {
                ExplorerMotorProfile profile = explorers[i] != null
                    ? explorers[i].Profile
                    : null;
                if (profile == null || profile.JumpHeight <= 0f)
                {
                    continue;
                }

                float jumpSpeed = Mathf.Sqrt(
                    2f * profile.JumpReferenceGravity * profile.JumpHeight);
                float cap = profile.MaxJumpEscapeSpeedRatio > 0f
                    ? escapeSpeed * profile.MaxJumpEscapeSpeedRatio
                    : float.PositiveInfinity;
                if (jumpSpeed <= cap)
                {
                    continue;
                }

                report.AddWarning(
                    $"{scenePath}: {explorers[i].name} jumps at {jumpSpeed:0.0} m/s but '{label}' " +
                    $"only holds it below {cap:0.0} m/s. The jump is clamped there, so low gravity " +
                    "reads weaker than it should.");
            }
        }

        static bool IsLandable(CelestialBodyType bodyType)
        {
            return bodyType is CelestialBodyType.Planet
                or CelestialBodyType.Moon
                or CelestialBodyType.Asteroid;
        }
    }
}
