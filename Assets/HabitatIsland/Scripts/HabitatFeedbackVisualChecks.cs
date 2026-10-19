#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace HabitatIsland.Editor
{
    // Isolated presentation probe. It does not move the real player or alter a level.
    public sealed class HabitatFeedbackVisualChecks : MonoBehaviour
    {
        public static string Status = "Not run";
        readonly StringBuilder report = new StringBuilder();
        HabitatPlayerVisual visual;
        int count;

        public static string Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play before visual checks");
            if (FindAnyObjectByType<HabitatFeedbackVisualChecks>()) throw new InvalidOperationException("Visual checks already running");
            var root = new GameObject("Feedback visual verification");
            root.transform.position = new Vector3(-1000f, -1000f, -1000f);
            var runner = root.AddComponent<HabitatFeedbackVisualChecks>();
            Status = "Running";
            runner.StartCoroutine(runner.Guarded());
            return Status;
        }

        void Check(bool pass, string label)
        {
            count++;
            if (!pass) throw new InvalidOperationException(label);
            report.AppendLine("PASS " + count + ": " + label);
        }

        IEnumerator Phase(HabitatPlayerMotion motion, float seconds, float speed)
        {
            visual.SetMotion(motion);
            float until = Time.realtimeSinceStartup + seconds;
            do
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, .08f);
                visual.SetMotion(motion);
                transform.position += Vector3.forward * speed * dt;
                visual.Render(dt, Vector3.forward);
                yield return new WaitForEndOfFrame();
            } while (Time.realtimeSinceStartup < until);
        }

        Vector3 HandPosition(string name)
        {
            Transform hand = visual.ModelRoot.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
            return transform.InverseTransformPoint(hand.position);
        }

        IEnumerator Run()
        {
            visual = gameObject.AddComponent<HabitatPlayerVisual>();
            Check(visual.Initialize(), "player prefab initializes");
            Check(Mathf.Abs(visual.ModelRoot.localScale.x - .75f) < .001f, "character is 75 percent of original display size");
            Check(Mathf.Abs(visual.CarryAnchor.localPosition.y - 1.35f) < .001f, "carry anchor follows smaller character");
            var skins = visual.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Check(skins.Length == 1 && Enumerable.Range(0, skins[0].sharedMesh.subMeshCount).Sum(i => (long)skins[0].sharedMesh.GetIndexCount(i)) / 3 == 9985,
                "empty-hands mesh has 9985 faces including glove cap; original tool faces absent");
            Check(skins[0].bones.Length > 0 && !visual.Animator.applyRootMotion, "original skinning and authoritative movement preserved");
            if (HabitatGame.Instance && HabitatGame.Instance.View)
            {
                var residents = (Transform)typeof(HabitatView).GetField("actorRoot",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(HabitatGame.Instance.View);
                // The View shares its GameObject with the HUD. Inspect only the
                // resident hierarchy, not the handbook's intentional UI bars.
                Transform[] sceneNodes = residents.GetComponentsInChildren<Transform>(true);
                Check(!sceneNodes.Any(t => t.name == "Progress track" || t.name == "Breeding progress" || t.name == "Habitat condition"),
                    "live map has no resident head bars or detached head badges");
            }
            yield return Phase(HabitatPlayerMotion.Walk, .45f, HabitatPlayerVisual.WalkSpeed);
            Check(Mathf.Abs(visual.MoveSpeed - 1.6f) < .08f, "2.5-cell travel uses Walking rather than Running blend");
            Vector3 walkingHand = HandPosition("mixamorig:RightHand");
            yield return Phase(HabitatPlayerMotion.Push, .45f, HabitatPlayerVisual.HandlingSpeed);
            Vector3 pushingHand = HandPosition("mixamorig:RightHand");
            Check(visual.HandlingPoseWeight > .95f && visual.TravelSpeed == 1.5f, "push has independent upper-body pose and lower travel speed");
            Check(Vector3.Distance(walkingHand, pushingHand) > .08f && pushingHand.z > .15f, "push visibly reaches both arms forward");
            yield return Phase(HabitatPlayerMotion.Pull, .3f, HabitatPlayerVisual.HandlingSpeed);
            Transform pose = visual.ModelRoot.parent;
            Check(Mathf.DeltaAngle(0f, pose.localEulerAngles.x) < -5f, "pull leans back instead of repeating forward push");
            visual.NotifyFall(.7f);
            yield return Phase(HabitatPlayerMotion.Fall, .25f, 0f);
            Check(visual.FallPoseWeight > .95f && visual.IsFalling, "fall extends arms into balance pose");
            visual.NotifyLanded();
            yield return Phase(HabitatPlayerMotion.Walk, .13f, 0f);
            Check(visual.IsLanding && pose.localScale.y < .95f, "landing has visible compression and recovery");
            yield return Phase(HabitatPlayerMotion.Walk, .5f, 0f);
            Check(visual.MoveSpeed < .025f && !visual.IsLanding && visual.HandlingPoseWeight < .01f && visual.FallPoseWeight < .01f,
                "stopping restores idle without accumulating procedural pose");
            Check(Vector3.Distance(pose.localScale, Vector3.one) < .001f, "landing returns to original display proportions");
        }

        IEnumerator Guarded()
        {
            IEnumerator sequence = Run();
            while (true)
            {
                bool more;
                object current = null;
                try { more = sequence.MoveNext(); if (more) current = sequence.Current; }
                catch (Exception error)
                {
                    Status = "FAILED: " + error.Message;
                    report.AppendLine(Status); Debug.LogError(Status); Finish(); yield break;
                }
                if (!more) break;
                yield return current;
            }
            Status = "PASS: " + count + " visual checks";
            report.AppendLine(Status); Debug.Log(Status); Finish();
        }

        void Finish()
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/feedback-visuals-20261015"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "runtime-checks.txt"), report.ToString());
            Destroy(gameObject);
        }
    }
}
#endif
