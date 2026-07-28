using Farion.Gameplay.Inventory;
using UnityEngine;

namespace Farion.Gameplay.Ships
{
    [DisallowMultipleComponent]
    public sealed class PersonalShipCargoInventory : InventoryContainerComponent
    {
        protected override string ContainerIdNamespace => "ship_cargo";
    }
}
