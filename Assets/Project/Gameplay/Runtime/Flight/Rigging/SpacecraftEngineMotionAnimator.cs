using System;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DisallowMultipleComponent]
    public sealed class SpacecraftEngineMotionAnimator : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] Transform visualRoot;
        [SerializeField] SpacecraftMotor motor;

        [Header("Response")]
        [SerializeField] bool animateFromThrust = true;
        [Min(0.01f)]
        [SerializeField] float thrustReferenceAcceleration = 18f;
        [Min(0f)]
        [SerializeField] float response = 8f;
        [SerializeField] SpacecraftEngineMotionPose[] engineMotionParts = Array.Empty<SpacecraftEngineMotionPose>();

        [Header("Runtime")]
        [Range(0f, 1f)]
        [SerializeField] float engineActivity;
        [SerializeField] Vector3 localMotionInput;

        public float EngineActivity => engineActivity;
        public Vector3 LocalMotionInput => localMotionInput;

        void Reset()
        {
            AutoAssignReferences();
        }

        void Awake()
        {
            AutoAssignReferences();
            InitializeParts();
        }

        void OnValidate()
        {
            thrustReferenceAcceleration = Mathf.Max(0.01f, thrustReferenceAcceleration);
            response = Mathf.Max(0f, response);
            ValidateParts();
            AutoAssignReferences();
        }

        void Update()
        {
            UpdateEngineMotion(Time.deltaTime);
        }

        void AutoAssignReferences()
        {
            SpacecraftRig rig = GetComponent<SpacecraftRig>();
            if (rig != null)
            {
                visualRoot ??= rig.VisualRoot;
            }

            motor ??= GetComponent<SpacecraftMotor>();
        }

        void InitializeParts()
        {
            engineActivity = 0f;
            localMotionInput = Vector3.zero;

            if (engineMotionParts == null)
            {
                return;
            }

            for (int i = 0; i < engineMotionParts.Length; i++)
            {
                engineMotionParts[i]?.CaptureInitialPose(visualRoot);
            }

            ApplyParts();
        }

        void ValidateParts()
        {
            if (engineMotionParts == null)
            {
                return;
            }

            for (int i = 0; i < engineMotionParts.Length; i++)
            {
                engineMotionParts[i]?.Validate();
            }
        }

        void UpdateEngineMotion(float deltaTime)
        {
            if (!animateFromThrust || motor == null)
            {
                engineActivity = Mathf.MoveTowards(engineActivity, 0f, response * deltaTime);
                localMotionInput = Vector3.MoveTowards(localMotionInput, Vector3.zero, response * deltaTime);
                ApplyParts();
                return;
            }

            float responseT = response <= 0f ? 1f : 1f - Mathf.Exp(-response * deltaTime);
            float targetActivity = Mathf.Clamp01(motor.LastThrustAcceleration.magnitude / thrustReferenceAcceleration);
            Vector3 targetInput = ClampAxes(motor.LastLocalTranslationInput);

            engineActivity = Mathf.Lerp(engineActivity, targetActivity, responseT);
            localMotionInput = Vector3.Lerp(localMotionInput, targetInput, responseT);
            ApplyParts();
        }

        void ApplyParts()
        {
            if (engineMotionParts == null)
            {
                return;
            }

            Vector3 input = ClampAxes(localMotionInput);
            float activity = Mathf.Clamp01(engineActivity);
            for (int i = 0; i < engineMotionParts.Length; i++)
            {
                engineMotionParts[i]?.Apply(input, activity);
            }
        }

        static Vector3 ClampAxes(Vector3 value)
        {
            return new Vector3(
                Mathf.Clamp(value.x, -1f, 1f),
                Mathf.Clamp(value.y, -1f, 1f),
                Mathf.Clamp(value.z, -1f, 1f));
        }
    }
}
