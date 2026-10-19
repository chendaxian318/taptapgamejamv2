using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using HabitatIsland;

public static class OptimizationCloseViewAudit
{
    public static async Task<string> Run()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var g = HabitatGame.Instance;
        if (!Application.isPlaying || !g || g.Busy) throw new Exception("Idle Play mode required");
        var type = typeof(HabitatGame);
        var snapshot = type.GetMethod("CaptureSnapshot", flags).Invoke(g, null);
        var history = (Stack<HabitatState>)type.GetField("history", flags).GetValue(g);
        var oldHistory = history.ToArray();
        var names = new[] { "chapterGreetingPending", "chapterChestPending", "chapterMorningGreetingPending", "chapterReturnReminderShown", "campaignReturnReminderLevel" };
        var saved = names.ToDictionary(n => n, n => type.GetField(n, flags).GetValue(g));
        bool session = g.SessionStarted, pause = g.paused, opening = g.playOpeningOnNewGame;
        int tool = g.tool;
        try
        {
            type.GetProperty("SessionStarted").SetValue(g, true);
            g.playOpeningOnNewGame = false;
            g.LoadLevel(8);
            g.paused = true;
            g.Hud.ClosePanels();
            foreach (var n in names.Take(3)) type.GetField(n, flags).SetValue(g, false);
            g.View.FocusPlayer();
            for (int i = 0; i < 100; i++) await Task.Delay(16);
            g.Hud.ClosePanels();
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/audit-optimization-20261018/level-8-default-close.png"));
            for (int i = 0; i < 12; i++) await Task.Delay(16);
            return "Captured default follow-player view on fourth-island audit fixture; not a route/UI acceptance test";
        }
        finally
        {
            g.CancelPendingAction();
            g.Hud.ClosePanels();
            type.GetMethod("RestoreSnapshot", flags).Invoke(g, new[] { snapshot });
            history.Clear();
            for (int i = oldHistory.Length - 1; i >= 0; i--) history.Push(oldHistory[i]);
            foreach (var p in saved) type.GetField(p.Key, flags).SetValue(g, p.Value);
            g.paused = pause; g.tool = tool; g.playOpeningOnNewGame = opening;
            type.GetProperty("SessionStarted").SetValue(g, session);
        }
    }
}
