using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;

public static class MapModelsChecks
{
    static int count;
    static readonly StringBuilder report = new StringBuilder();
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool pass, string label)
    { if (!pass) throw new Exception(label); report.AppendLine("PASS " + (++count) + ": " + label); }
    static async Task Frames(int n)
    {
        int first = Time.frameCount;
        double until = UnityEditor.EditorApplication.timeSinceStartup + 8;
        while (Time.frameCount - first < n)
        {
            if (!Application.isPlaying || UnityEditor.EditorApplication.timeSinceStartup > until) throw new Exception("No frames");
            await Task.Delay(16);
        }
    }
    static HabitatTrapVisual Trap() => UnityEngine.Object.FindAnyObjectByType<HabitatTrapVisual>();
    static Transform Dock() => GameObject.Find("Wooden harbor on island map").transform;
    public static async Task<string> Run()
    {
        var g = HabitatGame.Instance;
        if (!Application.isPlaying || !g) throw new Exception("Play the original island first.");
        var original = HabitatRules.Clone(g.State);
        var history = (Stack<HabitatState>)typeof(HabitatGame).GetField("history", Private).GetValue(g);
        var savedHistory = history.ToArray();
        int actions = g.ActionCount, tool = g.tool;
        bool paused = g.paused;
        count = 0; report.Clear();
        try
        {
            g.LoadLevel(HabitatFigJamLevel.Index);
            await Frames(3);
            Check(g.State.width == 18 && g.State.depth == 19 && g.View.PlayerVisual.IsModelActive, "original map and animated protagonist present");
            Check(Trap().IsConfigured && Trap().CurrentState == HabitatTrapVisualState.Ready, "authored landing cage starts open");
            Check(UnityEngine.Object.FindObjectsByType<HabitatShrimpVisual>().Length == 1, "original shrimp uses animation model");
            Bounds bounds = Dock().GetComponentInChildren<Renderer>().bounds;
            Check(bounds.max.x <= 2.11f && bounds.size.x > 9f, "complete harbor stays seaward of land at X=2.5");
            Check(Dock().GetComponentsInChildren<Collider>().All(c => c.gameObject.layer == 2), "harbor sampling colliders cannot intercept ordinary picking rays");
            Check(HabitatRules.AtDock(g.State), "original landing shop interaction remains available");
            g.Move(1, 0); g.Move(1, 0); g.Move(1, 0);
            Check(g.State.playerX == 6 && g.State.playerZ == 10, "player walks original three-step route to shrimp");
            g.tool = 0; g.Select(7, 10); g.ApplyTool();
            Check(g.State.carryingAnimal == 1, "original shrimp picked up by normal tool");
            g.Move(-1, 0); g.Move(-1, 0); g.Select(4, 9); g.ApplyTool();
            await Frames(3);
            Check(g.State.carryingAnimal < 0 && Trap().CurrentState == HabitatTrapVisualState.Closing, "placing original shrimp in map cage triggers close animation");
            await Frames(110);
            Check(Trap().HasCapturedShrimp && Trap().CurrentState == HabitatTrapVisualState.Occupied, "cage closes over actual resident");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/map-models-20261015/map-cage-occupied.png"));
            await Frames(2);
            g.Select(4, 9); g.ApplyTool(); await Frames(3);
            Check(g.State.carryingAnimal == 1 && Trap().CurrentState == HabitatTrapVisualState.Opening, "taking shrimp out plays opening animation");
            await Frames(110);
            Check(Trap().CurrentState == HabitatTrapVisualState.Ready, "map cage returns to open state");
            g.Undo(); await Frames(3);
            Check(Trap().CurrentState == HabitatTrapVisualState.Occupied, "undo restores captured state without replaying false capture");
            float dockY = Dock().position.y;
            g.State.waterLevel = 1; await Frames(3);
            Check(Mathf.Abs(Dock().position.y - dockY - .7f) < .001f && Trap().CurrentState == HabitatTrapVisualState.Occupied, "tide rebuild floats harbor and keeps cage state");
            g.State.waterLevel = 0; await Frames(3);
            Check(Mathf.Abs(Dock().position.y - dockY) < .001f && Trap().CurrentState == HabitatTrapVisualState.Occupied, "low tide restores placement");
            g.LoadLevel(HabitatFigJamLevel.Index); await Frames(3);
            g.View.FocusOverview(); await Frames(60);
            bounds = Dock().GetComponentInChildren<Renderer>().bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i&1)==0?-1:1, (i&2)==0?-1:1, (i&4)==0?-1:1));
                Vector3 viewport = g.View.GameCamera.WorldToViewportPoint(corner);
                Check(viewport.x > 0 && viewport.x < 1 && viewport.y > 0 && viewport.y < 1, "overview includes harbor corner " + i);
            }
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/map-models-20261015/map-overview.png"));
            await Frames(2);
            report.AppendLine("PASS: " + count + " checks; actual authored map used. Original state/history restored afterward.");
            File.WriteAllText("Logs/map-models-20261015/checks.txt", report.ToString());
            return report.ToString();
        }
        finally
        {
            g.State = original; history.Clear();
            for (int i = savedHistory.Length - 1; i >= 0; i--) history.Push(savedHistory[i]);
            g.ActionCount = actions; g.tool = tool; g.paused = paused;
            g.View.Initialize(g.State); g.Hud.ClosePanels();
            await Frames(4);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/map-models-20261015/map-player-models.png"));
        }
    }
}
