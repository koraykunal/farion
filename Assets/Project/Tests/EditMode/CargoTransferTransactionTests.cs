using Farion.Core.Persistence;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Ships;
using Farion.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class CargoTransferTransactionTests
    {
        GameObject explorerRoot;
        GameObject shipRoot;
        InventoryItemDefinition ore;

        [SetUp]
        public void SetUp()
        {
            ore = ScriptableObject.CreateInstance<InventoryItemDefinition>();
            TestFieldAccess.SetField(ore, "itemId", "item.test_ore");
            TestFieldAccess.SetField(ore, "maxStackSize", 10);
            explorerRoot = new GameObject("Explorer");
            explorerRoot.AddComponent<PersistentObjectId>().SetId("explorer.cargo_test");
            shipRoot = new GameObject("Ship");
            shipRoot.AddComponent<PersistentObjectId>().SetId("ship.cargo_test");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(explorerRoot);
            Object.DestroyImmediate(shipRoot);
            Object.DestroyImmediate(ore);
        }

        [Test]
        public void PartialCapacityLeavesBothContainersUntouched()
        {
            PlayerInventory explorer = explorerRoot.AddComponent<PlayerInventory>();
            ShuttleCargoInventory cargo = shipRoot.AddComponent<ShuttleCargoInventory>();
            TestFieldAccess.SetField(cargo, "slotCapacity", 1);
            Assert.That(explorer.TryAdd(ore, 15), Is.EqualTo(15));
            long explorerRevision = explorer.Revision;
            long cargoRevision = cargo.Revision;

            Assert.That(
                CargoTransferTransaction.CanExecute(explorer, cargo),
                Is.EqualTo(CargoTransferResult.InsufficientCapacity));
            Assert.That(
                CargoTransferTransaction.TryExecute(explorer, cargo),
                Is.EqualTo(CargoTransferResult.InsufficientCapacity));
            Assert.That(explorer.Count(ore), Is.EqualTo(15));
            Assert.That(cargo.Count(ore), Is.Zero);
            Assert.That(explorer.Revision, Is.EqualTo(explorerRevision));
            Assert.That(cargo.Revision, Is.EqualTo(cargoRevision));
        }

        [Test]
        public void CompleteTransferMovesEveryStackAndThenReportsEmptySource()
        {
            PlayerInventory explorer = explorerRoot.AddComponent<PlayerInventory>();
            ShuttleCargoInventory cargo = shipRoot.AddComponent<ShuttleCargoInventory>();
            TestFieldAccess.SetField(cargo, "slotCapacity", 2);
            Assert.That(explorer.TryAdd(ore, 15), Is.EqualTo(15));

            Assert.That(
                CargoTransferTransaction.CanExecute(explorer, cargo),
                Is.EqualTo(CargoTransferResult.Succeeded));
            Assert.That(
                CargoTransferTransaction.TryExecute(explorer, cargo),
                Is.EqualTo(CargoTransferResult.Succeeded));
            Assert.That(explorer.Count(ore), Is.Zero);
            Assert.That(cargo.Count(ore), Is.EqualTo(15));
            Assert.That(
                CargoTransferTransaction.CanExecute(explorer, cargo),
                Is.EqualTo(CargoTransferResult.EmptySource));
        }
    }
}
