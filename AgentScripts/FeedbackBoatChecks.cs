using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Main-map integration uses actual frame advancement and E/Escape input. The separate
// prototype is audited through compiled callback bindings only; it is not opened here.
public static class FeedbackBoatChecks
{
    static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly StringBuilder report = new StringBuilder();
    static readonly Dictionary<ushort, OpCode> opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null))
        .GroupBy(o => unchecked((ushort)o.Value)).ToDictionary(group => group.Key, group => group.First());
    static int checks, walkSamples;
    static string Folder => Path.GetFullPath("Logs/feedback-20261015");
    static void Check(bool pass, string label)
    {
        report.AppendLine((pass ? "PASS " : "FAIL ") + (++checks) + ": " + label);
        if (!pass) throw new Exception(label);
    }
    static T Get<T>(object instance, string name) => (T)instance.GetType().GetField(name, Fields).GetValue(instance);
    static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Fields).SetValue(instance, value);
    static Dictionary<FieldInfo, object> Scalars(object instance) => instance.GetType().GetFields(Fields)
        .Where(f => !f.IsInitOnly && !f.IsStatic && (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string)))
        .ToDictionary(f => f, f => f.GetValue(instance));
    static void RestoreScalars(object instance, Dictionary<FieldInfo, object> values)
    { foreach (var pair in values) pair.Key.SetValue(instance, pair.Value); }
    static string State(HabitatGame game) => JsonUtility.ToJson(game.State);
    static async Task Frames(int count)
    {
        int first = Time.frameCount;
        double deadline = UnityEditor.EditorApplication.timeSinceStartup + 8;
        while (Time.frameCount - first < count)
        {
            if (!Application.isPlaying || UnityEditor.EditorApplication.timeSinceStartup > deadline)
                throw new Exception("Play-mode frames did not advance.");
            await Task.Delay(12);
        }
    }
    static async Task Press(Keyboard keyboard, Key key)
    {
        keyboard.MakeCurrent();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); await Frames(2);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
    }
    static async Task WalkTo(HabitatGame game, Vector3 target, string label)
    {
        int stalled = 0;
        for (int i = 0; i < 600; i++)
        {
            Vector3 current = game.View.Harbor.WorldPosition(game.State), delta = target - current; delta.y = 0;
            if (delta.magnitude < .07f) { Check(true, label); return; }
            if (!game.State.onHarbor) throw new Exception("Unexpectedly left the harbor during " + label);
            Quaternion rotation = game.View.GameCamera.transform.rotation;
            Vector3 right = rotation * Vector3.right, forward = rotation * Vector3.forward;
            right.y = forward.y = 0; right.Normalize(); forward.Normalize();
            Vector2 axes = new Vector2(Vector3.Dot(delta.normalized, right), Vector3.Dot(delta.normalized, forward));
            game.MoveHarborByScreen(axes, Mathf.Min(.08f, delta.magnitude / 2.5f), rotation);
            await Frames(1); walkSamples++;
            Vector3 next = game.View.Harbor.WorldPosition(game.State);
            if (!game.View.Harbor.IsWalkable(next)) throw new Exception("Lost deck support during " + label);
            stalled = Vector3.Distance(current, next) < .001f ? stalled + 1 : 0;
            if (stalled > 20) throw new Exception("Route blocked during " + label + ": " + next + " to " + target);
        }
        throw new Exception("Walk exceeded the sample limit: " + label);
    }

    // Read real IL method tokens, including ldftn callback tokens; do not infer bindings
    // from source text or execute the standalone prototype's cross-scene confirmation.
    static IEnumerable<MethodBase> References(MethodBase method)
    {
        byte[] bytes = method.GetMethodBody()?.GetILAsByteArray();
        if (bytes == null) yield break;
        for (int p = 0; p < bytes.Length;)
        {
            ushort value = bytes[p++];
            if (value == 0xfe) value = (ushort)(0xfe00 | bytes[p++]);
            OpCode code = opcodes[value];
            int size;
            switch (code.OperandType)
            {
                case OperandType.InlineNone: size = 0; break;
                case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: size = 1; break;
                case OperandType.InlineVar: size = 2; break;
                case OperandType.InlineI8: case OperandType.InlineR: size = 8; break;
                case OperandType.InlineSwitch: size = 4 + 4 * BitConverter.ToInt32(bytes, p); break;
                default: size = 4; break;
            }
            if (code.OperandType == OperandType.InlineMethod)
            {
                MethodBase referenced = null;
                try { referenced = method.Module.ResolveMethod(BitConverter.ToInt32(bytes, p)); }
                catch (ArgumentException) { }
                if (referenced != null) yield return referenced;
            }
            p += size;
        }
    }
    static bool Reaches(MethodBase source, MethodBase target, HashSet<MethodBase> seen = null)
    {
        seen = seen ?? new HashSet<MethodBase>();
        if (source == null || !seen.Add(source)) return false;
        foreach (MethodBase next in References(source))
        {
            if (next == target) return true;
            if (next.DeclaringType != null && next.DeclaringType.FullName.StartsWith(typeof(HabitatDockPrototype).FullName)
                && Reaches(next, target, seen)) return true;
        }
        return false;
    }
    static void CheckPrototypeBindings()
    {
        Type dock = typeof(HabitatDockPrototype);
        MethodInfo build = dock.GetMethod("BuildHud", Fields);
        MethodInfo refresh = dock.GetMethod("RefreshModal", Fields);
        MethodInfo open = dock.GetMethod("TryOpenInteraction", Fields);
        MethodInfo sail = dock.GetMethod("TrySailToIsland", Fields, null, new[] { typeof(int) }, null);
        MethodInfo travel = typeof(HabitatGame).GetMethod("TravelToLevel", BindingFlags.Public | BindingFlags.Static);
        MethodInfo show = typeof(HabitatMapSelectionPanel).GetMethod("Show");
        MethodBase[] buildReferences = References(build).ToArray();
        var mapCallbacks = buildReferences.Where(method => method.Name.Contains("<BuildHud>")
            && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(int));
        Check(buildReferences.Any(method => method.DeclaringType == typeof(HabitatMapSelectionPanel) && method.Name == "Create")
            && mapCallbacks.Any(callback => References(callback).Contains(sail)),
            "compiled standalone shared-map callback forwards the selected index into TrySailToIsland(int)");
        Check(References(sail).Contains(travel), "standalone sail action forwards the selected index through TravelToLevel");
        Check(References(open).Contains(refresh) && References(refresh).Contains(show),
            "standalone interaction opening is bound to the shared map selection view");
        Check(!Reaches(open, travel), "opening the standalone interaction has no direct scene-travel callback");
        string[] enabled = UnityEditor.EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path)).ToArray();
        Check(enabled.Contains(HabitatDockPrototype.IslandSceneName) && enabled.Contains(HabitatDockPrototype.HarborSceneName),
            "both destinations required by standalone travel remain enabled in Build Settings");
        report.AppendLine("SCOPE: standalone HarborPrototype callback binding audit only; no standalone scene reload or complete cross-scene journey executed.");
    }

    public static async Task<string> Run()
    {
        HabitatGame game = HabitatGame.Instance;
        if (!Application.isPlaying || !game || game.Busy || (game.Fishing && game.Fishing.IsOpen))
            throw new InvalidOperationException("Start in an idle main-island player before the boat check.");
        HabitatHud hud = game.Hud;
        HabitatState original = game.State; string originalJson = State(game);
        Tuning tuning = game.tuning;
        var history = Get<Stack<HabitatState>>(game, "history"); var undo = history.ToArray();
        var gameValues = Scalars(game); var hudValues = Scalars(hud);
        var ui = hud.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.gameObject, t => t.gameObject.activeSelf);
        var map = Get<HabitatMapSelectionPanel>(hud, "mapSelection"); int mapIndex = map.SelectedLevel;
        string[] cameraNames = { "cameraYaw", "cameraSize", "cameraTarget", "cameraTargetSmooth", "followPlayer", "overviewCamera" };
        var camera = cameraNames.ToDictionary(name => name, name => typeof(HabitatView).GetField(name, Fields).GetValue(game.View));
        var previousKeyboard = Keyboard.current;
        var background = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        var selection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        Keyboard keyboard = null;
        bool passed = false; checks = walkSamples = 0; report.Clear(); Directory.CreateDirectory(Folder);
        try
        {
            CheckPrototypeBindings();
            game.tuning = JsonUtility.FromJson<Tuning>(JsonUtility.ToJson(tuning));
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("Feedback boat acceptance keyboard"); keyboard.MakeCurrent();
            game.LoadLevel(HabitatFigJamLevel.Index); game.paused = true; await Frames(4);
            Check(game.View.Harbor.Ready && game.State.playerX == game.State.dockX && game.State.playerZ == game.State.dockZ,
                "authored island starts at its connected landing with the real harbor bound");
            Check(!game.View.Harbor.NearBoat(game.State), "island-side player cannot remotely count as boarding the boat");
            Transform dockRoot = Get<Transform>(game.View.Harbor, "dock");
            Transform model = Get<Transform>(game.View.Harbor, "model");
            Vector3 outward = dockRoot.TransformDirection(Vector3.back);
            int dx = Mathf.Abs(outward.x) > Mathf.Abs(outward.z) ? (outward.x > 0 ? 1 : -1) : 0;
            int dz = dx == 0 ? (outward.z > 0 ? 1 : -1) : 0;
            game.Move(dx, dz); await Frames(3);
            Check(game.State.onHarbor && game.View.Harbor.IsWalkable(game.View.Harbor.WorldPosition(game.State)),
                "ordinary move enters the supported shore bridge without teleporting");
            await Press(keyboard, Key.E);
            Check(!hud.ModalOpen && !game.View.Harbor.NearBoat(game.State), "E on the shore bridge does not remotely open boat selection");
            await WalkTo(game, model.TransformPoint(HabitatImportedDockLayout.FromSource(-.64f, HabitatImportedDockLayout.SourceDeckY, .29f)),
                "walk from the shore bridge to the measured open deck");
            await WalkTo(game, model.TransformPoint(HabitatImportedDockLayout.BoatApproach), "walk across the deck to the boat boarding point");
            Check(game.View.Harbor.NearBoat(game.State) && !game.View.Harbor.NearShop(game.State) && !hud.ModalOpen,
                "supported boat approach enables boarding without entering a level automatically");
            string boatState = State(game); int actions = game.ActionCount, undoCount = game.UndoCount;
            await Press(keyboard, Key.E);
            Check(hud.MapSelectionOpen && State(game) == boatState, "actual E routes NearBoat through ApplyTool to the map, with no state mutation");
            var destination = map.GetComponentsInChildren<Button>().First(b => b.name == "Select destination 1");
            destination.onClick.Invoke(); await Frames(3);
            Check(map.SelectedLevel == 1 && game.State.levelIndex == HabitatFigJamLevel.Index && State(game) == boatState,
                "actual destination button changes only selection while the player remains at the boat");
            ScreenCapture.CaptureScreenshot(Path.Combine(Folder, "boat-map-selection.png")); await Frames(2);
            await Press(keyboard, Key.Escape);
            Check(!hud.ModalOpen && State(game) == boatState && game.ActionCount == actions && game.UndoCount == undoCount && game.paused,
                "Escape keeps exact boat position, facing, inventory, action history and previous pause state");
            await Press(keyboard, Key.E);
            map.SelectLevel(2);
            map.GetComponentsInChildren<Button>().First(b => b.name == "留在码头  Esc button").onClick.Invoke(); await Frames(2);
            Check(!hud.ModalOpen && State(game) == boatState, "the cancel button also leaves the player at the same boat state");
            await Press(keyboard, Key.E);
            map.GetComponentsInChildren<Button>().First(b => b.name == "Select destination 1").onClick.Invoke();
            map.GetComponentsInChildren<Button>().First(b => b.name == "Confirm destination").onClick.Invoke(); await Frames(4);
            Check(game.State.levelIndex == 1 && !game.State.onHarbor && !hud.ModalOpen && game.ActionCount == 0 && game.UndoCount == 0,
                "only the explicit confirmation enters the chosen existing level with its own initial state");
            passed = true;
        }
        catch (Exception error) { report.AppendLine("ERROR: " + error); throw; }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard); previousKeyboard?.MakeCurrent();
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            game.CancelPendingAction(); hud.ClosePanels();
            game.State = original; game.tuning = tuning;
            history.Clear(); for (int i = undo.Length - 1; i >= 0; i--) history.Push(undo[i]);
            RestoreScalars(game, gameValues); game.View.Initialize(original);
            foreach (var entry in camera) typeof(HabitatView).GetField(entry.Key, Fields).SetValue(game.View, entry.Value);
            typeof(HabitatView).GetMethod("UpdateCamera", Fields).Invoke(game.View, new object[] { true });
            map.SelectLevel(mapIndex); Set(hud, "nextRefresh", 0f); hud.Refresh();
            foreach (var entry in ui) if (entry.Key) entry.Key.SetActive(entry.Value);
            RestoreScalars(hud, hudValues);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(selection);
            bool restored = State(game) == originalJson && ReferenceEquals(game.tuning, tuning) && game.UndoCount == undo.Length;
            report.AppendLine((restored ? "PASS" : "FAIL") + ": original state/tuning/undo/actions/pause/camera/UI/input restored; no save file written.");
            report.AppendLine((passed && restored ? "PASS" : "FAIL") + ": " + checks + " boat checks; " + walkSamples + " supported walking samples.");
            File.WriteAllText(Path.Combine(Folder, "boat-checks.txt"), report.ToString());
            if (!restored) throw new Exception("Boat verification snapshot restoration mismatch.");
        }
        return report.ToString();
    }
}
