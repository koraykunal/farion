using System.Collections.Generic;
using System.Reflection;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Crafting;
using Farion.Core.Persistence;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Domain.Economy;
using Farion.Gameplay.Domain.Fleet;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Equipment;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Research;
using Farion.Gameplay.Ships;
using NUnit.Framework;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class GameplayDomainFoundationTests
    {
        [Test]
        public void DefinitionIdsNormalizeOuterWhitespaceAndRejectEmbeddedWhitespace()
        {
            Assert.That(
                DefinitionId.TryCreate("  item.iron_ore  ", out DefinitionId definitionId),
                Is.True);
            Assert.That(definitionId.Value, Is.EqualTo("item.iron_ore"));
            Assert.That(DefinitionId.TryCreate("item.iron ore", out _), Is.False);
            Assert.That(DefinitionId.TryCreate("\t", out _), Is.False);
        }

        [Test]
        public void InventoryExchangeCanReuseSlotsFreedByItsInputs()
        {
            DefinitionId oreId = new("item.ore");
            DefinitionId ingotId = new("item.ingot");
            InventoryContainerState inventory = CreateContainer("container.player", 1);
            Assert.That(
                inventory.TryAddStack(oreId, 10, 10),
                Is.EqualTo(InventoryOperationResult.Succeeded));

            InventoryOperationResult result = inventory.TryApplyStackChanges(
                new[]
                {
                    new InventoryStackChange(oreId, -10, 10),
                    new InventoryStackChange(ingotId, 1, 20)
                },
                inventory.Revision);

            Assert.That(result, Is.EqualTo(InventoryOperationResult.Succeeded));
            Assert.That(inventory.Count(oreId), Is.Zero);
            Assert.That(inventory.Count(ingotId), Is.EqualTo(1));
            Assert.That(inventory.UsedSlots, Is.EqualTo(1));
        }

        [Test]
        public void FailedInventoryTransactionDoesNotMutateStateOrRevision()
        {
            DefinitionId oreId = new("item.ore");
            DefinitionId ingotId = new("item.ingot");
            InventoryContainerState inventory = CreateContainer("container.player", 1);
            inventory.TryAddStack(oreId, 10, 10);
            long revision = inventory.Revision;

            InventoryOperationResult result = inventory.TryApplyStackChanges(
                new[]
                {
                    new InventoryStackChange(oreId, -9, 10),
                    new InventoryStackChange(ingotId, 1, 20)
                },
                revision);

            Assert.That(result, Is.EqualTo(InventoryOperationResult.InsufficientCapacity));
            Assert.That(inventory.Count(oreId), Is.EqualTo(10));
            Assert.That(inventory.Count(ingotId), Is.Zero);
            Assert.That(inventory.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void CancelledInventoryTransactionDoesNotAdvanceRevision()
        {
            DefinitionId oreId = new("item.ore");
            InventoryContainerState inventory = CreateContainer("container.player", 1);
            inventory.TryAddStack(oreId, 5, 10);
            long revision = inventory.Revision;

            InventoryOperationResult result = inventory.TryApplyStackChanges(
                new[]
                {
                    new InventoryStackChange(oreId, -1, 10),
                    new InventoryStackChange(oreId, 1, 10)
                },
                revision);

            Assert.That(result, Is.EqualTo(InventoryOperationResult.Succeeded));
            Assert.That(inventory.Count(oreId), Is.EqualTo(5));
            Assert.That(inventory.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void InventoryRejectsNegativePublicQuantitiesAndStaleCommands()
        {
            DefinitionId oreId = new("item.ore");
            InventoryContainerState inventory = CreateContainer("container.player", 1);

            Assert.That(
                inventory.TryAddStack(oreId, -1, 10),
                Is.EqualTo(InventoryOperationResult.InvalidQuantity));
            Assert.That(
                inventory.TryRemoveStack(oreId, -1, 10),
                Is.EqualTo(InventoryOperationResult.InvalidQuantity));

            inventory.TryAddStack(oreId, 1, 10, expectedRevision: 0);
            Assert.That(
                inventory.TryRemoveStack(oreId, 1, 10, expectedRevision: 0),
                Is.EqualTo(InventoryOperationResult.StaleRevision));
            Assert.That(inventory.Count(oreId), Is.EqualTo(1));
        }

        [Test]
        public void FailedStackTransferLeavesBothContainersUnchanged()
        {
            DefinitionId oreId = new("item.ore");
            DefinitionId coolantId = new("item.coolant");
            InventoryContainerState source = CreateContainer("container.source", 1);
            InventoryContainerState destination = CreateContainer("container.destination", 1);
            source.TryAddStack(oreId, 4, 10);
            destination.TryAddStack(coolantId, 10, 10);
            long sourceRevision = source.Revision;
            long destinationRevision = destination.Revision;

            InventoryOperationResult result = InventoryTransferService.TryTransferStack(
                source,
                destination,
                oreId,
                2,
                10);

            Assert.That(result, Is.EqualTo(InventoryOperationResult.InsufficientCapacity));
            Assert.That(source.Count(oreId), Is.EqualTo(4));
            Assert.That(destination.Count(oreId), Is.Zero);
            Assert.That(source.Revision, Is.EqualTo(sourceRevision));
            Assert.That(destination.Revision, Is.EqualTo(destinationRevision));
        }

        [Test]
        public void UniqueEquipmentInstanceTransfersAtomically()
        {
            InventoryContainerState source = CreateContainer("container.source", 1);
            InventoryContainerState destination = CreateContainer("container.destination", 1);
            EquipmentRepositoryState repository = CreateRepository();
            EquipmentInstanceState equipment = CreateEquipment(
                "equipment.engine.001",
                "ENG-001",
                "equipment.engine");
            repository.TryRegister(equipment);
            EquipmentPlacementService.TryStoreUnassigned(
                repository,
                source,
                equipment.InstanceId);

            InventoryOperationResult result =
                InventoryTransferService.TryTransferItemInstance(
                    repository,
                    source,
                    destination,
                    equipment.InstanceId);

            Assert.That(result, Is.EqualTo(InventoryOperationResult.Succeeded));
            Assert.That(source.ContainsItemInstance(equipment.InstanceId), Is.False);
            Assert.That(destination.ContainsItemInstance(equipment.InstanceId), Is.True);
            Assert.That(
                repository.TryGetLocation(
                    equipment.InstanceId,
                    out EquipmentLocation location),
                Is.True);
            Assert.That(
                location,
                Is.EqualTo(EquipmentLocation.InContainer(destination.ContainerId)));
        }

        [Test]
        public void PersonalShipOwnsNameHullFuelAndModuleInvariants()
        {
            PersonalShipState ship = new(
                new PersistentEntityId("ship.personal.01"),
                new PersistentEntityId("player.01"),
                new PersistentEntityId("container.ship.01"),
                "  Far Horizon   One  ",
                maximumHull: 100d,
                currentHull: 100d,
                maximumFuel: 50d,
                currentFuel: 25d);

            Assert.That(ship.DisplayName, Is.EqualTo("Far Horizon One"));
            Assert.That(
                ship.TryConsumeFuel(10d),
                Is.EqualTo(FleetOperationResult.Succeeded));
            Assert.That(ship.FuelCurrent, Is.EqualTo(15d));

            ship.TryApplyDamage(200d, out double appliedDamage);
            Assert.That(appliedDamage, Is.EqualTo(100d));
            Assert.That(ship.IsDisabled, Is.True);
            Assert.That(
                ship.TryConsumeFuel(1d),
                Is.EqualTo(FleetOperationResult.ShipDisabled));

            ship.TryRepair(20d, out double acceptedRepair);
            Assert.That(acceptedRepair, Is.EqualTo(20d));
            Assert.That(ship.IsDisabled, Is.False);
        }

        [Test]
        public void FleetEnforcesFourMembersAndUniquePersonalShips()
        {
            FleetState fleet = new(
                new PersistentEntityId("fleet.01"),
                new PersistentEntityId("ship.capital.01"));

            for (int i = 1; i <= FleetState.MaximumMemberCount; i++)
            {
                Assert.That(
                    fleet.TryRegisterMember(
                        new PersistentEntityId($"player.{i}"),
                        new PersistentEntityId($"ship.personal.{i}")),
                    Is.EqualTo(FleetOperationResult.Succeeded));
            }

            Assert.That(
                fleet.TryRegisterMember(
                    new PersistentEntityId("player.5"),
                    new PersistentEntityId("ship.personal.5")),
                Is.EqualTo(FleetOperationResult.CapacityExceeded));
            Assert.That(
                fleet.TryAssignPersonalShip(
                    new PersistentEntityId("player.1"),
                    new PersistentEntityId("ship.personal.2")),
                Is.EqualTo(FleetOperationResult.Conflict));
        }

        [Test]
        public void CapitalShipCannotRemoveRoomContainingMachines()
        {
            CapitalShipState ship = new(
                new PersistentEntityId("ship.capital.01"),
                new PersistentEntityId("fleet.01"),
                new PersistentEntityId("container.capital.01"),
                "Endeavour",
                roomCapacity: 2);
            PersistentEntityId roomId = new("room.refinery.01");
            PersistentEntityId machineId = new("machine.refinery.01");

            ship.TryRegisterRoom(roomId);
            ship.TryRegisterMachine(machineId, roomId);
            Assert.That(
                ship.TryRemoveRoom(roomId),
                Is.EqualTo(FleetOperationResult.Conflict));

            Assert.That(
                ship.TryRemoveMachine(machineId),
                Is.EqualTo(FleetOperationResult.Succeeded));
            Assert.That(
                ship.TryRemoveRoom(roomId),
                Is.EqualTo(FleetOperationResult.Succeeded));
        }

        [Test]
        public void FleetKnowledgeIsMonotonicAndIdempotent()
        {
            FleetKnowledgeState knowledge = new();
            DefinitionId capabilityId = new("capability.titanium_processing");

            Assert.That(
                knowledge.TryUnlockCapability(capabilityId),
                Is.EqualTo(FleetOperationResult.Succeeded));
            long revision = knowledge.Revision;
            Assert.That(
                knowledge.TryUnlockCapability(capabilityId),
                Is.EqualTo(FleetOperationResult.AlreadyExists));
            Assert.That(knowledge.HasCapability(capabilityId), Is.True);
            Assert.That(knowledge.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void PlayerInventoryAdaptsStackOperationsToDomainState()
        {
            GameObject owner = new("PlayerInventoryTest");
            InventoryItemDefinition item =
                ScriptableObject.CreateInstance<InventoryItemDefinition>();
            try
            {
                PersistentObjectId ownerId = owner.AddComponent<PersistentObjectId>();
                ownerId.SetId("player.test");
                PlayerInventory inventory = owner.AddComponent<PlayerInventory>();

                Assert.That(inventory.ContainerId.Value, Is.EqualTo("inventory.player.test"));
                Assert.That(inventory.TryAdd(item, 25), Is.EqualTo(25));
                Assert.That(inventory.Count(item), Is.EqualTo(25));
                Assert.That(inventory.Stacks.Count, Is.EqualTo(2));
                Assert.That(inventory.Revision, Is.GreaterThan(0));
                Assert.That(inventory.TryRemove(item, 30), Is.EqualTo(25));
                Assert.That(inventory.Count(item), Is.Zero);
                Assert.That(inventory.Stacks, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void RuntimeInventoryTransferMovesStackAtomically()
        {
            GameObject sourceOwner = new("InventoryTransferSource");
            GameObject destinationOwner = new("InventoryTransferDestination");
            InventoryItemDefinition item =
                ScriptableObject.CreateInstance<InventoryItemDefinition>();
            try
            {
                sourceOwner.AddComponent<PersistentObjectId>().SetId("transfer.source");
                destinationOwner.AddComponent<PersistentObjectId>().SetId("transfer.destination");
                PlayerInventory source = sourceOwner.AddComponent<PlayerInventory>();
                PlayerInventory destination = destinationOwner.AddComponent<PlayerInventory>();
                SetPrivateField(item, "itemId", "item.transfer_test");

                Assert.That(source.TryAdd(item, 5), Is.EqualTo(5));
                InventoryTransferResult result =
                    InventoryTransferTransaction.TryExecute(
                        source,
                        destination,
                        item,
                        3);

                Assert.That(result, Is.EqualTo(InventoryTransferResult.Succeeded));
                Assert.That(source.Count(item), Is.EqualTo(2));
                Assert.That(destination.Count(item), Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(destinationOwner);
                Object.DestroyImmediate(sourceOwner);
            }
        }

        [Test]
        public void ResearchUnlockConsumesInputsAndPublishesFleetKnowledge()
        {
            GameObject owner = new("ResearchUnlockInventory");
            InventoryItemDefinition sample =
                ScriptableObject.CreateInstance<InventoryItemDefinition>();
            InventoryItemDefinition plate =
                ScriptableObject.CreateInstance<InventoryItemDefinition>();
            ResearchDefinition research =
                ScriptableObject.CreateInstance<ResearchDefinition>();
            RecipeDefinition recipe =
                ScriptableObject.CreateInstance<RecipeDefinition>();
            try
            {
                owner.AddComponent<PersistentObjectId>().SetId("research.inventory");
                PlayerInventory inventory = owner.AddComponent<PlayerInventory>();
                SetPrivateField(sample, "itemId", "item.thermal_sample");
                SetPrivateField(plate, "itemId", "item.nickel_plate_test");
                SetPrivateField(recipe, "recipeId", "recipe.thermal_regulator_test");
                SetPrivateField(research, "researchId", "research.thermal_test");
                SetPrivateField(
                    research,
                    "requiredItems",
                    new List<ItemStackDefinition>
                    {
                        CreateStack(sample, 4),
                        CreateStack(plate, 1)
                    });
                SetPrivateField(
                    research,
                    "unlockedRecipes",
                    new List<RecipeDefinition> { recipe });
                SetPrivateField(
                    research,
                    "unlockedCapabilityIds",
                    new List<string> { "capability.environment.thermal_test" });
                FleetKnowledgeState knowledge = new();
                inventory.TryAdd(sample, 4);
                inventory.TryAdd(plate, 1);

                ResearchUnlockResult result =
                    ResearchUnlockTransaction.TryExecute(
                        research,
                        inventory,
                        knowledge);

                Assert.That(result, Is.EqualTo(ResearchUnlockResult.Succeeded));
                Assert.That(inventory.Count(sample), Is.Zero);
                Assert.That(inventory.Count(plate), Is.Zero);
                Assert.That(
                    knowledge.HasDiscovery(new DefinitionId("research.thermal_test")),
                    Is.True);
                Assert.That(
                    knowledge.HasBlueprint(new DefinitionId("recipe.thermal_regulator_test")),
                    Is.True);
                Assert.That(
                    knowledge.HasCapability(
                        new DefinitionId("capability.environment.thermal_test")),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(recipe);
                Object.DestroyImmediate(research);
                Object.DestroyImmediate(plate);
                Object.DestroyImmediate(sample);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void FleetKnowledgeResearchCompletionRejectsInvalidSetAtomically()
        {
            FleetKnowledgeState knowledge = new();
            DefinitionId researchId = new("research.atomic");
            DefinitionId blueprintId = new("recipe.atomic");

            FleetOperationResult result = knowledge.TryCompleteResearch(
                researchId,
                new[] { blueprintId },
                new[] { default(DefinitionId) });

            Assert.That(
                result,
                Is.EqualTo(FleetOperationResult.InvalidIdentifier));
            Assert.That(knowledge.Revision, Is.Zero);
            Assert.That(knowledge.HasDiscovery(researchId), Is.False);
            Assert.That(knowledge.HasBlueprint(blueprintId), Is.False);
        }

        [Test]
        public void StationAndTerminalRejectDefinitionsTheyDoNotOffer()
        {
            GameObject owner = new("TerminalOfferBoundary");
            RecipeDefinition recipe =
                ScriptableObject.CreateInstance<RecipeDefinition>();
            ResearchDefinition research =
                ScriptableObject.CreateInstance<ResearchDefinition>();
            InventoryItemDefinition output =
                ScriptableObject.CreateInstance<InventoryItemDefinition>();
            try
            {
                SetPrivateField(recipe, "recipeId", "recipe.offer_boundary");
                SetPrivateField(
                    recipe,
                    "stationType",
                    CraftingStationType.Refinery);
                SetPrivateField(output, "itemId", "item.offer_boundary");
                SetPrivateField(
                    recipe,
                    "outputs",
                    new List<ItemStackDefinition> { CreateStack(output, 1) });
                SetPrivateField(
                    research,
                    "researchId",
                    "research.offer_boundary");

                CraftingStationRuntime station =
                    owner.AddComponent<CraftingStationRuntime>();
                ResearchTerminalRuntime terminal =
                    owner.AddComponent<ResearchTerminalRuntime>();

                Assert.That(station.CanOffer(recipe), Is.False);
                Assert.That(terminal.CanOffer(research), Is.False);

                SetPrivateField(
                    station,
                    "availableRecipes",
                    new List<RecipeDefinition> { recipe });
                SetPrivateField(
                    terminal,
                    "availableResearch",
                    new List<ResearchDefinition> { research });

                Assert.That(station.CanOffer(recipe), Is.True);
                Assert.That(terminal.CanOffer(research), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(recipe);
                Object.DestroyImmediate(research);
                Object.DestroyImmediate(output);
            }
        }

        [Test]
        public void PersonalShipCargoSnapshotRestoresStackState()
        {
            GameObject owner = new("PersonalShipCargoSnapshot");
            InventoryItemDefinition item =
                ScriptableObject.CreateInstance<InventoryItemDefinition>();
            GameplayDefinitionRegistry registry =
                ScriptableObject.CreateInstance<GameplayDefinitionRegistry>();
            try
            {
                owner.AddComponent<PersistentObjectId>().SetId("ship.snapshot");
                PersonalShipCargoInventory cargo =
                    owner.AddComponent<PersonalShipCargoInventory>();
                SetPrivateField(item, "itemId", "item.cargo_snapshot");
                SetPrivateField(
                    registry,
                    "inventoryItems",
                    new List<InventoryItemDefinition> { item });

                Assert.That(cargo.TryAdd(item, 6), Is.EqualTo(6));
                InventoryContainerSnapshot snapshot =
                    cargo.CaptureContainerSnapshot();
                Assert.That(cargo.TryRemove(item, 6), Is.EqualTo(6));
                Assert.That(cargo.Count(item), Is.Zero);

                Assert.That(
                    cargo.ApplyContainerSnapshot(snapshot, registry),
                    Is.True);
                Assert.That(cargo.Count(item), Is.EqualTo(6));
                Assert.That(snapshot.ContainerId, Is.EqualTo(cargo.ContainerId.Value));
            }
            finally
            {
                Object.DestroyImmediate(registry);
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void CraftingExecutorRejectsRecipeAtWrongStationType()
        {
            GameObject owner = new("WrongStationCrafting");
            InventoryItemDefinition input =
                ScriptableObject.CreateInstance<InventoryItemDefinition>();
            InventoryItemDefinition output =
                ScriptableObject.CreateInstance<InventoryItemDefinition>();
            RecipeDefinition recipe =
                ScriptableObject.CreateInstance<RecipeDefinition>();
            try
            {
                owner.AddComponent<PersistentObjectId>().SetId("crafting.station");
                PlayerInventory inventory = owner.AddComponent<PlayerInventory>();
                SetPrivateField(input, "itemId", "item.station_input");
                SetPrivateField(output, "itemId", "item.station_output");
                SetPrivateField(recipe, "recipeId", "recipe.station_test");
                SetPrivateField(
                    recipe,
                    "stationType",
                    CraftingStationType.Fabricator);
                SetPrivateField(
                    recipe,
                    "inputs",
                    new List<ItemStackDefinition> { CreateStack(input, 1) });
                SetPrivateField(
                    recipe,
                    "outputs",
                    new List<ItemStackDefinition> { CreateStack(output, 1) });
                inventory.TryAdd(input, 1);

                CraftingRecipeResult result = CraftingRecipeExecutor.CanCraft(
                    recipe,
                    CraftingStationType.Refinery,
                    inventory,
                    hasRequiredResearch: true);

                Assert.That(result, Is.EqualTo(CraftingRecipeResult.WrongStationType));
                Assert.That(inventory.Count(input), Is.EqualTo(1));
                Assert.That(inventory.Count(output), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(recipe);
                Object.DestroyImmediate(output);
                Object.DestroyImmediate(input);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void EquipmentRepositoryRejectsDuplicateSerialNumbers()
        {
            EquipmentRepositoryState repository = CreateRepository();
            EquipmentInstanceState first = CreateEquipment(
                "equipment.engine.001",
                "ENG-001",
                "equipment.engine");
            EquipmentInstanceState duplicateSerial = CreateEquipment(
                "equipment.engine.002",
                "eng-001",
                "equipment.engine");

            Assert.That(
                repository.TryRegister(first),
                Is.EqualTo(EquipmentRepositoryResult.Succeeded));
            long revision = repository.Revision;
            Assert.That(
                repository.TryRegister(duplicateSerial),
                Is.EqualTo(EquipmentRepositoryResult.DuplicateSerialNumber));
            Assert.That(repository.Count, Is.EqualTo(1));
            Assert.That(repository.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void EquipmentLocationLedgerPreventsDuplicateContainerOwnership()
        {
            EquipmentRepositoryState repository = CreateRepository();
            EquipmentInstanceState equipment = CreateEquipment(
                "equipment.engine.001",
                "ENG-001",
                "equipment.engine");
            repository.TryRegister(equipment);
            InventoryContainerState source = CreateContainer("container.source", 1);
            InventoryContainerState duplicateDestination =
                CreateContainer("container.destination", 1);

            Assert.That(
                EquipmentPlacementService.TryStoreUnassigned(
                    repository,
                    source,
                    equipment.InstanceId),
                Is.EqualTo(EquipmentPlacementResult.Succeeded));
            long destinationRevision = duplicateDestination.Revision;
            Assert.That(
                EquipmentPlacementService.TryStoreUnassigned(
                    repository,
                    duplicateDestination,
                    equipment.InstanceId),
                Is.EqualTo(EquipmentPlacementResult.EquipmentAlreadyAssigned));

            Assert.That(source.ContainsItemInstance(equipment.InstanceId), Is.True);
            Assert.That(
                duplicateDestination.ContainsItemInstance(equipment.InstanceId),
                Is.False);
            Assert.That(duplicateDestination.Revision, Is.EqualTo(destinationRevision));
            repository.TryGetLocation(equipment.InstanceId, out EquipmentLocation location);
            Assert.That(
                location,
                Is.EqualTo(EquipmentLocation.InContainer(source.ContainerId)));
        }

        [Test]
        public void InstallingEquipmentSwapsThePreviousInstanceBackIntoStorage()
        {
            DefinitionId engineDefinitionId = new("equipment.engine");
            DefinitionId engineSlotId = new("slot.engine");
            EquipmentRepositoryState repository = CreateRepository();
            EquipmentInstanceState first = CreateEquipment(
                "equipment.engine.001",
                "ENG-001",
                engineDefinitionId.Value);
            EquipmentInstanceState second = CreateEquipment(
                "equipment.engine.002",
                "ENG-002",
                engineDefinitionId.Value);
            repository.TryRegister(first);
            repository.TryRegister(second);

            InventoryContainerState storage = CreateContainer("container.ship", 2);
            EquipmentPlacementService.TryStoreUnassigned(
                repository,
                storage,
                first.InstanceId);
            EquipmentPlacementService.TryStoreUnassigned(
                repository,
                storage,
                second.InstanceId);
            PersonalShipState ship = CreatePersonalShip();
            IEquipmentInstallationPolicy policy =
                new ExactEquipmentPolicy(engineDefinitionId, engineSlotId);

            Assert.That(
                EquipmentLoadoutService.TryInstallFromContainer(
                    repository,
                    storage,
                    ship,
                    policy,
                    engineSlotId,
                    first.InstanceId,
                    out PersistentEntityId firstDisplaced),
                Is.EqualTo(EquipmentLoadoutResult.Succeeded));
            Assert.That(firstDisplaced.IsValid, Is.False);

            Assert.That(
                EquipmentLoadoutService.TryInstallFromContainer(
                    repository,
                    storage,
                    ship,
                    policy,
                    engineSlotId,
                    second.InstanceId,
                    out PersistentEntityId secondDisplaced),
                Is.EqualTo(EquipmentLoadoutResult.Succeeded));

            Assert.That(secondDisplaced, Is.EqualTo(first.InstanceId));
            Assert.That(storage.ContainsItemInstance(first.InstanceId), Is.True);
            Assert.That(storage.ContainsItemInstance(second.InstanceId), Is.False);
            Assert.That(
                ship.TryGetInstalledModule(engineSlotId, out PersistentEntityId installed),
                Is.True);
            Assert.That(installed, Is.EqualTo(second.InstanceId));
            repository.TryGetLocation(
                first.InstanceId,
                out EquipmentLocation firstLocation);
            repository.TryGetLocation(
                second.InstanceId,
                out EquipmentLocation secondLocation);
            Assert.That(
                firstLocation,
                Is.EqualTo(EquipmentLocation.InContainer(storage.ContainerId)));
            Assert.That(
                secondLocation,
                Is.EqualTo(
                    EquipmentLocation.InPersonalShipSlot(ship.ShipId, engineSlotId)));
        }

        [Test]
        public void IncompatibleEquipmentInstallRollsBackBothAggregates()
        {
            DefinitionId engineSlotId = new("slot.engine");
            EquipmentRepositoryState repository = CreateRepository();
            EquipmentInstanceState scanner = CreateEquipment(
                "equipment.scanner.001",
                "SCN-001",
                "equipment.scanner");
            repository.TryRegister(scanner);
            InventoryContainerState storage = CreateContainer("container.ship", 1);
            EquipmentPlacementService.TryStoreUnassigned(
                repository,
                storage,
                scanner.InstanceId);
            PersonalShipState ship = CreatePersonalShip();
            long storageRevision = storage.Revision;
            long shipRevision = ship.Revision;

            EquipmentLoadoutResult result =
                EquipmentLoadoutService.TryInstallFromContainer(
                    repository,
                    storage,
                    ship,
                    new ExactEquipmentPolicy(
                        new DefinitionId("equipment.engine"),
                        engineSlotId),
                    engineSlotId,
                    scanner.InstanceId,
                    out _);

            Assert.That(result, Is.EqualTo(EquipmentLoadoutResult.IncompatibleEquipment));
            Assert.That(storage.ContainsItemInstance(scanner.InstanceId), Is.True);
            Assert.That(ship.IsEquipmentInstalled(scanner.InstanceId), Is.False);
            Assert.That(storage.Revision, Is.EqualTo(storageRevision));
            Assert.That(ship.Revision, Is.EqualTo(shipRevision));
        }

        [Test]
        public void UninstallToFullContainerLeavesEquipmentInstalled()
        {
            DefinitionId engineDefinitionId = new("equipment.engine");
            DefinitionId engineSlotId = new("slot.engine");
            EquipmentRepositoryState repository = CreateRepository();
            EquipmentInstanceState engine = CreateEquipment(
                "equipment.engine.001",
                "ENG-001",
                engineDefinitionId.Value);
            repository.TryRegister(engine);

            InventoryContainerState source = CreateContainer("container.source", 1);
            EquipmentPlacementService.TryStoreUnassigned(
                repository,
                source,
                engine.InstanceId);
            PersonalShipState ship = CreatePersonalShip();
            EquipmentLoadoutService.TryInstallFromContainer(
                repository,
                source,
                ship,
                new ExactEquipmentPolicy(engineDefinitionId, engineSlotId),
                engineSlotId,
                engine.InstanceId,
                out _);

            InventoryContainerState fullDestination =
                CreateContainer("container.destination", 1);
            EquipmentInstanceState unrelated = CreateEquipment(
                "equipment.unrelated.001",
                "MSC-001",
                "equipment.misc");
            repository.TryRegister(unrelated);
            EquipmentPlacementService.TryStoreUnassigned(
                repository,
                fullDestination,
                unrelated.InstanceId);
            long shipRevision = ship.Revision;
            long destinationRevision = fullDestination.Revision;

            EquipmentLoadoutResult result =
                EquipmentLoadoutService.TryUninstallToContainer(
                    repository,
                    ship,
                    fullDestination,
                    engineSlotId,
                    out _);

            Assert.That(result, Is.EqualTo(EquipmentLoadoutResult.InsufficientCapacity));
            Assert.That(ship.IsEquipmentInstalled(engine.InstanceId), Is.True);
            Assert.That(fullDestination.ContainsItemInstance(engine.InstanceId), Is.False);
            Assert.That(ship.Revision, Is.EqualTo(shipRevision));
            Assert.That(fullDestination.Revision, Is.EqualTo(destinationRevision));
            repository.TryGetLocation(
                engine.InstanceId,
                out EquipmentLocation location);
            Assert.That(
                location,
                Is.EqualTo(
                    EquipmentLocation.InPersonalShipSlot(ship.ShipId, engineSlotId)));
        }

        [Test]
        public void RegistryEquipmentPolicyUsesSlotTypeAndSizeCompatibility()
        {
            InventoryItemDefinition item =
                ScriptableObject.CreateInstance<InventoryItemDefinition>();
            EquipmentDefinition equipment =
                ScriptableObject.CreateInstance<EquipmentDefinition>();
            EquipmentSlotDefinition compatibleSlot =
                ScriptableObject.CreateInstance<EquipmentSlotDefinition>();
            EquipmentSlotDefinition wrongTypeSlot =
                ScriptableObject.CreateInstance<EquipmentSlotDefinition>();
            EquipmentSlotDefinition undersizedSlot =
                ScriptableObject.CreateInstance<EquipmentSlotDefinition>();
            GameplayDefinitionRegistry registry =
                ScriptableObject.CreateInstance<GameplayDefinitionRegistry>();
            try
            {
                SetPrivateField(item, "itemId", "item.engine.test");
                SetPrivateField(
                    item,
                    "storageMode",
                    InventoryStorageMode.UniqueInstance);
                SetPrivateField(equipment, "item", item);
                SetPrivateField(
                    equipment,
                    "sizeClass",
                    EquipmentSizeClass.Standard);
                SetPrivateField(
                    equipment,
                    "compatibleSlotTypeIds",
                    new List<string> { "slot_type.personal.engine" });

                ConfigureSlot(
                    compatibleSlot,
                    "slot.engine.main",
                    "slot_type.personal.engine",
                    EquipmentSizeClass.Heavy);
                ConfigureSlot(
                    wrongTypeSlot,
                    "slot.utility.main",
                    "slot_type.personal.utility",
                    EquipmentSizeClass.Heavy);
                ConfigureSlot(
                    undersizedSlot,
                    "slot.engine.compact",
                    "slot_type.personal.engine",
                    EquipmentSizeClass.Compact);

                SetPrivateField(
                    registry,
                    "inventoryItems",
                    new List<InventoryItemDefinition> { item });
                SetPrivateField(
                    registry,
                    "equipment",
                    new List<EquipmentDefinition> { equipment });
                SetPrivateField(
                    registry,
                    "equipmentSlots",
                    new List<EquipmentSlotDefinition>
                    {
                        compatibleSlot,
                        wrongTypeSlot,
                        undersizedSlot
                    });

                RegistryEquipmentInstallationPolicy policy = new(registry);
                Assert.That(
                    policy.CanInstall(
                        new DefinitionId("item.engine.test"),
                        new DefinitionId("slot.engine.main")),
                    Is.True);
                Assert.That(
                    policy.CanInstall(
                        new DefinitionId("item.engine.test"),
                        new DefinitionId("slot.utility.main")),
                    Is.False);
                Assert.That(
                    policy.CanInstall(
                        new DefinitionId("item.engine.test"),
                        new DefinitionId("slot.engine.compact")),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(registry);
                Object.DestroyImmediate(undersizedSlot);
                Object.DestroyImmediate(wrongTypeSlot);
                Object.DestroyImmediate(compatibleSlot);
                Object.DestroyImmediate(equipment);
                Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void EquipmentDefinitionRejectsStackableItems()
        {
            InventoryItemDefinition item =
                ScriptableObject.CreateInstance<InventoryItemDefinition>();
            EquipmentDefinition equipment =
                ScriptableObject.CreateInstance<EquipmentDefinition>();
            try
            {
                SetPrivateField(item, "itemId", "item.invalid_stackable_engine");
                SetPrivateField(equipment, "item", item);
                SetPrivateField(
                    equipment,
                    "compatibleSlotTypeIds",
                    new List<string> { "slot_type.personal.engine" });

                Assert.That(equipment.IsValid, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(equipment);
                Object.DestroyImmediate(item);
            }
        }

        static InventoryContainerState CreateContainer(string id, int slotCapacity)
        {
            return new InventoryContainerState(
                new PersistentEntityId(id),
                slotCapacity);
        }

        static EquipmentInstanceState CreateEquipment(
            string instanceId,
            string serialNumber,
            string definitionId)
        {
            return new EquipmentInstanceState(
                new PersistentEntityId(instanceId),
                new DefinitionId(definitionId),
                serialNumber);
        }

        static EquipmentRepositoryState CreateRepository()
        {
            return new EquipmentRepositoryState(
                new PersistentEntityId("repository.fleet.01"));
        }

        static PersonalShipState CreatePersonalShip()
        {
            return new PersonalShipState(
                new PersistentEntityId("ship.personal.01"),
                new PersistentEntityId("player.01"),
                new PersistentEntityId("container.ship.01"),
                "Test Ship",
                maximumHull: 100d,
                currentHull: 100d,
                maximumFuel: 50d,
                currentFuel: 50d);
        }

        static void ConfigureSlot(
            EquipmentSlotDefinition slot,
            string slotId,
            string slotTypeId,
            EquipmentSizeClass maximumSize)
        {
            SetPrivateField(slot, "slotId", slotId);
            SetPrivateField(slot, "slotTypeId", slotTypeId);
            SetPrivateField(slot, "maximumSize", maximumSize);
        }

        static ItemStackDefinition CreateStack(
            InventoryItemDefinition item,
            int amount)
        {
            ItemStackDefinition stack = new();
            SetPrivateField(stack, "item", item);
            SetPrivateField(stack, "amount", amount);
            return stack;
        }

        static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(
                field,
                Is.Not.Null,
                $"Missing private field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        sealed class ExactEquipmentPolicy : IEquipmentInstallationPolicy
        {
            readonly DefinitionId equipmentDefinitionId;
            readonly DefinitionId slotDefinitionId;

            public ExactEquipmentPolicy(
                DefinitionId equipmentDefinitionId,
                DefinitionId slotDefinitionId)
            {
                this.equipmentDefinitionId = equipmentDefinitionId;
                this.slotDefinitionId = slotDefinitionId;
            }

            public bool CanInstall(
                DefinitionId candidateEquipmentDefinitionId,
                DefinitionId candidateSlotDefinitionId)
            {
                return candidateEquipmentDefinitionId == equipmentDefinitionId &&
                       candidateSlotDefinitionId == slotDefinitionId;
            }
        }
    }
}
