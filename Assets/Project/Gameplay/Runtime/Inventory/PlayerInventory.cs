using Farion.Gameplay.Definitions;
using UnityEngine;

namespace Farion.Gameplay.Inventory
{
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : InventoryContainerComponent
    {
        public PlayerInventorySnapshot CaptureSnapshot()
        {
            return new PlayerInventorySnapshot(
                SlotCapacity,
                CaptureStackSnapshot());
        }

        public void ApplySnapshot(
            PlayerInventorySnapshot snapshot,
            GameplayDefinitionRegistry definitions)
        {
            if (snapshot == null)
            {
                return;
            }

            TryApplyStackSnapshot(
                snapshot.SlotCapacity,
                snapshot.Stacks,
                definitions,
                notify: true);
        }

        public bool CanApplySnapshot(
            PlayerInventorySnapshot snapshot,
            GameplayDefinitionRegistry definitions)
        {
            return snapshot != null &&
                   CanApplyStackSnapshot(
                       snapshot.SlotCapacity,
                       snapshot.Stacks,
                       definitions);
        }
    }
}
