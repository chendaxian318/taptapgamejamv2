using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HabitatIsland
{
    /// <summary>Opt-in player QA. Nothing is created, queued or written without --habitat-smoke.</summary>
    public sealed class HabitatPlayerSmoke : MonoBehaviour
    {
        [Serializable]
        sealed class SmokeReport
        {
            public bool pass;
            public string detail;
            public string utc;
            public int frames, renderWidth, renderHeight;
            public string screenshot;
            public List<string> checks = new List<string>();
        }

        string directory;
        Keyboard testKeyboard;
        Mouse testMouse;
        InputSettings.BackgroundBehavior originalBackgroundBehavior;
        bool settingsChanged;
        readonly SmokeReport report = new SmokeReport();
        int firstFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--habitat-smoke") < 0) return;
            if (FindAnyObjectByType<HabitatPlayerSmoke>() != null) return;
            string path = Path.Combine(Application.persistentDataPath, "PlayerSmoke");
            int index = Array.IndexOf(args, "--habitat-smoke-dir");
            if (index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)) path = args[index + 1];
            GameObject go = new GameObject("Habitat explicit player smoke");
            DontDestroyOnLoad(go);
            HabitatPlayerSmoke smoke = go.AddComponent<HabitatPlayerSmoke>();
            smoke.directory = Path.GetFullPath(path);
            HabitatGame.SaveSlotDirectoryForTests=Path.Combine(smoke.directory,"saves");
            smoke.StartCoroutine(smoke.Run());
        }

        IEnumerator Run()
        {
            firstFrame = Time.frameCount;
            for (int i = 0; i < 60; i++) yield return null;
            IEnumerator steps = RunInputChecks();
            bool failed = false;
            while (true)
            {
                bool more = false;
                try { more = steps.MoveNext(); }
                catch (Exception e)
                {
                    failed = true;
                    report.detail = e.GetType().Name + ": " + e.Message;
                    Debug.LogError("[HabitatSmoke] " + report.detail);
                }
                if (failed || !more) break;
                yield return steps.Current;
            }
            CleanupInput();
            HabitatGame game = HabitatGame.Instance;
            try
            {
                if (game != null) { game.LoadLevel(0); game.paused = false; }
                Directory.CreateDirectory(directory);
            }
            catch (Exception e) { failed = true; report.detail = "Reset/output directory failed: " + e.Message; }
            for (int i = 0; i < 5; i++) yield return null;
            report.screenshot = Path.Combine(directory, "smoke-player.png");
            try { ScreenCapture.CaptureScreenshot(report.screenshot); }
            catch (Exception e) { failed = true; report.detail = "Screenshot failed: " + e.Message; }
            // CaptureScreenshot writes after frame rendering; allow the image and IO to flush.
            for (int i = 0; i < 5; i++) yield return null;
            if (!File.Exists(report.screenshot)) { failed = true; report.detail = "Screenshot file was not produced."; }
            report.pass = !failed;
            if (!failed) report.detail = "Startup, keyboard pause, keyboard movement, pointer tile selection and screenshot passed.";
            report.utc = DateTime.UtcNow.ToString("o");
            report.frames = Time.frameCount - firstFrame;
            report.renderWidth = Screen.width; report.renderHeight = Screen.height;
            try { File.WriteAllText(Path.Combine(directory, "smoke-result.json"), JsonUtility.ToJson(report, true)); }
            catch (Exception e) { report.pass = false; Debug.LogError("[HabitatSmoke] Could not write result: " + e.Message); }
            Debug.Log("[HabitatSmoke] " + (report.pass ? "PASS" : "FAIL") + " — " + report.detail);
            yield return null;
            if (!Application.isEditor) Application.Quit(report.pass ? 0 : 2);
        }

        IEnumerator RunInputChecks()
        {
            HabitatGame game = HabitatGame.Instance;
            Require(game != null && game.State != null, "Game and model initialized");
            Require(game.View != null && game.View.GameCamera != null && game.Hud != null && game.Fishing != null, "View, camera, HUD and fishing initialized");
            Require(Screen.width > 0 && Screen.height > 0, "Player render dimensions are nonzero");
            originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settingsChanged = true;
            testKeyboard = InputSystem.AddDevice<Keyboard>("HabitatSmokeKeyboard"); testKeyboard.MakeCurrent();
            testMouse = InputSystem.AddDevice<Mouse>("HabitatSmokeMouse"); testMouse.MakeCurrent();
            game.playOpeningOnNewGame=false;game.NewSession();game.LoadLevel(0);game.Hud.ClosePanels();game.paused = false;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            InputSystem.QueueStateEvent(testMouse, new MouseState());
            yield return null;
            yield return null;

            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Space));
            yield return null;
            Require(game.paused, "Queued Space press toggles pause on");
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
            Require(game.paused, "Queued Space release does not toggle pause again");

            Require(game.State.playerX == 1 && game.State.playerZ == 2, "Tutorial starts at raw grid (1,2)");
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.W));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            float arrivalDeadline=Time.realtimeSinceStartup+5f;
            while(game.Busy&&Time.realtimeSinceStartup<arrivalDeadline)yield return null;
            Require(!game.Busy,"Queued movement animation finishes before deadline");
            Require(game.State.playerX == 1 && game.State.playerZ == 3, "Queued W press moves to raw grid (1,3)");
            yield return null;

            Vector3 projected = game.View.GameCamera.WorldToScreenPoint(new Vector3(2f, .75f, 5f));
            Require(projected.z > 0 && projected.x > 0 && projected.x < Screen.width && projected.y > 0 && projected.y < Screen.height, "Tile (2,5) projects inside the player window");
            Vector2 pointer = new Vector2(projected.x, projected.y);
            // Moving first lets EventSystem update its pointer-over-HUD state before the click.
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = pointer });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = pointer, buttons = 1 });
            yield return null;
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = pointer });
            Require(game.State.selectedX == 2 && game.State.selectedZ == 5, "Queued left click selects raw grid (2,5)");
            yield return null;
        }

        void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            report.checks.Add(description);
            Debug.Log("[HabitatSmoke] PASS: " + description);
        }

        void CleanupInput()
        {
            if (testKeyboard != null && testKeyboard.added) InputSystem.RemoveDevice(testKeyboard);
            if (testMouse != null && testMouse.added) InputSystem.RemoveDevice(testMouse);
            testKeyboard = null; testMouse = null;
            if (settingsChanged) { InputSystem.settings.backgroundBehavior = originalBackgroundBehavior; settingsChanged = false; }
        }
        void OnDestroy() { CleanupInput(); }
    }
}
