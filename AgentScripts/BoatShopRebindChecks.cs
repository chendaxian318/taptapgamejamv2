using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run only in the disposable Play session after the opening and before accepting
// the commission. Uses real sailing callbacks; never saves or changes PlayerPrefs.
public static class BoatShopRebindChecks
{
    static readonly string ReportPath = Path.GetFullPath("Verification/boat-shop-20261016/rebind-checks.txt");
    static double Clock => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    static async Task Frames(int count)
    {
        int start = Time.frameCount; double deadline = Clock + 15;
        while (Time.frameCount - start < count)
        {
            if (!Application.isPlaying || Clock > deadline) throw new Exception("Play frames stopped during rebind verification.");
            await Task.Delay(15);
        }
    }

    public static async Task<string> Run()
    {
        HabitatGame game = HabitatGame.Instance;
        if (!Application.isPlaying || !game || game.State == null || game.Busy || !game.Story || !game.View
            || game.State.levelIndex != 4 || !game.State.openingCompleted || game.State.chapterOneAccepted
            || game.State.carryingAnimal >= 0 || game.State.carryingProp != PropKind.None)
            throw new Exception("Requires idle index 4 after the opening, before commission acceptance, with empty hands.");
        var report = new StringBuilder(); int checks = 0;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        void Flush() => File.WriteAllText(ReportPath, report.ToString());
        void Check(bool value, string label)
        {
            report.AppendLine((value ? "PASS " : "FAIL ") + (++checks) + ": " + label); Flush();
            if (!value) throw new Exception(label);
        }
        bool priorPause = game.paused;
        var originalPlayer = game.View.PlayerVisual;
        HabitatState tideFixture = null; int originalWater = 0;
        Transform previousModel = null, previousBoat = null, previousBoss = null;
        Exception failure = null;

        void Inspect(string label, bool requireNew)
        {
            Transform model = game.View.Harbor.ModelTransform;
            var boat = model ? model.GetComponent<HabitatHarborBoat>() : null;
            var boss = game.GetComponent<HabitatShopkeeperPlaceholder>();
            Check(model && boat && boat.Vessel && boat.Footwell && boat.Seat
                && boss && boss.PlaceholderRoot && boss.UsingArtistPrefab
                && boss.Animator && boss.Animator.runtimeAnimatorController,
                label + ": authored boat, seat/footwell anchors, and animated shopkeeper are bound");
            var models = model.GetComponentsInChildren<Transform>(true);
            // FindObjectsByType excludes HideFlags.DontSave actors. Traverse the
            // active scene hierarchy instead so the runtime shopkeeper is counted.
            var sceneTransforms = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            Check(models.Count(t => t.name == "Wooden boat - berth and story") == 1
                && models.Count(t => t.name == "Harbor shopkeeper") == 1
                && models.Count(t => t.name == "Shopkeeper art") == 1
                && sceneTransforms.Count(t => t.GetComponent<HabitatHarborBoat>() != null) == 1
                && sceneTransforms.Count(t => t.name == "Harbor shopkeeper") == 1,
                label + ": exactly one vessel and one shopkeeper; no stale harbor duplicates");
            Check(game.View.PlayerVisual == originalPlayer && originalPlayer && originalPlayer.IsModelActive,
                label + ": the original player component and visible character remain present");
            if (requireNew)
                Check(model != previousModel && boat.Vessel != previousBoat && boss.PlaceholderRoot != previousBoss
                    && !previousModel && !previousBoat && !previousBoss,
                    label + ": old harbor, vessel and shopkeeper were destroyed and replaced");
            previousModel = model; previousBoat = boat.Vessel; previousBoss = boss.PlaceholderRoot;
        }

        async Task Sail(int destination)
        {
            game.Hud.ClosePanels(); game.ReturnToHarbor(); await Frames(3);
            Check(!game.Busy && game.View.Harbor.NearBoat(game.State), "boarding point is ready before sailing to " + destination);
            int source = game.State.levelIndex;
            game.SailToLevel(destination);
            Check(game.StoryPlaying && game.State.levelIndex == source,
                "SailToLevel(" + destination + ") begins departure without committing early");
            Check(game.Story.Skip(), "departure completes through the real Skip callback to " + destination);
            await Frames(5);
            Check(game.State.levelIndex == destination && !game.Busy && game.View.Harbor.NearBoat(game.State),
                "real sailing callback reaches island " + destination + " at its harbor");
        }

        try
        {
            game.paused = true; game.Hud.ClosePanels(); await Frames(3);
            Inspect("Initial island 4", false);
            await Sail(5); Inspect("Island 5", true);
            await Sail(4); Inspect("Returned island 4", true);
            Check(!game.State.chapterOneAccepted, "round-trip required no commission or inventory fixture");

            report.AppendLine("PRESENTATION FIXTURE: temporarily raise waterLevel by one while paused, then restore it. This checks tide-triggered rendering/rebinding, not pressure-plate gameplay."); Flush();
            tideFixture = game.State; originalWater = tideFixture.waterLevel;
            float originalBoatY = game.View.Harbor.ModelTransform.GetComponent<HabitatHarborBoat>().Vessel.position.y;
            tideFixture.waterLevel = originalWater + 1;
            game.View.Render(game.State, game.tuning, game.overlays); await Frames(5);
            Inspect("Raised-water render", true);
            var boat = game.View.Harbor.ModelTransform.GetComponent<HabitatHarborBoat>();
            Check(Mathf.Abs(boat.Vessel.position.y - originalBoatY - .7f) < .002f,
                "rebuilt moored vessel follows the new water level");
            tideFixture.waterLevel = originalWater;
            game.View.Render(game.State, game.tuning, game.overlays); await Frames(5);
            Inspect("Restored-water render", true);
            tideFixture = null;
            Check(game.State.levelIndex == 4 && game.State.waterLevel == originalWater && !game.StoryPlaying,
                "finished on island 4 with original tide and no running story");
            report.AppendLine("No save/export/load or PlayerPrefs API was called. Scope: sailing rebind and temporary tide presentation only; no puzzle completion claim.");
        }
        catch (Exception error) { failure = error; report.AppendLine("ERROR: " + error); }
        finally
        {
            try
            {
                if (game && Application.isPlaying)
                {
                    if (game.StoryPlaying) game.Story.Cancel();
                    if (tideFixture != null)
                    {
                        tideFixture.waterLevel = originalWater;
                        if (ReferenceEquals(game.State, tideFixture)) game.View.Render(game.State, game.tuning, game.overlays);
                    }
                    if (game.State.levelIndex == 5 && !game.Busy)
                    {
                        game.Hud.ClosePanels(); game.ReturnToHarbor();
                        game.SailToLevel(4);
                        if (game.StoryPlaying) game.Story.Skip();
                        await Frames(5);
                    }
                    game.paused = priorPause;
                    report.AppendLine("Cleanup: level=" + game.State.levelIndex + ", water=" + game.State.waterLevel + ", paused=" + game.paused);
                }
            }
            catch (Exception cleanup) { report.AppendLine("CLEANUP ERROR: " + cleanup); if (failure == null) failure = cleanup; }
            Flush();
        }
        if (failure != null) throw new Exception("Boat/shop rebind check failed; see " + ReportPath, failure);
        return "PASS: " + checks + " boat/shop rebind checks; returned to island 4. " + ReportPath;
    }
}
