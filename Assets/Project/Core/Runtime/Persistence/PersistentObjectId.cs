using UnityEngine;

namespace Farion.Core.Persistence
{
    [DisallowMultipleComponent]
    public sealed class PersistentObjectId : MonoBehaviour
    {
        [SerializeField] string persistentId;

        public string Id => Normalize(persistentId);
        public bool HasId => IsValid(Id);

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

        public static bool IsValid(string id)
        {
            string normalized = Normalize(id);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            for (int i = 0; i < normalized.Length; i++)
            {
                if (char.IsWhiteSpace(normalized[i]) || char.IsControl(normalized[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
