using System;
using System.Collections;
using System.Collections.Generic;
using Farion.Gameplay.Input;
using Farion.UI.Foundation;
using Farion.UI.Localization;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Farion.UI.Settings
{
    [DefaultExecutionOrder(-450)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UiSystemRoot))]
    public sealed class UiPreferencesService : MonoBehaviour
    {
        const string LocaleKey = "farion.ui.locale";
        const string ScaleKey = "farion.ui.scale";
        const string ReducedMotionKey = "farion.ui.reducedMotion";
        const string SubtitlesKey = "farion.ui.subtitles";
        const string SubtitleSizeKey = "farion.ui.subtitleSize";
        const string DisplayModeKey = "farion.display.mode";
        const string ResolutionWidthKey = "farion.display.resolutionWidth";
        const string ResolutionHeightKey = "farion.display.resolutionHeight";
        const string VSyncKey = "farion.display.vSync";
        const string RefreshNumeratorKey = "farion.display.refreshNumerator";
        const string RefreshDenominatorKey = "farion.display.refreshDenominator";

        const float MouseSensitivityStep = 0.1f;
        const float FieldOfViewStep = 5f;

        static readonly float[] SupportedScales = { 1f, 1.1f, 1.25f };

        static readonly FullScreenMode[] SupportedDisplayModes =
        {
            FullScreenMode.ExclusiveFullScreen,
            FullScreenMode.FullScreenWindow,
            FullScreenMode.Windowed
        };

        [Header("Composition")]
        [SerializeField] UiSystemRoot systemRoot;
        [SerializeField] CanvasScaler canvasScaler;

        [Header("Scale")]
        [SerializeField] Vector2 baseReferenceResolution = new(1920f, 1080f);

        string localeCode = UiLocalization.EnglishLocaleCode;
        int scaleIndex;
        bool reducedMotion;
        bool subtitlesEnabled = true;
        UiSubtitleSize subtitleSize = UiSubtitleSize.Standard;
        readonly List<Vector2Int> supportedResolutions = new();
        readonly List<RefreshRate> supportedRefreshRates = new();
        int resolutionIndex;
        int appliedResolutionIndex;
        int refreshRateIndex;
        RefreshRate appliedRefreshRate;
        int confirmedResolutionIndex;
        RefreshRate confirmedRefreshRate;
        FullScreenMode displayMode = FullScreenMode.FullScreenWindow;
        FullScreenMode appliedDisplayMode = FullScreenMode.FullScreenWindow;
        FullScreenMode confirmedDisplayMode = FullScreenMode.FullScreenWindow;
        bool awaitingDisplayConfirmation;
        bool vSyncEnabled = true;
        Coroutine localeRoutine;

        public event Action Changed;

        public string LocaleCode => localeCode;
        public float UiScale => SupportedScales[Mathf.Clamp(scaleIndex, 0, SupportedScales.Length - 1)];
        public bool ReducedMotion => reducedMotion;
        public bool SubtitlesEnabled => subtitlesEnabled;
        public UiSubtitleSize SubtitleSize => subtitleSize;
        public FullScreenMode DisplayMode => displayMode;
        public IReadOnlyList<FullScreenMode> DisplayModes => SupportedDisplayModes;
        public int DisplayModeIndex => FindDisplayModeIndex(displayMode);
        public IReadOnlyList<Vector2Int> Resolutions => supportedResolutions;
        public int ResolutionIndex =>
            Mathf.Clamp(resolutionIndex, 0, supportedResolutions.Count - 1);
        public Vector2Int Resolution => supportedResolutions[ResolutionIndex];
        public IReadOnlyList<RefreshRate> RefreshRates => supportedRefreshRates;
        public int RefreshRateIndex =>
            Mathf.Clamp(refreshRateIndex, 0, Mathf.Max(0, supportedRefreshRates.Count - 1));
        public RefreshRate RefreshRate => supportedRefreshRates.Count > 0
            ? supportedRefreshRates[RefreshRateIndex]
            : Screen.currentResolution.refreshRateRatio;
        public bool VSyncEnabled => vSyncEnabled;
        public float MouseSensitivity => FarionInputActions.MouseSensitivityScale;
        public bool InvertLookY => FarionInputActions.InvertLookY;
        public float FieldOfView => FarionViewPreferences.FieldOfView;
        public bool HasSelectableResolutions => supportedResolutions.Count > 1;
        public bool SupportsRefreshRateSelection =>
            displayMode == FullScreenMode.ExclusiveFullScreen &&
            supportedRefreshRates.Count > 1;
        public bool HasPendingDisplayMode => displayMode != appliedDisplayMode;
        public bool HasPendingResolution => resolutionIndex != appliedResolutionIndex;
        public bool HasPendingRefreshRate =>
            !MatchesRefreshRate(RefreshRate, appliedRefreshRate);
        public bool HasPendingDisplayChanges =>
            HasPendingDisplayMode || HasPendingResolution || HasPendingRefreshRate;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
            BuildSupportedResolutions();
            Load();
            ApplyVisualPreferences();
            ApplySavedDisplayPreferences();
            ApplyLocale();
        }

        void OnDisable()
        {
            if (localeRoutine != null)
            {
                StopCoroutine(localeRoutine);
                localeRoutine = null;
            }
        }

        public void CycleLocale(int direction)
        {
            SetLocaleCode(localeCode == UiLocalization.EnglishLocaleCode
                ? UiLocalization.TurkishLocaleCode
                : UiLocalization.EnglishLocaleCode);
        }

        public void SetLocaleCode(string value)
        {
            string normalized = UiLocalization.NormalizeLocaleCode(value);
            if (localeCode == normalized)
            {
                return;
            }

            localeCode = normalized;
            SaveAndNotify();
            ApplyLocale();
        }

        public void CycleUiScale(int direction)
        {
            int delta = direction < 0 ? -1 : 1;
            int next = (scaleIndex + delta + SupportedScales.Length) % SupportedScales.Length;
            SetUiScaleIndex(next);
        }

        public void SetUiScaleIndex(int value)
        {
            int normalized = Mathf.Clamp(value, 0, SupportedScales.Length - 1);
            if (scaleIndex == normalized)
            {
                return;
            }

            scaleIndex = normalized;
            ApplyScale();
            SaveAndNotify();
        }

        public void SetReducedMotion(bool value)
        {
            if (reducedMotion == value)
            {
                return;
            }

            reducedMotion = value;
            systemRoot?.SetReducedMotion(reducedMotion);
            SaveAndNotify();
        }

        public void SetSubtitlesEnabled(bool value)
        {
            if (subtitlesEnabled == value)
            {
                return;
            }

            subtitlesEnabled = value;
            SaveAndNotify();
        }

        public void SetSubtitleSize(UiSubtitleSize value)
        {
            if (subtitleSize == value)
            {
                return;
            }

            subtitleSize = value;
            SaveAndNotify();
        }

        public void SetDisplayModeIndex(int index)
        {
            FullScreenMode next = SupportedDisplayModes[
                Mathf.Clamp(index, 0, SupportedDisplayModes.Length - 1)];
            if (displayMode == next)
            {
                return;
            }

            displayMode = next;
            Changed?.Invoke();
        }

        public void SetResolutionIndex(int index)
        {
            int next = Mathf.Clamp(index, 0, supportedResolutions.Count - 1);
            if (resolutionIndex == next)
            {
                return;
            }

            resolutionIndex = next;
            RebuildRefreshRates(RefreshRate.value);
            Changed?.Invoke();
        }

        public void SetRefreshRateIndex(int index)
        {
            int next = Mathf.Clamp(
                index,
                0,
                Mathf.Max(0, supportedRefreshRates.Count - 1));
            if (refreshRateIndex == next)
            {
                return;
            }

            refreshRateIndex = next;
            Changed?.Invoke();
        }

        public void ApplyDisplayPreferences()
        {
            if (!HasPendingDisplayChanges)
            {
                return;
            }

            appliedDisplayMode = displayMode;
            appliedResolutionIndex = resolutionIndex;
            appliedRefreshRate = RefreshRate;
            awaitingDisplayConfirmation = true;
            ApplySavedDisplayPreferences();
            Changed?.Invoke();
        }

        public void ConfirmDisplayPreferences()
        {
            awaitingDisplayConfirmation = false;
            confirmedDisplayMode = appliedDisplayMode;
            confirmedResolutionIndex = appliedResolutionIndex;
            confirmedRefreshRate = appliedRefreshRate;
            SaveAndNotify();
        }

        public void RevertDisplayPreferences()
        {
            awaitingDisplayConfirmation = false;
            appliedDisplayMode = confirmedDisplayMode;
            appliedResolutionIndex = confirmedResolutionIndex;
            appliedRefreshRate = confirmedRefreshRate;
            displayMode = confirmedDisplayMode;
            resolutionIndex = confirmedResolutionIndex;
            RebuildRefreshRates(confirmedRefreshRate.value);
            ApplySavedDisplayPreferences();
            Changed?.Invoke();
        }

        public void DiscardPendingDisplayPreferences()
        {
            if (awaitingDisplayConfirmation)
            {
                RevertDisplayPreferences();
                return;
            }

            if (!HasPendingDisplayChanges)
            {
                return;
            }

            displayMode = appliedDisplayMode;
            resolutionIndex = appliedResolutionIndex;
            RebuildRefreshRates(appliedRefreshRate.value);
            Changed?.Invoke();
        }

        public void AdjustMouseSensitivity(int direction)
        {
            SetMouseSensitivity(
                FarionInputActions.MouseSensitivityScale +
                (direction < 0 ? -MouseSensitivityStep : MouseSensitivityStep));
        }

        void SetMouseSensitivity(float value)
        {
            float clamped = Mathf.Clamp(
                Mathf.Round(value / MouseSensitivityStep) * MouseSensitivityStep,
                FarionInputActions.MinimumMouseSensitivity,
                FarionInputActions.MaximumMouseSensitivity);
            if (Mathf.Approximately(FarionInputActions.MouseSensitivityScale, clamped))
            {
                return;
            }

            FarionInputActions.MouseSensitivityScale = clamped;
            Changed?.Invoke();
        }

        public void SetInvertLookY(bool value)
        {
            if (FarionInputActions.InvertLookY == value)
            {
                return;
            }

            FarionInputActions.InvertLookY = value;
            Changed?.Invoke();
        }

        public void AdjustFieldOfView(int direction)
        {
            SetFieldOfView(
                FarionViewPreferences.FieldOfView +
                (direction < 0 ? -FieldOfViewStep : FieldOfViewStep));
        }

        void SetFieldOfView(float value)
        {
            float clamped = Mathf.Clamp(
                Mathf.Round(value / FieldOfViewStep) * FieldOfViewStep,
                FarionViewPreferences.MinimumFieldOfView,
                FarionViewPreferences.MaximumFieldOfView);
            if (Mathf.Approximately(FarionViewPreferences.FieldOfView, clamped))
            {
                return;
            }

            FarionViewPreferences.FieldOfView = clamped;
            Changed?.Invoke();
        }

        public void ResetInputBindings()
        {
            FarionInputActions.ResetBindingOverrides();
            Changed?.Invoke();
        }

        public void SetVSyncEnabled(bool value)
        {
            if (vSyncEnabled == value)
            {
                return;
            }

            vSyncEnabled = value;
            QualitySettings.vSyncCount = vSyncEnabled ? 1 : 0;
            SaveAndNotify();
        }

        void Load()
        {
            localeCode = UiLocalization.NormalizeLocaleCode(
                PlayerPrefs.GetString(LocaleKey, UiLocalization.EnglishLocaleCode));
            scaleIndex = FindClosestScaleIndex(PlayerPrefs.GetFloat(ScaleKey, 1f));
            reducedMotion = PlayerPrefs.GetInt(ReducedMotionKey, 0) != 0;
            subtitlesEnabled = PlayerPrefs.GetInt(SubtitlesKey, 1) != 0;
            subtitleSize = (UiSubtitleSize)Mathf.Clamp(
                PlayerPrefs.GetInt(SubtitleSizeKey, (int)UiSubtitleSize.Standard),
                (int)UiSubtitleSize.Standard,
                (int)UiSubtitleSize.Large);
            displayMode = NormalizeDisplayMode(PlayerPrefs.GetInt(
                DisplayModeKey,
                (int)Screen.fullScreenMode));
            resolutionIndex = FindClosestResolutionIndex(
                PlayerPrefs.GetInt(ResolutionWidthKey, Screen.width),
                PlayerPrefs.GetInt(ResolutionHeightKey, Screen.height));
            appliedDisplayMode = displayMode;
            appliedResolutionIndex = resolutionIndex;
            confirmedDisplayMode = displayMode;
            confirmedResolutionIndex = resolutionIndex;
            vSyncEnabled = PlayerPrefs.GetInt(VSyncKey, 1) != 0;

            RefreshRate current = Screen.currentResolution.refreshRateRatio;
            uint numerator = (uint)Mathf.Max(
                1,
                PlayerPrefs.GetInt(RefreshNumeratorKey, (int)current.numerator));
            uint denominator = (uint)Mathf.Max(
                1,
                PlayerPrefs.GetInt(RefreshDenominatorKey, (int)current.denominator));
            RebuildRefreshRates((double)numerator / denominator);
            appliedRefreshRate = RefreshRate;
            confirmedRefreshRate = appliedRefreshRate;
        }

        void ApplyVisualPreferences()
        {
            ApplyScale();
            systemRoot?.SetReducedMotion(reducedMotion);
        }

        void ApplyScale()
        {
            if (canvasScaler == null)
            {
                return;
            }

            float scale = Mathf.Max(0.01f, UiScale);
            canvasScaler.referenceResolution = baseReferenceResolution / scale;
        }

        void ApplySavedDisplayPreferences()
        {
            Vector2Int resolution = supportedResolutions[Mathf.Clamp(
                appliedResolutionIndex,
                0,
                supportedResolutions.Count - 1)];
            Screen.SetResolution(
                resolution.x,
                resolution.y,
                appliedDisplayMode,
                appliedRefreshRate);
            QualitySettings.vSyncCount = vSyncEnabled ? 1 : 0;
        }

        void ApplyLocale()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (localeRoutine != null)
            {
                StopCoroutine(localeRoutine);
            }

            localeRoutine = StartCoroutine(ApplyLocaleWhenReady(localeCode));
        }

        IEnumerator ApplyLocaleWhenReady(string requestedLocale)
        {
            yield return LocalizationSettings.InitializationOperation;
            if (requestedLocale == localeCode)
            {
                UiLocalization.TrySelectLocale(requestedLocale);
            }

            localeRoutine = null;
        }

        void SaveAndNotify()
        {
            PlayerPrefs.SetString(LocaleKey, localeCode);
            PlayerPrefs.SetFloat(ScaleKey, UiScale);
            PlayerPrefs.SetInt(ReducedMotionKey, reducedMotion ? 1 : 0);
            PlayerPrefs.SetInt(SubtitlesKey, subtitlesEnabled ? 1 : 0);
            PlayerPrefs.SetInt(SubtitleSizeKey, (int)subtitleSize);
            Vector2Int savedResolution = supportedResolutions[Mathf.Clamp(
                confirmedResolutionIndex,
                0,
                supportedResolutions.Count - 1)];
            PlayerPrefs.SetInt(DisplayModeKey, (int)confirmedDisplayMode);
            PlayerPrefs.SetInt(ResolutionWidthKey, savedResolution.x);
            PlayerPrefs.SetInt(ResolutionHeightKey, savedResolution.y);
            PlayerPrefs.SetInt(VSyncKey, vSyncEnabled ? 1 : 0);
            PlayerPrefs.SetInt(
                RefreshNumeratorKey,
                (int)confirmedRefreshRate.numerator);
            PlayerPrefs.SetInt(
                RefreshDenominatorKey,
                (int)Mathf.Max(1, confirmedRefreshRate.denominator));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        void ResolveReferences()
        {
            systemRoot ??= GetComponent<UiSystemRoot>();
            Canvas canvas = UiCompositionScope.FindOwningCanvas(this);
            if (canvasScaler == null && canvas != null)
            {
                canvasScaler = canvas.GetComponent<CanvasScaler>();
            }
        }

        static int FindClosestScaleIndex(float value)
        {
            int closest = 0;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < SupportedScales.Length; i++)
            {
                float distance = Mathf.Abs(SupportedScales[i] - value);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = i;
                }
            }

            return closest;
        }

        void BuildSupportedResolutions()
        {
            supportedResolutions.Clear();
            HashSet<Vector2Int> seen = new();
            Resolution[] available = Screen.resolutions;
            for (int i = 0; i < available.Length; i++)
            {
                Vector2Int candidate = new(available[i].width, available[i].height);
                if (candidate.x > 0 && candidate.y > 0 && seen.Add(candidate))
                {
                    supportedResolutions.Add(candidate);
                }
            }

            Vector2Int nativeResolution = GetNativeResolution();
            if (seen.Add(nativeResolution))
            {
                supportedResolutions.Add(nativeResolution);
            }

            supportedResolutions.Sort(CompareResolutions);
        }

        void RebuildRefreshRates(double preferredValue)
        {
            supportedRefreshRates.Clear();
            Vector2Int resolution = supportedResolutions[ResolutionIndex];
            HashSet<long> seen = new();
            Resolution[] available = Screen.resolutions;
            for (int i = 0; i < available.Length; i++)
            {
                if (available[i].width != resolution.x ||
                    available[i].height != resolution.y)
                {
                    continue;
                }

                RefreshRate rate = available[i].refreshRateRatio;
                if (rate.value <= 0d ||
                    !seen.Add((long)System.Math.Round(rate.value * 1000d)))
                {
                    continue;
                }

                supportedRefreshRates.Add(rate);
            }

            if (supportedRefreshRates.Count == 0)
            {
                supportedRefreshRates.Add(Screen.currentResolution.refreshRateRatio);
            }

            supportedRefreshRates.Sort(CompareRefreshRates);
            refreshRateIndex = FindClosestRefreshRateIndex(preferredValue);
        }

        static int CompareRefreshRates(RefreshRate left, RefreshRate right)
        {
            return left.value.CompareTo(right.value);
        }

        int FindClosestRefreshRateIndex(double value)
        {
            int closest = 0;
            double closestDistance = double.MaxValue;
            for (int i = 0; i < supportedRefreshRates.Count; i++)
            {
                double distance = System.Math.Abs(
                    supportedRefreshRates[i].value - value);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = i;
                }
            }

            return closest;
        }

        static bool MatchesRefreshRate(RefreshRate left, RefreshRate right)
        {
            return System.Math.Abs(left.value - right.value) < 0.01d;
        }

        static int CompareResolutions(Vector2Int left, Vector2Int right)
        {
            int byArea = (left.x * left.y).CompareTo(right.x * right.y);
            return byArea != 0 ? byArea : left.x.CompareTo(right.x);
        }

        static Vector2Int GetNativeResolution()
        {
            Resolution current = Screen.currentResolution;
            return new Vector2Int(
                Mathf.Max(1, current.width > 0 ? current.width : Screen.width),
                Mathf.Max(1, current.height > 0 ? current.height : Screen.height));
        }

        int FindClosestResolutionIndex(int width, int height)
        {
            int closest = 0;
            long closestDistance = long.MaxValue;
            for (int i = 0; i < supportedResolutions.Count; i++)
            {
                long widthDelta = supportedResolutions[i].x - width;
                long heightDelta = supportedResolutions[i].y - height;
                long distance = widthDelta * widthDelta + heightDelta * heightDelta;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = i;
                }
            }

            return closest;
        }

        static FullScreenMode NormalizeDisplayMode(int value)
        {
            for (int i = 0; i < SupportedDisplayModes.Length; i++)
            {
                if ((int)SupportedDisplayModes[i] == value)
                {
                    return SupportedDisplayModes[i];
                }
            }

            return FullScreenMode.FullScreenWindow;
        }

        static int FindDisplayModeIndex(FullScreenMode mode)
        {
            for (int i = 0; i < SupportedDisplayModes.Length; i++)
            {
                if (SupportedDisplayModes[i] == mode)
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
