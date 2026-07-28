using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace Farion.UI.Localization
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class UiLocalizedText : MonoBehaviour
    {
        [SerializeField] TMP_Text target;
        [SerializeField] string entryKey;
        [TextArea]
        [SerializeField] string fallback;

        LocalizedString localizedString;

        public string EntryKey => entryKey;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            if (Application.isPlaying)
            {
                Bind();
            }
        }

        void OnDisable()
        {
            Unbind();
        }

        void OnValidate()
        {
            ResolveReferences();
            if (!Application.isPlaying && target != null && !string.IsNullOrWhiteSpace(fallback))
            {
                target.text = fallback;
            }
        }

        public void Configure(string key, string fallbackValue)
        {
            entryKey = key;
            fallback = fallbackValue;

            if (!isActiveAndEnabled)
            {
                Apply(fallback);
                return;
            }

            Unbind();
            Bind();
        }

        void Bind()
        {
            ResolveReferences();
            Unbind();

            if (string.IsNullOrWhiteSpace(entryKey))
            {
                Apply(fallback);
                return;
            }

            localizedString = new LocalizedString(UiLocalization.TableName, entryKey);
            localizedString.StringChanged += ApplyLocalized;
        }

        void Unbind()
        {
            if (localizedString == null)
            {
                return;
            }

            localizedString.StringChanged -= ApplyLocalized;
            localizedString = null;
        }

        void ResolveReferences()
        {
            target ??= GetComponent<TMP_Text>();
        }

        void ApplyLocalized(string value)
        {
            Apply(string.IsNullOrWhiteSpace(value) ? fallback : value);
        }

        void Apply(string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }
    }
}
