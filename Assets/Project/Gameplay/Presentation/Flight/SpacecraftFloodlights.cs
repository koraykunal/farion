using System;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Presentation.Lighting;
using UnityEngine;

namespace Farion.Gameplay.Presentation.Flight
{
    [DisallowMultipleComponent]
    public sealed class SpacecraftFloodlights : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] KeyboardSpacecraftInput inputSource;

        [Header("Fixtures")]
        [SerializeField] LightFixture[] fixtures = Array.Empty<LightFixture>();

        [Header("Control")]
        [Tooltip("Cleared on the networked shuttle variant so the toggle can only arrive " +
            "through the server, exactly like the landing gear.")]
        [SerializeField] bool allowManualToggle = true;
        [SerializeField] bool startOn;

        [Header("Runtime")]
        [SerializeField] bool isOn;

        public bool IsOn => isOn;

        public void SetOn(bool on)
        {
            isOn = on;
            ApplyFixtures();
        }

        public void Toggle()
        {
            SetOn(!isOn);
        }

        void Reset()
        {
            AutoAssignReferences();
        }

        void OnValidate()
        {
            AutoAssignReferences();
        }

        void Awake()
        {
            AutoAssignReferences();
            SetOn(startOn);
        }

        void Update()
        {
            if (!allowManualToggle || inputSource == null)
            {
                return;
            }

            if (inputSource.CurrentInput.ToggleFloodlights)
            {
                Toggle();
            }
        }

        void ApplyFixtures()
        {
            for (int i = 0; i < fixtures.Length; i++)
            {
                fixtures[i]?.SetOn(isOn);
            }
        }

        void AutoAssignReferences()
        {
            inputSource ??= GetComponentInParent<KeyboardSpacecraftInput>();
        }
    }
}
