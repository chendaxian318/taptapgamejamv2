using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;

// Load this script through the Unity C# tool and invoke ONE entry at a time.
// Second/Third/Fourth/Fifth each use the real HabitatGame controller and real Update frames.
// This is an island-route fixture, not proof of the preceding campaign purchases/ship journey.
public static class LatestFiveMapRuntime
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Folder = "Verification/optimization-20261018";
    static bool running;
    static HabitatGame game;
    static StringBuilder report;
    static double deadline;
    static int currentStep;
    static int[] currentAction;
    static readonly string[] Guides = { "move", "pull", "climb", "rod", "basket", "fan", "crab", "jellyfish", "tide", "shovel", "magnet", "launcher" };

    public static Task<string> Second() => Run(6, "second", true);
    public static Task<string> Third() => Run(7, "third", true);
    public static Task<string> Fourth() => Run(8, "fourth", true);
    public static Task<string> Fifth() => Run(10, "fifth", true);
    // Diagnostic only; writes separate evidence and must never replace the default result.
    public static Task<string> SecondPausedAnimalsDiagnostic() => Run(6, "second", false);

    static object Call(string method, params object[] args)
    { return typeof(HabitatGame).GetMethod(method, F).Invoke(game, args); }
    static void Set(string field, object value)
    { typeof(HabitatGame).GetField(field, F).SetValue(game, value); }
    static void Check(bool okay, string text)
    { report.AppendLine((okay ? "PASS " : "FAIL ") + text); if (!okay) throw new InvalidOperationException(text); }
    static double Now => UnityEditor.EditorApplication.timeSinceStartup;
    static void CheckDeadline()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Unity Play mode stopped during route.");
        if (Now > deadline) throw new TimeoutException("Route exceeded 115 seconds; no fake frame advancement was used.");
    }
    static async Task Frames(int count = 1)
    {
        int target = Time.frameCount + count;
        double until = Math.Min(deadline, Now + 10);
        while (Time.frameCount < target)
        {
            CheckDeadline();
            if (Now > until) throw new TimeoutException("Unity did not advance the requested real frames.");
            await Task.Delay(8);
        }
    }
    static async Task Until(Func<bool> condition, string label, float seconds = 15)
    {
        double until = Math.Min(deadline, Now + seconds);
        while (!condition())
        {
            CheckDeadline();
            if (Now > until) throw new TimeoutException(label + "; " + StateDescription());
            await Frames();
        }
    }
    static async Task Arrived()
    {
        // A synchronous action still needs a real Render/Update before the visual arrival test.
        await Frames();
        await Until(() => !game.Busy && game.View.ResidentsArrived(game.State), "Waiting for controller and residents to arrive");
    }
    static int[][] ReadRoute(string name)
    {
        string json = File.ReadAllText(Path.Combine(Folder, name + "-route.json"));
        return Regex.Matches(json, @"\[\s*(-?\d+(?:\s*,\s*-?\d+)*)\s*\]")
            .Cast<Match>().Select(m => m.Groups[1].Value.Split(',').Select(x => int.Parse(x.Trim())).ToArray()).ToArray();
    }
    static string StateDescription()
    {
        var s = game.State;
        return "P=" + s.playerX + "," + s.playerZ + " held=" + s.carryingProp + "/" + s.carryingAnimal
            + " coins=" + s.coins + " actions=" + game.ActionCount + " busy=" + game.Busy + " message=" + s.message
            + " residents=" + string.Join("; ", s.animals.Select(a => a.species + "#" + a.id + "@" + a.x + "," + a.z
                + " carried=" + a.carried + " suitable=" + HabitatRules.Evaluate(s, a, game.tuning).suitable));
    }
    static bool Movable(PropKind p) => p == PropKind.Crate || p == PropKind.Float || p == PropKind.ClimbBlock;
    static int MovableCount(HabitatState s) => s.tiles.Count(t => Movable(t.prop)) + (Movable(s.carryingProp) ? 1 : 0) + s.boatCrates + s.bagProps.Count(Movable);
    static string Hash(string path)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }
    static Dictionary<string, string> SaveHashes(string folder)
    { return Directory.Exists(folder) ? Directory.GetFiles(folder, "*", SearchOption.AllDirectories).ToDictionary(p => p, Hash) : new Dictionary<string, string>(); }
    static string Product(int code)
    {
        switch (code) { case 1: return "crate"; case 5: return "shovel"; case 6: return "magnet"; case 7: return "launcher"; default: throw new InvalidDataException("Unknown product code " + code); }
    }
    static void Setup(int level, bool animalMovement)
    {
        game.LoadLevel(level); game.Hud.ClosePanels(); game.paused = true; game.tool = 0; game.speed = 1;
        game.tuning = new Tuning { animalMovement = animalMovement };
        typeof(HabitatGame).GetProperty("SessionStarted").SetValue(game, true);
        game.playOpeningOnNewGame = false;
        foreach (string field in new[] { "chapterGreetingPending", "chapterChestPending", "chapterMorningGreetingPending" }) Set(field, false);
        var s = game.State;
        s.openingCompleted = true; s.chapterOneAccepted = s.chapterOneSailed = s.chapterOneDelivered = s.chapterOneMorningSeen = true;
        s.campaignUnlockedStage = HabitatChapterRules.CampaignNumber(level); s.campaignBriefedMask = 62;
        s.seenGuideKeys = Guides.ToList(); s.rod = true; s.specialRod = true;
        s.backBasketUnlocked = level != 6; // Second must earn its own basket by opening the actual authored chest.
        s.coins = level == 6 ? 200 : level == 10 ? 1150 : 0;
        Call("ResetPlayTime");
        report.AppendLine("PREREQUISITES: inherited earned special rod; " + (level == 6 ? "first commission balance 200, no basket" : "earned basket")
            + (level == 10 ? "; prior campaign balance 1150" : "") + ". These are prior-stage fixtures, not new-game free grants.");
        report.AppendLine("ECOLOGY: paused between route actions; explicit wind simulation retains animalMovement=" + animalMovement + ".");
    }
    static async Task<int> Execute(int[] a)
    {
        if (a.Length < 3) throw new InvalidDataException("Route action requires at least three integers.");
        int before = game.ActionCount;
        int expected = 1;
        switch (a[0])
        {
            case 0: game.Move(a[1], a[2], true); break;
            case 1: game.tool = 0; game.Select(a[1], a[2]); game.ApplyTool(); break;
            case 2: game.tool = 2; game.Select(a[1], a[2]); game.ApplyTool(); break;
            case 3: game.Select(a[1], a[2]); game.RotateTarget(); break;
            case 4:
                game.Buy("fan"); await Arrived();
                Check(game.ActionCount == before + 1 && game.State.carryingProp == PropKind.Fan && game.State.coins == 0,
                    "Real fan purchase consumes inherited 200 exactly once");
                game.StoreInventoryItem(); expected = 2; break;
            case 5: game.TakeInventoryItem(a[1]); break;
            case 6:
                float elapsed = game.State.elapsed;
                // This production controller method divides a riding-float tick into .1-second
                // segments and yields whenever its player/float transaction is pending.
                Call("AdvanceSimulation", a[1] / 10f); expected = 0;
                await Arrived();
                Check(Math.Abs(game.State.elapsed - elapsed - a[1] / 10f) < .025f,
                    "Real segmented simulation elapsed " + (game.State.elapsed - elapsed).ToString("F3") + " seconds");
                break;
            case 7:
                if (a.Length != 6) throw new InvalidDataException("Utility action must be [7,tool,x,z,dx,dz].");
                game.tool = a[1]; game.Select(a[2], a[3]);
                Check(game.ConfirmUtility(a[2], a[3], a[4], a[5]), "Utility confirmation accepted: " + game.State.message); break;
            case 8: game.Buy(Product(a[1])); break;
            case 9: game.TransferCargo(a[1] != 0); break;
            default: throw new InvalidDataException("Unsupported action kind " + a[0]);
        }
        await Arrived();
        Check(game.ActionCount == before + expected, "Action " + currentStep + " [" + string.Join(",", a) + "] committed " + expected + " operation(s); " + StateDescription());
        return expected;
    }
    public static async Task<string> Run(int level, string routeName, bool animalMovement = true)
    {
        if (running) throw new InvalidOperationException("Run these checks serially; another route is active.");
        game = HabitatGame.Instance;
        if (!game || !Application.isPlaying || game.Busy || game.Fishing && game.Fishing.IsOpen)
            throw new InvalidOperationException("Requires an idle HabitatGame in Unity Play mode.");
        running = true; report = new StringBuilder("Latest-plan real Unity controller route; no map/actor position injection.\n");
        currentStep = 0; currentAction = null; deadline = Now + 115;
        Directory.CreateDirectory(Folder);
        string stem = Path.Combine(Folder, routeName + (animalMovement ? "-runtime" : "-runtime-animals-paused-diagnostic"));
        object original = Call("CaptureSnapshot");
        string originalMessage = game.State.message;
        bool originalSession = game.SessionStarted, originalPause = game.paused, originalOpening = game.playOpeningOnNewGame;
        int originalTool = game.tool; float originalSpeed = game.speed;
        var history = (Stack<HabitatState>)typeof(HabitatGame).GetField("history", F).GetValue(game);
        var originalHistory = history.ToArray();
        var fields = new[] { "chapterGreetingPending", "chapterChestPending", "chapterMorningGreetingPending", "chapterReturnReminderShown", "campaignReturnReminderLevel" }
            .ToDictionary(n => n, n => typeof(HabitatGame).GetField(n, F).GetValue(game));
        var cameraFields = game.View.GetType().GetFields(F).Where(f => !f.IsInitOnly && !f.IsStatic && (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(Vector3)))
            .ToDictionary(f => f, f => f.GetValue(game.View));
        string saveFolder = Path.Combine(Application.persistentDataPath, "Playtests");
        var originalHashes = SaveHashes(saveFolder);
        string outcome = "FAIL not completed";
        double started = Now;
        try
        {
            Setup(level, animalMovement); await Arrived();
            int[] terrain = game.State.tiles.Select(t => t.height).ToArray();
            int initialMovables = MovableCount(game.State);
            string routeSource = level == 6 && animalMovement ? "second-default" : routeName;
            report.AppendLine("ROUTE SOURCE: " + routeSource + "-route.json");
            int[][] route = ReadRoute(routeSource);
            Check(route.Length > 0 && !game.State.won, "Loaded nonempty route on original, incomplete authored map");
            int expectedActions = 0;
            for (int i = 0; i < route.Length; i++)
            {
                currentStep = i + 1; currentAction = route[i]; game.Hud.ClosePanels();
                foreach (string field in new[] { "chapterGreetingPending", "chapterChestPending", "chapterMorningGreetingPending" }) Set(field, false);
                expectedActions += await Execute(currentAction);
            }
            await Until(() => game.State.won && !game.Busy && game.View.ResidentsArrived(game.State), "Waiting for actual completion after final arrival", 20);
            Check(game.State.animals.All(a => HabitatRules.Evaluate(game.State, a, game.tuning).suitable), "All authored animals suitable at actual completion");
            Check(game.ActionCount == expectedActions, "Total real committed operations=" + expectedActions + "; encoded route actions=" + route.Length);
            Check(game.State.tiles.Select(t => t.height).SequenceEqual(terrain), "Original terrain heights unchanged");
            Check(MovableCount(game.State) == initialMovables + (level == 10 ? 1 : 0), "All original boxes/float/climb blocks conserved; fifth bought exactly one extra box");
            Check(game.State.coins == (level == 10 ? 700 : 0), "Balance matches real purchases; no completion money invented");
            if (level == 6) Check(game.State.backBasketUnlocked, "Second obtained the actual basket chest");
            if (level == 10) Check(game.State.magnets == 0 && game.State.boatCrates == 0, "Fifth consumed purchased magnet and unloaded purchased crate exactly once");
            if (level == 8) report.AppendLine("DESIGN OBSERVATION: legal alternate solution bypasses tide; not claimed to reproduce PDF standard steps.");
            // Dismiss completion dialogue/summary so evidence shows the actual map and residents.
            game.Hud.ClosePanels(); Set("campaignReturnReminderLevel", level); game.tool = 0; game.View.FocusOverview();
            await Frames(4); game.Hud.ClosePanels(); await Frames(2);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(stem + ".png")); await Frames(5);
            File.WriteAllText(stem + "-snapshot.json", JsonUtility.ToJson(Call("CaptureSnapshot"), true));
            outcome = "PASS " + routeName + ": " + route.Length + " route actions / " + expectedActions + " real operations; animalMovement=" + animalMovement + "; " + (Now - started).ToString("F1") + "s";
            report.AppendLine(outcome);
        }
        catch (Exception ex)
        {
            var actual = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
            outcome = "FAIL " + routeName + " action " + currentStep + " [" + (currentAction == null ? "setup" : string.Join(",", currentAction)) + "]: " + actual.Message;
            report.AppendLine(outcome); report.AppendLine(StateDescription()); report.AppendLine(actual.ToString());
            File.WriteAllText(stem + "-failure-state.json", JsonUtility.ToJson(game.State, true));
        }
        finally
        {
            try
            {
                game.Fishing.Cancel(false); game.CancelPendingAction(); game.Hud.ClosePanels();
                Call("RestoreSnapshot", original); game.State.message = originalMessage;
                history.Clear(); for (int i = originalHistory.Length - 1; i >= 0; i--) history.Push(originalHistory[i]);
                foreach (var pair in fields) Set(pair.Key, pair.Value);
                foreach (var pair in cameraFields) pair.Key.SetValue(game.View, pair.Value);
                game.paused = originalPause; game.tool = originalTool; game.speed = originalSpeed; game.playOpeningOnNewGame = originalOpening;
                typeof(HabitatGame).GetProperty("SessionStarted").SetValue(game, originalSession);
                var after = SaveHashes(saveFolder);
                bool unchanged = originalHashes.Count == after.Count && originalHashes.All(p => after.TryGetValue(p.Key, out string hash) && hash == p.Value);
                report.AppendLine((unchanged ? "PASS " : "FAIL ") + "All player save files SHA256 unchanged; original in-memory snapshot, undo history, settings and camera fields restored.");
                if (!unchanged) outcome = "FAIL player save hashes changed unexpectedly; " + outcome;
                File.WriteAllText(stem + "-save-hashes.txt", string.Join("\n", originalHashes.Select(p => p.Value + "  " + p.Key)));
            }
            catch (Exception restoreError) { outcome = "FAIL restore: " + restoreError.Message + "; " + outcome; report.AppendLine(outcome); }
            finally { running = false; File.WriteAllText(stem + ".txt", report.ToString()); }
        }
        return outcome;
    }
}
