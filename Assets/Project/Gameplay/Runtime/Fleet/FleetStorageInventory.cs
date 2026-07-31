using Farion.Gameplay.Inventory;
using UnityEngine;

namespace Farion.Gameplay.Fleet
{
    [DisallowMultipleComponent]
    public sealed class FleetStorageInventory : InventoryContainerComponent
    {
        protected override string ContainerIdNamespace => "fleet_storage";
    }
}
