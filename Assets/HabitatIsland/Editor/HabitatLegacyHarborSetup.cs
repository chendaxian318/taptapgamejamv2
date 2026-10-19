using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HabitatIsland.Editor
{
    /// <summary>Preserves the standalone demonstration's original combined boat and dock.</summary>
    public static class HabitatLegacyHarborSetup
    {
        public const string Source = "Assets/HabitatIsland/Art/WoodenDock/WoodenDock_NoStairs.glb";
        public const string PrefabPath = "Assets/HabitatIsland/Resources/Models/WoodenDock/HarborPrototypeLegacy.prefab";
        const string BoatFreeSource = "Assets/HabitatIsland/Art/WoodenDock/WoodenDock_NoStairs_NoBoat.glb";

        [MenuItem("Tools/Habitat Island/Art/Preserve Legacy Harbor Demonstration")]
        public static void Build() { Debug.Log(Prepare()); }

        public static string Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play before preparing the legacy harbor prefab.");
            AssetDatabase.ImportAsset(Source, ImportAssetOptions.ForceSynchronousImport);
            GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (!imported) throw new FileNotFoundException("Original NoStairs harbor model is missing.", Source);
            var filters = imported.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length != 1 || !filters[0].sharedMesh)
                throw new InvalidOperationException("Expected one original combined harbor mesh.");
            Mesh original = filters[0].sharedMesh;
            if (original.subMeshCount != 1 || original.GetIndexCount(0) != 39025 * 3)
                throw new InvalidOperationException("Expected 39,025 original harbor triangles, including the original boat.");

            GameObject root = PrefabUtility.LoadPrefabContents(HabitatWoodenDockSetup.PrefabPath);
            try
            {
                int replacedFilters = 0, replacedColliders = 0;
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    string source = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    if (source != Source && source != BoatFreeSource) continue;
                    filter.sharedMesh = original; replacedFilters++;
                }
                foreach (MeshCollider collider in root.GetComponentsInChildren<MeshCollider>(true))
                {
                    string source = AssetDatabase.GetAssetPath(collider.sharedMesh);
                    if (source != Source && source != BoatFreeSource) continue;
                    collider.sharedMesh = original; replacedColliders++;
                }
                if (replacedFilters != 1 || replacedColliders != 1)
                    throw new InvalidOperationException("Expected one existing harbor renderer and collider to restore.");
                if (!root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                    .Any(m => m && m.HasProperty("baseColorTexture") && m.GetTexture("baseColorTexture")
                        && m.GetTexture("baseColorTexture").width == 8192))
                    throw new InvalidOperationException("The existing enhanced 8K harbor material must be retained.");
                root.name = "Legacy harbor demonstration - original combined boat";
                string previousGuid = AssetDatabase.AssetPathToGUID(PrefabPath);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                if (!saved) throw new IOException("Could not save the separate legacy harbor prefab.");
                if (!string.IsNullOrEmpty(previousGuid) && AssetDatabase.AssetPathToGUID(PrefabPath) != previousGuid)
                    throw new InvalidOperationException("Legacy prefab GUID changed unexpectedly.");
                AssetDatabase.SaveAssets();
                string report = "PASS: separate legacy HarborPrototype prefab uses original NoStairs GLB with its combined boat.\n"
                    + "PASS: original 39,025 triangles; renderer and collider restored together.\n"
                    + "PASS: existing normalized transforms and enhanced 8K materials preserved.\n"
                    + "PASS: main Harbor.prefab and all scenes remain unchanged.\n"
                    + "Prefab: " + PrefabPath + "\n";
                Directory.CreateDirectory("Verification/boat-shop-20261016");
                File.WriteAllText("Verification/boat-shop-20261016/legacy-harbor-import.txt", report);
                return report;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
