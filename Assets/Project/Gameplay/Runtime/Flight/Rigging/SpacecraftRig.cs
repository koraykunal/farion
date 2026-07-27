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
        [SerializeField] Transform cockpitCameraTarget;

        public Transform VisualRoot => visualRoot;
        public Transform InteriorSpawnPoint => interiorSpawnPoint;
        public Transform ExteriorExitPoint => exteriorExitPoint;
        public Transform PilotSeatPoint => pilotSeatPoint;
        public SpacecraftRampController RampController => rampController;
        public Transform ChaseCameraTarget => chaseCameraTarget;
        public Transform CockpitCameraTarget => cockpitCameraTarget;

        void OnValidate()
        {
            AutoAssignReferences();
        }

        void Reset()
        {
            AutoAssignReferences();
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

        void AutoAssignReferences()
        {
            visualRoot ??= SpacecraftRigTransformResolver.FindChild(transform, "VisualRoot");
            rampController ??= GetComponentInChildren<SpacecraftRampController>(true);
            interiorSpawnPoint ??= SpacecraftRigTransformResolver.FindChild(transform, "InteriorSpawnPoint");
            exteriorExitPoint ??= SpacecraftRigTransformResolver.FindChild(transform, "ExteriorExitPoint");
            pilotSeatPoint ??= SpacecraftRigTransformResolver.FindChild(transform, "PilotSeatPoint");
            chaseCameraTarget ??= SpacecraftRigTransformResolver.FindChild(transform, "ChaseCameraTarget");
            cockpitCameraTarget ??= SpacecraftRigTransformResolver.FindChild(transform, "CockpitCameraTarget");
        }
    }
}
