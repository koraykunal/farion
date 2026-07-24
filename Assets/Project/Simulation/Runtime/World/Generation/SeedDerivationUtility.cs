using Farion.Simulation.World.Identity;

namespace Farion.Simulation.World.Generation
{
    public static class SeedDerivationUtility
    {
        public static void ValidateGenerationVersion(GenerationVersion generationVersion)
        {
            if (generationVersion.Value <= 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(generationVersion),
                    "Generation version must be positive before deriving persistent seeds or ids.");
            }
        }

        public static ulong DeriveSeed(ulong parentSeed, string stream)
        {
            return StableHashUtility.NormalizeNonZero(StableHashUtility.Combine(parentSeed, stream));
        }

        public static ulong DeriveSeed(ulong universeSeed, GenerationVersion generationVersion, string stream)
        {
            ValidateGenerationVersion(generationVersion);

            ulong hash = StableHashUtility.Begin();
            hash = StableHashUtility.Mix(hash, universeSeed);
            hash = StableHashUtility.Mix(hash, generationVersion.Value);
            hash = StableHashUtility.Mix(hash, stream);
            return StableHashUtility.NormalizeNonZero(hash);
        }

        public static GeneratedEntityId DeriveId(
            ulong universeSeed,
            GenerationVersion generationVersion,
            UniverseEntityKind kind,
            string stream,
            long slot = 0)
        {
            ValidateGenerationVersion(generationVersion);

            ulong hash = StableHashUtility.Begin();
            hash = StableHashUtility.Mix(hash, universeSeed);
            hash = StableHashUtility.Mix(hash, generationVersion.Value);
            hash = StableHashUtility.Mix(hash, (int)kind);
            hash = StableHashUtility.Mix(hash, stream);
            hash = StableHashUtility.Mix(hash, slot);
            return GeneratedEntityId.FromHash(hash);
        }

        public static GeneratedEntityId DeriveChildId(
            GeneratedEntityId parentId,
            UniverseEntityKind kind,
            string stream,
            long slot = 0)
        {
            ulong hash = StableHashUtility.Begin();
            hash = StableHashUtility.Mix(hash, parentId.Value);
            hash = StableHashUtility.Mix(hash, (int)kind);
            hash = StableHashUtility.Mix(hash, stream);
            hash = StableHashUtility.Mix(hash, slot);
            return GeneratedEntityId.FromHash(hash);
        }
    }
}
