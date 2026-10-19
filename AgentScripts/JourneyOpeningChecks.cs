using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;

// Run shortly after entering Play from the main scene. Observes its automatic
// opening instead of invoking any story method or fast-forwarding the director.
public static class JourneyOpeningChecks
{
    public static async Task<string> Run()
    {
        var game = HabitatGame.Instance;
        if (!Application.isPlaying || !game || !game.StoryPlaying) throw new Exception("Automatic opening did not start.");
        string folder = Path.GetFullPath("Logs/journey-20261015"); Directory.CreateDirectory(folder);
        var captions = new HashSet<string>();
        var report = new List<string>();
        int oldActions = game.ActionCount;
        if (!game.Busy || game.State.openingCompleted || !game.State.openingCatchGranted
            || game.State.fish != HabitatJourneyRules.OpeningFish || game.State.shrimp != HabitatJourneyRules.OpeningShrimp)
            throw new Exception("New-game opening must grant one real catch and lock gameplay until it ends.");
        report.Add("PASS: main scene starts its Timeline automatically with exactly one real opening catch.");
        Vector3 start = game.View.PlayerVisual.transform.position;
        float furthest = 0;
        double deadline = UnityEditor.EditorApplication.timeSinceStartup + 24;
        while (game.StoryPlaying)
        {
            if (UnityEditor.EditorApplication.timeSinceStartup > deadline) throw new Exception("Opening never restored player control.");
            string caption = game.Story.CurrentCaption;
            if (!string.IsNullOrEmpty(caption) && captions.Add(caption))
            {
                report.Add("SHOT: " + caption);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "opening-" + captions.Count + ".png"));
            }
            furthest = Mathf.Max(furthest, Vector3.Distance(start, game.View.PlayerVisual.transform.position));
            await Task.Delay(20);
        }
        if (!game.State.openingCompleted || game.State.day != 1 || !game.State.onHarbor || !game.View.Harbor.NearShop(game.State)
            || game.Busy || game.ActionCount != oldActions || game.State.fish != 3 || game.State.shrimp != 2)
            throw new Exception("Natural completion did not leave the same character, real inventory and input at the main-map shop.");
        if (furthest < 1f || captions.Count < 3) throw new Exception("Insufficient live route/shot progress observed.");
        report.Add("PASS: natural Timeline playback moves the original character through the boat-to-shop route.");
        report.Add("PASS: completion stays in HabitatIsland, unlocks input and leaves Day 1 / 3 fish / 2 shrimp at the shop.");
        int fish = game.State.fish, shrimp = game.State.shrimp;
        if (game.BeginOpening() || game.State.fish != fish || game.State.shrimp != shrimp) throw new Exception("Repeated opening grants duplicated catch.");
        report.Add("PASS: a completed opening cannot replay or award duplicate inventory.");
        await Task.Delay(200);
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, "opening-finished.png"));
        string result = string.Join("\n", report);
        File.WriteAllText(Path.Combine(folder, "opening-checks.txt"), result);
        return result;
    }
}
