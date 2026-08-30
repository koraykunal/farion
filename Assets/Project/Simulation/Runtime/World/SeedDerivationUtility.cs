using Farion.Core.Identity;

namespace Farion.Simulation.World
{
    public static class SeedDerivationUtility
    {
        const int GenerationSeedVersion = 1;

        public static GeneratedEntityId DeriveId(
            ulong universeSeed,
            UniverseEntityKind kind,
            string stream,
            long slot = 0)
        {
            ulong hash = StableHashUtility.Begin();
            hash = StableHashUtility.Mix(hash, universeSeed);
            hash = StableHashUtility.Mix(hash, GenerationSeedVersion);
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
