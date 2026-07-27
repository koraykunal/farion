namespace Farion.Gameplay.Flight
{
    static class SpacecraftTouchdownEvaluator
    {
        public static bool IsSafe(
            SpacecraftSurfaceContactSample contact,
            float surfaceSlopeAngle,
            float maximumVerticalSpeed,
            float maximumTangentialSpeed,
            float maximumSlopeAngle)
        {
            return contact.HasContact &&
                contact.NormalSpeed <= maximumVerticalSpeed &&
                contact.TangentialSpeed <= maximumTangentialSpeed &&
                surfaceSlopeAngle <= maximumSlopeAngle;
        }
    }
}
