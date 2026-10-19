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

// Load with Unity CLI run_script, invoke AudioRuntimeChecks.Begin(), then poll Status.
// Outside Assets. Exercises live audio/UI but does not write user game saves.
// DSP samples establish signal output, not subjective listening quality.
public static class AudioRuntimeChecks
{
    const string Key="Habitat.AudioRuntimeChecks.20261016";
    const string Prefix="HabitatIsland.Audio.";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly string Folder=Path.GetFullPath("Logs/audio-20261016");
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
    static void Flush(){Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"runtime.txt"),Report.ToString());}
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
                +" | "+Path.Combine(Folder,"runtime.txt")+(s.ContainsKey("error")?" | "+s["error"]:"");
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
    static async Task Run()
    {
        Exception failure=null;
        try
        {
            Phase("Initial resources and isolated paused fixture");
            Check(Near(Sound.AmbienceVolume,PreferenceVolume("Ambience",.25f))
                &&Near(Sound.EffectsVolume,PreferenceVolume("Effects",.5f))
                &&Sound.Muted==(PlayerPrefs.GetInt(Prefix+"Muted",0)!=0),"initial volumes and mute use stored preferences or .25/.5/unmuted defaults");
            string[] paths={"Audio/Ambience/SeaWavesBirds","Audio/UI/Click","Audio/UI/Open","Audio/UI/Close","Audio/UI/Confirm","Audio/UI/Error"};
            foreach(string path in paths)
            {
                var clip=Resources.Load<AudioClip>(path);Require(clip,"Missing clip "+path);
                if(clip.loadState==AudioDataLoadState.Unloaded)clip.LoadAudioData();
                await Until(()=>clip.loadState==AudioDataLoadState.Loaded,8,"clip loaded: "+path);
                Check(clip.samples>0&&clip.frequency==44100,"loaded valid 44.1kHz clip "+path+" ("+clip.length.ToString("F3")+" seconds)");
            }
            Check(Instances()==1&&Sound.GetComponents<AudioSource>().Length==3&&ActiveListeners()==1,"one audio service, three sources, one active listener");
            Check(Sound.GetComponents<AudioSource>().All(x=>Near(x.spatialBlend,0)&&!x.playOnAwake&&!x.loop),"all three sources use 2D presentation and explicit scheduled playback");
            Time.timeScale=1;Game.Hud.ClosePanels();
            var fixture=JsonUtility.FromJson(Json(Session["snapshot"]),Session["snapshot"].GetType());
            Set(fixture,"state",HabitatLevels.Create(5));
            Call(Game,"RestoreSnapshot",fixture);Game.paused=true;
            Set(Game,"chapterGreetingPending",false);Set(Game,"chapterChestPending",false);Set(Game,"chapterMorningGreetingPending",false);
            Sound.enabled=true;Sound.SetAmbienceVolume(.2f);Sound.SetEffectsVolume(.4f);Sound.SetMuted(false);
            Report.AppendLine("FIXTURE: close pre-existing modal; clone snapshot and use formal level 5 to avoid tutorial/story triggers; ecology paused; temporary volumes .20/.40 never exceed defaults .25/.50. Restore prefs including absent keys in finally.");Flush();

            Phase("Live settings button callbacks and independent channels");
            await Click("Open Settings");await Click("Open Audio Settings");
            Check(Get<GameObject>(Game.Hud,"audioPanel").activeInHierarchy&&Game.paused,"settings opens the live audio panel and keeps ecology paused");
            await Click("Ambience volume down");
            Check(Near(Sound.AmbienceVolume,.15f)&&Near(Sound.EffectsVolume,.4f),"ambience minus changes only ambience to .15");
            await Click("Ambience volume up");
            Check(Near(Sound.AmbienceVolume,.2f)&&Near(Sound.EffectsVolume,.4f),"ambience plus returns only ambience to .20");
            await Click("Effects volume down");
            Check(Near(Sound.EffectsVolume,.35f)&&Near(Sound.AmbienceVolume,.2f)&&Near(Effects.volume,.35f),"effects minus changes only effects and immediately applies source volume .35");
            await Click("Effects volume up");
            Check(Near(Sound.EffectsVolume,.4f)&&Near(Sound.AmbienceVolume,.2f)&&Near(Effects.volume,.4f),"effects plus returns only effects and source to .40");
            await Seconds(.15);int cues=Sound.CuePlayCount;await Click("Preview Audio Cue");
            Check(Sound.CuePlayCount==cues+1&&Sound.LastPlayedCue==HabitatAudioCue.Confirm,"preview button combines generic click with exactly one confirmation cue");
            double effectPeak=0;double sampleUntil=Clock+.25;var buffer=new float[1024];
            while(Clock<sampleUntil){effectPeak=Math.Max(effectPeak,PeakOutput(Effects,buffer));await Frames(1);}
            Check(effectPeak>1e-7,"preview produces nonzero AudioSource.GetOutputData (peak="+effectPeak.ToString("G4")+")");
            await Screenshot();
            await Click("Toggle Audio Mute");
            Check(Sound.Muted&&Sound.GetComponents<AudioSource>().All(x=>Near(x.volume,0)),"mute immediately sets all three source volumes to zero");
            cues=Sound.CuePlayCount;
            await Click("Preview Audio Cue");HabitatAudio.Play(HabitatAudioCue.Error);await Frames(3);
            Check(Sound.CuePlayCount==cues,"muted preview and explicit error do not enqueue/play or increment cue count");
            await Click("Toggle Audio Mute");
            Check(!Sound.Muted&&Near(Effects.volume,.4f),"unmute restores effects source volume");
            await Click("Back from Audio Settings");
            Check(Get<GameObject>(Game.Hud,"settingsPanel").activeInHierarchy&&!Get<GameObject>(Game.Hud,"audioPanel").activeInHierarchy,"back button returns from audio panel to settings");
            Game.Hud.ClosePanels();Game.paused=true;await Seconds(.15);
            cues=Sound.CuePlayCount;
            HabitatAudio.Play(HabitatAudioCue.Click);HabitatAudio.Play(HabitatAudioCue.Open);HabitatAudio.Play(HabitatAudioCue.Confirm);await Frames(3);
            Check(Sound.CuePlayCount==cues+1&&Sound.LastPlayedCue==HabitatAudioCue.Confirm,"same-frame Click + Open + Confirm plays exactly one highest-priority Confirm");

            Phase("Disable/enable, singleton and natural equal-power loop with ecology paused");
            int cycles=Sound.AmbienceCycleCount;Sound.enabled=false;await Frames(2);
            Check(!Sound.AmbienceRunning&&Sound.GetComponents<AudioSource>().All(x=>!x.isPlaying),"disabling service stops every source and scheduling");
            Sound.enabled=true;
            await Until(()=>Sound.AmbienceRunning&&Sound.AmbienceCycleCount>cycles,8,"reenabled first ambience cycle");
            Check(Sound.AmbienceCycleCount==cycles+1,"enable restarts one scheduled ambience cycle");
            for(int i=0;i<12;i++)Require(HabitatAudio.Ensure()==Sound,"Ensure changed service identity.");
            Check(Instances()==1&&Sound.GetComponents<AudioSource>().Length==3,"repeated Ensure keeps the same service and three sources");
            await Seconds(1.35);
            float[] data=new float[1024];double ambientPeak=0,maxPowerError=0;int samples=0,overlap=0,gaps=0;
            int firstCycles=Sound.AmbienceCycleCount;double firstDsp=AudioSettings.dspTime,startWall=Clock;
            int firstSample=Ambient[0].timeSamples;double deadline=Clock+45;string pausedState=Json(Game.State);
            while(Sound.AmbienceCycleCount<=firstCycles||AudioSettings.dspTime<Get<double[]>(Sound,"ambienceStarts")[1]+1.25)
            {
                Require(Clock<deadline,"Timed out waiting for natural sea loop handoff.");
                Require(Game.paused,"Ecology unexpectedly resumed during audio loop check.");
                float v0=Ambient[0].volume,v1=Ambient[1].volume,power=v0*v0+v1*v1;
                maxPowerError=Math.Max(maxPowerError,Math.Abs(power-.04f));
                if(v0>.001f&&v1>.001f)overlap++;
                if(v0<=.00001f&&v1<=.00001f)gaps++;
                for(int i=0;i<2;i++)if(Ambient[i].isPlaying&&Ambient[i].volume>.001f)ambientPeak=Math.Max(ambientPeak,PeakOutput(Ambient[i],data));
                samples++;await Frames(1);
            }
            Check(Clock-startWall>30&&Sound.AmbienceCycleCount>=firstCycles+1,"observed a real natural ~38-second clip cycle handoff without seeking or shortening clip");
            Check(overlap>0&&maxPowerError<.001,"two sources overlap with volume squares summing to ambienceVolume squared (samples="+samples+", overlap="+overlap+", max error="+maxPowerError.ToString("G4")+")");
            Check(gaps==0,"no both-sources-silent frame after initial fade across natural loop");
            Check(ambientPeak>1e-7&&AudioSettings.dspTime>firstDsp+30&&Ambient[0].timeSamples!=firstSample,"actual environment source signal and DSP/sample position advance (peak="+ambientPeak.ToString("G4")+")");
            Check(Game.paused&&Json(Game.State)==pausedState,"environment progresses while paused ecology state stays identical");

            Phase("Level loading/restart do not duplicate audio infrastructure");
            var ids=Sound.GetComponents<AudioSource>().Select(x=>x.GetEntityId()).ToArray();
            Game.LoadLevel(6);Game.paused=true;await Frames(4);
            Check(HabitatAudio.Ensure()==Sound&&Instances()==1&&Sound.GetComponents<AudioSource>().Select(x=>x.GetEntityId()).SequenceEqual(ids)&&ActiveListeners()==1,"LoadLevel retains the same three sources, singleton and listener");
            Set(Game,"chapterGreetingPending",false);Set(Game,"chapterChestPending",false);Set(Game,"chapterMorningGreetingPending",false);
            Game.RestartCurrentLevel();Game.paused=true;await Frames(4);
            Check(HabitatAudio.Ensure()==Sound&&Instances()==1&&Sound.GetComponents<AudioSource>().Select(x=>x.GetEntityId()).SequenceEqual(ids)&&ActiveListeners()==1,"RestartCurrentLevel retains the same three sources, singleton and listener");
            Check(Sound.AmbienceRunning&&Ambient.Any(x=>x.isPlaying&&x.volume>.001f),"ambience remains active across load/restart");
            Report.AppendLine("LIMITS: signal output and real UI callbacks were verified, not subjective hearing, physical speakers/headphones, full puzzle playthrough, device hotplug or suspend/resume. No user game save written. Original modal intentionally remains closed.");
        }
        catch(Exception e){failure=e;Session["error"]=e.Message;Report.AppendLine("ERROR "+e);}
        finally
        {
            try{RestoreOriginal();}
            catch(Exception e){failure=failure??e;Session["error"]=e.Message;Report.AppendLine("RESTORE ERROR "+e);}
            // Restore preferences even if view/snapshot restoration itself failed.
            try{RestoreAudioPreferences();}
            catch(Exception e){failure=failure??e;Session["error"]=e.Message;Report.AppendLine("AUDIO RESTORE ERROR "+e);}
            Session["running"]=false;Session["status"]=failure==null?"PASS":"FAILED";Session["phase"]="finished";Flush();
        }
    }
    static void RestoreOriginal()
    {
        var s=Session;var original=(HabitatState)s["state"];
        Game.CancelPendingAction();if(Game.Fishing&&Game.Fishing.IsOpen)Game.Fishing.Cancel();Game.Hud.ClosePanels();
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
