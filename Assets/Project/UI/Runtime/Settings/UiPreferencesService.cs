using System;
using System.Collections;
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

        static readonly float[] SupportedScales = { 1f, 1.1f, 1.25f };

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
        Coroutine localeRoutine;

        public event Action Changed;

        public string LocaleCode => localeCode;
        public float UiScale => SupportedScales[Mathf.Clamp(scaleIndex, 0, SupportedScales.Length - 1)];
        public bool ReducedMotion => reducedMotion;
        public bool SubtitlesEnabled => subtitlesEnabled;
        public UiSubtitleSize SubtitleSize => subtitleSize;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
            Load();
            ApplyVisualPreferences();
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
    }
}
