using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HabitatIsland.Editor
{
    /// <summary>Play-mode checks of rendered instances; does not save or rebuild assets.</summary>
    public static class HabitatPlanVisualChecks
    {
        static int count;
        static StringBuilder report;
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        static void Check(bool condition, string label)
        {
            count++;
            if (!condition) throw new InvalidOperationException("Plan visual check failed: " + label);
            report.AppendLine("PASS " + count + ": " + label);
        }

        static Transform FindActive(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name && child.gameObject.activeInHierarchy) return child;
            throw new InvalidOperationException("Missing active visual: " + name);
        }

        static Bounds RenderedBounds(Transform root)
        {
            bool found = false;
            Bounds bounds = default;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!found) throw new InvalidOperationException("No rendered bounds under " + root.name);
            return bounds;
        }

        static HabitatShrimpVisual FindAnimatedShrimp(HabitatView view)
        {
            foreach (HabitatShrimpVisual shrimp in view.GetComponentsInChildren<HabitatShrimpVisual>())
                if (shrimp.gameObject.activeInHierarchy && shrimp.IsReady) return shrimp;
            throw new InvalidOperationException("View did not create and initialize an animated shrimp; static fallback is not sufficient.");
        }

        static Bounds BakedPoseBounds(Transform model)
        {
            Bounds world = default; bool found = false;
            var mesh = new Mesh();
            try
            {
                foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    // Actual Unity/glTF measurement verified that true provides
                    // renderer-local output for this rig; false double-scales
                    // when passed through the transform hierarchy below.
                    skin.BakeMesh(mesh, true);
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        Vector3 point = skin.transform.TransformPoint(vertex);
                        if (!found) { world = new Bounds(point, Vector3.zero); found = true; }
                        else world.Encapsulate(point);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
            if (!found) throw new InvalidOperationException("Animated shrimp has no baked runtime pose.");
            return world;
        }

        static Bounds WorldBounds(Transform model, Bounds local)
        {
            Bounds world = new Bounds(model.TransformPoint(local.center), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = local.center + Vector3.Scale(local.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                world.Encapsulate(model.TransformPoint(point));
            }
            return world;
        }

        // The display wrappers are outside the Animator. Different clips are
        // allowed to change mesh bounds without being mistaken for a scale bug.
        static Vector3 DisplayScale(Transform model, GameObject prefab)
        {
            Vector3 scale = Vector3.Scale(model.localScale, model.parent.localScale);
            Vector3 source = prefab.transform.localScale;
            return new Vector3(scale.x / source.x, scale.y / source.y, scale.z / source.z);
        }

        static HabitatState Fixture(HabitatState original)
        {
            HabitatState state = HabitatRules.Clone(original);
            state.width = state.depth = 7;
            state.tiles = new Tile[49];
            for (int i = 0; i < state.tiles.Length; i++) state.tiles[i] = new Tile { height = 1 };
            HabitatRules.TileAt(state, 3, 4).prop = PropKind.ClimbBlock;
            state.animals.Clear();
            state.animals.Add(new Animal { id = 77001, x = 3, z = 3, species = Species.Shrimp });
            state.playerX = 3; state.playerZ = 2; state.facingX = 0; state.facingZ = 1;
            state.selectedX = state.selectedZ = 3;
            state.carryingAnimal = -1; state.carryingProp = PropKind.None;
            state.waterLevel = state.tideBaseLevel = 0; state.tideActive = false;
            state.authoredLevel = false; state.dockX = state.dockZ = 0; state.dockRotation = 2;
            return state;
        }

        static string HashFile(string path)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return Convert.ToBase64String(hash.ComputeHash(stream));
        }

        static Dictionary<string, string> SourceHashes()
        {
            var hashes = new Dictionary<string, string>();
            string[] paths = {
                "Assets/HabitatIsland/Art/MeshyShrimp/MeshyShrimp.glb",
                "Assets/HabitatIsland/Art/Legacy/MeshyShrimp/Shrimp.prefab",
                "Assets/HabitatIsland/Art/Legacy/MeshyShrimp/Shrimp0.mat",
                "Assets/HabitatIsland/Art/AnimatedShrimp/creature_animated.glb",
                "Assets/HabitatIsland/Resources/Models/AnimatedShrimp/Shrimp.prefab",
                "Assets/HabitatIsland/Resources/Models/AnimatedShrimp/Shrimp0.mat",
                "Assets/HabitatIsland/Resources/Models/AnimatedShrimp/Shrimp.controller",
                "Assets/HabitatIsland/Resources/Models/AnimatedShrimp/Idle.anim",
                "Assets/HabitatIsland/Resources/Models/AnimatedShrimp/Move.anim",
                "Assets/HabitatIsland/Resources/Models/AnimatedShrimp/Carried.anim",
                "Assets/HabitatIsland/Resources/Models/KayKit/sand_with_grass.fbx",
                "Assets/HabitatIsland/Resources/Models/KayKit/block_bits_texture.png"
            };
            string project = Path.GetDirectoryName(Application.dataPath);
            foreach (string relative in paths)
            {
                string path = Path.Combine(project, relative);
                hashes.Add(path, HashFile(path));
                hashes.Add(path + ".meta", HashFile(path + ".meta"));
            }
            return hashes;
        }

        static Dictionary<FieldInfo, object> CameraState(HabitatView view)
        {
            var snapshot = new Dictionary<FieldInfo, object>();
            string[] names = { "cameraYaw", "cameraSize", "cameraTarget", "cameraTargetSmooth",
                "overviewSize", "framingAspect", "nextVisibilityCheck", "followPlayer", "overviewCamera", "framingDirty", "mapBounds" };
            foreach (string name in names)
            {
                FieldInfo field = typeof(HabitatView).GetField(name, PrivateInstance);
                if (field == null) throw new InvalidOperationException("Camera snapshot field missing: " + name);
                snapshot.Add(field, field.GetValue(view));
            }
            return snapshot;
        }

        static void CheckClimbOpacity(HabitatGame game, HabitatOcclusionFade fade)
        {
            Check(fade && fade.enabled, "terrain occlusion component is available for the climb regression");
            HabitatState state = HabitatFigJamLevel.Create();
            // Use the actual authored block and spawn, without moving props or
            // changing the map to reproduce the initial-view transparency bug.
            const int blockX = 6, blockZ = 13;
            Check(HabitatRules.TileAt(state, blockX, blockZ).prop == PropKind.ClimbBlock,
                "regression uses the unchanged original climbing block at (6,13)");
            state.selectedX = state.selectedZ = -1;
            game.State = state;
            fade.SetEnabled(false);
            game.View.Initialize(state);
            game.View.Render(state, game.tuning, false);
            Transform block = FindActive(game.View.transform, "ClimbBlock");
            var originalMaterials = new Dictionary<Renderer, Material[]>();
            var originalColors = new Dictionary<Renderer, Color[]>();
            foreach (Renderer renderer in block.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                originalMaterials.Add(renderer, materials);
                var colors = new Color[materials.Length];
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties, slot);
                    colors[slot] = properties.GetColor("_BaseColor");
                }
                originalColors.Add(renderer, colors);
            }
            Check(originalMaterials.Count >= 4, "authored block includes the solid body and all three ladder steps");
            Camera camera = game.View.GameCamera;
            Check(Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.x, 48f)) < .1f
                && Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.y, -14f)) < .1f,
                "climb regression uses the actual default 48/-14 camera");
            fade.SetEnabled(true);
            for (int selected = 0; selected < 2; selected++)
            {
                state.selectedX = selected == 0 ? -1 : blockX; state.selectedZ = selected == 0 ? -1 : blockZ;
                game.View.Render(state, game.tuning, false);
                fade.Refresh(.5f);
                bool opaque = true;
                foreach (var entry in originalMaterials)
                {
                    Material[] current = entry.Key.sharedMaterials;
                    opaque &= current.Length == entry.Value.Length;
                    for (int slot = 0; slot < current.Length; slot++)
                    {
                        Material material = current[slot];
                        var properties = new MaterialPropertyBlock(); entry.Key.GetPropertyBlock(properties, slot);
                        opaque &= material == entry.Value[slot] && material.GetFloat("_Surface") < .5f
                            && material.GetColor("_BaseColor").a >= .999f
                            && properties.GetColor("_BaseColor") == originalColors[entry.Key][slot];
                    }
                }
                string label = selected == 0 ? "unselected" : "selected";
                Check(opaque && !fade.IsCellTopFaded(blockX, blockZ), label + " climb body, grass cap and ladder remain fully opaque");
                Bounds bounds = RenderedBounds(block);
                Vector3 screen = camera.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y - .01f, bounds.center.z));
                Check(fade.TryPick(camera, screen, false, out int x, out int z) && x == blockX && z == blockZ,
                    label + " climb top directly selects the original block without Alt");
                Transform ladder = FindActive(block, "Climb steps");
                screen = camera.WorldToScreenPoint(ladder.GetComponent<Renderer>().bounds.center);
                Check(fade.TryPick(camera, screen, false, out x, out z) && x == blockX && z == blockZ,
                    label + " visible ladder directly selects its opaque block without Alt");
            }
        }

        public static string Run()
        {
            HabitatGame game = HabitatGame.Instance;
            if (!Application.isPlaying || !game || !game.View || !game.View.GameCamera)
                throw new InvalidOperationException("Enter Play mode before running plan visual checks.");
            HabitatView view = game.View;
            HabitatState original = game.State;
            bool paused = game.paused;
            int undoCount = game.UndoCount;
            HabitatOcclusionFade fade = view.GetComponent<HabitatOcclusionFade>();
            bool fadeEnabled = fade && fade.FadeEnabled;
            Camera camera = view.GameCamera;
            Vector3 cameraPosition = camera.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;
            float cameraSize = camera.orthographicSize;
            var cameraState = CameraState(view);
            count = 0;
            report = new StringBuilder("Habitat plan visual runtime checks\nUTC: " + DateTimeOffset.UtcNow.ToString("O") + "\n");
            string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/plan-visual-checks.txt"));
            Exception failure = null;
            Dictionary<string, string> hashes = null;
            try
            {
                hashes = SourceHashes();
                GameObject prefab = Resources.Load<GameObject>(HabitatShrimpVisual.ResourcePath);
                Check(prefab, "prepared animated shrimp prefab is available");
                HabitatShrimpVisual prefabVisual = prefab.GetComponent<HabitatShrimpVisual>();
                Check(prefabVisual && prefabVisual.AnimationBounds.size.x > .01f && prefabVisual.AnimationBounds.size.y > .01f
                    && prefabVisual.AnimationBounds.size.z > .01f, "animated prefab carries sampled motion bounds");
                Material sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/HabitatIsland/Resources/Models/AnimatedShrimp/Shrimp0.mat");
                Check(sourceMaterial, "source shrimp material is available");
                string sourceMaterialJson = EditorJsonUtility.ToJson(sourceMaterial);
                float sourceMetallic = sourceMaterial.GetFloat("metallicFactor");

                game.paused = true;
                if (fade) fade.SetEnabled(false);
                game.State = Fixture(original);
                view.Initialize(game.State);
                view.Render(game.State, game.tuning, true);

                Vector3 angles = camera.transform.eulerAngles;
                float yaw = Mathf.DeltaAngle(0f, angles.y);
                Check(camera.orthographic && angles.x >= 46f && angles.x <= 50f && yaw >= -16f && yaw <= -12f,
                    "live default camera reveals the face with a modest oblique view: " + angles.ToString("F2"));
                view.RotateCamera(90f); view.Render(game.State, game.tuning, true);
                Check(Mathf.Abs(Mathf.DeltaAngle(angles.y, camera.transform.eulerAngles.y) + 90f) < .1f,
                    "live camera still rotates by one exact quarter turn");
                view.RotateCamera(-90f); view.Render(game.State, game.tuning, true);

                HabitatShrimpVisual shrimpVisual = FindAnimatedShrimp(view);
                Transform model = shrimpVisual.transform;
                Check(model.GetComponentInChildren<SkinnedMeshRenderer>() && shrimpVisual.Animator
                    && !shrimpVisual.Animator.applyRootMotion && shrimpVisual.Animator.updateMode == AnimatorUpdateMode.UnscaledTime,
                    "View uses the initialized skinned shrimp with unscaled animation and no root motion");
                Transform actor = model.parent.parent;
                Transform badge = actor.Find("Habitat condition");
                Transform shadow = actor.Find("Contact shadow");
                Bounds ground = BakedPoseBounds(model);
                Vector3 ratios = DisplayScale(model, prefab);
                Check(ratios.x >= 1.39f && ratios.x <= 1.51f && Mathf.Abs(ratios.x - ratios.y) < .01f
                    && Mathf.Abs(ratios.x - ratios.z) < .01f,
                    "ground shrimp display is uniformly enlarged from the animated prefab: " + ratios.ToString("F3"));
                Bounds motionEnvelope = WorldBounds(model, shrimpVisual.AnimationBounds);
                Bounds envelopeWithTolerance = motionEnvelope; envelopeWithTolerance.Expand(.035f);
                Check(envelopeWithTolerance.Contains(ground.min) && envelopeWithTolerance.Contains(ground.max),
                    "sampled animation metadata contains the actual baked pose without a second normalization scale");
                Check(badge && badge.gameObject.activeInHierarchy && badge.position.y > Mathf.Max(ground.max.y, motionEnvelope.max.y) + .02f,
                    "actual condition badge clears both the current pose and sampled motion envelope");
                Check(shadow && shadow.gameObject.activeInHierarchy, "ground shrimp retains its contact shadow");
                report.AppendLine("Measured ground bounds: " + ground.size.ToString("F4")
                    + "; motion envelope: " + motionEnvelope.size.ToString("F4")
                    + "; badge clearance: " + (badge.position.y - motionEnvelope.max.y).ToString("F4")
                    + "; contact shadow: " + RenderedBounds(shadow).size.ToString("F4"));

                int inspectedSlots = 0;
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                for (int slot = 0; slot < renderer.sharedMaterials.Length; slot++)
                {
                    Material material = renderer.sharedMaterials[slot];
                    if (!material || !material.HasProperty("metallicFactor")) continue;
                    var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties, slot);
                    float metallic = properties.GetFloat("metallicFactor");
                    Check(material == sourceMaterial && material.GetTexture("baseColorTexture")
                        && material.GetTexture("metallicRoughnessTexture") && metallic > 0f && metallic < sourceMetallic * .25f,
                        "shrimp uses original textured material with a lower per-renderer metallic multiplier");
                    inspectedSlots++;
                }
                Check(inspectedSlots > 0 && EditorJsonUtility.ToJson(sourceMaterial) == sourceMaterialJson,
                    "runtime shrimp treatment leaves the source material properties unchanged");

                Animal shrimp = game.State.animals[0];
                shrimp.carried = true; game.State.carryingAnimal = shrimp.id;
                view.Render(game.State, game.tuning, true);
                Bounds carried = BakedPoseBounds(model);
                Vector3 carriedScale = DisplayScale(model, prefab);
                Check((carriedScale - Vector3.one).magnitude < .01f && carriedScale.y < ratios.y * .8f
                    && shrimpVisual.CurrentState == "Carried",
                    "carried shrimp uses compact display scale and requests Carried: " + carriedScale.ToString("F3"));
                report.AppendLine("Carried pose bounds (pose-dependent): " + carried.size.ToString("F4"));
                Check(!badge.gameObject.activeSelf && !shadow.gameObject.activeSelf,
                    "carrying hides the ground shadow and condition badge");
                shrimp.carried = false; game.State.carryingAnimal = -1;
                view.Render(game.State, game.tuning, true);
                Check((DisplayScale(model, prefab) - ratios).magnitude < .01f
                    && shrimpVisual.CurrentState != "Carried" && badge.gameObject.activeSelf && shadow.gameObject.activeSelf,
                    "putting the shrimp down restores its ground scale, ground clip, badge and shadow");

                Transform selection = FindActive(view.transform, "Selected cell");
                Bounds selected = RenderedBounds(selection);
                Check(selected.size.x > .94f && selected.size.x < 1.10f && selected.size.z > .94f && selected.size.z < 1.10f,
                    "selection remains a single grid cell instead of following the enlarged shrimp bounds");
                Transform climb = FindActive(view.transform, "ClimbBlock");
                Transform imported = climb.Find("Imported sandGrass");
                Check(imported, "climbing block retains the imported grass/sand geometry");
                Texture2D atlas = Resources.Load<Texture2D>("Models/KayKit/block_bits_texture");
                int atlasSlots = 0;
                foreach (MeshFilter filter in imported.GetComponentsInChildren<MeshFilter>())
                    Check(filter.sharedMesh && filter.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord0),
                        "climbing mesh has an imported primary UV channel");
                foreach (Renderer renderer in imported.GetComponentsInChildren<Renderer>())
                foreach (Material material in renderer.sharedMaterials)
                {
                    Check(material && atlas && material.GetTexture("_BaseMap") == atlas,
                        "climbing renderer binds the original nonempty KayKit block atlas");
                    Check(material.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF") && material.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"),
                        "dry grass/sand disables specular and environment reflections");
                    atlasSlots++;
                }
                Check(atlasSlots > 0, "climbing block has at least one textured renderer slot");
                CheckClimbOpacity(game, fade);
            }
            catch (Exception error) { failure = error; report.AppendLine("FAIL: " + error); }
            finally
            {
                try
                {
                    game.State = original;
                    if (fade) fade.SetEnabled(false);
                    view.Initialize(original);
                    view.Render(original, game.tuning, game.overlays);
                    foreach (var entry in cameraState) entry.Key.SetValue(view, entry.Value);
                    camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                    camera.orthographicSize = cameraSize;
                    if (fade) { fade.SetEnabled(fadeEnabled); if (fade.enabled) fade.Refresh(.4f); }
                    Check(game.UndoCount == undoCount && ReferenceEquals(game.State, original),
                        "cleanup restores the original state and preserves undo depth");
                    if (hashes != null)
                    {
                        bool intact = true;
                        foreach (var entry in hashes) intact &= File.Exists(entry.Key) && HashFile(entry.Key) == entry.Value;
                        Check(intact, "original/static and animated art assets plus metadata remain byte-identical (" + hashes.Count + " files)");
                    }
                    report.AppendLine("Cleanup: original state, camera framing/pose, fade setting and undo depth restored.");
                }
                catch (Exception error) { report.AppendLine("Cleanup failed: " + error); if (failure == null) failure = error; }
                finally { game.paused = paused; if (game.Hud) game.Hud.Refresh(); }
                report.AppendLine((failure == null ? "PASS: " : "FAILED after ") + count + " visual integration assertions.");
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, report.ToString());
            }
            if (failure != null) throw new InvalidOperationException("Plan visual checks failed; see " + reportPath, failure);
            string result = "PASS: " + count + " visual integration assertions. Report: " + reportPath;
            Debug.Log("[HabitatIsland] " + result);
            return result;
        }
    }
}
