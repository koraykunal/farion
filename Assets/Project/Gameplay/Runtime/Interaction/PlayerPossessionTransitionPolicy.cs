namespace Farion.Gameplay.Interaction
{
    public static class PlayerPossessionTransitionPolicy
    {
        public static bool CanTransition(
            PlayerPossessionMode currentMode,
            PlayerPossessionMode nextMode,
            PlayerPossessionTransitionRequest request)
        {
            if (!IsSupportedMode(nextMode))
            {
                return false;
            }

            if (currentMode == nextMode)
            {
                return request == PlayerPossessionTransitionRequest.Bootstrap ||
                       request == PlayerPossessionTransitionRequest.RestoreSnapshot;
            }

            switch (request)
            {
                case PlayerPossessionTransitionRequest.Bootstrap:
                case PlayerPossessionTransitionRequest.RestoreSnapshot:
                    return true;
                case PlayerPossessionTransitionRequest.EnterPilotSeat:
                    return nextMode == PlayerPossessionMode.Spacecraft &&
                           currentMode == PlayerPossessionMode.ShipInterior;
                case PlayerPossessionTransitionRequest.ExitPilotSeat:
                    return currentMode == PlayerPossessionMode.Spacecraft &&
                           (nextMode == PlayerPossessionMode.ShipInterior ||
                            nextMode == PlayerPossessionMode.OnFoot);
                case PlayerPossessionTransitionRequest.EnterShipInterior:
                    return currentMode == PlayerPossessionMode.OnFoot &&
                           nextMode == PlayerPossessionMode.ShipInterior;
                case PlayerPossessionTransitionRequest.ExitShipInterior:
                    return currentMode == PlayerPossessionMode.ShipInterior &&
                           nextMode == PlayerPossessionMode.OnFoot;
                default:
                    return false;
            }
        }

        public static bool IsSupportedMode(PlayerPossessionMode mode)
        {
            return mode == PlayerPossessionMode.Spacecraft ||
                   mode == PlayerPossessionMode.OnFoot ||
                   mode == PlayerPossessionMode.ShipInterior;
        }
    }
}
