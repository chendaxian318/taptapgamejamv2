using System;
using System.Collections.Generic;
using UnityEngine;

namespace HabitatIsland
{
    public enum HabitatAudioCue { Click, Open, Close, Confirm, Error, Back }

    /// <summary>Presentation-only audio. Call Play after accepting an input or committing an action.</summary>
    public sealed partial class HabitatAudio : MonoBehaviour
    {
        const string PreferencePrefix = "HabitatIsland.Audio.";
        const double ScheduleLead = .08;
        const double CueInterval = .06;
        const int CueCount = 6;
        static bool quitting;
        static readonly string[] CueNames = { "select", "open", "close", "confirm", "error", "back" };
        // At the existing default effects volume (.5), these match the pack's gentle mix.
        static readonly float[] CueGains = { .462f, .3234f, .3234f, .3234f, .462f, .462f };

        public static HabitatAudio Instance { get; private set; }
        public float AmbienceVolume => ambienceVolume;
        public float EffectsVolume => effectsVolume;
        public bool Muted => muted;
        public HabitatAudioCue LastPlayedCue { get; private set; }
        public int CuePlayCount { get; private set; }
        public int AmbienceCycleCount { get; private set; }
        public bool AmbienceRunning => ambienceScheduled;
        public bool Ducked => ducked;
        public float DuckGain => duckGain;

        readonly AudioSource[] ambienceSources = new AudioSource[2];
        readonly AudioClip[] cueClips = new AudioClip[CueCount];
        readonly bool[] queuedCues = new bool[CueCount];
        readonly ulong[] cueOrder = new ulong[CueCount];
        readonly double[] nextCueAt = new double[CueCount];
        readonly double[] ambienceStarts = new double[2];
        readonly bool[] cycleCounted = new bool[2];
        AudioSource effectsSource;
        AudioClip ambienceClip;
        float ambienceVolume = .25f, effectsVolume = .5f;
        float duckGain = 1f;
        bool ducked;
        bool muted, initialized, preferencesDirty, suspended, ambienceScheduled, warnedAudio;
        double clipSeconds, crossfadeSeconds, cycleSeconds, firstStart, latestStart, lastDsp;
        ulong requestOrder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; quitting = false; }

        public static HabitatAudio Ensure()
        {
            if (!Application.isPlaying || quitting) return null;
            if (Instance) return Instance;
            HabitatAudio existing = FindAnyObjectByType<HabitatAudio>(FindObjectsInactive.Include);
            if (existing) { Instance = existing; return existing; }
            return new GameObject("Habitat audio").AddComponent<HabitatAudio>();
        }

        public static void Play(HabitatAudioCue cue)
        {
            int index = (int)cue;
            if (index < 0 || index >= CueCount) return;
            HabitatAudio audio = Ensure();
            if (!audio || !audio.isActiveAndEnabled || audio.suspended || audio.muted || audio.effectsVolume <= 0f) return;
            audio.queuedCues[index] = true;
            audio.cueOrder[index] = ++audio.requestOrder;
        }

        public static void ClearTransient()
        {
            if (Instance) Instance.StopTransient();
        }

        public static void SetDucked(bool value)
        {
            HabitatAudio audio = Ensure();
            if (audio) audio.ducked = value;
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ambienceVolume = ValidVolume(PlayerPrefs.GetFloat(PreferencePrefix + "Ambience", .25f), .25f);
            effectsVolume = ValidVolume(PlayerPrefs.GetFloat(PreferencePrefix + "Effects", .5f), .5f);
            muted = PlayerPrefs.GetInt(PreferencePrefix + "Muted", 0) != 0;
            for (int i = 0; i < ambienceSources.Length; i++) ambienceSources[i] = CreateSource(128);
            effectsSource = CreateSource(64);
            var missing = new List<string>();
            ambienceClip = LoadClip("Audio/Ambience/SeaWavesBirds", missing);
            for (int i = 0; i < CueCount; i++) cueClips[i] = LoadClip("Audio/Interface/" + CueNames[i], missing);
            InitializeActions(missing);
            if (missing.Count > 0) WarnAudioOnce("Missing clips: " + string.Join(", ", missing));
            initialized = true;
        }

        AudioSource CreateSource(int priority)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = priority;
            source.volume = 0f;
            return source;
        }

        static AudioClip LoadClip(string path, List<string> missing)
        {
            AudioClip clip = Resources.Load<AudioClip>(path);
            if (!clip) { missing.Add(path); return null; }
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            return clip;
        }

        void OnEnable()
        {
            if (!initialized || Instance != this) return;
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            StartAmbience();
        }

        void LateUpdate()
        {
            if (!initialized || suspended) return;
            double now = AudioSettings.dspTime;
            duckGain = Mathf.MoveTowards(duckGain, ducked ? .45f : 1f, Time.unscaledDeltaTime * 1.8f);
            if (ambienceScheduled && now < lastDsp) StopAudio();
            if (!ambienceScheduled) StartAmbience();
            if (ambienceScheduled) UpdateAmbience(now);
            effectsSource.volume = muted ? 0f : effectsVolume;
            bool actionRequested = PlayQueuedActions(now);
            PlayQueuedCue(now, actionRequested);
            lastDsp = now;
        }

        void StartAmbience()
        {
            if (!initialized || suspended || !isActiveAndEnabled || !ambienceClip || ambienceScheduled) return;
            if (ambienceClip.loadState == AudioDataLoadState.Failed)
            { WarnAudioOnce("The sea ambience could not be loaded."); return; }
            // Streaming data may be released when both sources stop (disable/device reset).
            if (ambienceClip.loadState == AudioDataLoadState.Unloaded) ambienceClip.LoadAudioData();
            if (ambienceClip.loadState != AudioDataLoadState.Loaded) return;
            clipSeconds = ambienceClip.samples / (double)Math.Max(1, ambienceClip.frequency);
            if (clipSeconds <= .01) { WarnAudioOnce("The sea ambience clip is empty."); return; }
            // A short clip still needs room to finish before its source is reused.
            crossfadeSeconds = Math.Min(1.2, clipSeconds * .25);
            cycleSeconds = clipSeconds - crossfadeSeconds;
            double now = AudioSettings.dspTime;
            firstStart = now + ScheduleLead;
            ScheduleAmbience(0, firstStart);
            ScheduleAmbience(1, firstStart + cycleSeconds);
            latestStart = firstStart + cycleSeconds;
            lastDsp = now;
            ambienceScheduled = true;
        }

        void ScheduleAmbience(int index, double start)
        {
            AudioSource source = ambienceSources[index];
            source.Stop();
            source.clip = ambienceClip;
            source.volume = 0f;
            ambienceStarts[index] = start;
            cycleCounted[index] = false;
            source.PlayScheduled(start);
            source.SetScheduledEndTime(start + clipSeconds);
        }

        void UpdateAmbience(double now)
        {
            // Resume after a long DSP jump from a fresh pair, never schedule in the past.
            if (now >= latestStart + clipSeconds)
            {
                StopAudio();
                StartAmbience();
                return;
            }
            for (int i = 0; i < ambienceSources.Length; i++)
            {
                if (!cycleCounted[i] && now >= ambienceStarts[i])
                { cycleCounted[i] = true; AmbienceCycleCount++; }
                if (now >= ambienceStarts[i] + clipSeconds)
                {
                    double nextStart = latestStart + cycleSeconds;
                    if (nextStart <= now + .01)
                    {
                        StopAudio();
                        StartAmbience();
                        return;
                    }
                    ScheduleAmbience(i, nextStart);
                    latestStart = nextStart;
                }
                ambienceSources[i].volume = muted ? 0f : ambienceVolume * duckGain * AmbienceEnvelope(now, ambienceStarts[i]);
            }
        }

        float AmbienceEnvelope(double now, double start)
        {
            double elapsed = now - start;
            if (elapsed <= 0 || elapsed >= clipSeconds) return 0f;
            double fadeIn = start == firstStart ? Math.Min(1.0, cycleSeconds) : crossfadeSeconds;
            if (elapsed < fadeIn) return (float)Math.Sin(elapsed / fadeIn * Math.PI * .5);
            if (elapsed > cycleSeconds) return (float)Math.Cos((elapsed - cycleSeconds) / crossfadeSeconds * Math.PI * .5);
            return 1f;
        }

        void PlayQueuedCue(double now, bool actionRequested)
        {
            int selected = -1, priority = -1;
            ulong order = 0;
            for (int i = 0; i < CueCount; i++)
            {
                if (!queuedCues[i]) continue;
                queuedCues[i] = false;
                if (actionRequested && (i == (int)HabitatAudioCue.Click || i == (int)HabitatAudioCue.Confirm)) continue;
                if (muted || effectsVolume <= 0f || now < nextCueAt[i] || !cueClips[i]) continue;
                if (cueClips[i].loadState == AudioDataLoadState.Failed)
                { WarnAudioOnce("An interface clip could not be loaded."); continue; }
                if (cueClips[i].loadState == AudioDataLoadState.Unloaded) cueClips[i].LoadAudioData();
                if (cueClips[i].loadState != AudioDataLoadState.Loaded) continue;
                int rank = i == (int)HabitatAudioCue.Error ? 3 : i == (int)HabitatAudioCue.Confirm ? 2 : i == (int)HabitatAudioCue.Click ? 0 : 1;
                if (rank > priority || (rank == priority && cueOrder[i] > order))
                { selected = i; priority = rank; order = cueOrder[i]; }
            }
            if (selected < 0) return;
            effectsSource.PlayOneShot(cueClips[selected], CueGains[selected]);
            nextCueAt[selected] = now + CueInterval;
            LastPlayedCue = (HabitatAudioCue)selected;
            CuePlayCount++;
        }

        public void SetAmbienceVolume(float value)
        {
            value = ValidVolume(value, ambienceVolume);
            if (Mathf.Approximately(ambienceVolume, value)) return;
            ambienceVolume = value;
            PlayerPrefs.SetFloat(PreferencePrefix + "Ambience", value);
            preferencesDirty = true;
            ApplyVolume();
        }

        public void SetEffectsVolume(float value)
        {
            value = ValidVolume(value, effectsVolume);
            if (Mathf.Approximately(effectsVolume, value)) return;
            effectsVolume = value;
            PlayerPrefs.SetFloat(PreferencePrefix + "Effects", value);
            preferencesDirty = true;
            ApplyVolume();
        }

        public void SetMuted(bool value)
        {
            if (muted == value) return;
            muted = value;
            PlayerPrefs.SetInt(PreferencePrefix + "Muted", value ? 1 : 0);
            preferencesDirty = true;
            if (muted) StopTransient();
            ApplyVolume();
        }

        void ApplyVolume()
        {
            if (!initialized) return;
            if (effectsSource) effectsSource.volume = muted ? 0f : effectsVolume;
            ApplyActionVolumes();
            double now = AudioSettings.dspTime;
            for (int i = 0; i < ambienceSources.Length; i++)
                if (ambienceSources[i]) ambienceSources[i].volume = muted || !ambienceScheduled ? 0f : ambienceVolume * duckGain * AmbienceEnvelope(now, ambienceStarts[i]);
        }

        static float ValidVolume(float value, float fallback)
        { return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value); }

        void WarnAudioOnce(string detail)
        {
            if (warnedAudio) return;
            warnedAudio = true;
            Debug.LogWarning("[Habitat audio] " + detail + " Unavailable audio will remain silent.");
        }

        void StopAudio()
        {
            ambienceScheduled = false;
            for (int i = 0; i < ambienceSources.Length; i++) if (ambienceSources[i]) ambienceSources[i].Stop();
            StopTransient();
        }

        void StopTransient()
        {
            if (effectsSource) effectsSource.Stop();
            Array.Clear(queuedCues, 0, queuedCues.Length);
            Array.Clear(nextCueAt, 0, nextCueAt.Length);
            StopActions();
        }

        void SavePreferences()
        {
            if (!preferencesDirty) return;
            PlayerPrefs.Save();
            preferencesDirty = false;
        }

        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            StopAudio();
            StartAmbience();
        }

        void OnApplicationPause(bool paused)
        {
            if (!initialized || Instance != this) return;
            suspended = paused;
            if (paused) { StopAudio(); SavePreferences(); }
            else StartAmbience();
        }

        void OnDisable()
        {
            if (!initialized || Instance != this) return;
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            StopAudio();
            SavePreferences();
        }

        void OnApplicationQuit()
        {
            if (Instance != this) return;
            quitting = true;
            SavePreferences();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
