using Farion.Gameplay.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class InventorySlotView : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text detailText;
        [SerializeField] TMP_Text quantityText;
        [SerializeField] Image iconImage;

        public void SetStack(InventoryStack stack)
        {
            bool hasItem = stack != null && !stack.IsEmpty && stack.Item != null;
            InventoryItemDefinition item = hasItem ? stack.Item : null;

            SetText(nameText, item != null ? item.DisplayName : string.Empty);
            SetText(detailText, item != null ? $"{item.Category} / {item.Form}" : string.Empty);
            SetText(quantityText, hasItem ? stack.Quantity.ToString() : string.Empty);

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
        }

        static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
