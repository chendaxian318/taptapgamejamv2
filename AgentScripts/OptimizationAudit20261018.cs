using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using UnityEngine;
using UnityEditor;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine.Profiling;
using HabitatIsland;
using Newtonsoft.Json;

// Temporary diagnostic, not game production code. Restores game snapshot and presentation settings.
public static class OptimizationAudit20261018
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 const string Dir="Verification/audit-optimization-20261018";
 static HabitatGame g;static double deadline;
 static object Call(string n,params object[] a)=>typeof(HabitatGame).GetMethod(n,F).Invoke(g,a);
 static async Task Frames(int n){int end=Time.frameCount+n;while(Time.frameCount<end){if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("audit deadline");await Task.Delay(8);}}
 static void Write(string name,object data)=>File.WriteAllText(Dir+"/"+name+".json",JsonConvert.SerializeObject(data,Formatting.Indented));
 static object Stats(IEnumerable<double> source){var a=source.OrderBy(x=>x).ToArray();return new{n=a.Length,mean=a.Average(),median=a[a.Length/2],p95=a[(int)((a.Length-1)*.95)],max=a[a.Length-1]};}
 static object Bench(string name,Action action,int count=100){for(int i=0;i<5;i++)action();long bytes=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();for(int i=0;i<count;i++)action();sw.Stop();return new{name,count,millisecondsPerCall=sw.Elapsed.TotalMilliseconds/count,allocatedBytesPerCall=(GC.GetAllocatedBytesForCurrentThread()-bytes)/(double)count};}
 static object Inventory()
 {
  var rr=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
  var meshes=rr.OfType<SkinnedMeshRenderer>().Where(r=>r.sharedMesh).Select(r=>new{r.name,vertices=r.sharedMesh.vertexCount,indices=Enumerable.Range(0,r.sharedMesh.subMeshCount).Sum(i=>(long)r.sharedMesh.GetIndexCount(i)),r.updateWhenOffscreen,r.quality}).ToArray();
  var tex=new HashSet<Texture>();foreach(var r in rr)foreach(var m in r.sharedMaterials){if(!m)continue;foreach(int id in m.GetTexturePropertyNameIDs()){var t=m.GetTexture(id);if(t)tex.Add(t);}}
  return new{activeRenderers=rr.Length,meshRenderers=rr.OfType<MeshRenderer>().Count(),skinned=meshes,activeMaterials=rr.SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().Count(),lodGroups=UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude).Length,
   sceneMaterialTextures=tex.OfType<Texture2D>().Select(t=>new{t.name,path=AssetDatabase.GetAssetPath(t),t.width,t.height,format=t.format.ToString(),t.mipmapCount,t.isReadable,runtimeBytes=Profiler.GetRuntimeMemorySizeLong(t)}).OrderByDescending(t=>t.runtimeBytes).ToArray(),
   loadedEditorTextures=Resources.FindObjectsOfTypeAll<Texture2D>().Select(t=>new{t.name,path=AssetDatabase.GetAssetPath(t),t.width,t.height,format=t.format.ToString(),t.isReadable,runtimeBytes=Profiler.GetRuntimeMemorySizeLong(t)}).OrderByDescending(t=>t.runtimeBytes).Take(40).ToArray()};
 }
 public static async Task<string> Run()
 {
  g=HabitatGame.Instance;if(!g||!Application.isPlaying||g.Busy)throw new Exception("idle Play required");Directory.CreateDirectory(Dir);deadline=EditorApplication.timeSinceStartup+190;
  var snapshot=Call("CaptureSnapshot");bool session=g.SessionStarted,pause=g.paused,opening=g.playOpeningOnNewGame,overlays=g.overlays;int tool=g.tool;var history=(Stack<HabitatState>)typeof(HabitatGame).GetField("history",F).GetValue(g);var oldHistory=history.ToArray();
  var chapter=new[]{"chapterGreetingPending","chapterChestPending","chapterMorningGreetingPending","chapterReturnReminderShown","campaignReturnReminderLevel"}.ToDictionary(n=>n,n=>typeof(HabitatGame).GetField(n,F).GetValue(g));
  var runs=new List<object>();var benches=new List<object>();
  var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);var descriptions=handles.Select(ProfilerRecorderHandle.GetDescription).ToArray();
  Write("available-counters",descriptions.Select(d=>new{d.Name,category=d.Category.Name,unit=d.UnitType.ToString()}).Where(d=>d.Name.Contains("Memory")||d.Name.Contains("Habitat")||d.Name.Contains("Frame")||d.Name.Contains("Batches")||d.Name.Contains("Thread")||d.Name.Contains("Alloc")));
  var wanted=new[]{"Main Thread","CPU Main Thread Frame Time","Render Thread","GC Allocated In Frame","GC Used Memory","Total Used Memory","Draw Calls Count","Batches Count","SetPass Calls Count","Triangles Count","PlayerLoop","BehaviourUpdate","LateBehaviourUpdate"};
  var rec=new Dictionary<string,ProfilerRecorder>();foreach(string name in wanted){var d=descriptions.FirstOrDefault(v=>v.Name==name);if(string.IsNullOrEmpty(d.Name))continue;var r=ProfilerRecorder.StartNew(d.Category,name,1);if(r.Valid)rec[name]=r;else r.Dispose();}
  try{
   typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);g.playOpeningOnNewGame=false;
   Write("environment",new{unity=Application.unityVersion,screen=new{Screen.width,Screen.height},cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,ramMB=SystemInfo.systemMemorySize,vramMB=SystemInfo.graphicsMemorySize,quality=QualitySettings.names[QualitySettings.GetQualityLevel()],QualitySettings.vSyncCount,Application.targetFrameRate,pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name,scope="Unity Editor Play mode; NOT standalone player benchmark. Recorders may include Editor and diagnostic overhead. Memory is object/runtime estimate, not exclusive VRAM residency."});
   foreach(int level in new[]{5,8,10,4}){
    g.LoadLevel(level);g.paused=true;g.Hud.ClosePanels();g.tool=0;g.View.FocusOverview();g.State.openingCompleted=true;
    foreach(string n in new[]{"chapterGreetingPending","chapterChestPending","chapterMorningGreetingPending"})typeof(HabitatGame).GetField(n,F).SetValue(g,false);
    g.State.seenGuideKeys=new List<string>{"move","pull","climb","rod","basket","fan","crab","jellyfish","tide","shovel","magnet","launcher"};
    await Frames(20);g.Hud.ClosePanels();
    foreach(string mode in level==8?new[]{"overview-paused","close-paused","overview-ecology"}:new[]{"overview-paused"}){
     if(mode=="close-paused")g.View.FocusPlayer();else g.View.FocusOverview();g.paused=mode!="overview-ecology";await Frames(20);
     var frames=new double[150];var draw=new double[150];var batches=new double[150];var tris=new double[150];var samples=rec.ToDictionary(p=>p.Key,p=>new double[150]);
     for(int i=0;i<150;i++){await Frames(1);frames[i]=Time.unscaledDeltaTime*1000;draw[i]=UnityStats.drawCalls;batches[i]=rec.ContainsKey("Batches Count")?rec["Batches Count"].LastValue:-1;tris[i]=UnityStats.triangles;foreach(var p in rec)samples[p.Key][i]=p.Value.LastValue;}
     runs.Add(new{level,mode,frameMilliseconds=Stats(frames),drawCalls=Stats(draw),batches=Stats(batches),triangles=Stats(tris),recorderValues=samples.ToDictionary(p=>p.Key,p=>Stats(p.Value)),note="frame time includes frame cap/vsync/editor; statistics are observed Editor render counters, not isolated GPU timings"});Write("runtime-samples",runs);
    }
    g.paused=true;var s=HabitatRules.Clone(g.State);
    benches.Add(Bench("level"+level+" Clone",()=>HabitatRules.Clone(s)));
    benches.Add(Bench("level"+level+" evaluate all",()=>{foreach(var a in s.animals)HabitatRules.Evaluate(s,a,g.tuning);}));
    var before=HabitatRules.Clone(s);benches.Add(Bench("level"+level+" clone+Tick .016",()=>HabitatRules.Tick(HabitatRules.Clone(before),g.tuning,.016f)));
    Write("inventory-level-"+level,Inventory());
    ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/level-"+level+".png"));await Frames(3);
   }
   Write("isolated-rule-benchmarks",benches);
   return "PASS 6 Editor sampling scenarios, 4 runtime resource inventories and 12 isolated rule microbenchmarks; no production edits";
  }finally{foreach(var r in rec.Values)r.Dispose();g.CancelPendingAction();g.Hud.ClosePanels();Call("RestoreSnapshot",snapshot);history.Clear();for(int i=oldHistory.Length-1;i>=0;i--)history.Push(oldHistory[i]);foreach(var p in chapter)typeof(HabitatGame).GetField(p.Key,F).SetValue(g,p.Value);g.playOpeningOnNewGame=opening;g.paused=pause;g.tool=tool;g.overlays=overlays;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);}
 }
}
