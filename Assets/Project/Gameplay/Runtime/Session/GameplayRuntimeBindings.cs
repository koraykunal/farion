using System.Collections.Generic;
using System.Collections.ObjectModel;
using Farion.Core.Physics;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Research;
using Farion.Gameplay.Resources;
using Farion.Gameplay.Ships;
using Farion.Simulation.World;

namespace Farion.Gameplay.Session
{
    public sealed class GameplayRuntimeBindings
    {
        readonly ReadOnlyCollection<ResourceDepositRuntimeSpawner> resourceStreamers;

        public GameplayRuntimeBindings(
            GameplayDefinitionRegistry definitions,
            GravitySimulation gravitySimulation,
            WorldOriginRebaser originRebaser,
            PlayerInventory localPlayerInventory,
            PlayerPossessionController possession,
            FleetProgressionRuntime fleetProgression,
            IReadOnlyList<ResourceDepositRuntimeSpawner> resourceStreamers)
        {
            Definitions = definitions;
            GravitySimulation = gravitySimulation;
            OriginRebaser = originRebaser;
            LocalPlayerInventory = localPlayerInventory;
            Possession = possession;
            FleetProgression = fleetProgression;
            PersonalShip = ResolvePersonalShip(possession);
            this.resourceStreamers = new List<ResourceDepositRuntimeSpawner>(
                resourceStreamers ?? System.Array.Empty<ResourceDepositRuntimeSpawner>())
                .AsReadOnly();
        }

        public GameplayDefinitionRegistry Definitions { get; }
        public GravitySimulation GravitySimulation { get; }
        public WorldOriginRebaser OriginRebaser { get; }
        public PlayerInventory LocalPlayerInventory { get; }
        public PlayerPossessionController Possession { get; }
        public FleetProgressionRuntime FleetProgression { get; }
        public PersonalShipRuntimeBinding PersonalShip { get; }
        public IReadOnlyList<ResourceDepositRuntimeSpawner> ResourceStreamers =>
            resourceStreamers;
        public bool IsValid =>
            Definitions != null &&
            GravitySimulation != null &&
            OriginRebaser != null &&
            LocalPlayerInventory != null &&
            Possession != null &&
            FleetProgression != null &&
            PersonalShip != null &&
            PersonalShip.HasValidAuthoring &&
            HasNoMissingResourceStreamers();

        public bool Matches(GameplayRuntimeBindings other)
        {
            if (other == null ||
                !ReferenceEquals(Definitions, other.Definitions) ||
                !ReferenceEquals(GravitySimulation, other.GravitySimulation) ||
                !ReferenceEquals(OriginRebaser, other.OriginRebaser) ||
                !ReferenceEquals(LocalPlayerInventory, other.LocalPlayerInventory) ||
                !ReferenceEquals(Possession, other.Possession) ||
                !ReferenceEquals(FleetProgression, other.FleetProgression) ||
                !ReferenceEquals(PersonalShip, other.PersonalShip) ||
                resourceStreamers.Count != other.resourceStreamers.Count)
            {
                return false;
            }

            for (int i = 0; i < resourceStreamers.Count; i++)
            {
                if (!ReferenceEquals(
                        resourceStreamers[i],
                        other.resourceStreamers[i]))
                {
                    return false;
                }
            }

            return true;
        }

        bool HasNoMissingResourceStreamers()
        {
            for (int i = 0; i < resourceStreamers.Count; i++)
            {
                if (resourceStreamers[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

        static PersonalShipRuntimeBinding ResolvePersonalShip(
            PlayerPossessionController possession)
        {
            SpacecraftMotor motor = possession != null
                ? possession.SpacecraftMotor
                : null;
            return motor != null
                ? motor.GetComponent<PersonalShipRuntimeBinding>()
                : null;
        }
    }
}
