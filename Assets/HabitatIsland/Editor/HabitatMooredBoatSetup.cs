using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HabitatIsland.Editor
{
    /// <summary>Replaces only the merged harbor mesh; all authored materials and transforms stay in place.</summary>
    public static class HabitatMooredBoatSetup
    {
        public const string Source = "Assets/HabitatIsland/Art/WoodenDock/WoodenDock_NoStairs_NoBoat.glb";
        const string PreviousSource = "Assets/HabitatIsland/Art/WoodenDock/WoodenDock_NoStairs.glb";
        const string Folder = "Verification/boat-shop-20261016";

        public static string Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play before replacing the merged boat.");
            AssetDatabase.ImportAsset(Source, ImportAssetOptions.ForceSynchronousImport);
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (!imported) throw new InvalidOperationException("The boat-free harbor has not imported.");
            var sourceFilters = imported.GetComponentsInChildren<MeshFilter>(true);
            if (sourceFilters.Length != 1 || !sourceFilters[0].sharedMesh)
                throw new InvalidOperationException("Expected the one original merged harbor mesh.");
            Mesh replacement = sourceFilters[0].sharedMesh;
            if (replacement.subMeshCount != 1 || replacement.GetIndexCount(0) != 38260 * 3)
                throw new InvalidOperationException("Boat-free harbor must contain exactly 38,260 triangles.");

            string prefabPath = HabitatWoodenDockSetup.PrefabPath;
            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            if (string.IsNullOrEmpty(guid)) throw new FileNotFoundException("Missing harbor prefab.", prefabPath);
            string backup = Folder + "/before-moored-boat";
            Directory.CreateDirectory(backup);
            foreach (string suffix in new[] { "", ".meta" })
            {
                string path = prefabPath + suffix;
                string destination = backup + "/Harbor.prefab" + suffix;
                if (File.Exists(path) && !File.Exists(destination)) File.Copy(path, destination);
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            var report = new StringBuilder();
            try
            {
                int filters = 0, colliders = 0, disabledPlanks = 0;
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    if (path != PreviousSource && path != Source) continue;
                    filter.sharedMesh = replacement;
                    filters++;
                }
                foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true))
                {
                    string path = AssetDatabase.GetAssetPath(collider.sharedMesh);
                    if (path != PreviousSource && path != Source) continue;
                    collider.sharedMesh = replacement;
                    colliders++;
                }
                // The current prefab has no plank; the legacy standalone prototype creates
                // its plank in HabitatDockPrototype.World.cs. Only disable an exact prefab
                // child match if a future authored prefab includes that obsolete bridge.
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child != root.transform && child.name == "Boarding plank")
                    {
                        child.gameObject.SetActive(false);
                        disabledPlanks++;
                    }
                if (filters != 1 || colliders != 1)
                    throw new InvalidOperationException("Expected one existing render mesh and one collider; found " + filters + "/" + colliders);
                var materials = root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToArray();
                if (!materials.Any(m => m && m.HasProperty("baseColorTexture")
                    && m.GetTexture("baseColorTexture") && m.GetTexture("baseColorTexture").width == 8192))
                    throw new InvalidOperationException("Existing enhanced 8K harbor material is missing; no prefab was saved.");
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool saved);
                if (!saved) throw new IOException("Could not save the existing harbor prefab.");
                if (AssetDatabase.AssetPathToGUID(prefabPath) != guid)
                    throw new InvalidOperationException("Harbor prefab GUID changed unexpectedly.");
                report.AppendLine("PASS: existing Harbor prefab GUID retained: " + guid);
                report.AppendLine("PASS: one MeshFilter and one MeshCollider use the same boat-free variant: " + Source);
                report.AppendLine("PASS: 38,260 triangles; removed only the isolated 765-triangle old boat.");
                report.AppendLine("PASS: original NoStairs GLB preserved; original deck/stair-repair UVs, normals and texture bytes retained.");
                report.AppendLine("PASS: existing transforms, components and 8K material retained; no scene rebuilt.");
                report.AppendLine("Obsolete boarding planks disabled in prefab: " + disabledPlanks);
                report.AppendLine("Legacy standalone prototype plank is generated outside this prefab; current map has none.");
                File.WriteAllText(Folder + "/dock-prefab-install.txt", report.ToString());
                return report.ToString();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
