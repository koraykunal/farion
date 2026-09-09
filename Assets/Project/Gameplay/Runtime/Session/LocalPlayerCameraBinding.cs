using Farion.Gameplay.Character;
using Farion.Gameplay.Flight;
using UnityEngine;

namespace Farion.Gameplay.Session
{
    public static class LocalPlayerCameraBinding
    {
        public static void FollowExplorer(
            ExplorerCameraRig explorerRig,
            SpacecraftCameraRig spacecraftRig,
            ExplorerMotor motor,
            ExplorerInput input)
        {
            if (spacecraftRig != null)
            {
                spacecraftRig.enabled = false;
            }

            if (explorerRig == null)
            {
                return;
            }

            explorerRig.enabled = true;
            explorerRig.SetTarget(motor);
            explorerRig.SetInputSource(input);
        }

        public static void FollowSpacecraft(
            ExplorerCameraRig explorerRig,
            SpacecraftCameraRig spacecraftCameraRig,
            SpacecraftRig rig,
            SpacecraftMotor motor,
            Transform fallbackTarget,
            SpacecraftPilotCameraView view)
        {
            if (explorerRig != null)
            {
                explorerRig.enabled = false;
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
            ExplorerCameraRig explorerRig,
            SpacecraftCameraRig spacecraftCameraRig)
        {
            if (explorerRig != null)
            {
                explorerRig.SetTarget(null);
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
