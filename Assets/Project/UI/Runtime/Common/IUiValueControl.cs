using System;
using UnityEngine.UI;

namespace Farion.UI.Common
{
    public interface IUiValueControl
    {
        event Action<IUiValueControl> Focused;

        string DisplayTitle { get; }
        string DisplayDescription { get; }
        string DisplayValue { get; }
        Selectable Selectable { get; }

        void SetAvailable(bool available);
        void SetPending(bool pending);
    }
}
