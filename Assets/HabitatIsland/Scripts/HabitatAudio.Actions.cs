using System;
using System.Collections.Generic;
using UnityEngine;

namespace HabitatIsland
{
    public enum HabitatActionCue
    {
        StepGrass, StepWood, StepSand, Push, Pick, Drop, Soil, Trap, Chest,
        Harvest, Trade, Lure, Tide, FishingCast, FishingReel, FishingMiss, Rotate
    }

    public sealed partial class HabitatAudio
    {
        const int ActionCount = 17;
        const int ActionVoiceCount = 6;

        sealed class ActionSound
        {
            public readonly string[] Names;
            public readonly float Gain, Pitch;
            public readonly double Interval;
            public readonly int Priority;

            public ActionSound(string name, float gain, double interval, int priority = 1,
                float pitch = 1f, string variant = null)
            {
                Names = variant == null ? new[] { name } : new[] { name, variant };
                // Preserve the existing effects preference; default .5 yields pack gain * .55.
                Gain = gain * 1.1f;
                Pitch = pitch;
                Interval = interval;
                Priority = priority;
            }
        }

        static readonly ActionSound[] ActionSounds =
        {
            new ActionSound("footstep_grass_000", .28f, .12, 0, variant: "footstep_grass_001"),
            new ActionSound("footstep_wood_000", .28f, .12, 0, variant: "footstep_wood_001"),
            new ActionSound("step_sand_1", .38f, .12, 0, variant: "step_sand_2"),
            new ActionSound("impactWood_light_000", .42f, .12, variant: "impactWood_medium_000"),
            new ActionSound("pick", .55f, .08),
            new ActionSound("drop", .58f, .08),
            new ActionSound("digging", .35f, .15),
            new ActionSound("impactSoft_medium_000", .38f, .12, variant: "impactSoft_medium_001"),
            new ActionSound("reward", .32f, .25, 3),
            new ActionSound("harvest", .35f, .25, 3),
            new ActionSound("coin_pickup", .34f, .12, 3),
            new ActionSound("water", .32f, .2, pitch: 1.1f),
            new ActionSound("water", .45f, .5, pitch: .86f),
            new ActionSound("splash", .32f, .25, 2),
            new ActionSound("water", .32f, .2, 2, .9f),
            new ActionSound("splash", .26f, .25, 2, .86f),
            new ActionSound("impactSoft_medium_000", .32f, .12, pitch: 1.1f, variant: "impactSoft_medium_001")
        };

        readonly AudioSource[] actionSources = new AudioSource[ActionVoiceCount];
        readonly AudioClip[][] actionClips = new AudioClip[ActionCount][];
        readonly bool[] queuedActions = new bool[ActionCount];
        readonly ulong[] actionOrder = new ulong[ActionCount];
        readonly double[] nextActionAt = new double[ActionCount];
        readonly int[] actionVariants = new int[ActionCount];
        readonly float[] actionVoiceGains = new float[ActionVoiceCount];
        readonly int[] actionVoicePriorities = new int[ActionVoiceCount];
        readonly double[] actionVoiceStarts = new double[ActionVoiceCount];
        readonly double[] actionVoiceEnds = new double[ActionVoiceCount];

        public HabitatActionCue LastActionCue { get; private set; }
        public string LastActionClipName { get; private set; }
        public int ActionPlayCount { get; private set; }

        /// <summary>Call only for a committed action or an actual presentation event.</summary>
        public static void PlayAction(HabitatActionCue cue)
        {
            int index = (int)cue;
            if (index < 0 || index >= ActionCount) return;
            HabitatAudio audio = Ensure();
            if (!audio || !audio.isActiveAndEnabled || audio.suspended || audio.muted || audio.effectsVolume <= 0f) return;
            audio.queuedActions[index] = true;
            audio.actionOrder[index] = ++audio.requestOrder;
        }

        void InitializeActions(List<string> missing)
        {
            for (int i = 0; i < ActionVoiceCount; i++) actionSources[i] = CreateSource(80);
            var loaded = new Dictionary<string, AudioClip>();
            for (int i = 0; i < ActionCount; i++)
            {
                string[] names = ActionSounds[i].Names;
                actionClips[i] = new AudioClip[names.Length];
                for (int variant = 0; variant < names.Length; variant++)
                {
                    if (!loaded.TryGetValue(names[variant], out AudioClip clip))
                    {
                        clip = LoadClip("Audio/Coast/" + names[variant], missing);
                        loaded.Add(names[variant], clip);
                    }
                    actionClips[i][variant] = clip;
                }
            }
        }

        bool PlayQueuedActions(double now)
        {
            bool requested = false;
            ApplyActionVolumes();
            // Process accepted requests in order. Multiple identical events in a frame coalesce.
            for (int remaining = ActionCount; remaining > 0; remaining--)
            {
                int selected = -1;
                ulong order = ulong.MaxValue;
                for (int i = 0; i < ActionCount; i++)
                    if (queuedActions[i] && actionOrder[i] < order)
                    { selected = i; order = actionOrder[i]; }
                if (selected < 0) break;
                requested = true;
                queuedActions[selected] = false;
                if (muted || effectsVolume <= 0f || now < nextActionAt[selected]) continue;
                ActionSound sound = ActionSounds[selected];
                int variant = actionVariants[selected] % actionClips[selected].Length;
                AudioClip clip = actionClips[selected][variant];
                if (!clip) continue;
                if (clip.loadState == AudioDataLoadState.Failed)
                { WarnAudioOnce("An action clip could not be loaded."); continue; }
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                if (clip.loadState != AudioDataLoadState.Loaded) continue;
                int voice = FindActionVoice(now, sound.Priority);
                if (voice < 0) continue;
                AudioSource source = actionSources[voice];
                source.Stop();
                source.clip = clip;
                source.pitch = sound.Pitch;
                source.volume = effectsVolume * sound.Gain;
                source.Play();
                actionVoiceGains[voice] = sound.Gain;
                actionVoicePriorities[voice] = sound.Priority;
                actionVoiceStarts[voice] = now;
                actionVoiceEnds[voice] = now + clip.samples / (double)Math.Max(1, clip.frequency) / sound.Pitch;
                actionVariants[selected] = (variant + 1) % actionClips[selected].Length;
                nextActionAt[selected] = now + sound.Interval;
                LastActionCue = (HabitatActionCue)selected;
                LastActionClipName = clip.name;
                ActionPlayCount++;
            }
            return requested;
        }

        int FindActionVoice(double now, int priority)
        {
            int oldest = -1;
            for (int i = 0; i < ActionVoiceCount; i++)
            {
                if (now >= actionVoiceEnds[i]) return i;
                // A new footstep must not cut off a reward or another important action.
                if (actionVoicePriorities[i] > priority) continue;
                if (oldest < 0 || actionVoicePriorities[i] < actionVoicePriorities[oldest] ||
                    (actionVoicePriorities[i] == actionVoicePriorities[oldest] && actionVoiceStarts[i] < actionVoiceStarts[oldest]))
                    oldest = i;
            }
            return oldest;
        }

        void ApplyActionVolumes()
        {
            for (int i = 0; i < ActionVoiceCount; i++)
                if (actionSources[i]) actionSources[i].volume = muted ? 0f : effectsVolume * actionVoiceGains[i];
        }

        void StopActions()
        {
            for (int i = 0; i < ActionVoiceCount; i++)
                if (actionSources[i]) actionSources[i].Stop();
            Array.Clear(queuedActions, 0, queuedActions.Length);
            Array.Clear(nextActionAt, 0, nextActionAt.Length);
            Array.Clear(actionVoiceEnds, 0, actionVoiceEnds.Length);
        }
    }
}
