using UnityEngine;

namespace Farion.Core.Persistence
{
    [DisallowMultipleComponent]
    public sealed class PersistentObjectId : MonoBehaviour
    {
        [SerializeField] string persistentId;

        public string Id => Normalize(persistentId);
        public bool HasId => !string.IsNullOrEmpty(Id);

        public void SetId(string id)
        {
            persistentId = Normalize(id);
        }

        void OnValidate()
        {
            persistentId = Normalize(persistentId);
        }

        public static string Normalize(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        }
    }
}
