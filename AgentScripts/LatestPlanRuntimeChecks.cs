using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using HabitatIsland;
public static class LatestPlanRuntimeChecks
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static HabitatGame g;static StringBuilder log;
 static readonly string[] guides={"move","pull","climb","rod","basket","fan","crab","jellyfish","tide","shovel","magnet","launcher"};
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);log.AppendLine("PASS "+name);}
 static async Task Frames(int n=4){int end=Time.frameCount+n;while(Time.frameCount<end)await Task.Yield();}
 static async Task Until(Func<bool> done,float seconds=12){float end=Time.realtimeSinceStartup+seconds;while(!done()&&Time.realtimeSinceStartup<end)await Task.Yield();Check(done(),"arrival/completion before timeout");}
 static void Setup(HabitatState s){s.seenGuideKeys=guides.ToList();s.openingCompleted=true;s.chapterOneAccepted=true;s.chapterOneSailed=true;g.State=s;g.paused=true;g.Hud.ClosePanels();g.View.Initialize(s);}
 public static async Task<string> Run()
 {
  g=HabitatGame.Instance;log=new StringBuilder("Latest plan runtime checks; first-route plus controlled arrival/harvest fixtures.\n");
  var snapshot=typeof(HabitatGame).GetMethod("CaptureSnapshot",F).Invoke(g,null);bool pause=g.paused,session=g.SessionStarted;int tool=g.tool;
  string folder="Verification/latest-plan-20261018";Directory.CreateDirectory(folder);
  try
  {
   typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);
   g.LoadLevel(7);g.Hud.ClosePanels();
   var s=new HabitatState{width=7,depth=7,levelIndex=7,authoredLevel=true,levelName="到位与出壳验证",tiles=Enumerable.Range(0,49).Select(_=>new Tile{height=1}).ToArray(),playerX=0,playerZ=3,dockX=0,dockZ=3,carryingAnimal=2,backBasketUnlocked=true};
   s.tiles[3*7+4].height=0;s.animals.Add(new Animal{id=1,species=Species.Crab,x=2,z=3});s.animals.Add(new Animal{id=2,species=Species.Shrimp,x=0,z=3,carried=true});s.animals.Add(new Animal{id=3,species=Species.Goldfish,x=4,z=3});
   Setup(s);await Frames();Check(!s.won,"unsuitable crab blocks otherwise suitable residents");
   g.Move(1,0);Check(g.Busy&&!s.won&&s.animals[0].x==2,"candidate never grants crab position or victory early");g.CancelPendingAction();await Frames();Check(!g.State.won&&!g.State.animals[0].shellShed,"cancel leaves shell and victory untouched");
   g.Move(1,0);await Until(()=>g.State.animals[0].shellShed);Check(!g.State.won,"shell exit presentation precedes completion");
   await Until(()=>g.State.won&&!g.Busy);Check(g.View.ResidentsArrived(g.State),"simultaneous completion waits for all visible actors");
   var crab=g.View.GetComponentsInChildren<HabitatCrabVisual>().First();Check(crab.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh==Resources.Load<Mesh>("Models/HermitCrab/BodyWithoutShell"),"crab uses supplied mesh with detached shell");
   Check(g.View.GetComponentInChildren<HabitatShedShellVisual>()!=null,"collectible shell uses separated source model");
   ScreenCapture.CaptureScreenshot(Path.GetFullPath(folder+"/crab-shell.png"));await Frames(8);
   g.Hud.ClosePanels();g.Undo();await Frames();Check(!g.State.won&&!g.State.animals[0].shellShed,"undo restores crab shell and completion together");
   g.LoadLevel(8);s=new HabitatState{width=7,depth=7,levelIndex=8,authoredLevel=true,levelName="水母捕捞验证",tiles=Enumerable.Range(0,49).Select(_=>new Tile{height=0}).ToArray(),playerX=3,playerZ=3,dockX=3,dockZ=3,rod=true,won=true};s.tiles[3*7+3].height=1;s.animals.Add(new Animal{id=1,species=Species.Jellyfish,x=4,z=3});Setup(s);await Frames();g.Hud.ClosePanels();
   Check(g.Fishing.Begin(g,4,3),"water jellyfish opens fishing challenge");g.Fishing.Cancel(false);Check(s.jellyfish==0&&!s.animals[0].catchClaimed,"fishing cancel consumes nothing");
   Check(g.Fishing.Begin(g,4,3),"can retry after cancel");typeof(HabitatFishing).GetMethod("Finish",F).Invoke(g.Fishing,new object[]{true});Check(g.State.jellyfish==1&&g.State.animals[0].catchClaimed,"successful jellyfish fishing commits one catch");g.CompleteFishing(4,3,Species.Jellyfish,1);Check(g.State.jellyfish==1,"repeat finish cannot duplicate the catch");
   g.LoadLevel(5);Setup(g.State);await Frames();
   string source=File.ReadAllText(folder+"/first-route.json");
   var steps=Regex.Matches(source,@"\[\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*\]").Cast<Match>().Select(m=>new[]{int.Parse(m.Groups[1].Value),int.Parse(m.Groups[2].Value),int.Parse(m.Groups[3].Value)}).ToArray();Check(steps.Length>0,"first-level legal route loaded");
   for(int i=0;i<steps.Length;i++)
   {
    g.Hud.ClosePanels();int before=g.ActionCount;var action=steps[i];
    if(action[0]==0)g.Move(action[1],action[2],true);else{g.tool=0;g.Select(action[1],action[2]);g.ApplyTool();}
    await Until(()=>!g.Busy);await Until(()=>g.View.ResidentsArrived(g.State));await Frames(1);
    Check(g.ActionCount==before+1,"first-level normal controller action "+(i+1));
   }
   await Until(()=>g.State.won);Check(g.State.specialRod&&g.State.coins==0,"first island completed with source props and chest rod, no money injection");
   ScreenCapture.CaptureScreenshot(Path.GetFullPath(folder+"/first-complete.png"));await Frames(8);
   return "PASS runtime arrival, shell, jellyfish harvest and first-level "+steps.Length+" actions";
  }
  finally
  {
   g.Fishing.Cancel(false);g.CancelPendingAction();g.Hud.ClosePanels();typeof(HabitatGame).GetMethod("RestoreSnapshot",F).Invoke(g,new[]{snapshot});g.paused=pause;g.tool=tool;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);
   File.WriteAllText(folder+"/runtime-checks.txt",log.ToString());
  }
 }
}
