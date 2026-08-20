using UnityEngine;

namespace Farion.Audio.Spacecraft
{
    [CreateAssetMenu(menuName = "Farion/Audio/Spacecraft/Audio Tuning Profile", fileName = "SO_SpacecraftAudioTuningProfile")]
    public sealed class SpacecraftAudioTuningProfile : ScriptableObject
    {
        [Header("Normalization")]
        [Min(0.01f)]
        [SerializeField] float referenceSpeed = 260f;
        [Min(0.01f)]
        [SerializeField] float referenceLinearAcceleration = 24f;
        [Min(0.01f)]
        [SerializeField] float referenceAngularAcceleration = 4f;
        [Min(0.01f)]
        [SerializeField] float referenceImpactSpeed = 12f;

        [Header("Load Blend")]
        [Range(0f, 1f)]
        [SerializeField] float accelerationLoadWeight = 0.85f;
        [Range(0f, 1f)]
        [SerializeField] float boostLoadFloor = 0.75f;

        public float NormalizeSpeed(float speed)
        {
            return Mathf.Clamp01(Mathf.Max(0f, speed) / referenceSpeed);
        }

        public float NormalizeLinearAcceleration(float acceleration)
        {
            return Mathf.Clamp01(Mathf.Max(0f, acceleration) / referenceLinearAcceleration);
        }

        public float NormalizeAngularAcceleration(float acceleration)
        {
            return Mathf.Clamp01(Mathf.Max(0f, acceleration) / referenceAngularAcceleration);
        }

        public float NormalizeImpactSpeed(float impactSpeed)
        {
            return Mathf.Clamp01(Mathf.Max(0f, impactSpeed) / referenceImpactSpeed);
        }

        public float AccelerationLoadWeight => accelerationLoadWeight;
        public float BoostLoadFloor => boostLoadFloor;

        void OnValidate()
        {
            referenceSpeed = Mathf.Max(0.01f, referenceSpeed);
            referenceLinearAcceleration = Mathf.Max(0.01f, referenceLinearAcceleration);
            referenceAngularAcceleration = Mathf.Max(0.01f, referenceAngularAcceleration);
            referenceImpactSpeed = Mathf.Max(0.01f, referenceImpactSpeed);
            accelerationLoadWeight = Mathf.Clamp01(accelerationLoadWeight);
            boostLoadFloor = Mathf.Clamp01(boostLoadFloor);
        }
    }
}
