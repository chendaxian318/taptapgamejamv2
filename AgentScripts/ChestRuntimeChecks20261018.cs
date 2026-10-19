using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;

// Controlled animation/transaction fixtures. No player saves are written and no
// artificial frame advancement is used. Invoke Run() once from the Unity C# tool.
public static class ChestRuntimeChecks20261018
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Folder = "Verification/latest-plan-20261018";
    static HabitatGame game;
    static StringBuilder report;
    static bool running;
    static int passed;
    static double deadline;
    static double Now => UnityEditor.EditorApplication.timeSinceStartup;
    static readonly string[] Guides = { "move", "pull", "climb", "rod", "basket", "fan", "crab", "jellyfish", "tide", "shovel", "magnet", "launcher" };
    static object Call(string name, params object[] args) => typeof(HabitatGame).GetMethod(name, F).Invoke(game, args);
    static void Set(string name, object value) => typeof(HabitatGame).GetField(name, F).SetValue(game, value);
    static void Check(bool value, string name)
    { report.AppendLine((value ? "PASS " : "FAIL ") + name); if (!value) throw new InvalidOperationException(name); passed++; }
    static async Task Frames(int count = 1)
    {
        int target = Time.frameCount + count;
        double until = Math.Min(deadline, Now + 10);
        while (Time.frameCount < target)
        {
            if (!Application.isPlaying || Now > until) throw new TimeoutException("Real Unity frames stopped during chest check.");
            await Task.Delay(8);
        }
    }
    static async Task Until(Func<bool> condition, string label, float timeout = 8)
    {
        double until = Math.Min(deadline, Now + timeout);
        while (!condition())
        {
            if (Now > until) throw new TimeoutException(label + "; " + game.State.message);
            await Frames();
        }
    }
    static async Task Hold(float seconds)
    { double until = Now + seconds; while (Now < until) await Frames(); }
    static void Quiet()
    {
        game.Hud.ClosePanels(); game.paused = true; game.tool = 0; game.playOpeningOnNewGame = false;
        typeof(HabitatGame).GetProperty("SessionStarted").SetValue(game, true);
        foreach (string field in new[] { "chapterGreetingPending", "chapterChestPending", "chapterMorningGreetingPending" }) Set(field, false);
        var s = game.State;
        s.openingCompleted = true;
        s.chapterOneAccepted = s.chapterOneSailed = s.chapterOneDelivered = s.chapterOneMorningSeen = true;
        s.seenGuideKeys = Guides.ToList(); s.campaignUnlockedStage = 5; s.campaignBriefedMask = 62;
    }
    static async Task Fixture(ChestReward reward)
    {
        game.CancelPendingAction(); game.Hud.ClosePanels();
        // A deliberately isolated flat fixture: these checks establish controller
        // invariants, not a claim that the authored puzzle was solved.
        var s = new HabitatState {
            width = 7, depth = 7, tiles = Enumerable.Range(0,49).Select(_ => new Tile { height = 1 }).ToArray(),
            playerX = 2, playerZ = 3, facingX = 1, facingZ = 0, dockX = 0, dockZ = 3, dockRotation = 3,
            levelIndex = reward == ChestReward.BackBasket ? 6 : 5, levelName = "宝箱事务受控验证", authoredLevel = true,
            coins = 0, cargo = 0, traps = 0, rod = false, specialRod = false, backBasketUnlocked = false,
            sourceNodeId = "controlled-chest-test-not-authored-level", objective = "动画结束一次结算"
        };
        s.tiles[3 + 3 * s.width].prop = PropKind.Chest; s.tiles[3 + 3 * s.width].chestReward = reward;
        game.State = s; game.ActionCount = 0;
        ((Stack<HabitatState>)typeof(HabitatGame).GetField("history", F).GetValue(game)).Clear();
        game.tuning = new Tuning(); Quiet(); Call("ResetPlayTime");
        game.View.Initialize(s); game.View.FocusPlayer(); game.Select(3, 3);
        await Frames(3); await Until(() => game.View.ResidentsArrived(game.State), "Fixture resident arrival");
    }
    static bool NoReward() => !game.State.rod && !game.State.specialRod && !game.State.backBasketUnlocked
        && game.State.coins == 0 && game.State.openedChests == 0;
    static bool HasReward(ChestReward reward) => game.State.openedChests == 1
        && game.State.rod == (reward == ChestReward.FishingRod)
        && game.State.specialRod == (reward == ChestReward.FishingRod)
        && game.State.rodFromChest == (reward == ChestReward.FishingRod)
        && game.State.backBasketUnlocked == (reward == ChestReward.BackBasket)
        && game.State.coins == (reward == ChestReward.Coins ? 20 : 0);
    static string Hash(string path)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }
    static Dictionary<string,string> SaveHashes(string directory) => Directory.Exists(directory)
        ? Directory.GetFiles(directory,"*",SearchOption.AllDirectories).ToDictionary(p => p, Hash) : new Dictionary<string,string>();

    public static async Task<string> Run()
    {
        if (running) throw new InvalidOperationException("Another chest check is running.");
        game = HabitatGame.Instance;
        if (!game || !Application.isPlaying || game.Busy || game.Fishing.IsOpen) throw new InvalidOperationException("Requires idle game in Play mode.");
        running = true; passed = 0; deadline = Now + 100;
        report = new StringBuilder("Chest transaction controller checks / real Unity Update frames / controlled fixtures.\n");
        Directory.CreateDirectory(Folder);
        object snapshot = Call("CaptureSnapshot"); string message = game.State.message;
        bool pause = game.paused, session = game.SessionStarted, opening = game.playOpeningOnNewGame;
        int tool = game.tool; float speed = game.speed;
        var history = (Stack<HabitatState>)typeof(HabitatGame).GetField("history", F).GetValue(game);
        var savedHistory = history.ToArray();
        var fields = new[] { "chapterGreetingPending", "chapterChestPending", "chapterMorningGreetingPending", "chapterReturnReminderShown", "campaignReturnReminderLevel" }
            .ToDictionary(n => n, n => typeof(HabitatGame).GetField(n,F).GetValue(game));
        var camera = game.View.GetType().GetFields(F).Where(f => !f.IsInitOnly && !f.IsStatic && (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(Vector3)))
            .ToDictionary(f => f, f => f.GetValue(game.View));
        string saveDirectory = Path.Combine(Application.persistentDataPath, "Playtests");
        var saves = SaveHashes(saveDirectory);
        string outcome = "FAIL not completed";
        try
        {
            await Fixture(ChestReward.FishingRod);
            var visual = game.View.ChestVisualAt(3,3);
            Check(visual && visual.UsesDeliveredWood && visual.OpeningProgress == 0, "Closed chest uses delivered wood/edge materials and a real lid");
            game.ApplyTool();
            Check(game.Busy && game.ChestOpening && NoReward() && game.ActionCount == 0 && game.UndoCount == 0, "Opening starts without changing authoritative inventory, count or undo history");
            game.ApplyTool(); game.Move(0,1,true); game.Buy("crate"); game.Step(); game.Undo();
            Check(NoReward() && game.ActionCount == 0 && game.State.playerX == 2 && game.State.playerZ == 3 && game.UndoCount == 0,
                "Duplicate use, move, buy, tick and undo are blocked while opening");
            await Until(() => visual.OpeningProgress >= .40f, "Lid should visibly open");
            Check(game.ChestOpening && visual.LidAngle > 20 && NoReward() && HabitatRules.TileAt(game.State,3,3).prop == PropKind.Chest,
                "Visible lid animation precedes chest removal and reward");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Folder,"chest-opening.png"))); await Frames(2);
            game.CancelPendingAction();
            Check(!game.Busy && !game.ChestOpening && NoReward() && visual.OpeningProgress == 0 && Math.Abs(visual.LidAngle) < .01f,
                "Cancel closes the actual lid and grants no reward");
            await Hold(1.5f);
            Check(NoReward() && game.ActionCount == 0 && game.UndoCount == 0, "Cancelled opening has no late completion callback");

            foreach (ChestReward reward in new[] { ChestReward.FishingRod, ChestReward.BackBasket, ChestReward.Coins, ChestReward.Unspecified })
            {
                await Fixture(reward); game.ApplyTool();
                Check(game.Busy && NoReward(), reward + ": no reward on button press");
                await Until(() => !game.Busy, reward + " animation completion"); await Frames(2);
                Check(HasReward(reward) && HabitatRules.TileAt(game.State,3,3).prop == PropKind.None && game.ActionCount == 1 && game.UndoCount == 1,
                    reward + ": existing reward semantics committed once after lid completion");
                game.ApplyTool(); await Hold(.2f);
                Check(HasReward(reward) && game.ActionCount == 1 && game.UndoCount == 1, reward + ": repeated use cannot pay again");
                game.Undo(); await Frames(2);
                visual = game.View.ChestVisualAt(3,3);
                Check(NoReward() && HabitatRules.TileAt(game.State,3,3).prop == PropKind.Chest && visual && visual.OpeningProgress == 0 && game.UndoCount == 0,
                    reward + ": undo restores reward inventory and closed chest together");
                game.ApplyTool(); await Until(() => !game.Busy, reward + " reopening after undo");
                Check(HasReward(reward) && game.ActionCount == 2 && game.UndoCount == 1, reward + ": reopen after undo grants only the undone reward once");
            }

            await Fixture(ChestReward.FishingRod); game.ApplyTool();
            await Until(() => game.View.ChestVisualAt(3,3).OpeningProgress > .25f, "Partial opening before switching level");
            game.LoadLevel(5); Quiet(); await Hold(1.6f);
            Check(!game.Busy && !game.ChestOpening && !game.State.specialRod && game.State.openedChests == 0 && game.ActionCount == 0,
                "Switching map cancels previous opening without granting its pending reward");
            outcome = "PASS " + passed + " chest controller checks";
        }
        catch (Exception error)
        { outcome = "FAIL chest: " + error.Message; report.AppendLine(error.ToString()); }
        finally
        {
            try
            {
                game.CancelPendingAction(); game.Hud.ClosePanels(); Call("RestoreSnapshot", snapshot); game.State.message = message;
                history.Clear(); for (int i=savedHistory.Length-1;i>=0;i--) history.Push(savedHistory[i]);
                foreach (var pair in fields) Set(pair.Key,pair.Value);
                foreach (var pair in camera) pair.Key.SetValue(game.View,pair.Value);
                game.paused=pause; game.tool=tool; game.speed=speed; game.playOpeningOnNewGame=opening;
                typeof(HabitatGame).GetProperty("SessionStarted").SetValue(game,session);
                var after=SaveHashes(saveDirectory);
                bool same=saves.Count==after.Count&&saves.All(pair=>after.TryGetValue(pair.Key,out string value)&&value==pair.Value);
                Check(same,"Player save SHA256 unchanged; original snapshot, history, controls and camera restored");
                File.WriteAllText(Path.Combine(Folder,"chest-runtime-save-hashes.txt"),string.Join("\n",saves.Select(p=>p.Value+"  "+p.Key)));
            }
            catch(Exception restoreError) { outcome="FAIL restore: "+restoreError.Message+"; "+outcome; }
            report.AppendLine(outcome); File.WriteAllText(Path.Combine(Folder,"chest-runtime.txt"),report.ToString()); running=false;
        }
        return outcome;
    }
}
