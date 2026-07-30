using System;
using System.Collections.Generic;
using Farion.UI.Common;
using Farion.UI.Feedback;
using Farion.UI.Foundation;
using Farion.UI.Gameplay;
using Farion.UI.Input;
using Farion.UI.Loading;
using Farion.UI.Navigation;
using Farion.UI.SaveLoad;
using Farion.UI.Settings;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Farion.Editor.Validation
{
    public static class FarionUiProjectValidator
    {
        public const string SystemRootPrefabPath =
            "Assets/Project/Prefabs/UI/Foundation/UI_SystemRoot.prefab";
        public const string ConfirmationPrefabPath =
            "Assets/Project/Prefabs/UI/Foundation/UI_ConfirmationDialog.prefab";
        public const string LoadingPrefabPath =
            "Assets/Project/Prefabs/UI/Foundation/UI_LoadingOverlay.prefab";
        public const string FeedbackPrefabPath =
            "Assets/Project/Prefabs/UI/Foundation/UI_FeedbackOverlay.prefab";
        public const string SettingsPrefabPath =
            "Assets/Project/Prefabs/UI/Screens/UI_SettingsScreen.prefab";
        public const string SaveSlotPrefabPath =
            "Assets/Project/Prefabs/UI/Common/UI_SaveSlot.prefab";
        public const string SaveLoadPrefabPath =
            "Assets/Project/Prefabs/UI/Screens/UI_SaveLoadScreen.prefab";
        static readonly Vector2 RequiredReferenceResolution = new(1920f, 1080f);

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
            ValidateSystemRootPrefab(systemRootPrefab, report);
            ValidateConfirmationPrefab(confirmationPrefab, report);
            ValidateLoadingPrefab(loadingPrefab, report);
            ValidateFeedbackPrefab(feedbackPrefab, report);
            ValidateSettingsPrefab(settingsPrefab, report);
            ValidateSaveLoadPrefabs(saveSlotPrefab, saveLoadPrefab, report);
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
            ValidateRequiredComponent<FarionPanelFader>(
                prefab,
                LoadingPrefabPath,
                report);
            ValidateRequiredComponent<UiLoadingOverlayPresenter>(
                prefab,
                LoadingPrefabPath,
                report);

            FarionPanelFader fader = prefab.GetComponent<FarionPanelFader>();
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
            ValidateRequiredComponent<FarionPanelFader>(
                prefab,
                SettingsPrefabPath,
                report);

            UiSettingsCategoryView[] categories =
                prefab.GetComponentsInChildren<UiSettingsCategoryView>(true);
            if (categories.Length != 2)
            {
                report.AddError(
                    $"{SettingsPrefabPath}: expected exactly 2 settings " +
                    $"categories, found {categories.Length}.");
            }

            UiSettingsOptionView[] options =
                prefab.GetComponentsInChildren<UiSettingsOptionView>(true);
            if (options.Length != 5)
            {
                report.AddError(
                    $"{SettingsPrefabPath}: expected exactly 5 authored settings " +
                    $"options, found {options.Length}.");
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
            ValidateRequiredComponent<FarionPanelFader>(
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
            if (saveLoad == null)
            {
                report.AddError($"{scope}: missing Save/Load screen.");
            }
            else
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
                screen.GetComponentInChildren<GameplayMenuListPresenter>(true) != null;
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
