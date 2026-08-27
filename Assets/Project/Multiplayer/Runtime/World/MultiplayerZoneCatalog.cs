using Farion.Core.Identity;

namespace Farion.Multiplayer.World
{
    public static class MultiplayerZoneCatalog
    {
        public static readonly GeneratedEntityId StartingZoneId =
            GeneratedEntityId.FromHash(
                StableHashUtility.Combine("zone.starting_system"));

        public static GeneratedEntityId ZoneIdForBody(
            int bodyStableId,
            int startingBodyStableId)
        {
            return bodyStableId != 0 && bodyStableId == startingBodyStableId
                ? StartingZoneId
                : GeneratedEntityId.FromHash(
                    StableHashUtility.Combine(
                        (ulong)(uint)bodyStableId,
                        "zone.body"));
        }
    }
}
