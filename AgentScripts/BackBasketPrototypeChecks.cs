using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEditor;
using UnityEngine;

// Execute await BackBasketPrototypeChecks.Run() in HarborPrototype Play.
// Advances real frames; never opens/saves a scene or imports an asset. Restores runtime state.
public static class BackBasketPrototypeChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly StringBuilder report = new StringBuilder();
    static int checks;
    static T Get<T>(object instance, string name) => (T)instance.GetType().GetField(name, Hidden).GetValue(instance);
    static void Call(object instance, string name) => instance.GetType().GetMethod(name, Hidden).Invoke(instance, null);
    static void Check(bool pass, string label)
    {
        report.AppendLine((pass ? "PASS " : "FAIL ") + (++checks) + ": " + label);
        if (!pass) throw new InvalidOperationException(label);
    }
    static async Task Frames(int count)
    {
        int first = Time.frameCount; double deadline = EditorApplication.timeSinceStartup + 8;
        while (Time.frameCount - first < count)
        {
            if (!Application.isPlaying || EditorApplication.timeSinceStartup > deadline)
                throw new InvalidOperationException("Play frames stopped advancing.");
            await Task.Delay(12);
        }
    }
    struct Pose
    {
        public Transform node; public Vector3 position, scale; public Quaternion rotation; public bool active;
        public Pose(Transform t) { node = t; position = t.localPosition; rotation = t.localRotation; scale = t.localScale; active = t.gameObject.activeSelf; }
        public void Restore() { if (!node) return; node.localPosition = position; node.localRotation = rotation; node.localScale = scale; node.gameObject.SetActive(active); }
    }
    static async Task FinishNaturally(HabitatDockPrototype dock)
    {
        // Seek near the end, then let the real PlayableDirector stop callback finish it.
        dock.director.time = Math.Max(0, dock.director.duration - .08);
        dock.director.Evaluate();
        double deadline = EditorApplication.timeSinceStartup + 5;
        while (dock.IsSequencePlaying && EditorApplication.timeSinceStartup < deadline) await Frames(1);
        Check(!dock.IsSequencePlaying, "Timeline reaches its natural stop callback");
        await Frames(2);
    }

    public static async Task<string> Run()
    {
        var dock = UnityEngine.Object.FindFirstObjectByType<HabitatDockPrototype>();
        if (!Application.isPlaying || !dock || dock.gameObject.scene.name != HabitatDockPrototype.HarborSceneName)
            throw new InvalidOperationException("Open HarborPrototype in Play before running this check.");
        await Frames(2); // Awake's old basket destruction has completed.
        checks = 0; report.Clear();
        var fields = typeof(HabitatDockPrototype).GetFields(Hidden).Where(f => !f.IsInitOnly && !f.IsStatic
            && (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string) || f.FieldType == typeof(Vector3)))
            .ToDictionary(f => f, f => f.GetValue(dock));
        var sessionField = typeof(HabitatDockPrototype).GetField("session", BindingFlags.Static | BindingFlags.NonPublic);
        object session = sessionField.GetValue(null); string sessionJson = session == null ? null : JsonUtility.ToJson(session);
        var poses = dock.GetComponentsInChildren<Transform>(true).Select(t => new Pose(t)).ToArray();
        var player = dock.PlayerVisual; var originalAsset = dock.director.playableAsset;
        double originalTime = dock.director.time; bool wasPlaying = dock.IsSequencePlaying, wasPaused = dock.IsSequencePaused;
        float cameraSize = dock.harborCamera.orthographicSize, sunIntensity = dock.sun ? dock.sun.intensity : 0;
        var windowLight = Get<Light>(dock, "windowLight"); float windowIntensity = windowLight ? windowLight.intensity : 0;
        float animatorSpeed = player && player.Animator ? player.Animator.speed : 1;
        string before = dock.GetStatusJson();
        try
        {
            Check(player && player.BackBasketRoot && player.BackBasketRim, "runtime traveler supplies a persistent basket and rim");
            Transform basket = player.BackBasketRoot, rim = player.BackBasketRim, contents = Get<Transform>(dock, "catchRoot");
            var nodes = dock.GetComponentsInChildren<Transform>(true);
            Check(nodes.Count(t => t.name == "Traveler back basket v2") == 1, "exactly one permanent basket after Awake migration");
            Check(!nodes.Any(t => t.name == "Basket body" || t.name == "Basket rim" || t.name.StartsWith("Full catch basket", StringComparison.Ordinal)),
                "saved geometric basket has been removed, including inactive remnants");
            Check(contents && contents.parent == rim && contents != basket && contents != rim
                && contents.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Catch fish ", StringComparison.Ordinal)) == 4,
                "four temporary fish belong to a separate direct child of BackBasketRim");

            Check(dock.RestartFirstDayForChecks(), "opening replay begins"); await Frames(2);
            Check(contents.gameObject.activeInHierarchy && basket.gameObject.activeInHierarchy, "opening shows fish inside the permanent basket");
            await FinishNaturally(dock);
            Check(dock.IntroCompleted && !contents.gameObject.activeSelf && basket.gameObject.activeInHierarchy
                && player.BackBasketRoot == basket && dock.HasControl, "natural opening completion hides only fish and restores control");

            Check(dock.RestartFirstDayForChecks(), "opening can replay without creating a second basket"); await Frames(2);
            Check(dock.SkipSequence(), "opening skip is accepted"); await Frames(2);
            Check(!contents.gameObject.activeSelf && basket.gameObject.activeInHierarchy && player.BackBasketRim == rim
                && dock.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Traveler back basket v2") == 1,
                "skip preserves exactly the same permanent basket and hides temporary contents");

            int day = dock.Day;
            Check(dock.PlacePlayerForChecks(HabitatDockPrototype.HomeApproach) && dock.TryOpenInteraction(HabitatDockInteraction.Home)
                && dock.TryRest(), "near-home interaction starts the morning sequence"); await Frames(2);
            Transform actor = Get<Transform>(dock, "playerRoot");
            Check(!actor.gameObject.activeInHierarchy && !basket.gameObject.activeInHierarchy && !contents.gameObject.activeInHierarchy
                && basket.IsChildOf(actor) && contents.parent == rim, "morning hides actor and attached basket together; nothing is left floating");
            await FinishNaturally(dock);
            Check(dock.Day == day + 1 && dock.HasControl && actor.gameObject.activeInHierarchy && basket.gameObject.activeInHierarchy
                && !contents.gameObject.activeSelf && player.BackBasketRoot == basket,
                "morning completion restores the same basket and actor without temporary fish");
            report.AppendLine("PASS: " + checks + " prototype checks. Final tested status: " + dock.GetStatusJson());
        }
        catch (Exception error) { report.AppendLine("FAIL " + error); throw new InvalidOperationException(report.ToString(), error); }
        finally
        {
            // Stop without advancing a day, then restore the prior session and presentation.
            if (dock)
            {
                typeof(HabitatDockPrototype).GetField("<IsSequencePlaying>k__BackingField", Hidden).SetValue(dock, false);
                dock.director.Stop();
                foreach (var pair in fields) pair.Key.SetValue(dock, pair.Value);
                if (session != null) JsonUtility.FromJsonOverwrite(sessionJson, session);
                sessionField.SetValue(null, session);
                dock.director.playableAsset = originalAsset; dock.director.time = originalTime;
                if (wasPlaying) { dock.director.Play(); dock.director.time = originalTime; dock.director.Evaluate(); if (wasPaused) dock.director.Pause(); }
                foreach (Pose pose in poses) pose.Restore();
                dock.harborCamera.orthographicSize = cameraSize;
                if (dock.sun) dock.sun.intensity = sunIntensity;
                if (windowLight) windowLight.intensity = windowIntensity;
                if (player && player.Animator) player.Animator.speed = animatorSpeed;
                Call(dock, "RefreshModal"); Call(dock, "RefreshHud");
                report.AppendLine("RESTORED prior runtime session and presentation. Before: " + before);
            }
            string folder = Path.GetFullPath("Logs/back-basket-20261016"); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "prototype-checks.txt"), report.ToString(), new UTF8Encoding(false));
        }
        return report.ToString();
    }
}
