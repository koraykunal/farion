using Farion.Core.Identity;
using Farion.Core.Persistence;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Persistence;
using Farion.Gameplay.ResourceNodes;
using Farion.Simulation.World;
using Farion.Tests.Support;
using Farion.Multiplayer.Session;
using NUnit.Framework;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class PersistenceAndIdentityTests
    {
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
        [TestCase("  abc-123.x_ ", "abc-123.x_")]
        [TestCase("bad id", "")]
        [TestCase("", "")]
        [TestCase("evil/../path", "")]
        public void PersistentIdsAreTrimmedAndRestrictedToSafeCharacters(string requested, string expected)
        {
            Assert.That(NetworkSessionPlayer.NormalizePersistentId(requested), Is.EqualTo(expected));
            Assert.That(NetworkSessionPlayer.NormalizePersistentId(new string('a', 65)), Is.Empty);
        }

        [TestCase("76561198000000000", true)]
        [TestCase("192.168.1.5", false)]
        [TestCase("7656119800000000", false)]
        [TestCase("76561198000000abc", false)]
        public void OnlyTransportSteamIdsCountAsVerifiedIdentity(string address, bool expected)
        {
            Assert.That(NetworkSessionPlayer.IsSteamId(address), Is.EqualTo(expected));
        }

        [Test]
        public void ReconnectProofIsAStableHashThatRejectsEmptyOrOversizedSecrets()
        {
            string proof = NetworkSessionPlayer.HashSecret("secret-a");
            Assert.That(proof, Has.Length.EqualTo(64));
            Assert.That(proof, Is.EqualTo(NetworkSessionPlayer.HashSecret(" secret-a ")));
            Assert.That(proof, Is.Not.EqualTo(NetworkSessionPlayer.HashSecret("secret-b")));
            Assert.That(NetworkSessionPlayer.HashSecret("   "), Is.Empty);
            Assert.That(NetworkSessionPlayer.HashSecret(new string('s', 129)), Is.Empty);
        }
    }
}
