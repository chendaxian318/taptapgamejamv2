using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HabitatIsland
{
    public interface IHabitatDockTimelineTarget
    {
        void EvaluateTimelineShot(HabitatDockBeatClip shot, float progress);
        void GatherTimelineProperties(IPropertyCollector collector);
    }

    [TrackColor(.29f, .60f, .64f)]
    [TrackClipType(typeof(HabitatDockBeatClip))]
    [TrackBindingType(typeof(MonoBehaviour))]
    public sealed class HabitatDockBeatTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
            => ScriptPlayable<HabitatDockBeatMixer>.Create(graph, inputCount);

        public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            var target = director.GetGenericBinding(this) as IHabitatDockTimelineTarget;
            if (target != null) target.GatherTimelineProperties(driver);
            base.GatherProperties(director, driver);
        }
    }

    public sealed class HabitatDockBeatMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var target = playerData as IHabitatDockTimelineTarget;
            if (target == null) return;
            int best = -1;
            float weight = 0f;
            for (int i = 0; i < playable.GetInputCount(); i++)
                if (playable.GetInputWeight(i) > weight)
                { best = i; weight = playable.GetInputWeight(i); }
            if (best < 0) return;
            var input = (ScriptPlayable<HabitatDockBeatBehaviour>)playable.GetInput(best);
            var shot = input.GetBehaviour().shot;
            if (shot) target.EvaluateTimelineShot(shot,
                (float)(input.GetTime() / System.Math.Max(.0001, input.GetDuration())));
        }
    }
}
