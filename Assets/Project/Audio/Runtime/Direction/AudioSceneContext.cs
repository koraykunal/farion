using Farion.Audio.Spacecraft;
using UnityEngine;

namespace Farion.Audio.Direction
{
    [DefaultExecutionOrder(330)]
    [DisallowMultipleComponent]
    public sealed class AudioSceneContext : MonoBehaviour
    {
        [SerializeField] AudioSceneContextId contextId;
        [SerializeField] ShipAudioTelemetryProvider spacecraftTelemetry;

        AudioDirector director;

        void OnEnable()
        {
            BindDirector();
        }

        void Update()
        {
            if (director == null)
            {
                BindDirector();
            }

            if (director == null || contextId != AudioSceneContextId.Gameplay)
            {
                return;
            }

            ShipAudioTelemetry telemetry = spacecraftTelemetry != null
                ? spacecraftTelemetry.Telemetry
                : ShipAudioTelemetry.Silent;
            bool isInterior = telemetry.Perspective is
                SpacecraftAudioPerspective.Cockpit or
                SpacecraftAudioPerspective.ShipInterior;
            director.SetEnvironment(telemetry.Atmosphere, isInterior);
        }

        void BindDirector()
        {
            director = AudioDirector.Current;
            if (director == null)
            {
                return;
            }

            director.SetSceneContext(contextId);
            if (contextId == AudioSceneContextId.MainMenu)
            {
                director.SetEnvironment(0f, false);
            }
        }
    }
}
