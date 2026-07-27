namespace Farion.Core.Persistence
{
    public static class SaveGameStartupRequest
    {
        static SaveGameStartupMode mode;
        static string slotName;

        public static void RequestNewGame()
        {
            mode = SaveGameStartupMode.NewGame;
            slotName = string.Empty;
        }

        public static void RequestLoad(string requestedSlotName)
        {
            mode = SaveGameStartupMode.LoadGame;
            slotName = SaveGameSlotCatalog.ResolveSlotName(requestedSlotName);
        }

        public static bool Consume(out SaveGameStartupMode requestedMode, out string requestedSlotName)
        {
            requestedMode = mode;
            requestedSlotName = slotName;
            mode = SaveGameStartupMode.None;
            slotName = string.Empty;
            return requestedMode != SaveGameStartupMode.None;
        }
    }
}
