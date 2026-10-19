using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public static class ModelsDockChecks
{
    static readonly StringBuilder report = new StringBuilder();
    static int count;
    static string Folder => Path.GetFullPath("Logs/models-20261015");
    static void Check(bool pass, string label)
    {
        count++;
        if (!pass) throw new Exception("FAIL " + count + ": " + label);
        report.AppendLine("PASS " + count + ": " + label);
    }
    static async Task WaitFor(Func<bool> ready, double seconds, string label)
    {
        double until = UnityEditor.EditorApplication.timeSinceStartup + seconds;
        while (!ready() && UnityEditor.EditorApplication.timeSinceStartup < until) await Task.Delay(100);
        Check(ready(), label);
    }
    static async Task Press(Keyboard keyboard, Key key)
    {
        keyboard.MakeCurrent();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        await Task.Delay(90);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        await Task.Delay(90);
    }
    static async Task WatchSequence(HabitatDockPrototype dock, HashSet<string> shots)
    {
        var seen = new Dictionary<string, double>();
        var captured = new HashSet<string>();
        double deadline = UnityEditor.EditorApplication.timeSinceStartup + dock.director.duration + 5;
        while (dock.IsSequencePlaying && UnityEditor.EditorApplication.timeSinceStartup < deadline)
        {
            string shot = dock.CurrentShot;
            shots.Add(shot);
            if (!seen.ContainsKey(shot)) seen.Add(shot, UnityEditor.EditorApplication.timeSinceStartup);
            if (!captured.Contains(shot) && UnityEditor.EditorApplication.timeSinceStartup - seen[shot] > 1)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(Folder, "harbor-" + shot + ".png"));
                captured.Add(shot);
            }
            await Task.Delay(150);
        }
        Check(!dock.IsSequencePlaying && dock.HasControl, "Timeline completes naturally and restores control");
    }
    static async Task WalkTo(HabitatDockPrototype dock, Vector3 target)
    {
        for (int i = 0; i < 160 && Vector3.Distance(dock.PlayerPosition, target) > .19f; i++)
        {
            Vector3 delta = target - dock.PlayerPosition;
            float dt = Mathf.Min(.05f, delta.magnitude / dock.walkSpeed);
            Vector3 right = dock.harborCamera.transform.right, forward = dock.harborCamera.transform.forward;
            right.y = 0; forward.y = 0;
            Vector2 axes = new Vector2(Vector3.Dot(delta, right.normalized), Vector3.Dot(delta, forward.normalized));
            dock.MoveByScreen(axes.normalized, dt);
            await Task.Delay(16);
        }
        Check(Vector3.Distance(dock.PlayerPosition, target) < .2f, "walked to " + target + " without teleporting");
    }
    static async Task Navigate(HabitatDockPrototype dock, Vector3 goal)
    {
        const float step = .22f;
        Vector2Int Key(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x / step), Mathf.RoundToInt(p.z / step));
        Vector3 Point(Vector2Int k) => new Vector3(k.x * step, 0, k.y * step);
        Vector2Int start = Key(dock.PlayerPosition), finish = Key(goal);
        var queue = new Queue<Vector2Int>(); var parents = new Dictionary<Vector2Int, Vector2Int>();
        queue.Enqueue(start); parents[start] = start;
        var offsets = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (queue.Count > 0 && !parents.ContainsKey(finish))
        {
            var at = queue.Dequeue();
            foreach (var offset in offsets)
            {
                var next = at + offset;
                if (parents.ContainsKey(next) || Mathf.Abs(next.x) > 65 || Mathf.Abs(next.y) > 48 || !dock.IsWalkable(Point(next))) continue;
                parents[next] = at; queue.Enqueue(next);
            }
        }
        Check(parents.ContainsKey(finish), "imported deck connects current position to " + goal);
        var route = new List<Vector2Int>(); var current = finish;
        while (current != start) { route.Add(current); current = parents[current]; }
        route.Reverse();
        for (int i = 0; i < route.Count; i++)
        {
            if (i > 0 && i + 1 < route.Count && route[i] - route[i - 1] == route[i + 1] - route[i]) continue;
            await WalkTo(dock, Point(route[i]));
        }
        await WalkTo(dock, goal);
        Check(Vector3.Distance(dock.PlayerPosition, goal) < .23f, "actual walking reaches " + goal + " without teleporting");
    }

    public static async Task<string> Run()
    {
        count = 0; report.Clear(); Directory.CreateDirectory(Folder);
        var originalKeyboard = Keyboard.current;
        var background = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        Keyboard keyboard = null;
        try
        {
            Check(Application.isPlaying && HabitatGame.Instance, "main island is playing");
            var game = HabitatGame.Instance;
            game.Hud.ClosePanels(); game.paused = true;
            var savedState = JsonUtility.ToJson(game.State);
            int savedActions = game.ActionCount;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("Harbor acceptance keyboard");
            game.Hud.ToggleSettings();
            var entry = Array.Find(game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>(true), b => b.GetComponentInChildren<TMPro.TMP_Text>(true)?.text == "前往码头");
            Check(entry && entry.gameObject.activeInHierarchy, "settings contains visible harbor entry");
            entry.onClick.Invoke();
            await WaitFor(() => UnityEngine.Object.FindAnyObjectByType<HabitatDockPrototype>() != null, 12, "harbor scene loaded from real menu button");
            var dock = UnityEngine.Object.FindAnyObjectByType<HabitatDockPrototype>();
            await Task.Delay(150);
            Check(dock.IsSequencePlaying && !dock.HasControl && dock.Day == 1, "first entry starts first-day Timeline and locks movement");
            Check(dock.openingTimeline && dock.morningTimeline && dock.PlayerVisual && !dock.PlayerVisual.Animator.applyRootMotion, "actual Timeline assets and animated traveler are connected without root motion");
            var locked = dock.PlayerPosition;
            Check(!dock.MoveByScreen(Vector2.up, .1f) && dock.PlayerPosition == locked && !dock.TryDeliverCatch(), "sequence rejects gameplay movement and remote delivery");
            dock.ToggleSequencePause();
            double pausedAt = dock.director.time;
            await Task.Delay(300);
            Check(dock.IsSequencePaused && Math.Abs(dock.director.time - pausedAt) < .03, "pause freezes actual PlayableDirector time");
            dock.ToggleSequencePause();
            var shots = new HashSet<string>();
            await WatchSequence(dock, shots);
            foreach (string shot in new[] { "BlueSky", "ReturnToDock", "StowNet", "WalkToShop" })
                Check(shots.Contains(shot), "natural opening played shot " + shot);
            Check(dock.IntroCompleted && dock.HasCatch && dock.Day == 1 && dock.NearbyInteraction == HabitatDockInteraction.Shop, "opening ends at shop with full catch and control");
            await Press(keyboard, Key.E);
            Check(dock.Modal == HabitatDockInteraction.Shop, "E input opens nearby shop");
            Check(!dock.MoveByScreen(Vector2.up, .1f) && !dock.TryRest(), "shop modal locks walking and cannot rest remotely");
            Check(dock.TryDeliverCatch() && !dock.HasCatch && dock.DeliveredBatches == 1, "shop delivers one catch batch");
            Check(!dock.TryDeliverCatch() && dock.DeliveredBatches == 1, "repeated delivery does not duplicate catch");
            await Press(keyboard, Key.Escape);
            Check(dock.HasControl && dock.Modal == HabitatDockInteraction.None, "Escape closes shop and restores walking");
            Check(!dock.IsWalkable(new Vector3(20, 0, 0)) && !dock.IsWalkable(new Vector3(0, 0, 20)) && !dock.IsWalkable(HabitatImportedDockLayout.WindowTarget), "house walls, sea and map boundary reject walk positions");
            await Navigate(dock, HabitatDockPrototype.HomeApproach);
            await Press(keyboard, Key.E);
            Check(dock.Modal == HabitatDockInteraction.Home && dock.TryRest(), "walk to home, E, rest starts morning Timeline");
            shots.Clear(); await WatchSequence(dock, shots);
            Check(shots.Contains("MorningWindow") && dock.Day == 2 && !dock.HasCatch && dock.DeliveredBatches == 1, "window sunlight sequence advances exactly one day without inventing catch");
            Check(!dock.SkipSequence() && dock.Day == 2, "late skip cannot advance day twice");
            ScreenCapture.CaptureScreenshot(Path.Combine(Folder, "harbor-day2.png"));
            await WalkTo(dock, new Vector3(0, 0, 0));
            await Navigate(dock, HabitatDockPrototype.BoatApproach);
            await Press(keyboard, Key.E);
            Check(dock.Modal == HabitatDockInteraction.Boat && dock.TrySailToIsland(), "walk to boat, E and sail loads island");
            await WaitFor(() => HabitatGame.Instance != null, 12, "island returns after boat travel");
            game = HabitatGame.Instance;
            Check(JsonUtility.ToJson(game.State) == savedState && game.ActionCount == savedActions && game.paused, "round trip preserves exact island state and action count");
            game.OpenHarbor();
            await WaitFor(() => UnityEngine.Object.FindAnyObjectByType<HabitatDockPrototype>() != null, 12, "return to harbor a second time");
            await Task.Delay(180);
            dock = UnityEngine.Object.FindAnyObjectByType<HabitatDockPrototype>();
            Check(dock.Day == 2 && dock.DeliveredBatches == 1 && !dock.HasCatch && dock.IntroCompleted && dock.HasControl, "harbor session survives island trip without replaying intro");
            Check(dock.RestartFirstDayForChecks() && dock.SkipSequence() && dock.HasControl && dock.IntroCompleted, "explicit replay can be skipped and restores control");
            Check(!dock.SkipSequence() && dock.Day == 1, "repeated opening skip is inert");
            Check(dock.PlacePlayerForChecks(HabitatDockPrototype.HomeApproach) && dock.TryInteract() && dock.TryRest() && dock.SkipSequence() && dock.Day == 2, "morning skip commits one day and restores control");
            Check(!dock.SkipSequence() && dock.Day == 2 && dock.HasControl, "repeated morning skip is inert");
            Check(dock.PlacePlayerForChecks(HabitatDockPrototype.BoatApproach) && dock.TryInteract() && dock.TrySailToIsland(), "leave verification back at island");
            await WaitFor(() => HabitatGame.Instance != null, 12, "main island ready after checks");
            return "PASS: " + count + " harbor and travel checks";
        }
        catch (Exception e) { report.AppendLine(e.ToString()); return "FAIL: " + e.Message; }
        finally
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            File.WriteAllText(Path.Combine(Folder, "harbor-checks.txt"), report.ToString());
        }
    }
}
