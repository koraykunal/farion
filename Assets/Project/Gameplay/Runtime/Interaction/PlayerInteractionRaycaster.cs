using System.Collections.Generic;
using Farion.Gameplay.Character;
using Farion.Gameplay.Input;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DefaultExecutionOrder(250)]
    [DisallowMultipleComponent]
    public sealed class PlayerInteractionRaycaster : MonoBehaviour
    {
        const int MaxHits = 12;

        [Header("Input")]
        [SerializeField] MonoBehaviour inputSource;
        [SerializeField] PlayerControlLock controlLock;

        [Header("Raycast")]
        [SerializeField] Transform viewReference;
        [Min(0.1f)]
        [SerializeField] float maxDistance = 4f;
        [Min(0f)]
        [SerializeField] float castRadius = 0.35f;
        [SerializeField] LayerMask interactionLayers = ~0;
        [SerializeField] QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Runtime")]
        [SerializeField] bool hasTarget;
        [SerializeField] string currentPrompt;
        [SerializeField] Transform currentTargetTransform;

        readonly RaycastHit[] hits = new RaycastHit[MaxHits];
        readonly List<MonoBehaviour> behaviourBuffer = new(8);
        IFirstPersonInputSource resolvedInput;
        Collider[] ownColliders;
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
            if (inputSource != null && inputSource is not IFirstPersonInputSource)
            {
                inputSource = null;
            }
        }

        void Update()
        {
            ResolveInputSource();
            ResolveControlLock();
            if (IsGameplayInputLocked())
            {
                ClearTarget();
                return;
            }

            RefreshTarget();

            FirstPersonInputState input = resolvedInput?.CurrentInput ?? FirstPersonInputState.None;
            if (input.Interact && currentInteractable != null)
            {
                InteractionContext context = new(gameObject, viewReference, currentHit);
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
            Ray ray = new(view.position, view.forward);
            int hitCount = castRadius > 0f
                ? Physics.SphereCastNonAlloc(
                    ray,
                    castRadius,
                    hits,
                    maxDistance,
                    interactionLayers,
                    triggerInteraction)
                : Physics.RaycastNonAlloc(
                    ray,
                    hits,
                    maxDistance,
                    interactionLayers,
                    triggerInteraction);

            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || IsOwnCollider(hit.collider) || hit.distance >= closestDistance)
                {
                    continue;
                }

                IInteractable interactable = ResolveInteractable(hit.collider);
                if (interactable == null)
                {
                    continue;
                }

                InteractionContext context = new(gameObject, view, hit);
                if (!interactable.CanInteract(context))
                {
                    continue;
                }

                closestDistance = hit.distance;
                currentHit = hit;
                currentInteractable = interactable;
                currentTargetTransform = hit.collider.transform;
                currentPrompt = interactable.InteractionPrompt;
                hasTarget = true;
            }
        }

        void ResolveInputSource()
        {
            if (inputSource is IFirstPersonInputSource explicitSource)
            {
                resolvedInput = explicitSource;
                return;
            }

            resolvedInput ??= GetComponent<IFirstPersonInputSource>();
        }

        void ResolveControlLock()
        {
            if (controlLock == null)
            {
                controlLock = PlayerControlLock.Active;
            }
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

        IInteractable ResolveInteractable(Collider hitCollider)
        {
            Transform current = hitCollider.transform;
            while (current != null)
            {
                behaviourBuffer.Clear();
                current.GetComponents(behaviourBuffer);
                for (int i = 0; i < behaviourBuffer.Count; i++)
                {
                    if (behaviourBuffer[i] is IInteractable interactable)
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
