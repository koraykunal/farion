using System;
using System.Collections.Generic;
using Farion.Core.Physics;
using Farion.Rendering.Celestial;
using Farion.Rendering.PostProcessing;
using Farion.Simulation.Planetary;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Farion.Editor.Validation
{
    internal static class FarionCelestialVisualProjectValidator
    {
        const string RendererDataPath = "Assets/Settings/PC_Renderer.asset";
        const string TerrestrialSurfaceProfilePath =
            "Assets/Project/Design/Rendering/Celestial/SO_TerrestrialSurfaceProfile.asset";
        const string MoonSurfaceProfilePath =
            "Assets/Project/Design/Rendering/Celestial/SO_MoonSurfaceProfile.asset";
        const string PlanetVisualProfilePath =
            "Assets/Project/Design/Rendering/Celestial/SO_TerrestrialPlanetVisualProfile.asset";
        const string SurfacePatchProfilePath =
            "Assets/Project/Design/Rendering/Celestial/SO_CelestialSurfacePatchProfile.asset";
        const string StarVisualProfilePath =
            "Assets/Project/Design/Rendering/Celestial/SO_TestStarVisualProfile.asset";
        const string PlanetaryGenerationProfilePath =
            "Assets/Project/Design/Simulation/Planetary/SO_TestPlanetaryGeneration.asset";

        const string TerrestrialShaderName = "Farion/Celestial/Terrestrial Triplanar";
        const string MoonShaderName = "Farion/Celestial/Moon Triplanar";
        const string StarShaderName = "Farion/Lighting/Star Emission";
        const string OceanShaderName = "Hidden/Farion/Celestial/Ocean Post Process";
        const string AtmosphereShaderName =
            "Hidden/Farion/Celestial/Atmosphere Post Process";
        const string UrpBlueNoiseRoot =
            "Packages/com.unity.render-pipelines.universal/Textures/BlueNoise256/";

        public static void ValidateProjectAssets(FarionValidationReport report)
        {
            ValidateRendererData(report);
            ValidateSurfaceProfiles(report);
            ValidatePlanetaryGenerationProfile(report);
            ValidatePlanetVisualProfile(report);
            LoadRequired<CelestialSurfacePatchProfile>(SurfacePatchProfilePath, report);
            ValidateStarVisualProfile(report);
        }

        public static void ValidateSceneObject(
            GameObject gameObject,
            string scenePath,
            FarionValidationReport report)
        {
            if (gameObject.TryGetComponent(out CelestialBodyVisual bodyVisual))
            {
                ValidateBodyVisual(bodyVisual, scenePath, report);
            }

            if (gameObject.TryGetComponent(out TerrestrialPlanetVisual planetVisual))
            {
                ValidatePlanetVisual(planetVisual, scenePath, report);
            }

            if (gameObject.TryGetComponent(out CelestialSurfacePatchSystem patchSystem))
            {
                ValidateSurfacePatchSystem(patchSystem, scenePath, report);
            }

            if (gameObject.TryGetComponent(out CelestialStarVisual starVisual))
            {
                ValidateStarVisual(starVisual, scenePath, report);
            }
        }

        static void ValidateRendererData(FarionValidationReport report)
        {
            UniversalRendererData rendererData =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            if (rendererData == null)
            {
                report.AddError($"{RendererDataPath}: renderer data is missing.");
                return;
            }

            IReadOnlyList<ScriptableRendererFeature> features =
                rendererData.rendererFeatures;
            int oceanIndex = FindFeatureIndex<FarionOceanRendererFeature>(features);
            int atmosphereIndex =
                FindFeatureIndex<FarionAtmosphereRendererFeature>(features);

            if (oceanIndex < 0)
            {
                report.AddError(
                    $"{RendererDataPath}: {nameof(FarionOceanRendererFeature)} is missing.");
            }

            if (atmosphereIndex < 0)
            {
                report.AddError(
                    $"{RendererDataPath}: {nameof(FarionAtmosphereRendererFeature)} is missing.");
            }

            if (oceanIndex < 0 || atmosphereIndex < 0)
            {
                return;
            }

            FarionOceanRendererFeature oceanFeature =
                (FarionOceanRendererFeature)features[oceanIndex];
            FarionAtmosphereRendererFeature atmosphereFeature =
                (FarionAtmosphereRendererFeature)features[atmosphereIndex];

            if (!oceanFeature.isActive)
            {
                report.AddError(
                    $"{RendererDataPath}: ocean renderer feature is inactive.");
            }

            if (!atmosphereFeature.isActive)
            {
                report.AddError(
                    $"{RendererDataPath}: atmosphere renderer feature is inactive.");
            }

            if (oceanIndex >= atmosphereIndex)
            {
                report.AddError(
                    $"{RendererDataPath}: ocean feature must precede atmosphere.");
            }

            SerializedObject serializedOcean = new(oceanFeature);
            SerializedObject serializedAtmosphere = new(atmosphereFeature);
            ValidateShaderReference(
                serializedOcean,
                "oceanShader",
                OceanShaderName,
                RendererDataPath,
                report);
            ValidateShaderReference(
                serializedAtmosphere,
                "atmosphereShader",
                AtmosphereShaderName,
                RendererDataPath,
                report);

            int oceanEvent =
                (int)RenderPassEvent.BeforeRenderingPostProcessing;
            int atmosphereEvent = oceanEvent + 1;
            int underwaterEvent = oceanEvent + 2;
            ValidateIntegerProperty(
                serializedOcean,
                "renderPassEvent",
                oceanEvent,
                RendererDataPath,
                report);
            ValidateIntegerProperty(
                serializedAtmosphere,
                "renderPassEvent",
                atmosphereEvent,
                RendererDataPath,
                report);
            ValidateIntegerProperty(
                serializedOcean,
                "underwaterRenderPassEvent",
                underwaterEvent,
                RendererDataPath,
                report);
        }

        static void ValidateSurfaceProfiles(FarionValidationReport report)
        {
            TerrestrialSurfaceProfile terrestrial =
                LoadRequired<TerrestrialSurfaceProfile>(
                    TerrestrialSurfaceProfilePath,
                    report);
            if (terrestrial != null)
            {
                ValidateMaterial(
                    terrestrial.Material,
                    TerrestrialShaderName,
                    TerrestrialSurfaceProfilePath,
                    report);

                SurfaceVisualProfile surfaceProfile = terrestrial.SurfaceVisualProfile;
                if (surfaceProfile == null)
                {
                    report.AddError(
                        $"{TerrestrialSurfaceProfilePath}: surface visual profile is missing.");
                }
                else
                {
                    ValidateSurfaceProfile(surfaceProfile, report);
                }
            }

            CelestialSurfaceProfile moon =
                LoadRequired<CelestialSurfaceProfile>(
                    MoonSurfaceProfilePath,
                    report);
            if (moon != null)
            {
                ValidateMaterial(
                    moon.Material,
                    MoonShaderName,
                    MoonSurfaceProfilePath,
                    report);
                ValidateTexture(
                    moon.SurfaceNoiseTexture,
                    TextureImporterType.Default,
                    expectedSrgb: false,
                    requireSquare: true,
                    $"{MoonSurfaceProfilePath}: surface noise",
                    report);
                ValidateTexture(
                    moon.EjectaRayTexture,
                    TextureImporterType.Default,
                    expectedSrgb: false,
                    requireSquare: true,
                    $"{MoonSurfaceProfilePath}: ejecta mask",
                    report,
                    TextureWrapMode.Clamp);
                ValidateTexture(
                    moon.NormalMapFlat,
                    TextureImporterType.NormalMap,
                    expectedSrgb: false,
                    requireSquare: true,
                    $"{MoonSurfaceProfilePath}: flat normal",
                    report);
                ValidateTexture(
                    moon.NormalMapSteep,
                    TextureImporterType.NormalMap,
                    expectedSrgb: false,
                    requireSquare: true,
                    $"{MoonSurfaceProfilePath}: steep normal",
                    report);
            }
        }

        static void ValidatePlanetVisualProfile(FarionValidationReport report)
        {
            TerrestrialPlanetVisualProfile profile =
                LoadRequired<TerrestrialPlanetVisualProfile>(
                    PlanetVisualProfilePath,
                    report);
            if (profile == null)
            {
                return;
            }

            if (profile.ShapeProfile == null)
            {
                report.AddError($"{PlanetVisualProfilePath}: shape profile is missing.");
            }

            if (profile.SurfaceProfile == null)
            {
                report.AddError($"{PlanetVisualProfilePath}: surface profile is missing.");
            }

            if (profile.OceanProfile != null)
            {
                CelestialOceanProfile ocean = profile.OceanProfile;
                ValidateTexture(
                    ocean.WaveNormalA,
                    TextureImporterType.NormalMap,
                    expectedSrgb: false,
                    requireSquare: true,
                    $"{PlanetVisualProfilePath}: wave normal A",
                    report);
                ValidateTexture(
                    ocean.WaveNormalB,
                    TextureImporterType.NormalMap,
                    expectedSrgb: false,
                    requireSquare: true,
                    $"{PlanetVisualProfilePath}: wave normal B",
                    report);
            }

            CelestialAtmosphereProfile atmosphere = profile.AtmosphereProfile;
            if (atmosphere == null)
            {
                report.AddError(
                    $"{PlanetVisualProfilePath}: atmosphere profile is missing.");
                return;
            }

            if (atmosphere.OpticalDepthCompute == null)
            {
                report.AddError(
                    $"{PlanetVisualProfilePath}: atmosphere optical-depth compute shader is missing.");
            }

            if (atmosphere.BlueNoise == null)
            {
                report.AddError(
                    $"{PlanetVisualProfilePath}: atmosphere blue-noise texture is missing.");
                return;
            }

            string blueNoisePath = AssetDatabase.GetAssetPath(atmosphere.BlueNoise);
            if (!blueNoisePath.StartsWith(UrpBlueNoiseRoot, StringComparison.Ordinal))
            {
                report.AddError(
                    $"{PlanetVisualProfilePath}: atmosphere blue noise must use URP's " +
                    $"BlueNoise256 set, found '{blueNoisePath}'.");
            }
        }

        static void ValidatePlanetaryGenerationProfile(FarionValidationReport report)
        {
            PlanetaryGenerationProfile generation =
                LoadRequired<PlanetaryGenerationProfile>(
                    PlanetaryGenerationProfilePath,
                    report);
            if (generation == null)
            {
                return;
            }

            List<PlanetGenerationValidationIssue> issues = new();
            generation.CollectValidationIssues(64f, 9.81f, issues);
            for (int i = 0; i < issues.Count; i++)
            {
                PlanetGenerationValidationIssue issue = issues[i];
                string message = $"{PlanetaryGenerationProfilePath}: {issue}";
                if (issue.Severity == PlanetGenerationValidationSeverity.Error)
                {
                    report.AddError(message);
                }
                else
                {
                    report.AddWarning(message);
                }
            }

            TerrestrialSurfaceProfile surface =
                AssetDatabase.LoadAssetAtPath<TerrestrialSurfaceProfile>(
                    TerrestrialSurfaceProfilePath);
            SurfaceVisualProfile visual = surface != null
                ? surface.SurfaceVisualProfile
                : null;
            SurfaceMaterialDistributionProfile materials =
                generation.SurfaceMaterialDistribution;
            if (visual == null || materials == null)
            {
                return;
            }

            ValidateMaterialHasVisual(materials.FallbackMaterial, visual, "fallback", report);
            IReadOnlyList<SurfaceMaterialDistributionRule> rules = materials.Rules;
            for (int i = 0; i < rules.Count; i++)
            {
                SurfaceMaterialDistributionRule rule = rules[i];
                if (rule != null)
                {
                    ValidateMaterialHasVisual(rule.Material, visual, $"rule {i}", report);
                }
            }
        }

        static void ValidateMaterialHasVisual(
            SurfaceMaterialDefinition material,
            SurfaceVisualProfile visual,
            string source,
            FarionValidationReport report)
        {
            if (material != null && visual.ResolveMaterialIndex(material) < 0)
            {
                report.AddError(
                    $"{PlanetaryGenerationProfilePath}: surface material {source} " +
                    $"'{material.name}' has no visual rule.");
            }
        }

        static void ValidateStarVisualProfile(FarionValidationReport report)
        {
            CelestialStarVisualProfile star =
                LoadRequired<CelestialStarVisualProfile>(
                    StarVisualProfilePath,
                    report);
            if (star == null)
            {
                return;
            }

            ValidateMaterial(
                star.Material,
                StarShaderName,
                StarVisualProfilePath,
                report);
            if (star.UseScaledSpace &&
                star.ScaledSpaceDistance >= star.PhysicalRenderDistance)
            {
                report.AddWarning(
                    $"{StarVisualProfilePath}: scaled-space distance should normally be " +
                    "lower than physical render distance.");
            }
        }

        static void ValidateSurfaceProfile(
            SurfaceVisualProfile profile,
            FarionValidationReport report)
        {
            IReadOnlyList<SurfaceVisualRule> rules = profile.Rules;
            if (rules == null || rules.Count == 0)
            {
                report.AddError($"{AssetDatabase.GetAssetPath(profile)}: no surface-material rules.");
                return;
            }

            HashSet<UnityEngine.Object> assignedMaterials = new();
            for (int i = 0; i < rules.Count; i++)
            {
                SurfaceVisualRule rule = rules[i];
                string scope = $"{AssetDatabase.GetAssetPath(profile)}: rule {i}";
                if (rule == null || !rule.IsValid)
                {
                    report.AddError($"{scope} has no surface material definition.");
                    continue;
                }

                if (!assignedMaterials.Add(rule.Material))
                {
                    report.AddError(
                        $"{scope} duplicates surface material '{rule.Material.name}'.");
                }

                SurfaceTextureSet textures = rule.Textures;
                if (textures == null || !textures.HasSurfaceTextures)
                {
                    report.AddError(
                        $"{scope} requires base-color, normal, and roughness maps.");
                    continue;
                }

                ValidateTexture(
                    textures.BaseColor,
                    TextureImporterType.Default,
                    expectedSrgb: true,
                    requireSquare: true,
                    $"{scope} base color",
                    report);
                ValidateTexture(
                    textures.Normal,
                    TextureImporterType.NormalMap,
                    expectedSrgb: false,
                    requireSquare: true,
                    $"{scope} normal",
                    report);
                ValidateTexture(
                    textures.Roughness,
                    TextureImporterType.Default,
                    expectedSrgb: false,
                    requireSquare: true,
                    $"{scope} roughness",
                    report);

                if (textures.AmbientOcclusion != null)
                {
                    ValidateTexture(
                        textures.AmbientOcclusion,
                        TextureImporterType.Default,
                        expectedSrgb: false,
                        requireSquare: true,
                        $"{scope} ambient occlusion",
                        report);
                }

                if (textures.Height != null)
                {
                    ValidateTexture(
                        textures.Height,
                        TextureImporterType.Default,
                        expectedSrgb: false,
                        requireSquare: true,
                        $"{scope} height",
                        report);
                }

                if (textures.Emission != null)
                {
                    ValidateTexture(
                        textures.Emission,
                        TextureImporterType.Default,
                        expectedSrgb: true,
                        requireSquare: true,
                        $"{scope} emission",
                        report);
                }
            }
        }

        static void ValidateBodyVisual(
            CelestialBodyVisual visual,
            string scenePath,
            FarionValidationReport report)
        {
            SerializedObject serialized = new(visual);
            string scope = $"{scenePath}: {GetHierarchyPath(visual.transform)}";
            ValidateObjectReference(serialized, "surfaceProfile", scope, report);
            ValidateObjectReference(serialized, "shapeProfile", scope, report);
            ValidateObjectReference(serialized, "lodProfile", scope, report);
        }

        static void ValidatePlanetVisual(
            TerrestrialPlanetVisual visual,
            string scenePath,
            FarionValidationReport report)
        {
            string scope = $"{scenePath}: {GetHierarchyPath(visual.transform)}";
            if (visual.Profile == null)
            {
                report.AddError($"{scope} has no terrestrial visual profile.");
            }

            SerializedObject serialized = new(visual);
            ValidateObjectReference(serialized, "terrainVisual", scope, report);
            ValidateObjectReference(serialized, "surfaceModel", scope, report);
        }

        static void ValidateSurfacePatchSystem(
            CelestialSurfacePatchSystem patchSystem,
            string scenePath,
            FarionValidationReport report)
        {
            string scope = $"{scenePath}: {GetHierarchyPath(patchSystem.transform)}";
            SerializedObject serialized = new(patchSystem);
            ValidateObjectReference(serialized, "profile", scope, report);
            ValidateObjectReference(serialized, "bodyVisual", scope, report);
            ValidateObjectReference(serialized, "targetCamera", scope, report);
            ValidateObjectReference(serialized, "collisionObserverSource", scope, report);

            MonoBehaviour collisionObserverSource =
                serialized.FindProperty("collisionObserverSource")?.objectReferenceValue
                    as MonoBehaviour;
            if (collisionObserverSource != null &&
                collisionObserverSource is not ICelestialSurfaceCollisionObserver)
            {
                report.AddError(
                    $"{scope} collisionObserverSource must implement " +
                    $"{nameof(ICelestialSurfaceCollisionObserver)}.");
            }

            CelestialBodyVisual ownerVisual =
                patchSystem.GetComponent<CelestialBodyVisual>();
            CelestialBodyVisual assignedVisual =
                serialized.FindProperty("bodyVisual")?.objectReferenceValue
                    as CelestialBodyVisual;
            if (ownerVisual == null || assignedVisual != ownerVisual)
            {
                report.AddError(
                    $"{scope} bodyVisual must reference the CelestialBodyVisual " +
                    "on the same body root.");
                return;
            }

            if (ownerVisual.Body != null &&
                !ownerVisual.Body.SupportsNonConvexSurfaceCollider)
            {
                report.AddError(
                    $"{scope} adaptive surface collision requires a kinematic " +
                    "celestial body; dynamic N-body collision remains spherical.");
            }

            GravitySimulation[] simulations =
                UnityEngine.Object.FindObjectsByType<GravitySimulation>(
                    FindObjectsInactive.Include);
            GravitySimulation sceneSimulation = null;
            for (int i = 0; i < simulations.Length; i++)
            {
                if (simulations[i] != null &&
                    simulations[i].gameObject.scene == patchSystem.gameObject.scene)
                {
                    sceneSimulation = simulations[i];
                    break;
                }
            }

            if (sceneSimulation == null)
            {
                report.AddError(
                    $"{scope} has no GravitySimulation in its scene.");
            }
            else if (ownerVisual.Body != null &&
                sceneSimulation.PhysicsReferenceBody != ownerVisual.Body)
            {
                report.AddError(
                    $"{scope} owns local terrain collision but its body is not " +
                    "the GravitySimulation physics reference body. Moving a " +
                    "non-convex planetary surface under actors is unsupported.");
            }
        }

        static void ValidateStarVisual(
            CelestialStarVisual visual,
            string scenePath,
            FarionValidationReport report)
        {
            string scope = $"{scenePath}: {GetHierarchyPath(visual.transform)}";
            if (visual.Profile == null)
            {
                report.AddError($"{scope} has no star visual profile.");
            }

            SerializedObject serialized = new(visual);
            ValidateObjectReference(serialized, "lightSource", scope, report);
            ValidateObjectReference(serialized, "starRenderer", scope, report);
            ValidateObjectReference(serialized, "observerCamera", scope, report);

            SerializedProperty transformProperty =
                serialized.FindProperty("starVisualTransform");
            Transform starTransform =
                transformProperty?.objectReferenceValue as Transform;
            if (starTransform == null)
            {
                report.AddError($"{scope} has no star visual transform.");
            }
            else if (starTransform == visual.transform ||
                     !starTransform.IsChildOf(visual.transform))
            {
                report.AddError(
                    $"{scope} star visual transform must be a child of the physical star.");
            }
        }

        static void ValidateTexture(
            Texture2D texture,
            TextureImporterType expectedType,
            bool expectedSrgb,
            bool requireSquare,
            string scope,
            FarionValidationReport report,
            TextureWrapMode expectedWrap = TextureWrapMode.Repeat)
        {
            if (texture == null)
            {
                report.AddError($"{scope} is missing.");
                return;
            }

            string path = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer =
                AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                report.AddError($"{scope} has no texture importer: {path}");
                return;
            }

            if (importer.textureType != expectedType)
            {
                report.AddError(
                    $"{scope} must import as {expectedType}, found {importer.textureType}.");
            }

            if (importer.sRGBTexture != expectedSrgb)
            {
                report.AddError(
                    $"{scope} sRGB must be {expectedSrgb}, found {importer.sRGBTexture}.");
            }

            if (!importer.mipmapEnabled ||
                importer.wrapMode != expectedWrap ||
                importer.filterMode != FilterMode.Trilinear ||
                importer.anisoLevel < 4)
            {
                report.AddError(
                    $"{scope} must use mipmaps, {expectedWrap}, Trilinear, " +
                    "and anisotropy >= 4.");
            }

            if (requireSquare && texture.width != texture.height)
            {
                report.AddError(
                    $"{scope} must be square, found {texture.width}x{texture.height}.");
            }
        }

        static void ValidateMaterial(
            Material material,
            string expectedShader,
            string scope,
            FarionValidationReport report)
        {
            if (material == null)
            {
                report.AddError($"{scope}: material is missing.");
                return;
            }

            if (material.shader == null ||
                !string.Equals(
                    material.shader.name,
                    expectedShader,
                    StringComparison.Ordinal))
            {
                string actualShader =
                    material.shader != null ? material.shader.name : "<missing>";
                report.AddError(
                    $"{scope}: expected shader '{expectedShader}', found '{actualShader}'.");
            }
        }

        static void ValidateShaderReference(
            SerializedObject serialized,
            string propertyName,
            string expectedShader,
            string scope,
            FarionValidationReport report)
        {
            Shader shader =
                serialized.FindProperty(propertyName)?.objectReferenceValue as Shader;
            if (shader == null ||
                !string.Equals(shader.name, expectedShader, StringComparison.Ordinal))
            {
                string actualShader = shader != null ? shader.name : "<missing>";
                report.AddError(
                    $"{scope}: {propertyName} must use '{expectedShader}', " +
                    $"found '{actualShader}'.");
            }
        }

        static void ValidateIntegerProperty(
            SerializedObject serialized,
            string propertyName,
            int expectedValue,
            string scope,
            FarionValidationReport report)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.intValue != expectedValue)
            {
                string actualValue =
                    property != null ? property.intValue.ToString() : "<missing>";
                report.AddError(
                    $"{scope}: {propertyName} must be {expectedValue}, " +
                    $"found {actualValue}.");
            }
        }

        static void ValidateObjectReference(
            SerializedObject serialized,
            string propertyName,
            string scope,
            FarionValidationReport report)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property?.objectReferenceValue == null)
            {
                report.AddError($"{scope} has no {propertyName} reference.");
            }
        }

        static int FindFeatureIndex<T>(
            IReadOnlyList<ScriptableRendererFeature> features)
            where T : ScriptableRendererFeature
        {
            for (int i = 0; i < features.Count; i++)
            {
                if (features[i] is T)
                {
                    return i;
                }
            }

            return -1;
        }

        static T LoadRequired<T>(
            string path,
            FarionValidationReport report)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                report.AddError($"{path}: required asset is missing.");
            }

            return asset;
        }

        static string GetHierarchyPath(Transform target)
        {
            string path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = $"{target.name}/{path}";
            }

            return path;
        }
    }
}
