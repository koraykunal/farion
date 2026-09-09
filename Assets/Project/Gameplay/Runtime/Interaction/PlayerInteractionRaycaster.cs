using System.Collections.Generic;
using Farion.Core.Physics;
using Farion.Gameplay.Character;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Gameplay.Interaction
{
    [DefaultExecutionOrder(250)]
    [DisallowMultipleComponent]
    public sealed class PlayerInteractionRaycaster : MonoBehaviour
    {
        const int MaxHits = 12;

        [Header("Input")]
        [SerializeField] ExplorerInput inputSource;
        [SerializeField] PlayerControlLock controlLock;

        [Header("Raycast")]
        [SerializeField] Transform viewReference;
        [Min(0.1f)]
        [SerializeField] float maxDistance = 4f;
        [Min(0f)]
        [SerializeField] float castRadius = 0.35f;
        [SerializeField] LayerMask interactionLayers = FarionLayers.InteractionMask;
        [SerializeField] QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Runtime")]
        [SerializeField] bool hasTarget;
        [SerializeField] string currentPrompt;
        [SerializeField] Transform currentTargetTransform;

        readonly RaycastHit[] hits = new RaycastHit[MaxHits];
        readonly List<MonoBehaviour> behaviourBuffer = new(8);
        ExplorerInput resolvedInput;
        Collider[] ownColliders;
        IGameplayCommandGateway commandGateway;
        IInteractable currentInteractable;
        RaycastHit currentHit;

        public bool HasTarget => hasTarget;
        public string CurrentPrompt => currentPrompt;
        public Transform CurrentTargetTransform => currentTargetTransform;

        void Awake()
        {
            ResolveInputSource();
            ownColliders = GetComponentsInChildren<Collider>(true);
        }

        void OnValidate()
        {
            maxDistance = Mathf.Max(0.1f, maxDistance);
            castRadius = Mathf.Max(0f, castRadius);
        }

        void OnDisable()
        {
            ClearTarget();
        }

        void Update()
        {
            ResolveInputSource();
            if (IsGameplayInputLocked())
            {
                ClearTarget();
                return;
            }

            RefreshTarget();

            ExplorerInputState input = resolvedInput?.CurrentInput ?? ExplorerInputState.None;
            if (input.Interact && currentInteractable != null)
            {
                InteractionContext context = new(
                    gameObject,
                    viewReference,
                    currentHit,
                    commandGateway);
                if (currentInteractable.CanInteract(context))
                {
                    currentInteractable.Interact(context);
                }
            }
        }

        public void SetControlLock(PlayerControlLock nextControlLock)
        {
            controlLock = nextControlLock;
        }

        public void SetViewReference(Transform nextViewReference)
        {
            viewReference = nextViewReference;
        }

        public void SetCommandGateway(IGameplayCommandGateway nextCommandGateway)
        {
            commandGateway = nextCommandGateway;
        }

        void ClearTarget()
        {
            currentInteractable = null;
            currentTargetTransform = null;
            currentPrompt = string.Empty;
            hasTarget = false;
        }

        void RefreshTarget()
        {
            ClearTarget();

            Transform view = viewReference != null ? viewReference : transform;
            Vector3 origin = view.position;
            if (viewReference != null)
            {
                origin += view.forward * Mathf.Max(0f, Vector3.Dot(transform.position - view.position, view.forward));
            }

            Ray ray = new(origin, view.forward);
            PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();
            int blockingLayers = interactionLayers | FarionLayers.CameraObstacleMask;
            int hitCount = castRadius > 0f
                ? physicsScene.SphereCast(
                    ray.origin,
                    castRadius,
                    ray.direction,
                    hits,
                    maxDistance,
                    blockingLayers,
                    triggerInteraction)
                : physicsScene.Raycast(
                    ray.origin,
                    ray.direction,
                    hits,
                    maxDistance,
                    blockingLayers,
                    triggerInteraction);

            if (!TryFindNearestHit(hitCount, out RaycastHit nearest))
            {
                return;
            }

            InteractionContext context = new(
                gameObject,
                view,
                nearest,
                commandGateway);
            IInteractable interactable = ResolveInteractable(
                nearest.collider,
                context);
            if (interactable == null)
            {
                return;
            }

            currentHit = nearest;
            currentInteractable = interactable;
            currentTargetTransform = nearest.collider.transform;
            currentPrompt = interactable.InteractionPrompt;
            hasTarget = true;
        }

        bool TryFindNearestHit(int hitCount, out RaycastHit nearest)
        {
            nearest = default;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null ||
                    IsOwnCollider(hit.collider) ||
                    hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                nearest = hit;
            }

            return closestDistance < float.PositiveInfinity;
        }

        void ResolveInputSource()
        {
            if (inputSource != null)
            {
                resolvedInput = inputSource;
                return;
            }

            resolvedInput ??= GetComponent<ExplorerInput>();
        }

        bool IsGameplayInputLocked()
        {
            return controlLock != null && controlLock.IsGameplayInputLocked;
        }

        bool IsOwnCollider(Collider candidate)
        {
            if (ownColliders == null)
            {
                return false;
            }

            for (int i = 0; i < ownColliders.Length; i++)
            {
                if (ownColliders[i] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        IInteractable ResolveInteractable(
            Collider hitCollider,
            InteractionContext context)
        {
            Transform current = hitCollider.transform;
            while (current != null)
            {
                behaviourBuffer.Clear();
                current.GetComponents(behaviourBuffer);
                for (int i = 0; i < behaviourBuffer.Count; i++)
                {
                    if (behaviourBuffer[i] is IInteractable interactable &&
                        interactable.CanInteract(context))
                    {
                        return interactable;
                    }
                }

                current = current.parent;
            }

            return null;
        }
    }
}
