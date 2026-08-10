using Farion.Core.Identity;
using Farion.Core.Persistence;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Flight;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Farion.Gameplay.Ships
{
    [MovedFrom(
        true,
        "Farion.Gameplay.Ships",
        "Farion.Gameplay.Runtime",
        "PersonalShipRuntimeBinding")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PersistentObjectId))]
    [RequireComponent(typeof(SpacecraftMotor))]
    [RequireComponent(typeof(ShuttleCargoInventory))]
    public sealed class ShuttleRuntimeBinding : MonoBehaviour
    {
        [SerializeField] PersistentObjectId persistentId;
        [SerializeField] SpacecraftMotor motor;
        [SerializeField] ShuttleCargoInventory cargo;

        public bool HasValidAuthoring
        {
            get
            {
                ResolveReferences();
                return ShipId.IsValid &&
                       motor != null &&
                       cargo != null &&
                       motor.gameObject == gameObject &&
                       cargo.gameObject == gameObject;
            }
        }

        public SpacecraftMotor Motor
        {
            get
            {
                ResolveReferences();
                return motor;
            }
        }

        public ShuttleCargoInventory Cargo
        {
            get
            {
                ResolveReferences();
                return cargo;
            }
        }

        public PersistentEntityId ShipId
        {
            get
            {
                ResolveReferences();
                return persistentId != null &&
                       PersistentEntityId.TryCreate(
                           persistentId.Id,
                           out PersistentEntityId id)
                    ? id
                    : PersistentEntityId.None;
            }
        }

        void Awake()
        {
            ResolveReferences();
        }

        void OnValidate()
        {
            ResolveReferences();
        }

        void ResolveReferences()
        {
            persistentId ??= GetComponent<PersistentObjectId>();
            motor ??= GetComponent<SpacecraftMotor>();
            cargo ??= GetComponent<ShuttleCargoInventory>();
        }
    }
}
