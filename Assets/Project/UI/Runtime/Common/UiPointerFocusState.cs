namespace Farion.UI.Common
{
    public struct UiPointerFocusState
    {
        bool pointerInside;
        bool pointerOwnsSelection;
        bool selected;

        public bool IsFocused => pointerInside || selected && !pointerOwnsSelection;

        public void PointerEnter()
        {
            pointerInside = true;
        }

        public void PointerExit()
        {
            pointerInside = false;
        }

        public void PointerClick()
        {
            pointerOwnsSelection = true;
        }

        public void Select()
        {
            selected = true;
        }

        public void Deselect()
        {
            pointerOwnsSelection = false;
            selected = false;
        }

        public void Reset()
        {
            pointerInside = false;
            pointerOwnsSelection = false;
            selected = false;
        }
    }
}
