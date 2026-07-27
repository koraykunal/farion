using UnityEngine;

namespace Farion.Gameplay.Flight
{
    static class SpacecraftThrusterIntensityEvaluator
    {
        public static float EvaluateTarget(
            SpacecraftThrusterEffects.ThrusterRole role,
            SpacecraftThrusterState state,
            float intensityScale,
            bool allowBoostIntensity,
            float boostIntensityMultiplier)
        {
            float intensity = Evaluate(role, state) * Mathf.Max(0f, intensityScale);
            if (state.BoostActive && allowBoostIntensity)
            {
                intensity *= Mathf.Max(1f, boostIntensityMultiplier);
            }

            return Mathf.Clamp01(intensity);
        }

        static float Evaluate(SpacecraftThrusterEffects.ThrusterRole role, SpacecraftThrusterState state)
        {
            SpacecraftThrusterCommand command = state.Command;
            Vector3 translation = state.LocalTranslationInput;
            Vector3 rotation = state.LocalRotationInput;

            return role switch
            {
                SpacecraftThrusterEffects.ThrusterRole.MainForward => Mathf.Max(command.Forward, Positive(translation.z)),
                SpacecraftThrusterEffects.ThrusterRole.Reverse => Mathf.Max(command.Reverse, Negative(translation.z)),
                SpacecraftThrusterEffects.ThrusterRole.StrafeLeft => Mathf.Max(command.StrafeLeft, Negative(translation.x)),
                SpacecraftThrusterEffects.ThrusterRole.StrafeRight => Mathf.Max(command.StrafeRight, Positive(translation.x)),
                SpacecraftThrusterEffects.ThrusterRole.Ascend => Mathf.Max(command.Ascend, Positive(translation.y)),
                SpacecraftThrusterEffects.ThrusterRole.Descend => Mathf.Max(command.Descend, Negative(translation.y)),
                SpacecraftThrusterEffects.ThrusterRole.PitchUp => Mathf.Max(command.PitchUp, Positive(rotation.x)),
                SpacecraftThrusterEffects.ThrusterRole.PitchDown => Mathf.Max(command.PitchDown, Negative(rotation.x)),
                SpacecraftThrusterEffects.ThrusterRole.YawLeft => Mathf.Max(command.YawLeft, Negative(rotation.y)),
                SpacecraftThrusterEffects.ThrusterRole.YawRight => Mathf.Max(command.YawRight, Positive(rotation.y)),
                SpacecraftThrusterEffects.ThrusterRole.RollLeft => Mathf.Max(command.RollLeft, Negative(rotation.z)),
                SpacecraftThrusterEffects.ThrusterRole.RollRight => Mathf.Max(command.RollRight, Positive(rotation.z)),
                SpacecraftThrusterEffects.ThrusterRole.EngineActivity => state.EngineActivity,
                _ => 0f
            };
        }

        static float Positive(float value)
        {
            return Mathf.Clamp01(value);
        }

        static float Negative(float value)
        {
            return Mathf.Clamp01(-value);
        }
    }
}
