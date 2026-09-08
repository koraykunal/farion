using System.Collections.Generic;
using Farion.Core.Physics;
using Farion.Gameplay.Interaction;
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

        Vector3 visualLocalPosition;
        Quaternion visualLocalRotation = Quaternion.identity;

        void Awake()
        {
            ClassifyColliderLayers();
            if (visualRoot != null)
            {
                visualLocalPosition = visualRoot.localPosition;
                visualLocalRotation = visualRoot.localRotation;
            }
        }

        public bool TryGetGraphicalPose(
            Transform anchor,
            out Vector3 position,
            out Quaternion rotation)
        {
            if (anchor == null)
            {
                position = default;
                rotation = Quaternion.identity;
                return false;
            }

            if (visualRoot == null)
            {
                position = anchor.position;
                rotation = anchor.rotation;
                return true;
            }

            Quaternion graphicalRootRotation =
                visualRoot.rotation * Quaternion.Inverse(visualLocalRotation);
            Vector3 graphicalRootPosition =
                visualRoot.position - graphicalRootRotation * visualLocalPosition;
            Vector3 localPosition = transform.InverseTransformPoint(anchor.position);
            Quaternion localRotation = Quaternion.Inverse(transform.rotation) * anchor.rotation;
            position = graphicalRootPosition + graphicalRootRotation * localPosition;
            rotation = graphicalRootRotation * localRotation;
            return true;
        }

        void OnValidate()
        {
            AutoAssignReferences();
        }

        void Reset()
        {
            AutoAssignReferences();
        }

        [ContextMenu("Classify Collider Layers")]
        public void ClassifyColliderLayers()
        {
            List<Collider> colliders = new();
            GetComponentsInChildren(true, colliders);
            for (int i = 0; i < colliders.Count; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                GameObject owner = collider.gameObject;
                if (collider.isTrigger)
                {
                    owner.layer = FarionLayers.Interactable;
                    continue;
                }

                owner.layer = owner.GetComponentInParent<SpacecraftInteriorCollider>() != null
                    ? FarionLayers.SpacecraftInterior
                    : FarionLayers.SpacecraftExterior;
            }
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
