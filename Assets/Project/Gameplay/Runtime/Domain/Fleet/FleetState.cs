using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Domain.Fleet
{
    public sealed class FleetState
    {
        public const int MaximumMemberCount = 4;

        readonly Dictionary<PersistentEntityId, FleetMemberState> members = new();
        readonly ReadOnlyDictionary<PersistentEntityId, FleetMemberState> readOnlyMembers;

        public FleetState(
            PersistentEntityId fleetId,
            PersistentEntityId capitalShipId,
            FleetKnowledgeState knowledge = null)
        {
            if (!fleetId.IsValid || !capitalShipId.IsValid)
            {
                throw new ArgumentException("Fleet state requires valid fleet and capital ship ids.");
            }

            FleetId = fleetId;
            CapitalShipId = capitalShipId;
            Knowledge = knowledge ?? new FleetKnowledgeState();
            readOnlyMembers =
                new ReadOnlyDictionary<PersistentEntityId, FleetMemberState>(members);
        }

        public PersistentEntityId FleetId { get; }
        public PersistentEntityId CapitalShipId { get; }
        public FleetKnowledgeState Knowledge { get; }
        public IReadOnlyDictionary<PersistentEntityId, FleetMemberState> Members => readOnlyMembers;
        public long Revision { get; private set; }

        public FleetOperationResult TryRegisterMember(
            PersistentEntityId playerId,
            PersistentEntityId personalShipId,
            long expectedRevision = -1L)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return FleetOperationResult.StaleRevision;
            }

            if (!playerId.IsValid || !personalShipId.IsValid)
            {
                return FleetOperationResult.InvalidIdentifier;
            }

            if (members.ContainsKey(playerId) || IsPersonalShipAssigned(personalShipId))
            {
                return FleetOperationResult.AlreadyExists;
            }

            if (members.Count >= MaximumMemberCount)
            {
                return FleetOperationResult.CapacityExceeded;
            }

            IncrementRevision();
            members.Add(playerId, new FleetMemberState(playerId, personalShipId));
            return FleetOperationResult.Succeeded;
        }

        public FleetOperationResult TryAssignPersonalShip(
            PersistentEntityId playerId,
            PersistentEntityId personalShipId,
            long expectedRevision = -1L)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return FleetOperationResult.StaleRevision;
            }

            if (!playerId.IsValid || !personalShipId.IsValid)
            {
                return FleetOperationResult.InvalidIdentifier;
            }

            if (!members.TryGetValue(playerId, out FleetMemberState member))
            {
                return FleetOperationResult.NotFound;
            }

            if (member.PersonalShipId == personalShipId)
            {
                return FleetOperationResult.Succeeded;
            }

            if (IsPersonalShipAssigned(personalShipId))
            {
                return FleetOperationResult.Conflict;
            }

            IncrementRevision();
            members[playerId] = member.WithPersonalShip(personalShipId);
            return FleetOperationResult.Succeeded;
        }

        public FleetOperationResult TryRemoveMember(
            PersistentEntityId playerId,
            long expectedRevision = -1L)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return FleetOperationResult.StaleRevision;
            }

            if (!members.ContainsKey(playerId))
            {
                return FleetOperationResult.NotFound;
            }

            IncrementRevision();
            members.Remove(playerId);
            return FleetOperationResult.Succeeded;
        }

        public bool TryGetMember(
            PersistentEntityId playerId,
            out FleetMemberState member)
        {
            return members.TryGetValue(playerId, out member);
        }

        bool IsPersonalShipAssigned(PersistentEntityId shipId)
        {
            foreach (FleetMemberState member in members.Values)
            {
                if (member.PersonalShipId == shipId)
                {
                    return true;
                }
            }

            return false;
        }

        bool MatchesRevision(long expectedRevision)
        {
            return expectedRevision < 0L || expectedRevision == Revision;
        }

        void IncrementRevision()
        {
            if (Revision == long.MaxValue)
            {
                throw new InvalidOperationException("Fleet revision capacity was exhausted.");
            }

            Revision++;
        }
    }
}
