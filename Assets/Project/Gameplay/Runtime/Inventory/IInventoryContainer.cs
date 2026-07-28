using System;
using System.Collections.Generic;
using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Inventory
{
    public interface IInventoryContainer
    {
        event Action Changed;

        PersistentEntityId ContainerId { get; }
        long Revision { get; }
        IReadOnlyList<InventoryStack> Stacks { get; }
        int SlotCapacity { get; }
        bool IsFull { get; }

        bool CanAdd(InventoryItemDefinition item, int amount);
        int TryAdd(InventoryItemDefinition item, int amount);
        bool CanRemove(InventoryItemDefinition item, int amount);
        int TryRemove(InventoryItemDefinition item, int amount);
        bool CanRemoveAll(IReadOnlyList<ItemStackDefinition> costs);
        bool TryRemoveAll(IReadOnlyList<ItemStackDefinition> costs);
        bool CanExchange(
            IReadOnlyList<ItemStackDefinition> inputs,
            IReadOnlyList<ItemStackDefinition> outputs);
        bool TryExchange(
            IReadOnlyList<ItemStackDefinition> inputs,
            IReadOnlyList<ItemStackDefinition> outputs);
        int Count(InventoryItemDefinition item);
        int GetAvailableCapacity(InventoryItemDefinition item);
    }
}
