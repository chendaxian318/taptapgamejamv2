using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace HabitatIsland.Editor
{
    /// <summary>Imports the delivered boat and shopkeeper without rebuilding any scene.</summary>
    public static class HabitatStoryArtSetup
    {
        public const string BoatSource = "Assets/HabitatIsland/Art/WoodenBoat/WoodenBoat_GameReady.glb";
        public const string ShopSource = "Assets/HabitatIsland/Art/ShopAssistant/Shop_Assistant_Rigged.glb";
        public const string Folder = "Assets/HabitatIsland/Resources/Models/Story";
        public const string BoatPrefabPath = Folder + "/DepartureBoat.prefab";
        public const string ShopPrefabPath = Folder + "/Shopkeeper.prefab";
        public const string ShopAnimationFolder = Folder + "/ShopkeeperAnimation";
        public const string ControllerPath = ShopAnimationFolder + "/Shopkeeper.controller";

        [MenuItem("Tools/Habitat Island/Art/Prepare Boat And Shopkeeper")]
        public static void Build() { Debug.Log(Prepare()); }

        public static string Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before preparing story assets.");
            foreach (string path in new[] { BoatSource, ShopSource })
                if (!File.Exists(path)) throw new FileNotFoundException("Copy the delivered GLB first.", path);
            Directory.CreateDirectory(ShopAnimationFolder);
            AssetDatabase.Refresh();
            ConfigureImporter(BoatSource);
            ConfigureImporter(ShopSource);
            var report = new StringBuilder("Delivered story art: unchanged GLB sources; no scene or gameplay edits.\n");
            PrepareBoat(report);
            PrepareShopkeeper(report);
            AssetDatabase.SaveAssets();
            report.AppendLine("Existing prefab/material/animation/controller asset GUIDs are retained on rebuild.");
            Directory.CreateDirectory("Verification/boat-shop-20261016");
            File.WriteAllText("Verification/boat-shop-20261016/import.txt", report.ToString());
            return report.ToString();
        }

        static void ConfigureImporter(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (!importer) throw new InvalidOperationException("GLB importer unavailable: " + path);
            var serialized = new SerializedObject(importer);
            SerializedProperty animation = serialized.FindProperty("importSettings.animationMethod");
            SerializedProperty names = serialized.FindProperty("importSettings.nodeNameMethod");
            if (animation == null || names == null) throw new InvalidOperationException("Expected glTFast importer: " + path);
            bool changed = animation.intValue != 2 || names.intValue != 1;
            animation.intValue = 2; // Mecanim / transform animation, not Humanoid retargeting.
            names.intValue = 1; // OriginalUnique; keep delivered attachment and joint names.
            if (changed) { serialized.ApplyModifiedPropertiesWithoutUndo(); importer.SaveAndReimport(); }
        }

        static GameObject Instance(string path, Transform parent)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!source) throw new InvalidOperationException("GLB did not produce a model: " + path);
            GameObject result = UnityEngine.Object.Instantiate(source, parent, false);
            foreach (Collider collider in result.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (Rigidbody body in result.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
            foreach (Animation animation in result.GetComponentsInChildren<Animation>(true)) UnityEngine.Object.DestroyImmediate(animation);
            return result;
        }

        static void PrepareBoat(StringBuilder report)
        {
            var root = new GameObject("DepartureBoat");
            try
            {
                GameObject model = Instance(BoatSource, root.transform);
                model.name = "Delivered wooden boat";
                foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator);
                if (AssetDatabase.LoadAllAssetsAtPath(BoatSource).OfType<AnimationClip>().Any()
                    || model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 0)
                    throw new InvalidOperationException("The delivered boat is expected to be static.");
                Bounds bounds = HabitatEquipmentSetup.BakedBounds(root);
                if (Mathf.Abs(bounds.size.z - 3f) > .005f || Mathf.Abs(bounds.size.x - 1.223238f) > .005f
                    || Mathf.Abs(bounds.min.y + .191114f) > .005f)
                    throw new InvalidOperationException("Boat axes, scale or waterline changed: " + bounds);
                report.AppendLine("\nBoat SHA256: " + SourceHash(BoatSource));
                report.AppendLine("Boat: 3m long, +Y up / +Z bow, original waterline origin and scale=1. Bounds: " + bounds);
                // glTFast changes handedness by mirroring X; never rebuild these anchors from a guessed height.
                VerifyAnchor(root, "Seat_Center_Surface", new Vector3(-.000140f, .348508f, -.040063f), report);
                VerifyAnchor(root, "Footwell_Aft_Surface", new Vector3(-.000140f, -.154495f, -.316004f), report);
                VerifyAnchor(root, "Boarding_Port_Rim", new Vector3(.520207f, .530182f, -.316004f), report);
                VerifyAnchor(root, "Boarding_Starboard_Rim", new Vector3(-.520486f, .529502f, -.316004f), report);
                CopyMaterials(model, "WoodenBoat", report);
                if (!PrefabUtility.SaveAsPrefabAsset(root, BoatPrefabPath)) throw new InvalidOperationException("Could not save boat prefab.");
                report.AppendLine("Boat prefab: " + BoatPrefabPath + "; no artificial skeleton, animations or colliders.");
                report.AppendLine("Seat is a contact surface, not a calibrated character root; runtime must use delivered anchors.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void VerifyAnchor(GameObject root, string name, Vector3 expected, StringBuilder report)
        {
            Transform anchor = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
            Vector3 point = root.transform.InverseTransformPoint(anchor.position);
            if (Vector3.Distance(point, expected) > .002f)
                throw new InvalidOperationException("Unexpected imported boat anchor " + name + ": " + point);
            report.AppendLine(name + " (Unity prefab local)=" + point.ToString("F6"));
        }

        static void PrepareShopkeeper(StringBuilder report)
        {
            AnimationClip[] imported = AssetDatabase.LoadAllAssetsAtPath(ShopSource).OfType<AnimationClip>().ToArray();
            if (imported.Length != 3) throw new InvalidOperationException("Expected all three supplied shopkeeper clips, found " + imported.Length);
            AnimationClip idle = PrepareClip(imported.Single(c => c.name == "Idle"), 4f, true);
            AnimationClip talk = PrepareClip(imported.Single(c => c.name == "Talk"), 3f, true);
            AnimationClip greet = PrepareClip(imported.Single(c => c.name == "Greet"), 2f, false);
            AnimatorController controller = PrepareController(idle, talk, greet);
            var root = new GameObject("Shopkeeper");
            try
            {
                GameObject model = Instance(ShopSource, root.transform);
                model.name = "Delivered shop assistant rig";
                idle.SampleAnimation(model, 0f);
                Bounds bounds = HabitatEquipmentSetup.BakedBounds(root);
                if (Mathf.Abs(bounds.size.y - 1.6f) > .01f || Mathf.Abs(bounds.min.y) > .005f)
                    throw new InvalidOperationException("Unexpected shopkeeper foot origin, height or skin transform: " + bounds);
                int bones = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(s => s.bones).Distinct().Count();
                if (bones != 20) throw new InvalidOperationException("Expected the supplied 20-bone shopkeeper, found " + bones);
                var animator = model.GetComponent<Animator>();
                if (!animator) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                CopyMaterials(model, "Shopkeeper", report);
                report.AppendLine("\nShopkeeper SHA256: " + SourceHash(ShopSource));
                report.AppendLine("Shopkeeper: feet origin, +Y up / +Z face, original scale=1; 20 bones. Bounds: " + bounds);
                foreach (AnimationClip clip in new[] { idle, talk, greet })
                {
                    report.AppendLine("Clip " + clip.name + ": seconds=" + clip.length + "; fps=" + clip.frameRate + "; loop=" + clip.isLooping);
                    foreach (float progress in new[] { 0f, .25f, .5f, .75f, 1f })
                    {
                        clip.SampleAnimation(model, clip.length * progress);
                        Bounds poseBounds = HabitatEquipmentSetup.BakedBounds(root);
                        if (Mathf.Abs(poseBounds.min.y) > .005f || poseBounds.size.y > 1.8f)
                            throw new InvalidOperationException("Shopkeeper sample has unexpected feet/height: " + clip.name + " @ " + progress + ": " + poseBounds);
                    }
                }
                idle.SampleAnimation(model, 0f);
                if (!PrefabUtility.SaveAsPrefabAsset(root, ShopPrefabPath)) throw new InvalidOperationException("Could not save shopkeeper prefab.");
                report.AppendLine("Shopkeeper prefab: " + ShopPrefabPath + "; Generic Animator, no root motion.");
                report.AppendLine("Controller: bool Talking (Idle/Talk), trigger Greet (single 2s action, then Idle/Talk according to Talking).");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static AnimationClip PrepareClip(AnimationClip source, float duration, bool loop)
        {
            var bindings = AnimationUtility.GetCurveBindings(source);
            if (bindings.Length == 0) throw new InvalidOperationException("No transform curves in " + source.name);
            float first = float.PositiveInfinity, last = float.NegativeInfinity;
            foreach (var binding in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                if (curve == null || curve.length == 0) continue;
                first = Mathf.Min(first, curve.keys[0].time);
                last = Mathf.Max(last, curve.keys[curve.length - 1].time);
            }
            if (!float.IsFinite(first) || !float.IsFinite(last) || Mathf.Abs(last - first - duration) > .06f)
                throw new InvalidOperationException("Unexpected " + source.name + " key interval: " + first + " to " + last);
            string path = ShopAnimationFolder + "/" + source.name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (!clip) { clip = UnityEngine.Object.Instantiate(source); AssetDatabase.CreateAsset(clip, path); }
            else EditorUtility.CopySerialized(source, clip);
            clip.name = source.name; clip.legacy = false;
            foreach (var binding in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null) continue;
                Keyframe[] keys = curve.keys;
                for (int i = 0; i < keys.Length; i++) keys[i].time -= first;
                curve.keys = keys; AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0f; settings.stopTime = last - first;
            settings.loopTime = loop; settings.loopBlend = false;
            settings.keepOriginalPositionXZ = true; settings.keepOriginalPositionY = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static AnimatorController PrepareController(AnimationClip idle, AnimationClip talk, AnimationClip greet)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.parameters = new[] {
                new AnimatorControllerParameter { name = "Talking", type = AnimatorControllerParameterType.Bool },
                new AnimatorControllerParameter { name = "Greet", type = AnimatorControllerParameterType.Trigger }
            };
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            var clips = new[] { idle, talk, greet };
            var states = new Dictionary<string, AnimatorState>();
            foreach (AnimationClip clip in clips)
            {
                AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == clip.name);
                if (!state) state = machine.AddState(clip.name);
                state.motion = clip; state.writeDefaultValues = false; state.speed = 1f;
                foreach (AnimatorStateTransition transition in state.transitions) state.RemoveTransition(transition);
                states.Add(clip.name, state); EditorUtility.SetDirty(state);
            }
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
            machine.defaultState = states["Idle"];
            Transition(states["Idle"], states["Talk"], false).AddCondition(AnimatorConditionMode.If, 0f, "Talking");
            Transition(states["Talk"], states["Idle"], false).AddCondition(AnimatorConditionMode.IfNot, 0f, "Talking");
            var greeting = machine.AddAnyStateTransition(states["Greet"]);
            greeting.hasExitTime = false; greeting.hasFixedDuration = true; greeting.duration = .15f;
            greeting.canTransitionToSelf = false; greeting.AddCondition(AnimatorConditionMode.If, 0f, "Greet");
            Transition(states["Greet"], states["Talk"], true).AddCondition(AnimatorConditionMode.If, 0f, "Talking");
            Transition(states["Greet"], states["Idle"], true).AddCondition(AnimatorConditionMode.IfNot, 0f, "Talking");
            EditorUtility.SetDirty(machine); EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, bool exit)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = exit; transition.exitTime = 1f;
            transition.hasFixedDuration = true; transition.duration = .15f;
            transition.canTransitionToSelf = false;
            return transition;
        }

        static void CopyMaterials(GameObject model, string prefix, StringBuilder report)
        {
            var copies = new Dictionary<Material, Material>();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (!source) throw new InvalidOperationException("Missing source material on " + renderer.name);
                    if (!copies.TryGetValue(source, out Material copy))
                    {
                        string path = Folder + "/" + prefix + "_" + copies.Count + ".mat";
                        copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!copy) { copy = new Material(source); AssetDatabase.CreateAsset(copy, path); }
                        else EditorUtility.CopySerialized(source, copy);
                        HabitatIsland.EditorTools.HabitatTextureOptimization.Apply(copy);
                        copy.name = prefix + " original PBR"; copy.enableInstancing = true;
                        EditorUtility.SetDirty(copy); copies.Add(source, copy);
                        report.AppendLine("Material: " + path + "; shader=" + copy.shader.name + "; source metallic/roughness channel packing retained.");
                        foreach (string property in copy.GetTexturePropertyNames())
                        {
                            Texture texture = copy.GetTexture(property);
                            if (texture) report.AppendLine("  " + property + "=" + texture.width + "x" + texture.height);
                        }
                    }
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }
        }

        static string SourceHash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}

