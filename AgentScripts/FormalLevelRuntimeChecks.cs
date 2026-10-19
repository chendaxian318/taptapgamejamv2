using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Load with Unity CLI run_script, then invoke FormalLevelRuntimeChecks.Begin().
// This file is outside Assets and never changes scenes, prefabs or user save files.
// The fixture exercises actual boat callbacks, skipped departure completion and
// one real movement per source map. It does NOT solve puzzles or complete the story.
public static class FormalLevelRuntimeChecks
{
    const string Key="Habitat.FormalLevelRuntimeChecks.20261016";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly int[] Formal={5,6,7,8,4,9};
    static readonly string Folder=Path.GetFullPath("Logs/formal-levels-20261016");
    static Dictionary<string,object> Session=>AppDomain.CurrentDomain.GetData(Key) as Dictionary<string,object>;
    static HabitatGame Game=>(HabitatGame)Session["game"];
    static StringBuilder Report=>(StringBuilder)Session["report"];
    static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Flags).GetValue(target);
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
    static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Flags).Invoke(target,args);
    static string Json(object value)=>JsonUtility.ToJson(value);
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static List<HabitatGame.VisitedLevel> Visits=>Get<List<HabitatGame.VisitedLevel>>(Game,"visitedLevels");
    static Stack<HabitatState> History=>Get<Stack<HabitatState>>(Game,"history");
    static string Tiles(HabitatState s)=>s.width+"x"+s.depth+":"+string.Join("|",s.tiles.Select(Json));
    static string Shared(HabitatState s)=>string.Join("|",s.coins,s.cargo,s.fish,s.shrimp,s.rod,s.traps,
        s.shells,s.backBasketUnlocked,s.day,s.specialRod,s.chapterOneAccepted,s.chapterOneSailed,
        s.chapterOneDelivered,s.chapterOneMorningSeen,s.openingCompleted,s.openingCatchGranted);
    static string VisitsJson(IEnumerable<HabitatGame.VisitedLevel> values)=>string.Join("|",values.Select(v=>v.index+":"+v.actions+":"+Json(v.state)));
    static Dictionary<FieldInfo,object> Scalars(object target)=>target.GetType().GetFields(Flags)
        .Where(f=>!f.IsInitOnly&&!f.IsStatic&&(f.FieldType.IsPrimitive||f.FieldType.IsEnum||f.FieldType==typeof(string)))
        .ToDictionary(f=>f,f=>f.GetValue(target));
    static void RestoreScalars(object target,Dictionary<FieldInfo,object> values){foreach(var pair in values)pair.Key.SetValue(target,pair.Value);}
    static void Flush(){Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"runtime.txt"),Report.ToString());}
    static void Phase(string value){Session["phase"]=value;Report.AppendLine("SECTION "+value);Flush();}
    static void Check(bool condition,string message)
    {
        Session["checks"]=(int)Session["checks"]+1;
        Report.AppendLine((condition?"PASS ":"FAIL ")+Session["checks"]+": "+message);Flush();Require(condition,message);
    }
    static double Clock=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
    static async Task Frames(int frames)
    {
        int first=Time.frameCount;double deadline=Clock+15;
        while(Time.frameCount-first<frames)
        {
            Require(Application.isPlaying&&Clock<deadline,"Play frames stopped while awaiting "+frames+" frames.");
            await Task.Delay(12);
        }
    }
    static async Task Until(Func<bool> ready,double seconds,string label)
    {
        double deadline=Clock+seconds;
        while(!ready()){Require(Clock<deadline,"Timeout: "+label);await Frames(1);}
    }
    static async Task Seconds(double seconds){double deadline=Clock+seconds;while(Clock<deadline)await Frames(1);}
    static void Refresh(){Set(Game.Hud,"nextRefresh",0f);Game.Hud.Refresh();}
    static Button Button(string name)
    {
        Refresh();var found=Game.Hud.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name==name);
        Require(found,"HUD button missing: "+name);return found;
    }
    static async Task Click(string name)
    {
        var button=Button(name);Require(button.gameObject.activeInHierarchy&&button.interactable,"HUD button unavailable: "+name);
        Require(EventSystem.current,"Live EventSystem missing.");
        ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        await Frames(2);Refresh();
    }
    static async Task Screenshot(string name)
    {
        Refresh();await Frames(3);string path=Path.Combine(Folder,name+".png");
        ScreenCapture.CaptureScreenshot(path);await Frames(4);
        await Until(()=>File.Exists(path)&&new FileInfo(path).Length>0,10,"screenshot file "+name);
        Report.AppendLine("SCREENSHOT "+path);Flush();
    }

    public static string Status
    {
        get
        {
            var s=Session;if(s==null)return "Not started.";
            return (string)s["status"]+" | "+s["phase"]+" | checks="+s["checks"]
                +" | restored="+s["restored"]+" | "+Path.Combine(Folder,"runtime.txt")
                +(s.ContainsKey("error")?" | "+s["error"]:"");
        }
    }

    public static string Begin()
    {
        Require(Session==null||!(bool)Session["running"],"A formal runtime check is already running.");
        var game=HabitatGame.Instance;
        Require(Application.isPlaying&&game&&game.State!=null&&game.View&&game.Hud&&game.Story
            &&!game.Busy&&!game.Hud.ModalOpen&&!(game.Fishing&&game.Fishing.IsOpen)&&game.View.Harbor.Ready,
            "Run in an idle HabitatIsland Play session with story/fishing/panels closed and the harbor ready.");
        var camera=game.View.GameCamera;
        string[] cameraFields={"cameraYaw","cameraSize","cameraTarget","cameraTargetSmooth","followPlayer","overviewCamera",
            "overviewSize","framingAspect","framingDirty","nextVisibilityCheck"};
        var session=new Dictionary<string,object> {
            ["game"]=game,["originalSnapshot"]=Call(game,"CaptureSnapshot"),["originalState"]=game.State,
            ["originalJson"]=Json(game.State),["originalTuning"]=game.tuning,
            ["undo"]=Get<Stack<HabitatState>>(game,"history").ToArray(),
            ["visits"]=Get<List<HabitatGame.VisitedLevel>>(game,"visitedLevels").ToArray(),
            ["gameScalars"]=Scalars(game),["hudScalars"]=Scalars(game.Hud),
            ["cameraFields"]=cameraFields.ToDictionary(n=>n,n=>typeof(HabitatView).GetField(n,Flags).GetValue(game.View)),
            ["cameraPosition"]=camera.transform.position,["cameraRotation"]=camera.transform.rotation,
            ["cameraSize"]=camera.orthographicSize,["cameraOrtho"]=camera.orthographic,
            ["cameraColor"]=camera.backgroundColor,["cameraClear"]=camera.clearFlags,
            ["selected"]=EventSystem.current?EventSystem.current.currentSelectedGameObject:null,
            ["timeScale"]=Time.timeScale,["running"]=true,["restored"]=false,["status"]="RUNNING",
            ["phase"]="setup",["checks"]=0,["report"]=new StringBuilder()
        };
        AppDomain.CurrentDomain.SetData(Key,session);
        session["task"]=Run();return Status;
    }

    static async Task OpenBoat()
    {
        Require(!Game.Busy,"Cannot open boat during action.");Game.Hud.ClosePanels();Game.ReturnToHarbor();await Frames(3);
        Check(Game.View.Harbor.Ready&&Game.View.Harbor.NearBoat(Game.State),"real ReturnToHarbor reaches the boat on level "+Game.State.levelIndex);
        Game.Hud.OpenMapSelection(Game.State.levelIndex);await Frames(2);
        Check(Get<HabitatMapSelectionPanel>(Game.Hud,"mapSelection").gameObject.activeInHierarchy,"real boat destination panel opens");
    }
    static async Task Sail(int destination)
    {
        await OpenBoat();var before=Game.State;string shared=Shared(before);int source=before.levelIndex;
        await Click("Select destination "+destination);
        var picker=Get<HabitatMapSelectionPanel>(Game.Hud,"mapSelection");
        Check(picker.SelectedLevel==destination&&picker.SelectedDestinationAllowed&&ReferenceEquals(Game.State,before)
            &&!Game.StoryPlaying,"select level "+destination+" changes only the card selection");
        await Click("Confirm destination");
        Check(Game.StoryPlaying&&Game.Busy&&ReferenceEquals(Game.State,before)&&Game.State.levelIndex==source,
            "confirm level "+destination+" starts real departure without early destination commit");
        Check(Game.Story.Skip(),"departure Skip completes the real SailToLevel callback");
        await Until(()=>!Game.Busy&&Game.State.levelIndex==destination,10,"departure to "+destination);await Frames(4);
        Check(Game.State.levelIndex==destination&&!ReferenceEquals(Game.State,before),"departure commits destination "+destination);
        Check(Shared(Game.State)==shared,"coins/rod/shells/back basket and journey fields survive sailing to "+destination);
        Check(Game.paused&&Game.UndoCount==0,"arrival is paused and cannot undo purchases from another map");
    }

    static async Task RenderAndMove(int index)
    {
        var s=Game.State;var source=HabitatLevels.Create(index);
        Check(s.authoredLevel&&s.width==source.width&&s.depth==source.depth&&Tiles(s)==Tiles(source),
            "level "+index+" renders the authored "+s.width+"x"+s.depth+" source terrain");
        Check(Game.View.Harbor.Ready&&Game.View.GameCamera&&Game.View.GameCamera.enabled
            &&ReferenceEquals(Get<HabitatState>(Game.View,"state"),s),"level "+index+" has a bound view, enabled camera and ready harbor");
        Check(Game.View.GetComponentsInChildren<GridHit>(true).Length==s.tiles.Length,
            "level "+index+" exposes one grid hit target per source cell");
        // Only shoreline placement is fixture setup. We do not claim to walk the imported dock.
        s.onHarbor=false;s.playerX=source.playerX;s.playerZ=source.playerZ;
        s.facingX=source.facingX;s.facingZ=source.facingZ;s.selectedX=s.selectedZ=-1;
        Game.View.Initialize(s);Game.View.Render(s,Game.tuning,Game.overlays);await Frames(4);
        int[] dx={1,0,0,-1},dz={0,1,-1,0};int chosen=-1;
        for(int i=0;i<4;i++)
        {
            var trial=HabitatRules.Clone(s);var tile=HabitatRules.TileAt(s,s.playerX+dx[i],s.playerZ+dz[i]);
            if(tile==null||tile.prop!=PropKind.None||HabitatRules.IsWater(s,s.playerX+dx[i],s.playerZ+dz[i])
                ||Game.View.Harbor.CanEnter(s,dx[i],dz[i]))continue;
            if(HabitatRules.MovePlayer(trial,dx[i],dz[i],false)){chosen=i;break;}
        }
        Require(chosen>=0,"No ordinary legal shore step on source map "+index);
        int fromX=s.playerX,fromZ=s.playerZ,actions=Game.ActionCount;Game.Move(dx[chosen],dz[chosen]);
        Check(Game.Busy&&ReferenceEquals(Game.State,s)&&s.playerX==fromX&&s.playerZ==fromZ,
            "level "+index+" keeps source State uncommitted while its real move is animating");
        await Until(()=>!Game.Busy,10,"movement arrival on "+index);await Frames(2);
        var at=Game.State;var position=Game.View.PlayerWorldPosition;
        Check(!at.onHarbor&&at.playerX==fromX+dx[chosen]&&at.playerZ==fromZ+dz[chosen]
            &&Game.ActionCount==actions+1&&Mathf.Abs(position.x-at.playerX)<.06f&&Mathf.Abs(position.z-at.playerZ)<.06f,
            "level "+index+" commits one legal move only after the visual actor reaches the destination");
        Game.View.FocusOverview();await Seconds(.8);await Screenshot("level-"+index+"-overview");
    }

    static async Task Run()
    {
        Exception failure=null;
        try
        {
            Phase("Isolated fixture: completed commission flags and preset shared inventory; no story/puzzle completion claim");
            Time.timeScale=1;
            var snapshot=Session["originalSnapshot"];var fixture=JsonUtility.FromJson(Json(snapshot),snapshot.GetType());
            var initial=HabitatLevels.Create(0);
            initial.openingCompleted=initial.openingCatchGranted=initial.chapterOneAccepted=initial.chapterOneSailed=true;
            initial.chapterOneDelivered=initial.chapterOneMorningSeen=true;
            initial.tutorialCompleted=initial.tutorialGuideDismissed=true;initial.tutorialOfferPending=false;
            initial.coins=713;initial.cargo=3;initial.fish=5;initial.shrimp=7;initial.traps=2;initial.day=4;
            initial.rod=true;initial.shells=17;initial.backBasketUnlocked=true;
            Set(fixture,"state",initial);Set(fixture,"actions",0);Set(fixture,"visitedLevels",new List<HabitatGame.VisitedLevel>());
            Set(fixture,"tuning",new Tuning{animalMovement=false});
            Call(Game,"RestoreSnapshot",fixture);Game.paused=true;Game.speed=1;Game.tool=0;
            Set(Game,"chapterGreetingPending",false);Set(Game,"chapterChestPending",false);Set(Game,"chapterMorningGreetingPending",false);
            await Frames(4);Refresh();

            Phase("Seven actual destination buttons; selection does not sail");
            await OpenBoat();var untouched=Game.State;int actions=Game.ActionCount;
            var picker=Get<HabitatMapSelectionPanel>(Game.Hud,"mapSelection");
            var cards=picker.GetComponentsInChildren<Button>(true).Where(b=>b.name.StartsWith("Select destination ")).ToArray();
            Check(cards.Length==7&&HabitatLevels.CampaignDestinations.SequenceEqual(new[]{5,6,7,8,4,9,0}),
                "boat UI has six formal destination cards plus optional tutorial");
            foreach(int index in HabitatLevels.CampaignDestinations)
            {
                await Click("Select destination "+index);
                Check(picker.SelectedLevel==index&&picker.SelectedDestinationAllowed&&ReferenceEquals(Game.State,untouched)
                    &&Game.ActionCount==actions&&!Game.Busy&&!Game.StoryPlaying,"card "+index+" is selectable without state/travel changes");
            }
            await Screenshot("seven-destination-map");Game.Hud.ClosePanels();

            Phase("Six real confirmed sailings, source renders and one committed shore step each");
            var marked=new Dictionary<int,int>();
            foreach(int index in Formal)
            {
                await Sail(index);await RenderAndMove(index);
                // An isolated corner terrain edit is the cache sentinel, not a puzzle action.
                var corner=HabitatRules.TileAt(Game.State,0,0);marked[index]=corner.height+1;corner.height=marked[index];
                Game.State.coins+=11;Game.State.shells+=2;
                Report.AppendLine("FIXTURE level "+index+" corner (0,0) height="+corner.height+"; shared coins/shells incremented for stale-cache detection.");Flush();
            }
            string shared=Shared(Game.State);
            Phase("Reverse sailing preserves every island terrain edit and newest shared inventory");
            foreach(int index in Formal.Reverse().Skip(1).Concat(new[]{9}))
            {
                await Sail(index);
                Check(HabitatRules.TileAt(Game.State,0,0).height==marked[index],"revisit "+index+" restores its own modified terrain");
                Check(Shared(Game.State)==shared,"revisit "+index+" does not restore stale cached shared inventory");
            }

            Phase("In-memory CaptureSnapshot / JSON / RestoreSnapshot across all six formal islands");
            var captured=Call(Game,"CaptureSnapshot");string json=Json(captured);
            var copy=JsonUtility.FromJson(json,captured.GetType());string currentTiles=Tiles(Game.State);
            string cached=VisitsJson(Get<List<HabitatGame.VisitedLevel>>(copy,"visitedLevels"));
            Check(json.Contains("\"shells\"")&&json.Contains("\"backBasketUnlocked\"")&&json.Contains("\"visitedLevels\""),
                "snapshot JSON explicitly serializes new inventory and visited island cache");
            Game.State.coins=0;Game.State.shells=0;Game.State.rod=false;Game.State.backBasketUnlocked=false;
            HabitatRules.TileAt(Game.State,0,0).height+=4;Visits.Clear();
            Call(Game,"RestoreSnapshot",copy);await Frames(4);Refresh();
            Check(Shared(Game.State)==shared&&Tiles(Game.State)==currentTiles&&VisitsJson(Visits)==cached,
                "JSON roundtrip restores shared inventory, current terrain and all cached destination states");
            foreach(int index in Formal)
            {
                var state=Game.State.levelIndex==index?Game.State:Visits.First(v=>v.index==index).state;
                Check(HabitatRules.TileAt(state,0,0).height==marked[index],"snapshot preserves terrain sentinel for formal island "+index);
            }

            Phase("RestartCurrentLevel resets only current island and retains shared inventory/other caches");
            int resetIndex=Game.State.levelIndex;string otherCaches=VisitsJson(Visits.Where(v=>v.index!=resetIndex));
            Game.RestartCurrentLevel();await Frames(4);Refresh();
            Check(Tiles(Game.State)==Tiles(HabitatLevels.Create(resetIndex))&&Game.ActionCount==0&&Game.UndoCount==0,
                "restart recreates only source terrain and clears current-map actions");
            Check(Shared(Game.State)==shared&&VisitsJson(Visits.Where(v=>v.index!=resetIndex))==otherCaches&&!Visits.Any(v=>v.index==resetIndex),
                "restart retains newest inventory/gear and every other island cache");
            await Sail(6);
            Check(HabitatRules.TileAt(Game.State,0,0).height==marked[6]&&Shared(Game.State)==shared,
                "actual sailing after restart still restores another island's modified terrain and shared inventory");
            Report.AppendLine("LIMITS: shore anchoring, balances and quest flags were fixtures. No full dock walk, puzzle solution, fishing, delivery or natural departure timing was claimed. All travel used actual UI confirmation and Story.Skip.");
        }
        catch(Exception exception){failure=exception;Session["error"]=exception.Message;Report.AppendLine("ERROR "+exception);}
        finally
        {
            try{RestoreOriginal();}
            catch(Exception exception){failure=failure??exception;Session["error"]=exception.Message;Report.AppendLine("RESTORE ERROR "+exception);}
            Session["running"]=false;Session["status"]=failure==null?"PASS":"FAILED";Session["phase"]="finished";Flush();
        }
    }

    static void RestoreOriginal()
    {
        var s=Session;var original=(HabitatState)s["originalState"];
        Game.CancelPendingAction();if(Game.Fishing&&Game.Fishing.IsOpen)Game.Fishing.Cancel();Game.Hud.ClosePanels();
        Call(Game,"RestoreSnapshot",s["originalSnapshot"]);
        Game.State.message=original.message;
        bool snapshotOkay=Json(Game.State)==(string)s["originalJson"];
        // Restore pre-existing object identities and undo history after verifying the load path.
        Game.State=original;Game.tuning=(Tuning)s["originalTuning"];
        Visits.Clear();Visits.AddRange((HabitatGame.VisitedLevel[])s["visits"]);
        History.Clear();var undo=(HabitatState[])s["undo"];for(int i=undo.Length-1;i>=0;i--)History.Push(undo[i]);
        RestoreScalars(Game,(Dictionary<FieldInfo,object>)s["gameScalars"]);
        Game.View.Initialize(original);Game.View.Render(original,Game.tuning,Game.overlays);Refresh();
        foreach(var field in (Dictionary<string,object>)s["cameraFields"])Set(Game.View,field.Key,field.Value);
        var camera=Game.View.GameCamera;camera.transform.SetPositionAndRotation((Vector3)s["cameraPosition"],(Quaternion)s["cameraRotation"]);
        camera.orthographicSize=(float)s["cameraSize"];camera.orthographic=(bool)s["cameraOrtho"];
        camera.backgroundColor=(Color)s["cameraColor"];camera.clearFlags=(CameraClearFlags)s["cameraClear"];
        RestoreScalars(Game.Hud,(Dictionary<FieldInfo,object>)s["hudScalars"]);
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject((GameObject)s["selected"]);
        Time.timeScale=(float)s["timeScale"];
        bool restored=snapshotOkay&&ReferenceEquals(Game.State,original)&&Json(original)==(string)s["originalJson"]
            &&History.SequenceEqual(undo)&&!Game.Busy;
        s["restored"]=restored;
        Check(restored,"original CaptureSnapshot restored; original State/tuning/history/visits/camera/pause returned; no user save written");
    }
}
