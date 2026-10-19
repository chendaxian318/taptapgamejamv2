using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HabitatIsland.Editor
{
    /// <summary>Prepares the supplied static v2 basket without altering its geometry or any scene.</summary>
    public static class HabitatBackBasketSetup
    {
        public const string Source = "Assets/HabitatIsland/Art/BackBasket/BackBasket_v2.glb";
        public const string Folder = "Assets/HabitatIsland/Resources/Models/BackBasket";
        public const string PrefabPath = Folder + "/Basket.prefab";
        public const string ResourcePath = "Models/BackBasket/Basket";
        public const string MountName = "BackMount", RimName = "BasketRim", SourceOriginName = "SourceBaseOrigin";

        static void ConfigureImporter()
        {
            AssetDatabase.ImportAsset(Source, ImportAssetOptions.ForceSynchronousImport);
            AssetImporter importer = AssetImporter.GetAtPath(Source);
            if (!importer) throw new InvalidOperationException("Basket GLB importer is unavailable.");
            var serialized = new SerializedObject(importer);
            var animation = serialized.FindProperty("importSettings.animationMethod");
            var names = serialized.FindProperty("importSettings.nodeNameMethod");
            if (animation == null || names == null) throw new InvalidOperationException("Expected glTFast importer settings.");
            bool changed = animation.intValue != 2 || names.intValue != 1;
            animation.intValue = 2; names.intValue = 1; // Mecanim + OriginalUnique; source has no clips.
            if (changed) { serialized.ApplyModifiedPropertiesWithoutUndo(); importer.SaveAndReimport(); }
        }

        static List<Vector3> Points(Transform space, Transform part)
        {
            var points = new List<Vector3>();
            foreach (MeshFilter filter in part.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 p = space.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z))
                        throw new InvalidOperationException("Basket contains a non-finite vertex.");
                    points.Add(p);
                }
            }
            if (points.Count == 0) throw new InvalidOperationException("Basket part has no mesh: " + part.name);
            return points;
        }

        static Bounds BoundsOf(IList<Vector3> points)
        {
            if (points.Count == 0) throw new InvalidOperationException("Cannot measure an empty basket part.");
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 point in points) bounds.Encapsulate(point);
            return bounds;
        }

        static Transform Part(GameObject model, string name)
            => model.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);

        static Transform Marker(Transform parent, string name, Vector3 position)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent, false); marker.localPosition = position;
            return marker;
        }

        static void CopyMaterials(GameObject model, StringBuilder report)
        {
            var copies = new Dictionary<Material, Material>();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material original = materials[i];
                    if (!original) throw new InvalidOperationException("Missing basket material: " + renderer.name);
                    if (!copies.TryGetValue(original, out Material material))
                    {
                        string safeName = string.Concat(original.name.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_'));
                        string path = Folder + "/Basket_" + safeName + ".mat";
                        if (!paths.Add(path)) throw new InvalidOperationException("Basket has duplicate source material names: " + original.name);
                        material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!material) { material = new Material(original); AssetDatabase.CreateAsset(material, path); }
                        else EditorUtility.CopySerialized(original, material);
                        HabitatIsland.EditorTools.HabitatTextureOptimization.Apply(material);
                        material.name = Path.GetFileNameWithoutExtension(path);
                        material.enableInstancing = true; EditorUtility.SetDirty(material);
                        copies.Add(original, material);
                        report.AppendLine("Material: " + path + "; shader=" + material.shader.name);
                        foreach (string property in material.GetTexturePropertyNames())
                        {
                            Texture texture = material.GetTexture(property);
                            if (texture) report.AppendLine("  " + property + ": " + texture.width + "x" + texture.height
                                + "; UV scale=" + material.GetTextureScale(property) + "; UV offset=" + material.GetTextureOffset(property));
                        }
                    }
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            }
            if (copies.Count != 6) throw new InvalidOperationException("Expected the supplied basket's six PBR materials; found " + copies.Count);
            report.AppendLine("Independent materials: " + copies.Count + "; embedded texture references, colors and PBR packing preserved.");
        }

        static string SourceHash()
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(Source))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        [MenuItem("Tools/Habitat Island/Art/Prepare Back Basket V2")]
        public static void PrepareMenu() { Debug.Log(Prepare()); }

        public static string Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before preparing the back basket.");
            if (!File.Exists(Source)) throw new FileNotFoundException("Copy the supplied basket v2 GLB into the project first.", Source);
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh(); ConfigureImporter();
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (!source) throw new InvalidOperationException("Basket v2 GLB import failed.");
            var clips = AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>().ToArray();
            if (clips.Length != 0 || source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 0)
                throw new InvalidOperationException("Expected the delivered static v2 model without skins or animation clips.");
            var report = new StringBuilder("Back basket v2: static model, unchanged source geometry and units.\n");
            report.AppendLine("Source: " + Source + "\nSHA256: " + SourceHash());
            var root = new GameObject("Basket");
            try
            {
                var pivot = new GameObject("Source model at back mount").transform;
                pivot.SetParent(root.transform, false);
                GameObject model = UnityEngine.Object.Instantiate(source, pivot, false);
                model.name = "Original basket v2";
                // Attachment follows the player parent; this source has no autonomous animation or physics.
                foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator);
                foreach (Animation animation in model.GetComponentsInChildren<Animation>(true)) UnityEngine.Object.DestroyImmediate(animation);
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
                var sourcePoints = Points(root.transform, model.transform);
                Bounds full = BoundsOf(sourcePoints);
                if (Mathf.Abs(full.size.y - .65f) > .002f || Mathf.Abs(full.min.y) > .002f)
                    throw new InvalidOperationException("Unexpected basket source units or up-axis: " + full);

                var backboard = new List<Vector3>();
                for (int i = 1; i <= 3; i++) backboard.AddRange(Points(root.transform, Part(model, "Backboard_Plank_" + i)));
                Bounds boards = BoundsOf(backboard);
                var bodyPoints = Points(root.transform, Part(model, "Basket_WovenBody"));
                Bounds woven = BoundsOf(bodyPoints);
                Bounds straps = BoundsOf(Points(root.transform, Part(model, "ShoulderStrap_L"))
                    .Concat(Points(root.transform, Part(model, "ShoulderStrap_R"))).ToArray());
                if (boards.center.z >= woven.center.z || straps.center.z >= boards.center.z)
                    throw new InvalidOperationException("Measured basket backboard/strap directions do not match the supplied -Z back side.");
                // Mount on the wearer-facing rear plane of the three boards, centered vertically.
                Vector3 mount = new Vector3(boards.center.x, boards.center.y, boards.min.z);
                // Use only the woven body's top 5 cm, never the whole model's shoulder-strap bounds.
                var rimPoints = bodyPoints.Where(p => p.y >= woven.max.y - .05f).ToArray();
                if (rimPoints.Length < 50) throw new InvalidOperationException("Basket opening could not be measured from its woven rim.");
                Bounds rim = BoundsOf(rimPoints);
                Vector3 rimCenter = new Vector3(rim.center.x, woven.max.y - .03f, rim.center.z);
                pivot.localPosition = -mount; // No rotation, scaling, remeshing or per-part adjustment.
                Marker(root.transform, MountName, Vector3.zero);
                Marker(root.transform, SourceOriginName, -mount);
                Marker(root.transform, RimName, rimCenter - mount);

                CopyMaterials(model, report);
                var prepared = Points(root.transform, model.transform);
                if (prepared.Count != sourcePoints.Count) throw new InvalidOperationException("Basket vertex count changed during preparation.");
                float error = 0f;
                for (int i = 0; i < prepared.Count; i++) error = Mathf.Max(error, Vector3.Distance(sourcePoints[i], prepared[i] + mount));
                if (error > .00001f) throw new InvalidOperationException("Basket geometry changed beyond the mount translation: " + error);
                int triangles = model.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh.triangles.Length / 3);
                report.AppendLine("Source bounds: " + full + "; triangles=" + triangles + "; sampled vertices=" + prepared.Count);
                report.AppendLine("Source back mount: " + mount.ToString("F6") + "; only outer translation=" + (-mount).ToString("F6"));
                report.AppendLine("Prefab axes: +Y up, +Z basket front away from the wearer; backboards/straps toward -Z. Source units retained, scale=1.");
                report.AppendLine("BackMount=(0,0,0); wearer component aligns prefab +Z with the character's backward direction.");
                report.AppendLine("BasketRim=" + (rimCenter - mount).ToString("F6") + "; derived from " + rimPoints.Length + " woven rim vertices.");
                report.AppendLine("SourceBaseOrigin=" + (-mount).ToString("F6") + "; prepared bounds=" + BoundsOf(prepared));
                report.AppendLine("Source clips=0, skins=0. No animation driver, collision or catch contents added.");
                if (!PrefabUtility.SaveAsPrefabAsset(root, PrefabPath)) throw new InvalidOperationException("Failed to save basket prefab.");
                AssetDatabase.SaveAssets();
                report.AppendLine("Prefab: " + PrefabPath + "; existing prefab/material GUIDs retained by updating their paths in place.");
                Directory.CreateDirectory("Verification/back-basket-20261016");
                File.WriteAllText("Verification/back-basket-20261016/import.txt", report.ToString());
                return report.ToString();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}

