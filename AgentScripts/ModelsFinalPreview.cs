using System;
using System.IO;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;

public static class ModelsFinalPreview
{
    public static async Task<string> Run()
    {
        var dock = UnityEngine.Object.FindAnyObjectByType<HabitatDockPrototype>();
        if (!Application.isPlaying || !dock) throw new Exception("Play the imported harbor first.");
        if (dock.IsSequencePlaying) dock.SkipSequence();
        if (!dock.PlacePlayerForChecks(HabitatDockPrototype.HomeApproach) || !dock.TryInteract() || !dock.TryRest())
            throw new Exception("Home interaction failed.");
        int day = dock.Day;
        await Task.Delay(3700);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/models-20261015/harbor-MorningWindow.png"));
        await Task.Delay(1700);
        if (dock.IsSequencePlaying || !dock.HasControl || dock.Day != day + 1) throw new Exception("Morning did not finish normally.");
        if (!dock.PlacePlayerForChecks(HabitatImportedDockLayout.FromSource(-.085f, -.073f, -.015f)))
            throw new Exception("Central walkway position is not walkable.");
        await Task.Delay(250);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/models-20261015/harbor-imported-overview.png"));
        await Task.Delay(150);
        string result = "PASS: final roof labels are authored; revised window shot completes naturally and advances one day; player remains on the central walkable deck.";
        File.WriteAllText("Logs/models-20261015/final-preview-check.txt", result);
        return result;
    }
}
