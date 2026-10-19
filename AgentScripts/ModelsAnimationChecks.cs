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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Unity CLI run_script entry. All asynchronous observations wait for actual rendered frames.
// This file is outside Assets; it does not add a permanent scene component or change game rules.
public static class ModelsAnimationChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const int ShrimpId = 9701, TrapX = 3, TrapZ = 2;
    static readonly StringBuilder report = new StringBuilder();
    static readonly string[] Sources = {
        "Assets/HabitatIsland/Art/AnimatedShrimp/creature_animated.glb",
        "Assets/HabitatIsland/Art/AnimatedTrap/lobster_trap_rigged_v2.glb"
    };
    static readonly string[] CameraFields = {
        "cameraTarget", "cameraTargetSmooth", "cameraYaw", "cameraSize",
        "overviewSize", "followPlayer", "overviewCamera"
    };
    static int count;
    static bool running;
    static string Folder => Path.GetFullPath("Logs/models-20261015");

    static void Check(bool pass, string label)
    {
        count++;
        if (!pass) throw new InvalidOperationException("FAIL " + count + ": " + label);
        report.AppendLine("PASS " + count + ": " + label);
    }

    static string Hash(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    static async Task Frames(int frames)
    {
        int first = Time.frameCount;
        double deadline = UnityEditor.EditorApplication.timeSinceStartup + 10;
        while (Time.frameCount - first < frames)
        {
            if (!Application.isPlaying || UnityEditor.EditorApplication.timeSinceStartup >= deadline)
                throw new InvalidOperationException("Play stopped or no rendered frames arrived.");
            await Task.Delay(16);
        }
    }

    static async Task Observe(float seconds, Action sample = null)
    {
        float first = Time.unscaledTime;
        int frame = Time.frameCount;
        double deadline = UnityEditor.EditorApplication.timeSinceStartup + seconds + 10;
        while (Time.unscaledTime - first < seconds)
        {
            await Task.Delay(12);
            if (!Application.isPlaying || UnityEditor.EditorApplication.timeSinceStartup >= deadline)
                throw new InvalidOperationException("Play stopped or observation timed out.");
            if (Time.frameCount != frame) { frame = Time.frameCount; sample?.Invoke(); }
        }
    }

    static async Task Press(Keyboard keyboard, Key key)
    {
        keyboard.MakeCurrent();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        await Frames(2);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        await Frames(2);
    }

    sealed class Pose
    {
        public Transform[] bones;
        public Quaternion[] rotations;
        public Vector3[] positions;
        public Pose(Component root)
        {
            bones = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .SelectMany(s => s.bones).Where(b => b).Distinct().ToArray();
            rotations = bones.Select(b => b.localRotation).ToArray();
            positions = bones.Select(b => b.localPosition).ToArray();
        }
        public float MaxAngle()
        {
            float value = 0;
            for (int i = 0; i < bones.Length; i++) if (bones[i])
                value = Mathf.Max(value, Quaternion.Angle(rotations[i], bones[i].localRotation));
            return value;
        }
        public float MaxTranslation()
        {
            float value = 0;
            for (int i = 0; i < bones.Length; i++) if (bones[i])
                value = Mathf.Max(value, Vector3.Distance(positions[i], bones[i].localPosition));
            return value;
        }
    }

    static float ClipWeight(Animator animator, string name)
    {
        float value = 0;
        foreach (AnimatorClipInfo info in animator.GetCurrentAnimatorClipInfo(0))
            if (info.clip && info.clip.name == name) value += info.weight;
        if (animator.IsInTransition(0))
            foreach (AnimatorClipInfo info in animator.GetNextAnimatorClipInfo(0))
                if (info.clip && info.clip.name == name) value += info.weight;
        return value;
    }

    static HabitatShrimpVisual Shrimp(HabitatGame game)
    {
        var dictionary = (System.Collections.IDictionary)typeof(HabitatView).GetField("actors", Private).GetValue(game.View);
        object actor = dictionary[ShrimpId];
        if (actor == null) throw new InvalidOperationException("Fixture shrimp has no rendered actor.");
        return (HabitatShrimpVisual)actor.GetType().GetField("shrimp").GetValue(actor);
    }

    static HabitatTrapVisual Trap(HabitatGame game)
    {
        var tiles = (Array)typeof(HabitatView).GetField("tiles", Private).GetValue(game.View);
        object tile = tiles.GetValue(TrapX + TrapZ * game.State.width);
        return (HabitatTrapVisual)tile.GetType().GetField("trap").GetValue(tile);
    }

    static Transform Bone(Component root, string name)
    {
        Transform found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        if (!found) throw new InvalidOperationException("Missing expected bone " + name);
        return found;
    }

    static HabitatState Fixture()
    {
        var s = new HabitatState {
            width = 7, depth = 6, levelIndex = HabitatFigJamLevel.Index, authoredLevel = true,
            levelName = "Temporary model animation fixture", objective = "Animation checks; original state is restored afterwards.",
            playerX = 3, playerZ = 1, facingX = 0, facingZ = 1, waterLevel = 0,
            selectedX = TrapX, selectedZ = TrapZ, traps = 1, coins = 0, cargo = 0,
            dockX = 1, dockZ = 0, carryingAnimal = -1, goals = new Species[0]
        };
        s.tiles = Enumerable.Range(0, s.width * s.depth).Select(_ => new Tile { height = 1 }).ToArray();
        // The real movement rule has exactly one legal exit: east into the placed trap.
        HabitatRules.TileAt(s, 1, 2).prop = PropKind.Obstacle;
        HabitatRules.TileAt(s, 2, 1).prop = PropKind.Obstacle;
        HabitatRules.TileAt(s, 2, 3).prop = PropKind.Obstacle;
        s.animals.Add(new Animal { id = ShrimpId, species = Species.Shrimp, x = 2, z = 2 });
        return s;
    }

    static void Capture(string name)
    {
        ScreenCapture.CaptureScreenshot(Path.Combine(Folder, name + ".png"));
        report.AppendLine("SCREENSHOT requested: " + name + ".png");
    }

    public static async Task<string> Run()
    {
        if (running) return "FAIL: ModelsAnimationChecks is already running.";
        running = true; count = 0; report.Clear(); Directory.CreateDirectory(Folder);
        report.AppendLine("Actual-frame shrimp/trap integration checks");
        report.AppendLine("UTC: " + DateTimeOffset.UtcNow.ToString("O"));
        HabitatGame game = null;
        HabitatState savedState = null;
        HabitatState[] savedHistory = null;
        Stack<HabitatState> history = null;
        Tuning savedTuning = null;
        int savedActions = 0, savedTool = 0;
        float savedSpeed = 1, savedNextMove = 0, savedTimeScale = Time.timeScale;
        bool savedPause = true, savedOverlays = true;
        string savedExport = null;
        string originalStateJson = null, originalTuningJson = null;
        string[] originalHistoryJson = null;
        var cameraValues = new Dictionary<string, object>();
        Vector3 cameraPosition = Vector3.zero;
        Quaternion cameraRotation = Quaternion.identity;
        float cameraSize = 0;
        var sourceHashes = new Dictionary<string, string>();
        Keyboard originalKeyboard = Keyboard.current, keyboard = null;
        var background = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        Exception failure = null;
        try
        {
            Check(Application.isPlaying && HabitatGame.Instance, "main island is in Play mode");
            game = HabitatGame.Instance;
            Check(game.State.levelIndex == HabitatFigJamLevel.Index && game.State.authoredLevel, "checks start from the formal island");
            Check(!game.Hud.ModalOpen && !game.Fishing.IsOpen, "no user modal/fishing session would be interrupted");
            savedState = HabitatRules.Clone(game.State);
            originalStateJson = JsonUtility.ToJson(savedState);
            savedTuning = game.tuning;
            originalTuningJson = JsonUtility.ToJson(savedTuning);
            savedPause = game.paused; savedOverlays = game.overlays; savedSpeed = game.speed;
            savedActions = game.ActionCount; savedTool = game.tool; savedExport = game.LastExport;
            savedNextMove = (float)typeof(HabitatGame).GetField("nextMove", Private).GetValue(game);
            history = (Stack<HabitatState>)typeof(HabitatGame).GetField("history", Private).GetValue(game);
            savedHistory = history.Select(HabitatRules.Clone).ToArray();
            originalHistoryJson = savedHistory.Select(s => JsonUtility.ToJson(s)).ToArray();
            foreach (string field in CameraFields)
                cameraValues[field] = typeof(HabitatView).GetField(field, Private).GetValue(game.View);
            cameraPosition = game.View.GameCamera.transform.position;
            cameraRotation = game.View.GameCamera.transform.rotation;
            cameraSize = game.View.GameCamera.orthographicSize;
            foreach (string path in Sources)
            {
                Check(File.Exists(path), "source GLB exists: " + path);
                sourceHashes[path] = Hash(path);
                report.AppendLine("SOURCE SHA256: " + sourceHashes[path] + " " + path);
            }
            Check(sourceHashes[Sources[1]] == "d7faa313a751a84b5c568461e554ec08aed7c907cc25e236567b54c00dd495ac",
                "trap GLB bytes still match the delivered V2 model");
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("Models acceptance keyboard");
            keyboard.MakeCurrent();
            game.paused = true; game.overlays = false; game.speed = 1; game.tool = 0; history.Clear();
            game.tuning = new Tuning { moveSeconds = .5f, breedSeconds = 1000f, animalMovement = true };
            game.State = Fixture(); game.View.Initialize(game.State); game.View.FocusPlayer();
            game.View.RotateCamera(180f); game.View.Zoom(-2f); // Inspect the cage door from its visible front.
            await Frames(4);
            HabitatShrimpVisual shrimp = Shrimp(game);
            Check(shrimp && shrimp.IsReady && shrimp.Animator && !shrimp.Animator.applyRootMotion, "new shrimp rig is active and root motion is disabled");
            var idlePose = new Pose(shrimp);
            Check(idlePose.bones.Length == 45, "actual shrimp renderer uses all 45 supplied bones");
            Vector3 rigOrigin = shrimp.Animator.transform.localPosition;
            float idleTime = shrimp.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            float idleAngle = 0, idleTranslation = 0;
            await Observe(.75f, () => { idleAngle = Mathf.Max(idleAngle, idlePose.MaxAngle()); idleTranslation = Mathf.Max(idleTranslation, idlePose.MaxTranslation()); });
            Check(shrimp.CurrentState == "Idle" && ClipWeight(shrimp.Animator, "Idle") > .9f, "stationary paused fixture plays actual Idle clip");
            Check(shrimp.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime > idleTime && (idleAngle > .01f || idleTranslation > .00001f),
                "Idle advances over real frames and changes bone pose (degrees " + idleAngle.ToString("F4") + ")");
            Check(Vector3.Distance(rigOrigin, shrimp.Animator.transform.localPosition) < .0001f, "Idle does not translate the imported animation root");
            Capture("shrimp-idle");

            game.tool = 3; game.Select(TrapX, TrapZ);
            int actions = game.ActionCount, undos = game.UndoCount;
            game.ApplyTool();
            Check(HabitatRules.TileAt(game.State, TrapX, TrapZ).trap && game.State.traps == 0 &&
                game.ActionCount == actions + 1 && game.UndoCount == undos + 1, "normal placement tool consumes one cage and records one undo action");
            await Frames(3);
            HabitatTrapVisual trap = Trap(game);
            Check(trap && trap.IsConfigured && !trap.Animator.applyRootMotion, "placed cage uses the supplied animated prefab without root motion");
            Check(trap.CurrentState == HabitatTrapVisualState.Ready && !trap.IsTransitioning, "new empty trap binds directly to Ready");
            Transform hinge = Bone(trap, "door_hinge");
            Quaternion openDoor = hinge.localRotation;
            Vector3 trapOrigin = trap.transform.position;
            var trapInstance = trap.GetEntityId();
            Capture("trap-ready");

            Pose movePose = null;
            HabitatRules.Tick(game.State, game.tuning, .55f);
            Animal resident = game.State.animals.Single(a => a.id == ShrimpId);
            Check(resident.x == TrapX && resident.z == TrapZ && !resident.carried, "real rules advance the shrimp through its only legal exit into the trap");
            bool sawClosing = false, sawMove = false;
            float moveWeight = 0, moveAngle = 0, closeAngle = 0;
            await Observe(2.15f, () => {
                sawClosing |= trap.CurrentState == HabitatTrapVisualState.Closing;
                sawMove |= shrimp.CurrentState == "Move";
                moveWeight = Mathf.Max(moveWeight, ClipWeight(shrimp.Animator, "Move"));
                // Start measuring after the blend has mostly completed: a transition
                // between two static poses must not be mistaken for a moving clip.
                if (ClipWeight(shrimp.Animator, "Move") > .9f)
                {
                    if (movePose == null) movePose = new Pose(shrimp);
                    else moveAngle = Mathf.Max(moveAngle, movePose.MaxAngle());
                }
                closeAngle = Mathf.Max(closeAngle, Quaternion.Angle(openDoor, hinge.localRotation));
            });
            Check(sawMove && moveWeight > .1f && moveAngle > .1f, "real grid displacement plays Move with moving bones (weight " + moveWeight.ToString("F3") + ", degrees " + moveAngle.ToString("F3") + ")");
            Check(sawClosing && trap.CurrentState == HabitatTrapVisualState.Occupied && closeAngle > 60f, "capture closes the physical hinge from its open pose before settling Occupied");
            Check(trap.GetEntityId() == trapInstance && Vector3.Distance(trapOrigin, trap.transform.position) < .0001f, "capture updates the existing cage without rebuilding or moving its root");
            Check(shrimp.CurrentState == "Idle" && ClipWeight(shrimp.Animator, "Idle") > .8f, "settled captured shrimp returns to Idle");
            Check(Mathf.Abs(shrimp.transform.parent.localScale.x - .48f) < .001f, "captured shrimp uses the cage display scale");
            Quaternion closedDoor = hinge.localRotation;
            Transform frontFloat = Bone(trap, "float_front_swing"), sideFloat = Bone(trap, "float_side_swing");
            Quaternion frontBefore = frontFloat.localRotation, sideBefore = sideFloat.localRotation;
            float floatAngle = 0, floatTime = trap.AnimationTime;
            await Observe(.8f, () => floatAngle = Mathf.Max(floatAngle, Quaternion.Angle(frontBefore, frontFloat.localRotation), Quaternion.Angle(sideBefore, sideFloat.localRotation)));
            Check(trap.CurrentClipName == "Float_Sway" && trap.IsAnimating && Mathf.Abs(trap.AnimationTime - floatTime) > .1f && floatAngle > .05f,
                "occupied cage advances Float_Sway and changes a real float bone (degrees " + floatAngle.ToString("F3") + ")");
            HabitatRules.Tick(game.State, game.tuning, 1.1f);
            Check(resident.x == TrapX && resident.z == TrapZ, "captured shrimp remains on the trap under further rule ticks");
            Capture("trap-occupied");

            game.tool = 0; game.Select(TrapX, TrapZ);
            actions = game.ActionCount; undos = game.UndoCount;
            await Press(keyboard, Key.E);
            Check(resident.carried && game.State.carryingAnimal == ShrimpId && game.ActionCount == actions + 1 && game.UndoCount == undos + 1,
                "real E input carries the captured shrimp and records one action");
            Pose carryPose = null;
            float carryWeight = 0, carryAngle = 0, reopenAngle = 0;
            bool sawOpening = trap.CurrentState == HabitatTrapVisualState.Opening;
            await Observe(1.9f, () => {
                sawOpening |= trap.CurrentState == HabitatTrapVisualState.Opening;
                carryWeight = Mathf.Max(carryWeight, ClipWeight(shrimp.Animator, "Carried"));
                if (ClipWeight(shrimp.Animator, "Carried") > .95f)
                {
                    if (carryPose == null) carryPose = new Pose(shrimp);
                    else carryAngle = Mathf.Max(carryAngle, carryPose.MaxAngle());
                }
                reopenAngle = Mathf.Max(reopenAngle, Quaternion.Angle(closedDoor, hinge.localRotation));
            });
            Check(shrimp.CurrentState == "Carried" && carryWeight > .9f && carryAngle > .1f, "Carried has real clip weight and animated bones while held");
            Check(sawOpening && trap.CurrentState == HabitatTrapVisualState.Ready && !trap.IsTransitioning && reopenAngle > 60f,
                "taking the shrimp out opens the physical door once and restores Ready");
            Check(!trap.HasCapturedShrimp && Math.Abs(shrimp.transform.parent.localScale.x - HabitatView.ShrimpCarryScale / HabitatView.ShrimpGroundScale) < .001f,
                "carried shrimp leaves cage occupancy and returns to carry display scale");
            Capture("shrimp-carried-trap-open");

            game.Select(4, 1);
            await Press(keyboard, Key.E);
            Check(!resident.carried && game.State.carryingAnimal == -1 && resident.x == 4 && resident.z == 1, "E places the shrimp on a legal adjacent ground tile");
            await Observe(.9f);
            Check(shrimp.CurrentState == "Idle" && ClipWeight(shrimp.Animator, "Idle") > .8f && Mathf.Abs(shrimp.transform.parent.localScale.x - 1f) < .001f,
                "landing restores Idle and ordinary ground scale");
            Check(!shrimp.Animator.applyRootMotion && Vector3.Distance(rigOrigin, shrimp.Animator.transform.localPosition) < .0001f, "carry/landing never enables root motion or drifts the imported rig origin");

            game.Undo(); await Frames(3);
            Check(game.State.carryingAnimal == ShrimpId && Shrimp(game).CurrentState == "Carried" && Trap(game).CurrentState == HabitatTrapVisualState.Ready,
                "undo landing restores carried visual and ready cage without a capture event");
            game.Undo(); await Frames(3);
            trap = Trap(game);
            Check(game.State.carryingAnimal == -1 && trap.CurrentState == HabitatTrapVisualState.Occupied && !trap.IsTransitioning,
                "undo pickup restores occupied cage immediately without replaying Closing");

            trapInstance = trap.GetEntityId();
            game.State.carryingProp = PropKind.Climb; // Test fixture inventory, never copied back to the original level.
            Check(HabitatRules.PlaceSoil(game.State, 4, 2), "legal adjacent soil placement changes terrain and requests a cage tile rebuild");
            await Frames(3);
            trap = Trap(game);
            Check(trap.GetEntityId() != trapInstance && trap.CurrentState == HabitatTrapVisualState.Occupied && !trap.IsTransitioning,
                "terrain rebuild creates a new occupied cage already aligned to its current state");

            trapInstance = trap.GetEntityId();
            HabitatState replacement = HabitatRules.Clone(game.State);
            Check(HabitatRules.Interact(replacement, TrapX, TrapZ), "replacement snapshot is created using the normal pickup rule");
            game.State = replacement;
            await Frames(3);
            trap = Trap(game);
            Check(trap.GetEntityId() == trapInstance && trap.CurrentState == HabitatTrapVisualState.Ready && !trap.IsTransitioning,
                "new State instance resets unchanged cage geometry directly to Ready instead of false Opening");
            game.View.Initialize(game.State); await Frames(3);
            Check(Trap(game).CurrentState == HabitatTrapVisualState.Ready && !Trap(game).IsTransitioning && Shrimp(game).CurrentState == "Carried",
                "view initialization restores current carry/cage poses without replayed events");
            foreach (string path in Sources) Check(Hash(path) == sourceHashes[path], "source asset unchanged after runtime checks: " + path);
            Check(Mathf.Approximately(Time.timeScale, savedTimeScale), "animation integration does not modify global Time.timeScale");
        }
        catch (Exception e) { failure = e; report.AppendLine(e.ToString()); }
        finally
        {
            try
            {
                if (keyboard != null && keyboard.added)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.RemoveDevice(keyboard);
                }
                if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
                if (game && savedState != null && Application.isPlaying)
                {
                    game.paused = true;
                    game.State = savedState; game.tuning = savedTuning;
                    game.speed = savedSpeed; game.tool = savedTool; game.overlays = savedOverlays;
                    game.ActionCount = savedActions; game.LastExport = savedExport;
                    history.Clear();
                    for (int i = savedHistory.Length - 1; i >= 0; i--) history.Push(savedHistory[i]);
                    typeof(HabitatGame).GetField("nextMove", Private).SetValue(game, savedNextMove);
                    game.View.Initialize(game.State);
                    foreach (var pair in cameraValues) typeof(HabitatView).GetField(pair.Key, Private).SetValue(game.View, pair.Value);
                    game.View.Render(game.State, game.tuning, game.overlays); game.Hud.Refresh();
                    game.View.GameCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                    game.View.GameCamera.orthographicSize = cameraSize;
                    game.paused = savedPause;
                    Check(game.State.authoredLevel && game.State.levelIndex == HabitatFigJamLevel.Index &&
                        game.ActionCount == savedActions && game.UndoCount == savedHistory.Length &&
                        JsonUtility.ToJson(game.State) == originalStateJson && JsonUtility.ToJson(game.tuning) == originalTuningJson &&
                        history.Select(s => JsonUtility.ToJson(s)).SequenceEqual(originalHistoryJson),
                        "cleanup restores exact formal state/selection/tuning, action count and every undo snapshot in order");
                }
            }
            catch (Exception cleanup) { report.AppendLine("CLEANUP FAIL: " + cleanup); if (failure == null) failure = cleanup; }
            report.AppendLine(failure == null ? "PASS: " + count + " actual-frame animation assertions" : "FAIL: " + failure.Message);
            File.WriteAllText(Path.Combine(Folder, "animation-checks.txt"), report.ToString());
            running = false;
        }
        return failure == null ? "PASS: " + count + " actual-frame animation assertions; original state restored" : "FAIL: " + failure.Message;
    }
}
