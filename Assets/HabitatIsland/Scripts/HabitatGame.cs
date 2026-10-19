using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HabitatIsland
{
    [DefaultExecutionOrder(-50)]
    public partial class HabitatGame : MonoBehaviour
    {
        public static HabitatGame Instance;
        public HabitatState State;
        public Tuning tuning = new Tuning();
        public bool paused;
        public bool overlays = true;
        public float speed = 1;
        public int tool;
        public HabitatView View;
        public HabitatHud Hud;
        public HabitatFishing Fishing;
        public int startLevel = HabitatFigJamLevel.Index;
        public int ActionCount;
        public string LastExport;
        readonly Stack<HabitatState> history = new Stack<HabitatState>();
        static Snapshot harborReturn;
        static bool harborReturnPaused;
        static int requestedLevel = -1;
        HabitatState pendingState, pendingBefore;
        HabitatPlayerMotion pendingMotion;
        float harborStepDistance;
        bool recovering, pendingSimulation;
        int simulationStepsRemaining;
        float simulationStepSeconds;
        public bool Busy => pendingState != null || StoryPlaying;
        public bool IsRecovering => recovering;
        public float TransitionOpacity => 0f; // Recovery is now entirely visible; retained for diagnostics.
        float nextMove;
        public int UndoCount => history.Count;
        public static readonly string[] ToolNames = { "抱起 / 放下", "铺土 / 堆高", "钓竿引导", "布置虾笼", "收获后代", "一次性挖土", "磁铁", "发射器" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTravelSession() { harborReturn = null; requestedLevel = -1; }

        void Awake()
        {
            Debug.Log("[Habitat] Game Awake begins");
            Instance = this;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            State = HabitatLevels.Create(HabitatLevels.HomeHarborIndex);
            paused = State.authoredLevel || startLevel == 1 || startLevel == 0;
            if (harborReturn != null)
            {
                SessionStarted=true;
                State = harborReturn.state; tuning = harborReturn.tuning;
                ActionCount = harborReturn.actions; paused = harborReturnPaused;
                RestoreVisitedLevels(harborReturn.visitedLevels);
                harborReturn = null;
            }
            if (requestedLevel >= 0)
            {
                SessionStarted=true;
                State = HabitatLevels.Create(requestedLevel); requestedLevel = -1;
                ActionCount = 0; paused = State.authoredLevel || State.levelIndex == 1 || State.levelIndex == 0;
            }
            View = GetComponent<HabitatView>() ?? gameObject.AddComponent<HabitatView>();
            View.Initialize(State);
            HabitatAudio.Ensure();
            InitializeJourney();
            Hud = GetComponent<HabitatHud>() ?? gameObject.AddComponent<HabitatHud>();
            Hud.Initialize(this);
            Fishing = GetComponent<HabitatFishing>() ?? gameObject.AddComponent<HabitatFishing>();
            CaptureRetryPoint();
            Debug.Log("[Habitat] Game Awake complete");
        }

        void Update()
        {
            if (State == null || backgroundSuspended) return;
            if(!Busy && !Fishing.IsOpen && !HasTool(tool))tool=0;
            UpdatePlayTime();
            HandleInput();
            TickUtilityPresentation();
            TickChestPresentation();
            TickFishingCatch();
            if (!Busy && !paused && !Hud.ModalOpen && !Fishing.IsOpen) AdvanceSimulation(Mathf.Min(Time.unscaledDeltaTime, .1f) * speed);
            View.Render(pendingState != null && !recovering && !utilityDigPending && !ChestOpening && !FishingLanding ? pendingState : State, tuning, overlays);
            if (recovering) AdvanceRecovery();
            if (pendingState != null && !recovering && !ChestOpening && !FishingLanding && View.GridStepComplete && utilitySeconds<=0 && (!pendingLure || View.ResidentsArrived(pendingState)))
            {
                bool resumeSimulation=pendingSimulation;
                CommitPending(); pendingState = pendingBefore = null; pendingSimulation=false;
                View.CancelGridStep();
                FinishUtilityPresentation();
                if(resumeSimulation) ContinueSimulation();
            }
            UpdateTutorial();
            UpdateChapter();
            if(!Busy && !Fishing.IsOpen && HabitatMechanicLevels.HasHabitatGoal(State) && View.ResidentsArrived(State))
            {
                bool shed=false;
                foreach(var animal in State.animals)shed|=HabitatRules.TryShedCrabShell(State,animal,tuning);
                // The shell is visible on the next rendered frame. Wait for its exit animation before winning.
                if(!shed)HabitatMechanicLevels.CompleteHabitat(State,tuning);
            }
            if(!Busy && !Fishing.IsOpen && State.levelIndex==5 && State.won && !State.firstClearCgSeen && !Hud.ModalOpen && Story)
            {
                Hud.ClosePanels();
                Story.BeginCg("cg01",()=>{State.firstClearCgSeen=true;CaptureRetryPoint();});
            }
            Hud.Refresh();
            TickSafety();
        }

        void HandleInput()
        {
            if (StoryPlaying)
            {
                var keys = Keyboard.current;
                if (keys != null && (keys.spaceKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame)) Story.TogglePause();
                return;
            }
            var k = Keyboard.current;
            if (FishingLanding && k != null && k.escapeKey.wasPressedThisFrame) { CancelPendingAction(); State.message="已收竿：渔获尚未入篓，可重新尝试。"; return; }
            if (Busy) { CaptureBufferedInput(k); HandleCameraInput(k); return; }
            if (Fishing != null && Fishing.IsOpen) return;
            if (ConsumeBufferedInput()) return;
            HandleCameraInput(k);
            if (k != null)
            {
                if (k.spaceKey.wasPressedThisFrame && !Hud.ModalOpen) paused = !paused;
                if (k.escapeKey.wasPressedThisFrame) { Hud.HandleEscape(); return; }
                if(Hud.SessionMenuOpen)return;
                if(k.iKey.wasPressedThisFrame){Hud.ToggleInventory();return;}
                if(Hud.ModalOpen)return;
                if (k.f1Key.wasPressedThisFrame) Hud.ToggleHelp();
                if (k.tabKey.wasPressedThisFrame) Hud.ToggleSettings();
                if (k.bKey.wasPressedThisFrame) OpenShopInteraction();
                UpdateRestartHold(k);
                if (k.zKey.wasPressedThisFrame && !k.ctrlKey.isPressed && !Hud.ModalOpen) Undo();
                if (k.periodKey.wasPressedThisFrame && !Hud.ModalOpen) Step();

                int priorTool=tool;
                if (k.digit1Key.wasPressedThisFrame) TrySelectTool(0);
                if (k.digit2Key.wasPressedThisFrame) TrySelectTool(1);
                if (k.digit3Key.wasPressedThisFrame) TrySelectTool(2);
                if (k.digit4Key.wasPressedThisFrame) TrySelectTool(3);
                if (k.digit5Key.wasPressedThisFrame) TrySelectTool(4);
                if (k.digit6Key.wasPressedThisFrame) TrySelectTool(5);
                if (k.digit7Key.wasPressedThisFrame) TrySelectTool(6);
                if (k.digit8Key.wasPressedThisFrame) TrySelectTool(7);
                if(tool!=priorTool)HabitatAudio.Play(HabitatAudioCue.Click);
                if (!Hud.ModalOpen)
                {
                    Vector2Int input = ReadDirection(k,out bool first);
                    int dx=input.x,dz=input.y;
                    if (State.onHarbor)
                    {
                        Vector2 axes = new Vector2((k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0),
                            (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0));
                        Quaternion rotation = View.InputCameraRotation;
                        MoveHarborByScreen(axes, Time.unscaledDeltaTime, rotation);
                    }
                    else if ((dx != 0 || dz != 0) && (first || Time.unscaledTime >= nextMove))
                    {
                        Quaternion cameraRotation = View != null && View.GameCamera != null ? View.InputCameraRotation : Quaternion.Euler(HabitatView.DefaultCameraPitch, -HabitatView.DefaultCameraYaw, 0f);
                        Vector2Int step = ScreenDirectionToGrid(dx, dz, cameraRotation);
                        if (k.ctrlKey.isPressed) Pull(step.x, step.y);
                        else Move(step.x, step.y, k.leftShiftKey.isPressed || k.rightShiftKey.isPressed);
                        // Successful movement is gated by arrival, not by a repeat
                        // timer shorter than the visual travel duration.
                        nextMove = Time.unscaledTime + .12f;
                    }
                    if (k.eKey.wasPressedThisFrame) ApplyTool();
                    if (k.rKey.wasPressedThisFrame) RotateTarget();
                }
            }
            var m = Mouse.current;
            if (m == null || Hud.ModalOpen || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) return;
            if (m.leftButton.wasPressedThisFrame && View.Pick(m.position.ReadValue(), out int x, out int z)) Select(x, z);
            if (m.rightButton.wasPressedThisFrame)
            {
                if (View.Pick(m.position.ReadValue(), out int tx, out int tz)) { Select(tx, tz); ApplyTool(); }
            }

        }

        /// <summary>Map a screen-space arrow to one board step; direct Move calls remain in board coordinates.</summary>
        public static Vector2Int ScreenDirectionToGrid(int horizontal, int vertical, Quaternion cameraRotation, float pendingCameraTurn = 0f)
        {
            // Orbit yaw runs opposite to the camera transform's yaw. Include this frame's
            // Q/C turn because HabitatView applies its new camera transform during Render.
            Quaternion rotation = Quaternion.AngleAxis(-pendingCameraTurn, Vector3.up) * cameraRotation;
            Vector3 direction = rotation * (Vector3.right * horizontal + Vector3.up * vertical);
            if (Mathf.Abs(direction.x) < .0001f && Mathf.Abs(direction.z) < .0001f) return Vector2Int.zero;
            return Mathf.Abs(direction.x) > Mathf.Abs(direction.z)
                ? new Vector2Int(direction.x > 0f ? 1 : -1, 0)
                : new Vector2Int(0, direction.z > 0f ? 1 : -1);
        }

        public void Select(int x, int z)
        {
            if (State == null || Busy || State.onHarbor || (Fishing != null && Fishing.IsOpen) || !HabitatRules.Inside(State,x,z)) return;
            State.selectedX = x; State.selectedZ = z;
            int dx = x-State.playerX, dz = z-State.playerZ;
            if (dx == 0 && dz == 0) return;
            // Keep the exact tool target; only the character's heading snaps to a board axis.
            // On a diagonal tie, retain an already suitable heading to avoid needless turns.
            int ax = Math.Abs(dx), az = Math.Abs(dz);
            if (ax == az && ((State.facingX != 0 && State.facingX == Math.Sign(dx)) ||
                             (State.facingZ != 0 && State.facingZ == Math.Sign(dz)))) return;
            State.facingX = ax >= az ? Math.Sign(dx) : 0;
            State.facingZ = ax >= az ? 0 : Math.Sign(dz);
        }
        public void SelectAnimal(int id)
        {
            var a = State.animals.Find(v => v.id == id);
            if (a != null) Select(a.x, a.z);
        }
        public void Move(int dx, int dz, bool climb = false)
        {
            if (Busy) return;
            if (State.onHarbor)
            {
                MoveHarbor(() => View.Harbor.MoveWorld(State, new Vector3(dx, 0, dz), .1f));
                return;
            }
            if (View != null && View.Harbor.CanEnter(State, dx, dz))
            {
                Mutate(() => View.Harbor.TryEnter(State, dx, dz), HabitatActionCue.StepWood);
                View.FocusPlayer();
                return;
            }
            Tile target = HabitatRules.TileAt(State,State.playerX+dx,State.playerZ+dz);
            bool pushing = !climb && target != null && target.prop == PropKind.Crate;
            BeginGridAction(s => HabitatRules.MovePlayer(s,dx,dz,climb,tuning),
                pushing ? HabitatPlayerMotion.Push : climb ? HabitatPlayerMotion.Climb : HabitatPlayerMotion.Walk,
                Math.Abs(dx)+Math.Abs(dz)==1);
        }
        public void Pull(int dx, int dz)
        {
            if (Busy) return;
            if (State.onHarbor) { State.message = "先沿木桥回到岛上，再拉动箱子。"; return; }
            int x=State.playerX-dx,z=State.playerZ-dz;
            if (HabitatRules.IsRecoveryTarget(State,x,z)) { BeginRecovery(x,z); return; }
            BeginGridAction(s => HabitatRules.PullPlayer(s,dx,dz),HabitatPlayerMotion.Pull,Math.Abs(dx)+Math.Abs(dz)==1);
        }

        static void SelectFacingCell(HabitatState state)
        {
            // Keep an off-board target off-board; never reuse an older valid mouse target.
            state.selectedX=state.playerX+state.facingX;
            state.selectedZ=state.playerZ+state.facingZ;
        }

        void BeginGridAction(Func<HabitatState,bool> action,HabitatPlayerMotion motion,bool selectFacingOnFailure=false)
        {
            if (Busy) return;
            var candidate=HabitatRules.Clone(State);
            if (!action(candidate))
            {
                State.facingX=candidate.facingX; State.facingZ=candidate.facingZ;
                if(selectFacingOnFailure)SelectFacingCell(State);
                State.message=candidate.message; State.events=candidate.events;
                HabitatAudio.Play(HabitatAudioCue.Error);
                return;
            }
            SelectFacingCell(candidate);
            if(HabitatRules.TileAt(State,State.playerX,State.playerZ)?.prop==PropKind.Raft
                &&HabitatRules.TileAt(candidate,candidate.playerX,candidate.playerZ)?.prop==PropKind.Raft
                &&HabitatRules.TileAt(candidate,State.playerX,State.playerZ)?.prop!=PropKind.Raft)
                motion=HabitatPlayerMotion.Ride;
            pendingBefore=HabitatRules.Clone(State); pendingState=candidate;
            pendingMotion=motion;
            pendingSimulation=false;
            // State remains at the source until BOTH character and moved props arrive.
            View.BeginGridStep(State,candidate,motion);
        }

        void CommitPending()
        {
            HabitatState before=pendingSimulation?State:pendingBefore;
            if(!pendingSimulation) { PushHistory(pendingBefore); ActionCount++; }
            State=pendingState;
            if(pendingLure){HabitatAudio.PlayAction(HabitatActionCue.Lure);pendingLure=false;}
            // Only the arrived, authoritative state may produce action audio.
            // Candidate Tick and cancelled movement remain completely silent.
            if(!pendingSimulation && before!=null)
            {
                if(before.playerX!=State.playerX||before.playerZ!=State.playerZ)
                    HabitatAudio.PlayAction(FootstepFor(State));
                if((pendingMotion==HabitatPlayerMotion.Push||pendingMotion==HabitatPlayerMotion.Pull)
                    && MovedBox(before,State)) HabitatAudio.PlayAction(HabitatActionCue.Push);
            }
            if(before!=null) PlayEcologyChange(before.waterLevel,before.shrimp);
        }

        static HabitatActionCue FootstepFor(HabitatState state)
        {
            if(state.onHarbor)return HabitatActionCue.StepWood;
            Tile tile=HabitatRules.TileAt(state,state.playerX,state.playerZ);
            if(tile!=null&&(tile.prop==PropKind.Crate||tile.prop==PropKind.Float||tile.prop==PropKind.Raft))
                return HabitatActionCue.StepWood;
            if(tile!=null&&tile.prop!=PropKind.ClimbBlock&&tile.prop!=PropKind.Climb&&tile.height<=1)
                return HabitatActionCue.StepSand;
            return HabitatActionCue.StepGrass;
        }

        static bool MovedBox(HabitatState before,HabitatState after)
        {
            if(before.tiles==null||after.tiles==null||before.tiles.Length!=after.tiles.Length)return false;
            for(int i=0;i<before.tiles.Length;i++)
            {
                PropKind oldProp=before.tiles[i].prop,newProp=after.tiles[i].prop;
                bool wasBox=oldProp==PropKind.Crate||oldProp==PropKind.Float||oldProp==PropKind.ClimbBlock;
                bool isBox=newProp==PropKind.Crate||newProp==PropKind.Float||newProp==PropKind.ClimbBlock;
                // A tide-only Crate -> Float conversion has not moved a box.
                if(wasBox!=isBox)return true;
            }
            return false;
        }

        void PlayEcologyChange(int priorWater,int priorShrimp)
        {
            if(State.waterLevel!=priorWater)HabitatAudio.PlayAction(HabitatActionCue.Tide);
            if(State.shrimp>priorShrimp)HabitatAudio.PlayAction(HabitatActionCue.Harvest);
        }

        void TickCommitted(float seconds)
        {
            int priorWater=State.waterLevel,priorShrimp=State.shrimp;
            HabitatRules.Tick(State,tuning,seconds);
            PlayEcologyChange(priorWater,priorShrimp);
        }

        bool RidingFloat()
        { return !State.onHarbor && HabitatRules.TileAt(State,State.playerX,State.playerZ)?.prop==PropKind.Float; }

        void AdvanceSimulation(float seconds)
        {
            if(seconds<=0f||float.IsNaN(seconds)||float.IsInfinity(seconds))return;
            // Keep normal ecology updates on the existing object: replacing State
            // every frame would make the view snap/rebuild instead of interpolate.
            if(!RidingFloat()) { TickCommitted(seconds); return; }
            simulationStepsRemaining=Math.Max(1,(int)Math.Ceiling(seconds/.1f));
            simulationStepSeconds=seconds/simulationStepsRemaining;
            ContinueSimulation();
        }

        void ContinueSimulation()
        {
            while(simulationStepsRemaining>0)
            {
                simulationStepsRemaining--;
                float dt=simulationStepSeconds;
                // A Model.Tick substep can trigger at most one wind movement.
                // Stop at that movement before advancing later ticks, including
                // the rest of a manual one-second Step or a fast simulation frame.
                bool windDue=State.movementClock+dt>=Math.Max(.2f,tuning.moveSeconds);
                if(RidingFloat()&&windDue)
                {
                    var candidate=HabitatRules.Clone(State);
                    HabitatRules.Tick(candidate,tuning,dt);
                    if(candidate.playerX!=State.playerX||candidate.playerZ!=State.playerZ)
                    {
                        SelectFacingCell(candidate);
                        pendingBefore=null;pendingState=candidate;pendingSimulation=true;
                        View.BeginGridStep(State,candidate,HabitatPlayerMotion.Ride);
                        return;
                    }
                    // A blocked wind preview is deterministic and has no source
                    // side effects; apply that same tick normally to retain refs.
                }
                TickCommitted(dt);
            }
            simulationStepSeconds=0f;
        }

        void BeginRecovery(int x,int z)
        {
            if (Busy) return;
            var candidate=HabitatRules.Clone(State);
            if (!HabitatRules.RecoverBox(candidate,x,z))
            { State.message=candidate.message; State.events=candidate.events; HabitatAudio.Play(HabitatAudioCue.Error); return; }
            SelectFacingCell(candidate);
            if(!View || !View.BeginRecoveryPresentation(State,candidate,x,z))
            { State.message="打捞动作资源尚未就绪，请稍后再试。";HabitatAudio.Play(HabitatAudioCue.Error);return; }
            pendingBefore=HabitatRules.Clone(State); pendingState=candidate;
            pendingMotion=HabitatPlayerMotion.Pull;
            pendingSimulation=false;
            recovering=true;
        }

        void AdvanceRecovery()
        {
            if(!View || !View.RecoveryPresentationValid || pendingState==null || pendingBefore==null)
            { CancelPendingAction();return; }
            if(!View.RecoveryPresentationComplete)return;
            CommitPending();
            recovering=false;pendingState=pendingBefore=null;pendingSimulation=false;
            View.EndRecoveryPresentation(false);
            View.Render(State,tuning,overlays);
        }

        public void CancelPendingAction()
        {
            ClearBufferedInput(); pendingLure=false;
            FinishFishingCatch();
            FinishChestPresentation();
            FinishUtilityPresentation();
            HabitatAudio.ClearTransient(); harborStepDistance=0f;
            ResetChapterPresentation();
            CancelJourneySequence();
            if (View && View.PlayerVisual) View.PlayerVisual.CancelEquipmentAction();
            pendingState=pendingBefore=null; recovering=pendingSimulation=false;
            simulationStepsRemaining=0;simulationStepSeconds=0f;
            if (View) View.EndRecoveryPresentation(true);
            if (View) View.CancelGridStep();
        }

        public bool MoveHarborByScreen(Vector2 axes, float seconds, Quaternion cameraRotation)
            => MoveHarbor(() => View.Harbor.MoveByScreen(State, axes, cameraRotation, seconds));

        bool MoveHarbor(Func<bool> movement)
        {
            if (Busy || !State.onHarbor || View == null || Hud.ModalOpen || (Fishing != null && Fishing.IsOpen)) return false;
            // Only entering/leaving creates an undo step; continuous walking does not
            // fill the history with a snapshot on every frame.
            HabitatState beforeExit = State.harborZ > -.4f ? HabitatRules.Clone(State) : null;
            Vector3 beforePosition=View.Harbor.WorldPosition(State);
            bool moved = movement();
            if (beforeExit != null && !State.onHarbor) { PushHistory(beforeExit); ActionCount++; }
            if(moved)
            {
                Vector3 travelled=View.Harbor.WorldPosition(State)-beforePosition;travelled.y=0f;
                if(!State.onHarbor)
                { harborStepDistance=0f; HabitatAudio.PlayAction(FootstepFor(State)); }
                else
                {
                    harborStepDistance+=travelled.magnitude;
                    if(harborStepDistance>=.65f)
                    { harborStepDistance%=.65f; HabitatAudio.PlayAction(HabitatActionCue.StepWood); }
                }
            }
            return moved;
        }
        public void OpenHarbor()
        {
            // The normal game now has one harbor, one inventory and one player.
            ReturnToHarbor();
        }

        public void OpenHarborPrototype()
        {
            if (Busy) return;
            const string harbor = "HarborPrototype";
            if (!Application.CanStreamedLevelBeLoaded(harbor))
            {
                State.message = "码头场景尚未加入场景列表，请先运行码头原型设置。";
                return;
            }
            Hud.ClosePanels();
            harborReturn = CaptureSnapshot();
            harborReturnPaused = paused;
            HabitatAudio.ClearTransient();
            UnityEngine.SceneManagement.SceneManager.LoadScene(harbor);
        }
        public static void TravelToLevel(int index)
        {
            if (index<0 || index>=HabitatLevels.Names.Length) return;
            if (Instance) { Instance.SailToLevel(index); return; }
            if (!Application.CanStreamedLevelBeLoaded("HabitatIsland")) return;
            harborReturn=null; requestedLevel=index;
            HabitatAudio.ClearTransient();
            UnityEngine.SceneManagement.SceneManager.LoadScene("HabitatIsland");
        }
        public void GetTarget(out int x, out int z)
        {
            x = State.selectedX; z = State.selectedZ;
            if (!HabitatRules.Inside(State, x, z)) { x = State.playerX + State.facingX; z = State.playerZ + State.facingZ; }
        }
        public bool TryNearbyCatch(out int x,out int z)
        {
            x=z=-1;
            if(State==null||State.onHarbor||Busy||StoryPlaying||Fishing&&Fishing.IsOpen||State.carryingProp!=PropKind.None||State.carryingAnimal>=0||tool!=0&&tool!=4)return false;
            GetTarget(out int selectedX,out int selectedZ);
            // An explicit nearby interaction takes priority over the contextual fishing shortcut.
            if(tool==0&&Math.Abs(selectedX-State.playerX)+Math.Abs(selectedZ-State.playerZ)==1)
            {
                var selectedTile=HabitatRules.TileAt(State,selectedX,selectedZ);
                if(selectedTile!=null&&(selectedTile.prop!=PropKind.None&&selectedTile.prop!=PropKind.Road
                    ||State.animals.Exists(a=>!a.carried&&a.x==selectedX&&a.z==selectedZ&&a.species!=Species.Goldfish)))return false;
            }
            Animal nearest=null;int best=int.MaxValue;
            foreach(var a in State.animals)
            {
                if(a.species!=Species.Goldfish&&a.species!=Species.Jellyfish)continue;
                int distance=Math.Abs(a.x-State.playerX)+Math.Abs(a.z-State.playerZ);
                bool selected=a.x==selectedX&&a.z==selectedZ;
                if(!selected&&distance>1||!HabitatRules.CanHarvest(State,a,tuning,out _))continue;
                int score=selected?-1:distance;
                if(score<best){nearest=a;best=score;}
            }
            if(nearest==null)return false;x=nearest.x;z=nearest.z;return true;
        }
        public void ApplyTool()
        {
            if (Busy) return;
            GetTarget(out int raftX,out int raftZ);
            if(tool==0&&!State.onHarbor&&State.carryingProp==PropKind.None&&State.carryingAnimal<0
                &&HabitatRules.RaftDirection(HabitatRules.TileAt(State,State.playerX,State.playerZ),raftX-State.playerX,raftZ-State.playerZ)
                &&HabitatRules.TileAt(State,raftX,raftZ)?.prop==PropKind.None
                &&!State.animals.Exists(a=>!a.carried&&a.x==raftX&&a.z==raftZ)
                &&HabitatRules.IsWater(State,raftX,raftZ))
            {Move(raftX-State.playerX,raftZ-State.playerZ);return;}
            if(TryNearbyCatch(out int fishX,out int fishZ))
            {Select(fishX,fishZ);tool=4;if(!Fishing.Begin(this,fishX,fishZ))HabitatAudio.Play(HabitatAudioCue.Error);return;}
            if(tool>=5&&!State.onHarbor){Hud.OpenUtilityPreview();return;}
            if (State.onHarbor)
            {
                var nearby = View.Harbor.NearestInteraction(State);
                if (nearby == HabitatDockInteraction.Boat) InteractWithBoat();
                else if (nearby == HabitatDockInteraction.Shop) OpenShopInteraction();
                else if (nearby == HabitatDockInteraction.Home) Hud.OpenHome();
                else State.message = "靠近家按 E 休息；靠近商店交易；靠近小船选择目的地。";
                return;
            }
            GetTarget(out int x, out int z);
            if (tool==0 && State.carryingProp==PropKind.None && State.carryingAnimal<0
                && HabitatRules.TileAt(State,x,z)?.prop==PropKind.Chest
                && !State.animals.Exists(a=>!a.carried&&a.x==x&&a.z==z))
            { BeginChestAction(x,z); return; }
            if(tool==0&&State.carryingProp==PropKind.None&&State.carryingAnimal<0&&HabitatRules.IsBox(HabitatRules.TileAt(State,x,z)?.prop??PropKind.None))
            {BeginGridAction(s=>HabitatRules.PushBox(s,x,z),HabitatPlayerMotion.Push);return;}
            switch (tool)
            {
                case 0:
                    bool placingSoil = State.carryingProp == PropKind.Climb;
                    if (Mutate(() => HabitatRules.Interact(State, x, z)) && placingSoil)
                        PlayToolVisual("Dig", x, z);
                    break;
                case 1:
                    if (Mutate(() => HabitatRules.PlaceSoil(State, x, z),HabitatActionCue.Soil)) PlayToolVisual("Dig", x, z);
                    break;
                case 2: BeginLureAction(x,z); break;
                case 3: Mutate(() => HabitatRules.PlaceTrap(State, x, z),HabitatActionCue.Trap); break;
                case 4:
                    if (State.animals.Exists(a => !a.carried && a.x == x && a.z == z && (a.species == Species.Goldfish || a.species == Species.Jellyfish)))
                    {if(!Fishing.Begin(this,x,z))HabitatAudio.Play(HabitatAudioCue.Error);}
                    else if (Mutate(() => HabitatRules.Fish(State, x, z),HabitatActionCue.Harvest)) PlayToolVisual("Hook_Pull", x, z);
                    break;
            }
        }
        public void CompleteFishing(int x,int z,Species species=Species.Goldfish,int expectedId=-1)
        {
            if(Busy || expectedId>=0&&!State.animals.Exists(a=>a.id==expectedId&&a.x==x&&a.z==z&&a.species==species))return;
            BeginFishingCatch(x,z,species);
        }

        // Successful gameplay events notify presentation without delaying or repeating settlement.
        public void PlayToolVisual(string action, int x, int z)
        {
            if (!View || !View.PlayerVisual || State == null) return;
            View.PlayerVisual.RefreshEquipment(State, tool, StoryPlaying || State.onHarbor,
                Fishing != null && Fishing.IsOpen);
            float surface = Mathf.Max(HabitatRules.WaterSurfaceY(State.waterLevel) + .045f, HabitatRules.Height(State, x, z) * .7f + .10f);
            View.PlayerVisual.PlayToolAction(action, new Vector3(x, surface, z));
        }
        public void RotateTarget() { if (Busy || State.onHarbor) return; GetTarget(out int x, out int z); Mutate(() => HabitatRules.Rotate(State, x, z),HabitatActionCue.Rotate); }
        public void Buy(string item)
        {
            if (Busy) return;
            if (!CanPurchaseHere || item=="sell"&&!View.Harbor.NearShop(State)) { State.message = "请先走到码头商店旁。"; return; }
            Mutate(() => HabitatRules.Shop(State, item),HabitatActionCue.Trade);
        }
        public void TogglePause() { if (!Busy) paused = !paused; }
        public void CycleSpeed() { if (!Busy) speed = speed < 1.5f ? 2 : speed < 3 ? 4 : 1; }
        public void Step() { if (Busy) return; PushHistory(HabitatRules.Clone(State)); paused = true; AdvanceSimulation(1f); }
        public void Undo()
        {
            if (Busy) return;
            HabitatAudio.ClearTransient(); harborStepDistance=0f;
            if (history.Count == 0) { State.message = "还没有可撤销的操作。"; return; }
            if (View && View.PlayerVisual) View.PlayerVisual.CancelEquipmentAction();
            State = history.Pop(); paused = true;
            ClearBufferedInput();
            if(history.Count<retryHistoryDepth)CaptureRetryPoint();
            State.message = "已恢复上一步，模拟已暂停；按空格继续。";
        }
        public void LoadLevel(int index)
        {
            CancelPendingAction();
            if (Fishing != null) Fishing.Cancel(false);
            if (Hud != null) Hud.ClosePanels();
            State = HabitatLevels.Create(index); history.Clear(); ActionCount = 0; paused = index == 0 || index == 1 || State.authoredLevel;
            visitedLevels.RemoveAll(level => level.index == State.levelIndex);
            if (View != null) View.Initialize(State);
            CaptureRetryPoint();
        }
        bool Mutate(Func<bool> action,HabitatActionCue? actionCue=null)
        {
            if (Busy) return false;
            var before = HabitatRules.Clone(State);
            bool success = action();
            if (success)
            {
                PushHistory(before); ActionCount++;
                if(State.openedChests>before.openedChests)HabitatAudio.PlayAction(HabitatActionCue.Chest);
                else if(actionCue==HabitatActionCue.Trade&&State.coins==before.coins
                    &&before.carryingProp==PropKind.None&&State.carryingProp!=PropKind.None)
                    HabitatAudio.PlayAction(HabitatActionCue.Pick);
                else if(actionCue.HasValue)HabitatAudio.PlayAction(actionCue.Value);
                else if(before.carryingProp==PropKind.Climb&&State.carryingProp==PropKind.None)
                    HabitatAudio.PlayAction(HabitatActionCue.Soil);
                else if(before.carryingAnimal>=0&&State.carryingAnimal<0
                    ||before.carryingProp!=PropKind.None&&State.carryingProp==PropKind.None)
                    HabitatAudio.PlayAction(HabitatActionCue.Drop);
                else if(before.carryingAnimal<0&&State.carryingAnimal>=0
                    ||before.carryingProp==PropKind.None&&State.carryingProp!=PropKind.None||State.shells>before.shells)
                    HabitatAudio.PlayAction(HabitatActionCue.Pick);
                else HabitatAudio.Play(HabitatAudioCue.Confirm);
                if(State.waterLevel!=before.waterLevel)HabitatAudio.PlayAction(HabitatActionCue.Tide);
                if(actionCue==HabitatActionCue.Trade)CaptureRetryPoint();
            }
            else HabitatAudio.Play(HabitatAudioCue.Error);
            return success;
        }
        [Serializable] class Snapshot
        {
            public string version = "HabitatIsland/1";
            public HabitatState state; public Tuning tuning; public int actions;
            public List<VisitedLevel> visitedLevels;
            public RetryPoint retryPoint;
        }
        public bool SaveSession()
        {
            if (Busy || Fishing && Fishing.IsOpen) { State.message = "请等当前动作或片段结束后再保存。"; HabitatAudio.Play(HabitatAudioCue.Error); return false; }
            try
            {
                string folder = SaveSlotDirectory;
                Directory.CreateDirectory(folder);
                string json = JsonUtility.ToJson(CaptureSnapshot(), true);
                string path = Path.Combine(folder, "playtest-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json");
                File.WriteAllText(path, json);
                WriteLatestSave(Path.Combine(folder, "latest.json"), json);
                LastExport = path; State.message = "已保存测试记录：" + path;
                HabitatAudio.Play(HabitatAudioCue.Confirm);
                return true;
            }
            catch (Exception e) { State.message = "保存失败：" + e.Message; HabitatAudio.Play(HabitatAudioCue.Error); return false; }
        }
        public bool LoadSession()
        {
            HabitatAudio.ClearTransient();
            try
            {
                string path = LatestAvailablePath();
                if (!SaveFileExists(path)) { State.message = "还没有保存的测试记录。"; HabitatAudio.Play(HabitatAudioCue.Error); return false; }
                var saved = ReadSaveWithBackup(path,out bool recovered);
                RestoreSnapshot(saved);
                if(recovered)State.message="主存档不可用，已恢复上一份有效备份。原文件保留。";
                SessionStarted=true;
                HabitatAudio.Play(HabitatAudioCue.Confirm);
                return true;
            }
            catch (Exception e) { State.message = "读取失败：" + e.Message; HabitatAudio.Play(HabitatAudioCue.Error); return false; }
        }
        public static void WriteLatestSave(string path,string json)
        {
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllText(temporary,json);
                if(File.Exists(path))File.Replace(temporary,path,path+".bak");
                else File.Move(temporary,path);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        void OnDestroy()
        {
            DisposeChapter();
            DisposeTutorial();
            if (Story) { Story.Completed -= CompleteJourneySequence; Story.Cancel(); }
            if (Instance == this) Instance = null;
        }
    }
}
