using System;
using Farion.Core.Persistence;
using Farion.Gameplay.Domain.Fleet;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Flight;
using UnityEngine;

namespace Farion.Gameplay.Ships
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PersistentObjectId))]
    [RequireComponent(typeof(SpacecraftMotor))]
    [RequireComponent(typeof(PersonalShipCargoInventory))]
    public sealed class PersonalShipRuntimeBinding : MonoBehaviour
    {
        const string DefaultDisplayName = "Farion";
        const double DefaultMaximumHull = 100d;
        const double DefaultMaximumFuel = 100d;

        [Header("Authoring")]
        [SerializeField] string defaultDisplayName = DefaultDisplayName;
        [Min(0.01f)]
        [SerializeField] double maximumHull = DefaultMaximumHull;
        [Min(0f)]
        [SerializeField] double initialHull = DefaultMaximumHull;
        [Min(0.01f)]
        [SerializeField] double maximumFuel = DefaultMaximumFuel;
        [Min(0f)]
        [SerializeField] double initialFuel = DefaultMaximumFuel;

        [Header("Runtime Owners")]
        [SerializeField] PersistentObjectId persistentId;
        [SerializeField] SpacecraftMotor motor;
        [SerializeField] PersonalShipCargoInventory cargo;

        PersonalShipState state;

        public event Action<PersonalShipState> StateChanged;

        public bool IsInitialized => state != null;
        public bool HasValidAuthoring
        {
            get
            {
                ResolveReferences();
                return persistentId != null &&
                       persistentId.HasId &&
                       motor != null &&
                       cargo != null &&
                       motor.gameObject == gameObject &&
                       cargo.gameObject == gameObject &&
                       ShipNamePolicy.TryNormalize(defaultDisplayName, out _) &&
                       IsPositiveFinite(maximumHull) &&
                       IsFiniteInRange(initialHull, 0d, maximumHull) &&
                       IsPositiveFinite(maximumFuel) &&
                       IsFiniteInRange(initialFuel, 0d, maximumFuel);
            }
        }

        public PersonalShipState State => state;
        public SpacecraftMotor Motor
        {
            get
            {
                ResolveReferences();
                return motor;
            }
        }

        public PersonalShipCargoInventory Cargo
        {
            get
            {
                ResolveReferences();
                return cargo;
            }
        }

        public PersistentEntityId ShipId
        {
            get
            {
                ResolveReferences();
                return persistentId != null &&
                       PersistentEntityId.TryCreate(
                           persistentId.Id,
                           out PersistentEntityId id)
                    ? id
                    : PersistentEntityId.None;
            }
        }

        void Awake()
        {
            ResolveReferences();
        }

        void OnValidate()
        {
            ResolveReferences();
            defaultDisplayName =
                ShipNamePolicy.TryNormalize(
                    defaultDisplayName,
                    out string normalizedName)
                    ? normalizedName
                    : DefaultDisplayName;
            maximumHull = IsPositiveFinite(maximumHull)
                ? maximumHull
                : DefaultMaximumHull;
            initialHull = ClampFinite(initialHull, 0d, maximumHull, maximumHull);
            maximumFuel = IsPositiveFinite(maximumFuel)
                ? maximumFuel
                : DefaultMaximumFuel;
            initialFuel = ClampFinite(initialFuel, 0d, maximumFuel, maximumFuel);
            if (!Application.isPlaying)
            {
                state = null;
            }
        }

        public bool TryInitialize(PersistentEntityId ownerPlayerId)
        {
            ResolveReferences();
            if (!ownerPlayerId.IsValid || !HasValidAuthoring)
            {
                return false;
            }

            PersistentEntityId shipId = ShipId;
            PersistentEntityId cargoId = cargo.ContainerId;
            if (state != null)
            {
                return state.ShipId == shipId &&
                       state.OwnerPlayerId == ownerPlayerId &&
                       state.CargoContainerId == cargoId;
            }

            state = new PersonalShipState(
                shipId,
                ownerPlayerId,
                cargoId,
                defaultDisplayName,
                maximumHull,
                initialHull,
                maximumFuel,
                initialFuel);
            StateChanged?.Invoke(state);
            return true;
        }

        public FleetOperationResult TryRename(
            string displayName,
            long expectedRevision = -1L)
        {
            if (state == null)
            {
                return FleetOperationResult.NotFound;
            }

            long previousRevision = state.Revision;
            FleetOperationResult result =
                state.TryRename(displayName, expectedRevision);
            NotifyIfChanged(previousRevision);
            return result;
        }

        public FleetOperationResult TryConsumeFuel(
            double amount,
            long expectedRevision = -1L)
        {
            if (state == null)
            {
                return FleetOperationResult.NotFound;
            }

            long previousRevision = state.Revision;
            FleetOperationResult result =
                state.TryConsumeFuel(amount, expectedRevision);
            NotifyIfChanged(previousRevision);
            return result;
        }

        public FleetOperationResult TryRefuel(
            double amount,
            out double accepted,
            long expectedRevision = -1L)
        {
            accepted = 0d;
            if (state == null)
            {
                return FleetOperationResult.NotFound;
            }

            long previousRevision = state.Revision;
            FleetOperationResult result =
                state.TryRefuel(amount, out accepted, expectedRevision);
            NotifyIfChanged(previousRevision);
            return result;
        }

        public FleetOperationResult TryApplyDamage(
            double amount,
            out double applied,
            long expectedRevision = -1L)
        {
            applied = 0d;
            if (state == null)
            {
                return FleetOperationResult.NotFound;
            }

            long previousRevision = state.Revision;
            FleetOperationResult result =
                state.TryApplyDamage(amount, out applied, expectedRevision);
            NotifyIfChanged(previousRevision);
            return result;
        }

        public FleetOperationResult TryRepair(
            double amount,
            out double accepted,
            long expectedRevision = -1L)
        {
            accepted = 0d;
            if (state == null)
            {
                return FleetOperationResult.NotFound;
            }

            long previousRevision = state.Revision;
            FleetOperationResult result =
                state.TryRepair(amount, out accepted, expectedRevision);
            NotifyIfChanged(previousRevision);
            return result;
        }

        void ResolveReferences()
        {
            persistentId ??= GetComponent<PersistentObjectId>();
            motor ??= GetComponent<SpacecraftMotor>();
            cargo ??= GetComponent<PersonalShipCargoInventory>();
        }

        void NotifyIfChanged(long previousRevision)
        {
            if (state != null && state.Revision != previousRevision)
            {
                StateChanged?.Invoke(state);
            }
        }

        static bool IsPositiveFinite(double value)
        {
            return value > 0d && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        static bool IsFiniteInRange(
            double value,
            double minimum,
            double maximum)
        {
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value) &&
                   value >= minimum &&
                   value <= maximum;
        }

        static double ClampFinite(
            double value,
            double minimum,
            double maximum,
            double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return fallback;
            }

            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
