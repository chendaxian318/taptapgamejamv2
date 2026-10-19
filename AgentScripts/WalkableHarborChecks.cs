using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class WalkableHarborChecks
{
    static readonly StringBuilder report = new StringBuilder();
    static int checks, samples;
    static void Check(bool pass, string label)
    { if (!pass) throw new Exception(label); report.AppendLine("PASS " + (++checks) + ": " + label); }
    static async Task Frames(int n)
    {
        int first = Time.frameCount;
        double until = UnityEditor.EditorApplication.timeSinceStartup + 8;
        while (Time.frameCount - first < n)
        { if (!Application.isPlaying || UnityEditor.EditorApplication.timeSinceStartup > until) throw new Exception("No frames"); await Task.Delay(12); }
    }
    static Transform Model => GameObject.Find("Wooden harbor on island map").transform;
    static Vector3 Source(float x, float z) => Model.TransformPoint(HabitatImportedDockLayout.FromSource(x, HabitatImportedDockLayout.SourceDeckY, z));
    static async Task WalkTo(HabitatGame g, Vector3 target, string label)
    {
        int iterations = 0;
        while (true)
        {
            Vector3 current = g.View.Harbor.WorldPosition(g.State), delta = target - current; delta.y = 0;
            if (delta.magnitude < .085f) break;
            if (!g.State.onHarbor || ++iterations > 700) throw new Exception("Route blocked: " + label + " at " + current + " target " + target);
            Quaternion rotation = g.View.GameCamera.transform.rotation;
            Vector3 right = rotation * Vector3.right, forward = rotation * Vector3.forward;
            right.y = forward.y = 0; right.Normalize(); forward.Normalize();
            Vector2 axes = new Vector2(Vector3.Dot(delta.normalized, right), Vector3.Dot(delta.normalized, forward));
            g.MoveHarborByScreen(axes, Mathf.Min(.06f, delta.magnitude / 2.6f), rotation);
            await Frames(1);
            if (!g.View.Harbor.IsWalkable(g.View.Harbor.WorldPosition(g.State))) throw new Exception("Lost foot support on " + label);
            samples++;
        }
        Check(true, label);
    }
    static async Task Press(Keyboard k, Key key, int frames = 2)
    {
        k.MakeCurrent(); InputSystem.QueueStateEvent(k, new KeyboardState(key)); await Frames(frames);
        InputSystem.QueueStateEvent(k, new KeyboardState()); await Frames(2);
    }
    public static async Task<string> Run()
    {
        var g = HabitatGame.Instance;
        if (!Application.isPlaying || !g) throw new Exception("Play main island first");
        var original = HabitatRules.Clone(g.State);
        var history = (Stack<HabitatState>)typeof(HabitatGame).GetField("history", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(g);
        var undo = history.ToArray(); int actions = g.ActionCount, tool = g.tool; bool paused = g.paused;
        var background = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        var previousKeyboard = Keyboard.current; Keyboard keyboard = null;
        checks = samples = 0; report.Clear(); Directory.CreateDirectory("Logs/walkable-harbor-20261015");
        try
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            g.LoadLevel(HabitatFigJamLevel.Index); await Frames(4);
            Check(Mathf.Abs(Model.localScale.x - .7f) < .001f && Model.GetComponentInChildren<MeshCollider>().gameObject.layer == 2, "harbor enlarged and detailed surface ignores ordinary picking rays");
            g.Move(1,0);g.Move(1,0);g.Move(1,0);g.tool=0;g.Select(7,10);g.ApplyTool();
            g.Move(-1,0);g.Move(-1,0);g.Move(-1,0);await Frames(4);
            Check(g.State.carryingAnimal==1&&g.State.playerX==3,"carry original shrimp back to landing by legal grid actions");
            await Press(keyboard, Key.A, 65);
            Check(g.State.onHarbor && g.View.Harbor.WorldPosition(g.State).x < 2, "held A crosses original island edge onto real bridge");
            Check(g.View.PlayerVisual.MoveSpeed > .1f || g.View.Harbor.WorldPosition(g.State).x < 1, "same protagonist moves on harbor");
            g.Select(7,10);g.ApplyTool();await Frames(3);
            var shrimp=UnityEngine.Object.FindAnyObjectByType<HabitatShrimpVisual>();
            Check(g.State.carryingAnimal==1&&g.State.selectedX<0&&shrimp.CurrentState=="Carried",
                "carried shrimp animation persists and island tool cannot act remotely from bridge");
            int undoAfterEntry = g.UndoCount;
            await WalkTo(g, Source(-.64f, .29f), "walk across bridge to open deck");
            await WalkTo(g, Model.TransformPoint(HabitatImportedDockLayout.DockArrival), "walk from bridge to arrival platform");
            await WalkTo(g, Model.TransformPoint(HabitatImportedDockLayout.ShopWaypoint), "follow open deck around shop");
            await WalkTo(g, Model.TransformPoint(HabitatImportedDockLayout.ShopApproach), "reach shop without teleporting");
            Check(g.UndoCount == undoAfterEntry, "continuous walking does not flood undo history");
            await Press(keyboard, Key.E);
            Check(g.Hud.ModalOpen, "actual E opens existing shop at imported shop model");
            Vector3 stopped = g.View.Harbor.WorldPosition(g.State);
            Check(!g.MoveHarborByScreen(Vector2.right, .1f, g.View.GameCamera.transform.rotation) && Vector3.Distance(stopped, g.View.Harbor.WorldPosition(g.State)) < .001f, "shop modal blocks movement");
            await Press(keyboard, Key.Escape);
            Check(!g.Hud.ModalOpen, "Escape restores walking");
            var roundtrip = JsonUtility.FromJson<HabitatState>(JsonUtility.ToJson(g.State));
            var clone = HabitatRules.Clone(g.State);
            Check(roundtrip.onHarbor && clone.onHarbor && Math.Abs(roundtrip.harborZ - g.State.harborZ) < .001f && clone.harborX == g.State.harborX, "save JSON and undo clone preserve continuous harbor position");
            g.State = roundtrip; await Frames(3);
            Check(Vector3.Distance(g.View.PlayerVisual.transform.position, g.View.Harbor.WorldPosition(g.State)) < .001f, "state replacement snaps same player to saved deck position");
            Quaternion previousRotation=g.View.GameCamera.transform.rotation;
            await Press(keyboard,Key.Q);
            Check(Mathf.Abs(Quaternion.Angle(previousRotation,g.View.GameCamera.transform.rotation)-90f)<.1f,"Q rotates main camera by 90 degrees while on harbor");
            foreach (Vector3 point in HabitatImportedDockLayout.HomePath.Skip(1)) await WalkTo(g, Model.TransformPoint(point), "home walkway segment");
            await Press(keyboard,Key.C);
            Check(!g.View.Harbor.IsWalkable(Source(.65f, -.43f)) && !g.View.Harbor.IsWalkable(Model.position + Vector3.forward * 30), "building interior and open sea are not walkable");
            float oldY = g.View.Harbor.WorldPosition(g.State).y;
            float localX = g.State.harborX, localZ = g.State.harborZ;
            g.State.waterLevel = 1; await Frames(3);
            Check(Mathf.Abs(g.View.Harbor.WorldPosition(g.State).y - oldY - .7f) < .01f && g.State.harborX == localX && g.State.harborZ == localZ, "tide rebuild preserves walking position and lifts feet with deck");
            g.State.waterLevel = 0; await Frames(3);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/walkable-harbor-20261015/player-on-harbor.png")); await Frames(2);
            foreach (Vector3 point in HabitatImportedDockLayout.HomePath.Reverse().Skip(1)) await WalkTo(g, Model.TransformPoint(point), "return along home walkway");
            await WalkTo(g, Model.TransformPoint(HabitatImportedDockLayout.ShopWaypoint), "return around shop");
            await WalkTo(g, Model.TransformPoint(HabitatImportedDockLayout.DockArrival), "return to bridge platform");
            await WalkTo(g, Source(-.64f, .29f), "return to bridge interior");
            Transform dock = Model.parent;
            await WalkTo(g, dock.TransformPoint(new Vector3(0, 0, -.26f)), "walk back along bridge to shore");
            g.State.waterLevel = 1; await Frames(3);
            for (int i = 0; i < 8; i++) { g.Move(1, 0); await Frames(1); }
            Check(g.State.onHarbor && g.State.message.Contains("淹没"), "flooded landing cannot put player in water");
            g.State.waterLevel = 0; await Frames(3);
            for (int i = 0; i < 8 && g.State.onHarbor; i++) { g.Move(1, 0); await Frames(1); }
            Check(!g.State.onHarbor && g.State.playerX == 3 && g.State.playerZ == 10, "walk back to original landing after low tide");
            g.Undo(); await Frames(3);
            Check(g.State.onHarbor && g.View.Harbor.IsWalkable(g.View.Harbor.WorldPosition(g.State)), "undo exit restores supported bridge position");
            g.Move(1, 0); await Frames(3); g.Move(1, 0); await Frames(3);
            Check(!g.State.onHarbor && g.State.playerX == 4, "ordinary grid movement resumes after leaving harbor");
            g.Select(4,9);g.ApplyTool();await Frames(3);
            Check(g.State.carryingAnimal<0&&g.State.animals.Find(a=>a.id==1).x==4&&g.State.animals.Find(a=>a.id==1).z==9,
                "shrimp carried across harbor and back can be placed in the original map cage");
            report.AppendLine("PASS: " + checks + " assertions; " + samples + " supported movement samples. Original state restored.");
            File.WriteAllText("Logs/walkable-harbor-20261015/checks.txt", report.ToString());
            return report.ToString();
        }
        catch (Exception e) { File.WriteAllText("Logs/walkable-harbor-20261015/checks.txt", report + "FAIL: " + e); throw; }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard); previousKeyboard?.MakeCurrent();
            InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            g.Hud.ClosePanels(); g.State = original; g.ActionCount = actions; g.tool = tool; g.paused = paused;
            history.Clear(); for (int i = undo.Length - 1; i >= 0; i--) history.Push(undo[i]);
            g.View.Initialize(g.State); await Frames(4);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/walkable-harbor-20261015/island-with-walkable-harbor.png"));
        }
    }
}
