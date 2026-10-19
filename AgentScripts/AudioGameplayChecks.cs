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

// Load with Unity CLI run_script, invoke AudioGameplayChecks.Begin(), then poll Status.
// Outside Assets. Exercises live audio/UI but does not write user game saves.
// Tests actual game transactions and frame-delayed commits, not direct PlayAction calls.
public static class AudioGameplayChecks
{
    const string Key="Habitat.AudioGameplayChecks.20261016";
    const string Prefix="HabitatIsland.Audio.";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly string Folder=Path.GetFullPath("Logs/audio-pack-20261016");
    static Dictionary<string,object> Session=>AppDomain.CurrentDomain.GetData(Key) as Dictionary<string,object>;
    static HabitatGame Game=>(HabitatGame)Session["game"];
    static HabitatAudio Sound=>(HabitatAudio)Session["audio"];
    static StringBuilder Report=>(StringBuilder)Session["report"];
    static AudioSource[] Ambient=>Get<AudioSource[]>(Sound,"ambienceSources");
    static AudioSource Effects=>Get<AudioSource>(Sound,"effectsSource");
    static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Flags).GetValue(target);
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
    static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Flags).Invoke(target,args);
    static string Json(object value)=>JsonUtility.ToJson(value);
    static double Clock=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static bool Near(float a,float b)=>Mathf.Abs(a-b)<.0001f;
    static Dictionary<FieldInfo,object> Scalars(object target)=>target.GetType().GetFields(Flags)
        .Where(f=>!f.IsInitOnly&&!f.IsStatic&&(f.FieldType.IsPrimitive||f.FieldType.IsEnum||f.FieldType==typeof(string)))
        .ToDictionary(f=>f,f=>f.GetValue(target));
    static void RestoreScalars(object target,Dictionary<FieldInfo,object> values){foreach(var p in values)p.Key.SetValue(target,p.Value);}
    static void Flush(){Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"gameplay.txt"),Report.ToString());}
    static void Phase(string phase){Session["phase"]=phase;Report.AppendLine("SECTION "+phase);Flush();}
    static void Check(bool condition,string message)
    {
        Session["checks"]=(int)Session["checks"]+1;
        Report.AppendLine((condition?"PASS ":"FAIL ")+Session["checks"]+": "+message);Flush();Require(condition,message);
    }
    static async Task Frames(int count)
    {
        int start=Time.frameCount;double deadline=Clock+15;
        while(Time.frameCount-start<count)
        {
            Require(Application.isPlaying&&Clock<deadline,"Play frames stopped during audio checks.");
            await Task.Delay(12);
        }
    }
    static async Task Seconds(double seconds){double deadline=Clock+seconds;while(Clock<deadline)await Frames(1);}
    static async Task Until(Func<bool> ready,double seconds,string label)
    {double deadline=Clock+seconds;while(!ready()){Require(Clock<deadline,"Timeout: "+label);await Frames(1);}}
    static void Refresh(){Set(Game.Hud,"nextRefresh",0f);Game.Hud.Refresh();}
    static async Task Click(string name)
    {
        await Seconds(.1);Refresh();
        var button=Game.Hud.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name==name);
        Require(button&&button.gameObject.activeInHierarchy&&button.interactable,"HUD button unavailable: "+name);
        Require(EventSystem.current,"No live EventSystem.");
        ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        await Frames(2);Refresh();
    }
    static async Task Screenshot()
    {
        Refresh();await Frames(3);string path=Path.Combine(Folder,"audio-settings.png");
        ScreenCapture.CaptureScreenshot(path);await Frames(4);
        await Until(()=>File.Exists(path)&&new FileInfo(path).Length>0,10,"audio settings screenshot");
        Report.AppendLine("SCREENSHOT "+path);Flush();
    }
    static int Instances()=>UnityEngine.Object.FindObjectsByType<HabitatAudio>(FindObjectsInactive.Include).Length;
    static int ActiveListeners()=>UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include).Count(x=>x.isActiveAndEnabled);
    static double PeakOutput(AudioSource source,float[] buffer)
    {source.GetOutputData(buffer,0);double peak=0;for(int i=0;i<buffer.Length;i++)peak=Math.Max(peak,Math.Abs(buffer[i]));return peak;}
    static float PreferenceVolume(string key,float fallback)
    {float v=PlayerPrefs.GetFloat(Prefix+key,fallback);return float.IsNaN(v)||float.IsInfinity(v)?fallback:Mathf.Clamp01(v);}

    public static string Status
    {
        get
        {
            var s=Session;if(s==null)return "Not started.";
            return s["status"]+" | "+s["phase"]+" | checks="+s["checks"]+" | restored="+s["restored"]
                +" | "+Path.Combine(Folder,"gameplay.txt")+(s.ContainsKey("error")?" | "+s["error"]:"");
        }
    }
    public static string Begin()
    {
        Require(Session==null||!(bool)Session["running"],"Audio checks are already running.");
        var game=HabitatGame.Instance;var sound=HabitatAudio.Ensure();
        Require(Application.isPlaying&&game&&game.State!=null&&game.View&&game.Hud&&sound
            &&!game.Busy&&!game.StoryPlaying&&!(game.Fishing&&game.Fishing.IsOpen),
            "Run in idle HabitatIsland Play with no action/story/fishing in progress. Existing modal may be closed by this fixture.");
        var camera=game.View.GameCamera;
        string[] cameraFields={"cameraYaw","cameraSize","cameraTarget","cameraTargetSmooth","followPlayer","overviewCamera",
            "overviewSize","framingAspect","framingDirty","nextVisibilityCheck"};
        var s=new Dictionary<string,object>{
            ["game"]=game,["audio"]=sound,["snapshot"]=Call(game,"CaptureSnapshot"),["state"]=game.State,
            ["stateJson"]=Json(game.State),["tuning"]=game.tuning,["gameScalars"]=Scalars(game),["hudScalars"]=Scalars(game.Hud),
            ["undo"]=Get<Stack<HabitatState>>(game,"history").ToArray(),
            ["visits"]=Get<List<HabitatGame.VisitedLevel>>(game,"visitedLevels").ToArray(),
            ["cameraFields"]=cameraFields.ToDictionary(n=>n,n=>typeof(HabitatView).GetField(n,Flags).GetValue(game.View)),
            ["cameraPosition"]=camera.transform.position,["cameraRotation"]=camera.transform.rotation,
            ["cameraSize"]=camera.orthographicSize,["cameraOrtho"]=camera.orthographic,
            ["cameraColor"]=camera.backgroundColor,["cameraClear"]=camera.clearFlags,
            ["selected"]=EventSystem.current?EventSystem.current.currentSelectedGameObject:null,
            ["timeScale"]=Time.timeScale,["ambience"]=sound.AmbienceVolume,["effects"]=sound.EffectsVolume,
            ["muted"]=sound.Muted,["enabled"]=sound.enabled,
            ["ambiencePresent"]=PlayerPrefs.HasKey(Prefix+"Ambience"),["effectsPresent"]=PlayerPrefs.HasKey(Prefix+"Effects"),
            ["mutedPresent"]=PlayerPrefs.HasKey(Prefix+"Muted"),
            ["ambiencePref"]=PlayerPrefs.GetFloat(Prefix+"Ambience",.25f),["effectsPref"]=PlayerPrefs.GetFloat(Prefix+"Effects",.5f),
            ["mutedPref"]=PlayerPrefs.GetInt(Prefix+"Muted",0),
            ["running"]=true,["restored"]=false,["status"]="RUNNING",["phase"]="setup",["checks"]=0,["report"]=new StringBuilder()
        };
        AppDomain.CurrentDomain.SetData(Key,s);s["task"]=Run();return Status;
    }
    static Tile Q(HabitatState state,int x,int z)=>HabitatRules.TileAt(state,x,z);
    static HabitatState Fixture(int height=2)
    {
        var state=new HabitatState { width=9,depth=9,tiles=new Tile[81],playerX=4,playerZ=4,
            selectedX=4,selectedZ=5,facingX=0,facingZ=1,waterLevel=0,tideBaseLevel=0,
            levelIndex=5,levelName="音效接线临时夹具",objective="临时检查，不保存",authoredLevel=true,
            dockX=1,dockZ=1,dockRotation=-1,openingCompleted=true,openingCatchGranted=true,
            rod=true,coins=30,traps=2,cargo=3,goals=Array.Empty<Species>() };
        for(int i=0;i<state.tiles.Length;i++)state.tiles[i]=new Tile{height=height};
        return state;
    }
    static HabitatState FishingFixture()
    {
        var state=Fixture(1);Q(state,4,5).height=Q(state,4,6).height=0;
        state.animals.Add(new Animal{id=81001,x=4,z=6,homeX=4,homeZ=6,species=Species.Goldfish,offspring=2});
        state.selectedX=4;state.selectedZ=6;return state;
    }
    static async Task Install(HabitatState state,int tool=0)
    {
        if(Game.Fishing&&Game.Fishing.IsOpen)Game.Fishing.Cancel(false);
        Game.CancelPendingAction();Game.Hud.ClosePanels();
        var fixture=JsonUtility.FromJson(Json(Session["snapshot"]),Session["snapshot"].GetType());
        Set(fixture,"state",state);Set(fixture,"tuning",new Tuning{animalMovement=false,breedSeconds=1000});Set(fixture,"actions",0);
        Call(Game,"RestoreSnapshot",fixture);Game.paused=true;Game.speed=1f;Game.tool=tool;
        Set(Game,"chapterGreetingPending",false);Set(Game,"chapterChestPending",false);Set(Game,"chapterMorningGreetingPending",false);
        await Frames(4);HabitatAudio.ClearTransient();await Frames(2);
        Require(!Game.Busy&&!Game.StoryPlaying&&!Game.State.onHarbor,"Fixture failed to become idle.");
    }
    static async Task Arrive()
    {await Until(()=>!Game.Busy,10,"committed movement");await Frames(3);}
    static bool OneAction(int before,HabitatActionCue cue)=>Sound.ActionPlayCount==before+1&&Sound.LastActionCue==cue;
    static async Task Run()
    {
        Exception failure=null;
        try
        {
            Session["fishingScalars"]=Scalars(Game.Fishing);
            Time.timeScale=1;Sound.enabled=true;Sound.SetMuted(false);Sound.SetEffectsVolume(.4f);Sound.SetAmbienceVolume(.2f);
            Phase("Actual movement arrival, cancellation and undo");
            await Install(Fixture());int count=Sound.ActionPlayCount;string before=Json(Game.State);
            Game.Move(0,1);
            Check(Game.Busy&&Json(Game.State)==before&&Sound.ActionPlayCount==count,"successful Move previews without changing authoritative state or playing a step");
            await Arrive();
            Check(Game.State.playerZ==5&&OneAction(count,HabitatActionCue.StepGrass)&&Sound.LastActionClipName.StartsWith("footstep_grass_"),"arrived grass movement commits exactly one grass step");
            count=Sound.ActionPlayCount;Game.Undo();await Frames(3);
            Check(Game.State.playerZ==4&&Sound.ActionPlayCount==count,"Undo restores position without reverse footsteps or reward sounds");
            count=Sound.ActionPlayCount;Game.Move(0,1);Game.CancelPendingAction();await Frames(5);
            Check(!Game.Busy&&Game.State.playerZ==4&&Sound.ActionPlayCount==count,"cancelling an in-flight movement leaves source position and action audio unchanged");
            var state=Fixture();Q(state,4,5).prop=PropKind.Obstacle;await Install(state);count=Sound.ActionPlayCount;
            int cues=Sound.CuePlayCount;Game.Move(0,1);await Frames(3);
            Check(!Game.Busy&&Game.State.playerZ==4&&Sound.ActionPlayCount==count&&Sound.CuePlayCount==cues+1&&Sound.LastPlayedCue==HabitatAudioCue.Error,"blocked wall movement produces Error but no successful action sound");
            await Install(Fixture(1));count=Sound.ActionPlayCount;Game.Move(0,1);await Arrive();
            Check(Game.State.playerZ==5&&OneAction(count,HabitatActionCue.StepSand)&&Sound.LastActionClipName.StartsWith("step_sand_"),"arrived low-bank movement uses the sand step mapping");
            state=Fixture();state.waterLevel=state.tideBaseLevel=1;Q(state,4,5).height=0;Q(state,4,5).prop=PropKind.Float;
            await Install(state);count=Sound.ActionPlayCount;
            Game.Move(0,1);await Arrive();
            Check(Game.State.playerZ==5&&Q(Game.State,4,5).prop==PropKind.Float&&OneAction(count,HabitatActionCue.StepWood)
                &&Sound.LastActionClipName.StartsWith("footstep_wood_"),"arrived movement onto a same-height floating wooden crate uses the wood step mapping");
            state=Fixture();Q(state,4,5).prop=PropKind.Crate;await Install(state);count=Sound.ActionPlayCount;before=Json(Game.State);
            Game.Move(0,1);
            Check(Game.Busy&&Json(Game.State)==before&&Sound.ActionPlayCount==count,"push preview is silent and retains the original box/player state");
            await Arrive();
            Check(Game.State.playerZ==5&&Q(Game.State,4,6).prop==PropKind.Crate&&Sound.ActionPlayCount==count+2&&Sound.LastActionCue==HabitatActionCue.Push,"committed push plays one arrived footstep and one box impact");
            count=Sound.ActionPlayCount;Game.Pull(0,-1);await Arrive();
            Check(Game.State.playerZ==4&&Q(Game.State,4,5).prop==PropKind.Crate&&Sound.ActionPlayCount==count+2&&Sound.LastActionCue==HabitatActionCue.Push,"committed backward pull plays one step and one box impact");
            state=Fixture(1);Q(state,4,4).height=2;Q(state,4,5).prop=PropKind.Crate;Q(state,4,6).prop=PropKind.Tide;
            await Install(state);count=Sound.ActionPlayCount;Game.Move(0,1);await Arrive();
            Check(Game.State.playerZ==4&&Game.State.waterLevel==1&&Q(Game.State,4,6).prop==PropKind.Crate
                &&Sound.ActionPlayCount==count+2&&Sound.LastActionCue==HabitatActionCue.Tide,"a push that floods the front cell plays Push + Tide only, with no invented footstep");

            Phase("Actual tool transactions, collection and commerce");
            state=Fixture();Q(state,4,5).prop=PropKind.Fan;await Install(state);count=Sound.ActionPlayCount;
            Game.ApplyTool();await Frames(3);
            Check(Game.State.carryingProp==PropKind.Fan&&OneAction(count,HabitatActionCue.Pick),"successful Interact pickup plays Pick");
            count=Sound.ActionPlayCount;Game.ApplyTool();await Frames(3);
            Check(Game.State.carryingProp==PropKind.None&&Q(Game.State,4,5).prop==PropKind.Fan&&OneAction(count,HabitatActionCue.Drop),"successful Interact placement plays Drop");
            state=Fixture();state.carryingProp=PropKind.Climb;await Install(state,1);count=Sound.ActionPlayCount;
            Game.ApplyTool();await Frames(3);
            Check(Q(Game.State,4,5).height==3&&Game.State.carryingProp==PropKind.None&&OneAction(count,HabitatActionCue.Soil),"successful soil placement raises the tile and plays Soil");
            await Install(Fixture(),3);count=Sound.ActionPlayCount;Game.ApplyTool();await Frames(3);
            Check(Q(Game.State,4,5).trap&&Game.State.traps==1&&OneAction(count,HabitatActionCue.Trap),"successful trap placement consumes one trap and plays Trap");
            count=Sound.ActionPlayCount;cues=Sound.CuePlayCount;Game.ApplyTool();await Frames(3);
            Check(Game.State.traps==1&&Sound.ActionPlayCount==count&&Sound.CuePlayCount==cues+1&&Sound.LastPlayedCue==HabitatAudioCue.Error,"duplicate trap placement uses Error and produces no success sound or inventory change");
            state=Fixture();Q(state,4,5).prop=PropKind.Chest;Q(state,4,5).chestReward=ChestReward.Unspecified;
            await Install(state);count=Sound.ActionPlayCount;Game.ApplyTool();await Frames(3);
            Check(Game.State.openedChests==1&&Q(Game.State,4,5).prop==PropKind.None&&OneAction(count,HabitatActionCue.Chest),"opening a real chest plays Chest exactly once");
            state=FishingFixture();await Install(state,2);count=Sound.ActionPlayCount;Game.ApplyTool();await Frames(3);
            Check(Game.State.animals[0].z==5&&OneAction(count,HabitatActionCue.Lure),"successful rod lure moves the fish and plays Lure");
            state=Fixture();state.animals.Add(new Animal{id=81002,x=4,z=5,species=Species.Shrimp,offspring=2});
            await Install(state,4);count=Sound.ActionPlayCount;Game.ApplyTool();await Frames(3);
            Check(Game.State.shrimp==2&&Game.State.animals.Count==1&&Game.State.animals[0].offspring==0&&OneAction(count,HabitatActionCue.Harvest),"manual offspring harvest preserves the parent and plays Harvest");
            state=Fixture();Q(state,4,5).prop=PropKind.Road;await Install(state);count=Sound.ActionPlayCount;
            Game.RotateTarget();await Frames(3);
            Check(Q(Game.State,4,5).rotation==1&&OneAction(count,HabitatActionCue.Rotate),"real road rotation plays Rotate");
            state=Fixture();state.dockX=4;state.dockZ=3;await Install(state);count=Sound.ActionPlayCount;cues=Sound.CuePlayCount;
            Game.Buy("trap");await Frames(3);
            Check(Game.State.coins==27&&Game.State.traps==3&&OneAction(count,HabitatActionCue.Trade)&&Sound.CuePlayCount==cues,"successful paid purchase plays only Trade, with no generic confirmation stacked");
            Game.State.coins=0;count=Sound.ActionPlayCount;cues=Sound.CuePlayCount;Game.Buy("trap");await Frames(3);
            Check(Game.State.traps==3&&Sound.ActionPlayCount==count&&Sound.CuePlayCount==cues+1&&Sound.LastPlayedCue==HabitatAudioCue.Error,"insufficient funds plays Error without Trade or inventory increase");
            count=Sound.ActionPlayCount;Game.Buy("soil");await Frames(3);
            Check(Game.State.coins==0&&Game.State.cargo==2&&Game.State.carryingProp==PropKind.Climb&&OneAction(count,HabitatActionCue.Pick),"free cargo soil retrieval uses Pick rather than a paid transaction sound");

            Phase("Fishing start, voluntary/programmatic cancellation and real settlement");
            await Install(FishingFixture(),4);count=Sound.ActionPlayCount;Game.ApplyTool();await Frames(3);
            Check(Game.Fishing.IsOpen&&Game.State.fish==0&&OneAction(count,HabitatActionCue.FishingCast),"actual fishing modal start plays FishingCast without awarding a catch");
            count=Sound.ActionPlayCount;Game.Fishing.Cancel();await Frames(3);
            Check(!Game.Fishing.IsOpen&&Game.State.animals[0].offspring==2&&OneAction(count,HabitatActionCue.FishingReel),"voluntary cancellation plays FishingReel and retains offspring");
            Game.ApplyTool();await Frames(3);count=Sound.ActionPlayCount;Game.Fishing.Cancel(false);await Frames(3);
            Check(!Game.Fishing.IsOpen&&Game.State.animals[0].offspring==2&&Sound.ActionPlayCount==count,"programmatic fishing cancellation is silent and retains offspring");
            Game.ApplyTool();await Frames(3);count=Sound.ActionPlayCount;
            Game.Fishing.BarSize=.12f;Game.Fishing.CatchGainPerSecond=0;Game.Fishing.EscapeLossPerSecond=100;
            Set(Game.Fishing,"<Progress>k__BackingField",.01f);Set(Game.Fishing,"<Bar>k__BackingField",.07f);
            Set(Game.Fishing,"elapsed",1f);Set(Game.Fishing,"phase",0f);Set(Game.Fishing,"velocity",0f);
            Game.Fishing.Advance(.1f,false);await Frames(3);
            Check(!Game.Fishing.IsOpen&&Game.State.fish==0&&Game.State.animals[0].offspring==2&&OneAction(count,HabitatActionCue.FishingMiss),"Advance reaches actual failed Finish: FishingMiss, no lost offspring or false reward");
            RestoreScalars(Game.Fishing,(Dictionary<FieldInfo,object>)Session["fishingScalars"]);
            Game.ApplyTool();await Frames(3);count=Sound.ActionPlayCount;
            Game.Fishing.BarSize=.8f;Game.Fishing.CatchGainPerSecond=10;Game.Fishing.EscapeLossPerSecond=0;
            Set(Game.Fishing,"<Progress>k__BackingField",.999f);Set(Game.Fishing,"<Bar>k__BackingField",.5f);
            Set(Game.Fishing,"elapsed",.5f);Set(Game.Fishing,"phase",0f);Set(Game.Fishing,"velocity",0f);
            Game.Fishing.Advance(.1f,false);await Frames(3);
            Check(!Game.Fishing.IsOpen&&Game.State.fish==2&&Game.State.animals.Count==1&&Game.State.animals[0].offspring==0
                &&OneAction(count,HabitatActionCue.Harvest),"Advance reaches actual successful Finish: one Harvest only after catch settlement, parent preserved");
            count=Sound.ActionPlayCount;cues=Sound.CuePlayCount;Game.CompleteFishing(4,6);await Frames(3);
            Check(Game.State.fish==2&&Sound.ActionPlayCount==count&&Sound.CuePlayCount==cues+1&&Sound.LastPlayedCue==HabitatAudioCue.Error,"a second stale fishing completion cannot produce duplicate rewards or Harvest sound");

            Phase("Committed ecology: automatic trap harvest and tide change");
            state=Fixture();Q(state,4,5).trap=true;state.animals.Add(new Animal{id=81003,x=4,z=5,species=Species.Shrimp,offspring=2});
            await Install(state);count=Sound.ActionPlayCount;Game.Step();await Arrive();
            Check(Game.State.shrimp==2&&Game.State.animals[0].offspring==0&&OneAction(count,HabitatActionCue.Harvest),"a real manual simulation Step auto-collects trap offspring with exactly one Harvest");
            count=Sound.ActionPlayCount;Game.Step();await Arrive();
            Check(Game.State.shrimp==2&&Sound.ActionPlayCount==count,"subsequent empty trap Step stays silent");
            state=Fixture();Q(state,6,6).prop=PropKind.Crate;Q(state,6,6).coveredProp=PropKind.Tide;
            await Install(state);count=Sound.ActionPlayCount;Game.Step();await Arrive();
            Check(Game.State.waterLevel==1&&Game.State.tideActive&&OneAction(count,HabitatActionCue.Tide),"committed ecology detects the pressure plate tide change exactly once");
            count=Sound.ActionPlayCount;Game.Step();await Arrive();
            Check(Game.State.waterLevel==1&&Sound.ActionPlayCount==count,"unchanged tide on subsequent Step stays silent");
            Report.AppendLine("LIMITS: deterministic temporary fixtures and genuine Play frames verify event routing; no subjective listening or full-level playthrough claimed. No game save written. Fixture adjusts fishing rates only to exercise both real completion branches.");
        }
        catch(Exception e){failure=e;Session["error"]=e.Message;Report.AppendLine("ERROR "+e);}
        finally
        {
            try{RestoreOriginal();}
            catch(Exception e){failure=failure??e;Session["error"]=e.Message;Report.AppendLine("RESTORE ERROR "+e);}
            try{RestoreAudioPreferences();}
            catch(Exception e){failure=failure??e;Session["error"]=e.Message;Report.AppendLine("AUDIO RESTORE ERROR "+e);}
            Session["running"]=false;Session["status"]=failure==null?"PASS":"FAILED";Session["phase"]="finished";Flush();
        }
    }
    static void RestoreOriginal()
    {
        var s=Session;var original=(HabitatState)s["state"];
        Game.CancelPendingAction();if(Game.Fishing&&Game.Fishing.IsOpen)Game.Fishing.Cancel(false);Game.Hud.ClosePanels();
        if(s.ContainsKey("fishingScalars"))RestoreScalars(Game.Fishing,(Dictionary<FieldInfo,object>)s["fishingScalars"]);
        Call(Game,"RestoreSnapshot",s["snapshot"]);Game.State.message=original.message;
        bool snapshotOkay=Json(Game.State)==(string)s["stateJson"];
        Game.State=original;Game.tuning=(Tuning)s["tuning"];
        var visits=Get<List<HabitatGame.VisitedLevel>>(Game,"visitedLevels");visits.Clear();visits.AddRange((HabitatGame.VisitedLevel[])s["visits"]);
        var history=Get<Stack<HabitatState>>(Game,"history");history.Clear();var undo=(HabitatState[])s["undo"];for(int i=undo.Length-1;i>=0;i--)history.Push(undo[i]);
        RestoreScalars(Game,(Dictionary<FieldInfo,object>)s["gameScalars"]);
        Game.View.Initialize(original);Game.View.Render(original,Game.tuning,Game.overlays);Refresh();
        foreach(var field in (Dictionary<string,object>)s["cameraFields"])Set(Game.View,field.Key,field.Value);
        var camera=Game.View.GameCamera;camera.transform.SetPositionAndRotation((Vector3)s["cameraPosition"],(Quaternion)s["cameraRotation"]);
        camera.orthographicSize=(float)s["cameraSize"];camera.orthographic=(bool)s["cameraOrtho"];
        camera.backgroundColor=(Color)s["cameraColor"];camera.clearFlags=(CameraClearFlags)s["cameraClear"];
        RestoreScalars(Game.Hud,(Dictionary<FieldInfo,object>)s["hudScalars"]);
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject((GameObject)s["selected"]);
        Time.timeScale=(float)s["timeScale"];
        RestoreAudioPreferences();
        bool restored=snapshotOkay&&ReferenceEquals(Game.State,original)&&Json(original)==(string)s["stateJson"]&&history.SequenceEqual(undo)
            &&Near(Sound.AmbienceVolume,(float)s["ambience"])&&Near(Sound.EffectsVolume,(float)s["effects"])
            &&Sound.Muted==(bool)s["muted"]&&Sound.enabled==(bool)s["enabled"]&&!Game.Busy;
        s["restored"]=restored;
        Check(restored,"original snapshot/state/undo/visits/pause/camera/timeScale and audio values restored; all three PlayerPrefs restore original value and existence");
    }
    static void RestoreAudioPreferences()
    {
        var s=Session;
        Sound.SetAmbienceVolume((float)s["ambience"]);Sound.SetEffectsVolume((float)s["effects"]);Sound.SetMuted((bool)s["muted"]);
        Sound.enabled=(bool)s["enabled"];
        if((bool)s["ambiencePresent"])PlayerPrefs.SetFloat(Prefix+"Ambience",(float)s["ambiencePref"]);else PlayerPrefs.DeleteKey(Prefix+"Ambience");
        if((bool)s["effectsPresent"])PlayerPrefs.SetFloat(Prefix+"Effects",(float)s["effectsPref"]);else PlayerPrefs.DeleteKey(Prefix+"Effects");
        if((bool)s["mutedPresent"])PlayerPrefs.SetInt(Prefix+"Muted",(int)s["mutedPref"]);else PlayerPrefs.DeleteKey(Prefix+"Muted");
        PlayerPrefs.Save();
        bool prefsOkay=PlayerPrefs.HasKey(Prefix+"Ambience")==((bool)s["ambiencePresent"])
            &&PlayerPrefs.HasKey(Prefix+"Effects")==((bool)s["effectsPresent"])
            &&PlayerPrefs.HasKey(Prefix+"Muted")==((bool)s["mutedPresent"])
            &&(!(bool)s["ambiencePresent"]||PlayerPrefs.GetFloat(Prefix+"Ambience").Equals((float)s["ambiencePref"]))
            &&(!(bool)s["effectsPresent"]||PlayerPrefs.GetFloat(Prefix+"Effects").Equals((float)s["effectsPref"]))
            &&(!(bool)s["mutedPresent"]||PlayerPrefs.GetInt(Prefix+"Muted")==((int)s["mutedPref"]));
        Require(prefsOkay,"PlayerPrefs original values/existence could not be restored.");
    }
}
