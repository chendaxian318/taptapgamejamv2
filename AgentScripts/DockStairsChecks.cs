using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HabitatIsland;
using HabitatIsland.Editor;

public static class DockStairsChecks
{
    const string Folder = "Logs/dock-stairs-20261015";
    public static string VerifySavedScene()
    {
        var active = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(HabitatDockPrototypeSetup.ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(HabitatDockPrototypeSetup.ScenePath, OpenSceneMode.Additive);
        try
        {
            var filters = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true)).ToArray();
            var colliders = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshCollider>(true)).ToArray();
            int updated = filters.Count(f => AssetDatabase.GetAssetPath(f.sharedMesh) == HabitatWoodenDockSetup.Source);
            int physical = colliders.Count(c => AssetDatabase.GetAssetPath(c.sharedMesh) == HabitatWoodenDockSetup.Source);
            int stale = filters.Count(f => AssetDatabase.GetAssetPath(f.sharedMesh) == HabitatWoodenDockSetup.OriginalSource);
            if (updated != 1 || physical != 1 || stale != 0) throw new Exception("Saved harbor references are inconsistent.");
            string result = "Saved HarborPrototype: edited render mesh=1; edited collider=1; old dock references=0. Active scene preserved: " + active.path;
            File.WriteAllText(Folder + "/scene-checks.txt", result);
            return result;
        }
        finally { SceneManager.SetActiveScene(active); if (opened) EditorSceneManager.CloseScene(scene,true); }
    }
    public static string Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play first.");
        var active = SceneManager.GetActiveScene();
        if (active.isDirty) throw new Exception("The active scene has unsaved edits.");
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(HabitatWoodenDockSetup.OriginalSource);
        var edited = AssetDatabase.LoadAssetAtPath<GameObject>(HabitatWoodenDockSetup.Source);
        if (!original || !edited) throw new Exception("Missing dock source/variant.");
        Mesh oldMesh = original.GetComponentInChildren<MeshFilter>().sharedMesh;
        Mesh newMesh = edited.GetComponentInChildren<MeshFilter>().sharedMesh;
        if ((long)newMesh.GetIndexCount(0) != 39025 * 3) throw new Exception("Unexpected edited triangle count.");
        HabitatWoodenDockSetup.Prepare();
        // The prototype contains a serialized clone rather than a connected prefab instance.
        // Swap only its mesh/collider references; preserve Timeline, actors and other objects.
        string scenePath = HabitatDockPrototypeSetup.ScenePath;
        Scene harbor = SceneManager.GetSceneByPath(scenePath);
        bool opened = !harbor.IsValid() || !harbor.isLoaded;
        if (opened) harbor = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        if (harbor.isDirty) throw new Exception("Harbor scene has unsaved edits.");
        int filters = 0, colliders = 0;
        try
        {
            foreach (var root in harbor.GetRootGameObjects())
            {
                foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
                    if (f.sharedMesh == oldMesh) { f.sharedMesh = newMesh; EditorUtility.SetDirty(f); filters++; }
                foreach (var c in root.GetComponentsInChildren<MeshCollider>(true))
                    if (c.sharedMesh == oldMesh) { c.sharedMesh = newMesh; EditorUtility.SetDirty(c); colliders++; }
            }
            if (filters > 0 || colliders > 0)
            {
                EditorSceneManager.MarkSceneDirty(harbor);
                if (!EditorSceneManager.SaveScene(harbor)) throw new Exception("Harbor save failed.");
            }
        }
        finally
        {
            SceneManager.SetActiveScene(active);
            if (opened) EditorSceneManager.CloseScene(harbor, true);
        }
        return "Prefab rebuilt; HarborPrototype mesh swaps=" + filters + "; collider swaps=" + colliders + ". " + Verify();
    }
    public static string Verify()
    {
        Directory.CreateDirectory(Folder);
        var lines = new List<string>();
        void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL: " + label); lines.Add("PASS: " + label); }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HabitatWoodenDockSetup.PrefabPath);
        var instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var filter = instance.GetComponentInChildren<MeshFilter>();
            var mesh = filter.sharedMesh;
            var col = instance.GetComponentInChildren<MeshCollider>();
            Check(AssetDatabase.GetAssetPath(mesh) == HabitatWoodenDockSetup.Source, "prefab references the stairs-removed GLB");
            Check(col.sharedMesh == mesh, "renderer and collider use the identical edited mesh");
            Check(mesh.GetIndexCount(0) == 39025 * 3, "39,025 triangles: old stairs removed and deck patched");
            var material = instance.GetComponentInChildren<Renderer>().sharedMaterial;
            var color = material.GetTexture("baseColorTexture");
            Check(color && color.width == 8192 && color.height == 8192, "existing 8K color atlas retained");
            Physics.SyncTransforms();
            int hits = 0; float min = float.PositiveInfinity, max = float.NegativeInfinity;
            for (int ix = 0; ix < 15; ix++) for (int iz = 0; iz < 17; iz++)
            {
                float x = Mathf.Lerp(.245f, .367f, ix / 14f), z = Mathf.Lerp(-.325f, -.215f, iz / 16f);
                var point = HabitatImportedDockLayout.FromSource(x, -.0748f, z);
                // Start below retained plant leaves and awning; sample the actual repaired floor.
                bool hit = col.Raycast(new Ray(point + Vector3.up * (.006f * HabitatImportedDockLayout.SourceScale), Vector3.down), out var h, .4f);
                Check(hit, "solid deck ray " + ix + "," + iz);
                float sourceY = (h.point.y - HabitatImportedDockLayout.SourceOffset.y) / HabitatImportedDockLayout.SourceScale;
                Check(sourceY > -.081f && sourceY < -.070f, "repaired floor height without holes at " + ix + "," + iz);
                hits++; min = Mathf.Min(min,sourceY); max = Mathf.Max(max,sourceY);
            }
            for (int i=0;i<=32;i++)
            {
                // Cross the former stair footprint from the home approach toward the front deck.
                // Stop with full actor clearance before the retained house wall at x ~ .40.
                float x = Mathf.Lerp(.22f,.36f,i/32f);
                Vector3 p = HabitatImportedDockLayout.FromSource(x,-.0748f,-.275f);
                Check(HabitatImportedDockLayout.IsWalkable(p,.4f), "player clearance across repaired deck " + i);
            }
            Check(!HabitatImportedDockLayout.IsWalkable(HabitatImportedDockLayout.FromSource(.65f,0,-.43f),.4f), "house interior remains blocked");
            Check(!HabitatImportedDockLayout.IsWalkable(HabitatImportedDockLayout.FromSource(1.2f,0,-.27f),.4f), "off-deck water remains blocked");
            lines.Add("Height grid: " + hits + " rays; source Y range=" + min + ".." + max);
            instance.transform.localScale = Vector3.one * .7f;
            var harbor = new HabitatMapHarbor();
            // Width zero disables the separate boarding bridge, isolating the real dock collider.
            harbor.Bind(instance.transform, instance.transform, Vector3.zero, Vector3.zero, 0f);
            Physics.SyncTransforms();
            for (int i=0;i<=32;i++)
            {
                Vector3 p = HabitatImportedDockLayout.FromSource(Mathf.Lerp(.22f,.36f,i/32f),-.0748f,-.275f);
                Check(harbor.IsWalkable(instance.transform.TransformPoint(p)), "actual map ground sampler and player clearance " + i);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
        File.WriteAllLines(Folder + "/unity-checks.txt",lines);
        return lines.Count + " checks/report lines saved to " + Folder + "/unity-checks.txt";
    }
}
