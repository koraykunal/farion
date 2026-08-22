using System;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Presentation.Lighting;
using UnityEngine;

namespace Farion.Gameplay.Presentation.Flight
{
    [DisallowMultipleComponent]
    public sealed class SpacecraftHullAlarm : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] SpacecraftHull hull;

        [Header("Fixtures")]
        [Tooltip("May include the front floodlights. They stutter on every impact, and if a " +
            "fixture also carries an alarm profile it turns red once the hull is breached.")]
        [SerializeField] LightFixture[] fixtures = Array.Empty<LightFixture>();

        [Header("Response")]
        [Tooltip("Impact damage that produces a full-strength stutter. Lower values make the " +
            "lamps react to scrapes, higher values reserve the flicker for real collisions.")]
        [Min(0.01f)]
        [SerializeField] float fullFlickerDamage = 60f;

        [Header("Runtime")]
        [SerializeField] bool alarmActive;

        public bool IsAlarming => alarmActive;

        void Reset()
        {
            AutoAssignReferences();
        }

        void OnValidate()
        {
            fullFlickerDamage = Mathf.Max(0.01f, fullFlickerDamage);
            AutoAssignReferences();
        }

        void Awake()
        {
            AutoAssignReferences();
        }

        void OnEnable()
        {
            AutoAssignReferences();
            if (hull != null)
            {
                hull.Damaged += HandleHullDamaged;
            }
        }

        void OnDisable()
        {
            if (hull != null)
            {
                hull.Damaged -= HandleHullDamaged;
            }
        }

        void Update()
        {
            bool breached = hull != null && hull.IsBreached;
            if (breached == alarmActive)
            {
                return;
            }

            alarmActive = breached;
            for (int i = 0; i < fixtures.Length; i++)
            {
                fixtures[i]?.SetAlarm(breached);
            }
        }

        void HandleHullDamaged(float amount)
        {
            float severity = Mathf.Clamp01(amount / fullFlickerDamage);
            for (int i = 0; i < fixtures.Length; i++)
            {
                fixtures[i]?.PlayFaultFlicker(severity);
            }
        }

        void AutoAssignReferences()
        {
            hull ??= GetComponentInParent<SpacecraftHull>();
        }
    }
}
