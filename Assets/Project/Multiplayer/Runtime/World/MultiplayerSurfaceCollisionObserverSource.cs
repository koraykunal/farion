using System.Collections.Generic;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Spacecraft;
using Farion.Simulation.Physics;
using UnityEngine;

namespace Farion.Multiplayer.World
{
    [DisallowMultipleComponent]
    public sealed class MultiplayerSurfaceCollisionObserverSource :
        MonoBehaviour,
        ICelestialSurfaceCollisionObserverGroup
    {
        public void GetSurfaceCollisionObservers(
            List<CelestialSurfaceCollisionObserverState> results)
        {
            IReadOnlyList<NetworkStarterShuttle> ships = NetworkStarterShuttle.ActiveShips;
            for (int i = 0; i < ships.Count; i++)
            {
                if (ships[i] != null &&
                    ships[i].gameObject.scene == gameObject.scene &&
                    ships[i].TryGetSurfaceCollisionObserver(
                        out CelestialSurfaceCollisionObserverState shipObserver))
                {
                    results.Add(shipObserver);
                }
            }

            IReadOnlyList<NetworkExplorerController> explorers =
                NetworkExplorerController.ActiveExplorers;
            for (int i = 0; i < explorers.Count; i++)
            {
                if (explorers[i] != null &&
                    explorers[i].gameObject.scene == gameObject.scene &&
                    explorers[i].TryGetSurfaceCollisionObserver(
                        out CelestialSurfaceCollisionObserverState explorerObserver))
                {
                    results.Add(explorerObserver);
                }
            }
        }
    }
}
