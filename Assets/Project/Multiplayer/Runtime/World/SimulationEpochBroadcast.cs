using FishNet.Broadcast;

namespace Farion.Multiplayer.World
{
    public struct SimulationEpochBroadcast : IBroadcast
    {
        public double EpochSeconds;

        public SimulationEpochBroadcast(double epochSeconds)
        {
            EpochSeconds = epochSeconds;
        }
    }
}
