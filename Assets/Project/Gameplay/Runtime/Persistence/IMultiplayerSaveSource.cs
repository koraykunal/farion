using System.Collections.Generic;
using Farion.Gameplay.Definitions;

namespace Farion.Gameplay.Persistence
{
    public interface IMultiplayerSaveSource
    {
        void Capture(
            List<MultiplayerPlayerSaveEntry> players,
            List<MultiplayerShipSaveEntry> shipCargo);

        void Apply(
            IReadOnlyList<MultiplayerPlayerSaveEntry> players,
            IReadOnlyList<MultiplayerShipSaveEntry> shipCargo,
            GameplayDefinitionRegistry definitions);
    }
}
