using UnityEngine;

namespace Farion.Gameplay.Flight
{
    readonly struct SpacecraftThrusterState
    {
        public SpacecraftThrusterState(
            Vector3 localTranslationInput,
            Vector3 localRotationInput,
            SpacecraftThrusterCommand command,
            float normalizedThrust,
            float engineActivity,
            bool boostActive,
            float boostBlend)
        {
            LocalTranslationInput = ClampAxes(localTranslationInput);
            LocalRotationInput = ClampAxes(localRotationInput);
            Command = command;
            NormalizedThrust = Mathf.Clamp01(normalizedThrust);
            EngineActivity = Mathf.Clamp01(engineActivity);
            BoostActive = boostActive;
            BoostBlend = Mathf.Clamp01(boostBlend);
        }

        public Vector3 LocalTranslationInput { get; }
        public Vector3 LocalRotationInput { get; }
        public SpacecraftThrusterCommand Command { get; }
        public float NormalizedThrust { get; }
        public float EngineActivity { get; }
        public bool BoostActive { get; }
        public float BoostBlend { get; }

        public static SpacecraftThrusterState Idle => new(
            Vector3.zero,
            Vector3.zero,
            SpacecraftThrusterCommand.None,
            0f,
            0f,
            false,
            0f);

        static Vector3 ClampAxes(Vector3 value)
        {
            return new Vector3(
                Mathf.Clamp(value.x, -1f, 1f),
                Mathf.Clamp(value.y, -1f, 1f),
                Mathf.Clamp(value.z, -1f, 1f));
        }
    }
}
