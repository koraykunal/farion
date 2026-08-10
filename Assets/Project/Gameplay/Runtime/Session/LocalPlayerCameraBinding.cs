using Farion.Gameplay.Character;
using Farion.Gameplay.Flight;
using UnityEngine;

namespace Farion.Gameplay.Session
{
    public static class LocalPlayerCameraBinding
    {
        public static void FollowExplorer(
            FirstPersonCameraRig firstPersonRig,
            SpacecraftCameraRig spacecraftRig,
            FirstPersonMotor motor,
            IFirstPersonInputSource input)
        {
            if (spacecraftRig != null)
            {
                spacecraftRig.enabled = false;
            }

            if (firstPersonRig == null)
            {
                return;
            }

            firstPersonRig.enabled = true;
            firstPersonRig.SetTarget(motor);
            firstPersonRig.SetInputSource(input);
        }

        public static void FollowSpacecraft(
            FirstPersonCameraRig firstPersonRig,
            SpacecraftCameraRig spacecraftCameraRig,
            SpacecraftRig rig,
            SpacecraftMotor motor,
            Transform fallbackTarget,
            SpacecraftPilotCameraView view)
        {
            if (firstPersonRig != null)
            {
                firstPersonRig.enabled = false;
            }

            if (spacecraftCameraRig == null)
            {
                return;
            }

            spacecraftCameraRig.SetExteriorTarget(ResolveExteriorTarget(rig, fallbackTarget));
            spacecraftCameraRig.SetCockpitTarget(rig != null ? rig.CockpitCameraTarget : null);
            spacecraftCameraRig.SetMotor(motor);
            spacecraftCameraRig.SetView(view);
            spacecraftCameraRig.enabled = true;
            spacecraftCameraRig.SnapToTarget();
        }

        public static void Release(
            FirstPersonCameraRig firstPersonRig,
            SpacecraftCameraRig spacecraftCameraRig)
        {
            if (firstPersonRig != null)
            {
                firstPersonRig.SetTarget(null);
            }

            if (spacecraftCameraRig != null)
            {
                spacecraftCameraRig.SetMotor(null);
                spacecraftCameraRig.enabled = false;
            }
        }

        public static void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        static Transform ResolveExteriorTarget(SpacecraftRig rig, Transform fallbackTarget)
        {
            if (rig != null && rig.ChaseCameraTarget != null)
            {
                return rig.ChaseCameraTarget;
            }

            return fallbackTarget;
        }
    }
}
