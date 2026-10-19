using System;
using HabitatIsland;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

public static class JourneySetup
{
    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play before setup.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/HabitatIsland/Scenes/HabitatIsland.unity" || scene.isDirty)
            throw new Exception("Open the clean main scene before setup.");
        HabitatGame game = null;
        foreach (var root in scene.GetRootGameObjects())
            if (!game) game = root.GetComponentInChildren<HabitatGame>(true);
        if (!game) throw new Exception("Main game not found.");
        var opening = AssetDatabase.LoadAssetAtPath<TimelineAsset>("Assets/HabitatIsland/Timelines/HarborPrototype/DayOneOpening.playable");
        var morning = AssetDatabase.LoadAssetAtPath<TimelineAsset>("Assets/HabitatIsland/Timelines/HarborPrototype/MorningWindow.playable");
        if (!opening || !morning) throw new Exception("Existing Timeline asset missing.");
        var story = game.GetComponent<HabitatMainStory>();
        if (!story) story = game.gameObject.AddComponent<HabitatMainStory>();
        story.openingTimeline = opening; story.morningTimeline = morning;
        game.Story = story; game.playOpeningOnNewGame = true;
        EditorUtility.SetDirty(story); EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
        AssetDatabase.SaveAssets();
        return "Main scene now references the original opening and morning Timelines; automatic opening enabled. Existing world, models and Timeline assets preserved.";
    }
}
