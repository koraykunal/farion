namespace Farion.Simulation.Planetary
{
    public readonly struct PlanetGenerationValidationIssue
    {
        public PlanetGenerationValidationIssue(
            PlanetGenerationValidationSeverity severity,
            string source,
            string message)
        {
            Severity = severity;
            Source = source;
            Message = message;
        }

        public PlanetGenerationValidationSeverity Severity { get; }
        public string Source { get; }
        public string Message { get; }

        public static PlanetGenerationValidationIssue Warning(string source, string message)
        {
            return new PlanetGenerationValidationIssue(PlanetGenerationValidationSeverity.Warning, source, message);
        }

        public static PlanetGenerationValidationIssue Error(string source, string message)
        {
            return new PlanetGenerationValidationIssue(PlanetGenerationValidationSeverity.Error, source, message);
        }

        public override string ToString()
        {
            return $"{Severity}: {Source} - {Message}";
        }
    }
}
