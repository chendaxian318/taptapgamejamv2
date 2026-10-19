using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HabitatIsland.Editor
{
    /// <summary>Prepares the three delivered equipment models; never edits gameplay scenes.</summary>
    public static partial class HabitatEquipmentSetup
    {
        public const string RodSource = "Assets/HabitatIsland/Art/FishingRod/fishing_rod_unirig_animated.glb";
        public const string ShovelSource = "Assets/HabitatIsland/Art/Shovel/Shovel_PlayerReady.glb";
        public const string BellowsSource = "Assets/HabitatIsland/Art/ShellBellows/Shell_Bellows_Rigged.glb";
        public const string ModelsFolder = "Assets/HabitatIsland/Resources/Models";
        public const string RodPrefabPath = ModelsFolder + "/FishingRod/Rod.prefab";
        public const string ShovelPrefabPath = ModelsFolder + "/Shovel/Shovel.prefab";
        public const string BellowsPrefabPath = ModelsFolder + "/ShellBellows/Bellows.prefab";
        public const float BellowsFootprint = .82f;
        // The root_nozzle node rotates its child +Z shell offsets into GLB world +Y.
        // glTFast mirrors X only: imported +X nozzle -> game +Z, keeping world Y up.
        public static Quaternion BellowsRotation => Quaternion.Euler(0f, -90f, 0f);

        sealed class ClipSpec
        {
            public readonly string name;
            public readonly float seconds;
            public readonly bool loop;
            public ClipSpec(string name, float seconds, bool loop)
            { this.name = name; this.seconds = seconds; this.loop = loop; }
        }

        static void ConfigureImporter(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (!importer) throw new InvalidOperationException("GLB importer is unavailable: " + path);
            var serialized = new SerializedObject(importer);
            SerializedProperty method = serialized.FindProperty("importSettings.animationMethod");
            SerializedProperty names = serialized.FindProperty("importSettings.nodeNameMethod");
            if (method == null || names == null) throw new InvalidOperationException("Expected glTFast importer settings: " + path);
            bool changed = method.intValue != 2 || names.intValue != 1;
            method.intValue = 2; // AnimationMethod.Mecanim
            names.intValue = 1; // NameImportMethod.OriginalUnique
            if (changed) { serialized.ApplyModifiedPropertiesWithoutUndo(); importer.SaveAndReimport(); }
        }

        static AnimationClip PrepareClip(AnimationClip source, string folder, ClipSpec spec)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(source);
            EditorCurveBinding[] objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(source);
            float first = float.PositiveInfinity, last = float.NegativeInfinity;
            foreach (EditorCurveBinding binding in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                if (curve == null || curve.length == 0) continue;
                first = Mathf.Min(first, curve.keys[0].time); last = Mathf.Max(last, curve.keys[curve.length - 1].time);
            }
            foreach (EditorCurveBinding binding in objectBindings)
            {
                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(source, binding);
                if (keys == null || keys.Length == 0) continue;
                first = Mathf.Min(first, keys[0].time); last = Mathf.Max(last, keys[keys.Length - 1].time);
            }
            if (!float.IsFinite(first) || !float.IsFinite(last) || last <= first)
                throw new InvalidOperationException("Animation has no usable key interval: " + source.name);
            float duration = last - first;
            if (spec.seconds > 0f && Mathf.Abs(duration - spec.seconds) > .06f)
                throw new InvalidOperationException(source.name + " has " + duration + " seconds, expected " + spec.seconds);

            string path = folder + "/" + spec.name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (!clip) { clip = UnityEngine.Object.Instantiate(source); AssetDatabase.CreateAsset(clip, path); }
            else EditorUtility.CopySerialized(source, clip); // Preserve the existing independent asset's GUID.
            clip.name = spec.name; clip.legacy = false;
            // Rebase only. Supplied poses, tangents, weights, scales and frame rate remain intact.
            foreach (EditorCurveBinding binding in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null) continue;
                Keyframe[] keys = curve.keys;
                for (int i = 0; i < keys.Length; i++) keys[i].time -= first;
                curve.keys = keys; AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
            foreach (EditorCurveBinding binding in objectBindings)
            {
                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                for (int i = 0; i < keys.Length; i++) keys[i].time -= first;
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            }
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            foreach (AnimationEvent e in events) e.time = Mathf.Max(0f, e.time - first);
            AnimationUtility.SetAnimationEvents(clip, events);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0f; settings.stopTime = duration;
            settings.loopTime = spec.loop; settings.loopBlend = false; settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        public static Bounds BakedBounds(GameObject root)
        {
            bool found = false; Bounds bounds = default;
            Action<Vector3> include = p =>
            {
                if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z))
                    throw new InvalidOperationException("Equipment mesh contains a non-finite vertex.");
                if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                else bounds.Encapsulate(p);
            };
            foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.sharedMesh) continue;
                var mesh = new Mesh();
                try
                {
                    // Match the verified glTF skin-compensation bake used by HabitatTrapSetup.
                    skin.BakeMesh(mesh, true);
                    foreach (Vector3 v in mesh.vertices) include(root.transform.InverseTransformPoint(skin.transform.TransformPoint(v)));
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh)
                    foreach (Vector3 v in filter.sharedMesh.vertices) include(root.transform.InverseTransformPoint(filter.transform.TransformPoint(v)));
            if (!found) throw new InvalidOperationException("Equipment has no mesh vertices: " + root.name);
            return bounds;
        }

        static void CopyMaterials(GameObject instance, string folder, string prefix, StringBuilder report)
        {
            int index = 0;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (!source) throw new InvalidOperationException("Missing source PBR material: " + renderer.name);
                    string path = folder + "/" + prefix + "_" + index++ + ".mat";
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!material) { material = new Material(source); AssetDatabase.CreateAsset(material, path); }
                    else EditorUtility.CopySerialized(source, material);
                    HabitatIsland.EditorTools.HabitatTextureOptimization.Apply(material);
                    material.name = prefix + " original PBR " + i;
                    material.enableInstancing = true; EditorUtility.SetDirty(material); materials[i] = material;
                    report.AppendLine("Material: " + path + "; shader=" + material.shader.name);
                    foreach (string property in material.GetTexturePropertyNames())
                    {
                        Texture texture = material.GetTexture(property);
                        if (texture) report.AppendLine("  " + property + "=" + texture.width + "x" + texture.height);
                    }
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }
        }

        static void Prepare(string sourcePath, string prefabPath, string prefix, string gripName, bool bellows, ClipSpec[] specs, StringBuilder report)
        {
            ConfigureImporter(sourcePath);
            string folder = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (!source) throw new InvalidOperationException("GLB import failed: " + sourcePath);
            AnimationClip[] importedClips = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().ToArray();
            if (specs == null) specs = importedClips.Select(c => new ClipSpec(c.name, -1f, c.isLooping)).ToArray();
            var preparedClips = specs.Select(spec => PrepareClip(importedClips.Single(c => c.name == spec.name), folder, spec)).ToArray();
            var root = new GameObject(prefix);
            try
            {
                var pivot = new GameObject(bellows ? "Bellows orientation and size" : "Source grip pivot").transform;
                pivot.SetParent(root.transform, false);
                GameObject instance = UnityEngine.Object.Instantiate(source, pivot, false);
                instance.name = "Original " + prefix + " rig";
                foreach (Animation legacy in instance.GetComponentsInChildren<Animation>(true)) UnityEngine.Object.DestroyImmediate(legacy);
                Animator animator = instance.GetComponent<Animator>();
                if (!animator) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = null; animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (preparedClips.Length > 0) preparedClips[0].SampleAnimation(instance, 0f);
                report.AppendLine("\nSource: " + sourcePath);
                if (bellows)
                {
                    Transform nozzle = instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == "root_nozzle");
                    Vector3 sourceNozzle = instance.transform.InverseTransformPoint(nozzle.position);
                    if (sourceNozzle.x < .5f || Mathf.Abs(sourceNozzle.y) > .02f || Mathf.Abs(sourceNozzle.z) > .02f)
                        throw new InvalidOperationException("Bellows imported nozzle basis differs from verified GLB: " + sourceNozzle);
                    Transform upper = instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == "shell_upper");
                    Transform lower = instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == "shell_lower");
                    Vector3 shellUp = instance.transform.InverseTransformDirection(upper.position - lower.position).normalized;
                    if (Vector3.Dot(shellUp, Vector3.up) < .99f)
                        throw new InvalidOperationException("Bellows world shell-up basis is not +Y: " + shellUp);
                    pivot.localRotation = BellowsRotation;
                    Bounds raw = BakedBounds(root);
                    float span = Mathf.Max(raw.size.x, raw.size.z);
                    if (span < .001f) throw new InvalidOperationException("Invalid bellows dimensions.");
                    float scale = BellowsFootprint / span;
                    pivot.localScale = Vector3.one * scale;
                    pivot.localPosition = -new Vector3(raw.center.x, raw.min.y, raw.center.z) * scale;
                    report.AppendLine("Evaluated world axes: GLB -X nozzle / +Y upper shell; glTFast mirrors X only.");
                    report.AppendLine("Imported nozzle bone: " + sourceNozzle + "; measured shell-up=" + shellUp + "; yaw -90 maps +X nozzle to +Z, preserving Y up.");
                    report.AppendLine("Oriented source bounds: " + raw + "; uniform scale=" + scale.ToString("F7"));
                }
                else if(gripName!=null)
                {
                    Transform grip = instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == gripName);
                    Vector3 gripPosition = root.transform.InverseTransformPoint(grip.position);
                    if (gripPosition.magnitude > .002f) throw new InvalidOperationException(prefix + " grip is not at the source origin: " + gripPosition);
                    report.AppendLine("Source scale and orientation preserved. Grip " + gripName + " at " + gripPosition + "; outer scale=1.");
                }
                CopyMaterials(instance, folder, prefix, report);
                foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
                Bounds ready = BakedBounds(root);
                if (bellows && (Mathf.Abs(Mathf.Max(ready.size.x, ready.size.z) - BellowsFootprint) > .002f || Mathf.Abs(ready.min.y) > .002f))
                    throw new InvalidOperationException("Bellows normalization failed: " + ready);
                report.AppendLine("Ready bounds: " + ready);
                report.AppendLine("Bones: " + instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(s => s.bones).Distinct().Count());
                foreach (AnimationClip clip in preparedClips)
                {
                    report.AppendLine("Clip: " + clip.name + "; seconds=" + clip.length + "; fps=" + clip.frameRate + "; loop=" + clip.isLooping);
                    clip.SampleAnimation(instance, clip.length * .5f);
                    report.AppendLine("  Mid-action bounds: " + BakedBounds(root));
                }
                if (preparedClips.Length > 0) preparedClips[0].SampleAnimation(instance, 0f);
                else report.AppendLine("Static supplied rig: no source animations; no invented animation added.");
                root.AddComponent<HabitatEquipmentAnimation>().Configure(animator, preparedClips);
                if (!PrefabUtility.SaveAsPrefabAsset(root, prefabPath)) throw new InvalidOperationException("Failed to save " + prefabPath);
                report.AppendLine("Prefab: " + prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [MenuItem("Tools/Habitat Island/Art/Prepare Fishing Equipment")]
        public static void PrepareMenu() { Debug.Log(PrepareAll()); }

        [MenuItem("Tools/Habitat Island/Art/Prepare Delivered Fish and Utilities")]
        public static void PrepareDeliveredMenu(){Debug.Log(PrepareDelivered());}
        [MenuItem("Tools/Habitat Island/Art/Prepare Delivered Hermit Crab")]
        public static void PrepareCrabMenu(){Debug.Log(PrepareCrab());}
        [MenuItem("Tools/Habitat Island/Art/Prepare Delivered Jellyfish")]
        public static void PrepareJellyfishMenu(){Debug.Log(PrepareJellyfish());}
        public static string PrepareJellyfish()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            string path=ModelsFolder+"/Jellyfish/Jellyfish.prefab";
            Directory.CreateDirectory(ModelsFolder+"/Jellyfish");AssetDatabase.Refresh();
            var report=new StringBuilder("Delivered jellyfish: original PBR, 33-bone rig; bell-base origin retained.\n");
            Prepare("Assets/HabitatIsland/Art/Jellyfish/Jellyfish_GameReady.glb",path,"Jellyfish",null,false,
                new[]{new ClipSpec("Idle",3,true),new ClipSpec("Swim",1.6f,true)},report);
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var animation=root.GetComponent<HabitatEquipmentAnimation>();
                float bottom=0f;
                foreach(var clip in animation.Clips)
                    for(int i=0;i<=24;i++)
                    {
                        clip.SampleAnimation(animation.Animator.gameObject,clip.length*i/24f);
                        bottom=Mathf.Min(bottom,BakedBounds(root).min.y);
                    }
                animation.Clips[0].SampleAnimation(animation.Animator.gameObject,0f);
                var visual=root.GetComponent<HabitatJellyfishVisual>();
                if(!visual)visual=root.AddComponent<HabitatJellyfishVisual>();
                visual.ConfigureClearance(-bottom+.015f);
                report.AppendLine("Dry ground / carry clearance: "+visual.GroundClearance);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();Directory.CreateDirectory("Verification/jellyfish-20261017");
            File.WriteAllText("Verification/jellyfish-20261017/import.txt",report.ToString());return report.ToString();
        }
        public static string PrepareCrab()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            Directory.CreateDirectory(ModelsFolder+"/HermitCrab");AssetDatabase.Refresh();
            var report=new StringBuilder("Delivered hermit crab: original PBR, 27-bone rig and authored clips.\n");
            Prepare("Assets/HabitatIsland/Art/HermitCrab/HermitCrab_GameReady.glb",ModelsFolder+"/HermitCrab/HermitCrab.prefab","HermitCrab",null,false,
                new[]{new ClipSpec("Idle",3,true),new ClipSpec("Walk",1.6f,true),new ClipSpec("Startle",1.2f,false)},report);
            AssetDatabase.SaveAssets();Directory.CreateDirectory("Verification/crab-ui-20261016");
            File.WriteAllText("Verification/crab-ui-20261016/import.txt",report.ToString());return report.ToString();
        }
        public static string PrepareDelivered()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            var report=new StringBuilder("Delivered goldfish, magnet and cannon: source rigs and PBR retained.\n");
            foreach(string name in new[]{"Goldfish","Magnet","Cannon"})Directory.CreateDirectory(ModelsFolder+"/"+name);
            AssetDatabase.Refresh();
            Prepare("Assets/HabitatIsland/Art/Goldfish/Goldfish_GameReady.glb",ModelsFolder+"/Goldfish/Goldfish.prefab","Goldfish",null,false,
                new[]{new ClipSpec("Idle",3,true),new ClipSpec("Swim",2,true),new ClipSpec("Turn_Left",1.2f,false),new ClipSpec("Turn_Right",1.2f,false)},report);
            Prepare("Assets/HabitatIsland/Art/Magnet/Magnet_GameReady.glb",ModelsFolder+"/Magnet/Magnet.prefab","Magnet",null,false,null,report);
            Prepare("Assets/HabitatIsland/Art/Cannon/Cannon_GameReady.glb",ModelsFolder+"/Cannon/Cannon.prefab","Cannon",null,false,
                new[]{new ClipSpec("Idle",2,true),new ClipSpec("Fire",1.2f,false),new ClipSpec("Aim_Up",1,false)},report);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Verification/workflow-assets-20261016");
            File.WriteAllText("Verification/workflow-assets-20261016/import.txt",report.ToString());
            return report.ToString();
        }

        public static string PrepareAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before preparing equipment assets.");
            foreach (string source in new[] { RodSource, ShovelSource, BellowsSource })
                if (!File.Exists(source)) throw new FileNotFoundException("Copy the delivered equipment GLB first.", source);
            foreach (string prefab in new[] { RodPrefabPath, ShovelPrefabPath, BellowsPrefabPath }) Directory.CreateDirectory(Path.GetDirectoryName(prefab));
            AssetDatabase.Refresh();
            var report = new StringBuilder("Equipment import: original GLB rigs / embedded PBR / independent rebased clips.\n");
            Prepare(RodSource, RodPrefabPath, "Rod", "Grip_CTRL", false, new[] {
                new ClipSpec("Idle_Sway", 4f, true), new ClipSpec("Cast", 3f, false), new ClipSpec("Hook_Pull", 3.5f, false) }, report);
            Prepare(ShovelSource, ShovelPrefabPath, "Shovel", "Grip_Primary", false, null, report);
            Prepare(BellowsSource, BellowsPrefabPath, "Bellows", null, true, new[] {
                new ClipSpec("Idle", 2f, true), new ClipSpec("Pump_Loop", 1.5f, true), new ClipSpec("Pump_Once", 1f, false) }, report);
            AssetDatabase.SaveAssets();
            report.AppendLine("No scene edited; no backpack imported. Equipment animation uses a manually evaluated graph and requires owner Tick.");
            report.AppendLine("Existing material, clip and prefab paths are updated in place to preserve GUIDs.");
            Directory.CreateDirectory("Verification/equipment-20261016");
            File.WriteAllText("Verification/equipment-20261016/import.txt", report.ToString());
            return report.ToString();
        }
    }
}

