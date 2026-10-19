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
using UnityEngine.InputSystem;
public static class FishingPresentationChecks {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 const string Dir="Verification/fishing-presentation-checks-20261018";
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
  var oldKeyboard=Keyboard.current;var inputBackground=InputSystem.settings.backgroundBehavior;var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
  InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;var keyboard=InputSystem.AddDevice<Keyboard>("FishingQAKeyboard");InputSystem.EnableDevice(keyboard);keyboard.MakeCurrent();
  try{
   object earned=JsonUtility.FromJson(File.ReadAllText("Verification/final-acceptance-20261018/player-smoke/first-earned-snapshot.json"),original.GetType());
   Action<bool,string> check=(ok,name)=>{log.AppendLine((ok?"PASS ":"FAIL ")+name);if(!ok)throw new Exception(name);};
   Action restore=()=>{g.Fishing.Cancel(false);g.CancelPendingAction();Call("RestoreSnapshot",earned);typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);g.Hud.ClosePanels();g.paused=true;g.tool=4;typeof(HabitatGame).GetField("chapterGreetingPending",F).SetValue(g,false);typeof(HabitatGame).GetField("campaignReturnReminderLevel",F).SetValue(g,5);};
   restore();await Wait(.25f);g.Hud.ClosePanels();
   var fish=g.State.animals.First(a=>a.species==Species.Goldfish);int id=fish.id,x=fish.x,z=fish.z;
   int baseline=g.State.glassFish,actions=g.ActionCount;
   check(g.Fishing.Begin(g,x,z),"casting starts");await Wait(.25f);
   check(g.Fishing.IsCasting&&g.Fishing.Progress==.35f&&g.State.glassFish==baseline,"cast does not advance minigame/reward");
   g.Fishing.Cancel(false);check(!g.Busy&&!g.Fishing.IsOpen&&g.View.PlayerVisual.FishingPoseStage==0&&g.State.glassFish==baseline,"cast cancellation clears pose without reward");
   foreach(float cancelAt in new[]{.2f,1.35f,2.4f}){
    restore();await Wait(.2f);g.Hud.ClosePanels();actions=g.ActionCount;
    g.CompleteFishing(x,z,Species.Goldfish,id);check(g.FishingLanding&&g.Busy,"landing enters pending transaction");
    await Wait(cancelAt);check(g.State.glassFish==baseline&&!g.State.animals.First(a=>a.id==id).catchClaimed&&g.ActionCount==actions,"no premature reward/claim/action at "+cancelAt);
    g.CompleteFishing(x,z,Species.Goldfish,id);check(g.State.glassFish==baseline,"duplicate callback while pending ignored");
    g.CancelPendingAction();await Wait(.15f);check(!g.Busy&&!g.FishingCatchVisual&&g.View.PlayerVisual.FishingPoseStage==0&&g.State.glassFish==baseline,"cancel cleans pending visual and reward at "+cancelAt);
   }
   restore();await Wait(.2f);g.Hud.ClosePanels();g.CompleteFishing(x,z,Species.Goldfish,id);await Wait(.5f);
   check(!g.SaveSession(3),"cannot save half-finished landing");Call("RestoreSnapshot",earned);await Wait(3);
   check(!g.Busy&&g.State.glassFish==baseline&&!g.FishingCatchVisual,"load cancels pending catch and no delayed grant");
   restore();await Wait(.2f);g.Hud.ClosePanels();g.CompleteFishing(x,z,Species.Goldfish,id);await Wait(.3f);g.FishingCatchVisual.gameObject.SetActive(false);await Wait(.2f);
   check(!g.Busy&&g.State.glassFish==baseline&&!g.FishingCatchVisual,"invalid visual cancels without granting fish");
   restore();await Wait(.2f);g.Hud.ClosePanels();actions=g.ActionCount;g.CompleteFishing(x,z,Species.Goldfish,id);
   Transform facing=(Transform)typeof(HabitatPlayerVisual).GetField("facingRoot",F).GetValue(g.View.PlayerVisual);
   await Wait(.35f);Quaternion direction=facing.rotation;float maxTurn=0;double until=Now+6;float maxHeight=-100;
   while(g.FishingLanding){if(Now>until)throw new Exception("landing timeout");check(g.State.glassFish==baseline,"reward withheld through landing frame");maxTurn=Mathf.Max(maxTurn,Quaternion.Angle(direction,facing.rotation));maxHeight=Mathf.Max(maxHeight,g.FishingCatchVisual.position.y);await Frame();}
   check(maxTurn<1.5f,"fixed facing during fish arc, maximum yaw change="+maxTurn);
   check(maxHeight>HabitatRules.WaterSurfaceY(g.State.waterLevel)+.8f,"caught fish visibly clears water");
   check(g.State.glassFish==baseline+1&&g.ActionCount==actions+1,"completion adds one catch and one undo entry");
   check(g.State.animals.Any(a=>a.id==id)&&g.State.animals.First(a=>a.id==id).catchClaimed,"parent remains and first catch marked only on completion");
   check(!g.FishingCatchVisual&&g.View.PlayerVisual.FishingPoseStage==0,"completed presentation cleans up");
   g.CompleteFishing(x,z,Species.Goldfish,id);await Wait(.2f);check(!g.Busy&&g.State.glassFish==baseline+1&&g.ActionCount==actions+1,"duplicate completed catch cannot grant twice");
   g.Undo();await Wait(.2f);check(g.State.glassFish==baseline&&!g.State.animals.First(a=>a.id==id).catchClaimed,"undo restores catch availability and inventory together");
   restore();await Wait(.2f);g.Hud.ClosePanels();g.CompleteFishing(x,z,Species.Goldfish,id);await Wait(.45f);
   log.AppendLine("PRE ESC busy="+g.Busy+" landing="+g.FishingLanding+" story="+g.StoryPlaying+" fish="+g.State.glassFish+" keyboard="+Keyboard.current.name+" message="+g.State.message);
   UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape));
   InputSystem.Update();check(keyboard.escapeKey.wasPressedThisFrame,"injected Escape reaches InputSystem");Call("HandleInput");await Frame();UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());InputSystem.Update();await Frame();
   check(!g.Busy&&!g.FishingCatchVisual&&g.State.glassFish==baseline,"Escape controller branch cancels landing without reward: busy="+g.Busy+" landing="+g.FishingLanding+" fish="+g.State.glassFish+" keyboard="+Keyboard.current.name+" message="+g.State.message);
   // Controlled open-water jellyfish fixture verifies the other supported catch visual.
   restore();var jelly=g.State.animals.First();jelly.species=Species.Jellyfish;
   for(int xx=0;xx<=jelly.x;xx++){var tile=HabitatRules.TileAt(g.State,xx,jelly.z);tile.height=0;tile.prop=PropKind.None;}
   g.View.Initialize(g.State);await Wait(.25f);g.Hud.ClosePanels();int jellyBefore=g.State.jellyfish;
   check(HabitatRules.CanHarvest(g.State,jelly,g.tuning,out _),"controlled jellyfish fixture meets capture rules");
   g.CompleteFishing(jelly.x,jelly.z,Species.Jellyfish,jelly.id);check(g.FishingLanding&&g.FishingCatchVisual,"jellyfish uses its delivered model for landing");
   await Wait(1);check(g.State.jellyfish==jellyBefore,"jellyfish reward also deferred");await Wait(2.1f);
   check(!g.Busy&&g.State.jellyfish==jellyBefore+1,"jellyfish landing completes exactly once");
   result="PASS fishing presentation transaction checks";
  }catch(Exception ex){result="FAIL "+ex;}
  finally{
   UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
   InputSystem.RemoveDevice(keyboard);oldKeyboard?.MakeCurrent();InputSystem.settings.backgroundBehavior=inputBackground;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
   recording=false;g.Fishing.Cancel(false);g.Fishing.enabled=fishEnabled;g.CancelPendingAction();g.Hud.ClosePanels();Call("RestoreSnapshot",original);undo.Clear();for(int i=oldUndo.Length-1;i>=0;i--)undo.Push(oldUndo[i]);foreach(var p in flags)typeof(HabitatGame).GetField(p.Key,F).SetValue(g,p.Value);g.paused=paused;g.playOpeningOnNewGame=opening;g.tool=tool;g.speed=speed;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);
   bool unchanged=beforeHash==Hashes(real);sf.SetValue(null,oldSave);if(!session)g.Hud.ShowStartMenu();log.AppendLine(unchanged?"PASS original save SHA256 unchanged":"FAIL original saves changed");if(!unchanged)result="FAIL original saves changed";log.AppendLine(result);File.WriteAllText(Dir+"/recording.txt",log.ToString());
  }return result;
 }
}
