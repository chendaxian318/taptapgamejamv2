using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HabitatIsland
{
    public enum HabitatDockBeat { BlueSky, ReturnToDock, StowNet, WalkToShop, MorningWindow }

    /// <summary>Editable shot data. Timing belongs to Timeline; this asset only describes a shot.</summary>
    [Serializable]
    public sealed class HabitatDockBeatClip : PlayableAsset, ITimelineClipAsset
    {
        public HabitatDockBeat beat;
        [TextArea(2, 4)] public string caption;
        public Vector3 actorStart;
        public Vector3 actorEnd;
        public bool useWaypoint;
        public Vector3 waypoint;
        [Range(0.05f, 0.95f)] public float waypointTime = .4f;
        [Tooltip("Optional route through the imported harbor; includes start and end. Empty preserves the original waypoint mode.")]
        public Vector3[] pathPoints;
        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<HabitatDockBeatBehaviour>.Create(graph);
            playable.GetBehaviour().shot = this;
            return playable;
        }

        public Vector3 SampleActor(float progress)
        {
            float t = Mathf.Clamp01(progress);
            if (pathPoints != null && pathPoints.Length > 1)
            {
                float total = 0;
                for (int i = 1; i < pathPoints.Length; i++) total += Vector3.Distance(pathPoints[i - 1], pathPoints[i]);
                float remaining = total * t;
                for (int i = 1; i < pathPoints.Length; i++)
                {
                    float length = Vector3.Distance(pathPoints[i - 1], pathPoints[i]);
                    if (remaining <= length && length > .00001f)
                        return Vector3.Lerp(pathPoints[i - 1], pathPoints[i], remaining / length);
                    remaining -= length;
                }
                return pathPoints[pathPoints.Length - 1];
            }
            if (!useWaypoint) return Vector3.Lerp(actorStart, actorEnd, t);
            float split = Mathf.Clamp(waypointTime, .05f, .95f);
            return t < split ? Vector3.Lerp(actorStart, waypoint, t / split)
                : Vector3.Lerp(waypoint, actorEnd, (t - split) / (1f - split));
        }
    }

    public sealed class HabitatDockBeatBehaviour : PlayableBehaviour
    {
        public HabitatDockBeatClip shot;
    }
}
