using System;using System.IO;using System.Linq;using System.Text;using System.Reflection;using System.Threading.Tasks;using System.Security.Cryptography;using UnityEngine;using UnityEngine.UI;using HabitatIsland;
public static class Feedback221708PriorityChecks
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;const string Dir="Verification/feedback-221708";static HabitatGame g;static StringBuilder log;static int count;
 static object Call(string n,params object[] a)=>typeof(HabitatGame).GetMethod(n,F).Invoke(g,a);
 static void Check(bool v,string s){log.AppendLine((v?"PASS ":"FAIL ")+s);File.WriteAllText(Dir+"/priority.txt",log.ToString());if(!v)throw new Exception(s+" "+g.State.message);count++;}
 static async Task Frames(int n=3){int end=Time.frameCount+n;while(Time.frameCount<end)await Task.Delay(12);}
 static async Task Until(Func<bool> f,string s,int limit=30){double end=Time.realtimeSinceStartupAsDouble+limit;while(!f()){if(Time.realtimeSinceStartupAsDouble>end)throw new Exception("timeout "+s);await Frames(1);}}
 static string Hashes(string p){using(var sha=SHA256.Create())return !Directory.Exists(p)?"":string.Join("\n",Directory.GetFiles(p,"*",SearchOption.AllDirectories).OrderBy(x=>x).Select(x=>x+Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(x)))));}
 static async Task Shot(string s){await Frames(3);ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/"+s+".png"));await Frames(8);}
 public static async Task<string> Run()
 {
  g=HabitatGame.Instance;log=new StringBuilder();var sf=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);object old=sf.GetValue(null);string real=Path.Combine(Application.persistentDataPath,"Playtests"),hash=Hashes(real);sf.SetValue(null,Path.GetFullPath(Dir+"/isolated-flow-"+DateTime.Now.ToString("HHmmssfff")));object saved=Call("CaptureSnapshot");bool paused=g.paused,started=g.SessionStarted;int tool=g.tool;
  try
  {
   g.LoadLevel(8);g.paused=true;g.Hud.ClosePanels();
   var state=g.State;state.width=7;state.depth=5;state.tiles=Enumerable.Range(0,35).Select(i=>new Tile{height=1}).ToArray();state.animals.Clear();state.playerX=3;state.playerZ=1;state.onHarbor=false;state.rod=state.specialRod=true;state.won=false;for(int x=2;x<7;x++)state.tiles[14+x].height=0;
   state.animals.Add(new Animal{id=1,x=2,z=2,species=Species.Jellyfish});state.animals.Add(new Animal{id=2,x=0,z=0,species=Species.Goldfish});g.View.Initialize(state);await Frames(6);g.Hud.ClosePanels();
   Check(HabitatRules.CanHarvest(g.State,g.State.animals[0],g.tuning,out _),"lure target is already eligible for partial harvest");g.tool=2;g.Select(2,2);Check(!g.TryNearbyCatch(out _,out _),"selected lure tool is not intercepted by harvest shortcut");g.ApplyTool();Check(g.Busy&&!g.Fishing.IsOpen&&g.State.animals[0].x==2,"lure begins deferred move, not fishing minigame");await Until(()=>!g.Busy,"lure arrival");Check(g.State.animals[0].x==3&&g.State.animals[0].z==2,"lure arrives and commits correctly");
   g.tool=0;g.Select(3,2);Check(!g.TryNearbyCatch(out _,out _),"explicit adjacent jellyfish interaction has priority");g.ApplyTool();Check(g.State.carryingAnimal==1&&!g.Fishing.IsOpen,"existing near jellyfish interaction preserved");
   var shrimp=HabitatRules.Clone(g.State);shrimp.backBasketUnlocked=true;shrimp.carryingAnimal=3;shrimp.animals.Add(new Animal{id=3,x=3,z=1,species=Species.Shrimp,carried=true,catchClaimed=true,offspring=3});Check(HabitatRules.CarriedShrimpCatchCount(shrimp)==3&&HabitatRules.CatchCount(shrimp,Species.Shrimp)==3,"shrimp sale preview counts available offspring exactly");Check(HabitatRules.StoreCarriedShrimp(shrimp)&&shrimp.shrimp==3&&shrimp.animals.Last().offspring==0,"shrimp pre-clear harvest uses preview quantity and retains parent");
   g.LoadLevel(11);g.Hud.ClosePanels();g.View.Harbor.TryPlaceAt(g.State,HabitatImportedDockLayout.BoatApproach);g.Story.BeginDeparture(()=>{});await Frames(12);var caption=(TMPro.TMP_Text)typeof(HabitatHud).GetField("storyCaption",F).GetValue(g.Hud);Check(caption.color==Color.white,"official dark task panel has readable white text");await Shot("departure-task-ui");g.Story.Cancel();
   return "PASS "+count+" partial-harvest interaction priority checks.";
  }
  finally{g.Story.Cancel();Call("CancelPendingAction");if(g.Fishing)g.Fishing.Cancel(false);Call("RestoreSnapshot",saved);g.paused=paused;g.tool=tool;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,started);g.Hud.ClosePanels();sf.SetValue(null,old);Check(Hashes(real)==hash,"real player save SHA256 unchanged");}
 }
}
