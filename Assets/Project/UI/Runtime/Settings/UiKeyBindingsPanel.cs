using System;
using System.Collections.Generic;
using System.Text;
using Farion.Gameplay.Input;
using Farion.UI.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Farion.UI.Settings
{
    [DisallowMultipleComponent]
    public sealed class UiKeyBindingsPanel : MonoBehaviour
    {
        static readonly string[] RebindableMaps = { "OnFoot", "Flight", "Vehicle" };

        readonly struct BindingRow
        {
            public BindingRow(InputAction action, int bindingIndex, string label)
            {
                Action = action;
                BindingIndex = bindingIndex;
                Label = label;
            }

            public InputAction Action { get; }
            public int BindingIndex { get; }
            public string Label { get; }
        }

        [Header("Composition")]
        [SerializeField] UiKeyBindingRowView rowTemplate;
        [SerializeField] RectTransform rowContainer;
        [SerializeField] TMP_Text statusText;

        readonly List<BindingRow> rows = new();
        readonly List<UiKeyBindingRowView> rowViews = new();
        readonly StringBuilder labelBuilder = new();
        InputActionRebindingExtensions.RebindingOperation activeRebind;
        int activeRowIndex = -1;
        int conflictRowIndex = -1;
        string overrideBeforeRebind;

        void OnEnable()
        {
            activeRowIndex = -1;
            conflictRowIndex = -1;
            BuildRows();
            RefreshRows();
            SetStatus(UiTextKeys.SettingsBindingsHint);
        }

        void OnDisable()
        {
            CancelActiveRebind();
        }

        public void ResetAll()
        {
            CancelActiveRebind();
            conflictRowIndex = -1;
            FarionInputActions.ResetBindingOverrides();
            RefreshRows();
            SetStatus(UiTextKeys.SettingsBindingsReset);
        }

        void BuildRows()
        {
            if (rows.Count > 0 || rowTemplate == null || rowContainer == null)
            {
                return;
            }

            InputActionAsset asset = FarionInputActions.Asset;
            for (int m = 0; m < RebindableMaps.Length; m++)
            {
                InputActionMap map = asset.FindActionMap(RebindableMaps[m]);
                if (map == null)
                {
                    continue;
                }

                foreach (InputAction action in map.actions)
                {
                    CollectBindings(action);
                }
            }

            for (int i = 0; i < rows.Count; i++)
            {
                UiKeyBindingRowView view = Instantiate(rowTemplate, rowContainer);
                view.gameObject.SetActive(true);
                view.name = $"Binding_{i:00}";
                view.RebindRequested += HandleRebindRequested;
                rowViews.Add(view);
            }
        }

        void CollectBindings(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite || !IsRebindablePath(binding.effectivePath))
                {
                    continue;
                }

                rows.Add(new BindingRow(action, i, BuildLabel(action, binding)));
            }
        }

        static bool IsRebindablePath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                path.StartsWith("<Keyboard>", StringComparison.Ordinal);
        }

        string BuildLabel(InputAction action, InputBinding binding)
        {
            labelBuilder.Clear();
            AppendSpaced(action.name);
            if (binding.isPartOfComposite && !string.IsNullOrEmpty(binding.name))
            {
                labelBuilder.Append(' ');
                AppendSpaced(binding.name);
            }

            return labelBuilder.ToString();
        }

        void AppendSpaced(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0 && char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
                {
                    labelBuilder.Append(' ');
                }

                labelBuilder.Append(value[i]);
            }
        }

        void RefreshRows()
        {
            for (int i = 0; i < rowViews.Count && i < rows.Count; i++)
            {
                RefreshRow(i);
            }
        }

        void RefreshRow(int index)
        {
            if (index == activeRowIndex)
            {
                rowViews[index].SetBinding(
                    UiLocalization.Get(UiTextKeys.SettingsBindingsListening),
                    UiKeyBindingState.Listening);
                return;
            }

            BindingRow row = rows[index];
            rowViews[index].Configure(
                row.Label,
                UiLocalization.Get(UiTextKeys.SettingsBindingsHint),
                ResolveBindingDisplay(row),
                ResolveRowState(index, row));
        }

        UiKeyBindingState ResolveRowState(int index, BindingRow row)
        {
            if (index == conflictRowIndex)
            {
                return UiKeyBindingState.Conflict;
            }

            InputBinding binding = row.Action.bindings[row.BindingIndex];
            if (string.IsNullOrEmpty(binding.effectivePath))
            {
                return UiKeyBindingState.Unbound;
            }

            return string.IsNullOrEmpty(binding.overridePath)
                ? UiKeyBindingState.Default
                : UiKeyBindingState.Customized;
        }

        static string ResolveBindingDisplay(BindingRow row)
        {
            string display = row.Action.GetBindingDisplayString(
                row.BindingIndex,
                InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
            return string.IsNullOrWhiteSpace(display)
                ? UiLocalization.Get(UiTextKeys.SettingsBindingsUnbound)
                : display;
        }

        void HandleRebindRequested(UiKeyBindingRowView view)
        {
            BeginRebind(rowViews.IndexOf(view));
        }

        void BeginRebind(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= rows.Count)
            {
                return;
            }

            CancelActiveRebind();

            BindingRow row = rows[rowIndex];
            activeRowIndex = rowIndex;
            conflictRowIndex = -1;
            RefreshRows();
            SetStatus(UiTextKeys.SettingsBindingsListening);

            InputBinding binding = row.Action.bindings[row.BindingIndex];
            overrideBeforeRebind = binding.overridePath;
            row.Action.Disable();
            InputActionRebindingExtensions.RebindingOperation operation =
                row.Action
                    .PerformInteractiveRebinding(row.BindingIndex)
                    .WithControlsExcluding("<Mouse>")
                    .WithCancelingThrough("<Keyboard>/escape")
                    .WithMatchingEventsBeingSuppressed()
                    .OnComplete(_ => CompleteRebind(row, rowIndex))
                    .OnCancel(_ => CancelRebind(row));
            if (binding.isPartOfComposite)
            {
                operation = operation.WithExpectedControlType("Button");
            }

            activeRebind = operation;
            operation.Start();
        }

        void CompleteRebind(BindingRow row, int rowIndex)
        {
            DisposeActiveRebind();
            row.Action.Enable();
            activeRowIndex = -1;
            if (TryFindConflict(row, out string conflictLabel))
            {
                row.Action.ApplyBindingOverride(
                    row.BindingIndex,
                    overrideBeforeRebind);
                conflictRowIndex = rowIndex;
                SetStatus(UiTextKeys.SettingsBindingsConflict, conflictLabel);
            }
            else
            {
                conflictRowIndex = -1;
                FarionInputActions.SaveBindingOverrides();
                SetStatus(UiTextKeys.SettingsBindingsSaved);
            }

            RefreshRows();
        }

        void CancelRebind(BindingRow row)
        {
            DisposeActiveRebind();
            row.Action.Enable();
            activeRowIndex = -1;
            RefreshRows();
            SetStatus(UiTextKeys.SettingsBindingsHint);
        }

        void CancelActiveRebind()
        {
            if (activeRebind == null)
            {
                return;
            }

            if (activeRowIndex >= 0 && activeRowIndex < rows.Count)
            {
                rows[activeRowIndex].Action.Enable();
            }

            activeRebind.Cancel();
            DisposeActiveRebind();
            activeRowIndex = -1;
            RefreshRows();
        }

        void DisposeActiveRebind()
        {
            activeRebind?.Dispose();
            activeRebind = null;
        }

        bool TryFindConflict(BindingRow changed, out string conflictLabel)
        {
            conflictLabel = string.Empty;
            string path = changed.Action.bindings[changed.BindingIndex].effectivePath;
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            InputActionMap map = changed.Action.actionMap;
            for (int i = 0; i < rows.Count; i++)
            {
                BindingRow other = rows[i];
                if (other.Action.actionMap != map)
                {
                    continue;
                }

                if (other.Action == changed.Action &&
                    other.BindingIndex == changed.BindingIndex)
                {
                    continue;
                }

                if (other.Action.bindings[other.BindingIndex].effectivePath == path)
                {
                    conflictLabel = other.Label;
                    return true;
                }
            }

            return false;
        }

        void SetStatus(string entryKey, string argument = null)
        {
            if (statusText == null)
            {
                return;
            }

            string message = UiLocalization.Get(entryKey);
            statusText.SetText(
                argument == null
                    ? message
                    : string.Format(message, argument));
        }
    }
}
