using System.Collections.Generic;
using Farion.Core.Persistence;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Persistence;
using Farion.Multiplayer.Spawning;
using Farion.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class InventorySnapshotTests
    {
        GameObject sourceRoot;
        GameObject targetRoot;
        InventoryItemDefinition ore;
        GameplayDefinitionRegistry registry;

        [SetUp]
        public void SetUp()
        {
            ore = ScriptableObject.CreateInstance<InventoryItemDefinition>();
            TestFieldAccess.SetField(ore, "itemId", "item.snapshot_ore");
            registry = ScriptableObject.CreateInstance<GameplayDefinitionRegistry>();
            TestFieldAccess.SetField(
                registry,
                "inventoryItems",
                new List<InventoryItemDefinition> { ore });
            sourceRoot = new GameObject("Source");
            sourceRoot.AddComponent<PersistentObjectId>().SetId("explorer.net.2");
            targetRoot = new GameObject("Target");
            targetRoot.AddComponent<PersistentObjectId>().SetId("explorer.net.3");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(sourceRoot);
            Object.DestroyImmediate(targetRoot);
            Object.DestroyImmediate(registry);
            Object.DestroyImmediate(ore);
        }

        [Test]
        public void SnapshotFromAnotherSessionIdRestoresOnlyWhenRetargeted()
        {
            PlayerInventory source = sourceRoot.AddComponent<PlayerInventory>();
            PlayerInventory target = targetRoot.AddComponent<PlayerInventory>();
            Assert.That(source.TryAdd(ore, 7), Is.EqualTo(7));
            InventoryContainerSnapshot snapshot = source.CaptureContainerSnapshot();

            Assert.That(target.ApplyContainerSnapshot(snapshot, registry), Is.False);
            Assert.That(target.Count(ore), Is.Zero);

            Assert.That(
                target.ApplyContainerSnapshot(
                    snapshot.WithContainerId(target.ContainerId.Value),
                    registry),
                Is.True);
            Assert.That(target.Count(ore), Is.EqualTo(7));
            Assert.That(target.Revision, Is.EqualTo(snapshot.Revision));
        }

        [Test]
        public void LegacySnapshotWithoutRevisionStillApplies()
        {
            PlayerInventory target = targetRoot.AddComponent<PlayerInventory>();
            InventoryContainerSnapshot legacy = new(
                target.ContainerId.Value,
                4,
                new[] { new InventoryStackSnapshot("item.snapshot_ore", 3) });

            Assert.That(legacy.HasRevision, Is.False);
            Assert.That(target.ApplyContainerSnapshot(legacy, registry), Is.True);
            Assert.That(target.Count(ore), Is.EqualTo(3));
            Assert.That(target.SlotCapacity, Is.EqualTo(4));
        }

        [Test]
        public void RestoreValidationRejectsUnknownItemsBeforeAnythingIsApplied()
        {
            MultiplayerPlayerSaveEntry known = new(
                "player-a",
                "A",
                0,
                new InventoryContainerSnapshot(
                    "inventory.explorer.net.9",
                    4,
                    new[] { new InventoryStackSnapshot("item.snapshot_ore", 2) }));
            MultiplayerPlayerSaveEntry unknown = new(
                "player-b",
                "B",
                1,
                new InventoryContainerSnapshot(
                    "inventory.explorer.net.10",
                    4,
                    new[] { new InventoryStackSnapshot("item.removed", 2) }));

            Assert.That(
                MultiplayerPlayerSpawner.CanRestore(new[] { known }, null, registry),
                Is.True);
            Assert.That(
                MultiplayerPlayerSpawner.CanRestore(new[] { known, unknown }, null, registry),
                Is.False);
            Assert.That(
                MultiplayerPlayerSpawner.CanRestore(
                    null,
                    new[]
                    {
                        new MultiplayerShipSaveEntry(
                            "player-a",
                            0,
                            unknown.CarriedInventory,
                            default,
                            default)
                    },
                    registry),
                Is.False);
        }
    }
}
