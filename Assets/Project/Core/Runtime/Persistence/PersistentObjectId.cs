using Farion.Core.Identity;
using UnityEngine;

namespace Farion.Core.Persistence
{
    [DisallowMultipleComponent]
    public sealed class PersistentObjectId : MonoBehaviour
    {
        [SerializeField] string persistentId;

        public string Id => IdentifierText.Normalize(persistentId);
        public bool HasId => IdentifierText.IsValid(Id);

        public bool TryGetEntityId(out PersistentEntityId entityId)
        {
            return PersistentEntityId.TryCreate(persistentId, out entityId);
        }

        public void SetId(string id)
        {
            persistentId = IdentifierText.Normalize(id);
        }

        void OnValidate()
        {
            persistentId = IdentifierText.Normalize(persistentId);
        }

        public static string Normalize(string id)
        {
            return IdentifierText.Normalize(id);
        }

        public static bool IsValid(string id)
        {
            return IdentifierText.IsValidRaw(id);
        }
    }
}
