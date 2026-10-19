using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HabitatIsland.Editor
{
    /// <summary>Creates a reusable static resident while leaving the supplied GLB intact.</summary>
    public static class HabitatShrimpSetup
    {
        public const string Source = "Assets/HabitatIsland/Art/MeshyShrimp/MeshyShrimp.glb";
        // Historical static model is retained for editing, outside player Resources.
        public const string Folder = "Assets/HabitatIsland/Art/Legacy/MeshyShrimp";
        public const string PrefabPath = Folder + "/Shrimp.prefab";
        public const float Footprint = .80f;
        // Imported model orientation, verified in the three-angle preview.
        public const float SourceYaw = 0f;

        public static Bounds MeshBounds(GameObject root)
        {
            bool found = false;
            Bounds result = new Bounds();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                Matrix4x4 matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 p = matrix.MultiplyPoint3x4(vertex);
                    if (!found) { result = new Bounds(p, Vector3.zero); found = true; }
                    else result.Encapsulate(p);
                }
            }
            if (!found) throw new InvalidOperationException("Shrimp contains no static mesh");
            return result;
        }

        public static string Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before preparing assets");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (!source) throw new InvalidOperationException("Import the shrimp GLB first");
            var root = new GameObject("Meshy Shrimp");
            try
            {
                GameObject instance = UnityEngine.Object.Instantiate(source, root.transform, false);
                instance.name = "Original textured mesh";
                instance.transform.localRotation = Quaternion.Euler(0, SourceYaw, 0) * instance.transform.localRotation;
                Bounds bounds = MeshBounds(root);
                float span = Mathf.Max(bounds.size.x, bounds.size.z);
                if (span < .001f) throw new InvalidOperationException("Invalid shrimp bounds");
                float scale = Footprint / span;
                instance.transform.localScale *= scale;
                instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
                int slots = 0;
                var report = new StringBuilder();
                report.AppendLine("Source: " + Source);
                report.AppendLine("Uniform scale: " + scale.ToString("F7"));
                report.AppendLine("Source yaw: " + SourceYaw);
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material original = materials[i];
                        if (!original) throw new InvalidOperationException("Missing shrimp material");
                        string path = Folder + "/Shrimp" + slots++ + ".mat";
                        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!material) { material = new Material(original); AssetDatabase.CreateAsset(material, path); }
                        else EditorUtility.CopySerialized(original, material);
                        HabitatIsland.EditorTools.HabitatTextureOptimization.Apply(material);
                        material.name = "Meshy Shrimp original PBR " + i;
                        // Keep the glTF shader's G-roughness/B-metallic packing and original UV transform.
                        material.enableInstancing = true;
                        EditorUtility.SetDirty(material);
                        materials[i] = material;
                        report.AppendLine("Material: " + material.shader.name);
                        foreach (string property in material.GetTexturePropertyNames())
                        {
                            Texture texture = material.GetTexture(property);
                            if (texture) report.AppendLine(property + ": " + texture.name + " " + texture.width + "x" + texture.height);
                        }
                    }
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                    UnityEngine.Object.DestroyImmediate(collider);
                Bounds finalBounds = MeshBounds(root);
                report.AppendLine("Normalized bounds: " + finalBounds);
                report.AppendLine("Static mesh only; no rig or animation added.");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory("Verification");
                File.WriteAllText("Verification/meshy-shrimp-import.txt", report.ToString());
                return report.ToString();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static string Preview()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab) throw new InvalidOperationException("Prepare the shrimp first");
            Directory.CreateDirectory("Verification");
            for (int i = 0; i < 3; i++)
            {
                var preview = new PreviewRenderUtility(true);
                Texture2D picture = null;
                try
                {
                    GameObject instance = UnityEngine.Object.Instantiate(prefab);
                    preview.AddSingleGO(instance);
                    Bounds bounds = MeshBounds(instance);
                    Vector3 center = bounds.center;
                    Quaternion angle = Quaternion.Euler(28, i * 90, 0);
                    preview.camera.transform.SetPositionAndRotation(center + angle * new Vector3(0, 0, -3), angle);
                    preview.camera.orthographic = true;
                    preview.camera.orthographicSize = .60f;
                    preview.camera.nearClipPlane = .01f;
                    preview.camera.farClipPlane = 10f;
                    preview.camera.clearFlags = CameraClearFlags.SolidColor;
                    preview.camera.backgroundColor = new Color(.12f, .20f, .22f, 1);
                    preview.lights[0].transform.rotation = Quaternion.Euler(40, -35, 0);
                    preview.lights[0].intensity = 1.4f;
                    preview.lights[1].transform.rotation = Quaternion.Euler(30, 145, 0);
                    preview.lights[1].intensity = 1.0f;
                    preview.ambientColor = new Color(.5f, .5f, .5f);
                    preview.BeginStaticPreview(new Rect(0, 0, 720, 720));
                    preview.Render(true);
                    picture = preview.EndStaticPreview();
                    File.WriteAllBytes("Verification/meshy-shrimp-angle-" + i + ".png", picture.EncodeToPNG());
                }
                finally { if (picture) UnityEngine.Object.DestroyImmediate(picture); preview.Cleanup(); }
            }
            return "Saved three shrimp preview angles in Verification";
        }
    }
}

