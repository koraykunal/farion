using Farion.Core.Identity;
using Farion.Core.Persistence;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Persistence;
using Farion.Gameplay.ResourceNodes;
using Farion.Simulation.World;
using Farion.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class PersistenceAndIdentityTests
    {
        [Test]
        public void CurrentSaveVersionIsAccepted()
        {
            Assert.That(
                SaveGameSchema.IsSupportedVersion(SaveGameSchema.CurrentVersion),
                Is.True);
        }

        [TestCase(0)]
        [TestCase(SaveGameSchema.CurrentVersion - 1)]
        [TestCase(SaveGameSchema.CurrentVersion + 1)]
        public void OtherSaveVersionsAreRejected(int version)
        {
            Assert.That(SaveGameSchema.IsSupportedVersion(version), Is.False);
        }

        [Test]
        public void PersistentObjectIdRejectsEmbeddedWhitespaceLikeDomainIdentity()
        {
            GameObject owner = new("Invalid Persistent Id");
            try
            {
                PersistentObjectId objectId = owner.AddComponent<PersistentObjectId>();
                objectId.SetId("player invalid");

                Assert.That(objectId.HasId, Is.False);
                Assert.That(PersistentEntityId.TryCreate(objectId.Id, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ChildEntityIdsAreDeterministicAndParentScoped()
        {
            GeneratedEntityId parentA = SeedDerivationUtility.DeriveId(
                1234UL,
                UniverseEntityKind.CelestialBody,
                "planet-a");
            GeneratedEntityId parentB = SeedDerivationUtility.DeriveId(
                1234UL,
                UniverseEntityKind.CelestialBody,
                "planet-b");

            GeneratedEntityId first = SeedDerivationUtility.DeriveChildId(
                parentA,
                UniverseEntityKind.ResourceDeposit,
                "iron",
                42);
            GeneratedEntityId repeated = SeedDerivationUtility.DeriveChildId(
                parentA,
                UniverseEntityKind.ResourceDeposit,
                "iron",
                42);
            GeneratedEntityId otherPlanet = SeedDerivationUtility.DeriveChildId(
                parentB,
                UniverseEntityKind.ResourceDeposit,
                "iron",
                42);

            Assert.That(first, Is.EqualTo(repeated));
            Assert.That(first, Is.Not.EqualTo(otherPlanet));
        }

        [Test]
        public void ResourceDeltaStoreReplacesStateWhenApplyingSnapshot()
        {
            ResourceDepositDeltaStore store = new();
            GeneratedEntityId oldId = new(10UL);
            GeneratedEntityId newId = new(20UL);
            store.RecordExtraction(oldId, 4, 0);

            store.ApplySnapshot(new[]
            {
                new ResourceDepositDeltaSnapshot(newId, 7)
            });

            Assert.That(store.GetExtractedAmount(oldId), Is.Zero);
            Assert.That(store.GetExtractedAmount(newId), Is.EqualTo(7));
        }
    }
}
