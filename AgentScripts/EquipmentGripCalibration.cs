using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HabitatIsland;
using UnityEngine;

// Execute Run() through the Unity C# API in Play, after the equipment checks.
// All 54 samples are synchronous on an inactive temporary clone. No asset, State,
// original bone, tool, animator, input, camera or simulation setting is changed.
public static class EquipmentGripCalibration
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    struct Pose
    {
        public Transform t; public Vector3 position, scale; public Quaternion rotation;
        public Pose(Transform node) { t = node; position = t.localPosition; rotation = t.localRotation; scale = t.localScale; }
        public void Restore() { t.localPosition = position; t.localRotation = rotation; t.localScale = scale; }
    }
    static Transform Node(Transform root, string name, bool optional = false)
    {
        var matches = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray();
        if (matches.Length == 1) return matches[0];
        if (optional && matches.Length == 0) return null;
        throw new InvalidOperationException("Expected one " + name + " below " + root.name + "; found " + matches.Length + ".");
    }
    // MeshyTraveler has Hand bones but no finger bones. Match the production
    // PalmPosition fallback exactly instead of inventing a different palm anchor.
    static Vector3 Palm(Transform hand, Transform finger)
        => finger ? Vector3.Lerp(hand.position, finger.position, .65f) : hand.position;
    static string PathBelow(Transform node, Transform root)
    {
        var parts = new List<string>();
        for (; node != root; node = node.parent)
        { if (!node) throw new InvalidOperationException("Animator is outside the player hierarchy."); parts.Insert(0, node.name); }
        return string.Join("/", parts);
    }

    public static string Run()
    {
        var game = HabitatGame.Instance;
        if (!Application.isPlaying || !game || game.Busy || game.StoryPlaying || game.Fishing.IsOpen)
            throw new InvalidOperationException("Run only in idle Play, outside a story/fishing/movement transaction.");
        var original = game.View.PlayerVisual;
        if (!original || !original.Animator) throw new InvalidOperationException("The real player animator is required.");
        Node(original.transform, "Held shovel"); // Existing real-instance sizing, produced by the runtime checks.
        var state = game.State; string stateJson = JsonUtility.ToJson(state);
        var originalPoses = original.GetComponentsInChildren<Transform>(true).Select(t => new Pose(t)).ToArray();
        var clips = original.Animator.GetCurrentAnimatorClipInfo(0);
        if (clips.Length == 0) throw new InvalidOperationException("Player has no current locomotion clip.");
        AnimationClip clip = clips.OrderByDescending(c => c.weight).First().clip;
        float clipTime = Mathf.Repeat(original.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f) * clip.length;
        var report = new StringBuilder("Shovel Dig grip calibration: 6 candidates x 9 progress samples.\n");
        GameObject clone = null;
        try
        {
            clone = UnityEngine.Object.Instantiate(original.gameObject);
            clone.name = "Equipment grip calibration temporary"; clone.hideFlags = HideFlags.HideAndDontSave;
            clone.SetActive(false);
            var visual = clone.GetComponent<HabitatPlayerVisual>();
            Transform facing = Node(clone.transform, "Traveler facing");
            typeof(HabitatPlayerVisual).GetField("facingRoot", Hidden).SetValue(visual, facing);
            Transform ra = Node(clone.transform, "mixamorig:RightArm"), rf = Node(clone.transform, "mixamorig:RightForeArm"),
                rh = Node(clone.transform, "mixamorig:RightHand"), rp = Node(clone.transform, "mixamorig:RightHandMiddle1", true),
                la = Node(clone.transform, "mixamorig:LeftArm"), lf = Node(clone.transform, "mixamorig:LeftForeArm"),
                lh = Node(clone.transform, "mixamorig:LeftHand"), lp = Node(clone.transform, "mixamorig:LeftHandMiddle1", true),
                socket = Node(clone.transform, "Traveler right palm tool socket"), shovel = Node(clone.transform, "Held shovel"),
                primary = Node(shovel, "Grip_Primary"), secondary = Node(shovel, "Grip_Secondary");
            string animatorPath = PathBelow(original.Animator.transform, original.transform);
            GameObject character = string.IsNullOrEmpty(animatorPath) ? clone : clone.transform.Find(animatorPath).gameObject;
            var baseline = clone.GetComponentsInChildren<Transform>(true).Select(t => new Pose(t)).ToArray();
            var poseArm = typeof(HabitatPlayerVisual).GetMethod("PoseArm", Hidden);
            var poseWorld = typeof(HabitatPlayerVisual).GetMethod("PoseArmWorld", Hidden);
            var candidates = new[] { new Vector3(.06f, .07f, 22f), new Vector3(.02f, .035f, 18f),
                new Vector3(.015f, .035f, 18f), new Vector3(.02f, .03f, 18f),
                new Vector3(.015f, .03f, 18f), new Vector3(.015f, .03f, 16f) };
            float best = float.PositiveInfinity; int bestIndex = -1;
            report.AppendLine("Character clip=" + clip.name + ", initial clip time=" + clipTime.ToString("F5"));
            report.AppendLine("Grip anchors match production: right=" + (rp ? rp.name + " palm interpolation" : rh.name + " fallback")
                + ", left=" + (lp ? lp.name + " palm interpolation" : lh.name + " fallback"));
            for (int c = 0; c < candidates.Length; c++)
            {
                Vector3 parameters = candidates[c]; float worst = 0f, worstPrimary = 0f, worstProgress = 0f;
                for (int step = 0; step <= 8; step++)
                {
                    foreach (Pose pose in baseline) pose.Restore();
                    float progress = step / 8f, dig = Mathf.Sin(progress * Mathf.PI);
                    // Recreate the actual underlying character animation before applying the tool IK.
                    clip.SampleAnimation(character, Mathf.Repeat(clipTime + progress * .72f, clip.length));
                    float side = facing.InverseTransformPoint(ra.position).x >= 0f ? 1f : -1f;
                    poseArm.Invoke(visual, new object[] { ra, rf, rh,
                        new Vector3(side * .08f, .82f - dig * parameters.x, .10f + dig * parameters.y),
                        new Vector3(side * .42f, .48f, .05f), 1f });
                    Quaternion orientation = facing.rotation * Quaternion.Euler(-24f - dig * parameters.z, 0f, -side * 48f);
                    socket.SetPositionAndRotation(Palm(rh, rp), orientation);
                    socket.position += Palm(rh, rp) - primary.position;
                    Vector3 leftGrip = secondary.position;
                    for (int iteration = 0; iteration < 2; iteration++)
                        poseWorld.Invoke(visual, new object[] { la, lf, lh, leftGrip - (Palm(lh, lp) - lh.position),
                            facing.TransformPoint(new Vector3(-side * .40f, .45f, .12f)), 1f });
                    float error = Vector3.Distance(secondary.position, Palm(lh, lp));
                    if (error > worst) { worst = error; worstProgress = progress; }
                    worstPrimary = Mathf.Max(worstPrimary, Vector3.Distance(primary.position, Palm(rh, rp)));
                }
                report.AppendLine("C" + c + ": yDrop=" + parameters.x + ", zReach=" + parameters.y + ", pitch=" + parameters.z
                    + "; maxSecondary=" + worst.ToString("F8") + "m @ progress " + worstProgress.ToString("F3")
                    + "; maxPrimary=" + worstPrimary.ToString("F8") + "m");
                if (worst < best) { best = worst; bestIndex = c; }
            }
            Vector3 winner = candidates[bestIndex];
            report.AppendLine("BEST C" + bestIndex + ": right.y=.82-dig*" + winner.x + "; right.z=.10+dig*" + winner.y
                + "; pitch=-24-dig*" + winner.z + "; maximum secondary error=" + best.ToString("F8") + "m.");
        }
        finally
        {
            if (clone) UnityEngine.Object.DestroyImmediate(clone);
            bool unchanged = ReferenceEquals(game.State, state) && JsonUtility.ToJson(state) == stateJson
                && originalPoses.All(p => p.t && p.t.localPosition == p.position && p.t.localRotation == p.rotation && p.t.localScale == p.scale);
            report.AppendLine((unchanged ? "PASS" : "FAIL") + " original State, bones and tool transforms unchanged; temporary clone destroyed.");
            if (!unchanged) throw new InvalidOperationException(report.ToString());
        }
        return report.ToString();
    }
}
