namespace Farion.Gameplay.Flight
{
    public readonly struct SpacecraftLandingGuidanceSample
    {
        public SpacecraftLandingGuidanceSample(
            SpacecraftLandingGuidanceLevel level,
            SpacecraftLandingGuidanceCommand command,
            string advisory,
            float verticalSpeedRatio,
            float tangentialSpeedRatio,
            float stress)
        {
            Level = level;
            Command = command;
            Advisory = advisory;
            VerticalSpeedRatio = verticalSpeedRatio;
            TangentialSpeedRatio = tangentialSpeedRatio;
            Stress = stress;
        }

        public SpacecraftLandingGuidanceLevel Level { get; }
        public SpacecraftLandingGuidanceCommand Command { get; }
        public string Advisory { get; }
        public float VerticalSpeedRatio { get; }
        public float TangentialSpeedRatio { get; }
        public float Stress { get; }

        public static SpacecraftLandingGuidanceSample Offline => new(
            SpacecraftLandingGuidanceLevel.Offline,
            SpacecraftLandingGuidanceCommand.None,
            "NO FRAME",
            0f,
            0f,
            0f);
    }
}
