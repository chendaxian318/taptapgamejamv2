using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HabitatIsland
{
    /// <summary>Explicitly ticked equipment presentation. Never changes gameplay state.</summary>
    [DisallowMultipleComponent]
    public sealed class HabitatEquipmentAnimation : MonoBehaviour
    {
        [SerializeField] Animator targetAnimator;
        [SerializeField] AnimationClip[] clips = Array.Empty<AnimationClip>();
        [NonSerialized] PlayableGraph graph;
        [NonSerialized] AnimationPlayableOutput output;
        [NonSerialized] AnimationClipPlayable playable;
        [NonSerialized] AnimationClip currentClip, graphClip;
        [NonSerialized] float timeSeconds, playbackSpeed = 1f;
        [NonSerialized] bool looping = true, advancing;

        public Animator Animator => targetAnimator;
        public IReadOnlyList<AnimationClip> Clips => clips;
        public string CurrentClipName => currentClip ? currentClip.name : "";
        public float TimeSeconds => timeSeconds;
        public float Duration => currentClip ? currentClip.length : 0f;
        public bool Finished => currentClip && !looping && timeSeconds >= Duration;
        public bool IsPlaying => currentClip && advancing && !Finished;
        public bool IsLooping => looping;
        public float PlaybackSpeed => playbackSpeed;
        public bool IsConfigured
        {
            get
            {
                if (!targetAnimator || clips == null) return false;
                foreach (AnimationClip clip in clips) if (clip) return true;
                return false;
            }
        }

        public void Configure(Animator animator, AnimationClip[] sourceClips)
        {
            ReleaseGraph();
            targetAnimator = animator;
            clips = sourceClips == null ? Array.Empty<AnimationClip>() : (AnimationClip[])sourceClips.Clone();
            currentClip = null; timeSeconds = 0f; playbackSpeed = 1f; looping = true; advancing = false;
        }

        public bool HasClip(string name) => FindClip(name);

        /// <summary>Re-selecting the current clip keeps its time unless restart is explicit.</summary>
        public bool Play(string name, bool loop = true, float speed = 1f, bool restart = false)
        {
            AnimationClip clip = FindClip(name);
            if (!targetAnimator || !clip || !float.IsFinite(speed) || speed < 0f) return false;
            if (currentClip != clip || restart) timeSeconds = 0f;
            currentClip = clip; looping = loop; playbackSpeed = speed; advancing = true;
            if (looping && Duration > 0f) timeSeconds = (float)((double)timeSeconds % Duration);
            ApplyPose();
            return true;
        }

        /// <summary>Sample an exact clamped time and hold it until Play is called again.</summary>
        public bool Sample(string name, float seconds)
        {
            AnimationClip clip = FindClip(name);
            if (!targetAnimator || !clip || !float.IsFinite(seconds)) return false;
            currentClip = clip; timeSeconds = Mathf.Clamp(seconds, 0f, clip.length);
            looping = false; advancing = false;
            ApplyPose();
            return true;
        }

        // Intentionally no Update: the owning presentation decides when to advance.
        public void Tick(float dt)
        {
            if (!currentClip || !float.IsFinite(dt) || dt < 0f) return;
            if (advancing)
            {
                double next = timeSeconds + (double)dt * playbackSpeed;
                timeSeconds = Duration <= 0f ? 0f : looping ? (float)(next % Duration) : (float)Math.Min(next, Duration);
            }
            ApplyPose();
        }

        AnimationClip FindClip(string name)
        {
            if (string.IsNullOrEmpty(name) || clips == null) return null;
            foreach (AnimationClip clip in clips)
                if (clip && string.Equals(clip.name, name, StringComparison.Ordinal)) return clip;
            return null;
        }

        void ApplyPose()
        {
            // Inactive equipment retains its selected time without keeping a graph alive.
            if (!isActiveAndEnabled || !targetAnimator || !currentClip) return;
            if (!graph.IsValid())
            {
                targetAnimator.runtimeAnimatorController = null;
                targetAnimator.applyRootMotion = false;
                targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                targetAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
                graph = PlayableGraph.Create("Habitat equipment: " + name);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                output = AnimationPlayableOutput.Create(graph, "Equipment pose", targetAnimator);
                graph.Play();
            }
            if (graphClip != currentClip || !playable.IsValid())
            {
                // A single connected playable prevents old clips from accumulating or blending.
                if (playable.IsValid()) graph.DestroyPlayable(playable);
                playable = AnimationClipPlayable.Create(graph, currentClip);
                playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false); playable.SetSpeed(0d);
                output.SetSourcePlayable(playable); graphClip = currentClip;
            }
            playable.SetTime(timeSeconds);
            graph.Evaluate(0f);
        }

        void OnEnable() { ApplyPose(); }
        void OnDisable() { ReleaseGraph(); }
        void OnDestroy() { ReleaseGraph(); }
        void ReleaseGraph()
        {
            if (graph.IsValid()) graph.Destroy();
            graph = default; output = default; playable = default; graphClip = null;
        }
    }
}
