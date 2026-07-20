namespace Farion.Gameplay.Flight
{
    public readonly struct SpacecraftEntryCorridorSample
    {
        public SpacecraftEntryCorridorSample(
            SpacecraftEntryCorridorState state,
            float entryAngleDegrees,
            float speedToEscapeRatio,
            float speedToCircularRatio,
            float periapsisAltitude,
            float atmosphereDepth,
            float normalizedRisk,
            string advisory)
        {
            State = state;
            EntryAngleDegrees = entryAngleDegrees;
            SpeedToEscapeRatio = speedToEscapeRatio;
            SpeedToCircularRatio = speedToCircularRatio;
            PeriapsisAltitude = periapsisAltitude;
            AtmosphereDepth = atmosphereDepth;
            NormalizedRisk = normalizedRisk;
            Advisory = advisory;
        }

        public SpacecraftEntryCorridorState State { get; }
        public bool HasFrame => State != SpacecraftEntryCorridorState.NoFrame;
        public float EntryAngleDegrees { get; }
        public float SpeedToEscapeRatio { get; }
        public float SpeedToCircularRatio { get; }
        public float PeriapsisAltitude { get; }
        public float AtmosphereDepth { get; }
        public float NormalizedRisk { get; }
        public string Advisory { get; }

        public static SpacecraftEntryCorridorSample NoFrame => new(
            SpacecraftEntryCorridorState.NoFrame,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            "NO FRAME");
    }
}
