using Farion.Core.Identity;
using Farion.Core.Persistence;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Persistence;
using Farion.Gameplay.ResourceNodes;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;
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
        public void ShuttleSystemsParticipantAcceptsSavesWithoutFuelAndHull()
        {
            GameplaySaveData legacy =
                JsonUtility.FromJson<GameplaySaveData>(
                    "{\"schemaVersion\":8,\"savedAtUtc\":\"2026-07-30T00:00:00Z\"}");
            GameObject shipObject = new("Shuttle Systems Save Test");
            Farion.Gameplay.Flight.SpacecraftFlightProfile profile =
                ScriptableObject.CreateInstance<
                    Farion.Gameplay.Flight.SpacecraftFlightProfile>();
            try
            {
                ShuttleRuntimeBinding shuttle =
                    shipObject.AddComponent<ShuttleRuntimeBinding>();
                TestFieldAccess.SetField(shuttle.Motor, "flightProfile", profile);
                GameplaySaveContext context = new(
                    new GameplayRuntimeBindings(
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        shuttle,
                        null));
                ShuttleSystemsSaveParticipant participant = new();

                shuttle.Motor.RefillFuel();

                Assert.That(legacy.ShuttleFuel.Capacity, Is.EqualTo(0f));
                Assert.That(legacy.ShuttleHull.Capacity, Is.EqualTo(0f));
                Assert.That(participant.CanApply(legacy, context), Is.True);
                Assert.That(participant.Apply(legacy, context), Is.True);
                Assert.That(shuttle.Motor.FuelNormalized, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(shipObject);
                Object.DestroyImmediate(profile);
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

        [Test]
        public void GameplaySessionIdentityKeepsPlayerActorShuttleAndInventoryDistinct()
        {
            PersistentEntityId inventoryId =
                new("inventory.player.explorer");

            bool created = GameplaySessionIdentity.TryCreate(
                "player.local",
                "player.explorer",
                "fleet.local",
                "ship.starter",
                inventoryId,
                out GameplaySessionIdentity identity);

            Assert.That(created, Is.True);
            Assert.That(identity.IsValid, Is.True);
            Assert.That(identity.LocalPlayerId.Value, Is.EqualTo("player.local"));
            Assert.That(identity.ExplorerActorId.Value, Is.EqualTo("player.explorer"));
            Assert.That(identity.FleetId.Value, Is.EqualTo("fleet.local"));
            Assert.That(
                identity.AssignedShuttleId.Value,
                Is.EqualTo("ship.starter"));
            Assert.That(identity.CarriedInventoryId, Is.EqualTo(inventoryId));
        }

        [Test]
        public void GameplaySessionIdentityRejectsInvalidCrossReferences()
        {
            bool created = GameplaySessionIdentity.TryCreate(
                "player.local",
                "player explorer",
                "fleet.local",
                "ship.starter",
                new PersistentEntityId("inventory.player.explorer"),
                out _);

            Assert.That(created, Is.False);
        }
    }
}
