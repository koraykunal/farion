using System.Collections.Generic;
using UnityEngine;

namespace Farion.Simulation.Planetary
{
    [CreateAssetMenu(menuName = "Farion/Simulation/Planetary/Biome Definition", fileName = "SO_Biome")]
    public sealed class BiomeDefinition : ScriptableObject
    {
        [SerializeField] string biomeId = "biome.temperate";
        [SerializeField] string displayName = "Temperate";
        [SerializeField] Color previewColor = new(0.25f, 0.55f, 0.28f, 1f);

        [Header("Physical Compatibility")]
        [SerializeField] List<PlanetType> allowedPlanetTypes = new();
        [SerializeField] Vector2 radiusRange = new(0f, 1000000f);
        [SerializeField] Vector2 surfaceGravityRange = new(0f, 1000f);
        [SerializeField] bool requiresAtmosphere;

        public string BiomeId => string.IsNullOrWhiteSpace(biomeId) ? name : biomeId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? BiomeId : displayName.Trim();
        public Color PreviewColor => previewColor;

        public bool IsCompatibleWith(PlanetGenerationContext context)
        {
            return AllowsPlanetType(context.PlanetType) &&
                Contains(radiusRange, context.Radius) &&
                Contains(surfaceGravityRange, context.SurfaceGravity) &&
                (!requiresAtmosphere || context.HasAtmosphere);
        }

        public void CollectValidationIssues(
            PlanetGenerationContext context,
            List<PlanetGenerationValidationIssue> issues,
            string source)
        {
            if (issues == null)
            {
                return;
            }

            string issueSource = $"{source}: {DisplayName}";
            if (!AllowsPlanetType(context.PlanetType))
            {
                issues.Add(PlanetGenerationValidationIssue.Error(
                    issueSource,
                    $"Planet type {context.PlanetType} is not allowed for biome {BiomeId}."));
            }

            if (!Contains(radiusRange, context.Radius))
            {
                issues.Add(PlanetGenerationValidationIssue.Error(
                    issueSource,
                    $"Body radius {context.Radius:0.###} is outside biome range {radiusRange.x:0.###}-{radiusRange.y:0.###}."));
            }

            if (!Contains(surfaceGravityRange, context.SurfaceGravity))
            {
                issues.Add(PlanetGenerationValidationIssue.Error(
                    issueSource,
                    $"Surface gravity {context.SurfaceGravity:0.###} is outside biome range {surfaceGravityRange.x:0.###}-{surfaceGravityRange.y:0.###}."));
            }

            if (requiresAtmosphere && !context.HasAtmosphere)
            {
                issues.Add(PlanetGenerationValidationIssue.Error(
                    issueSource,
                    $"Biome {BiomeId} requires an atmosphere."));
            }

        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(biomeId))
            {
                biomeId = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = biomeId;
            }

            NormalizeRange(ref radiusRange, 0f, 1000000f);
            NormalizeRange(ref surfaceGravityRange, 0f, 1000f);
            allowedPlanetTypes ??= new List<PlanetType>();
        }

        bool AllowsPlanetType(PlanetType planetType)
        {
            return allowedPlanetTypes == null || allowedPlanetTypes.Count == 0 || allowedPlanetTypes.Contains(planetType);
        }

        static bool Contains(Vector2 range, float value)
        {
            return value >= range.x && value <= range.y;
        }

        static void NormalizeRange(ref Vector2 range, float min, float max)
        {
            range.x = Mathf.Clamp(range.x, min, max);
            range.y = Mathf.Clamp(range.y, min, max);
            if (range.y < range.x)
            {
                range.y = range.x;
            }
        }
    }
}
