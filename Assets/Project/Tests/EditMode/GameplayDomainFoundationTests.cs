using System.Reflection;
using System.Collections.Generic;
using Farion.Core.Identity;
using Farion.Core.Persistence;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Domain.Economy;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Domain.Systems;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;
using NUnit.Framework;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class GameplayDomainFoundationTests
    {
        [Test]
        public void ResourcePoolNeverDrainsBelowEmptyAndReportsWhatItSupplied()
        {
            ResourcePool pool = ResourcePool.Full(10f);

            Assert.That(pool.Drain(4f), Is.EqualTo(4f));
            Assert.That(pool.Current, Is.EqualTo(6f));
            Assert.That(pool.Drain(100f), Is.EqualTo(6f));
            Assert.That(pool.IsEmpty, Is.True);
            Assert.That(pool.Drain(1f), Is.EqualTo(0f));
            Assert.That(pool.Normalized, Is.EqualTo(0f));
        }

        [Test]
        public void ResourcePoolNeverFillsAboveCapacity()
        {
            ResourcePool pool = ResourcePool.Drained(10f);

            Assert.That(pool.Fill(4f), Is.EqualTo(4f));
            Assert.That(pool.Fill(100f), Is.EqualTo(6f));
            Assert.That(pool.IsFull, Is.True);
            Assert.That(pool.Fill(1f), Is.EqualTo(0f));
        }

        [Test]
        public void ShrinkingCapacityClampsTheStoredAmount()
        {
            ResourcePool pool = ResourcePool.Full(10f);
            pool.SetCapacity(4f);

            Assert.That(pool.Current, Is.EqualTo(4f));
            Assert.That(pool.Normalized, Is.EqualTo(1f));

            pool.SetCapacity(0f);
            Assert.That(pool.Normalized, Is.EqualTo(0f));
        }

        [Test]
        public void ModuleBonusesAccumulateAdditivelyAndStayNonNegative()
        {
            ShipModuleBonuses combined = new ShipModuleBonuses(0.2f, 0.1f, 0.5f)
                .Combine(new ShipModuleBonuses(0.3f, 0.1f, 0.5f));

            Assert.That(combined.MaxSpeedMultiplier, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(combined.BoostSpeedMultiplier, Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(combined.FuelCapacityMultiplier, Is.EqualTo(2f).Within(0.0001f));

            ShipModuleBonuses crippled = new(-5f, 0f, 0f);
            Assert.That(crippled.MaxSpeedMultiplier, Is.EqualTo(0f));
            Assert.That(ShipModuleBonuses.None.MaxSpeedMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void DefinitionIdsNormalizeOuterWhitespaceAndRejectEmbeddedWhitespace()
        {
            Assert.That(
                DefinitionId.TryCreate("  item.iron_ore  ", out DefinitionId definitionId),
                Is.True);
            Assert.That(definitionId.Value, Is.EqualTo("item.iron_ore"));
            Assert.That(DefinitionId.TryCreate("item.iron ore", out _), Is.False);
            Assert.That(DefinitionId.TryCreate("\t", out _), Is.False);
        }

        [Test]
        public void InventoryExchangeCanReuseSlotsFreedByItsInputs()
        {
            DefinitionId oreId = new("item.ore");
            DefinitionId ingotId = new("item.ingot");
            InventoryContainerState inventory = CreateContainer("container.player", 1);
            Assert.That(
                inventory.TryAddStack(oreId, 10, 10),
                Is.EqualTo(InventoryOperationResult.Succeeded));

            InventoryOperationResult result = inventory.TryApplyStackChanges(
                new[]
                {
                    new InventoryStackChange(oreId, -10, 10),
                    new InventoryStackChange(ingotId, 1, 20)
                },
                inventory.Revision);

            Assert.That(result, Is.EqualTo(InventoryOperationResult.Succeeded));
            Assert.That(inventory.Count(oreId), Is.Zero);
            Assert.That(inventory.Count(ingotId), Is.EqualTo(1));
            Assert.That(inventory.UsedSlots, Is.EqualTo(1));
        }

        [Test]
        public void FailedInventoryTransactionDoesNotMutateStateOrRevision()
        {
            DefinitionId oreId = new("item.ore");
            DefinitionId ingotId = new("item.ingot");
            InventoryContainerState inventory = CreateContainer("container.player", 1);
            inventory.TryAddStack(oreId, 10, 10);
            long revision = inventory.Revision;

            InventoryOperationResult result = inventory.TryApplyStackChanges(
                new[]
                {
                    new InventoryStackChange(oreId, -9, 10),
                    new InventoryStackChange(ingotId, 1, 20)
                },
                revision);

            Assert.That(result, Is.EqualTo(InventoryOperationResult.InsufficientCapacity));
            Assert.That(inventory.Count(oreId), Is.EqualTo(10));
            Assert.That(inventory.Count(ingotId), Is.Zero);
            Assert.That(inventory.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void InventoryTransferIsAtomicAcrossCapacityFailureAndSuccess()
        {
            DefinitionId oreId = new("item.ore");
            DefinitionId ingotId = new("item.ingot");
            InventoryContainerState source = CreateContainer("container.player", 1);
            InventoryContainerState fullCargo = CreateContainer("container.full_cargo", 1);
            InventoryContainerState emptyCargo = CreateContainer("container.empty_cargo", 1);
            source.TryAddStack(oreId, 10, 10);
            fullCargo.TryAddStack(ingotId, 10, 10);
            long sourceRevision = source.Revision;
            long fullCargoRevision = fullCargo.Revision;

            Assert.That(
                InventoryTransferService.TryTransferAllStacks(source, fullCargo),
                Is.EqualTo(InventoryOperationResult.InsufficientCapacity));
            Assert.That(source.Count(oreId), Is.EqualTo(10));
            Assert.That(fullCargo.Count(oreId), Is.Zero);
            Assert.That(source.Revision, Is.EqualTo(sourceRevision));
            Assert.That(fullCargo.Revision, Is.EqualTo(fullCargoRevision));

            Assert.That(
                InventoryTransferService.TryTransferAllStacks(source, emptyCargo),
                Is.EqualTo(InventoryOperationResult.Succeeded));
            Assert.That(source.Count(oreId), Is.Zero);
            Assert.That(emptyCargo.Count(oreId), Is.EqualTo(10));
        }

        [Test]
        public void CancelledInventoryTransactionDoesNotAdvanceRevision()
        {
            DefinitionId oreId = new("item.ore");
            InventoryContainerState inventory = CreateContainer("container.player", 1);
            inventory.TryAddStack(oreId, 5, 10);
            long revision = inventory.Revision;

            InventoryOperationResult result = inventory.TryApplyStackChanges(
                new[]
                {
                    new InventoryStackChange(oreId, -1, 10),
                    new InventoryStackChange(oreId, 1, 10)
                },
                revision);

            Assert.That(result, Is.EqualTo(InventoryOperationResult.Succeeded));
            Assert.That(inventory.Count(oreId), Is.EqualTo(5));
            Assert.That(inventory.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void InventoryRejectsNegativePublicQuantitiesAndStaleCommands()
        {
            DefinitionId oreId = new("item.ore");
            InventoryContainerState inventory = CreateContainer("container.player", 1);

            Assert.That(
                inventory.TryAddStack(oreId, -1, 10),
                Is.EqualTo(InventoryOperationResult.InvalidQuantity));
            Assert.That(
                inventory.TryRemoveStack(oreId, -1, 10),
                Is.EqualTo(InventoryOperationResult.InvalidQuantity));

            inventory.TryAddStack(oreId, 1, 10, expectedRevision: 0);
            Assert.That(
                inventory.TryRemoveStack(oreId, 1, 10, expectedRevision: 0),
                Is.EqualTo(InventoryOperationResult.StaleRevision));
            Assert.That(inventory.Count(oreId), Is.EqualTo(1));
        }

        [Test]
        public void PlayerInventoryAdaptsStackOperationsToDomainState()
        {
            GameObject owner = new("PlayerInventoryTest");
            InventoryItemDefinition item =
                ScriptableObject.CreateInstance<InventoryItemDefinition>();
            try
            {
                PersistentObjectId ownerId = owner.AddComponent<PersistentObjectId>();
                ownerId.SetId("player.test");
                PlayerInventory inventory = owner.AddComponent<PlayerInventory>();

                Assert.That(inventory.ContainerId.Value, Is.EqualTo("inventory.player.test"));
                Assert.That(inventory.TryAdd(item, 25), Is.EqualTo(25));
                Assert.That(inventory.Count(item), Is.EqualTo(25));
                Assert.That(inventory.Stacks.Count, Is.EqualTo(2));
                Assert.That(inventory.Revision, Is.GreaterThan(0));
                Assert.That(inventory.TryRemove(item, 30), Is.EqualTo(25));
                Assert.That(inventory.Count(item), Is.Zero);
                Assert.That(inventory.Stacks, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ShuttleCargoSnapshotRestoresStackState()
        {
            GameObject owner = new("ShuttleCargoSnapshot");
            InventoryItemDefinition item =
                ScriptableObject.CreateInstance<InventoryItemDefinition>();
            GameplayDefinitionRegistry registry =
                ScriptableObject.CreateInstance<GameplayDefinitionRegistry>();
            try
            {
                owner.AddComponent<PersistentObjectId>().SetId("ship.snapshot");
                ShuttleCargoInventory cargo =
                    owner.AddComponent<ShuttleCargoInventory>();
                SetPrivateField(item, "itemId", "item.cargo_snapshot");
                SetPrivateField(
                    registry,
                    "inventoryItems",
                    new List<InventoryItemDefinition> { item });

                Assert.That(cargo.TryAdd(item, 6), Is.EqualTo(6));
                InventoryContainerSnapshot snapshot =
                    cargo.CaptureContainerSnapshot();
                Assert.That(cargo.TryRemove(item, 6), Is.EqualTo(6));
                Assert.That(cargo.Count(item), Is.Zero);

                Assert.That(
                    cargo.ApplyContainerSnapshot(snapshot, registry),
                    Is.True);
                Assert.That(cargo.Count(item), Is.EqualTo(6));
                Assert.That(snapshot.ContainerId, Is.EqualTo(cargo.ContainerId.Value));
            }
            finally
            {
                Object.DestroyImmediate(registry);
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(owner);
            }
        }

        static InventoryContainerState CreateContainer(string id, int slotCapacity)
        {
            return new InventoryContainerState(
                new PersistentEntityId(id),
                slotCapacity);
        }

        static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(
                field,
                Is.Not.Null,
                $"Missing private field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
