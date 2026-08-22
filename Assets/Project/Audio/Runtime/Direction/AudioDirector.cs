using System;
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace Farion.Audio.Direction
{
    public enum AudioSceneContextId
    {
        MainMenu = 0,
        Gameplay = 1
    }

    public enum UiAudioCue
    {
        Focus = 0,
        Confirm = 10,
        Back = 20,
        Unavailable = 30,
        Success = 40,
        Error = 50
    }

    public enum AudioBusId
    {
        Master = 0,
        Music = 10,
        Ambience = 20,
        Sfx = 30,
        Ui = 40
    }

    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StudioListener))]
    public sealed class AudioDirector : MonoBehaviour
    {
        const string MenuScoreEvent = "event:/Music/AdaptiveScore";
        const string SpaceAmbientEvent = "event:/Music/SpaceAmbient";
        const string AmbienceEvent = "event:/Ambience/World";
        const string GameContextParameter = "GameContext";
        const string AtmosphereParameter = "Atmosphere";
        const string InteriorParameter = "Interior";
        const string PausedParameter = "Paused";
        const float CueDedupeSeconds = 0.06f;
        const float FocusSuppressionSeconds = 0.1f;

        static readonly string[] BusPaths =
        {
            "bus:/",
            "bus:/Music",
            "bus:/Ambience",
            "bus:/SFX",
            "bus:/UI"
        };

        static readonly string[] VolumeKeys =
        {
            "farion.audio.master",
            "farion.audio.music",
            "farion.audio.ambience",
            "farion.audio.sfx",
            "farion.audio.ui"
        };

        static readonly string[] UiEventPaths =
        {
            "event:/UI/Focus",
            "event:/UI/Confirm",
            "event:/UI/Back",
            "event:/UI/Unavailable",
            "event:/UI/Success",
            "event:/UI/Error"
        };

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static readonly HashSet<string> MissingEventWarnings = new();
#endif

        [Header("Environment Response")]
        [Min(0f)] [SerializeField] float atmosphereAttackSeconds = 0.8f;
        [Min(0f)] [SerializeField] float atmosphereReleaseSeconds = 1.8f;
        [Min(0f)] [SerializeField] float interiorBlendSeconds = 0.35f;

        [Header("Scene Mix")]
        [Min(0f)] [SerializeField] float musicFadeInSeconds = 2f;

        readonly float[] busVolumes = { 1f, 1f, 1f, 1f, 1f };
        readonly bool[] missingBusWarnings = new bool[5];
        EventInstance scoreInstance;
        EventInstance spaceAmbientInstance;
        EventInstance ambienceInstance;
        Camera listenerCamera;
        bool ownsRuntime;
        AudioSceneContextId sceneContext;
        float activeMusicFade = 1f;
        float atmosphere;
        float targetAtmosphere;
        float interior;
        float targetInterior;
        UiAudioCue lastCue;
        float lastCueTime = float.NegativeInfinity;
        float suppressFocusUntil = float.NegativeInfinity;

        public static AudioDirector Current { get; private set; }
        public event Action Changed;

        void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(gameObject);
                return;
            }

            Current = this;
            ownsRuntime = true;
            DontDestroyOnLoad(gameObject);
            SyncListenerToCamera();
            LoadVolumes();
            StartPersistentEvent(MenuScoreEvent, out scoreInstance);
            StartPersistentEvent(SpaceAmbientEvent, out spaceAmbientInstance);
            StartPersistentEvent(AmbienceEvent, out ambienceInstance);
            ApplySceneMix();
            ApplyAllBusVolumes();
        }

        void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            SyncListenerToCamera();
            activeMusicFade = MoveTowards(
                activeMusicFade,
                1f,
                musicFadeInSeconds,
                deltaTime);
            atmosphere = MoveTowards(
                atmosphere,
                targetAtmosphere,
                targetAtmosphere > atmosphere ? atmosphereAttackSeconds : atmosphereReleaseSeconds,
                deltaTime);
            interior = MoveTowards(interior, targetInterior, interiorBlendSeconds, deltaTime);

            ApplySceneMix();
            SetGlobalParameter(AtmosphereParameter, atmosphere);
            SetGlobalParameter(InteriorParameter, interior);
        }

        void OnValidate()
        {
            atmosphereAttackSeconds = Mathf.Max(0f, atmosphereAttackSeconds);
            atmosphereReleaseSeconds = Mathf.Max(0f, atmosphereReleaseSeconds);
            interiorBlendSeconds = Mathf.Max(0f, interiorBlendSeconds);
            musicFadeInSeconds = Mathf.Max(0f, musicFadeInSeconds);
        }

        void OnDestroy()
        {
            if (!ownsRuntime || Current != this)
            {
                return;
            }

            StopAndRelease(ref ambienceInstance, immediate: false);
            StopAndRelease(ref spaceAmbientInstance, immediate: false);
            StopAndRelease(ref scoreInstance, immediate: false);
            Current = null;
        }

        public void SetSceneContext(AudioSceneContextId context)
        {
            sceneContext = context;
            activeMusicFade = 0f;
            ApplySceneMix();
            SetGlobalParameter(GameContextParameter, (float)context);
        }

        public void SetEnvironment(float atmosphereAmount, bool isInterior)
        {
            targetAtmosphere = Mathf.Clamp01(atmosphereAmount);
            targetInterior = isInterior ? 1f : 0f;
        }

        public void SetPaused(bool paused)
        {
            SetGlobalParameter(PausedParameter, paused ? 1f : 0f);
        }

        public void PlayUi(UiAudioCue cue)
        {
            float now = Time.unscaledTime;
            if (cue == UiAudioCue.Focus && now < suppressFocusUntil)
            {
                return;
            }

            if (cue == lastCue && now - lastCueTime < CueDedupeSeconds)
            {
                return;
            }

            if (cue is UiAudioCue.Confirm or UiAudioCue.Back)
            {
                suppressFocusUntil = now + FocusSuppressionSeconds;
            }

            lastCue = cue;
            lastCueTime = now;
            try
            {
                RuntimeManager.PlayOneShot(UiEventPaths[ToUiCueIndex(cue)]);
            }
            catch (EventNotFoundException)
            {
                WarnMissingEventOnce(UiEventPaths[ToUiCueIndex(cue)]);
            }
        }

        public float GetBusVolume(AudioBusId bus)
        {
            return busVolumes[ToBusIndex(bus)];
        }

        public void AdjustBusVolume(AudioBusId bus, int direction)
        {
            float current = GetBusVolume(bus);
            SetBusVolume(bus, current + (direction < 0 ? -0.1f : 0.1f));
        }

        public void SetBusVolume(AudioBusId bus, float value)
        {
            int index = ToBusIndex(bus);
            float normalized = Mathf.Round(Mathf.Clamp01(value) * 10f) / 10f;
            if (Mathf.Approximately(busVolumes[index], normalized))
            {
                return;
            }

            busVolumes[index] = normalized;
            PlayerPrefs.SetFloat(VolumeKeys[index], normalized);
            PlayerPrefs.Save();
            ApplyBusVolume(index);
            Changed?.Invoke();
        }

        internal static float ToLinearVolume(float normalized)
        {
            normalized = Mathf.Clamp01(normalized);
            return normalized * normalized;
        }

        internal static void ResolveSceneMix(
            AudioSceneContextId context,
            float fade,
            out float menuVolume,
            out float gameplayVolume)
        {
            fade = Mathf.Clamp01(fade);
            menuVolume = context == AudioSceneContextId.MainMenu ? fade : 0f;
            gameplayVolume = context == AudioSceneContextId.Gameplay ? fade : 0f;
        }

        void LoadVolumes()
        {
            for (int i = 0; i < busVolumes.Length; i++)
            {
                busVolumes[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKeys[i], 1f));
            }
        }

        void ApplyAllBusVolumes()
        {
            for (int i = 0; i < busVolumes.Length; i++)
            {
                ApplyBusVolume(i);
            }
        }

        void ApplyBusVolume(int index)
        {
            try
            {
                Bus bus = RuntimeManager.GetBus(BusPaths[index]);
                FMOD.RESULT result = bus.setVolume(ToLinearVolume(busVolumes[index]));
                if (result != FMOD.RESULT.OK && !missingBusWarnings[index])
                {
                    missingBusWarnings[index] = true;
                    Debug.LogWarning($"FMOD bus '{BusPaths[index]}' volume could not be set: {result}.", this);
                }
            }
            catch (BusNotFoundException)
            {
                if (!missingBusWarnings[index])
                {
                    missingBusWarnings[index] = true;
                    Debug.LogWarning($"FMOD bus was not found: {BusPaths[index]}.", this);
                }
            }
        }

        static void StartPersistentEvent(string path, out EventInstance instance)
        {
            try
            {
                instance = RuntimeManager.CreateInstance(path);
                if (instance.isValid())
                {
                    instance.start();
                }
            }
            catch (EventNotFoundException)
            {
                instance = default;
                WarnMissingEventOnce(path);
            }
        }

        void ApplySceneMix()
        {
            ResolveSceneMix(
                sceneContext,
                activeMusicFade,
                out float menuVolume,
                out float gameplayVolume);
            SetInstanceVolume(scoreInstance, menuVolume);
            SetInstanceVolume(spaceAmbientInstance, gameplayVolume);
        }

        void SyncListenerToCamera()
        {
            if (listenerCamera == null || !listenerCamera.isActiveAndEnabled)
            {
                listenerCamera = Camera.main;
            }

            if (listenerCamera != null)
            {
                transform.SetPositionAndRotation(
                    listenerCamera.transform.position,
                    listenerCamera.transform.rotation);
            }
        }

        static void SetInstanceVolume(EventInstance instance, float volume)
        {
            if (instance.isValid())
            {
                instance.setVolume(volume);
            }
        }

        static void WarnMissingEventOnce(string path)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (MissingEventWarnings.Add(path))
            {
                Debug.LogWarning($"FMOD event was not found: {path}. Refresh or rebuild the FMOD banks.");
            }
#endif
        }

        static void StopAndRelease(ref EventInstance instance, bool immediate)
        {
            if (!instance.isValid())
            {
                return;
            }

            instance.stop(
                immediate
                    ? FMOD.Studio.STOP_MODE.IMMEDIATE
                    : FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            instance.release();
            instance.clearHandle();
        }

        static void SetGlobalParameter(string name, float value)
        {
            RuntimeManager.StudioSystem.setParameterByName(name, value);
        }

        static float MoveTowards(float current, float target, float seconds, float deltaTime)
        {
            return seconds <= 0f
                ? target
                : Mathf.MoveTowards(current, target, Mathf.Max(0f, deltaTime) / seconds);
        }

        static int ToBusIndex(AudioBusId bus)
        {
            return bus switch
            {
                AudioBusId.Master => 0,
                AudioBusId.Music => 1,
                AudioBusId.Ambience => 2,
                AudioBusId.Sfx => 3,
                AudioBusId.Ui => 4,
                _ => throw new ArgumentOutOfRangeException(nameof(bus), bus, null)
            };
        }

        static int ToUiCueIndex(UiAudioCue cue)
        {
            return cue switch
            {
                UiAudioCue.Focus => 0,
                UiAudioCue.Confirm => 1,
                UiAudioCue.Back => 2,
                UiAudioCue.Unavailable => 3,
                UiAudioCue.Success => 4,
                UiAudioCue.Error => 5,
                _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, null)
            };
        }
    }
}
