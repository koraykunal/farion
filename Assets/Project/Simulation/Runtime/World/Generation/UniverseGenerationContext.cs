using System;
using Farion.Simulation.World.Identity;

namespace Farion.Simulation.World.Generation
{
    public readonly struct UniverseGenerationContext : IEquatable<UniverseGenerationContext>
    {
        public UniverseGenerationContext(ulong universeSeed, GenerationVersion generationVersion)
        {
            SeedDerivationUtility.ValidateGenerationVersion(generationVersion);

            UniverseSeed = universeSeed;
            GenerationVersion = generationVersion;
        }

        public ulong UniverseSeed { get; }
        public GenerationVersion GenerationVersion { get; }

        public ulong DeriveSeed(string stream)
        {
            return SeedDerivationUtility.DeriveSeed(UniverseSeed, GenerationVersion, stream);
        }

        public GeneratedEntityId DeriveEntityId(UniverseEntityKind kind, string stream, long slot = 0)
        {
            return SeedDerivationUtility.DeriveId(UniverseSeed, GenerationVersion, kind, stream, slot);
        }

        public static bool operator ==(UniverseGenerationContext left, UniverseGenerationContext right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(UniverseGenerationContext left, UniverseGenerationContext right)
        {
            return !left.Equals(right);
        }

        public bool Equals(UniverseGenerationContext other)
        {
            return UniverseSeed == other.UniverseSeed && GenerationVersion.Equals(other.GenerationVersion);
        }

        public override bool Equals(object obj)
        {
            return obj is UniverseGenerationContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (UniverseSeed.GetHashCode() * 397) ^ GenerationVersion.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"seed={UniverseSeed} generationVersion={GenerationVersion}";
        }
    }
}
