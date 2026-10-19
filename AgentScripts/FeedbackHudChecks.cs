using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Run serially through Unity's C# execution API after the project compiles, in the main scene.
// The report distinguishes actual input/scene integration from the visual screenshot for review.
public static class FeedbackHudChecks
{
    static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly StringBuilder report = new StringBuilder();
    static int count;
    static string Folder => Path.GetFullPath("Logs/feedback-20261015");
    static void Check(bool pass, string text)
    {
        report.AppendLine((pass ? "PASS " : "FAIL ") + (++count) + ": " + text);
        if (!pass) throw new Exception(text);
    }
    static T Get<T>(object instance, string name) => (T)instance.GetType().GetField(name, Fields).GetValue(instance);
    static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Fields).SetValue(instance, value);
    static Dictionary<FieldInfo, object> Scalars(object instance) => instance.GetType().GetFields(Fields)
        .Where(f => !f.IsInitOnly && !f.IsStatic && (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string)))
        .ToDictionary(f => f, f => f.GetValue(instance));
    static void RestoreScalars(object instance, Dictionary<FieldInfo, object> saved)
    { foreach (var entry in saved) entry.Key.SetValue(instance, entry.Value); }
    static string State(HabitatGame game) => JsonUtility.ToJson(game.State);
    static async Task Frames(int frames)
    {
        int start = Time.frameCount;
        double deadline = UnityEditor.EditorApplication.timeSinceStartup + 8;
        while (Time.frameCount - start < frames)
        {
            if (!Application.isPlaying || UnityEditor.EditorApplication.timeSinceStartup > deadline)
                throw new Exception("Play-mode frames did not advance.");
            await Task.Delay(12);
        }
    }
    static async Task Idle(HabitatGame game)
    {
        double deadline = UnityEditor.EditorApplication.timeSinceStartup + 8;
        while (game.Busy && UnityEditor.EditorApplication.timeSinceStartup < deadline) await Frames(1);
        Check(!game.Busy, "movement transition finishes after HUD interaction attempts");
    }
    static void Refresh(HabitatHud hud)
    { Set(hud, "nextRefresh", 0f); hud.Refresh(); Canvas.ForceUpdateCanvases(); }

    public static async Task<string> Run()
    {
        HabitatGame game = HabitatGame.Instance;
        if (!Application.isPlaying || !game || game.Busy || (game.Fishing && game.Fishing.IsOpen))
            throw new InvalidOperationException("Run on an idle main-island player; finish movement/fishing first.");
        HabitatHud hud = game.Hud;
        HabitatState original = game.State;
        string originalJson = State(game);
        Tuning originalTuning = game.tuning;
        var history = Get<Stack<HabitatState>>(game, "history");
        var undo = history.ToArray();
        var gameScalars = Scalars(game);
        var hudScalars = Scalars(hud);
        var originalUi = hud.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.gameObject, t => t.gameObject.activeSelf);
        var map = Get<HabitatMapSelectionPanel>(hud, "mapSelection");
        int originalSelectedMap = map.SelectedLevel;
        string[] cameraFields = { "cameraYaw", "cameraSize", "cameraTarget", "cameraTargetSmooth", "followPlayer", "overviewCamera" };
        var camera = cameraFields.ToDictionary(name => name, name => typeof(HabitatView).GetField(name, Fields).GetValue(game.View));
        var previousKeyboard = Keyboard.current;
        var background = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        var selection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        Keyboard keyboard = null;
        count = 0; report.Clear(); Directory.CreateDirectory(Folder);
        bool passed = false;
        try
        {
            game.tuning = JsonUtility.FromJson<Tuning>(JsonUtility.ToJson(originalTuning));
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("Feedback HUD acceptance keyboard"); keyboard.MakeCurrent();
            game.LoadLevel(3); game.paused = true;
            await Frames(3); Refresh(hud);

            var transforms = hud.GetComponentsInChildren<RectTransform>(true);
            Check(!transforms.Any(t => t.name == "Movement hint"), "the old persistent bottom movement hint is absent");
            RectTransform tools = transforms.First(t => t.name == "Keeper tools");
            Check(!tools.gameObject.activeSelf && tools.anchorMin.y == 1 && tools.anchorMax.y == 1,
                "tool palette starts folded and is anchored at the left top instead of the bottom");
            RectTransform toast = transforms.First(t => t.name == "Island message");
            Check(toast.anchorMin.y == 1 && toast.anchorMax.y == 1, "temporary state messages appear at the top");
            Check(!transforms.Any(t => t.parent && t.parent.name == "Habitat HUD" && t.gameObject.activeInHierarchy
                && t.anchorMin.y == 0 && t.anchorMax.y == 0), "no active permanent main-HUD panel occupies the bottom");
            Button toolsToggle = hud.GetComponentsInChildren<Button>(true).First(b => b.name == "Toggle keeper tools");
            toolsToggle.onClick.Invoke();
            Check(tools.gameObject.activeSelf && tools.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Tool ")) == 5,
                "all five gameplay tools remain reachable through the folded palette");
            toolsToggle.onClick.Invoke();
            Transform residents = Get<Transform>(game.View, "actorRoot");
            Check(game.State.animals.Select(a => a.species).Distinct().Count() == 6, "six-species sandbox supplies every resident type for the bar audit");
            Check(!residents.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Progress track" || t.name == "Breeding progress" || t.name == "Habitat condition" || t.name == "Habitat light"),
                "no resident contains a head progress track, progress fill or old status badge");

            string before = State(game); int originalLevel = game.State.levelIndex;
            hud.OpenMapSelection();
            Check(hud.ModalOpen && hud.MapSelectionOpen && game.paused && State(game) == before,
                "opening the map pauses input without changing the island or player state");
            Check(map.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Select destination ")) == HabitatLevels.Names.Length,
                "map destination count exactly matches the implemented level list");
            Check(HabitatLevels.Names.All(name => map.GetComponentsInChildren<TMP_Text>().Any(t => t.text == name)),
                "destination labels use the current actual level names");
            map.SelectLevel(0);
            Check(map.SelectedLevel == 0 && game.State.levelIndex == originalLevel && State(game) == before,
                "selecting another destination does not enter it or alter player state");
            ScreenCapture.CaptureScreenshot(Path.Combine(Folder, "map-selection.png")); await Frames(2);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); await Frames(2);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
            Check(!hud.ModalOpen && !hud.MapSelectionOpen && game.paused && State(game) == before,
                "actual Escape closes the map and keeps the previous island and pause state");

            game.paused = false;
            hud.OpenMapSelection(); map.SelectLevel(1); hud.ClosePanels();
            Check(!game.paused && State(game) == before, "ClosePanels restores a previously unpaused game without travelling");
            game.paused = true;
            hud.OpenMapSelection(); map.SelectLevel(2);
            map.ConfirmSelection();
            Check(game.State.levelIndex == 2 && !hud.ModalOpen && !ReferenceEquals(game.State, original),
                "the explicit confirm action enters exactly the selected existing level");
            game.paused = true; await Frames(2);

            game.LoadLevel(0); game.paused = true; await Frames(3);
            game.Move(0, -1);
            Check(game.Busy, "a real legal grid step enters the Busy transition");
            string busyState = State(game), busyTuning = JsonUtility.ToJson(game.tuning);
            int busyActions = game.ActionCount, busyTool = game.tool, busyUndo = game.UndoCount;
            float busySpeed = game.speed; bool busyPause = game.paused, busyOverlay = game.overlays;
            hud.ToggleSettings(); hud.ToggleShop(); hud.ToggleHelp(); hud.OpenMapSelection();
            Check(!hud.ModalOpen, "settings, shop, help and map cannot open during a movement transition");
            var buttons = hud.GetComponentsInChildren<Button>(true);
            int attemptedButtons = 0;
            foreach (Button button in buttons)
            {
                string name = button.name;
                if (name == "Tool 2" || name == "Button 暂停" || name == "Button 1×" || name == "Button 撤销  Z"
                    || name == "Button 显示范围" || name == "Button 自主移动：开" || name == "Button 恢复参数" || name == "Button 单步一秒"
                    || name == "Button +" || name == "Button −" || (name == "Button ×" && button.transform.parent.name == "Selected habitat"))
                { attemptedButtons++; button.onClick.Invoke(); }
            }
            Check(attemptedButtons >= 10, "busy guard exercised at least ten real HUD button callbacks");
            Check(State(game) == busyState && JsonUtility.ToJson(game.tuning) == busyTuning && game.ActionCount == busyActions
                && game.UndoCount == busyUndo && game.tool == busyTool && game.speed == busySpeed && game.paused == busyPause && game.overlays == busyOverlay,
                "queued HUD pointer callbacks cannot change state, tuning, pause, speed, tools, overlays or undo while Busy");
            await Idle(game);
            passed = true;
        }
        catch (Exception exception)
        { report.AppendLine("ERROR: " + exception); throw; }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            previousKeyboard?.MakeCurrent();
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            // LoadLevel is the public cancellation path for an unfinished move or salvage.
            // Restore the actual original objects and all undo snapshots, not a new test save.
            if (game.Busy) game.LoadLevel(original.levelIndex); else hud.ClosePanels();
            game.State = original; game.tuning = originalTuning;
            history.Clear(); for (int i = undo.Length - 1; i >= 0; i--) history.Push(undo[i]);
            RestoreScalars(game, gameScalars);
            game.View.Initialize(original);
            foreach (var entry in camera) typeof(HabitatView).GetField(entry.Key, Fields).SetValue(game.View, entry.Value);
            typeof(HabitatView).GetMethod("UpdateCamera", Fields).Invoke(game.View, new object[] { true });
            map.SelectLevel(originalSelectedMap);
            Refresh(hud);
            foreach (var entry in originalUi) if (entry.Key) entry.Key.SetActive(entry.Value);
            RestoreScalars(hud, hudScalars);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(selection);
            bool restored = State(game) == originalJson && ReferenceEquals(game.tuning, originalTuning) && game.UndoCount == undo.Length;
            report.AppendLine((restored ? "PASS" : "FAIL") + ": original state/tuning/history/actions/pause/camera/UI restored synchronously; no save file written.");
            report.AppendLine((passed && restored ? "PASS" : "FAIL") + ": " + count + " HUD assertions. Screenshot is for visual review, not a claim of full gameplay verification.");
            File.WriteAllText(Path.Combine(Folder, "hud-checks.txt"), report.ToString());
            if (!restored) throw new Exception("HUD verification could not restore the original runtime snapshot.");
        }
        return report.ToString();
    }
}
