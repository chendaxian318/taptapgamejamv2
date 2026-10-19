using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HabitatIsland
{
    public enum HabitatTrapVisualState { Ready, Opening, Closing, Occupied, Preview }

    /// <summary>Presentation only. Tile/animal state owns capture; this component never changes it.</summary>
    [DisallowMultipleComponent]
    public sealed class HabitatTrapVisual : MonoBehaviour
    {
        public const string ResourcePath = "Models/AnimatedTrap/Trap";
        // The delivered 24 fps action is rebased from frame 1 to time zero by the asset helper.
        const float OpenStart = .5f, OpenEnd = 2f, CloseStart = 3f, CloseEnd = 4.5f;
        const float DoorDuration = 1.5f;
        [SerializeField] Animator targetAnimator;
        [SerializeField] Transform modelRoot;
        [SerializeField] AnimationClip doorClip, floatClip, demoClip;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable doorPlayable, floatPlayable, demoPlayable;
        bool initialized, occupied, preview;
        float openness, floatTime;

        public Animator Animator => targetAnimator;
        public Transform ModelRoot => modelRoot;
        public HabitatTrapVisualState CurrentState { get; private set; } = HabitatTrapVisualState.Ready;
        public float AnimationTime { get; private set; }
        public string CurrentClipName { get; private set; } = "";
        public bool IsTransitioning => CurrentState == HabitatTrapVisualState.Opening || CurrentState == HabitatTrapVisualState.Closing;
        public bool IsAnimating => IsTransitioning || CurrentState == HabitatTrapVisualState.Occupied;
        public bool HasCapturedShrimp => occupied;
        public bool IsConfigured => targetAnimator && modelRoot && doorClip && floatClip && demoClip;

        /// <summary>Asset preparation only; clips retain the supplied rig's transform paths.</summary>
        public void Configure(Animator animator, Transform rig, AnimationClip door, AnimationClip floats, AnimationClip demo)
        {
            ReleaseGraph();
            targetAnimator = animator; modelRoot = rig;
            doorClip = door; floatClip = floats; demoClip = demo;
            initialized = false;
        }

        /// <summary>Initial placement/load/undo/rebuild: align immediately without an invented capture event.</summary>
        public void ResetVisual(bool hasShrimp)
        {
            initialized = true; occupied = hasShrimp; preview = false;
            openness = hasShrimp ? 0f : 1f; floatTime = 0f;
            CurrentState = hasShrimp ? HabitatTrapVisualState.Occupied : HabitatTrapVisualState.Ready;
            ApplyPose();
        }

        /// <summary>Call once per frame. A carried shrimp is not an occupant of its previous tile.</summary>
        public void Render(bool hasShrimp, float unscaledDeltaTime)
        {
            if (!initialized || preview) { ResetVisual(hasShrimp); return; }
            if (hasShrimp != occupied)
            {
                occupied = hasShrimp;
                CurrentState = occupied ? HabitatTrapVisualState.Closing : HabitatTrapVisualState.Opening;
                floatTime = 0f;
            }
            float dt = float.IsFinite(unscaledDeltaTime) ? Mathf.Clamp(unscaledDeltaTime, 0f, .1f) : 0f;
            if (CurrentState == HabitatTrapVisualState.Closing)
            {
                openness = Mathf.MoveTowards(openness, 0f, dt / DoorDuration);
                if (openness <= 0f) CurrentState = HabitatTrapVisualState.Occupied;
            }
            else if (CurrentState == HabitatTrapVisualState.Opening)
            {
                openness = Mathf.MoveTowards(openness, 1f, dt / DoorDuration);
                if (openness >= 1f) CurrentState = HabitatTrapVisualState.Ready;
            }
            else if (CurrentState == HabitatTrapVisualState.Occupied && floatClip)
                floatTime = Mathf.Repeat(floatTime + dt, Mathf.Max(.001f, floatClip.length));
            ApplyPose();
        }

        /// <summary>Inspection only. The next normal Render/ResetVisual restores the actual tile state.</summary>
        public bool SamplePreview(string clipName, float normalizedTime)
        {
            if (!IsConfigured || !float.IsFinite(normalizedTime)) return false;
            int slot = clipName == doorClip.name ? 0 : clipName == floatClip.name ? 1 : clipName == demoClip.name ? 2 : -1;
            if (slot < 0) return false;
            AnimationClip clip = slot == 0 ? doorClip : slot == 1 ? floatClip : demoClip;
            preview = true; CurrentState = HabitatTrapVisualState.Preview;
            Sample(slot, Mathf.Clamp01(normalizedTime) * clip.length);
            return true;
        }

        void ApplyPose()
        {
            if (CurrentState == HabitatTrapVisualState.Occupied) Sample(1, floatTime);
            else if (CurrentState == HabitatTrapVisualState.Closing)
                Sample(0, Mathf.Lerp(CloseStart, CloseEnd, 1f - openness));
            else Sample(0, Mathf.Lerp(OpenStart, OpenEnd, openness));
        }

        void Sample(int slot, float seconds)
        {
            AnimationTime = seconds;
            AnimationClip clip = slot == 0 ? doorClip : slot == 1 ? floatClip : demoClip;
            CurrentClipName = clip ? clip.name : "";
            // Culled tiles keep logical presentation state without a live graph.
            // Reactivation samples the current pose instead of restarting capture.
            if (!isActiveAndEnabled || !IsConfigured) return;
            EnsureGraph();
            for (int i = 0; i < 3; i++) mixer.SetInputWeight(i, i == slot ? 1f : 0f);
            AnimationClipPlayable selected = slot == 0 ? doorPlayable : slot == 1 ? floatPlayable : demoPlayable;
            selected.SetTime(seconds);
            graph.Evaluate(0f);
        }

        void EnsureGraph()
        {
            if (graph.IsValid()) return;
            targetAnimator.runtimeAnimatorController = null;
            targetAnimator.applyRootMotion = false;
            targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            targetAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            graph = PlayableGraph.Create("Habitat trap visual");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 3);
            doorPlayable = CreateClip(doorClip, 0);
            floatPlayable = CreateClip(floatClip, 1);
            demoPlayable = CreateClip(demoClip, 2);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Trap pose", targetAnimator);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        AnimationClipPlayable CreateClip(AnimationClip clip, int slot)
        {
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false); playable.SetSpeed(0);
            graph.Connect(playable, 0, mixer, slot);
            return playable;
        }

        void OnDisable() { ReleaseGraph(); }
        void OnDestroy() { ReleaseGraph(); }
        void ReleaseGraph() { if (graph.IsValid()) graph.Destroy(); }
    }
}
