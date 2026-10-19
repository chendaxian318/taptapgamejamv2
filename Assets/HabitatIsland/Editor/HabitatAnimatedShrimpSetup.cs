using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace HabitatIsland.Editor
{
    /// <summary>Builds animation assets from the supplied GLB; never edits that source.</summary>
    public static class HabitatAnimatedShrimpSetup
    {
        public const string Source = "Assets/HabitatIsland/Art/AnimatedShrimp/creature_animated.glb";
        public const string Folder = "Assets/HabitatIsland/Resources/Models/AnimatedShrimp";
        public const string PrefabPath = Folder + "/Shrimp.prefab";
        const float SourceFootprint = .80f;
        static readonly string[] ClipNames = { "Idle", "Move", "Carried" };

        static void EnsureMecanimImport()
        {
            AssetDatabase.ImportAsset(Source, ImportAssetOptions.ForceSynchronousImport);
            AssetImporter importer = AssetImporter.GetAtPath(Source);
            if (!importer) throw new InvalidOperationException("Animated shrimp GLB is missing or has no importer.");
            var serialized = new SerializedObject(importer);
            SerializedProperty method = serialized.FindProperty("importSettings.animationMethod");
            if (method == null || method.propertyType != SerializedPropertyType.Enum)
                throw new InvalidOperationException("Animated shrimp requires the installed glTFast importer.");
            int mecanim = Array.IndexOf(method.enumNames, "Mecanim");
            if (mecanim < 0) throw new InvalidOperationException("glTFast importer has no Mecanim mode.");
            method.enumValueIndex = mecanim;
            SerializedProperty offscreen = serialized.FindProperty("instantiationSettings.skinUpdateWhenOffscreen");
            if (offscreen != null) offscreen.boolValue = true;
            if (serialized.ApplyModifiedPropertiesWithoutUndo()) importer.SaveAndReimport();
        }

        static AnimationClip SaveClip(AnimationClip source, string name)
        {
            string path = Folder + "/" + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (!clip)
            {
                clip = UnityEngine.Object.Instantiate(source);
                AssetDatabase.CreateAsset(clip, path);
            }
            else EditorUtility.CopySerialized(source, clip);
            clip.name = name; clip.legacy = false; clip.wrapMode = WrapMode.Loop;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            // The supplied loops already have matching endpoints. Do not blend
            // or remove the authored root-bone motion inside the stationary rig.
            settings.loopBlend = false; settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static Bounds BakedBounds(GameObject root, SkinnedMeshRenderer[] skins,
            Dictionary<SkinnedMeshRenderer, Bounds> skinBounds = null)
        {
            Bounds result = default;
            bool found = false;
            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            try
            {
                foreach (SkinnedMeshRenderer skin in skins)
                {
                    // Verified with this glTF skin in Unity 6000.6.5f1: false
                    // already includes the normalization scale, so transforming
                    // that output again shrinks it twice. true yields the local
                    // vertices needed by the same conversion as TravelerSetup.
                    skin.BakeMesh(mesh, true); mesh.GetVertices(vertices);
                    Matrix4x4 matrix = root.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                    bool localFound = false; Bounds local = default;
                    foreach (Vector3 vertex in vertices)
                    {
                        if (!localFound) { local = new Bounds(vertex, Vector3.zero); localFound = true; }
                        else local.Encapsulate(vertex);
                        Vector3 point = matrix.MultiplyPoint3x4(vertex);
                        if (!found) { result = new Bounds(point, Vector3.zero); found = true; }
                        else result.Encapsulate(point);
                    }
                    if (skinBounds != null && localFound)
                    {
                        if (skinBounds.TryGetValue(skin, out Bounds previous)) { previous.Encapsulate(local); skinBounds[skin] = previous; }
                        else skinBounds.Add(skin, local);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
            if (!found) throw new InvalidOperationException("Animated shrimp has no baked skinned vertices.");
            return result;
        }

        public static string Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before preparing animated shrimp assets.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            EnsureMecanimImport();
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (!source) throw new InvalidOperationException("Animated shrimp GLB has not imported as a model.");
            AnimationClip[] importedClips = AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>().ToArray();
            var clips = new List<AnimationClip>();
            foreach (string name in ClipNames)
            {
                AnimationClip original = importedClips.SingleOrDefault(clip => clip.name == name);
                if (!original) throw new InvalidOperationException("Missing GLB clip " + name + "; found: " + string.Join(", ", importedClips.Select(clip => clip.name)));
                clips.Add(SaveClip(original, name));
            }
            var root = new GameObject("Animated Shrimp");
            try
            {
                Transform normalization = new GameObject("Normalized rig").transform;
                normalization.SetParent(root.transform, false);
                GameObject instance = UnityEngine.Object.Instantiate(source, normalization, false);
                instance.name = "Original animated rig";
                foreach (Animation animation in instance.GetComponentsInChildren<Animation>(true)) UnityEngine.Object.DestroyImmediate(animation);
                foreach (Animator imported in instance.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(imported);
                foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
                SkinnedMeshRenderer[] skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Bounds bindBounds = BakedBounds(root, skins);
                float span = Mathf.Max(bindBounds.size.x, bindBounds.size.z);
                if (span < .001f) throw new InvalidOperationException("Animated shrimp has an invalid bind-pose footprint.");
                float scale = SourceFootprint / span;
                normalization.localScale = Vector3.one * scale;
                normalization.localPosition = -new Vector3(bindBounds.center.x, bindBounds.min.y, bindBounds.center.z) * scale;

                int slot = 0;
                foreach (SkinnedMeshRenderer skin in skins)
                {
                    Material[] materials = skin.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (!materials[i]) throw new InvalidOperationException("Animated shrimp has a missing source material.");
                        string path = Folder + "/Shrimp" + slot++ + ".mat";
                        Material copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!copy) { copy = new Material(materials[i]); AssetDatabase.CreateAsset(copy, path); }
                        else EditorUtility.CopySerialized(materials[i], copy);
                        HabitatIsland.EditorTools.HabitatTextureOptimization.Apply(copy);
                        copy.name = "Animated Shrimp original PBR " + i;
                        copy.enableInstancing = true;
                        EditorUtility.SetDirty(copy); materials[i] = copy;
                    }
                    skin.sharedMaterials = materials;
                    skin.updateWhenOffscreen = true; skin.shadowCastingMode = ShadowCastingMode.On;
                    skin.receiveShadows = true; skin.skinnedMotionVectors = true;
                }

                var report = new StringBuilder("Animated shrimp import\nSource: " + Source + "\n");
                report.AppendLine("Bind-pose bounds: " + bindBounds + "; uniform normalization: " + scale.ToString("F7"));
                var localBounds = new Dictionary<SkinnedMeshRenderer, Bounds>();
                Bounds animatedBounds = default; bool sampled = false;
                foreach (AnimationClip clip in clips)
                {
                    int frames = Mathf.CeilToInt(clip.length * 30f);
                    Bounds firstPose = default, lastPose = default;
                    float largestBoundsChange = 0f;
                    for (int frame = 0; frame <= frames; frame++)
                    {
                        clip.SampleAnimation(instance, clip.length * frame / Mathf.Max(1, frames));
                        Bounds bounds = BakedBounds(root, skins, localBounds);
                        if (frame == 0) firstPose = bounds;
                        lastPose = bounds;
                        largestBoundsChange = Mathf.Max(largestBoundsChange,
                            (bounds.center - firstPose.center).magnitude + (bounds.size - firstPose.size).magnitude);
                        if (!sampled) { animatedBounds = bounds; sampled = true; } else animatedBounds.Encapsulate(bounds);
                    }
                    if (largestBoundsChange < .0001f)
                        throw new InvalidOperationException(clip.name + " did not change baked mesh bounds; check animation binding paths.");
                    if ((lastPose.center - firstPose.center).magnitude + (lastPose.size - firstPose.size).magnitude > .003f)
                        throw new InvalidOperationException(clip.name + " baked bounds do not meet at the loop endpoints.");
                    report.AppendLine(clip.name + ": " + clip.length.ToString("F3") + " s; looped; sampled " + (frames + 1)
                        + " poses; baked bounds change " + largestBoundsChange.ToString("F5") + ".");
                }
                foreach (var entry in localBounds)
                {
                    Bounds bounds = entry.Value; bounds.Expand(.02f / scale);
                    entry.Key.localBounds = bounds;
                }
                // The lowest animated pose sits on the display floor; this is a
                // static wrapper offset, not animation or world/root motion.
                float floorOffset = Mathf.Max(0f, -animatedBounds.min.y);
                normalization.localPosition += Vector3.up * floorOffset;
                animatedBounds.center += Vector3.up * floorOffset;
                clips[0].SampleAnimation(instance, 0f);

                string controllerPath = Folder + "/Shrimp.controller";
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                AnimatorStateMachine machine = controller.layers[0].stateMachine;
                foreach (AnimationClip clip in clips)
                {
                    AnimatorState state = machine.states.Select(child => child.state).FirstOrDefault(item => item.name == clip.name);
                    if (!state) state = machine.AddState(clip.name);
                    state.motion = clip; state.speed = 1f; state.transitions = Array.Empty<AnimatorStateTransition>();
                    if (clip.name == "Idle") machine.defaultState = state;
                    EditorUtility.SetDirty(state);
                }
                EditorUtility.SetDirty(machine); EditorUtility.SetDirty(controller);
                Animator animator = instance.AddComponent<Animator>();
                animator.avatar = null; animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false; animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                HabitatShrimpVisual visual = root.AddComponent<HabitatShrimpVisual>();
                var settings = new SerializedObject(visual);
                settings.FindProperty("animationBounds").boundsValue = animatedBounds;
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                report.AppendLine("Sampled normalized bounds: " + animatedBounds);
                report.AppendLine("Ground/carry multipliers remain owned by View (1.4 / 1.0). No extra view bob for this model.");
                report.AppendLine("Generic transform animation, original packed PBR textures, no root motion; source GLB unchanged.");
                report.AppendLine("Prefab: " + PrefabPath);
                string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/animated-shrimp-import.txt"));
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, report.ToString());
                return report.ToString();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}

