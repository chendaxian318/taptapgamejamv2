using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace HabitatIsland.Editor
{
    /// <summary>Controller integration checks. Invoke Run while the playtest scene is in Play mode.</summary>
    public static class HabitatRuntimeChecks
    {
        static int count;
        static StringBuilder report;
        static void Check(bool condition, string label)
        {
            count++;
            if (!condition) throw new InvalidOperationException("Runtime check failed: " + label);
            report.AppendLine("PASS " + count + ": " + label);
        }

        static void Tool(HabitatGame game, int tool, int x, int z)
        {
            game.tool = tool;
            game.Select(x, z);
            game.ApplyTool();
        }

        static void Present(HabitatGame game)
        {
            game.View.Render(game.State, game.tuning, game.overlays);
            game.Hud.Refresh();
            Canvas.ForceUpdateCanvases();
            Check(game.View.GameCamera != null && game.View.GameCamera.enabled,
                "level " + (game.State.levelIndex + 1) + " renders with live camera and HUD");
        }

        static void WaitForFirstBirth(HabitatGame game)
        {
            // Explicit controller single-steps test the real pause/snapshot path.
            for (int i = 0; i < 19; i++) game.Step();
        }

        static void CheckFishing(HabitatGame game)
        {
            Animal fish = game.State.animals.Find(a => a.species == Species.Goldfish);
            Animal jelly = game.State.animals.Find(a => a.species == Species.Jellyfish);
            Check(fish != null && fish.offspring > 0 && jelly != null,
                "sandbox supplies a bred goldfish for fishing integration");

            // Focus this fixture on fishing; walking/carrying routes are checked above.
            int bestDistance = int.MaxValue;
            for (int z = 0; z < game.State.depth; z++)
                for (int x = 0; x < game.State.width; x++)
                {
                    Tile tile = HabitatRules.TileAt(game.State, x, z);
                    int distance = Math.Abs(x - fish.x) + Math.Abs(z - fish.z);
                    if (distance >= bestDistance || HabitatRules.IsWater(game.State, x, z) ||
                        tile.prop == PropKind.Obstacle || tile.prop == PropKind.Fan || tile.prop == PropKind.Chest) continue;
                    bestDistance = distance;
                    game.State.playerX = x; game.State.playerZ = z;
                }
            Check(bestDistance <= 4 && !HabitatRules.IsWater(game.State, game.State.playerX, game.State.playerZ),
                "fishing fixture stands on legal land within rod range");

            // Regression: another species appears earlier in the list on the same water tile.
            // A goldfish catch must harvest only goldfish offspring.
            jelly.x = fish.x; jelly.z = fish.z; jelly.offspring = 1;
            int fishId = fish.id, jellyId = jelly.id, offspring = fish.offspring;
            int inventory = game.State.fish, actions = game.ActionCount, undo = game.UndoCount;
            game.paused = false;
            Tool(game, 4, fish.x, fish.z);
            Check(game.Fishing.IsOpen && game.paused && fish.offspring == offspring && game.State.fish == inventory,
                "harvest tool opens fishing challenge and pauses without collecting");
            Canvas.ForceUpdateCanvases();
            game.Fishing.Cancel();
            Check(!game.Fishing.IsOpen && !game.paused && fish.offspring == offspring && game.State.fish == inventory,
                "cancel restores prior running state and preserves fish offspring");
            Check(game.ActionCount == actions && game.UndoCount == undo,
                "cancelled cast does not create a harvest transaction");

            Tool(game, 4, fish.x, fish.z);
            Check(game.Fishing.IsOpen, "failed-attempt fixture opens a fresh cast");
            for (int i = 0; i < 4000 && game.Fishing.IsOpen; i++) game.Fishing.Advance(.01f, false);
            Check(!game.Fishing.IsOpen && game.Fishing.Progress < 1f && !game.paused,
                "never holding input makes fish escape and restores pause state");
            Check(fish.offspring == offspring && game.State.fish == inventory && game.ActionCount == actions && game.UndoCount == undo,
                "failed fishing preserves offspring, inventory and undo history");

            Tool(game, 4, fish.x, fish.z);
            Check(game.Fishing.IsOpen, "successful-attempt fixture opens a fresh cast");
            int trackingSteps = 0;
            while (trackingSteps < 4000 && game.Fishing.IsOpen)
            {
                bool held = game.Fishing.Bar < game.Fishing.Fish;
                game.Fishing.Advance(.01f, held);
                trackingSteps++;
            }
            Check(!game.Fishing.IsOpen && game.Fishing.Progress >= 1f && !game.paused,
                "hold/release feedback tracking succeeds at default fishing difficulty");
            report.AppendLine("Fishing tracking duration: " + (trackingSteps * .01f).ToString("0.00") + " simulated seconds.");
            Check(game.State.fish == inventory + offspring && fish.offspring == 0 && game.State.animals.Contains(fish),
                "successful catch adds fish inventory, consumes offspring and preserves adult");
            Check(jelly.offspring == 1,
                "goldfish species filter preserves cohabiting jellyfish offspring");
            Check(game.ActionCount == actions + 1 && game.UndoCount == undo + 1,
                "successful catch creates exactly one undoable harvest transaction");
            game.Undo();
            Check(game.State.fish == inventory && game.State.animals.Find(a => a.id == fishId).offspring == offspring &&
                  game.State.animals.Find(a => a.id == jellyId).offspring == 1 && game.paused && !game.Fishing.IsOpen,
                "undo restores harvested fish offspring and inventory without reopening fishing");
        }

        public static string Run()
        {
            if (!Application.isPlaying || HabitatGame.Instance == null)
                throw new InvalidOperationException("Enter Play mode in the Habitat Island scene before running runtime checks.");

            HabitatGame game = HabitatGame.Instance;
            string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/runtime-checks.txt"));
            string latestPath = Path.Combine(Application.persistentDataPath, "Playtests/latest.json");
            bool hadLatest = File.Exists(latestPath);
            byte[] previousLatest = hadLatest ? File.ReadAllBytes(latestPath) : null;
            bool wroteLatest = false;
            Exception failure = null;
            count = 0;
            report = new StringBuilder("Habitat Island runtime integration checks\nUTC: " + DateTimeOffset.UtcNow.ToString("O") + "\n");
            try
            {
                Check(game.View != null && game.Hud != null && game.Fishing != null, "controller references initialized");
                game.Hud.ClosePanels();
                game.tuning = new Tuning();
                game.speed = 1;
                game.LoadLevel(0);
                game.paused = true;
                Present(game);
                Check(game.State.levelIndex == 0 && !game.State.won, "tutorial starts unsolved");

                Tool(game, 0, 2, 2);
                int shrimpId = game.State.carryingAnimal;
                Check(shrimpId >= 0 && game.State.animals.Find(a => a.id == shrimpId).carried,
                    "ApplyTool picks up the tutorial shrimp");
                Tool(game, 0, 1, 3);
                Check(game.State.carryingAnimal == -1 && game.State.animals.Find(a => a.id == shrimpId).z == 3,
                    "ApplyTool places shrimp in destination cage");
                Check(game.ActionCount == 2 && game.UndoCount == 2, "successful tool actions create undo snapshots");
                game.Undo();
                Check(game.State.carryingAnimal == shrimpId && game.State.animals.Find(a => a.id == shrimpId).carried && game.paused,
                    "Undo restores held animal and pauses simulation");
                Tool(game, 0, 1, 3);
                WaitForFirstBirth(game);
                Check(game.State.won && game.State.earnedSpecies.Contains(Species.Crab),
                    "default-duration tutorial completes via real controller Step");
                Check(game.State.animals.Find(a => a.species == Species.Crab).offspring > 0,
                    "tutorial breeding preserves adult and creates offspring");
                float beforeUndoTime = game.State.elapsed;
                game.Undo();
                Check(game.State.elapsed < beforeUndoTime && game.paused, "Undo reverses controller time step");

                game.SaveSession();
                wroteLatest = File.Exists(latestPath);
                Check(wroteLatest && !string.IsNullOrEmpty(game.LastExport) && File.Exists(game.LastExport),
                    "SaveSession writes timestamped and latest JSON");
                int savedCoins = game.State.coins;
                float savedElapsed = game.State.elapsed;
                game.State.coins = savedCoins + 987;
                game.State.animals.Clear();
                // Regression: the Lab remembers a running state before loading a paused save.
                game.paused = false;
                game.Hud.ToggleSettings();
                game.LoadSession();
                Check(game.State.coins == savedCoins && game.State.animals.Count == 4 && Math.Abs(game.State.elapsed - savedElapsed) < .01f,
                    "LoadSession restores resources, animals and elapsed time");
                Check(game.paused && game.UndoCount == 0, "LoadSession pauses and resets undo history");
                Check(!game.Hud.ModalOpen && game.paused,
                    "LoadSession from open Lab closes its modal and preserves loaded pause state");

                game.paused = false;
                game.Hud.ToggleSettings();
                Check(game.Hud.ModalOpen && game.paused, "opening settings pauses simulation");
                game.Hud.ClosePanels();
                Check(!game.Hud.ModalOpen && !game.paused, "closing settings restores running state");
                game.paused = true;
                game.Hud.ToggleHelp();
                game.Hud.ClosePanels();
                Check(game.paused, "closing help preserves a preexisting pause");

                // Regression: closing the old Lab must not overwrite the new level's pause default.
                game.paused = false;
                game.Hud.ToggleSettings();
                game.LoadLevel(1);
                Check(game.State.levelIndex == 1 && game.paused, "lab loads paused for controlled setup");
                Check(!game.Hud.ModalOpen && game.paused,
                    "LoadLevel from open Lab closes its modal and preserves chapter-two pause default");
                game.tuning.animalMovement = false;
                int stock = game.State.cargo;
                game.Hud.ToggleShop();
                game.Buy("soil");
                Check(game.State.carryingProp == PropKind.Climb && game.State.cargo == stock - 1,
                    "Buy withdraws one dock cargo item through shop");
                int coins = game.State.coins;
                int actions = game.ActionCount;
                game.Buy("crate");
                Check(game.State.coins == coins && game.ActionCount == actions && game.State.carryingProp == PropKind.Climb,
                    "full hands reject second purchase without charge or action snapshot");
                game.Hud.ClosePanels();
                game.Move(1, 0); game.Move(1, 0);
                Check(game.State.playerX == 3 && game.State.playerZ == 2, "controller walks to pond closure");
                Tool(game, 1, 4, 2);
                Check(HabitatRules.TileAt(game.State, 4, 2).height == 1 && game.State.carryingProp == PropKind.None,
                    "soil tool seals pond from adjacent tile");
                game.Move(-1, 0); game.Move(-1, 0);
                game.Buy("soil");
                game.Move(0, 1); game.Move(0, 1); game.Move(0, 1);
                game.Move(1, 0); game.Move(1, 0); game.Move(1, 0); game.Move(1, 0);
                Check(game.State.playerX == 5 && game.State.playerZ == 5, "carried soil reaches highland by walkable route");
                Tool(game, 1, 6, 5);
                Check(HabitatRules.TileAt(game.State, 6, 5).height == 2, "second soil block connects turtle highland");
                WaitForFirstBirth(game);
                Check(game.State.won && game.State.earnedSpecies.Contains(Species.Goldfish) && game.State.earnedSpecies.Contains(Species.Turtle),
                    "pond and highland goals complete through controller actions");
                Present(game);

                game.LoadLevel(2);
                game.paused = true;
                game.Move(1, 0); game.Move(0, 1); game.Move(0, 1);
                Check(game.State.playerX == 2 && game.State.playerZ == 4, "controller reaches tide crate using stairs");
                game.Move(1, 0);
                Tile pressure = HabitatRules.TileAt(game.State, 4, 4);
                Check(game.State.tideActive && game.State.waterLevel == 1 && pressure.coveredProp == PropKind.Tide && pressure.prop == PropKind.Crate,
                    "controller push activates crate-covered pressure plate");
                WaitForFirstBirth(game);
                Check(game.State.won, "tide layout breeds jellyfish and turtle after activation");
                Present(game);

                game.LoadLevel(3);
                game.paused = true;
                WaitForFirstBirth(game);
                Check(game.State.won && game.State.earnedSpecies.Count == 6,
                    "six-species sandbox completes with autonomous movement disabled");
                Present(game);
                CheckFishing(game);
            }
            catch (Exception e)
            {
                failure = e;
                report.AppendLine("FAIL: " + e);
            }
            finally
            {
                try
                {
                    if (wroteLatest)
                    {
                        if (hadLatest) File.WriteAllBytes(latestPath, previousLatest);
                        else File.Delete(latestPath);
                    }
                    game.Hud.ClosePanels();
                    game.tuning = new Tuning();
                    game.speed = 1;
                    game.tool = 0;
                    game.LoadLevel(0);
                    game.paused = true;
                    game.View.Render(game.State, game.tuning, game.overlays);
                    game.Hud.Refresh();
                    report.AppendLine("Cleanup: tutorial reset, simulation paused, preexisting latest save preserved.");
                }
                catch (Exception cleanupError)
                {
                    report.AppendLine("Cleanup failed: " + cleanupError);
                    if (failure == null) failure = cleanupError;
                }
                report.AppendLine((failure == null ? "PASS: " : "FAILED after ") + count + " runtime integration assertions.");
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, report.ToString());
            }
            if (failure != null) throw new InvalidOperationException("Habitat runtime checks failed; see " + reportPath, failure);
            string result = "PASS: " + count + " runtime integration assertions. Report: " + reportPath;
            Debug.Log("[HabitatIsland] " + result);
            return result;
        }
    }
}
