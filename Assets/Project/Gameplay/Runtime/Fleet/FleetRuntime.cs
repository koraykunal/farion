using Farion.Core.Identity;
using Farion.Core.Persistence;
using Farion.Gameplay.Domain.Identity;
using UnityEngine;

namespace Farion.Gameplay.Fleet
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PersistentObjectId))]
    [RequireComponent(typeof(FleetStorageInventory))]
    public sealed class FleetRuntime : MonoBehaviour
    {
        [SerializeField] PersistentObjectId persistentId;
        [SerializeField] FleetStorageInventory storage;

        public PersistentEntityId FleetId
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

        public FleetStorageInventory Storage
        {
            get
            {
                ResolveReferences();
                return storage;
            }
        }

        public bool HasValidAuthoring
        {
            get
            {
                ResolveReferences();
                return FleetId.IsValid &&
                       storage != null &&
                       storage.gameObject == gameObject &&
                       PersistentEntityId.TryCreate(
                           $"fleet_storage.{FleetId.Value}",
                           out PersistentEntityId expectedStorageId) &&
                       storage.ContainerId == expectedStorageId;
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
            storage ??= GetComponent<FleetStorageInventory>();
        }
    }
}
