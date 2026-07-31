using Farion.Core.Persistence;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Persistence;
using Farion.Gameplay.Resources;
using Farion.Gameplay.Session;
using Farion.Simulation.World;
using Farion.Simulation.World.Generation;
using Farion.Simulation.World.Identity;
using NUnit.Framework;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class PersistenceAndIdentityTests
    {
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void SupportedSaveVersionsAreAccepted(int version)
        {
            Assert.That(SaveGameSchema.IsSupportedVersion(version), Is.True);
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(7)]
        public void UnsupportedSaveVersionsAreRejected(int version)
        {
            Assert.That(SaveGameSchema.IsSupportedVersion(version), Is.False);
        }

        [Test]
        public void SchemaFourSaveMigratesWithExplicitCargoAndKnowledgeDefaults()
        {
            GameplaySaveData legacy =
                JsonUtility.FromJson<GameplaySaveData>(
                    "{\"schemaVersion\":4,\"savedAtUtc\":\"2026-07-30T00:00:00Z\"}");
            InventoryContainerSnapshot cargoDefault = new(
                "inventory.ship.starter.cargo",
                12,
                System.Array.Empty<InventoryStackSnapshot>());
            InventoryContainerSnapshot fleetStorageDefault = new(
                "fleet_storage.fleet.local",
                48,
                System.Array.Empty<InventoryStackSnapshot>());

            bool migrated = GameplaySaveMigration.TryMigrateToCurrent(
                legacy,
                cargoDefault,
                fleetStorageDefault,
                out GameplaySaveData current);

            Assert.That(migrated, Is.True);
            Assert.That(current.SchemaVersion, Is.EqualTo(6));
            Assert.That(current.SourceSchemaVersion, Is.EqualTo(4));
            Assert.That(current.ShuttleCargo.ContainerId,
                Is.EqualTo("inventory.ship.starter.cargo"));
            Assert.That(current.ShuttleCargo.Stacks, Is.Empty);
            Assert.That(current.FleetKnowledge, Is.Not.Null);
            Assert.That(current.FleetKnowledge.Capabilities, Is.Empty);
            Assert.That(
                current.FleetStorage.ContainerId,
                Is.EqualTo("fleet_storage.fleet.local"));
        }

        [Test]
        public void SchemaFiveSaveRejectsMissingRequiredAggregates()
        {
            GameplaySaveData incomplete =
                JsonUtility.FromJson<GameplaySaveData>(
                    "{\"schemaVersion\":5,\"savedAtUtc\":\"2026-07-30T00:00:00Z\"}");
            InventoryContainerSnapshot cargoDefault = new(
                "inventory.ship.starter.cargo",
                12,
                System.Array.Empty<InventoryStackSnapshot>());
            InventoryContainerSnapshot fleetStorageDefault = new(
                "fleet_storage.fleet.local",
                48,
                System.Array.Empty<InventoryStackSnapshot>());

            bool migrated = GameplaySaveMigration.TryMigrateToCurrent(
                incomplete,
                cargoDefault,
                fleetStorageDefault,
                out _);

            Assert.That(migrated, Is.False);
        }

        [Test]
        public void ChildEntityIdsAreDeterministicAndParentScoped()
        {
            GeneratedEntityId parentA = SeedDerivationUtility.DeriveId(
                1234UL,
                GenerationVersion.Current,
                UniverseEntityKind.CelestialBody,
                "planet-a");
            GeneratedEntityId parentB = SeedDerivationUtility.DeriveId(
                1234UL,
                GenerationVersion.Current,
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
        public void WorldOriginSnapshotPreservesRuntimeMetadata()
        {
            Vector3 offset = new(1200.5f, -4f, 980f);
            WorldOriginSnapshot snapshot = new(offset, 3);

            Assert.That(snapshot.IsSupported, Is.True);
            Assert.That(snapshot.AccumulatedOffset, Is.EqualTo(offset));
            Assert.That(snapshot.ShiftCount, Is.EqualTo(3));
        }

        [Test]
        public void ResourceDeltaStoreReplacesStateWhenApplyingSnapshot()
        {
            ResourceDepositDeltaStore store = new();
            GeneratedEntityId oldId = new(10UL);
            GeneratedEntityId newId = new(20UL);
            store.RecordExtraction(oldId, 4);

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
