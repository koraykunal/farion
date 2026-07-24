using System;

namespace Farion.Simulation.World.Identity
{
    public readonly struct UniverseAddress : IEquatable<UniverseAddress>
    {
        public UniverseAddress(
            GeneratedEntityId entityId,
            GeneratedEntityId parentId,
            UniverseEntityKind kind)
        {
            if (!entityId.IsValid)
            {
                throw new ArgumentException("Universe addresses require a valid entity id.", nameof(entityId));
            }

            if (kind == UniverseEntityKind.Unknown)
            {
                throw new ArgumentException("Universe addresses require a concrete entity kind.", nameof(kind));
            }

            EntityId = entityId;
            ParentId = parentId;
            Kind = kind;
        }

        public GeneratedEntityId EntityId { get; }
        public GeneratedEntityId ParentId { get; }
        public UniverseEntityKind Kind { get; }
        public bool HasParent => ParentId.IsValid;

        public static bool operator ==(UniverseAddress left, UniverseAddress right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(UniverseAddress left, UniverseAddress right)
        {
            return !left.Equals(right);
        }

        public bool Equals(UniverseAddress other)
        {
            return EntityId.Equals(other.EntityId) && ParentId.Equals(other.ParentId) && Kind == other.Kind;
        }

        public override bool Equals(object obj)
        {
            return obj is UniverseAddress other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = EntityId.GetHashCode();
                hash = hash * 397 ^ ParentId.GetHashCode();
                hash = hash * 397 ^ (int)Kind;
                return hash;
            }
        }

        public override string ToString()
        {
            return HasParent
                ? $"{Kind}:{EntityId} parent={ParentId}"
                : $"{Kind}:{EntityId}";
        }
    }
}
