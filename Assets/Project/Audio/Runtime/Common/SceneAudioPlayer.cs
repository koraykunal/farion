using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Farion.Audio
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class SceneAudioPlayer : MonoBehaviour
    {
        [SerializeField] bool playOnEnable = true;
        [SerializeField] bool logPlayback;
        [SerializeField] SceneAudioLoop[] loops = Array.Empty<SceneAudioLoop>();

        void Awake()
        {
            EnsureSources();
        }

        void OnEnable()
        {
            if (playOnEnable)
            {
                Play();
            }
        }

        void Start()
        {
            if (playOnEnable)
            {
                Play();
            }
        }

        void OnDisable()
        {
            Stop(immediate: true);
        }

        void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            for (int i = 0; i < loops.Length; i++)
            {
                loops[i]?.Tick(deltaTime, logPlayback, this);
            }
        }

        [ContextMenu("Play")]
        public void Play()
        {
            EnsureSources();
            for (int i = 0; i < loops.Length; i++)
            {
                loops[i]?.Play(logPlayback, this);
            }
        }

        [ContextMenu("Stop")]
        public void Stop()
        {
            Stop(immediate: false);
        }

        public void Stop(bool immediate)
        {
            for (int i = 0; i < loops.Length; i++)
            {
                loops[i]?.Stop(immediate);
            }
        }

        void OnValidate()
        {
            if (loops == null)
            {
                loops = Array.Empty<SceneAudioLoop>();
            }

            for (int i = 0; i < loops.Length; i++)
            {
                loops[i]?.Validate();
            }
        }

        void EnsureSources()
        {
            if (loops == null)
            {
                return;
            }

            for (int i = 0; i < loops.Length; i++)
            {
                loops[i]?.EnsureSource(transform);
            }
        }

        [Serializable]
        sealed class SceneAudioLoop
        {
            [SerializeField] string label;
            [SerializeField] AudioSource source;
            [SerializeField] AudioClip clip;
            [SerializeField] AudioMixerGroup outputGroup;
            [Range(0f, 1f)]
            [SerializeField] float volume = 0.7f;
            [Min(0.01f)]
            [SerializeField] float pitch = 1f;
            [Min(0f)]
            [SerializeField] float fadeInSeconds = 1.5f;
            [Min(0f)]
            [SerializeField] float fadeOutSeconds = 0.8f;
            [SerializeField] bool spatial;

            float targetVolume;
            bool stopping;

            public void EnsureSource(Transform owner)
            {
                Validate();
                if (source != null)
                {
                    ConfigureSource();
                    return;
                }

                GameObject child = new(string.IsNullOrWhiteSpace(label) ? "AudioLoop" : $"Audio_{label}");
                child.transform.SetParent(owner, false);
                source = child.AddComponent<AudioSource>();
                ConfigureSource();
            }

            public void Play(bool logPlayback, UnityEngine.Object logContext)
            {
                if (source == null)
                {
                    return;
                }

                ConfigureSource();
                targetVolume = volume;
                stopping = false;
                if (!source.isPlaying && clip != null)
                {
                    TryPlay(logPlayback, logContext, resetVolume: true);
                }
            }

            public void Stop(bool immediate)
            {
                if (source == null)
                {
                    return;
                }

                if (immediate || fadeOutSeconds <= 0f)
                {
                    source.Stop();
                    source.volume = 0f;
                    targetVolume = 0f;
                    stopping = false;
                    return;
                }

                targetVolume = 0f;
                stopping = true;
            }

            public void Tick(float deltaTime, bool logPlayback, UnityEngine.Object logContext)
            {
                if (source == null)
                {
                    return;
                }

                if (!source.isPlaying && targetVolume > 0.0001f && clip != null)
                {
                    TryPlay(logPlayback, logContext, resetVolume: source.volume <= 0.0001f);
                }

                if (!source.isPlaying)
                {
                    return;
                }

                float seconds = targetVolume > source.volume ? fadeInSeconds : fadeOutSeconds;
                source.volume = seconds <= 0f
                    ? targetVolume
                    : Mathf.MoveTowards(source.volume, targetVolume, deltaTime / seconds);

                if (stopping && source.volume <= 0.0001f)
                {
                    source.Stop();
                    stopping = false;
                }
            }

            public void Validate()
            {
                volume = Mathf.Clamp01(volume);
                pitch = Mathf.Max(0.01f, pitch);
                fadeInSeconds = Mathf.Max(0f, fadeInSeconds);
                fadeOutSeconds = Mathf.Max(0f, fadeOutSeconds);
            }

            void ConfigureSource()
            {
                if (source == null)
                {
                    return;
                }

                source.clip = clip;
                source.outputAudioMixerGroup = outputGroup;
                source.loop = true;
                source.playOnAwake = false;
                source.pitch = pitch;
                source.spatialBlend = spatial ? 1f : 0f;
                source.dopplerLevel = 0f;
                source.ignoreListenerPause = true;
            }

            void TryPlay(bool logPlayback, UnityEngine.Object logContext, bool resetVolume)
            {
                if (source == null || clip == null)
                {
                    return;
                }

                if (clip.loadState == AudioDataLoadState.Unloaded)
                {
                    clip.LoadAudioData();
                }

                if (clip.loadState == AudioDataLoadState.Failed)
                {
                    if (logPlayback)
                    {
                        Debug.LogWarning($"Audio clip failed to load: {clip.name}", logContext);
                    }

                    return;
                }

                if (resetVolume)
                {
                    source.volume = fadeInSeconds <= 0f ? volume : 0f;
                }

                source.Play();
                if (logPlayback)
                {
                    Debug.Log(
                        $"Playing scene audio '{clip.name}' on '{source.gameObject.name}' loadState={clip.loadState} volume={volume:0.00} pitch={pitch:0.00}.",
                        logContext);
                }
            }
        }
    }
}
