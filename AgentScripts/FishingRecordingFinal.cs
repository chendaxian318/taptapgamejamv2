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
public static class FishingRecordingFinal {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 const string Dir="Verification/fishing-recording-final-20261018";
 static HabitatGame g;static List<string> files;static List<double> times;static int index;static double nextCapture;static bool recording;
 static object Call(string name,params object[] args)=>typeof(HabitatGame).GetMethod(name,F).Invoke(g,args);
 static double Now=>Time.realtimeSinceStartupAsDouble;
 static void Capture(){if(!recording||Now<nextCapture)return;string p=Path.GetFullPath(Dir+"/frames/"+(index++).ToString("D5")+".png");files.Add(p);times.Add(Now);ScreenCapture.CaptureScreenshot(p);nextCapture=Now+1.0/20;}
 static async Task Frame(){int f=Time.frameCount;double end=Now+10;while(Time.frameCount==f){if(Now>end||!Application.isPlaying)throw new Exception("Frames interrupted");await Task.Delay(4);}Capture();}
 static async Task Wait(float seconds){double end=Now+seconds;while(Now<end)await Frame();}
 static string Hashes(string dir){using(var sha=SHA256.Create())return !Directory.Exists(dir)?"":string.Join("\n",Directory.GetFiles(dir,"*",SearchOption.AllDirectories).OrderBy(p=>p).Select(p=>p+":"+Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)))));}
 public static async Task<string> Run(){
  g=HabitatGame.Instance;if(!Application.isPlaying||!g||g.Busy)throw new Exception("Idle Play required");Directory.CreateDirectory(Dir+"/frames");files=new List<string>();times=new List<double>();index=0;
  object original=Call("CaptureSnapshot");bool paused=g.paused,session=g.SessionStarted,opening=g.playOpeningOnNewGame,fishEnabled=g.Fishing.enabled;int tool=g.tool;float speed=g.speed;
  var undo=(Stack<HabitatState>)typeof(HabitatGame).GetField("history",F).GetValue(g);var oldUndo=undo.ToArray();
  var flags=new[]{"chapterGreetingPending","chapterChestPending","chapterMorningGreetingPending","chapterReturnReminderShown","campaignReturnReminderLevel","autoSaveRequested"}.ToDictionary(n=>n,n=>typeof(HabitatGame).GetField(n,F).GetValue(g));
  var sf=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);object oldSave=sf.GetValue(null);
  string real=Path.Combine(Application.persistentDataPath,"Playtests"),beforeHash=Hashes(real);sf.SetValue(null,Path.GetFullPath(Dir+"/isolated-saves"));string result="incomplete";
  var log=new StringBuilder("Actual Unity gameplay capture. Existing legitimately earned first-island snapshot. Hold/release driven automatically through production Advance on real frames; no progress/success injection. Silent recording.\n");
  try{
   Call("RestoreSnapshot",JsonUtility.FromJson(File.ReadAllText("Verification/final-acceptance-20261018/player-smoke/first-earned-snapshot.json"),original.GetType()));
   typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);g.paused=true;g.speed=1;g.Hud.ClosePanels();g.View.FocusPlayer();
   await Wait(1);if(g.Hud.ChapterDialogueOpen)for(int i=0;i<10&&g.Hud.ChapterDialogueOpen;i++){g.Hud.AdvanceChapterDialogue();await Frame();}g.Hud.ClosePanels();await Wait(.5f);
   var fish=g.State.animals.First(a=>a.species==Species.Goldfish);if(!HabitatRules.CanHarvest(g.State,fish,g.tuning,out string reason))throw new Exception(reason);
   g.tool=0;g.Select(fish.x,fish.z);await Wait(.6f);
   recording=true;nextCapture=0;Capture();await Wait(2.0f);
   int before=g.State.glassFish;g.Fishing.enabled=false;g.ApplyTool();if(!g.Fishing.IsOpen)throw new Exception("Fishing did not open");log.AppendLine("Fishing start at "+(Now-times[0]));
   double end=Now+38,last=Now;float previous=g.Fishing.Fish;
   while(g.Fishing.IsOpen){
    if(Now>end)throw new Exception("Fishing timeout");float dt=(float)Math.Max(.005,Now-last);float derivative=(g.Fishing.Fish-previous)/dt;previous=g.Fishing.Fish;last=Now;
    float velocity=(float)typeof(HabitatFishing).GetField("velocity",F).GetValue(g.Fishing);float desired=Mathf.Clamp(derivative+5*(g.Fishing.Fish-g.Fishing.Bar),-.72f,.72f);bool held=velocity<desired;
    g.Fishing.Advance(Mathf.Min(Time.unscaledDeltaTime,.1f),held);
    if(g.Fishing.IsCasting&&g.Fishing.Progress!=.35f)throw new Exception("Casting changed challenge progress");
    if(g.State.glassFish!=before)throw new Exception("Premature reward during fishing");
    await Frame();
   }
   g.Fishing.enabled=fishEnabled;
   if(!g.FishingLanding||!g.Busy||g.State.glassFish!=before)throw new Exception("Success must enter pending landing without reward");
   log.AppendLine("Landing start at "+(Now-times[0]));
   Transform facing=(Transform)typeof(HabitatPlayerVisual).GetField("facingRoot",F).GetValue(g.View.PlayerVisual);Quaternion initialFacing=facing.rotation;float maxTurn=0;
   double arrival=Now+12;while(g.Busy||!g.View.ResidentsArrived(g.State)){if(Now>arrival)throw new Exception("Catch arrival timeout");
    if(g.FishingLanding)maxTurn=Mathf.Max(maxTurn,Quaternion.Angle(initialFacing,facing.rotation));
    if(g.FishingLanding&&g.State.glassFish!=before)throw new Exception("Premature reward before fish lands");
    await Frame();}
   if(g.State.glassFish!=before+1)throw new Exception("Catch did not award exactly one fish: "+g.State.message);
   if(maxTurn>2)throw new Exception("Unexpected turn during landing: "+maxTurn);log.AppendLine("Landing yaw max="+maxTurn);
   log.AppendLine("Successful actual catch +1 at "+(Now-times[0])+" message="+g.State.message);await Wait(3.5f);recording=false;await Wait(1);
   foreach(string f in files)if(!File.Exists(f))throw new Exception("Missing captured frame "+f);
   var concat=new StringBuilder();for(int i=0;i<files.Count;i++){concat.AppendLine("file '"+files[i].Replace('\\','/')+"'");concat.AppendLine("duration "+(i+1<times.Count?times[i+1]-times[i]:1.0/20).ToString("F6",System.Globalization.CultureInfo.InvariantCulture));}concat.AppendLine("file '"+files.Last().Replace('\\','/')+"'");File.WriteAllText(Dir+"/frames.ffconcat",concat.ToString(),new UTF8Encoding(false));
   result="PASS recorded "+files.Count+" actual frames, "+(times.Last()-times.First()).ToString("F2")+" seconds; caught exactly one fish";
  }catch(Exception ex){result="FAIL "+ex;}
  finally{
   recording=false;g.Fishing.Cancel(false);g.Fishing.enabled=fishEnabled;g.CancelPendingAction();g.Hud.ClosePanels();Call("RestoreSnapshot",original);undo.Clear();for(int i=oldUndo.Length-1;i>=0;i--)undo.Push(oldUndo[i]);foreach(var p in flags)typeof(HabitatGame).GetField(p.Key,F).SetValue(g,p.Value);g.paused=paused;g.playOpeningOnNewGame=opening;g.tool=tool;g.speed=speed;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);
   bool unchanged=beforeHash==Hashes(real);sf.SetValue(null,oldSave);if(!session)g.Hud.ShowStartMenu();log.AppendLine(unchanged?"PASS original save SHA256 unchanged":"FAIL original saves changed");if(!unchanged)result="FAIL original saves changed";log.AppendLine(result);File.WriteAllText(Dir+"/recording.txt",log.ToString());
  }return result;
 }
}
