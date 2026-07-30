namespace Farion.UI.Foundation
{
    public enum UiScreenId
    {
        None = 0,

        MainMenu = 10,
        Settings = 20,
        Credits = 30,
        SaveLoad = 40,

        GameplayHud = 100,
        PauseMenu = 110,
        Inventory = 120,

        Confirmation = 900,
        Loading = 910,
        Feedback = 920
    }

    public enum UiScreenLayer
    {
        Hud = 0,
        Screen = 100,
        Modal = 200,
        System = 300
    }
}
