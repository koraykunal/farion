using Farion.Gameplay.Domain.Systems;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpacecraftMotor))]
    public sealed class SpacecraftHull : MonoBehaviour
    {
        const float DefaultIntegrity = 500f;

        [Header("Sources")]
        [SerializeField] SpacecraftMotor motor;
        [SerializeField] SpacecraftSurfaceContactProbe surfaceContactProbe;

        [Header("Runtime")]
        [SerializeField] float integrityNormalized = 1f;

        ResourcePool integrity;
        bool externalSimulation;

        public ResourcePool Integrity => integrity;
        public float Normalized => integrity.Normalized;
        public bool IsBreached => integrity.Capacity > 0f && integrity.IsEmpty;
        public float Capacity => motor == null
            ? DefaultIntegrity
            : motor.FlightProfile != null
                ? motor.FlightProfile.EvaluateHullIntegrity(motor.ModuleBonuses)
                : DefaultIntegrity * motor.ModuleBonuses.HullCapacityMultiplier;

        void Awake()
        {
            ResolveReferences();
            Restore();
        }

        void OnValidate()
        {
            ResolveReferences();
        }

        void FixedUpdate()
        {
            if (!externalSimulation)
            {
                Step();
            }
        }

        public void SetExternalSimulation(bool enabled)
        {
            externalSimulation = enabled;
        }

        public void Step()
        {
            SyncCapacity();
            float impactSpeed = surfaceContactProbe != null
                ? surfaceContactProbe.ConsumeImpactSpeed()
                : 0f;
            if (impactSpeed > 0f)
            {
                ApplyImpact(impactSpeed);
            }
        }

        public void SyncCapacity()
        {
            float capacity = Capacity;
            if (integrity.Capacity == capacity)
            {
                return;
            }

            integrity.SetCapacity(capacity);
            ApplyRuntimeState();
        }

        public float ApplyImpact(float impactSpeed)
        {
            SpacecraftFlightProfile profile = motor != null ? motor.FlightProfile : null;
            return profile != null
                ? ApplyDamage(profile.EvaluateImpactDamage(impactSpeed))
                : 0f;
        }

        public float ApplyDamage(float damage)
        {
            float applied = integrity.Drain(damage);
            if (applied <= 0f)
            {
                return 0f;
            }

            ApplyRuntimeState();
            return applied;
        }

        public float Repair(float amount)
        {
            SyncCapacity();
            float repaired = integrity.Fill(amount);
            ApplyRuntimeState();
            return repaired;
        }

        public void Restore()
        {
            integrity = ResourcePool.Full(Capacity);
            ApplyRuntimeState();
        }

        public void RestoreIntegrity(ResourcePool saved)
        {
            integrity = new ResourcePool(Capacity, saved.Current);
            ApplyRuntimeState();
        }

        public void SetIntegrityAmount(float current)
        {
            integrity = new ResourcePool(Capacity, current);
            ApplyRuntimeState();
        }

        void ResolveReferences()
        {
            motor ??= GetComponent<SpacecraftMotor>();
            surfaceContactProbe ??= GetComponent<SpacecraftSurfaceContactProbe>();
        }

        void ApplyRuntimeState()
        {
            integrityNormalized = integrity.Normalized;
            motor?.SetDriveDisabled(IsBreached);
        }
    }
}
