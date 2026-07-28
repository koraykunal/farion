using System;
using System.Collections.Generic;
using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Domain.Fleet
{
    public sealed class CapitalShipState
    {
        readonly HashSet<PersistentEntityId> roomIds = new();
        readonly Dictionary<PersistentEntityId, PersistentEntityId> machineRoomIds = new();

        public CapitalShipState(
            PersistentEntityId shipId,
            PersistentEntityId fleetId,
            PersistentEntityId storageContainerId,
            string displayName,
            int roomCapacity)
        {
            if (!shipId.IsValid || !fleetId.IsValid || !storageContainerId.IsValid)
            {
                throw new ArgumentException("Capital ships require valid ship, fleet, and storage ids.");
            }

            if (!ShipNamePolicy.TryNormalize(displayName, out string normalizedName))
            {
                throw new ArgumentException("Capital ship name is invalid.", nameof(displayName));
            }

            if (roomCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(roomCapacity));
            }

            ShipId = shipId;
            FleetId = fleetId;
            StorageContainerId = storageContainerId;
            DisplayName = normalizedName;
            RoomCapacity = roomCapacity;
        }

        public PersistentEntityId ShipId { get; }
        public PersistentEntityId FleetId { get; }
        public PersistentEntityId StorageContainerId { get; }
        public string DisplayName { get; private set; }
        public int RoomCapacity { get; private set; }
        public long Revision { get; private set; }
        public IEnumerable<PersistentEntityId> RoomIds
        {
            get
            {
                foreach (PersistentEntityId roomId in roomIds)
                {
                    yield return roomId;
                }
            }
        }

        public IEnumerable<PersistentEntityId> MachineIds
        {
            get
            {
                foreach (PersistentEntityId machineId in machineRoomIds.Keys)
                {
                    yield return machineId;
                }
            }
        }

        public FleetOperationResult TryRename(string displayName, long expectedRevision = -1L)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return FleetOperationResult.StaleRevision;
            }

            if (!ShipNamePolicy.TryNormalize(displayName, out string normalizedName))
            {
                return FleetOperationResult.InvalidName;
            }

            if (string.Equals(DisplayName, normalizedName, StringComparison.Ordinal))
            {
                return FleetOperationResult.Succeeded;
            }

            IncrementRevision();
            DisplayName = normalizedName;
            return FleetOperationResult.Succeeded;
        }

        public FleetOperationResult TryRegisterRoom(
            PersistentEntityId roomId,
            long expectedRevision = -1L)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return FleetOperationResult.StaleRevision;
            }

            if (!roomId.IsValid)
            {
                return FleetOperationResult.InvalidIdentifier;
            }

            if (roomIds.Contains(roomId) || machineRoomIds.ContainsKey(roomId))
            {
                return FleetOperationResult.AlreadyExists;
            }

            if (roomIds.Count >= RoomCapacity)
            {
                return FleetOperationResult.CapacityExceeded;
            }

            IncrementRevision();
            roomIds.Add(roomId);
            return FleetOperationResult.Succeeded;
        }

        public FleetOperationResult TryRemoveRoom(
            PersistentEntityId roomId,
            long expectedRevision = -1L)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return FleetOperationResult.StaleRevision;
            }

            if (!roomIds.Contains(roomId))
            {
                return FleetOperationResult.NotFound;
            }

            foreach (PersistentEntityId parentRoomId in machineRoomIds.Values)
            {
                if (parentRoomId == roomId)
                {
                    return FleetOperationResult.Conflict;
                }
            }

            IncrementRevision();
            roomIds.Remove(roomId);
            return FleetOperationResult.Succeeded;
        }

        public FleetOperationResult TryRegisterMachine(
            PersistentEntityId machineId,
            PersistentEntityId roomId,
            long expectedRevision = -1L)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return FleetOperationResult.StaleRevision;
            }

            if (!machineId.IsValid || !roomId.IsValid)
            {
                return FleetOperationResult.InvalidIdentifier;
            }

            if (!roomIds.Contains(roomId))
            {
                return FleetOperationResult.NotFound;
            }

            if (roomIds.Contains(machineId) || machineRoomIds.ContainsKey(machineId))
            {
                return FleetOperationResult.AlreadyExists;
            }

            IncrementRevision();
            machineRoomIds.Add(machineId, roomId);
            return FleetOperationResult.Succeeded;
        }

        public FleetOperationResult TryRemoveMachine(
            PersistentEntityId machineId,
            long expectedRevision = -1L)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return FleetOperationResult.StaleRevision;
            }

            if (!machineRoomIds.ContainsKey(machineId))
            {
                return FleetOperationResult.NotFound;
            }

            IncrementRevision();
            machineRoomIds.Remove(machineId);
            return FleetOperationResult.Succeeded;
        }

        public FleetOperationResult TryIncreaseRoomCapacity(
            int amount,
            long expectedRevision = -1L)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return FleetOperationResult.StaleRevision;
            }

            if (amount <= 0 || RoomCapacity > int.MaxValue - amount)
            {
                return FleetOperationResult.InvalidAmount;
            }

            IncrementRevision();
            RoomCapacity += amount;
            return FleetOperationResult.Succeeded;
        }

        public bool TryGetMachineRoom(
            PersistentEntityId machineId,
            out PersistentEntityId roomId)
        {
            return machineRoomIds.TryGetValue(machineId, out roomId);
        }

        bool MatchesRevision(long expectedRevision)
        {
            return expectedRevision < 0L || expectedRevision == Revision;
        }

        void IncrementRevision()
        {
            if (Revision == long.MaxValue)
            {
                throw new InvalidOperationException("Capital ship revision capacity was exhausted.");
            }

            Revision++;
        }
    }
}
