using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Execute serially through the Unity C# API while the main scene is playing.
// This script observes genuine Game.Update frames; it never calls Render to
// advance a pending animation and never writes a save, asset, report or build.
public static class FeedbackMovementChecks
{
    static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly StringBuilder report = new StringBuilder();
    static int assertions;
    static T Get<T>(object instance, string field) => (T)instance.GetType().GetField(field, Fields).GetValue(instance);
    static void Set(object instance, string field, object value) => instance.GetType().GetField(field, Fields).SetValue(instance, value);
    static Dictionary<FieldInfo, object> Scalars(object instance) => instance.GetType().GetFields(Fields)
        .Where(f => !f.IsInitOnly && !f.IsStatic && (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string)))
        .ToDictionary(f => f, f => f.GetValue(instance));
    static void RestoreScalars(object instance, Dictionary<FieldInfo, object> saved)
    { foreach (var entry in saved) entry.Key.SetValue(instance, entry.Value); }
    static void Check(bool passed, string label)
    {
        report.AppendLine((passed ? "PASS " : "FAIL ") + (++assertions) + ": " + label);
        if (!passed) throw new InvalidOperationException(label);
    }
    static void Require(bool passed, string label)
    { if (!passed) throw new InvalidOperationException(label); }
    static string Json(HabitatState state) => JsonUtility.ToJson(state);
    static string Gameplay(HabitatState state)
    {
        var copy = HabitatRules.Clone(state); copy.message = ""; copy.events.Clear();
        return Json(copy);
    }
    static Tile Q(HabitatState state, int x, int z) => HabitatRules.TileAt(state, x, z);
    static Transform Player(HabitatView view) => Get<Transform>(view, "player");
    static Transform MovingProp(HabitatView view)
    {
        IList moving = Get<IList>(view, "gridStepProps");
        return moving.Count == 0 ? null : Get<Transform>(moving[0], "root");
    }
    static HabitatState Flat(int height = 1)
    {
        var state = new HabitatState { width = 7, depth = 7, playerX = 3, playerZ = 3,
            dockX = 1, dockZ = 1, levelName = "移动事务检查", objective = "临时检查，不保存", selectedX = 4, selectedZ = 3 };
        state.tiles = new Tile[state.width * state.depth];
        for (int i = 0; i < state.tiles.Length; i++) state.tiles[i] = new Tile { height = height };
        return state;
    }
    static async Task Frames(int count)
    {
        int start = Time.frameCount;
        double deadline = UnityEditor.EditorApplication.timeSinceStartup + 8;
        while (Time.frameCount - start < count)
        {
            if (!Application.isPlaying || UnityEditor.EditorApplication.timeSinceStartup > deadline)
                throw new InvalidOperationException("The Unity Play loop did not advance real frames.");
            await Task.Delay(12);
        }
    }
    static async Task Install(HabitatGame game, HabitatState fixture)
    {
        game.CancelPendingAction(); game.Hud.ClosePanels();
        game.paused = true; game.tool = 0; game.ActionCount = 0;
        Get<Stack<HabitatState>>(game, "history").Clear();
        game.State = fixture; game.View.Initialize(fixture);
        await Frames(2);
        Check(!game.Busy && game.UndoCount == 0 && game.ActionCount == 0 && ReferenceEquals(game.State, fixture),
            "fixture installed without a pending move or inherited undo action");
    }
    static void RejectBusyInput(HabitatGame game, string label)
    {
        HabitatState original = game.State; string before = Json(original);
        int undo = game.UndoCount, actions = game.ActionCount;
        bool recovering = game.IsRecovering;
        game.Move(0, 1); game.Pull(-1, 0); game.ApplyTool(); game.Undo(); game.Step();
        game.Select(0, 0); game.RotateTarget(); game.Buy("soil");
        Check(game.Busy && game.IsRecovering == recovering && ReferenceEquals(game.State, original) && Json(game.State) == before
            && game.UndoCount == undo && game.ActionCount == actions,
            label + ": repeated movement, pull, tool, undo, step, selection, rotate and purchase cannot mutate an in-flight action");
    }
    sealed class TravelTrace
    {
        public readonly List<float> speeds = new List<float>();
        public int frames;
        public float elapsed;
        public bool movedInBetween, propMovedInBetween, simultaneous, falling, landing, landingAtDestination, handlingPose;
        public float MeanSpeed => speeds.Count == 0 ? 0f : speeds.Average();
    }
    static async Task<TravelTrace> ObserveTravel(HabitatGame game, HabitatState source, string sourceJson,
        Vector3 start, Vector3 destination, bool expectProp = false)
    {
        var trace = new TravelTrace();
        Vector3 previous = Player(game.View).position;
        int previousFrame = Time.frameCount;
        float started = Time.unscaledTime;
        bool guardedMidway = false;
        double deadline = UnityEditor.EditorApplication.timeSinceStartup + 12;
        while (game.Busy)
        {
            await Frames(1);
            Require(UnityEditor.EditorApplication.timeSinceStartup < deadline, "Grid action exceeded its completion deadline.");
            if (!game.Busy) break;
            trace.frames++;
            Require(ReferenceEquals(game.State, source) && Json(game.State) == sourceJson,
                "Authoritative state changed before grid travel and landing completed.");
            Require(game.UndoCount == 0 && game.ActionCount == 0,
                "A pending grid action added an undo entry or action count before arrival.");
            Require(game.View.GridStepActive && !game.View.GridStepComplete,
                "Busy grid action lost its active visual transaction.");
            Vector3 current = Player(game.View).position;
            float distance = Vector3.Distance(start, destination);
            bool interior = distance > .001f && Vector3.Distance(current, start) > .015f && Vector3.Distance(current, destination) > .015f;
            trace.movedInBetween |= interior;
            if (Mathf.Abs(start.y - destination.y) < .001f && Time.frameCount - previousFrame == 1
                && Vector3.Distance(current, destination) > .025f)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, .08f);
                float travelled = Vector2.Distance(new Vector2(previous.x, previous.z), new Vector2(current.x, current.z));
                if (dt > .00001f && travelled > .00001f) trace.speeds.Add(travelled / dt);
            }
            if (expectProp)
            {
                Require(game.View.GridStepMovingPropCount == 1, "A single-box transaction lost its moving prop.");
                Transform prop = MovingProp(game.View);
                if (prop)
                {
                    IList motions = Get<IList>(game.View, "gridStepProps");
                    Vector3 propStart = Get<Vector3>(motions[0], "start"), propEnd = Get<Vector3>(motions[0], "end");
                    bool propInterior = Vector3.Distance(prop.position, propStart) > .015f && Vector3.Distance(prop.position, propEnd) > .015f;
                    trace.propMovedInBetween |= propInterior;
                    trace.simultaneous |= propInterior && interior;
                }
            }
            HabitatPlayerVisual visual = game.View.PlayerVisual;
            trace.falling |= visual.IsFalling;
            trace.landing |= visual.IsLanding;
            trace.landingAtDestination |= visual.IsLanding && Vector3.Distance(current, destination) < .002f;
            trace.handlingPose |= visual.HandlingPoseWeight > .15f;
            if (!guardedMidway && (interior || trace.propMovedInBetween || visual.IsFalling))
            {
                RejectBusyInput(game, "observed intermediate animation frame");
                guardedMidway = true;
            }
            previous = current; previousFrame = Time.frameCount;
        }
        trace.elapsed = Time.unscaledTime - started;
        Check(trace.frames > 0 && !game.Busy && !game.View.GridStepActive && game.View.GridStepComplete,
            "real frames completed the visual transaction before releasing the Busy lock");
        Check(!ReferenceEquals(game.State, source) && game.ActionCount == 1 && game.UndoCount == 1,
            "arrival commits the candidate exactly once with one undo entry");
        Check(Vector3.Distance(Player(game.View).position, destination) < .002f,
            "the player is at the destination when the authoritative candidate is committed");
        return trace;
    }
    static async Task CheckUndo(HabitatGame game, HabitatState expected, string label)
    {
        string before = Gameplay(expected);
        game.Undo(); await Frames(2);
        Check(!game.Busy && game.UndoCount == 0 && Gameplay(game.State) == before,
            label + ": one undo restores the pre-action player, props, inventory and tide");
        Vector3 target = Get<Vector3>(game.View, "playerDestination");
        Check(Vector3.Distance(Player(game.View).position, target) < .002f,
            label + ": undo also restores the visual location without a new movement transaction");
    }
    static async Task CheckLoadCancellation(HabitatGame game, int levelIndex, string label)
    {
        game.LoadLevel(levelIndex); game.paused = true;
        HabitatState loaded = game.State; string loadedJson = Json(loaded);
        Check(loaded.levelIndex == levelIndex && !game.Busy && !game.IsRecovering && game.TransitionOpacity == 0f
            && !game.View.GridStepActive && game.View.GridStepMovingPropCount == 0 && game.UndoCount == 0 && game.ActionCount == 0,
            label + ": LoadLevel immediately cancels the old candidate, visual motion, blackout and history");
        // Longer than the original one-cell push or salvage sequence at the
        // normal frame rate; no test manually advances the discarded candidate.
        await Frames(48);
        Check(ReferenceEquals(game.State, loaded) && Json(game.State) == loadedJson && !game.Busy && !game.IsRecovering
            && game.TransitionOpacity == 0f && game.UndoCount == 0 && game.ActionCount == 0,
            label + ": subsequent real frames never commit the cancelled candidate into the newly loaded map");
    }
    static HabitatState FloatFixture(bool corner=false)
    {
        HabitatState s=Flat(0);
        Q(s,3,3).prop=PropKind.Float;Q(s,2,3).prop=PropKind.Fan;Q(s,2,3).rotation=1;
        if(corner){Q(s,4,2).prop=PropKind.Fan;Q(s,4,2).rotation=0;Q(s,3,4).prop=PropKind.Obstacle;}
        s.carryingAnimal=1;
        s.animals.Add(new Animal{id=1,species=Species.Shrimp,x=3,z=3,homeX=3,homeZ=3,carried=true});
        return s;
    }
    static async Task<List<Vector2Int>> ObserveRides(HabitatGame game,int expectedUndo)
    {
        HabitatState source=game.State;string sourceJson=Json(source);
        var route=new List<Vector2Int>{new Vector2Int(source.playerX,source.playerZ)};
        bool intermediate=false;int checkedFrames=0;
        double deadline=UnityEditor.EditorApplication.timeSinceStartup+12;
        while(game.Busy)
        {
            await Frames(1);
            Require(UnityEditor.EditorApplication.timeSinceStartup<deadline,"Segmented float ride did not finish.");
            if(!ReferenceEquals(game.State,source))
            {
                Vector2Int arrived=new Vector2Int(game.State.playerX,game.State.playerZ);
                Vector2Int previous=route[route.Count-1];
                Require(Mathf.Abs(arrived.x-previous.x)+Mathf.Abs(arrived.y-previous.y)==1,
                    "A wind segment skipped a tile or cut diagonally across a corner.");
                route.Add(arrived);source=game.State;sourceJson=Json(source);
            }
            if(!game.Busy)break;
            Require(Json(game.State)==sourceJson,"Authoritative ride state changed before the current support reached its next cell.");
            Require(game.UndoCount==expectedUndo&&game.ActionCount==0,"Passive wind segments added extra undo or action-count entries.");
            HabitatState candidate=Get<HabitatState>(game,"pendingState");
            Require(Math.Abs(candidate.playerX-source.playerX)+Math.Abs(candidate.playerZ-source.playerZ)==1,
                "A pending ride represents more than one cardinal wind step.");
            Transform prop=MovingProp(game.View);
            if(prop)
            {
                IList motions=Get<IList>(game.View,"gridStepProps");
                Vector3 offset=Get<Vector3>(game.View,"playerDestination")-Get<Vector3>(motions[0],"end");
                Require(Vector3.Distance(Player(game.View).position,prop.position+offset)<.002f,
                    "The passenger moved ahead of or separated vertically from its animated floating support.");
                HabitatPlayerVisual visual=game.View.PlayerVisual;
                Require(visual.Motion==HabitatPlayerMotion.Ride&&visual.MoveSpeed<.001f&&visual.HandlingPoseWeight<.001f,
                    "A passenger used walking or push/pull animation while riding.");
                checkedFrames++;
                Vector3 start=Get<Vector3>(motions[0],"start"),end=Get<Vector3>(motions[0],"end");
                intermediate|=Vector3.Distance(prop.position,start)>.02f&&Vector3.Distance(prop.position,end)>.02f;
            }
        }
        Check(intermediate&&checkedFrames>=2&&!game.Busy&&!game.View.GridStepActive,
            "float rides show real intermediate frames with the idle passenger attached to the moving support");
        Check(game.UndoCount==expectedUndo&&game.ActionCount==0,
            "completed wind simulation preserves its intended undo count and does not count as a manual action");
        Check(game.State.animals[0].x==game.State.playerX&&game.State.animals[0].z==game.State.playerZ,
            "carried animal coordinates commit with the passenger at the end of each ride");
        return route;
    }

    public static async Task<string> Run()
    {
        HabitatGame game = HabitatGame.Instance;
        if (!Application.isPlaying || !game || game.Busy || game.Hud.ModalOpen || (game.Fishing && game.Fishing.IsOpen))
            throw new InvalidOperationException("Run on the idle main-island scene in Play mode with no modal or fishing session.");
        HabitatState original = game.State;
        string originalJson = Json(original);
        Tuning originalTuning = game.tuning;
        var history = Get<Stack<HabitatState>>(game, "history");
        HabitatState[] undo = history.ToArray();
        var gameScalars = Scalars(game); var hudScalars = Scalars(game.Hud);
        var uiActive = game.Hud.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.gameObject, t => t.gameObject.activeSelf);
        string[] cameraFields = { "cameraYaw", "cameraSize", "cameraTarget", "cameraTargetSmooth", "followPlayer", "overviewCamera", "overviewSize", "framingAspect", "framingDirty" };
        var camera = cameraFields.ToDictionary(n => n, n => typeof(HabitatView).GetField(n, Fields).GetValue(game.View));
        Vector3 cameraPosition = game.View.GameCamera.transform.position;
        Quaternion cameraRotation = game.View.GameCamera.transform.rotation;
        float cameraSize = game.View.GameCamera.orthographicSize;
        var selectedUi = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        var previousKeyboard = Keyboard.current;
        HabitatScreenTransition originalTransition = Get<HabitatScreenTransition>(game, "screenTransition");
        CanvasGroup originalCover = originalTransition ? Get<CanvasGroup>(originalTransition, "group") : null;
        Keyboard keyboard = null;
        Exception failure = null;
        assertions = 0; report.Clear();
        try
        {
            game.paused = true;
            game.tuning = JsonUtility.FromJson<Tuning>(JsonUtility.ToJson(originalTuning));
            game.tuning.animalMovement = false;
            keyboard = InputSystem.AddDevice<Keyboard>("Feedback movement acceptance keyboard"); keyboard.MakeCurrent();
            Check(game.View.PlayerVisual && game.View.PlayerVisual.IsModelActive, "the actual character model is available for handling and fall verification");

            HabitatState fixture = Flat(); fixture.carryingProp = PropKind.Climb;
            await Install(game, fixture);
            HabitatState before = HabitatRules.Clone(fixture); string sourceJson = Json(fixture);
            Vector3 start = Player(game.View).position;
            game.Move(1, 0);
            Vector3 destination = Get<Vector3>(game.View, "playerDestination");
            Check(game.Busy && game.View.GridStepActive && !game.View.GridStepComplete && ReferenceEquals(game.State, fixture)
                && Json(game.State) == sourceJson && game.State.playerX == 3 && game.State.carryingProp == PropKind.Climb,
                "a flat step begins with the authoritative tile and carried item unchanged");
            RejectBusyInput(game, "flat step");
            TravelTrace walk = await ObserveTravel(game, fixture, sourceJson, start, destination);
            Check(walk.movedInBetween && walk.speeds.Count >= 2 && walk.speeds.All(v => Mathf.Abs(v - HabitatPlayerVisual.WalkSpeed) < .08f),
                "flat movement has visible intermediate positions and constant measured speed (samples=" + walk.speeds.Count + ", mean=" + walk.MeanSpeed.ToString("F3") + ")");
            Check(game.State.playerX == 4 && game.State.playerZ == 3 && game.State.carryingProp == PropKind.Climb,
                "flat arrival changes exactly one grid coordinate while retaining the carried item");
            await CheckUndo(game, before, "flat step");

            fixture = Flat(); Q(fixture, 4, 3).prop = PropKind.Crate;
            await Install(game, fixture); before = HabitatRules.Clone(fixture); sourceJson = Json(fixture);
            start = Player(game.View).position; game.Move(1, 0);
            destination = Get<Vector3>(game.View, "playerDestination");
            Check(game.Busy && game.View.GridStepMovingPropCount == 1 && Q(game.State, 4, 3).prop == PropKind.Crate
                && Q(game.State, 5, 3).prop == PropKind.None && game.State.playerX == 3,
                "push planning leaves the player and authoritative crate at their source cells");
            RejectBusyInput(game, "push");
            TravelTrace push = await ObserveTravel(game, fixture, sourceJson, start, destination, true);
            Check(push.simultaneous && push.handlingPose, "both character and crate move through intermediate positions with the handling pose active");
            Check(push.speeds.Count >= 2 && push.speeds.All(v => Mathf.Abs(v - HabitatPlayerVisual.HandlingSpeed) < .08f)
                && push.MeanSpeed < walk.MeanSpeed * .8f,
                "measured push speed is constant and slower than ordinary walking (mean=" + push.MeanSpeed.ToString("F3") + ")");
            Check(game.State.playerX == 4 && Q(game.State, 4, 3).prop == PropKind.None && Q(game.State, 5, 3).prop == PropKind.Crate,
                "push arrival commits the crate and character together");
            await CheckUndo(game, before, "push");

            fixture = Flat(2); Q(fixture, 4, 3).height = 1; Q(fixture, 4, 3).prop = PropKind.Crate;
            Q(fixture, 5, 3).height = 1; Q(fixture, 5, 3).prop = PropKind.Tide;
            await Install(game, fixture); before = HabitatRules.Clone(fixture); sourceJson = Json(fixture);
            start = Player(game.View).position; game.Move(1, 0);
            destination = Get<Vector3>(game.View, "playerDestination");
            Check(game.Busy && game.View.GridStepMovingPropCount == 1 && Vector3.Distance(start, destination) < .002f
                && !game.State.tideActive && game.State.waterLevel == 0,
                "a crate-only tidal push is a pending action even though the player cannot enter the newly flooded source tile");
            TravelTrace tidal = await ObserveTravel(game, fixture, sourceJson, start, destination, true);
            Check(tidal.propMovedInBetween && game.State.playerX == 3 && game.State.tideActive && game.State.waterLevel == 1
                && Q(game.State, 4, 3).prop == PropKind.None && Q(game.State, 5, 3).coveredProp == PropKind.Tide,
                "crate-only arrival commits the successful box and tide change without incorrectly cancelling the operation");
            await CheckUndo(game, before, "crate-only tidal push");

            fixture = Flat(3); Q(fixture, 4, 3).height = 1;
            await Install(game, fixture); before = HabitatRules.Clone(fixture); sourceJson = Json(fixture);
            start = Player(game.View).position; game.Move(1, 0);
            destination = Get<Vector3>(game.View, "playerDestination");
            Check(game.Busy && destination.y < start.y - 1f && game.State.playerX == 3,
                "a two-layer descent begins before changing the logical standing tile");
            TravelTrace fall = await ObserveTravel(game, fixture, sourceJson, start, destination);
            Check(fall.movedInBetween && fall.falling && fall.landing && fall.landingAtDestination,
                "descent shows airborne and landing phases and keeps the old logical tile during the landing pose");
            Check(!game.View.PlayerVisual.IsFalling && !game.View.PlayerVisual.IsLanding && game.State.playerX == 4,
                "descent commits only after both falling and landing have completed");
            await CheckUndo(game, before, "descent");

            fixture = Flat(2); Q(fixture, 4, 3).height = 0; Q(fixture, 4, 3).prop = PropKind.Float;
            await Install(game, fixture); before = HabitatRules.Clone(fixture); sourceJson = Json(fixture);
            game.ApplyTool();
            Check(game.Busy && game.IsRecovering && game.TransitionOpacity == 0f && ReferenceEquals(game.State, fixture)
                && Json(game.State) == sourceJson && game.UndoCount == 0 && game.ActionCount == 0,
                "recovery begins with the old world intact and the blackout initially transparent");
            RejectBusyInput(game, "recovery fade-out");
            bool sawFadingOut = false, sawCommitAtBlack = false, sawFadingIn = false;
            double recoveryDeadline = UnityEditor.EditorApplication.timeSinceStartup + 8;
            while (game.Busy)
            {
                await Frames(1);
                Require(UnityEditor.EditorApplication.timeSinceStartup < recoveryDeadline, "Recovery transition exceeded its completion deadline.");
                if (!sawCommitAtBlack && ReferenceEquals(game.State, fixture))
                {
                    Require(Json(game.State) == sourceJson && game.UndoCount == 0 && game.ActionCount == 0,
                        "Recovery mutated source state or undo before the black frame.");
                    sawFadingOut |= game.TransitionOpacity > 0f && game.TransitionOpacity < 1f;
                }
                else if (!sawCommitAtBlack)
                {
                    Check(game.IsRecovering && game.TransitionOpacity >= .9999f,
                        "the first observed recovery commit occurs behind a fully opaque black frame");
                    Check(game.State.playerX == 2 && game.State.playerZ == 3 && Q(game.State, 3, 3).prop == PropKind.Crate
                        && Q(game.State, 4, 3).prop == PropKind.None && game.State.carryingProp == PropKind.None
                        && game.State.carryingAnimal < 0 && game.ActionCount == 1 && game.UndoCount == 1,
                        "blackout commit places the recovered crate on the old standing tile, retreats one tile and leaves hands empty");
                    sawCommitAtBlack = true;
                    RejectBusyInput(game, "recovery after black-frame commit");
                }
                else
                {
                    Require(game.ActionCount == 1 && game.UndoCount == 1, "Recovery committed its history more than once.");
                    sawFadingIn |= game.TransitionOpacity > 0f && game.TransitionOpacity < 1f;
                }
            }
            Check(sawFadingOut && sawCommitAtBlack && sawFadingIn && !game.IsRecovering && game.TransitionOpacity == 0f,
                "recovery completes a real fade-out, opaque commit and fade-in sequence");
            await CheckUndo(game, before, "recovery");

            fixture = Flat(2); Q(fixture, 4, 3).height = 0; Q(fixture, 4, 3).prop = PropKind.Float;
            Q(fixture, 2, 3).prop = PropKind.Obstacle;
            await Install(game, fixture); string rejectedBefore = Gameplay(fixture);
            game.ApplyTool(); await Frames(3);
            Check(!game.Busy && !game.IsRecovering && game.TransitionOpacity == 0f && Gameplay(game.State) == rejectedBefore
                && game.UndoCount == 0 && game.ActionCount == 0 && !string.IsNullOrEmpty(game.State.message),
                "an obstructed recovery is rejected with an explanation, no state mutation, no blackout and no undo entry");

            fixture = Flat(); Q(fixture, 4, 3).prop = PropKind.Crate;
            await Install(game, fixture); game.Move(1, 0); await Frames(1);
            Check(game.Busy && ReferenceEquals(game.State, fixture) && game.View.GridStepMovingPropCount == 1,
                "load-cancellation fixture has an actual in-flight push with uncommitted source state");
            await CheckLoadCancellation(game, 0, "load during a push");

            fixture = Flat(2); Q(fixture, 4, 3).height = 0; Q(fixture, 4, 3).prop = PropKind.Float;
            await Install(game, fixture); game.ApplyTool(); await Frames(1);
            Check(game.Busy && game.IsRecovering && ReferenceEquals(game.State, fixture) && game.TransitionOpacity > 0f
                && game.TransitionOpacity < 1f,
                "load-cancellation fixture has an actual recovery fade-out before the black-frame commit");
            await CheckLoadCancellation(game, 1, "load during recovery fade-out");

            fixture = Flat(2); Q(fixture, 4, 3).height = 0; Q(fixture, 4, 3).prop = PropKind.Float;
            await Install(game, fixture); game.ApplyTool();
            double commitDeadline = UnityEditor.EditorApplication.timeSinceStartup + 8;
            while (ReferenceEquals(game.State, fixture) && game.Busy)
            {
                await Frames(1);
                Require(UnityEditor.EditorApplication.timeSinceStartup < commitDeadline, "Recovery cancellation fixture never reached its black-frame commit.");
            }
            Check(game.Busy && game.IsRecovering && !ReferenceEquals(game.State, fixture) && game.UndoCount == 1 && game.ActionCount == 1,
                "load-cancellation fixture reaches the committed blackout while the recovery presentation is still active");
            await CheckLoadCancellation(game, 2, "load after recovery commit but before fade-in completes");

            fixture=FloatFixture();fixture.movementClock=2.49f;
            game.tuning.moveSeconds=2.5f;
            await Install(game,fixture);game.paused=false;
            double rideDeadline=UnityEditor.EditorApplication.timeSinceStartup+5;
            while(!game.Busy)
            {
                await Frames(1);
                Require(UnityEditor.EditorApplication.timeSinceStartup<rideDeadline,"Automatic wind never entered a ride transaction.");
            }
            game.paused=true; // Prevent another automatic tick after this observed arrival.
            Check(ReferenceEquals(game.State,fixture)&&game.State.playerX==3&&Q(game.State,3,3).prop==PropKind.Float
                &&Q(game.State,4,3).prop==PropKind.None&&game.State.animals[0].x==3&&game.UndoCount==0&&game.ActionCount==0,
                "automatic wind defers authoritative passenger, carried animal and float coordinates until visual arrival");
            RejectBusyInput(game,"automatic float ride");
            List<Vector2Int> rideRoute=await ObserveRides(game,0);
            Check(rideRoute.Count==2&&game.State.playerX==4&&game.State.playerZ==3&&Q(game.State,4,3).prop==PropKind.Float,
                "one automatic wind event commits exactly one cardinal passenger movement");

            fixture=FloatFixture(true);fixture.movementClock=.19f;game.tuning.moveSeconds=.2f;
            await Install(game,fixture);before=HabitatRules.Clone(fixture);
            HabitatState expected=HabitatRules.Clone(fixture);HabitatRules.Tick(expected,game.tuning,1f);
            game.Step();
            Check(game.Busy&&ReferenceEquals(game.State,fixture)&&game.State.playerX==3&&game.UndoCount==1&&game.ActionCount==0,
                "one-second Step starts a ride while retaining exactly its original single undo entry");
            rideRoute=await ObserveRides(game,1);
            Check(rideRoute.Count==5&&rideRoute[1]==new Vector2Int(4,3)&&rideRoute[2]==new Vector2Int(4,4)
                &&rideRoute[3]==new Vector2Int(4,5)&&rideRoute[4]==new Vector2Int(4,6),
                "multiple wind ticks animate separate cardinal segments through the actual corner instead of one diagonal shortcut");
            Check(Gameplay(game.State)==Gameplay(expected)&&Mathf.Abs(game.State.elapsed-1f)<.0001f,
                "segmented Step preserves the full one second of model simulation including leftover ticks after the last ride");
            await CheckUndo(game,before,"segmented float Step");

            fixture=FloatFixture(true);fixture.movementClock=.19f;
            await Install(game,fixture);game.Step();await Frames(1);
            Check(game.Busy&&Get<int>(game,"simulationStepsRemaining")>0,"float cancellation fixture has unconsumed simulation segments");
            await CheckLoadCancellation(game,0,"load during a segmented float ride");
            Check(Get<int>(game,"simulationStepsRemaining")==0,"loading clears the rest of the queued simulation as well as its current ride");

            fixture=Flat();await Install(game,fixture);game.Step();
            Check(ReferenceEquals(game.State,fixture)&&!game.Busy&&game.UndoCount==1&&game.ActionCount==0&&Mathf.Abs(game.State.elapsed-1f)<.0001f,
                "a Step without passenger movement retains the original State reference and synchronous ecology behavior");

            report.AppendLine("TIMING: walk=" + walk.elapsed.ToString("F3") + "s; push=" + push.elapsed.ToString("F3")
                + "s; fall=" + fall.elapsed.ToString("F3") + "s (observed frame durations, not target-FPS claims).");
        }
        catch (Exception exception)
        { failure = exception; report.AppendLine("ERROR: " + exception); }
        finally
        {
            // Cancellation MUST precede replacing State: a late candidate must
            // never overwrite the original user snapshot after this test exits.
            game.CancelPendingAction();
            game.Hud.ClosePanels();
            game.State = original; game.tuning = originalTuning;
            history.Clear(); for (int i = undo.Length - 1; i >= 0; i--) history.Push(undo[i]);
            RestoreScalars(game, gameScalars);
            game.View.Initialize(original);
            foreach (var entry in camera) typeof(HabitatView).GetField(entry.Key, Fields).SetValue(game.View, entry.Value);
            game.View.GameCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            game.View.GameCamera.orthographicSize = cameraSize;
            Set(game.Hud, "nextRefresh", 0f); game.Hud.Refresh();
            foreach (var entry in uiActive) if (entry.Key) entry.Key.SetActive(entry.Value);
            RestoreScalars(game.Hud, hudScalars);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(selectedUi);
            HabitatScreenTransition currentTransition = Get<HabitatScreenTransition>(game, "screenTransition");
            if (currentTransition)
            {
                CanvasGroup currentCover = Get<CanvasGroup>(currentTransition, "group");
                if (currentCover && currentCover != originalCover)
                { UnityEngine.Object.Destroy(currentCover.gameObject); Set(currentTransition, "group", null); }
                if (currentTransition != originalTransition) UnityEngine.Object.Destroy(currentTransition);
            }
            Set(game, "screenTransition", originalTransition);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            previousKeyboard?.MakeCurrent();
            bool restored = ReferenceEquals(game.State, original) && Json(original) == originalJson
                && ReferenceEquals(game.tuning, originalTuning) && history.SequenceEqual(undo) && !game.Busy
                && !game.View.GridStepActive && game.ActionCount == (int)gameScalars[typeof(HabitatGame).GetField("ActionCount", Fields)]
                && game.paused == (bool)gameScalars[typeof(HabitatGame).GetField("paused", Fields)];
            report.AppendLine((restored ? "PASS" : "FAIL") + ": original State reference, tuning, complete undo history, actions, pause, camera and UI restored; no save/assets/build written.");
            if (!restored && failure == null) failure = new Exception("The original runtime snapshot was not completely restored.");
        }
        report.AppendLine((failure == null ? "PASS" : "FAIL") + ": " + assertions + " movement and recovery assertions on actual Play frames.");
        if (failure != null) throw new InvalidOperationException(report.ToString(), failure);
        return report.ToString();
    }
}
