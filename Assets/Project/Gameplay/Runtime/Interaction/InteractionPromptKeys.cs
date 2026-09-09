namespace Farion.Gameplay.Interaction
{
    public static class InteractionPromptKeys
    {
        public const char ArgumentSeparator = '|';

        public const string Harvest = "interact.harvest";
        public const string EnterShip = "interact.enter_ship";
        public const string PilotSeat = "interact.pilot_seat";
        public const string ToggleRamp = "interact.toggle_ramp";
        public const string LoadCargo = "interact.load_cargo";
        public const string UnloadCargo = "interact.unload_cargo";
        public const string ProcessMaterials = "interact.process_materials";
        public const string ProcessRecipe = "interact.process_recipe";

        public static string WithArgument(string key, string argument)
        {
            return string.IsNullOrWhiteSpace(argument)
                ? key
                : key + ArgumentSeparator + argument.Trim();
        }
    }
}
