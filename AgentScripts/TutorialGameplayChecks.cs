using System;
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
using UnityEngine.UI;

// RunEntry, then RunLesson, serially in the same Play session. RunEntry deliberately
// leaves a temporary fixture on the tutorial boat. Restore is the emergency exit.
// AppDomain data survives run_script compiling each entry point into a new assembly.
// Never writes saves, scenes or assets. Screenshots/reports go under Logs only.
public static class TutorialGameplayChecks
{
    const string SessionKey="Habitat.TutorialGameplayChecks.20261016";
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly string Folder=Path.GetFullPath("Logs/tutorial-20261016");
    static Dictionary<string,object> Session => AppDomain.CurrentDomain.GetData(SessionKey) as Dictionary<string,object>;
    static HabitatGame Game => (HabitatGame)Session["game"];
    static StringBuilder Report => (StringBuilder)Session["report"];
    static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,F).GetValue(o);
    static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,F).Invoke(o,args);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,F).SetValue(o,value);
    static string Json(object o)=>JsonUtility.ToJson(o);
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static void Check(bool condition,string message)
    {
        int count=(int)Session["count"]+1;Session["count"]=count;
        Report.AppendLine((condition?"PASS ":"FAIL ")+count+": "+message);Require(condition,message);
    }
    static Dictionary<FieldInfo,object> Scalars(object o)=>o.GetType().GetFields(F)
        .Where(f=>!f.IsInitOnly&&!f.IsStatic&&(f.FieldType.IsPrimitive||f.FieldType.IsEnum||f.FieldType==typeof(string)))
        .ToDictionary(f=>f,f=>f.GetValue(o));
    static void PutScalars(object o,Dictionary<FieldInfo,object> saved)
    {foreach(var item in saved)item.Key.SetValue(o,item.Value);}
    static Stack<HabitatState> History(HabitatGame game)=>Get<Stack<HabitatState>>(game,"history");
    static List<HabitatGame.VisitedLevel> Visits(HabitatGame game)=>Get<List<HabitatGame.VisitedLevel>>(game,"visitedLevels");
    static string VisitsJson(IEnumerable<HabitatGame.VisitedLevel> visits)
        =>string.Join("|",visits.Select(v=>v.index+":"+v.actions+":"+Json(v.state)));
    static string Inventory(HabitatState s)=>string.Join("|",s.coins,s.cargo,s.fish,s.shrimp,s.rod,s.traps,s.day,
        s.deliveredFish,s.deliveredShrimp,s.deliveryCount,s.deliveryIncome,s.openingCompleted,s.openingCatchGranted);
    static string Map(HabitatState s)=>s.width+"x"+s.depth+":"+string.Join("|",s.tiles.Select(Json))+"/"+string.Join("|",s.animals.Select(Json));
    static string Flags(HabitatState s)=>string.Join("|",s.tutorialOfferPending,s.tutorialCompleted,s.tutorialGuideDismissed,
        s.tutorialObservedCrab,s.tutorialObservedHabitat);
    static Animal Crab(HabitatGame game)=>game.State.animals.First(a=>a.species==Species.Crab&&!a.carried);
    static Animal Shrimp(HabitatGame game)=>game.State.animals.First(a=>a.species==Species.Shrimp);
    static Vector3 Flat(Vector3 value)=>new Vector3(value.x,0,value.z);
    static void Refresh(){Set(Game.Hud,"nextRefresh",0f);Game.Hud.Refresh();}
    static Button Button(string name)
    {
        Refresh();var found=Game.Hud.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name==name);
        Require(found,"Missing real HUD button: "+name);return found;
    }
    static async Task Click(string name)
    {
        var button=Button(name);Require(button.gameObject.activeInHierarchy&&button.interactable,"HUD button unavailable: "+name);
        Require(EventSystem.current,"The live HUD has no EventSystem.");
        ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        await Frames(2);Refresh();
    }
    static async Task Frames(int count)
    {
        int start=Time.frameCount;double deadline=UnityEditor.EditorApplication.timeSinceStartup+10;
        while(Time.frameCount-start<count)
        {Require(Application.isPlaying&&UnityEditor.EditorApplication.timeSinceStartup<deadline,"Real Play frames stopped.");await Task.Delay(12);}
    }
    static async Task Until(Func<bool> condition,float timeout,string label)
    {
        double deadline=UnityEditor.EditorApplication.timeSinceStartup+timeout;
        while(!condition()){Require(UnityEditor.EditorApplication.timeSinceStartup<deadline,"Timed out: "+label);await Frames(1);}
    }
    static async Task Shot(string name)
    {
        Refresh();await Frames(2);Directory.CreateDirectory(Folder);
        string path=Path.Combine(Folder,name+".png");ScreenCapture.CaptureScreenshot(path);await Frames(3);Report.AppendLine("SCREENSHOT "+path);
    }
    static void Write(string name){Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,name),Report.ToString());}
    static void StartSession(HabitatGame game)
    {
        Require(Session==null,"A tutorial fixture already exists. Continue RunLesson or call Restore first.");
        Require(Application.isPlaying&&game&&game.State!=null&&game.State.levelIndex==HabitatFigJamLevel.Index
            &&!game.Busy&&!game.Hud.ModalOpen&&!game.Fishing.IsOpen&&game.Story&&game.View.Harbor.Ready,
            "Use the idle formal island, after opening, with all panels closed.");
        string[] cameraFields={"cameraYaw","cameraSize","cameraTarget","cameraTargetSmooth","followPlayer","overviewCamera","overviewSize","framingAspect","framingDirty"};
        var s=new Dictionary<string,object>{
            ["game"]=game,["original"]=game.State,["originalJson"]=Json(game.State),["tuning"]=game.tuning,
            ["undo"]=History(game).ToArray(),["visits"]=Visits(game).ToArray(),["visitsJson"]=VisitsJson(Visits(game)),
            ["gameScalars"]=Scalars(game),["hudScalars"]=Scalars(game.Hud),
            ["ui"]=game.Hud.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.gameObject,t=>t.gameObject.activeSelf),
            ["camera"]=cameraFields.ToDictionary(n=>n,n=>typeof(HabitatView).GetField(n,F).GetValue(game.View)),
            ["cameraPosition"]=game.View.GameCamera.transform.position,["cameraRotation"]=game.View.GameCamera.transform.rotation,
            ["cameraSize"]=game.View.GameCamera.orthographicSize,["selected"]=EventSystem.current?EventSystem.current.currentSelectedGameObject:null,
            ["oldKeyboard"]=Keyboard.current,["oldMouse"]=Mouse.current,
            ["background"]=InputSystem.settings.backgroundBehavior,["editorInput"]=InputSystem.settings.editorInputBehaviorInPlayMode,
            ["report"]=new StringBuilder(),["count"]=0,["stage"]="entry",
            ["timeScale"]=Time.timeScale
        };
        AppDomain.CurrentDomain.SetData(SessionKey,s);
        var keyboard=InputSystem.AddDevice<Keyboard>("Tutorial acceptance keyboard");s["keyboard"]=keyboard;
        var mouse=InputSystem.AddDevice<Mouse>("Tutorial acceptance mouse");s["mouse"]=mouse;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.EnableDevice(keyboard);InputSystem.EnableDevice(mouse);keyboard.MakeCurrent();mouse.MakeCurrent();
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
    }
    public static async Task<string> RunEntry()
    {
        StartSession(HabitatGame.Instance);
        try
        {
            var game=Game;game.CancelPendingAction();game.Hud.ClosePanels();History(game).Clear();Visits(game).Clear();
            game.State=HabitatLevels.Create(HabitatFigJamLevel.Index);game.tuning=new Tuning();
            game.paused=true;game.speed=1;game.tool=0;game.ActionCount=0;Time.timeScale=1;
            game.View.Initialize(game.State);await Frames(3);
            Report.AppendLine("Fixture: fresh formal island; all default Tuning values, animalMovement=true, breeding=18 seconds, speed 1. No manual Tick or save writes.");
            Check(Mathf.Abs(game.tuning.breedSeconds-18f)<.001f&&game.tuning.animalMovement,"fixture retains default movement and the 18-second breeding time");
            Check(game.BeginOpening()&&game.StoryPlaying&&game.Busy,"fresh formal game starts the actual opening Timeline");
            Refresh();Check(!game.Hud.TutorialCardVisible&&!game.State.tutorialOfferPending,"recommendation stays hidden until the opening completes");
            await Until(()=>!game.StoryPlaying,23,"natural opening completion");await Frames(2);Refresh();
            Check(game.State.openingCompleted&&game.State.tutorialOfferPending&&!game.State.tutorialCompleted&&game.Hud.TutorialCardVisible,
                "natural opening completion reveals the formal-island tutorial recommendation");
            Session["formalMap"]=Map(game.State);Session["entryInventory"]=Inventory(game.State);
            await Shot("01-formal-recommendation");
            var formal=game.State;await Click("Tutorial primary action");
            var selector=Get<HabitatMapSelectionPanel>(game.Hud,"mapSelection");
            Check(ReferenceEquals(formal,game.State)&&game.State.levelIndex==HabitatFigJamLevel.Index&&game.Hud.MapSelectionOpen
                &&selector.SelectedLevel==0&&game.View.Harbor.NearBoat(game.State),
                "real tutorial button opens the boat selector preselected at 0 without sailing");
            Check(Inventory(game.State)==(string)Session["entryInventory"]&&Map(game.State)==(string)Session["formalMap"],
                "opening the selector preserves the formal map and inventory");
            await Shot("02-tutorial-destination");await Click("Confirm destination");
            Check(game.State.levelIndex==0&&game.State.onHarbor&&!game.Hud.ModalOpen&&game.View.Harbor.NearBoat(game.State)
                &&game.HasVisitedLevel(HabitatFigJamLevel.Index),"real confirmation sails to tutorial 0 and retains the formal-island cache");
            Check(Inventory(game.State)==(string)Session["entryInventory"]&&!game.StoryPlaying&&game.paused,
                "tutorial arrival preserves inventory without awarding a second opening catch");
            Check(game.TutorialStep==HabitatTutorialStep.Arrival&&game.Hud.TutorialCardVisible&&game.TutorialMarkerVisible,
                "tutorial arrival shows a walking instruction and an actual shore marker");
            Session["stage"]="lesson";Report.AppendLine("CHECKPOINT: RunEntry passed; temporary tutorial boat fixture retained. Run RunLesson next, or Restore to cancel.");
            Write("entry-checks.txt");return Report.ToString();
        }
        catch(Exception error)
        {
            Report.AppendLine("ERROR "+error);string result=Restore();throw new InvalidOperationException(result,error);
        }
    }
    static async Task WalkTo(Vector3 goal,string label)
    {
        var game=Game;int samples=0,stalled=0;double deadline=UnityEditor.EditorApplication.timeSinceStartup+16;
        while(game.State.onHarbor)
        {
            Vector3 before=game.View.Harbor.WorldPosition(game.State),delta=Flat(goal-before);
            if(delta.magnitude<.025f)break;
            Require(++samples<1000&&UnityEditor.EditorApplication.timeSinceStartup<deadline,"Walk exceeded its sample/deadline limit: "+label);
            float dt=Mathf.Min(Mathf.Max(.001f,Time.unscaledDeltaTime),.04f,delta.magnitude/2.5f);
            game.View.Harbor.MoveWorld(game.State,delta,dt);await Frames(1);
            if(!game.State.onHarbor)break;
            Vector3 after=game.View.Harbor.WorldPosition(game.State);float moved=Flat(after-before).magnitude;
            Require(game.View.Harbor.IsWalkable(after),"No actual deck/plank support: "+label);
            Require(moved<=2.5f*dt+.004f,"Unexpected teleport: "+label);
            stalled=moved<.0005f?stalled+1:0;
            Require(stalled<30,"Harbor walk blocked at "+after+" toward "+goal+" ("+label+")");
        }
        Check(true,label+" traversed with "+samples+" collision-supported small steps");
    }
    static async Task Step(int x,int z)
    {
        int px=Game.State.playerX,pz=Game.State.playerZ;Game.Move(x,z);
        Require(Game.Busy,"Requested legal step did not begin: "+px+","+pz+" + "+x+","+z+"; "+Game.State.message);
        await Until(()=>!Game.Busy,4,"grid arrival");await Frames(1);
        Require(Game.State.playerX==px+x&&Game.State.playerZ==pz+z,"Grid step did not commit its destination.");
    }
    static async Task SnapshotRoundtrip(string label)
    {
        var game=Game;string flags=Flags(game.State),map=Map(game.State),inventory=Inventory(game.State);
        object snapshot=Call(game,"CaptureSnapshot");string json=Json(snapshot);
        object copy=JsonUtility.FromJson(json,snapshot.GetType());
        var savedState=Get<HabitatState>(copy,"state");
        Check(Flags(savedState)==flags&&json.Contains("tutorialObservedHabitat")&&json.Contains("tutorialGuideDismissed"),label+" serializes all tutorial fields");
        Call(game,"RestoreSnapshot",copy);await Frames(2);Refresh();
        Check(Flags(game.State)==flags&&Map(game.State)==map&&Inventory(game.State)==inventory&&game.HasVisitedLevel(HabitatFigJamLevel.Index),
            label+" restores tutorial, island cache and inventory in memory without saving");
    }
    public static async Task<string> RunLesson()
    {
        Require(Session!=null&&(string)Session["stage"]=="lesson","RunEntry must complete in this Play session first.");
        Exception failure=null;
        try
        {
            var game=Game;Require(game.State.levelIndex==0&&game.State.onHarbor&&!game.Busy&&!game.Hud.ModalOpen,"Tutorial arrival checkpoint was changed.");
            var harbor=game.View.Harbor;var dock=Get<Transform>(harbor,"dock");
            Vector3 shore=Get<Vector3>(harbor,"shore"),bridgeEnd=Get<Vector3>(harbor,"bridgeEnd");
            await WalkTo(dock.TransformPoint(bridgeEnd),"boat to wooden bridge");
            await WalkTo(dock.TransformPoint(shore),"actual bridge to shore");
            await WalkTo(dock.TransformPoint(new Vector3(0,shore.y,.25f)),"shore exit");await Frames(2);Refresh();
            Check(!game.State.onHarbor&&game.State.playerX==game.State.dockX&&game.State.playerZ==game.State.dockZ
                &&game.TutorialStep==HabitatTutorialStep.ObserveCrab,"walking across the real bridge enters the island without editing onHarbor");
            await Shot("03-walked-ashore");
            await Click("Dismiss tutorial guide");
            Check(game.State.tutorialGuideDismissed&&!game.Hud.TutorialCardVisible&&!game.TutorialMarkerVisible,"dismiss button hides the guide and marker");
            await Click("Open Help");await Click("Show tutorial guide");
            Check(!game.State.tutorialGuideDismissed&&!game.Hud.ModalOpen&&game.Hud.TutorialCardVisible&&game.TutorialMarkerVisible,
                "real Help button restores the current tutorial step and target");
            await Click("Reveal tutorial target");
            Check(!game.State.tutorialObservedCrab&&game.TutorialStep==HabitatTutorialStep.ObserveCrab&&game.State.carryingAnimal<0,
                "finding the target only moves the camera; it does not observe or pick up an animal");
            // Distinct shared values ensure a reset cannot silently replace earned inventory
            // with the level's defaults. This changes only the temporary test fixture.
            game.State.day=6;game.State.fish=7;game.State.shrimp=5;game.State.coins=47;game.State.cargo=1;game.State.traps=1;
            string resetInventory=Inventory(game.State),formalCache=Map(Visits(game).First(v=>v.index==HabitatFigJamLevel.Index).state);
            game.RestartCurrentLevel();await Frames(2);Refresh();
            Check(game.State.levelIndex==0&&Inventory(game.State)==resetInventory&&game.HasVisitedLevel(HabitatFigJamLevel.Index)
                &&Map(Visits(game).First(v=>v.index==HabitatFigJamLevel.Index).state)==formalCache&&game.UndoCount==0
                &&game.TutorialStep==HabitatTutorialStep.ObserveCrab&&!game.State.tutorialObservedCrab&&!game.State.tutorialGuideDismissed,
                "tutorial restart resets local lesson/undo while preserving earned inventory, day and the formal-island cache");
            int crabX=Crab(game).x,crabZ=Crab(game).z;game.Move(0,-1);
            Check(game.Busy,"legal shore step begins a transaction before observation");
            game.Select(crabX,crabZ);game.ApplyTool();game.DismissTutorial();game.OpenTutorialDestination(4);await Frames(1);
            Check(game.Busy&&!game.State.tutorialObservedCrab&&game.TutorialStep==HabitatTutorialStep.ObserveCrab
                &&game.State.carryingAnimal<0&&!game.State.tutorialGuideDismissed&&game.State.levelIndex==0&&!game.Hud.ModalOpen,
                "busy movement rejects selection, tools, dismissal and travel without advancing tutorial progress");
            await Until(()=>!game.Busy,4,"busy observation step finishes");
            game.Select(crabX,crabZ);await Frames(2);Refresh();
            Check(game.State.tutorialObservedCrab&&game.TutorialStep==HabitatTutorialStep.PickShrimp,
                "selecting the actual crab advances to pickup only after committed movement");
            await Step(0,1);game.tool=0;game.Select(Shrimp(game).x,Shrimp(game).z);int undo=game.UndoCount;
            game.ApplyTool();await Frames(2);Refresh();
            Check(Shrimp(game).carried&&game.State.carryingAnimal==Shrimp(game).id&&game.TutorialStep==HabitatTutorialStep.PlaceShrimp
                &&game.UndoCount==undo+1,"actual interact tool picks up the real shrimp and records one undo");
            game.Undo();await Frames(2);Refresh();
            Check(!Shrimp(game).carried&&game.State.carryingAnimal<0&&game.TutorialStep==HabitatTutorialStep.PickShrimp
                &&game.State.tutorialObservedCrab,"Undo pickup returns the guide to pickup while retaining the crab observation");
            game.Select(Shrimp(game).x,Shrimp(game).z);game.ApplyTool();await Frames(2);
            await Step(1,0);Check(Shrimp(game).carried&&game.TutorialStep==HabitatTutorialStep.PlaceShrimp,"carried shrimp and guide persist through a real walking transaction");
            await Shot("04-carrying-shrimp");await Step(-1,0);
            game.Select(HabitatTutorialRules.TrapX,HabitatTutorialRules.TrapZ);game.ApplyTool();await Frames(2);Refresh();
            Check(!Shrimp(game).carried&&game.State.carryingAnimal<0&&Shrimp(game).x==1&&Shrimp(game).z==3
                &&game.State.tiles[3*game.State.width+1].trap&&game.TutorialStep==HabitatTutorialStep.ObserveHabitat,
                "actual interact tool places the shrimp in the designated trap and requests habitat observation");
            await Click("Reveal tutorial target");
            Check(!game.State.tutorialObservedHabitat&&game.TutorialStep==HabitatTutorialStep.ObserveHabitat,
                "finding the crab does not substitute for selecting it to inspect its improved habitat");
            game.Select(Crab(game).x,Crab(game).z);await Frames(2);Refresh();
            Check(game.State.tutorialObservedHabitat&&HabitatRules.Evaluate(game.State,Crab(game),game.tuning).suitable
                &&game.TutorialStep==HabitatTutorialStep.Resume&&game.paused,"selecting the crab confirms suitable habitat without resuming time automatically");
            await SnapshotRoundtrip("pre-breeding Snapshot JSON roundtrip");
            float elapsed=game.State.elapsed,wall=Time.unscaledTime;await Click("Tutorial primary action");
            Check(!game.paused&&game.TutorialStep==HabitatTutorialStep.Wait,"real tutorial Resume button starts the simulation");
            await Until(()=>game.State.tutorialCompleted,24,"default 18-second live breeding");game.paused=true;await Frames(2);Refresh();
            float simulated=game.State.elapsed-elapsed,realSeconds=Time.unscaledTime-wall;
            Check(Crab(game).spawnedTotal>=1&&game.State.won&&game.TutorialFinished&&!game.State.tutorialOfferPending
                &&simulated>=17.99f&&realSeconds>=17.5f,"actual Update/Tick reaches first crab breeding and completion after "+simulated.ToString("F2")+" simulation seconds / "+realSeconds.ToString("F2")+" real seconds");
            await Shot("05-tutorial-complete");await SnapshotRoundtrip("completed Snapshot JSON roundtrip");
            string completedMap=Map(game.State),inventory=Inventory(game.State);await Click("Tutorial primary action");
            Check(game.State.levelIndex==0&&game.Hud.MapSelectionOpen&&Get<HabitatMapSelectionPanel>(game.Hud,"mapSelection").SelectedLevel==HabitatFigJamLevel.Index,
                "real completion button preselects the formal island without automatically sailing");
            await Click("Confirm destination");
            Report.AppendLine("RETURN DIAGNOSTIC: level="+game.State.levelIndex
                +", mapEqual="+(Map(game.State)==(string)Session["formalMap"])
                +", inventoryEqual="+(Inventory(game.State)==inventory)
                +", completed="+game.State.tutorialCompleted+", offer="+game.State.tutorialOfferPending
                +", card="+game.Hud.TutorialCardVisible);
            Check(game.State.levelIndex==HabitatFigJamLevel.Index&&Map(game.State)==(string)Session["formalMap"]
                &&Inventory(game.State)==inventory&&game.State.tutorialCompleted&&!game.State.tutorialOfferPending&&!game.Hud.TutorialCardVisible,
                "confirmed return restores every formal map cell/resident and preserves inventory plus tutorial completion");
            var cached=Visits(game).Find(v=>v.index==0);
            Check(cached!=null&&Map(cached.state)==completedMap&&cached.state.tutorialObservedCrab&&cached.state.tutorialObservedHabitat&&cached.state.tutorialCompleted,
                "the completed tutorial island remains cached with its moved shrimp and observation fields");
            await Shot("06-returned-formal-island");Report.AppendLine("PASS: entry, actual bridge walk, observation, pickup/Undo, trap placement, real 18-second breeding, snapshot and formal return completed.");
            Session["stage"]="complete";
        }
        catch(Exception error){failure=error;Report.AppendLine("ERROR "+error);}
        string result=Restore();if(failure!=null)throw new InvalidOperationException(result,failure);return result;
    }
    public static string Restore()
    {
        var session=Session;if(session==null)return "No tutorial fixture is active.";
        var report=(StringBuilder)session["report"];var game=(HabitatGame)session["game"];
        try
        {
            game.CancelPendingAction();if(game.Fishing)game.Fishing.Cancel();game.Hud.ClosePanels();
            var original=(HabitatState)session["original"];game.State=original;game.tuning=(Tuning)session["tuning"];
            var history=History(game);var undo=(HabitatState[])session["undo"];history.Clear();for(int i=undo.Length-1;i>=0;i--)history.Push(undo[i]);
            var visits=Visits(game);visits.Clear();visits.AddRange((HabitatGame.VisitedLevel[])session["visits"]);
            PutScalars(game,(Dictionary<FieldInfo,object>)session["gameScalars"]);game.View.Initialize(original);
            foreach(var item in (Dictionary<string,object>)session["camera"])Set(game.View,item.Key,item.Value);
            game.View.GameCamera.transform.SetPositionAndRotation((Vector3)session["cameraPosition"],(Quaternion)session["cameraRotation"]);
            game.View.GameCamera.orthographicSize=(float)session["cameraSize"];Refresh();
            foreach(var item in (Dictionary<GameObject,bool>)session["ui"])if(item.Key)item.Key.SetActive(item.Value);
            PutScalars(game.Hud,(Dictionary<FieldInfo,object>)session["hudScalars"]);
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject((GameObject)session["selected"]);
            bool restored=ReferenceEquals(game.State,original)&&Json(original)==(string)session["originalJson"]&&history.SequenceEqual(undo)
                &&VisitsJson(visits)==(string)session["visitsJson"]&&ReferenceEquals(game.tuning,session["tuning"])&&!game.Busy;
            report.AppendLine((restored?"PASS":"FAIL")+": original State reference/content, tuning, history, visited islands, pause/tool, camera and UI restored; no save was written.");
            Require(restored,"Original runtime restoration mismatch.");
        }
        finally
        {
            if(session.TryGetValue("keyboard",out object keyboard)&&keyboard is Keyboard k)InputSystem.RemoveDevice(k);
            if(session.TryGetValue("mouse",out object mouse)&&mouse is Mouse m)InputSystem.RemoveDevice(m);
            InputSystem.settings.backgroundBehavior=(InputSettings.BackgroundBehavior)session["background"];
            InputSystem.settings.editorInputBehaviorInPlayMode=(InputSettings.EditorInputBehaviorInPlayMode)session["editorInput"];
            (session["oldKeyboard"] as Keyboard)?.MakeCurrent();(session["oldMouse"] as Mouse)?.MakeCurrent();
            Time.timeScale=(float)session["timeScale"];
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"gameplay-checks.txt"),report.ToString());
            AppDomain.CurrentDomain.SetData(SessionKey,null);
        }
        return report.ToString();
    }
}
