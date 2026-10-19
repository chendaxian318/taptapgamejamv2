#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HabitatIsland.Editor
{
    // Real-frame presentation checks; never included in an exported player.
    public sealed class HabitatTravelerChecks : MonoBehaviour
    {
        public static string Status = "Not run";
        const string Source = "Assets/HabitatIsland/Art/MeshyTraveler/MeshyTraveler.glb";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly Vector2Int[] CrateRoute = {
            new Vector2Int(0,1), new Vector2Int(1,0), new Vector2Int(0,1),
            new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(0,1), new Vector2Int(1,0)
        };
        readonly StringBuilder report = new StringBuilder();
        HabitatGame game;
        GameObject isolatedRoot;
        int count;

        public static string Begin()
        {
            if (!Application.isPlaying || !HabitatGame.Instance)
                throw new InvalidOperationException("Enter Play in the Habitat Island scene first");
            if (FindAnyObjectByType<HabitatTravelerChecks>())
                throw new InvalidOperationException("Traveler checks already running");
            var runner = HabitatGame.Instance.gameObject.AddComponent<HabitatTravelerChecks>();
            runner.StartCoroutine(runner.Guarded());
            return "Real-frame traveler checks started";
        }

        void Check(bool pass, string label)
        {
            count++;
            if (!pass) throw new InvalidOperationException(label);
            report.AppendLine("PASS " + count + ": " + label);
        }

        float ClipWeight(Animator animator, string name)
        {
            float weight = 0f;
            foreach (AnimatorClipInfo info in animator.GetCurrentAnimatorClipInfo(0))
                if (info.clip && info.clip.name == name) weight += info.weight;
            return weight;
        }

        Bounds WorldSkinBounds(HabitatPlayerVisual visual)
        {
            bool found = false;
            var bounds = new Bounds();
            var mesh = new Mesh();
            try
            {
                foreach (SkinnedMeshRenderer skin in visual.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.BakeMesh(mesh, true);
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        Vector3 point = skin.transform.TransformPoint(vertex);
                        if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                        else bounds.Encapsulate(point);
                    }
                    mesh.Clear();
                }
            }
            finally { DestroyImmediate(mesh); }
            if (!found) throw new InvalidOperationException("No baked traveler vertices");
            return bounds;
        }

        IEnumerator MoveOne(int dx, int dz, bool climb, bool inspectAnimation = false)
        {
            int fromX = game.State.playerX, fromZ = game.State.playerZ;
            game.Move(dx, dz, climb);
            Check(game.State.playerX == fromX + dx && game.State.playerZ == fromZ + dz,
                "legal controller step (" + fromX + "," + fromZ + ") -> (" + game.State.playerX + "," + game.State.playerZ + ")");
            float until = Time.realtimeSinceStartup + .34f, peakSpeed = 0f, peakWeight = 0f;
            do
            {
                yield return null;
                HabitatPlayerVisual visual = game.View.PlayerVisual;
                peakSpeed = Mathf.Max(peakSpeed, visual.MoveSpeed);
                peakWeight = Mathf.Max(peakWeight, ClipWeight(visual.Animator, "Walking") + ClipWeight(visual.Animator, "Running"));
            } while (Time.realtimeSinceStartup < until);
            if (inspectAnimation)
            {
                Check(peakSpeed > .2f, "real horizontal step drives MoveSpeed above idle (peak " + peakSpeed.ToString("F3") + ")");
                Check(peakWeight > .05f, "Walking/Running receives live animation weight (peak " + peakWeight.ToString("F3") + ")");
            }
        }

        IEnumerator WalkToCrate()
        {
            foreach (Vector2Int step in CrateRoute) yield return MoveOne(step.x, step.y, true);
            Check(game.State.playerX == 6 && game.State.playerZ == 14, "unchanged authored route reaches tide-crate approach");
        }

        void CheckFootHeight(string label)
        {
            HabitatPlayerVisual visual = game.View.PlayerVisual;
            float expected = Mathf.Max(HabitatRules.Height(game.State, game.State.playerX, game.State.playerZ) * .7f + .055f,
                game.State.waterLevel * .7f + .05f);
            Check(Mathf.Abs(visual.transform.position.y - expected) < .015f, label + " visual root follows logical standing height");
            float feet = WorldSkinBounds(visual).min.y;
            Check(Mathf.Abs(feet - expected) < .10f, label + " animated feet remain at the standing surface (offset " + (feet - expected).ToString("F3") + ")");
        }

        IEnumerator Run()
        {
            game = HabitatGame.Instance;
            game.LoadLevel(HabitatFigJamLevel.Index);
            game.paused = true; game.tool = 0; game.View.FocusPlayer();
            yield return null;
            var visual = game.View.PlayerVisual;
            Check(game.State.levelIndex == 4 && game.State.playerX == 3 && game.State.playerZ == 10,
                "formal level 4 starts at original (3,10)");
            Check(visual && visual.IsModelActive && visual.Animator && visual.Animator.runtimeAnimatorController,
                "imported traveler model and animator are active");
            var skins = visual.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Check(skins.Length == 1 && skins[0].sharedMesh && skins[0].bones.Length > 0, "exactly one intact skinned character renderer");
            Transform billboard = (Transform)typeof(HabitatView).GetField("playerBillboard", Flags).GetValue(game.View);
            Check(billboard && !billboard.gameObject.activeInHierarchy, "old Keeper artwork billboard is hidden");
            Check(!visual.Animator.applyRootMotion && visual.Animator.updateMode == AnimatorUpdateMode.UnscaledTime
                && visual.Animator.cullingMode == AnimatorCullingMode.AlwaysAnimate, "root motion disabled; animation uses unscaled time and remains active offscreen");
            Check(skins[0].updateWhenOffscreen, "skinned renderer remains updated after camera pans away");

            var clipNames = new HashSet<string>();
            int clips = 0;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(Source))
                if (asset is AnimationClip clip) { clips++; clipNames.Add(clip.name); }
            Check(clips == 8 && clipNames.Count == 8 && clipNames.Contains("Idle_9") && clipNames.Contains("Walking") && clipNames.Contains("Running"),
                "all eight original GLB animation clips remain imported");
            bool materialsOkay = skins[0].sharedMaterials.Length > 0;
            foreach (Material material in skins[0].sharedMaterials)
            {
                materialsOkay &= material && material.shader && material.HasProperty("_Metallic")
                    && material.GetFloat("_Metallic") < .001f && !material.IsKeywordEnabled("_EMISSION");
                if (material && material.HasProperty("_EmissionColor"))
                { Color emission = material.GetColor("_EmissionColor"); materialsOkay &= emission.r + emission.g + emission.b < .001f; }
            }
            Check(materialsOkay, "character material is nonmetallic and has no duplicated emission");
            Bounds initialBounds = WorldSkinBounds(visual);
            Check(visual.CarryAnchor && visual.CarryAnchor.position.y > initialBounds.max.y + .05f,
                "carry anchor is above the animated character silhouette");

            float normalized = visual.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Transform sampleBone = skins[0].bones[0];
            Quaternion boneBefore = sampleBone.localRotation;
            yield return new WaitForSecondsRealtime(.65f);
            Check(ClipWeight(visual.Animator, "Idle_9") > .95f && visual.MoveSpeed < .025f, "stationary traveler gives Idle_9 full live weight");
            Check(visual.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime > normalized + .02f
                || Quaternion.Angle(sampleBone.localRotation, boneBefore) > .1f, "idle animation advances over real rendered frames");

            yield return MoveOne(0, 1, true, true);
            yield return MoveOne(1, 0, true, true);
            yield return new WaitForSecondsRealtime(1f);
            Check(visual.MoveSpeed < .025f && ClipWeight(visual.Animator, "Idle_9") > .95f, "stopping for one second returns to Idle_9");
            Vector3 stopped = visual.transform.position;
            foreach (Vector2Int direction in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
            {
                game.State.facingX = direction.x; game.State.facingZ = direction.y;
                yield return new WaitForSecondsRealtime(.5f);
                Vector3 expected = new Vector3(direction.x, 0, direction.y);
                Check(Vector3.Dot(visual.ModelRoot.parent.forward, expected) > .999f,
                    "world-facing rotation reaches (" + direction.x + "," + direction.y + ")");
                Check(Vector3.Distance(visual.transform.position, stopped) < .003f && visual.MoveSpeed < .025f
                    && ClipWeight(visual.Animator, "Idle_9") > .95f, "turning in place preserves position and idle animation");
            }

            game.LoadLevel(HabitatFigJamLevel.Index);
            game.LoadLevel(HabitatFigJamLevel.Index);
            yield return null;
            int instances = 0;
            foreach (Transform node in game.View.GetComponentsInChildren<Transform>(true))
                if (node.name == "Meshy traveler") instances++;
            visual = game.View.PlayerVisual;
            Check(instances == 1 && game.View.GetComponentsInChildren<HabitatPlayerVisual>(true).Length == 1,
                "two consecutive level reloads retain exactly one traveler instance");
            Check(game.State.playerX == 3 && game.State.playerZ == 10
                && Mathf.Abs(visual.transform.position.x - 3f) < .001f && Mathf.Abs(visual.transform.position.z - 10f) < .001f,
                "reloading snaps traveler back to the original spawn");

            yield return WalkToCrate();
            yield return MoveOne(1, 0, true);
            Check(HabitatRules.TileAt(game.State, 7, 14).prop == PropKind.Crate && !game.State.tideActive,
                "climbing stands on original crate without pushing it");
            yield return new WaitForSecondsRealtime(1f);
            CheckFootHeight("crate climb");

            game.LoadLevel(HabitatFigJamLevel.Index);
            yield return null;
            yield return WalkToCrate();
            yield return MoveOne(1, 0, false);
            Check(game.State.tideActive && game.State.waterLevel == 1
                && HabitatRules.TileAt(game.State, 8, 14).prop == PropKind.Crate
                && HabitatRules.TileAt(game.State, 8, 14).coveredProp == PropKind.Tide, "original push moves crate onto (8,14) tide plate");
            game.tool = 0; game.Select(8, 14); game.ApplyTool();
            yield return new WaitForSecondsRealtime(1f);
            Check(game.State.carryingProp == PropKind.Crate && !game.State.tideActive && game.State.waterLevel == 0,
                "normal interaction picks up the tide crate and releases the tide");
            var carried = (Transform)typeof(HabitatView).GetField("carriedVisual", Flags).GetValue(game.View);
            Check(carried && carried.childCount > 0 && Vector3.Distance(carried.position, game.View.PlayerVisual.CarryAnchor.position) < .001f,
                "carried crate uses the same 3D carry-anchor position");
            CheckFootHeight("released tide");

            // The pure vertical test is isolated: it never changes live state or grid height.
            isolatedRoot = new GameObject("Traveler vertical-motion fixture");
            isolatedRoot.transform.position = new Vector3(-100, 0, -100);
            var isolated = isolatedRoot.AddComponent<HabitatPlayerVisual>();
            Check(isolated.Initialize(), "isolated vertical-motion fixture loads the real prefab");
            isolated.ResetVisual(Vector3.forward);
            for (int i = 0; i < 14; i++)
            {
                isolatedRoot.transform.position += Vector3.up * .05f;
                isolated.Render(1f / 60f, Vector3.forward);
                yield return null;
            }
            Check(isolated.MoveSpeed == 0f && ClipWeight(isolated.Animator, "Idle_9") > .95f,
                "pure tide-height movement never starts Walking/Running");
            Destroy(isolatedRoot); isolatedRoot = null;
        }

        IEnumerator Guarded()
        {
            Status = "Running";
            report.AppendLine("UTC: " + DateTimeOffset.UtcNow.ToString("O"));
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            Exception failure = null;
            while (stack.Count > 0)
            {
                object current = null; bool more = false;
                try { more = stack.Peek().MoveNext(); if (more) current = stack.Peek().Current; }
                catch (Exception error) { failure = error; break; }
                if (!more) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested); else yield return current;
            }
            if (failure != null) report.AppendLine("FAIL: " + failure);
            try
            {
                if (isolatedRoot) Destroy(isolatedRoot);
                if (game)
                {
                    game.LoadLevel(HabitatFigJamLevel.Index);
                    game.paused = true;
                    game.State.facingX = 0; game.State.facingZ = -1;
                    game.View.PlayerVisual.ResetVisual(Vector3.back);
                    game.View.FocusPlayer(); game.Hud.ClosePanels();
                    game.View.Render(game.State, game.tuning, game.overlays); game.Hud.Refresh();
                }
                report.AppendLine("Cleanup: formal level 4 reset; facing -Z; local player camera; all panels closed.");
            }
            catch (Exception error)
            {
                report.AppendLine("Cleanup failed: " + error);
                if (failure == null) failure = error;
            }
            Status = (failure == null ? "PASS: " : "FAIL: ") + count + " real-frame traveler checks"
                + (failure == null ? "" : "; " + failure.Message);
            report.Insert(0, Status + "\n");
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/meshy-traveler-checks.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report.ToString());
            Debug.Log("[HabitatIsland] " + Status + "; report: " + path);
            Destroy(this);
        }
    }
}
#endif
