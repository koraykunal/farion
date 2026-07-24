using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DisallowMultipleComponent]
    public sealed class SpacecraftRig : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] Transform visualRoot;

        [Header("Boarding")]
        [SerializeField] Transform interiorSpawnPoint;
        [SerializeField] Transform exteriorExitPoint;
        [SerializeField] Transform pilotSeatPoint;
        [SerializeField] SpacecraftRampController rampController;

        [Header("Camera Targets")]
        [SerializeField] Transform chaseCameraTarget;
        [SerializeField] Transform landingCameraTarget;
        [SerializeField] Transform cockpitCameraTarget;

        public Transform VisualRoot => visualRoot;
        public Transform InteriorSpawnPoint => interiorSpawnPoint;
        public Transform ExteriorExitPoint => exteriorExitPoint;
        public Transform PilotSeatPoint => pilotSeatPoint;
        public SpacecraftRampController RampController => rampController;
        public Transform ChaseCameraTarget => chaseCameraTarget != null ? chaseCameraTarget : transform;
        public Transform LandingCameraTarget => landingCameraTarget != null ? landingCameraTarget : ChaseCameraTarget;
        public Transform CockpitCameraTarget => cockpitCameraTarget;

        void OnValidate()
        {
            ResolveOptionalReferences();
        }

        void Reset()
        {
            ResolveOptionalReferences();
        }

        public void OpenRamp()
        {
            if (rampController != null)
            {
                rampController.Open();
            }
        }

        public void CloseRamp()
        {
            if (rampController != null)
            {
                rampController.Close();
            }
        }

        public void ToggleRamp()
        {
            if (rampController != null)
            {
                rampController.Toggle();
            }
        }

        void ResolveOptionalReferences()
        {
            visualRoot ??= FindChild("VisualRoot");
            rampController ??= GetComponentInChildren<SpacecraftRampController>(true);
            interiorSpawnPoint ??= FindChild("InteriorSpawnPoint");
            exteriorExitPoint ??= FindChild("ExteriorExitPoint");
            pilotSeatPoint ??= FindChild("PilotSeatPoint");
            chaseCameraTarget ??= FindChild("ChaseCameraTarget");
            landingCameraTarget ??= FindChild("LandingCameraTarget");
            cockpitCameraTarget ??= FindChild("CockpitCameraTarget");
        }

        Transform FindChild(string childName)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child != null && child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
