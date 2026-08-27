using System;
using System.Collections.Generic;
using Farion.Gameplay.Presentation.Flight;
using Farion.Simulation.Physics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Editor.Authoring
{
    static class FarionCelestialRescale
    {
        const string ScenePath = "Assets/Project/Scenes/SC_WorldZone.unity";
        const string DefinitionFolder = "Assets/Project/Design/Simulation/Physics/";
        const string ShuttlePrefabPath =
            "Assets/Project/Prefabs/Gameplay/Spacecraft/PF_PlayerStarterShuttle.prefab";

        const float StarGravity = 350f;
        const float StarRadius = 12000f;
        const float PlanetRadius = 16000f;
        const float PlanetGravity = 9.81f;
        const float MoonRadius = 1200f;
        const float MoonGravity = 1.62f;
        const float EmberRadius = 5000f;
        const float EmberGravity = 4f;
        const float RimeRadius = 8000f;
        const float RimeGravity = 5f;

        const float PlanetOrbitRadius = 600000f;
        const float MoonOrbitRadius = 130000f;
        const float EmberOrbitRadius = 380000f;
        const float RimeOrbitRadius = 1700000f;

        const float PlanetDaySeconds = 1200f;
        const float StarSpinDegreesPerSecond = 0.01f;
        const float EmberSpinDegreesPerSecond = 0.25f;
        const float RimeSpinDegreesPerSecond = 0.12f;

        [MenuItem("Farion/Authoring/Apply Celestial Rescale")]
        public static void Apply()
        {
            float starMu = StarGravity * StarRadius * StarRadius;
            float planetMu = PlanetGravity * PlanetRadius * PlanetRadius;

            float planetSpeed = Mathf.Sqrt(starMu / PlanetOrbitRadius);
            float moonRelativeSpeed = Mathf.Sqrt(planetMu / MoonOrbitRadius);
            float emberSpeed = Mathf.Sqrt(starMu / EmberOrbitRadius);
            float rimeSpeed = Mathf.Sqrt(starMu / RimeOrbitRadius);
            float moonPeriod = 2f * Mathf.PI * MoonOrbitRadius / moonRelativeSpeed;

            WriteDefinition(
                "SO_StartingStar",
                StarRadius,
                StarGravity,
                Vector3.zero,
                new Vector3(0f, StarSpinDegreesPerSecond, 0f));
            WriteDefinition(
                "SO_StartingPlanet",
                PlanetRadius,
                PlanetGravity,
                new Vector3(0f, 0f, planetSpeed),
                new Vector3(0f, 360f / PlanetDaySeconds, 0f));
            WriteDefinition(
                "SO_StartingMoon",
                MoonRadius,
                MoonGravity,
                new Vector3(0f, 0f, planetSpeed + moonRelativeSpeed),
                new Vector3(0f, 360f / moonPeriod, 0f));
            WriteDefinition(
                "SO_StartingInnerPlanet",
                EmberRadius,
                EmberGravity,
                new Vector3(0f, 0f, -emberSpeed),
                new Vector3(0f, EmberSpinDegreesPerSecond, 0f));
            WriteDefinition(
                "SO_StartingOuterPlanet",
                RimeRadius,
                RimeGravity,
                new Vector3(-rimeSpeed, 0f, 0f),
                new Vector3(0f, RimeSpinDegreesPerSecond, 0f));

            ApplyScenePositions();
            ApplyProfiles();
            ApplyShuttlePrefab();

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Celestial rescale applied. planet v={planetSpeed:0.###} " +
                $"moon v={moonRelativeSpeed:0.###} period={moonPeriod:0.#} s " +
                $"ember v={emberSpeed:0.###} rime v={rimeSpeed:0.###}");
        }

        static void WriteDefinition(
            string assetName,
            float radius,
            float surfaceGravity,
            Vector3 initialVelocity,
            Vector3 spinDegreesPerSecond)
        {
            string path = DefinitionFolder + assetName + ".asset";
            CelestialBodyDefinition definition =
                AssetDatabase.LoadAssetAtPath<CelestialBodyDefinition>(path);
            if (definition == null)
            {
                throw new InvalidOperationException($"Missing celestial definition at {path}.");
            }

            SerializedObject serialized = new(definition);
            serialized.FindProperty("radius").floatValue = radius;
            serialized.FindProperty("surfaceGravity").floatValue = surfaceGravity;
            serialized.FindProperty("initialVelocity").vector3Value = initialVelocity;
            serialized
                .FindProperty("initialAngularVelocityDegreesPerSecond")
                .vector3Value = spinDegreesPerSecond;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        static void ApplyScenePositions()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Dictionary<string, CelestialBody> bodies = new(StringComparer.Ordinal);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (CelestialBody body in root.GetComponentsInChildren<CelestialBody>(true))
                {
                    bodies[body.BodyName] = body;
                }
            }

            foreach (string required in new[]
            {
                "Starting Star",
                "Starting Planet",
                "Starting Moon",
                "Ember",
                "Rime"
            })
            {
                if (!bodies.ContainsKey(required))
                {
                    throw new InvalidOperationException(
                        $"{ScenePath} has no celestial body named '{required}'.");
                }
            }

            CelestialBody planet = bodies["Starting Planet"];
            planet.transform.position = new Vector3(0f, -PlanetRadius, 0f);
            planet.transform.rotation = Quaternion.identity;

            float landingRadius = FarionLandingSitePlacement.OrientToBestLandingSite(planet);
            float systemPlaneY = -landingRadius;
            planet.transform.position = new Vector3(0f, systemPlaneY, 0f);

            bodies["Starting Star"].transform.position =
                new Vector3(-PlanetOrbitRadius, systemPlaneY, 0f);
            bodies["Starting Moon"].transform.position =
                new Vector3(MoonOrbitRadius, systemPlaneY, 0f);
            bodies["Ember"].transform.position =
                new Vector3(-PlanetOrbitRadius - EmberOrbitRadius, systemPlaneY, 0f);
            bodies["Rime"].transform.position =
                new Vector3(-PlanetOrbitRadius, systemPlaneY, RimeOrbitRadius);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void ApplyProfiles()
        {
            SetFields(
                "Assets/Project/Design/Rendering/Celestial/SO_CelestialSurfacePatchProfile.asset",
                new Dictionary<string, object>
                {
                    ["enterAltitudeReliefMultiple"] = 32f,
                    ["exitAltitudeReliefMultiple"] = 48f,
                    ["minimumSurfaceAltitudeMeters"] = 250f,
                    ["patchResolution"] = 16,
                    ["targetTriangleEdgeMeters"] = 2.5f,
                    ["splitDistanceMultiplier"] = 2.2f,
                    ["splitHysteresisRatio"] = 0.15f,
                    ["maxActivePatches"] = 1536,
                    ["collisionTriangleEdgeMeters"] = 25f,
                    ["collisionRadiusMeters"] = 400f,
                    ["bakeCollisionMeshes"] = true,
                    ["collisionPredictionSeconds"] = 0.75f,
                    ["collisionSafetyMarginMeters"] = 24f,
                    ["observerMoveThresholdMeters"] = 2f,
                    ["observerAngleThresholdDegrees"] = 0.08f,
                    ["maximumPatchBuildsPerFrame"] = 16,
                    ["patchBuildBudgetMilliseconds"] = 3.5f
                });

            SetFields(
                "Assets/Project/Design/Simulation/Celestial/SO_ContinentRidgeShapeProfile.asset",
                new Dictionary<string, object>
                {
                    ["elevationScaleMeters"] = 24f,
                    ["featureReferenceRadiusMeters"] = 1600f
                });
            SetFields(
                "Assets/Project/Design/Simulation/Celestial/SO_FrozenPlainsShapeProfile.asset",
                new Dictionary<string, object>
                {
                    ["elevationScaleMeters"] = 45f,
                    ["featureReferenceRadiusMeters"] = 800f
                });
            SetFields(
                "Assets/Project/Design/Simulation/Celestial/SO_VolcanicRidgeShapeProfile.asset",
                new Dictionary<string, object>
                {
                    ["elevationScaleMeters"] = 13f,
                    ["featureReferenceRadiusMeters"] = 500f
                });
            SetFields(
                "Assets/Project/Design/Simulation/Celestial/SO_MoonCraterShapeProfile.asset",
                new Dictionary<string, object> { ["reliefScaleMeters"] = 120f });

            SetFields(
                "Assets/Project/Design/Simulation/Celestial/SO_StartingStellarRadiation.asset",
                new Dictionary<string, object>
                {
                    ["referenceOrbitDistanceInStarRadii"] = 269.51636f
                });

            SetFields(
                "Assets/Project/Design/Rendering/Lighting/SO_CelestialLightingProfile.asset",
                new Dictionary<string, object>
                {
                    ["nearClipPlane"] = 0.3f,
                    ["ambientLight"] = new Color(0.055f, 0.065f, 0.09f, 1f)
                });

            SetFields(
                "Assets/Project/Design/Gameplay/Flight/SO_PlayerStarterShuttleFlightProfile.asset",
                new Dictionary<string, object>
                {
                    ["maxForwardSpeed"] = 250f,
                    ["maxBoostForwardSpeed"] = 650f,
                    ["maxReverseSpeed"] = 90f,
                    ["maxStrafeSpeed"] = 60f,
                    ["maxVerticalSpeed"] = 60f,
                    ["forwardAcceleration"] = 32f,
                    ["boostForwardAcceleration"] = 60f,
                    ["reverseAcceleration"] = 30f,
                    ["strafeAcceleration"] = 18f,
                    ["verticalAcceleration"] = 24f,
                    ["brakeGain"] = 3.2f,
                    ["pitchRateDeg"] = 65f,
                    ["yawRateDeg"] = 35f,
                    ["rollRateDeg"] = 110f,
                    ["pitchAccelerationDeg"] = 180f,
                    ["yawAccelerationDeg"] = 120f,
                    ["rollAccelerationDeg"] = 280f,
                    ["optimalSpeedBandEnd"] = 0.75f,
                    ["offBandAngularScale"] = 0.55f,
                    ["compensateGravityInAssistedMode"] = true,
                    ["limitManualFlightEnvelope"] = true,
                    ["manualEnvelopeStart"] = 0.85f,
                    ["translationSpoolRate"] = 7f,
                    ["rotationSpoolRate"] = 8f,
                    ["boostSpoolRate"] = 5f,
                    ["boostSurgeStrength"] = 0.8f,
                    ["inputDeadZone"] = 0.04f,
                    ["fuelCapacity"] = 1000f,
                    ["fuelPerAccelerationUnit"] = 0.05f,
                    ["idleFuelPerSecond"] = 0f,
                    ["hullIntegrity"] = 500f,
                    ["impactToleranceSpeed"] = 12f,
                    ["criticalImpactSpeed"] = 90f,
                    ["minimumImpactMassRatio"] = 0.05f,
                    ["rigidbodyMass"] = 12000f,
                    ["overrideCenterOfMass"] = false,
                    ["angularDamping"] = 0f
                });

            SetFields(
                "Assets/Project/Design/Gameplay/Character/SO_DefaultFirstPersonMotorProfile.asset",
                new Dictionary<string, object>
                {
                    ["jumpHeight"] = 1.4f,
                    ["jumpReferenceGravity"] = 9.81f,
                    ["maximumJumpSpeed"] = 8f,
                    ["minimumJumpSpeed"] = 1.2f,
                    ["maxJumpEscapeSpeedRatio"] = 0.45f,
                    ["surfacePenetrationSlop"] = 0.35f,
                    ["surfacePenetrationRecoverySpeed"] = 4f
                });
        }

        static void ApplyShuttlePrefab()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(ShuttlePrefabPath);
            try
            {
                SpacecraftFloodlights floodlights =
                    contents.GetComponentInChildren<SpacecraftFloodlights>(true);
                if (floodlights == null)
                {
                    throw new InvalidOperationException(
                        $"{ShuttlePrefabPath} has no SpacecraftFloodlights component.");
                }

                SerializedObject serialized = new(floodlights);
                serialized.FindProperty("startOn").boolValue = true;
                serialized.FindProperty("isOn").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, ShuttlePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static void SetFields(string path, Dictionary<string, object> values)
        {
            ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null)
            {
                throw new InvalidOperationException($"Missing asset at {path}.");
            }

            SerializedObject serialized = new(asset);
            foreach (KeyValuePair<string, object> entry in values)
            {
                SerializedProperty property = serialized.FindProperty(entry.Key);
                if (property == null)
                {
                    throw new InvalidOperationException(
                        $"{path} has no serialized field '{entry.Key}'.");
                }

                switch (entry.Value)
                {
                    case float floatValue:
                        property.floatValue = floatValue;
                        break;
                    case int intValue:
                        property.intValue = intValue;
                        break;
                    case bool boolValue:
                        property.boolValue = boolValue;
                        break;
                    case Color colorValue:
                        property.colorValue = colorValue;
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unsupported value type for '{entry.Key}'.");
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }
    }
}
