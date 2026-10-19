using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

// Execute serially through the Unity C# API in Play, after the natural opening.
// All observations use real Update/LateUpdate frames. Never writes saves/assets.
public static class EquipmentGameplayChecks
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const float PrimaryTolerance = .02f, SecondaryTolerance = .02f;
    static readonly StringBuilder report = new StringBuilder();
    static string folder;
    static int assertions;
    static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Fields).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Fields).SetValue(o, value);
    static string Json(object o) => JsonUtility.ToJson(o);
    static Dictionary<FieldInfo, object> Scalars(object o) => o.GetType().GetFields(Fields)
        .Where(f => !f.IsInitOnly && !f.IsStatic && (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string)))
        .ToDictionary(f => f, f => f.GetValue(o));
    static void RestoreScalars(object o, Dictionary<FieldInfo, object> values)
    { foreach (var item in values) item.Key.SetValue(o, item.Value); }
    static void Require(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
    static void Check(bool value, string label)
    {
        report.AppendLine((value ? "PASS " : "FAIL ") + (++assertions) + ": " + label);
        Require(value, label);
    }
    static async Task Frames(int count)
    {
        int start = Time.frameCount;
        double deadline = UnityEditor.EditorApplication.timeSinceStartup + 10;
        while (Time.frameCount - start < count)
        {
            Require(Application.isPlaying && UnityEditor.EditorApplication.timeSinceStartup < deadline,
                "Unity did not advance genuine Play frames.");
            await Task.Delay(12);
        }
    }
    static async Task Seconds(float seconds)
    { float start = Time.unscaledTime; while (Time.unscaledTime - start < seconds) await Frames(1); }
    static async Task Press(Keyboard keyboard, Key key)
    {
        int start = Time.frameCount;
        report.AppendLine("INPUT before " + key + ": keyboard=" + keyboard.deviceId + ", current="
            + (Keyboard.current == null ? "null" : Keyboard.current.deviceId.ToString()) + ", enabled=" + keyboard.enabled
            + ", added=" + keyboard.added + ", focused=" + Application.isFocused + ", updateMode=" + InputSystem.settings.updateMode);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); await Frames(2);
        report.AppendLine("INPUT down " + key + ": frames=" + (Time.frameCount - start) + ", pressed=" + keyboard[key].isPressed
            + ", current=" + (Keyboard.current == null ? "null" : Keyboard.current.deviceId.ToString())
            + ", game.tool=" + HabitatGame.Instance.tool);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
        report.AppendLine("INPUT released " + key + ": frames=" + (Time.frameCount - start) + ", pressed=" + keyboard[key].isPressed);
    }
    static string EquipmentDiagnostic(HabitatGame game)
    {
        var visual = game.View.PlayerVisual;
        var shovel = Resources.Load<GameObject>("Models/Shovel/Shovel");
        var rightHand = Get<Transform>(visual, "rightHand");
        return "game.tool=" + game.tool + ", EquippedTool=" + visual.EquippedTool + ", IsModelActive=" + visual.IsModelActive
            + ", Motion=" + visual.Motion + ", equipmentBlocked=" + Get<bool>(visual, "equipmentBlocked")
            + ", rightHand=" + (rightHand ? rightHand.name : "null") + ", Resources.LoadShovel=" + (shovel ? shovel.name : "null")
            + ", triedShovel=" + Get<bool>(visual, "triedShovel") + ", shovelInstanceExists=" + (Get<object>(visual, "shovelEquipment") != null)
            + ", equipmentTool=" + Get<int>(visual, "equipmentTool") + ", EquipmentRoot=" + (visual.EquipmentRoot ? visual.EquipmentRoot.name : "null")
            + ", EquipmentAction=" + visual.EquipmentAction + ", falling=" + visual.IsFalling + ", landing=" + visual.IsLanding
            + ", cinematic=" + game.View.IsCinematic + ", story=" + game.StoryPlaying + ", recovering=" + game.IsRecovering
            + ", Busy=" + game.Busy + ", ModalOpen=" + game.Hud.ModalOpen + ", FishingOpen=" + game.Fishing.IsOpen
            + ", gameEnabled=" + game.isActiveAndEnabled + ", visualEnabled=" + visual.isActiveAndEnabled
            + ", onHarbor=" + game.State.onHarbor + ", carryingProp=" + game.State.carryingProp + ", carryingAnimal=" + game.State.carryingAnimal
            + ", player=" + game.State.playerX + "," + game.State.playerZ + ", frame=" + Time.frameCount;
    }
    static async Task Shot(string name)
    {
        string path = Path.Combine(folder, name + ".png");
        ScreenCapture.CaptureScreenshot(path); await Frames(3);
        report.AppendLine("SCREENSHOT " + path);
    }
    // The callback captures values after this camera rendered, rather than
    // returning live Transforms that the next Update could change before await resumes.
    static async Task<T> AfterCamera<T>(Camera camera, Func<T> sample)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<ScriptableRenderContext, Camera> handler = null;
        handler = (context, rendered) =>
        {
            if (rendered != camera || completion.Task.IsCompleted) return;
            try { completion.TrySetResult(sample()); }
            catch (Exception exception) { completion.TrySetException(exception); }
        };
        RenderPipelineManager.endCameraRendering += handler;
        try
        {
            Task finished = await Task.WhenAny(completion.Task, Task.Delay(8000));
            Require(finished == completion.Task, "Main camera did not reach endCameraRendering within eight seconds.");
            return await completion.Task;
        }
        finally { RenderPipelineManager.endCameraRendering -= handler; }
    }
    sealed class LineSample
    {
        public int frame;
        public bool enabled;
        public Vector3 start, tip, end, expected;
        public float startError, endError, clipTime;
        public override string ToString() => "frame=" + frame + ", lineEnabled=" + enabled
            + ", start=" + start.ToString("F5") + ", tip=" + tip.ToString("F5") + ", startError=" + startError.ToString("F6")
            + ", end=" + end.ToString("F5") + ", expected=" + expected.ToString("F5") + ", endError=" + endError.ToString("F6")
            + ", clipTime=" + clipTime.ToString("F5");
    }
    static LineSample ReadLine(HabitatPlayerVisual visual, Transform tip, Vector3 expected)
    {
        var line = visual.FishingLine;
        var result = new LineSample { frame = Time.frameCount, enabled = line && line.enabled && line.positionCount >= 2,
            tip = tip.position, expected = expected, clipTime = visual.EquipmentAnimation.TimeSeconds };
        if (line && line.positionCount >= 2)
        { result.start = line.GetPosition(0); result.end = line.GetPosition(line.positionCount - 1); }
        result.startError = Vector3.Distance(result.start, result.tip); result.endError = Vector3.Distance(result.end, result.expected);
        return result;
    }
    static async Task FrontShot(HabitatGame game, string name)
    {
        Camera camera = game.View.GameCamera;
        Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
        float size = camera.orthographicSize; bool enabled = game.enabled;
        try
        {
            // Freeze only Game.Update for this diagnostic photo, after all gameplay
            // measurements. Player LateUpdate and rendering still run. Never an assertion source.
            game.enabled = false;
            Transform facing = Get<Transform>(game.View.PlayerVisual, "facingRoot");
            Vector3 forward = facing ? facing.forward : Vector3.forward; forward.y = 0f; forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 target = game.View.PlayerWorldPosition + Vector3.up * .65f + forward * .15f;
            camera.transform.position = target + forward * 3.2f + Vector3.up * 1.3f + right * .65f;
            camera.transform.LookAt(target); camera.orthographicSize = 1.35f;
            await Shot(name);
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position, rotation); camera.orthographicSize = size; game.enabled = enabled;
        }
    }
    static Transform Bone(Transform root, string name)
        => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    static bool LineHidden(HabitatPlayerVisual visual) => !visual.FishingLine || !visual.FishingLine.enabled;
    static HashSet<Transform> BackpackObjects() => new HashSet<Transform>(UnityEngine.Object.FindObjectsByType<Transform>(
        FindObjectsInactive.Include).Where(t => t.gameObject.scene.IsValid()
        && (t.name.Contains("背篓") || t.name.IndexOf("backpack", StringComparison.OrdinalIgnoreCase) >= 0
        || t.name.IndexOf("back_basket", StringComparison.OrdinalIgnoreCase) >= 0)));
    static Tile Q(HabitatState state, int x, int z) => HabitatRules.TileAt(state, x, z);
    static HabitatState Fixture(HabitatState source, bool fans = false)
    {
        var state = HabitatRules.Clone(source);
        state.width = state.depth = 9; state.tiles = new Tile[81];
        for (int i = 0; i < state.tiles.Length; i++) state.tiles[i] = new Tile { height = 1 };
        state.animals.Clear(); state.events.Clear(); state.earnedSpecies.Clear(); state.goals = Array.Empty<Species>();
        state.playerX = state.playerZ = 4; state.facingX = 0; state.facingZ = 1;
        state.selectedX = 4; state.selectedZ = 6; state.dockX = state.dockZ = 1; state.dockRotation = -1;
        state.onHarbor = false; state.carryingAnimal = -1; state.carryingProp = PropKind.None;
        state.waterLevel = state.tideBaseLevel = state.tickCount = 0; state.tideActive = false;
        state.movementClock = state.elapsed = 0f; state.rod = true; state.fish = state.shrimp = 0;
        state.openingCompleted = state.openingCatchGranted = true; state.won = false;
        state.levelName = "装备实帧检查夹具"; state.objective = "临时检查，绝不保存";
        if (fans)
        {
            int[] x = { 4, 7, 4, 1 }, z = { 7, 4, 1, 4 };
            for (int i = 0; i < 4; i++) { Q(state, x[i], z[i]).prop = PropKind.Fan; Q(state, x[i], z[i]).rotation = i; }
        }
        else
        {
            Q(state, 4, 5).height = Q(state, 4, 6).height = 0;
            state.animals.Add(new Animal { id = 81001, x = 4, z = 6, homeX = 4, homeZ = 6, species = Species.Goldfish, offspring = 2 });
        }
        return state;
    }
    static async Task Install(HabitatGame game, HabitatState fixture, int tool = 0)
    {
        if (game.Fishing.IsOpen) game.Fishing.Cancel();
        game.CancelPendingAction(); game.Hud.ClosePanels(); game.paused = true; game.speed = 1f;
        game.State = fixture; game.tool = tool; game.ActionCount = 0;
        Get<Stack<HabitatState>>(game, "history").Clear();
        game.View.Initialize(fixture); game.View.FocusPlayer();
        Set(game.View, "cameraSize", 2.8f);
        await Frames(4);
        Require(!game.Busy && ReferenceEquals(game.State, fixture), "Fixture did not remain isolated and idle.");
    }
    static async Task<float> ObserveGrip(HabitatPlayerVisual visual, float seconds, string label, bool secondary)
    {
        Require(visual.EquipmentRoot && visual.PrimaryEquipmentGrip, label + ": no equipped grip.");
        if (secondary) Require(visual.SecondaryEquipmentGrip, label + ": shovel has no secondary grip.");
        float primaryMax = 0f, secondaryMax = 0f, angleMax = 0f, start = Time.unscaledTime;
        Quaternion initialRotation = visual.EquipmentSocket.rotation;
        bool photographedDig = false;
        int samples = 0;
        do
        {
            Require(visual.EquipmentRoot && visual.PrimaryEquipmentGrip, label + ": equipment vanished during measurement.");
            Vector3 rendered = await AfterCamera(HabitatGame.Instance.View.GameCamera, () => new Vector3(
                Vector3.Distance(visual.PrimaryEquipmentGrip.position, visual.RightPalmPosition),
                secondary ? Vector3.Distance(visual.SecondaryEquipmentGrip.position, visual.LeftPalmPosition) : 0f,
                Quaternion.Angle(initialRotation, visual.EquipmentSocket.rotation)));
            float primary = rendered.x, second = rendered.y;
            angleMax = Mathf.Max(angleMax, rendered.z);
            Require(float.IsFinite(primary) && float.IsFinite(second), label + ": nonfinite grip position.");
            primaryMax = Mathf.Max(primaryMax, primary); secondaryMax = Mathf.Max(secondaryMax, second); samples++;
            if (label == "shovel Dig" && !photographedDig && Time.unscaledTime - start >= .25f)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "shovel-dig.png"));
                photographedDig = true;
            }
        } while (Time.unscaledTime - start < seconds);
        report.AppendLine("MEASURE " + label + ": samples=" + samples + ", primaryMax=" + primaryMax.ToString("F5")
            + "m, secondaryMax=" + secondaryMax.ToString("F5") + "m; limits=" + PrimaryTolerance + "/" + SecondaryTolerance + "m");
        Check(samples >= 3 && primaryMax <= PrimaryTolerance && (!secondary || secondaryMax <= SecondaryTolerance),
            label + ": actual animated palms remain on the delivered grip nodes");
        return angleMax;
    }
    static HabitatEquipmentAnimation[] Fans(HabitatGame game)
    {
        var visuals = Get<Array>(game.View, "tiles"); var result = new List<HabitatEquipmentAnimation>();
        for (int i = 0; i < game.State.tiles.Length; i++)
        {
            if (game.State.tiles[i].prop != PropKind.Fan) continue;
            var visual = visuals.GetValue(i);
            var animation = Get<HabitatEquipmentAnimation>(visual, "bellows");
            Require(animation && animation.IsConfigured, "Fan at index " + i + " is still missing the imported bellows.");
            var nozzle = Bone(animation.transform, "root_nozzle");
            var upper = Bone(animation.transform, "shell_upper"); var lower = Bone(animation.transform, "shell_lower");
            Require(nozzle && upper && lower && animation.Animator, "Imported bellows hierarchy is incomplete.");
            Vector3 forward = (nozzle.position - animation.Animator.transform.position).normalized;
            Vector3 up = (upper.position - lower.position).normalized;
            int rotation = ((game.State.tiles[i].rotation % 4) + 4) % 4;
            Vector3 expected = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left }[rotation];
            Check(Vector3.Dot(forward, expected) > .98f && Vector3.Dot(up, Vector3.up) > .98f,
                "Fan " + i + " nozzle agrees with logical rotation " + rotation + " and shell remains upright");
            Require(!Get<Transform>(visual, "prop").GetComponentsInChildren<Transform>(true).Any(t => t.name == "Spinning rotor"),
                "Legacy fan rotor overlaps the imported bellows.");
            result.Add(animation);
        }
        return result.ToArray();
    }

    public static async Task<string> Run()
    {
        assertions = 0; report.Clear(); folder = Path.GetFullPath("Logs/equipment-20261016"); Directory.CreateDirectory(folder);
        var game = HabitatGame.Instance;
        Require(Application.isPlaying && game && game.State != null && game.View && game.View.PlayerVisual,
            "Run during the actual main-map Play session.");
        Require(game.State.openingCompleted && !game.Busy && !game.Hud.ModalOpen && !game.Fishing.IsOpen
            && game.Story && game.View.Harbor.Ready, "Finish opening and close panels before this serial check.");
        HabitatState original = game.State; string originalJson = Json(original); Tuning tuning = game.tuning;
        var history = Get<Stack<HabitatState>>(game, "history"); var savedHistory = history.ToArray();
        var visits = Get<List<HabitatGame.VisitedLevel>>(game, "visitedLevels"); var savedVisits = visits.ToArray();
        string historyJson = string.Join("|", savedHistory.Select(Json));
        string visitsJson = string.Join("|", savedVisits.Select(Json));
        var gameScalars = Scalars(game); var hudScalars = Scalars(game.Hud); var fishingScalars = Scalars(game.Fishing);
        var uiActive = game.Hud.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.gameObject, t => t.gameObject.activeSelf);
        string[] cameraNames = { "cameraYaw", "cameraSize", "cameraTarget", "cameraTargetSmooth", "followPlayer", "overviewCamera", "overviewSize", "framingAspect", "framingDirty", "nextVisibilityCheck" };
        var cameraFields = cameraNames.ToDictionary(n => n, n => typeof(HabitatView).GetField(n, Fields).GetValue(game.View));
        var camera = game.View.GameCamera; Vector3 cameraPosition = camera.transform.position; Quaternion cameraRotation = camera.transform.rotation;
        float cameraSize = camera.orthographicSize; Color cameraColor = camera.backgroundColor; var clearFlags = camera.clearFlags;
        bool cameraOrthographic = camera.orthographic;
        GameObject selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        Keyboard oldKeyboard = Keyboard.current, keyboard = null; Mouse oldMouse = Mouse.current, mouse = null;
        var background = InputSystem.settings.backgroundBehavior; var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        var originalBackpacks = BackpackObjects(); Exception failure = null;
        try
        {
            keyboard = InputSystem.AddDevice<Keyboard>("Equipment acceptance keyboard"); keyboard.MakeCurrent();
            mouse = InputSystem.AddDevice<Mouse>("Equipment acceptance mouse"); mouse.MakeCurrent();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.EnableDevice(keyboard); InputSystem.EnableDevice(mouse);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            visits.Clear(); game.tuning = JsonUtility.FromJson<Tuning>(Json(tuning));
            game.tuning.animalMovement = false; game.tuning.breedSeconds = 9999f;
            await Install(game, Fixture(original));
            var visual = game.View.PlayerVisual;
            Check(visual.IsModelActive && visual.EquippedTool == "None" && LineHidden(visual), "empty-hand tool starts without a rod, shovel or fishing line");
            await Press(keyboard, Key.Digit2);
            string shovelDiagnostic = EquipmentDiagnostic(game);
            report.AppendLine("DIAGNOSTIC after tool-1: " + shovelDiagnostic);
            Check(game.tool == 1 && visual.EquippedTool == "Shovel", "real tool-1 hotkey equips the delivered shovel [" + shovelDiagnostic + "]");
            await ObserveGrip(visual, .25f, "shovel idle", true); await Shot("shovel-held");
            await FrontShot(game, "shovel-held-front");
            await Press(keyboard, Key.Digit3);
            Check(game.tool == 2 && visual.EquippedTool == "Rod" && visual.EquipmentAnimation.CurrentClipName == "Idle_Sway", "real tool-2 hotkey equips the rod and idle clip");
            await ObserveGrip(visual, .25f, "rod idle", false);
            await Press(keyboard, Key.Digit5);
            Check(game.tool == 4 && visual.EquippedTool == "Rod", "real tool-4 hotkey retains the rod for offspring harvest");
            game.State.rod = false; await Frames(2);
            Check(visual.EquippedTool == "None", "rod ownership is required for the visible rod");
            game.State.rod = true; await Press(keyboard, Key.Digit3);

            Animal fish = game.State.animals[0]; int fishX = fish.x, fishZ = fish.z;
            game.ApplyTool();
            Check(fish.x == 4 && fish.z == 5 && game.UndoCount == 1 && game.ActionCount == 1
                && visual.EquipmentAction == "Cast", "successful Lure moves the actual fish and starts Cast exactly once");
            await Frames(2);
            Require(visual.EquipmentAnimation.CurrentClipName == "Cast" && !visual.EquipmentAnimation.IsLooping, "Cast did not select a nonlooping imported clip.");
            Transform tip = Bone(visual.EquipmentRoot, "Rod_Tip_Attachment"); Quaternion castStart = tip.rotation;
            await ObserveGrip(visual, .42f, "rod Cast", false);
            Check(Quaternion.Angle(castStart, tip.rotation) > .1f && visual.EquipmentAnimation.TimeSeconds > .1f,
                "Cast advances and changes the actual rod-tip pose on real frames");
            Vector3 expectedEnd = new Vector3(4, .1f, 5);
            report.AppendLine("LINE immediate Task phase: " + ReadLine(visual, tip, expectedEnd));
            LineSample lineSample = await AfterCamera(game.View.GameCamera, () => ReadLine(visual, tip, expectedEnd));
            string lineDiagnostic = lineSample + "; keyboard=" + keyboard.deviceId + ", enabled=" + keyboard.enabled
                + ", current=" + (Keyboard.current == null ? "null" : Keyboard.current.deviceId.ToString())
                + ", mouseEnabled=" + mouse.enabled + ", focused=" + Application.isFocused;
            report.AppendLine("LINE after endCameraRendering: " + lineDiagnostic);
            await Shot("rod-cast-diagnostic"); await FrontShot(game, "rod-cast-front");
            Check(lineSample.enabled && lineSample.startError < .025f && lineSample.endError < .025f,
                "rendered fishing line connects the animated tip to the fish's new Lure location [" + lineDiagnostic + "]");
            await Shot("rod-cast"); game.Undo(); await Frames(3);
            Check(game.State.animals[0].x == fishX && game.State.animals[0].z == fishZ && game.UndoCount == 0
                && visual.EquipmentAction == "" && LineHidden(visual), "Undo restores Lure state and clears its action and line");

            await Install(game, Fixture(original), 1);
            game.State.carryingProp = PropKind.Climb; game.State.selectedX = 5; game.State.selectedZ = 4;
            await Frames(2); int oldHeight = Q(game.State, 5, 4).height;
            game.ApplyTool();
            Check(Q(game.State, 5, 4).height == oldHeight + 1 && game.State.carryingProp == PropKind.None
                && game.UndoCount == 1 && visual.EquipmentAction == "Dig", "successful soil placement commits once and starts Dig");
            float digAngle = await ObserveGrip(visual, .8f, "shovel Dig", true);
            Check(digAngle > 3f,
                "Dig visibly changes the shovel/arm pose despite the source GLB having no clips");
            Check(visual.EquipmentAction == "", "Dig ends without replaying itself");
            int undo = game.UndoCount, actions = game.ActionCount; game.ApplyTool(); await Frames(3);
            Check(game.UndoCount == undo && game.ActionCount == actions && Q(game.State, 5, 4).height == oldHeight + 1
                && visual.EquipmentAction == "", "failed soil placement adds no Dig, terrain change or undo");

            await Install(game, Fixture(original), 4);
            game.ApplyTool(); await Frames(3);
            Check(game.Fishing.IsOpen && visual.EquipmentAction == "Cast" && visual.FishingLine && visual.FishingLine.enabled,
                "actual harvest tool opens Fishing.Begin and starts the rod cast plus line");
            await Press(keyboard, Key.Escape);
            Check(!game.Fishing.IsOpen && game.State.animals[0].offspring == 2 && game.State.fish == 0 && game.UndoCount == 0
                && visual.EquipmentAction == "" && LineHidden(visual), "real Escape cancels fishing without harvest and clears the line");
            // Assist only the minigame tuning; real Update/input still reaches Finish -> CompleteFishing.
            game.Fishing.BarSize = .8f; game.Fishing.CatchGainPerSecond = 1f; game.Fishing.EscapeLossPerSecond = 0f;
            Require(game.Fishing.Begin(game, 4, 6), "Second real fishing Begin was rejected.");
            int fishingFrames = 0; double deadline = UnityEditor.EditorApplication.timeSinceStartup + 10;
            while (game.Fishing.IsOpen)
            {
                float velocity = Get<float>(game.Fishing, "velocity");
                bool held = game.Fishing.Bar + velocity * .15f < game.Fishing.Fish;
                InputSystem.QueueStateEvent(keyboard, held ? new KeyboardState(Key.Space) : new KeyboardState());
                await Frames(1); fishingFrames++;
                Require(UnityEditor.EditorApplication.timeSinceStartup < deadline, "Assisted real-frame fishing did not complete.");
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
            Check(fishingFrames >= 3 && game.State.fish == 2 && game.State.animals[0].offspring == 0
                && game.UndoCount == 1 && game.ActionCount == 1 && visual.EquipmentAction == "Hook_Pull"
                && visual.EquipmentAnimation.CurrentClipName == "Hook_Pull" && !visual.EquipmentAnimation.IsLooping,
                "real minigame completion harvests two offspring once and starts Hook_Pull");
            await ObserveGrip(visual, .25f, "rod Hook_Pull", false); await Shot("rod-hook-pull");
            game.Undo(); await Frames(3);
            Check(game.State.fish == 0 && game.State.animals[0].offspring == 2 && visual.EquipmentAction == "" && LineHidden(visual),
                "harvest Undo clears Hook_Pull and restores actual offspring");
            Require(game.Fishing.Begin(game, 4, 6), "Load cancellation fixture could not begin fishing.");
            await Frames(2); game.LoadLevel(1); game.paused = true; await Frames(3);
            Check(game.State.levelIndex == 1 && !game.Fishing.IsOpen && !game.Busy && visual.EquipmentAction == "" && LineHidden(visual),
                "LoadLevel cancels an active cast, minigame and line without a late completion");

            await Install(game, Fixture(original, true));
            var fans = Fans(game);
            Check(fans.Length == 4 && fans.All(a => a.CurrentClipName == "Idle" && a.IsLooping), "all four real Fan props use Idle while gameplay is paused");
            float idleTime = fans[0].TimeSeconds; await Seconds(.15f);
            Check(fans[0].TimeSeconds != idleTime, "bellows Idle advances on actual paused-game presentation frames");
            game.tuning.moveSeconds = .75f; game.paused = false; await Frames(3);
            Check(fans.All(a => a.CurrentClipName == "Pump_Loop" && a.IsLooping), "unpaused gameplay selects Pump_Loop on every Fan");
            Transform shell = Bone(fans[0].transform, "shell_upper"); Quaternion shellBefore = shell.localRotation;
            await Seconds(.23f);
            Check(Quaternion.Angle(shellBefore, shell.localRotation) > .1f, "Pump_Loop moves the real shell bone rather than only advancing a counter");
            await Shot("bellows-four-directions");
            game.paused = true; await Seconds(1.15f);
            Check(fans.All(a => a.CurrentClipName == "Idle"), "pausing returns all bellows to Idle after any pending pulse");
            game.State.movementClock = .74f; int beforeTick = game.State.tickCount;
            game.Step(); await Frames(3);
            Check(game.State.tickCount > beforeTick && fans.All(a => a.CurrentClipName == "Pump_Once" && !a.IsLooping),
                "real manual simulation Step triggers Pump_Once on every Fan");
            shellBefore = shell.localRotation; await Seconds(.2f);
            Check(Quaternion.Angle(shellBefore, shell.localRotation) > .1f, "Pump_Once visibly articulates the shell");
            await Seconds(1.1f);
            Check(fans.All(a => a.CurrentClipName == "Idle"), "single-step pulse returns to Idle without repeating settlement");
            game.State.selectedX = 4; game.State.selectedZ = 7; game.State.playerZ = 6;
            game.View.Initialize(game.State); await Frames(3); game.RotateTarget(); await Frames(3);
            Check(Q(game.State, 4, 7).rotation == 1, "real RotateTarget updates the selected fan direction");
            Fans(game);

            await Install(game, Fixture(original), 2); game.ApplyTool(); await Frames(2);
            Require(visual.EquipmentAction == "Cast", "Story cleanup fixture did not start Cast.");
            Require(game.View.Harbor.TryPlaceAt(game.State, HabitatImportedDockLayout.HomeApproach), "Could not reach actual home for story cleanup check.");
            int day = game.State.day; game.RestAtHome(); await Frames(3);
            Check(game.StoryPlaying && game.View.IsCinematic && visual.EquippedTool == "None"
                && visual.EquipmentAction == "" && LineHidden(visual), "actual home morning hides tools and cancels the previous cast and fishing line");
            game.CancelPendingAction(); await Frames(3);
            Check(!game.StoryPlaying && !game.View.IsCinematic && !game.Busy && game.State.day == day
                && visual.EquipmentAction == "" && LineHidden(visual), "story cancellation restores gameplay without a stale equipment action or day grant");
            game.RestAtHome(); await Frames(2); Require(game.StoryPlaying, "Second morning did not start.");
            game.Story.Skip(); await Frames(3);
            Check(!game.StoryPlaying && game.State.day == day + 1 && visual.EquipmentAction == "" && LineHidden(visual),
                "story completion also clears equipment and advances only its intended day");
            Check(BackpackObjects().All(originalBackpacks.Contains), "no new backpack object was introduced during tools, map changes or story");
        }
        catch (Exception exception) { failure = exception; report.AppendLine("ERROR " + exception); }
        finally
        {
            // Cancel before restoring references: late story/fishing callbacks must not touch original state.
            if (game.Fishing.IsOpen) game.Fishing.Cancel(); game.CancelPendingAction(); game.Hud.ClosePanels();
            game.State = original; game.tuning = tuning;
            history.Clear(); for (int i = savedHistory.Length - 1; i >= 0; i--) history.Push(savedHistory[i]);
            visits.Clear(); visits.AddRange(savedVisits); RestoreScalars(game, gameScalars); RestoreScalars(game.Fishing, fishingScalars);
            game.View.Initialize(original);
            // Restoration only, never used to advance/check an animation above.
            game.View.Render(original, tuning, game.overlays);
            foreach (var item in cameraFields) Set(game.View, item.Key, item.Value);
            camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation); camera.orthographicSize = cameraSize;
            camera.orthographic = cameraOrthographic; camera.backgroundColor = cameraColor; camera.clearFlags = clearFlags;
            Set(game.Hud, "nextRefresh", 0f); game.Hud.Refresh();
            foreach (var item in uiActive) if (item.Key) item.Key.SetActive(item.Value);
            RestoreScalars(game.Hud, hudScalars);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(selected);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
            oldKeyboard?.MakeCurrent(); oldMouse?.MakeCurrent();
            InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            bool restored = ReferenceEquals(game.State, original) && Json(original) == originalJson && ReferenceEquals(game.tuning, tuning)
                && history.SequenceEqual(savedHistory) && string.Join("|", history.Select(Json)) == historyJson
                && visits.SequenceEqual(savedVisits) && string.Join("|", visits.Select(Json)) == visitsJson && !game.Busy
                && gameScalars.All(item => Equals(item.Value, item.Key.GetValue(game)))
                && Keyboard.current == oldKeyboard && Mouse.current == oldMouse;
            report.AppendLine((restored ? "PASS" : "FAIL") + ": original State, tuning, undo, visited maps, tool, pause, scalar settings, camera, UI and input restored; no save written.");
            if (!restored && failure == null) failure = new InvalidOperationException("Runtime restoration differed from the original snapshot.");
            report.AppendLine((failure == null ? "PASS" : "FAIL") + ": " + assertions + " equipment assertions observed on real Play frames.");
            File.WriteAllText(Path.Combine(folder, "gameplay-checks.txt"), report.ToString(), new UTF8Encoding(false));
        }
        if (failure != null) throw new InvalidOperationException(report.ToString(), failure);
        return report.ToString();
    }
}
