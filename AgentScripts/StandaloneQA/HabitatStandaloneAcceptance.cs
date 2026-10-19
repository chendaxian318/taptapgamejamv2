using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;
using HabitatIsland;
using Newtonsoft.Json;

// Explicit QA only. Normal launches do not create this component or touch test files.
public sealed class HabitatStandaloneAcceptance : MonoBehaviour
{
    public static string OutputDirectory;
    public static bool KeepAutomationActive=true;
    static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    readonly List<string> errors=new List<string>();
    readonly List<object> results=new List<object>();
    readonly List<object> performance=new List<object>();
    readonly Dictionary<string,List<double>> journeyFrames=new Dictionary<string,List<double>>();
    bool samplingJourney;
    HabitatGame game;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var args=Environment.GetCommandLineArgs();
        if(Array.IndexOf(args,"--habitat-acceptance")<0)return;
        int p=Array.IndexOf(args,"--habitat-acceptance-dir");
        OutputDirectory=Path.GetFullPath(p>=0&&p+1<args.Length?args[p+1]:Path.Combine(Application.persistentDataPath,"StandaloneAcceptance"));
        Directory.CreateDirectory(OutputDirectory);
        HabitatGame.SaveSlotDirectoryForTests=Path.Combine(OutputDirectory,"saves");
        DontDestroyOnLoad(new GameObject("Explicit standalone acceptance").AddComponent<HabitatStandaloneAcceptance>());
    }
    void OnEnable(){Application.logMessageReceived+=Log;}
    void OnDisable(){Application.logMessageReceived-=Log;}
    // The user's desktop remains usable during this opt-in run. Explicit focus tests
    // temporarily disable this override and still verify the real background pause.
    void LateUpdate(){if(KeepAutomationActive&&game&&game.BackgroundSuspended)game.SetBackgroundSuspended(false);
        if(samplingJourney&&game){string key=game.StoryPlaying?"story-and-travel":game.Fishing.IsOpen?"fishing":game.Busy?"movement-and-actions":"idle-and-ecology";
            if(!journeyFrames.TryGetValue(key,out var list))journeyFrames[key]=list=new List<double>();list.Add(Time.unscaledDeltaTime*1000.0);}}
    void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(type+": "+message+"\n"+stack);}
    static async Task Frame(){int f=Time.frameCount;while(Time.frameCount==f)await Task.Delay(4);}
    static async Task Frames(int n){for(int i=0;i<n;i++)await Frame();}
    void Write(string name,object value)=>File.WriteAllText(Path.Combine(OutputDirectory,name+".json"),JsonConvert.SerializeObject(value,Formatting.Indented));
    static Dictionary<string,string> Hashes(string root)
    {
        var d=new Dictionary<string,string>();if(Directory.Exists(root))foreach(var p in Directory.GetFiles(root,"*",SearchOption.AllDirectories))using(var sha=SHA256.Create())d[p]=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p)));return d;
    }
    static object Stats(IEnumerable<double> data)
    {
        var a=data.OrderBy(v=>v).ToArray();return new{count=a.Length,mean=a.Average(),median=a[a.Length/2],p95=a[(int)((a.Length-1)*.95)],p99=a[(int)((a.Length-1)*.99)],max=a.Last()};
    }
    async Task Capture(string name)
    {
        await Frames(3);string path=Path.Combine(OutputDirectory,name+".png");ScreenCapture.CaptureScreenshot(path);
        double deadline=Time.realtimeSinceStartupAsDouble+8;
        do{await Frame();}while(!File.Exists(path)&&Time.realtimeSinceStartupAsDouble<deadline);
        if(!File.Exists(path))throw new Exception("Screenshot absent: "+name);
    }
    async Task Test(string label,Func<Task<string>> run)
    {
        try{string report=await run();results.Add(new{name=label,pass=report.StartsWith("PASS"),report});}
        catch(Exception e){results.Add(new{name=label,pass=false,report=e.ToString()});await Capture("failed-"+label);}
        Write("checks",results);
    }
    Task<string> InteractionTextChecks()
    {
        var s=HabitatLevels.Create(5);s.playerX=7;s.playerZ=4;
        var fish=s.animals.First(a=>a.species==Species.Goldfish);int x=fish.x,z=fish.z;
        if(HabitatRules.Interact(s,x,z)||!s.message.Contains("捕捞")||s.message.Contains("引导")||fish.x!=x||fish.z!=z||fish.carried)
            throw new Exception("Confirmed-plan fish interaction message or state is incorrect");
        s=HabitatLevels.Create(9);s.sourceNodeId="legacy-optional";s.playerX=9;s.playerZ=15;
        fish=s.animals.First(a=>a.species==Species.Goldfish);
        if(HabitatRules.Interact(s,fish.x,fish.z)||!s.message.Contains("引导"))throw new Exception("Legacy fish message changed");
        return Task.FromResult("PASS 2 current/legacy fish-interaction text checks; no movement, carrying or rule change");
    }
    async Task<string> NormalSpeedBreeding()
    {
        var capture=typeof(HabitatGame).GetMethod("CaptureSnapshot",F);
        var restore=typeof(HabitatGame).GetMethod("RestoreSnapshot",F);
        var original=capture.Invoke(game,null);bool session=game.SessionStarted,pause=game.paused;float speed=game.speed;
        try
        {
            string file=Path.Combine(OutputDirectory,"first-earned-snapshot.json");
            if(!File.Exists(file))file=Path.Combine(OutputDirectory,"campaign/full-campaign-first.json");
            restore.Invoke(game,new[]{JsonUtility.FromJson(File.ReadAllText(file),original.GetType())});
            typeof(HabitatGame).GetProperty("SessionStarted").SetValue(game,true);
            game.Hud.ClosePanels();game.speed=1;game.paused=false;
            var fish=game.State.animals.First(a=>a.species==Species.Goldfish);int before=fish.offspring;
            double start=Time.realtimeSinceStartupAsDouble,end=start+game.tuning.breedSeconds*3+15;
            while(game.State.animals.First(a=>a.id==fish.id).offspring<before+2)
            {
                // Restoring a just-completed island can show its normal return reminder.
                // Dismiss it like the campaign driver, then resume ecology at its real speed.
                if(game.Hud.ModalOpen)game.Hud.ClosePanelsFromInput();
                if(game.paused&&!game.Busy)game.TogglePause();
                if(Time.realtimeSinceStartupAsDouble>end)throw new Exception("Normal-speed breeding did not complete two real cycles; paused="+game.paused+" modal="+game.Hud.ModalOpen+" busy="+game.Busy+" elapsed="+game.State.elapsed+" offspring="+game.State.animals.First(a=>a.id==fish.id).offspring);
                await Frame();
            }
            return "PASS 1 normal 1x breeding check: two natural offspring in "+(Time.realtimeSinceStartupAsDouble-start).ToString("F2")+" wall seconds, original earned island snapshot, no time/inventory injection";
        }
        finally{game.CancelPendingAction();game.Hud.ClosePanels();restore.Invoke(game,new[]{original});game.speed=speed;game.paused=pause;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(game,session);}
    }
    async Task Benchmark()
    {
        game.playOpeningOnNewGame=false;game.NewSession();
        foreach(int level in new[]{11,5,6,7,8,10,4,9})
        {
            string earned=Path.Combine(OutputDirectory,"campaign",level==11?"main-ending.json":"full-campaign-"+(level==5?"first":level.ToString())+".json");
            if(File.Exists(earned)){
                var original=typeof(HabitatGame).GetMethod("CaptureSnapshot",F).Invoke(game,null);
                typeof(HabitatGame).GetMethod("RestoreSnapshot",F).Invoke(game,new[]{JsonUtility.FromJson(File.ReadAllText(earned),original.GetType())});
            }else game.LoadLevel(level);
            game.paused=true;game.Hud.ClosePanels();game.tool=0;
            foreach(string field in new[]{"chapterGreetingPending","chapterChestPending","chapterMorningGreetingPending"})typeof(HabitatGame).GetField(field,F).SetValue(game,false);
            game.State.seenGuideKeys=new List<string>{"move","pull","climb","rod","basket","fan","crab","jellyfish","tide","shovel","magnet","launcher"};
            foreach(string view in level==11||level==8||level==5?new[]{"overview","player"}:new[]{"overview"})
            {
                if(view=="overview")game.View.FocusOverview();else game.View.FocusPlayer();
                game.Hud.ClosePanels();await Frames(90);await Capture("level-"+level+"-"+view);
                var frame=new List<double>();var gc=new List<double>();var draws=new List<double>();var triangles=new List<double>();
                using(var allocated=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1))
                using(var draw=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1))
                using(var tris=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",1))
                {
                    for(int i=0;i<300;i++)
                    {
                        await Frame();frame.Add(Time.unscaledDeltaTime*1000.0);
                        if(allocated.Valid)gc.Add(allocated.LastValue);if(draw.Valid)draws.Add(draw.LastValue);if(tris.Valid)triangles.Add(tris.LastValue);
                    }
                }
                performance.Add(new{level,view,resolution=new{Screen.width,Screen.height},frameMilliseconds=Stats(frame),meanFps=1000/frame.Average(),framesOver33ms=frame.Count(v=>v>33.34),gcBytes=gc.Count>0?Stats(gc):null,drawCalls=draws.Count>0?Stats(draws):null,triangles=triangles.Count>0?Stats(triangles):null,allocatedMemory=Profiler.GetTotalAllocatedMemoryLong(),reservedMemory=Profiler.GetTotalReservedMemoryLong(),managedMemory=GC.GetTotalMemory(false),note="Release EXE real rendered frames, includes vsync/frame cap and small opt-in sampling overhead; no artificial logic acceleration. Null counters unavailable in this release build."});
                Write("performance",performance);
            }
        }
    }
    async void Start()
    {
        var saves=Path.Combine(Application.persistentDataPath,"Playtests");var before=Hashes(saves);bool pass=true;
        bool checksOnly=Array.IndexOf(Environment.GetCommandLineArgs(),"--habitat-acceptance-checks-only")>=0;
        try
        {
            await Frames(60);game=HabitatGame.Instance;if(!game)throw new Exception("Game did not initialize");
            Write("environment",new{utc=DateTime.UtcNow,unity=Application.unityVersion,isEditor=Application.isEditor,version=Application.version,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,ramMB=SystemInfo.systemMemorySize,vramMB=SystemInfo.graphicsMemorySize,screen=new{Screen.width,Screen.height},QualitySettings.vSyncCount,Application.targetFrameRate,scope="Opt-in standalone QA; gameplay fixtures are explicitly isolated and are not campaign completion proof."});
            await Capture("main-menu");
            foreach(string folder in new[]{"input","recovery","utility","mouse"})Directory.CreateDirectory(Path.Combine(OutputDirectory,folder));
            if(!checksOnly){samplingJourney=true;await Test("campaign",StandaloneCampaignChecks.Run);samplingJourney=false;
                Write("journey-performance",journeyFrames.Select(p=>new{scenario=p.Key,frameMilliseconds=Stats(p.Value),meanFps=1000/p.Value.Average(),framesOver33ms=p.Value.Count(v=>v>33.34),note="Actual campaign EXE frames including loading and first-use hitches; ecological waits use internal 4x speed, animation and render time are unscaled."}).ToArray());}
            await Test("interaction-text",InteractionTextChecks);
            await Test("normal-speed-breeding",NormalSpeedBreeding);
            await Test("input",StandaloneInputChecks.Run);
            await Test("mouse",StandaloneMouseChecks.Run);
            await Test("recovery",StandaloneRecoveryChecks.Run);
            await Test("utility",StandaloneUtilityChecks.Run);
            await Benchmark();
        }
        catch(Exception e){pass=false;File.WriteAllText(Path.Combine(OutputDirectory,"failure.txt"),e.ToString());}
        finally
        {
            var after=Hashes(saves);bool unchanged=before.Count==after.Count&&before.All(p=>after.TryGetValue(p.Key,out var v)&&v==p.Value);
            pass=pass&&unchanged&&errors.Count==0&&results.Count==(checksOnly?6:7)&&results.All(r=>(bool)r.GetType().GetProperty("pass").GetValue(r));
            Write("result",new{pass,checksOnly,originalSavesUnchanged=unchanged,checks=results,performanceScenarios=performance.Count,errors,finished=DateTime.UtcNow});
            Application.Quit(pass?0:2);
        }
    }
}
