using System.Collections.Generic;
using Farion.Gameplay.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Farion.UI.Input
{
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InputSystemUIInputModule))]
    public sealed class UiInputModuleBinder : MonoBehaviour
    {
        [SerializeField] InputSystemUIInputModule inputModule;

        readonly List<InputActionReference> ownedReferences = new();

        void Reset()
        {
            inputModule = GetComponent<InputSystemUIInputModule>();
        }

        void Awake()
        {
            Configure();
        }

        void OnDestroy()
        {
            ReleaseReferences();
        }

        public void Configure()
        {
            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"{nameof(UiInputModuleBinder)} binds runtime-created actions only in Play Mode. " +
                    "No input actions were serialized into the scene.",
                    this);
#endif
                return;
            }

            inputModule ??= GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                return;
            }

            ReleaseReferences();
            FarionInputActions.Enable();

            inputModule.actionsAsset = FarionInputActions.Asset;
            inputModule.point = Own(FarionInputActions.UiPoint);
            inputModule.move = Own(FarionInputActions.UiNavigate);
            inputModule.submit = Own(FarionInputActions.UiSubmit);
            inputModule.cancel = Own(FarionInputActions.UiCancel);
            inputModule.leftClick = Own(FarionInputActions.UiLeftClick);
            inputModule.middleClick = Own(FarionInputActions.UiMiddleClick);
            inputModule.rightClick = Own(FarionInputActions.UiRightClick);
            inputModule.scrollWheel = Own(FarionInputActions.UiScrollWheel);
        }

        InputActionReference Own(InputAction action)
        {
            InputActionReference reference = InputActionReference.Create(action);
            reference.hideFlags = HideFlags.HideAndDontSave;
            ownedReferences.Add(reference);
            return reference;
        }

        void ReleaseReferences()
        {
            for (int i = 0; i < ownedReferences.Count; i++)
            {
                InputActionReference reference = ownedReferences[i];
                if (reference == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(reference);
                }
                else
                {
                    DestroyImmediate(reference);
                }
            }

            ownedReferences.Clear();
        }
    }
}
