using System;
using Farion.Gameplay.Domain.Systems;
using Farion.Simulation.Physics;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpacecraftMotor))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SpacecraftHull : MonoBehaviour
    {
        const float DefaultIntegrity = 500f;

        [Header("Sources")]
        [SerializeField] SpacecraftMotor motor;

        [Header("Runtime")]
        [SerializeField] float integrityNormalized = 1f;

        Rigidbody cachedRigidbody;
        ResourcePool integrity;
        Vector3 preStepLinearVelocity;
        Vector3 preStepAngularVelocity;
        float pendingImpactSpeed;
        bool externalSimulation;

        public event Action<float> Damaged;

        Rigidbody Rigidbody => cachedRigidbody != null
            ? cachedRigidbody
            : cachedRigidbody = GetComponent<Rigidbody>();
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
            RefillIntegrity();
            BeginSimulationStep();
        }

        void OnValidate()
        {
            ResolveReferences();
        }

        void FixedUpdate()
        {
            if (externalSimulation)
            {
                return;
            }

            Step();
            BeginSimulationStep();
        }

        public void SetExternalSimulation(bool enabled)
        {
            externalSimulation = enabled;
        }

        public void BeginSimulationStep()
        {
            Rigidbody body = Rigidbody;
            preStepLinearVelocity = body.linearVelocity;
            preStepAngularVelocity = body.angularVelocity;
        }

        public void Step()
        {
            SyncCapacity();
            float impactSpeed = pendingImpactSpeed;
            pendingImpactSpeed = 0f;
            if (impactSpeed > 0f)
            {
                ApplyImpact(impactSpeed);
            }
        }

        public void DiscardPendingImpact()
        {
            pendingImpactSpeed = 0f;
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

        float ApplyDamage(float damage)
        {
            float applied = integrity.Drain(damage);
            if (applied <= 0f)
            {
                return 0f;
            }

            ApplyRuntimeState();
            RaiseDamaged(applied);
            return applied;
        }

        public void RefillIntegrity()
        {
            integrity = ResourcePool.Full(Capacity);
            ApplyRuntimeState();
        }

        public void RestoreIntegrity(ResourcePool saved)
        {
            integrity = new ResourcePool(Capacity, saved.Current);
            ApplyRuntimeState();
        }

        public void SetIntegrityAmount(float current, bool notifyDamage = true)
        {
            float previous = integrity.Current;
            integrity = new ResourcePool(Capacity, current);
            ApplyRuntimeState();
            if (notifyDamage)
            {
                RaiseDamaged(previous - integrity.Current);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            CaptureImpact(collision);
        }

        void CaptureImpact(Collision collision)
        {
            SpacecraftFlightProfile profile = motor != null ? motor.FlightProfile : null;
            if (profile == null || collision.contactCount <= 0)
            {
                return;
            }

            CelestialBody celestial = ResolveCelestialBody(collision.collider);
            Rigidbody other = collision.rigidbody;
            if (celestial == null &&
                other != null &&
                other.mass < Rigidbody.mass * profile.MinimumImpactMassRatio)
            {
                return;
            }

            Vector3 point = collision.GetContact(0).point;
            Vector3 otherVelocity = celestial != null
                ? celestial.GetVelocityAtPoint(point)
                : other != null
                    ? other.GetPointVelocity(point)
                    : Vector3.zero;
            Vector3 shipVelocity = preStepLinearVelocity + Vector3.Cross(
                preStepAngularVelocity,
                point - Rigidbody.worldCenterOfMass);
            RegisterImpact((shipVelocity - otherVelocity).magnitude);
        }

        public void RegisterImpact(float impactSpeed)
        {
            pendingImpactSpeed = Mathf.Max(pendingImpactSpeed, impactSpeed);
        }

        static CelestialBody ResolveCelestialBody(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            return collider.TryGetComponent(out CelestialBody body)
                ? body
                : collider.GetComponentInParent<CelestialBody>();
        }

        void RaiseDamaged(float amount)
        {
            if (amount > 0f)
            {
                Damaged?.Invoke(amount);
            }
        }

        void ResolveReferences()
        {
            motor ??= GetComponent<SpacecraftMotor>();
            cachedRigidbody ??= GetComponent<Rigidbody>();
        }

        void ApplyRuntimeState()
        {
            integrityNormalized = integrity.Normalized;
            motor?.SetDriveDisabled(IsBreached);
        }
    }
}
