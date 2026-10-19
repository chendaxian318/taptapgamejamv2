using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HabitatIsland.Editor
{
    /// <summary>Prepare the extracted shopkeeper net without rebuilding either harbor scene.</summary>
    public static class HabitatLandingNetSetup
    {
        const string Source = "Assets/HabitatIsland/Art/LandingNet/LandingNet.glb";
        const string Folder = "Assets/HabitatIsland/Resources/Models/Story";

        [MenuItem("Tools/Habitat Island/Art/Prepare Landing Net")]
        public static void Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play before preparing the asset.");
            AssetDatabase.ImportAsset(Source, ImportAssetOptions.ForceSynchronousImport);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (!source) throw new InvalidOperationException("LandingNet.glb import failed.");
            var root = new GameObject("Landing net • shopkeeper woven basket");
            try
            {
                var model = UnityEngine.Object.Instantiate(source, root.transform, false);
                model.name = "Original wood hoop and volumetric net";
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var animator in model.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var original = materials[i];
                        if (!original || !original.shader || original.shader.name.Contains("Error"))
                            throw new InvalidOperationException("Missing imported PBR material.");
                        string path = Folder + "/LandingNet_" + i + ".mat";
                        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!material) { material = new Material(original); AssetDatabase.CreateAsset(material, path); }
                        else EditorUtility.CopySerialized(original, material);
                        HabitatIsland.EditorTools.HabitatTextureOptimization.Apply(material);
                        material.name = "Landing net original PBR"; material.enableInstancing = true;
                        EditorUtility.SetDirty(material); materials[i] = material;
                    }
                    renderer.sharedMaterials = materials;
                }
                Bounds bounds = HabitatEquipmentSetup.BakedBounds(root);
                if (bounds.size.z < .4f || bounds.size.y < 1f || bounds.size.x < .6f)
                    throw new InvalidOperationException("Net lost its basket depth or socket dimensions: " + bounds);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/LandingNet.prefab");
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory("Verification/landing-net-20261016");
                File.WriteAllText("Verification/landing-net-20261016/import.txt",
                    "Source: " + Source + "\nBounds: " + bounds + "\nGrip origin: local zero; hoop +Y; original net geometry/UV; capped extended handle.\nNo colliders or independent animation; Timeline owns socket rotation and visibility.\n");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}

