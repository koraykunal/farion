using Farion.Gameplay.Inventory;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Farion.Gameplay.Ships
{
    [MovedFrom(
        true,
        "Farion.Gameplay.Ships",
        "Farion.Gameplay.Runtime",
        "PersonalShipCargoInventory")]
    [DisallowMultipleComponent]
    public sealed class ShuttleCargoInventory : InventoryContainerComponent
    {
        protected override string ContainerIdNamespace => "ship_cargo";
    }
}
