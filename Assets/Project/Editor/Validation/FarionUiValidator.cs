using Farion.Editor;
using System;
using System.Reflection;
using System.Collections.Generic;
using Farion.UI.Common;
using Farion.UI.Feedback;
using Farion.UI.Foundation;
using Farion.UI.Gameplay;
using Farion.UI.Input;
using Farion.UI.Loading;
using Farion.UI.Localization;
using Farion.UI.Navigation;
using Farion.UI.SaveLoad;
using Farion.UI.Settings;
using Farion.UI.Styling;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Farion.Editor.Validation
{
    public static class FarionUiValidator
    {
        public const string SystemRootPrefabPath =
            FarionAssetPaths.UiSystemRootPrefab;
        public const string ConfirmationPrefabPath =
            FarionAssetPaths.UiConfirmationPrefab;
        public const string LoadingPrefabPath =
            FarionAssetPaths.UiLoadingPrefab;
        public const string FeedbackPrefabPath =
            FarionAssetPaths.UiFeedbackPrefab;
        public const string SettingsPrefabPath =
            FarionAssetPaths.UiSettingsPrefab;
        public const string SaveSlotPrefabPath =
            FarionAssetPaths.UiSaveSlotPrefab;
        public const string SaveLoadPrefabPath =
            FarionAssetPaths.UiSaveLoadPrefab;
        public const string InventoryPrefabPath =
            FarionAssetPaths.UiInventoryPrefab;
        public const string PausePrefabPath =
            FarionAssetPaths.UiPausePrefab;
        static readonly string[] FlightHudGraphicsBindings =
        {
            "speedValueText",
            "fuelValueText",
            "fuelArcFillImage",
            "hullValueText",
            "hullBarFillImage",
            "hullBarFrameImage",
            "assistIndicatorImage",
            "shakeTarget"
        };

        public const string FlightHudPrefabPath =
            FarionAssetPaths.UiFlightHudPrefab;
        const string UiPrefabFolder = FarionAssetPaths.UiPrefabRoot;
        const string MultiplayerPresentationScenePath =
            FarionAssetPaths.GameplayShellScene;
        const string MenuButtonFramePath =
            FarionAssetPaths.UiMenuButtonFrameTexture;
        const string LegacyFontPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        static readonly Vector2 RequiredReferenceResolution = new(1920f, 1080f);
        const float MinimumTextContrast = 4.5f;
        const float MinimumSurfaceLuminanceStep = 1.5f;

        [MenuItem("Farion/Validation/Validate UI Foundation")]
        public static void ValidateFromMenu()
        {
            FarionValidationReport report = new();
            ValidateProject(report);
            report.Log();
            if (report.HasErrors)
            {
                throw new BuildFailedException(
                    $"Farion UI validation failed with {report.Errors.Count} error(s).");
            }
        }

        public static void ValidateProject(FarionValidationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            ValidateFoundationPrefabs(report);
            ValidateBuildScenes(report);
            ValidateLocalizationTables(report);
        }

        static void ValidateLocalizationTables(FarionValidationReport report)
        {
            List<StringTable> tables = new();
            foreach (string guid in AssetDatabase.FindAssets("t:StringTable"))
            {
                StringTable table = AssetDatabase.LoadAssetAtPath<StringTable>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (table != null &&
                    string.Equals(
                        table.TableCollectionName,
                        UiLocalization.TableName,
                        StringComparison.Ordinal))
                {
                    tables.Add(table);
                }
            }

            if (tables.Count == 0)
            {
                report.AddError(
                    $"Localization table collection '{UiLocalization.TableName}' was not found.");
                return;
            }

            HashSet<string> unionKeys = new(StringComparer.Ordinal);
            foreach (StringTable table in tables)
            {
                foreach (StringTableEntry entry in table.Values)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.Key))
                    {
                        unionKeys.Add(entry.Key);
                    }
                }
            }

            foreach (StringTable table in tables)
            {
                string locale = table.LocaleIdentifier.Code;
                foreach (string key in unionKeys)
                {
                    StringTableEntry entry = table.GetEntry(key);
                    if (entry == null || string.IsNullOrWhiteSpace(entry.LocalizedValue))
                    {
                        report.AddError(
                            $"Localization key '{key}' has no '{locale}' translation in '{UiLocalization.TableName}'.");
                    }
                }
            }

            ValidateAuthoredLocalizationKeys(report, unionKeys);
            ValidateDeclaredTextKeys(report, unionKeys);
        }

        static void ValidateDeclaredTextKeys(
            FarionValidationReport report,
            HashSet<string> knownKeys)
        {
            foreach (FieldInfo field in typeof(UiTextKeys).GetFields(
                         BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(string))
                {
                    continue;
                }

                string key = (string)field.GetRawConstantValue();
                if (!knownKeys.Contains(key))
                {
                    report.AddError(
                        $"{nameof(UiTextKeys)}.{field.Name} references unknown localization key '{key}'.");
                }
            }
        }

        static void ValidateAuthoredLocalizationKeys(
            FarionValidationReport report,
            HashSet<string> knownKeys)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { FarionAssetPaths.PrefabRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                foreach (UiLocalizedText text in prefab.GetComponentsInChildren<UiLocalizedText>(true))
                {
                    if (string.IsNullOrWhiteSpace(text.EntryKey))
                    {
                        report.AddError(
                            $"{path} has a {nameof(UiLocalizedText)} without a localization key.");
                        continue;
                    }

                    if (!knownKeys.Contains(text.EntryKey))
                    {
                        report.AddError(
                            $"{path} references unknown localization key '{text.EntryKey}'.");
                    }
                }
            }
        }

        static void ValidateFoundationPrefabs(FarionValidationReport report)
        {
            GameObject systemRootPrefab =
                LoadRequiredPrefab(SystemRootPrefabPath, report);
            GameObject confirmationPrefab =
                LoadRequiredPrefab(ConfirmationPrefabPath, report);
            GameObject loadingPrefab =
                LoadRequiredPrefab(LoadingPrefabPath, report);
            GameObject feedbackPrefab =
                LoadRequiredPrefab(FeedbackPrefabPath, report);
            GameObject settingsPrefab =
                LoadRequiredPrefab(SettingsPrefabPath, report);
            GameObject saveSlotPrefab =
                LoadRequiredPrefab(SaveSlotPrefabPath, report);
            GameObject saveLoadPrefab =
                LoadRequiredPrefab(SaveLoadPrefabPath, report);
            GameObject inventoryPrefab =
                LoadRequiredPrefab(InventoryPrefabPath, report);
            GameObject pausePrefab =
                LoadRequiredPrefab(PausePrefabPath, report);
            GameObject flightHudPrefab =
                LoadRequiredPrefab(FlightHudPrefabPath, report);
            ValidateSystemRootPrefab(systemRootPrefab, report);
            ValidateConfirmationPrefab(confirmationPrefab, report);
            ValidateLoadingPrefab(loadingPrefab, report);
            ValidateFeedbackPrefab(feedbackPrefab, report);
            ValidateSettingsPrefab(settingsPrefab, report);
            ValidateSaveLoadPrefabs(saveSlotPrefab, saveLoadPrefab, report);
            ValidateFixedScreenBounds(inventoryPrefab, InventoryPrefabPath, report);
            ValidateFixedScreenBounds(pausePrefab, PausePrefabPath, report);
            if (flightHudPrefab != null)
            {
                ValidateRequiredComponentInChildren<UiSafeAreaFitter>(
                    flightHudPrefab,
                    FlightHudPrefabPath,
                    report);
                ValidateFlightHudGraphics(flightHudPrefab, report);
            }
            ValidateVisualContracts(systemRootPrefab, report);
        }

        static void ValidateFlightHudGraphics(
            GameObject prefab,
            FarionValidationReport report)
        {
            UiSpacecraftFlightHudGraphics graphics =
                prefab.GetComponentInChildren<UiSpacecraftFlightHudGraphics>(true);
            if (graphics == null)
            {
                report.AddError(
                    $"{FlightHudPrefabPath}: missing {nameof(UiSpacecraftFlightHudGraphics)}.");
                return;
            }

            SerializedObject serialized = new(graphics);
            foreach (string field in FlightHudGraphicsBindings)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null || property.objectReferenceValue == null)
                {
                    report.AddError(
                        $"{FlightHudPrefabPath}: {nameof(UiSpacecraftFlightHudGraphics)}.{field} is unassigned.");
                }
            }
        }

        static GameObject LoadRequiredPrefab(
            string path,
            FarionValidationReport report)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                report.AddError($"Required UI prefab is missing: {path}");
                return null;
            }

            ValidateMissingScripts(prefab, path, report);
            return prefab;
        }

        static void ValidateSystemRootPrefab(
            GameObject prefab,
            FarionValidationReport report)
        {
            if (prefab == null)
            {
                return;
            }

            ValidateRequiredComponent<UiSystemRoot>(
                prefab,
                SystemRootPrefabPath,
                report);
            ValidateRequiredComponent<UiScreenRouter>(
                prefab,
                SystemRootPrefabPath,
                report);
            ValidateRequiredComponent<UiFocusController>(
                prefab,
                SystemRootPrefabPath,
                report);
            ValidateRequiredComponent<UiInputDeviceService>(
                prefab,
                SystemRootPrefabPath,
                report);
            ValidateRequiredComponent<UiPreferencesService>(
                prefab,
                SystemRootPrefabPath,
                report);

            UiSystemRoot systemRoot = prefab.GetComponent<UiSystemRoot>();
            if (systemRoot != null && systemRoot.Theme == null)
            {
                report.AddError(
                    $"{SystemRootPrefabPath}: {nameof(UiSystemRoot)} has no theme.");
            }

            bool containsSystemView =
                prefab.GetComponentInChildren<UiScreenView>(true) != null ||
                prefab.GetComponentInChildren<UiFeedbackService>(true) != null ||
                prefab.GetComponentInChildren<UiConfirmationDialog>(true) != null;
            if (containsSystemView || prefab.transform.childCount > 0)
            {
                report.AddError(
                    $"{SystemRootPrefabPath}: UI_SystemRoot must remain service-only. " +
                    "Confirmation, loading, and feedback prefabs are Canvas siblings.");
            }
        }

        static void ValidateConfirmationPrefab(
            GameObject prefab,
            FarionValidationReport report)
        {
            if (prefab == null)
            {
                return;
            }

            ValidateRequiredComponentInChildren<UiConfirmationDialog>(
                prefab,
                ConfirmationPrefabPath,
                report);
            ValidateScreenPrefab(
                prefab,
                ConfirmationPrefabPath,
                UiScreenId.Confirmation,
                UiScreenLayer.Modal,
                requiresInitialSelection: true,
                report);
        }

        static void ValidateLoadingPrefab(
            GameObject prefab,
            FarionValidationReport report)
        {
            if (prefab == null)
            {
                return;
            }

            ValidateScreenPrefab(
                prefab,
                LoadingPrefabPath,
                UiScreenId.Loading,
                UiScreenLayer.System,
                requiresInitialSelection: false,
                report);
            ValidateRequiredComponent<UiPanelFader>(
                prefab,
                LoadingPrefabPath,
                report);
            ValidateRequiredComponent<UiLoadingOverlayPresenter>(
                prefab,
                LoadingPrefabPath,
                report);
            ValidateRequiredComponentInChildren<UiSafeAreaFitter>(
                prefab,
                LoadingPrefabPath,
                report);

            UiPanelFader fader = prefab.GetComponent<UiPanelFader>();
            if (fader != null && !fader.FadeOnly)
            {
                report.AddError(
                    $"{LoadingPrefabPath}: system loading must use a fade-only " +
                    "transition without positional motion.");
            }

            UiLoadingOverlayPresenter presenter =
                prefab.GetComponent<UiLoadingOverlayPresenter>();
            if (presenter != null && !presenter.HasCompletePresentation)
            {
                report.AddError(
                    $"{LoadingPrefabPath}: loading presenter requires system, " +
                    "title, status, progress value, and progress fill references.");
            }
        }

        static void ValidateFeedbackPrefab(
            GameObject prefab,
            FarionValidationReport report)
        {
            if (prefab == null)
            {
                return;
            }

            UiFeedbackService feedback =
                prefab.GetComponentInChildren<UiFeedbackService>(true);
            if (feedback == null)
            {
                report.AddError(
                    $"{FeedbackPrefabPath}: missing {nameof(UiFeedbackService)}.");
                return;
            }

            if (!feedback.IncludesSeverityLabel)
            {
                report.AddError(
                    $"{FeedbackPrefabPath}: severity labels must be enabled so " +
                    "feedback does not depend on color alone.");
            }

            if (prefab.GetComponentInChildren<UiScreenView>(true) != null)
            {
                report.AddError(
                    $"{FeedbackPrefabPath}: transient feedback must not enter " +
                    "the navigation stack.");
            }
        }

        static void ValidateSettingsPrefab(
            GameObject prefab,
            FarionValidationReport report)
        {
            if (prefab == null)
            {
                return;
            }

            ValidateRequiredComponentInChildren<UiSettingsScreenPresenter>(
                prefab,
                SettingsPrefabPath,
                report);
            ValidateRequiredComponentInChildren<UiSafeAreaFitter>(
                prefab,
                SettingsPrefabPath,
                report);
            ValidateRequiredComponent<UiPanelFader>(
                prefab,
                SettingsPrefabPath,
                report);

            UiSettingsCategoryView[] categories =
                prefab.GetComponentsInChildren<UiSettingsCategoryView>(true);
            ValidateSettingsCategories(categories, report);

            UiSettingsOptionView[] options =
                prefab.GetComponentsInChildren<UiSettingsOptionView>(true);
            ValidateSettingsOptions(options, report);

            UiSettingsScreenPresenter settingsPresenter =
                prefab.GetComponentInChildren<UiSettingsScreenPresenter>(true);
            if (settingsPresenter != null &&
                !settingsPresenter.HasCompleteDisplayControls)
            {
                report.AddError(
                    $"{SettingsPrefabPath}: display mode, resolution, and " +
                    "refresh rate dropdowns must all be authored.");
            }

            ValidateRequiredComponentInChildren<UiSettingsActionView>(
                prefab,
                SettingsPrefabPath,
                report);

            ValidateScreenPrefab(
                prefab,
                SettingsPrefabPath,
                UiScreenId.Settings,
                UiScreenLayer.Screen,
                requiresInitialSelection: true,
                report);
        }

        static void ValidateSaveLoadPrefabs(
            GameObject slotPrefab,
            GameObject screenPrefab,
            FarionValidationReport report)
        {
            if (slotPrefab != null)
            {
                ValidateRequiredComponent<UiSaveSlotView>(
                    slotPrefab,
                    SaveSlotPrefabPath,
                    report);
            }

            if (screenPrefab == null)
            {
                return;
            }

            ValidateRequiredComponent<UiSaveLoadScreenPresenter>(
                screenPrefab,
                SaveLoadPrefabPath,
                report);
            ValidateRequiredComponentInChildren<UiSafeAreaFitter>(
                screenPrefab,
                SaveLoadPrefabPath,
                report);
            ValidateRequiredComponent<UiPanelFader>(
                screenPrefab,
                SaveLoadPrefabPath,
                report);

            UiSaveLoadScreenPresenter presenter =
                screenPrefab.GetComponent<UiSaveLoadScreenPresenter>();
            if (presenter != null && !presenter.HasCompletePresentation)
            {
                report.AddError(
                    $"{SaveLoadPrefabPath}: presenter requires four authored " +
                    "slots, detail content, and all three actions.");
            }

            UiSaveSlotView[] slots =
                screenPrefab.GetComponentsInChildren<UiSaveSlotView>(true);
            if (slots.Length != 4)
            {
                report.AddError(
                    $"{SaveLoadPrefabPath}: expected exactly 4 authored save " +
                    $"slots, found {slots.Length}.");
            }

            ValidateScreenPrefab(
                screenPrefab,
                SaveLoadPrefabPath,
                UiScreenId.SaveLoad,
                UiScreenLayer.Screen,
                requiresInitialSelection: true,
                report);
        }

        static void ValidateSettingsCategories(
            UiSettingsCategoryView[] categories,
            FarionValidationReport report)
        {
            HashSet<UiSettingsCategory> authored = new();
            for (int i = 0; i < categories.Length; i++)
            {
                UiSettingsCategory category = categories[i].Category;
                if (!Enum.IsDefined(typeof(UiSettingsCategory), category))
                {
                    report.AddError(
                        $"{SettingsPrefabPath}: category at index {i} has " +
                        $"unknown identity '{(int)category}'.");
                    continue;
                }

                if (!authored.Add(category))
                {
                    report.AddError(
                        $"{SettingsPrefabPath}: duplicate settings category " +
                        $"'{category}'.");
                }
            }

            foreach (UiSettingsCategory required in
                     Enum.GetValues(typeof(UiSettingsCategory)))
            {
                if (!authored.Contains(required))
                {
                    report.AddError(
                        $"{SettingsPrefabPath}: missing settings category " +
                        $"'{required}'.");
                }
            }
        }

        static readonly UiSettingId[] DropdownBackedSettings =
        {
            UiSettingId.DisplayMode,
            UiSettingId.Resolution,
            UiSettingId.RefreshRate
        };

        static void ValidateSettingsOptions(
            UiSettingsOptionView[] options,
            FarionValidationReport report)
        {
            HashSet<UiSettingId> authored = new();
            for (int i = 0; i < options.Length; i++)
            {
                UiSettingId setting = options[i].SettingId;
                if (!Enum.IsDefined(typeof(UiSettingId), setting))
                {
                    report.AddError(
                        $"{SettingsPrefabPath}: option at index {i} has " +
                        $"unknown identity '{(int)setting}'.");
                    continue;
                }

                if (!authored.Add(setting))
                {
                    report.AddError(
                        $"{SettingsPrefabPath}: duplicate settings option " +
                        $"'{setting}'.");
                }
            }

            foreach (UiSettingId required in Enum.GetValues(typeof(UiSettingId)))
            {
                if (Array.IndexOf(DropdownBackedSettings, required) >= 0)
                {
                    continue;
                }

                if (!authored.Contains(required))
                {
                    report.AddError(
                        $"{SettingsPrefabPath}: missing settings option " +
                        $"'{required}'.");
                }
            }
        }

        static void ValidateFixedScreenBounds(
            GameObject prefab,
            string path,
            FarionValidationReport report)
        {
            if (prefab == null)
            {
                return;
            }

            RectTransform rect = prefab.GetComponent<RectTransform>();
            if (rect == null || rect.anchorMin != rect.anchorMax)
            {
                report.AddError($"{path}: fixed screen root requires fixed anchors.");
                return;
            }

            const float padding = 32f;
            Vector2 size = rect.rect.size;
            Vector2 pivotPosition = Vector2.Scale(
                RequiredReferenceResolution,
                rect.anchorMin) + rect.anchoredPosition;
            Vector2 min = pivotPosition - Vector2.Scale(size, rect.pivot);
            Vector2 max = min + size;
            if (min.x < padding || min.y < padding ||
                max.x > RequiredReferenceResolution.x - padding ||
                max.y > RequiredReferenceResolution.y - padding)
            {
                report.AddError(
                    $"{path}: authored root must remain inside the 32 px " +
                    "reference safe margin.");
            }
        }

        static void ValidateVisualContracts(
            GameObject systemRootPrefab,
            FarionValidationReport report)
        {
            Sprite expectedFrame = AssetDatabase.LoadAssetAtPath<Sprite>(
                MenuButtonFramePath);
            string[] prefabGuids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { UiPrefabFolder });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                ValidateMissingScripts(prefab, path, report);
                Transform[] hierarchy =
                    prefab.GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < hierarchy.Length; j++)
                {
                    if (!string.Equals(
                            hierarchy[j].name,
                            "SelectionFrame",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Image image = hierarchy[j].GetComponent<Image>();
                    if (image == null || image.sprite != expectedFrame)
                    {
                        report.AddError(
                            $"{path}: {GetHierarchyPath(hierarchy[j])} must use " +
                            $"{MenuButtonFramePath}.");
                    }
                }

                ValidateLegacyFonts(
                    prefab.GetComponentsInChildren<TMP_Text>(true),
                    path,
                    report);
            }

            UiTheme theme = systemRootPrefab != null
                ? systemRootPrefab.GetComponent<UiSystemRoot>()?.Theme
                : null;
            if (theme == null)
            {
                return;
            }

            Color panel = Composite(theme.PanelSurface, theme.VoidSurface);
            Color button = Composite(theme.ButtonSurface, panel);
            Color highlighted = Composite(theme.ButtonSurfaceHighlighted, panel);
            ValidateSurfaceStep(theme.VoidSurface, theme.PanelSurface, "Void/Panel", report);
            ValidateSurfaceStep(theme.PanelSurface, theme.RaisedSurface, "Panel/Raised", report);
            ValidateSurfaceStep(theme.RaisedSurface, theme.ButtonSurface, "Raised/Interactive", report);
            ValidateSurfaceStep(
                theme.ButtonSurface,
                theme.ButtonSurfaceHighlighted,
                "Interactive/Highlighted",
                report);
            ValidateContrast(theme.PrimaryText, button, "PrimaryText/ButtonSurface", report);
            ValidateContrast(theme.SecondaryText, button, "SecondaryText/ButtonSurface", report);
            ValidateContrast(
                theme.PrimaryText,
                highlighted,
                "PrimaryText/ButtonSurfaceHighlighted",
                report);
            ValidateContrast(
                theme.SupportingText,
                highlighted,
                "SupportingText/ButtonSurfaceHighlighted",
                report);
        }

        static void ValidateContrast(
            Color foreground,
            Color background,
            string role,
            FarionValidationReport report)
        {
            Color composed = Composite(foreground, background);
            float ratio = ContrastRatio(composed, background);
            if (ratio < MinimumTextContrast)
            {
                report.AddError(
                    $"UI theme contrast {role} is {ratio:0.00}:1; " +
                    $"minimum is {MinimumTextContrast:0.0}:1.");
            }
        }

        static void ValidateSurfaceStep(
            Color lower,
            Color higher,
            string role,
            FarionValidationReport report)
        {
            if (lower.a < 0.999f || higher.a < 0.999f)
            {
                report.AddError($"UI theme surface step {role} must be opaque.");
                return;
            }

            float lowerLuminance = RelativeLuminance(lower);
            float ratio = RelativeLuminance(higher) / Mathf.Max(0.0001f, lowerLuminance);
            if (ratio < MinimumSurfaceLuminanceStep)
            {
                report.AddError(
                    $"UI theme surface step {role} is {ratio:0.00}x; " +
                    $"minimum is {MinimumSurfaceLuminanceStep:0.0}x.");
            }
        }

        static void ValidateLegacyFonts(
            TMP_Text[] textComponents,
            string scope,
            FarionValidationReport report)
        {
            for (int i = 0; i < textComponents.Length; i++)
            {
                TMP_Text text = textComponents[i];
                if (text.font != null &&
                    string.Equals(
                        AssetDatabase.GetAssetPath(text.font),
                        LegacyFontPath,
                        StringComparison.Ordinal))
                {
                    report.AddError(
                        $"{scope}: {GetHierarchyPath(text.transform)} still uses " +
                        "the legacy Liberation Sans font.");
                }
            }
        }

        static Color Composite(Color foreground, Color background)
        {
            float alpha = foreground.a + background.a * (1f - foreground.a);
            if (alpha <= 0f)
            {
                return Color.clear;
            }

            return new Color(
                (foreground.r * foreground.a +
                 background.r * background.a * (1f - foreground.a)) / alpha,
                (foreground.g * foreground.a +
                 background.g * background.a * (1f - foreground.a)) / alpha,
                (foreground.b * foreground.a +
                 background.b * background.a * (1f - foreground.a)) / alpha,
                alpha);
        }

        static float ContrastRatio(Color first, Color second)
        {
            float firstLuminance = RelativeLuminance(first);
            float secondLuminance = RelativeLuminance(second);
            float lighter = Mathf.Max(firstLuminance, secondLuminance);
            float darker = Mathf.Min(firstLuminance, secondLuminance);
            return (lighter + 0.05f) / (darker + 0.05f);
        }

        static float RelativeLuminance(Color color)
        {
            return 0.2126f * ToLinear(color.r) +
                   0.7152f * ToLinear(color.g) +
                   0.0722f * ToLinear(color.b);
        }

        static float ToLinear(float channel)
        {
            return channel <= 0.04045f
                ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }

        static void ValidateScreenPrefab(
            GameObject prefab,
            string path,
            UiScreenId expectedId,
            UiScreenLayer expectedLayer,
            bool requiresInitialSelection,
            FarionValidationReport report)
        {
            UiScreenView[] views =
                prefab.GetComponentsInChildren<UiScreenView>(true);
            if (views.Length != 1)
            {
                report.AddError(
                    $"{path}: expected exactly one {nameof(UiScreenView)}, " +
                    $"found {views.Length}.");
                return;
            }

            UiScreenView view = views[0];
            if (view.ScreenId != expectedId)
            {
                report.AddError(
                    $"{path}: screen id must be {expectedId}, found {view.ScreenId}.");
            }

            if (view.Layer != expectedLayer)
            {
                report.AddError(
                    $"{path}: screen layer must be {expectedLayer}, found {view.Layer}.");
            }

            if (!view.LocksGameplayInput)
            {
                report.AddError($"{path}: blocking system views must lock gameplay input.");
            }

            if (view.VisibleOnAwake)
            {
                report.AddError($"{path}: system views must start hidden.");
            }

            if (!view.BlocksRaycastsWhenVisible)
            {
                report.AddError($"{path}: blocking system views must block raycasts.");
            }

            if (requiresInitialSelection && !view.HasExplicitFirstSelection)
            {
                report.AddError($"{path}: confirmation requires an initial selection.");
            }
        }

        static void ValidateBuildScenes(FarionValidationReport report)
        {
            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                if (!buildScene.enabled ||
                    string.IsNullOrWhiteSpace(buildScene.path) ||
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(buildScene.path) == null)
                {
                    continue;
                }

                ValidateScene(buildScene.path, report);
            }
        }

        static void ValidateScene(
            string path,
            FarionValidationReport report)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            try
            {
                UiSystemRoot[] systemRoots =
                    GetSceneComponents<UiSystemRoot>(scene);
                UiScreenView[] sceneScreens =
                    GetSceneComponents<UiScreenView>(scene);
                if (systemRoots.Length == 0 && sceneScreens.Length == 0)
                {
                    return;
                }

                ValidateLegacyFonts(
                    GetSceneComponents<TMP_Text>(scene),
                    path,
                    report);

                if (systemRoots.Length == 0)
                {
                    report.AddError(
                        $"{path}: interactive UI exists without a {nameof(UiSystemRoot)}.");
                    return;
                }

                ValidateEventSystem(scene, path, report);
                for (int i = 0; i < systemRoots.Length; i++)
                {
                    ValidateComposition(systemRoots[i], path, report);
                }
            }
            finally
            {
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }

        static void ValidateComposition(
            UiSystemRoot systemRoot,
            string scenePath,
            FarionValidationReport report)
        {
            Canvas canvas = systemRoot.OwningCanvas;
            if (canvas == null)
            {
                report.AddError(
                    $"{scenePath}: {GetHierarchyPath(systemRoot.transform)} " +
                    "must be placed below a Canvas.");
                return;
            }

            string scope = $"{scenePath}: {GetHierarchyPath(canvas.transform)}";
            if (systemRoot.transform.parent != canvas.transform)
            {
                report.AddError(
                    $"{scope}: UI_SystemRoot must be a direct Canvas child.");
            }

            UiSystemRoot[] roots =
                UiCompositionScope.FindAllInScope<UiSystemRoot>(systemRoot);
            if (roots.Length != 1)
            {
                report.AddError(
                    $"{scope}: expected one UI_SystemRoot, found {roots.Length}.");
            }

            ValidateCanvasScaler(canvas, scope, report);
            ValidatePrefabInstance(
                systemRoot,
                SystemRootPrefabPath,
                scope,
                report);

            UiFeedbackService[] feedbackServices =
                UiCompositionScope.FindAllInScope<UiFeedbackService>(systemRoot);
            UiConfirmationDialog[] confirmationDialogs =
                UiCompositionScope.FindAllInScope<UiConfirmationDialog>(systemRoot);
            UiScreenView[] screens =
                UiCompositionScope.FindAllInScope<UiScreenView>(systemRoot);

            ValidateSingleSibling(
                feedbackServices,
                systemRoot,
                canvas,
                FeedbackPrefabPath,
                scope,
                report);
            ValidateSingleSibling(
                confirmationDialogs,
                systemRoot,
                canvas,
                ConfirmationPrefabPath,
                scope,
                report);

            UiScreenView loading = FindScreen(screens, UiScreenId.Loading);
            if (loading == null)
            {
                report.AddError($"{scope}: missing Loading system screen.");
            }
            else
            {
                ValidateSibling(
                    loading,
                    systemRoot,
                    canvas,
                    LoadingPrefabPath,
                    scope,
                    report);
            }

            UiScreenView settings = FindScreen(screens, UiScreenId.Settings);
            if (settings == null)
            {
                report.AddError($"{scope}: missing Settings screen.");
            }
            else
            {
                ValidateSibling(
                    settings,
                    systemRoot,
                    canvas,
                    SettingsPrefabPath,
                    scope,
                    report);
            }

            UiScreenView saveLoad = FindScreen(screens, UiScreenId.SaveLoad);
            bool requiresSaveLoad = scenePath != MultiplayerPresentationScenePath;
            if (requiresSaveLoad && saveLoad == null)
            {
                report.AddError($"{scope}: missing Save/Load screen.");
            }
            else if (saveLoad != null)
            {
                ValidateSibling(
                    saveLoad,
                    systemRoot,
                    canvas,
                    SaveLoadPrefabPath,
                    scope,
                    report);
            }

            ValidateScreenRegistry(screens, scope, report);
        }

        static void ValidateCanvasScaler(
            Canvas canvas,
            string scope,
            FarionValidationReport report)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                report.AddError($"{scope}: Canvas requires a CanvasScaler.");
                return;
            }

            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                report.AddError(
                    $"{scope}: CanvasScaler must use Scale With Screen Size.");
            }

            if ((scaler.referenceResolution - RequiredReferenceResolution)
                .sqrMagnitude > 0.01f)
            {
                report.AddError(
                    $"{scope}: reference resolution must be 1920 x 1080.");
            }

            if (!Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f))
            {
                report.AddError(
                    $"{scope}: CanvasScaler Match must be 0.5.");
            }
        }

        static void ValidateEventSystem(
            Scene scene,
            string scenePath,
            FarionValidationReport report)
        {
            EventSystem[] eventSystems = GetSceneComponents<EventSystem>(scene);
            if (eventSystems.Length != 1)
            {
                report.AddError(
                    $"{scenePath}: expected one EventSystem, found {eventSystems.Length}.");
                return;
            }

            EventSystem eventSystem = eventSystems[0];
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                report.AddError(
                    $"{scenePath}: EventSystem requires InputSystemUIInputModule.");
            }

            if (eventSystem.GetComponent<UiInputModuleBinder>() == null)
            {
                report.AddError(
                    $"{scenePath}: EventSystem requires UiInputModuleBinder.");
            }
        }

        static void ValidateScreenRegistry(
            UiScreenView[] screens,
            string scope,
            FarionValidationReport report)
        {
            Dictionary<UiScreenId, UiScreenView> ids = new();
            for (int i = 0; i < screens.Length; i++)
            {
                UiScreenView screen = screens[i];
                if (screen.ScreenId == UiScreenId.None)
                {
                    report.AddError(
                        $"{scope}: {GetHierarchyPath(screen.transform)} has screen id None.");
                    continue;
                }

                if (ids.TryGetValue(screen.ScreenId, out UiScreenView duplicate))
                {
                    report.AddError(
                        $"{scope}: duplicate screen id {screen.ScreenId} on " +
                        $"{GetHierarchyPath(duplicate.transform)} and " +
                        $"{GetHierarchyPath(screen.transform)}.");
                    continue;
                }

                ids.Add(screen.ScreenId, screen);
                ValidateInitialSelection(screen, scope, report);
                if (screen.Layer == UiScreenLayer.Hud &&
                    screen.LocksGameplayInput)
                {
                    report.AddError(
                        $"{scope}: HUD screen {screen.name} must not lock gameplay input.");
                }
            }
        }

        static void ValidateInitialSelection(
            UiScreenView screen,
            string scope,
            FarionValidationReport report)
        {
            if (screen.Layer == UiScreenLayer.Hud ||
                screen.Layer == UiScreenLayer.System ||
                screen.HasExplicitFirstSelection)
            {
                return;
            }

            Selectable[] authoredSelections =
                screen.GetComponentsInChildren<Selectable>(true);
            bool hasRuntimeMenuProvider =
                screen.GetComponentInChildren<UiGameplayMenuListPresenter>(true) != null;
            if (authoredSelections.Length > 0 && !hasRuntimeMenuProvider)
            {
                report.AddError(
                    $"{scope}: {GetHierarchyPath(screen.transform)} contains " +
                    "selectable controls but has no explicit first selection.");
            }
        }

        static UiScreenView FindScreen(
            UiScreenView[] screens,
            UiScreenId screenId)
        {
            for (int i = 0; i < screens.Length; i++)
            {
                if (screens[i].ScreenId == screenId)
                {
                    return screens[i];
                }
            }

            return null;
        }

        static void ValidateSingleSibling<T>(
            T[] services,
            UiSystemRoot systemRoot,
            Canvas canvas,
            string expectedPrefabPath,
            string scope,
            FarionValidationReport report)
            where T : Component
        {
            if (services.Length != 1)
            {
                report.AddError(
                    $"{scope}: expected one {typeof(T).Name}, found {services.Length}.");
                return;
            }

            ValidateSibling(
                services[0],
                systemRoot,
                canvas,
                expectedPrefabPath,
                scope,
                report);
        }

        static void ValidateSibling(
            Component component,
            UiSystemRoot systemRoot,
            Canvas canvas,
            string expectedPrefabPath,
            string scope,
            FarionValidationReport report)
        {
            if (component.transform.IsChildOf(systemRoot.transform))
            {
                report.AddError(
                    $"{scope}: {component.name} must be a Canvas sibling, " +
                    "not a child of UI_SystemRoot.");
            }

            Transform instanceRoot =
                PrefabUtility.GetNearestPrefabInstanceRoot(component.gameObject)
                    ?.transform;
            if (instanceRoot == null || instanceRoot.parent != canvas.transform)
            {
                report.AddError(
                    $"{scope}: {component.name} prefab root must be a direct Canvas child.");
            }

            ValidatePrefabInstance(
                component,
                expectedPrefabPath,
                scope,
                report);
        }

        static void ValidatePrefabInstance(
            Component component,
            string expectedPath,
            string scope,
            FarionValidationReport report)
        {
            string actualPath =
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    component.gameObject);
            if (!string.Equals(actualPath, expectedPath, StringComparison.Ordinal))
            {
                report.AddError(
                    $"{scope}: {component.name} must be an instance of " +
                    $"{expectedPath}, found '{actualPath}'.");
            }
        }

        static void ValidateRequiredComponent<T>(
            GameObject prefab,
            string path,
            FarionValidationReport report)
            where T : Component
        {
            if (prefab.GetComponent<T>() == null)
            {
                report.AddError($"{path}: root is missing {typeof(T).Name}.");
            }
        }

        static void ValidateRequiredComponentInChildren<T>(
            GameObject prefab,
            string path,
            FarionValidationReport report)
            where T : Component
        {
            if (prefab.GetComponentInChildren<T>(true) == null)
            {
                report.AddError($"{path}: missing {typeof(T).Name}.");
            }
        }

        static void ValidateMissingScripts(
            GameObject prefab,
            string path,
            FarionValidationReport report)
        {
            Transform[] hierarchy = prefab.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < hierarchy.Length; i++)
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        hierarchy[i].gameObject) > 0)
                {
                    report.AddError(
                        $"{path}: {GetHierarchyPath(hierarchy[i])} has a missing script.");
                }
            }
        }

        static T[] GetSceneComponents<T>(Scene scene)
            where T : Component
        {
            List<T> components = new();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                components.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return components.ToArray();
        }

        static string GetHierarchyPath(Transform target)
        {
            string path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = $"{target.name}/{path}";
            }

            return path;
        }
    }
}
