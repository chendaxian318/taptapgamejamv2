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
using UnityEngine.UI;
using UnityEngine.EventSystems;
public static class OpeningPromptChecks {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 const string Dir="Verification/opening-prompt-20261018";
 static HabitatGame g; static StringBuilder log; static int count;
 static object Call(string name,params object[] args)=>typeof(HabitatGame).GetMethod(name,F).Invoke(g,args);
 static Transform[] Birds()=> (Transform[])typeof(HabitatMainStory).GetField("gulls",F).GetValue(g.Story);
 static void Check(bool ok,string name){log.AppendLine((ok?"PASS ":"FAIL ")+name);if(!ok)throw new Exception(name);count++;}
 static double Now=>UnityEditor.EditorApplication.timeSinceStartup;
 static async Task Frames(int n=1){int until=Time.frameCount+n;double limit=Now+12;while(Time.frameCount<until){if(Now>limit)throw new Exception("Frame timeout");await Task.Delay(8);}}
 static async Task Wait(float seconds){double end=Now+seconds;while(Now<end)await Frames();}
 static async Task Shot(string name){string path=Path.GetFullPath(Dir+"/"+name+".png");ScreenCapture.CaptureScreenshot(path);double end=Now+8;while(!File.Exists(path)){if(Now>end)throw new Exception("Screenshot timeout");await Frames();}}
 static string Hashes(string dir){using(var sha=SHA256.Create())return !Directory.Exists(dir)?"":string.Join("\n",Directory.GetFiles(dir,"*",SearchOption.AllDirectories).OrderBy(p=>p).Select(p=>p+":"+Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)))));}
 static bool Offscreen(Transform bird){var planes=GeometryUtility.CalculateFrustumPlanes(g.View.GameCamera);return bird.GetComponentsInChildren<Renderer>().All(r=>!GeometryUtility.TestPlanesAABB(planes,r.bounds));}
 public static async Task<string> Run(){
  g=HabitatGame.Instance;log=new StringBuilder();count=0;Directory.CreateDirectory(Dir);
  if(!Application.isPlaying||!g||g.Busy)throw new Exception("Idle Play required");
  object original=Call("CaptureSnapshot");bool paused=g.paused,session=g.SessionStarted,opening=g.playOpeningOnNewGame;int tool=g.tool;
  var undo=(Stack<HabitatState>)typeof(HabitatGame).GetField("history",F).GetValue(g);var oldUndo=undo.ToArray();
  var flags=new[]{"chapterGreetingPending","chapterChestPending","chapterMorningGreetingPending","chapterReturnReminderShown","campaignReturnReminderLevel","autoSaveRequested"}.ToDictionary(n=>n,n=>typeof(HabitatGame).GetField(n,F).GetValue(g));
  var sf=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);object oldSave=sf.GetValue(null);
  string real=Path.Combine(Application.persistentDataPath,"Playtests"),hashes=Hashes(real);sf.SetValue(null,Path.GetFullPath(Dir+"/isolated-"+DateTime.Now.ToString("HHmmssfff")));
  int completed=0;Action<bool> onComplete=morning=>completed++;g.Story.Completed+=onComplete;
  float oldAspect=g.View.GameCamera.aspect;string result="incomplete";
  try{
   g.playOpeningOnNewGame=true;g.NewSession();Check(g.StoryPlaying,"real NewSession starts authored opening");g.Story.TogglePause();
   foreach(float aspect in new[]{16f/9,16f/10,4f/3}){
    g.View.GameCamera.aspect=aspect;
    typeof(HabitatMainStory).GetMethod("ConfigureGullFlight",F).Invoke(g.Story,new object[]{g.Story.openingTimeline});
    g.Story.director.time=0;g.Story.director.Evaluate();await Frames(2);
    Check(Birds().All(Offscreen),"all birds outside left edge at start aspect "+aspect);
    Vector3[] last=null;double lt=0;bool visible=false;float maxJump=0;
    for(int i=0;i<=34;i++){
     double time=i*.1;g.Story.director.time=time;g.Story.director.Evaluate();
     var positions=Birds().Select(b=>b.localPosition).ToArray();
     if(last!=null){for(int b=0;b<positions.Length;b++){Check(positions[b].x>=last[b].x&&Mathf.Abs(positions[b].y-last[b].y)<.001f,"continuous bird "+b+" t="+time+" aspect="+aspect);maxJump=Mathf.Max(maxJump,Vector3.Distance(positions[b],last[b]));}}
     if(time>1&&time<2)visible|=Birds().Any(b=>!Offscreen(b));
     last=positions;lt=time;
    }
    Check(visible,"flock crosses visible sky aspect "+aspect);
    g.Story.director.time=3.29;g.Story.director.Evaluate();await Frames(2);
    Check(Birds().All(Offscreen),"entire birds clear sky frame before cut aspect "+aspect);
    log.AppendLine("max 0.1s displacement="+maxJump);
   }
   g.View.GameCamera.aspect=oldAspect;typeof(HabitatMainStory).GetMethod("ConfigureGullFlight",F).Invoke(g.Story,new object[]{g.Story.openingTimeline});
   g.Story.director.time=1.65;g.Story.director.Evaluate();await Frames(3);await Shot("sky-midflight");
   var frozen=Birds().Select(b=>b.position).ToArray();double frozenTime=g.Story.director.time;await Wait(.25f);
   Check(Birds().Select((b,i)=>Vector3.Distance(b.position,frozen[i])<.0001f).All(v=>v)&&Math.Abs(g.Story.director.time-frozenTime)<.001,"pause holds flock and Timeline");
   // Restart the same new game, then let the full authored opening advance on real frames.
   Call("CancelJourneySequence");g.NewSession();var previous=Birds().Select(b=>b.localPosition).ToArray();int visibleFrames=0;double stop=Now+45;bool captured=false;
   while(g.StoryPlaying){
    await Frames();if(Now>stop)throw new Exception("Natural opening timed out");var birds=Birds();if(birds==null)break;
    for(int i=0;i<birds.Length;i++){if(birds[i].localPosition.x+0.001f<previous[i].x)throw new Exception("Natural flight reversed/teleported");previous[i]=birds[i].localPosition;}
    if(g.Story.CurrentShot=="BlueSky"&&birds.Any(b=>!Offscreen(b)))visibleFrames++;
    if(!captured&&g.Story.director.time>2.6){captured=true;ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/sky-departure.png"));}
   }
   Check(visibleFrames>20,"real-frame opening renders moving flock, visible frames="+visibleFrames);
   Check(completed==1&&g.State.openingCompleted,"natural completion commits opening exactly once");
   Check(Birds()==null,"temporary birds cleaned after completion");g.Story.Skip();await Frames(2);Check(completed==1,"skip after completion cannot duplicate completion");
   typeof(HabitatGame).GetField("chapterGreetingPending",F).SetValue(g,false);g.Hud.ClosePanels();await Wait(.4f);
   var panel=(GameObject)typeof(HabitatHud).GetField("contextPanel",F).GetValue(g.Hud);
   var r=(RectTransform)panel.transform;var bubble=panel.GetComponentInChildren<RawImage>(true).rectTransform;
   Check(bubble.sizeDelta==new Vector2(144,77),"CLICK artwork reduced to 60 percent width/height");
   Check(r.sizeDelta.x==180 && r.sizeDelta.y>=104,"click area remains larger than artwork");
   Check(panel.activeInHierarchy,"shop interaction visible after opening; modal="+g.Hud.ModalOpen+" busy="+g.Busy+" harbor="+g.State.onHarbor+" nearest="+g.View.Harbor.NearestInteraction(g.State));
   await Shot("shop-prompt");
   Canvas.ForceUpdateCanvases();var caption=panel.GetComponentsInChildren<TMPro.TMP_Text>().Single();caption.ForceMeshUpdate();
   Check(caption.preferredHeight<=caption.rectTransform.rect.height+1,"E/action caption fits without clipping");
   var center=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center));var hits=new List<RaycastResult>();var eventData=new PointerEventData(EventSystem.current){position=center};EventSystem.current.RaycastAll(eventData,hits);
   Check(hits.Any(h=>h.gameObject==panel),"resized prompt receives UI raycast at button center");
   panel.GetComponent<Button>().onClick.Invoke();await Frames(3);Check(g.Hud.ModalOpen,"prompt click still opens merchant interaction");
   g.Hud.ClosePanels();g.NewSession();await Frames(3);Check(g.Story.Skip(),"skip active opening succeeds");Check(completed==2,"skip commits once");Check(!g.Story.Skip()&&completed==2,"repeat skip has no duplicate commit");
   // Controlled raft fixture checks the longest label through the actual HUD refresh.
   typeof(HabitatGame).GetField("chapterGreetingPending",F).SetValue(g,false);g.Hud.ClosePanels();g.paused=true;
   var fixture=new HabitatState {width=5,depth=5,tiles=Enumerable.Range(0,25).Select(_=>new Tile{height=0}).ToArray(),playerX=2,playerZ=2,facingX=1,facingZ=0,levelIndex=6,openingCompleted=true,backBasketUnlocked=true,chapterOneAccepted=true,chapterOneDelivered=true,campaignBriefedMask=62};
   fixture.tiles[12].prop=PropKind.Raft;fixture.tiles[12].rotation=1;fixture.seenGuideKeys=new List<string>{"move","pull","climb","rod","basket","fan","crab","jellyfish","tide","shovel","magnet","launcher"};
   g.State=fixture;g.tool=0;g.View.Initialize(fixture);g.View.FocusPlayer();g.Select(3,2);await Wait(.5f);
   Check(panel.activeInHierarchy&&caption.text.Contains("竹排"),"long raft label visible in controlled fixture");caption.ForceMeshUpdate();
   Check(caption.preferredHeight<=caption.rectTransform.rect.height+1,"long label expands caption without text clipping");
   Check(r.sizeDelta.y==78+caption.rectTransform.sizeDelta.y,"button bounds include expanded long caption");await Shot("raft-long-prompt");
   result="PASS "+count+" checks";
  }catch(Exception ex){result="FAIL "+ex;}
  finally{
   g.Story.Completed-=onComplete;Call("CancelJourneySequence");g.CancelPendingAction();g.Hud.ClosePanels();Call("RestoreSnapshot",original);undo.Clear();for(int i=oldUndo.Length-1;i>=0;i--)undo.Push(oldUndo[i]);foreach(var p in flags)typeof(HabitatGame).GetField(p.Key,F).SetValue(g,p.Value);
   g.paused=paused;g.playOpeningOnNewGame=opening;g.tool=tool;g.View.GameCamera.aspect=oldAspect;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);
   bool unchanged=hashes==Hashes(real);sf.SetValue(null,oldSave);if(!session)g.Hud.ShowStartMenu();log.AppendLine(unchanged?"PASS original save SHA256 unchanged":"FAIL original saves changed");if(!unchanged)result="FAIL original saves changed";
   log.AppendLine(result);File.WriteAllText(Dir+"/checks.txt",log.ToString());
  }return result;
 }
}
