using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HabitatIsland.Editor
{
    /// <summary>Prepares the supplied V2 GLB without editing any gameplay scene.</summary>
    public static class HabitatTrapSetup
    {
        public const string Source = "Assets/HabitatIsland/Art/AnimatedTrap/lobster_trap_rigged_v2.glb";
        public const string Folder = "Assets/HabitatIsland/Resources/Models/AnimatedTrap";
        public const string PrefabPath = Folder + "/Trap.prefab";
        public const float Footprint = .88f;
        public const float SourceYaw = 0f; // Supplied door faces +Z after glTFast's handedness conversion.

        static void ConfigureImporter()
        {
            AssetDatabase.ImportAsset(Source, ImportAssetOptions.ForceSynchronousImport);
            AssetImporter importer = AssetImporter.GetAtPath(Source);
            if (!importer) throw new InvalidOperationException("Trap GLB importer is unavailable.");
            var serialized = new SerializedObject(importer);
            SerializedProperty method = serialized.FindProperty("importSettings.animationMethod");
            SerializedProperty names = serialized.FindProperty("importSettings.nodeNameMethod");
            if (method == null || names == null) throw new InvalidOperationException("Expected glTFast importer settings.");
            bool changed = method.intValue != 2 || names.intValue != 1;
            method.intValue = 2; // AnimationMethod.Mecanim
            names.intValue = 1; // NameImportMethod.OriginalUnique
            if (changed) { serialized.ApplyModifiedPropertiesWithoutUndo(); importer.SaveAndReimport(); }
        }

        static AnimationClip PrepareClip(AnimationClip source)
        {
            string path = Folder + "/" + source.name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (!clip) { clip = UnityEngine.Object.Instantiate(source); AssetDatabase.CreateAsset(clip, path); }
            else EditorUtility.CopySerialized(source, clip);
            clip.name = source.name; clip.legacy = false; clip.frameRate = 24f;
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            float first = float.PositiveInfinity, last = 0;
            foreach (EditorCurveBinding binding in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length == 0) continue;
                first = Mathf.Min(first, curve.keys[0].time);
                last = Mathf.Max(last, curve.keys[curve.length - 1].time);
            }
            if (!float.IsFinite(first) || Mathf.Abs(last - first - 5f) > .1f)
                throw new InvalidOperationException("Expected the delivered five-second trap action: " + source.name);
            // Keep original keys, tangents, scales and motion; only rebase frame 1 to zero.
            foreach (EditorCurveBinding binding in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null) continue;
                Keyframe[] keys = curve.keys;
                for (int i = 0; i < keys.Length; i++) keys[i].time -= first;
                curve.keys = keys;
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0f; settings.stopTime = last - first;
            settings.loopTime = source.name != "Door_Open_Close";
            settings.loopBlend = false; settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        public static Bounds BakedBounds(GameObject root)
        {
            bool found = false; Bounds bounds = new Bounds();
            foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = new Mesh();
                try
                {
                    // Imported glTF skin compensation requires the scale-aware bake here.
                    // Unity 6000.6.5f1 verification: false already includes the normalized
                    // skin scale, so TransformPoint would apply that scale a second time.
                    skin.BakeMesh(mesh, true);
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        Vector3 p = root.transform.InverseTransformPoint(skin.transform.TransformPoint(vertex));
                        if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                        else bounds.Encapsulate(p);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
            if (!found) throw new InvalidOperationException("Trap has no skinned mesh vertices.");
            return bounds;
        }

        [MenuItem("Tools/Habitat Island/Art/Prepare Animated Trap")]
        public static void PrepareMenu() { Debug.Log(Prepare()); }

        public static string Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before preparing trap assets.");
            if (!File.Exists(Source)) throw new FileNotFoundException("Copy the delivered trap GLB first.", Source);
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh(); ConfigureImporter();
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (!source) throw new InvalidOperationException("Trap GLB import failed.");
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>().ToArray();
            AnimationClip door = PrepareClip(clips.Single(c => c.name == "Door_Open_Close"));
            AnimationClip floats = PrepareClip(clips.Single(c => c.name == "Float_Sway"));
            AnimationClip demo = PrepareClip(clips.Single(c => c.name == "Demo_Door_And_Floats"));
            var root = new GameObject("Animated lobster trap V2");
            try
            {
                // Keep animation paths under the imported root unchanged; normalize via an outer pivot.
                var pivot = new GameObject("Trap normalization").transform;
                pivot.SetParent(root.transform, false);
                GameObject instance = UnityEngine.Object.Instantiate(source, pivot, false);
                instance.name = "Original trap rig";
                foreach (Animation legacy in instance.GetComponentsInChildren<Animation>(true))
                    UnityEngine.Object.DestroyImmediate(legacy);
                Animator animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = null; animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                pivot.localRotation = Quaternion.Euler(0, SourceYaw, 0);
                door.SampleAnimation(instance, 0f);
                Bounds sourceBounds = BakedBounds(root);
                float span = Mathf.Max(sourceBounds.size.x, sourceBounds.size.z);
                if (span < .001f) throw new InvalidOperationException("Invalid trap dimensions.");
                float scale = Footprint / span;
                pivot.localScale = Vector3.one * scale;
                pivot.localPosition = -new Vector3(sourceBounds.center.x, sourceBounds.min.y, sourceBounds.center.z) * scale;

                var report = new StringBuilder();
                report.AppendLine("Source: " + Source);
                report.AppendLine("Source closed bounds: " + sourceBounds);
                report.AppendLine("Uniform scale: " + scale.ToString("F7") + "; door faces +Z; closed footprint: " + Footprint);
                int index = 0;
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material original = materials[i];
                        if (!original) throw new InvalidOperationException("Trap material is missing.");
                        string path = Folder + "/Trap_" + index++ + ".mat";
                        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!material) { material = new Material(original); AssetDatabase.CreateAsset(material, path); }
                        else EditorUtility.CopySerialized(original, material);
                        HabitatIsland.EditorTools.HabitatTextureOptimization.Apply(material);
                        material.name = "Trap original PBR " + i;
                        // Preserve embedded base color, UV transforms and glTF metallic/roughness packing.
                        material.enableInstancing = true; EditorUtility.SetDirty(material); materials[i] = material;
                        report.AppendLine("Material: " + material.shader.name);
                        foreach (string property in material.GetTexturePropertyNames())
                        {
                            Texture texture = material.GetTexture(property);
                            if (texture) report.AppendLine(property + ": " + texture.width + "x" + texture.height);
                        }
                    }
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                    if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                }
                foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);

                Bounds closed = BakedBounds(root);
                door.SampleAnimation(instance, 2f);
                Bounds opened = BakedBounds(root);
                report.AppendLine("Normalized closed bounds: " + closed);
                report.AppendLine("Normalized open bounds: " + opened);
                report.AppendLine("Bones: " + instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(s => s.bones).Distinct().Count());
                foreach (AnimationClip clip in new[] { door, floats, demo })
                    report.AppendLine("Clip: " + clip.name + "; seconds=" + clip.length + "; fps=" + clip.frameRate);
                var visual = root.AddComponent<HabitatTrapVisual>();
                visual.Configure(animator, instance.transform, door, floats, demo);
                // Persist the ready pose; the runtime first bind chooses occupied/ready without a fake event.
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); AssetDatabase.SaveAssets();
                report.AppendLine("Prefab: " + PrefabPath);
                report.AppendLine("Ready=open pose; capture=original closing section; occupied=Float_Sway; remove shrimp=original opening section.");
                report.AppendLine("ResetVisual handles load/undo/rebuild. No cage recovery or gameplay rule was added.");
                Directory.CreateDirectory("Verification");
                File.WriteAllText("Verification/animated-trap-import.txt", report.ToString());
                return report.ToString();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}

