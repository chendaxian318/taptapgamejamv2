using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HabitatIsland;
using HabitatIsland.Editor;

public static class FeedbackSetup
{
    public static string Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play before migration.");
        var active=SceneManager.GetActiveScene();
        if(active.isDirty)throw new Exception("Unsaved scene changes.");
        var scene=EditorSceneManager.OpenScene(HabitatDockPrototypeSetup.ScenePath,OpenSceneMode.Additive);
        try
        {
            HabitatDockPrototype dock=null;
            foreach(var root in scene.GetRootGameObjects())if(!dock)dock=root.GetComponentInChildren<HabitatDockPrototype>(true);
            if(!dock)throw new Exception("Dock missing.");
            var serialized=new SerializedObject(dock);
            var prop=serialized.FindProperty("editorTravelerPreview");
            var old=prop.objectReferenceValue as GameObject;
            if(old)
            {
                var next=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HabitatTravelerSetup.PrefabPath),old.transform.parent,false);
                next.name=old.name;next.transform.localPosition=old.transform.localPosition;next.transform.localRotation=old.transform.localRotation;
                prop.objectReferenceValue=next; serialized.ApplyModifiedPropertiesWithoutUndo();
                UnityEngine.Object.DestroyImmediate(old);
            }
            dock.ApplyPlayerPresentationDefaults();EditorUtility.SetDirty(dock);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed.");
            AssetDatabase.SaveAssets();
            return "Harbor saved: empty hands preview, 0.75 size, walk speed 2.5; Timeline and edited dock preserved.";
        }
        finally { SceneManager.SetActiveScene(active);EditorSceneManager.CloseScene(scene,true); }
    }
}
