using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HabitatIsland;
public static class StandaloneUtilityChecks
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static HabitatGame g; static StringBuilder log;static int count;
 static void Check(bool ok,string msg){if(!ok)throw new Exception(msg);log.AppendLine("PASS "+(++count)+" "+msg);}
 static async Task Frames(int n=3){int end=Time.frameCount+n;while(Time.frameCount<end)await Task.Yield();}
 static async Task Wait(){float end=Time.realtimeSinceStartup+8;while(g.Busy&&Time.realtimeSinceStartup<end)await Task.Yield();Check(!g.Busy,"animation completes within deadline");await Frames();}
 static void Setup(int tool){g.CancelPendingAction();g.Hud.ClosePanels();g.LoadLevel(1);g.State=new HabitatState{width=7,depth=7,levelIndex=1,tiles=Enumerable.Range(0,49).Select(_=>new Tile{height=1}).ToArray(),playerX=2,playerZ=3,facingX=1,facingZ=0,dockX=0,dockZ=0,rod=true,specialRod=true,backBasketUnlocked=true,openingCompleted=true};g.State.seenGuideKeys=new List<string>{"move","pull","climb","rod","basket","fan","crab","jellyfish","tide","shovel","magnet","launcher"};g.tool=tool;g.paused=true;g.View.Initialize(g.State);}
 static Tile Q(int x,int z)=>HabitatRules.TileAt(g.State,x,z);
 public static async Task<string> Run()
 {
  g=HabitatGame.Instance;log=new StringBuilder("Controlled real animation checks; not a fifth-level launcher route.\n");count=0;
  var snapshot=typeof(HabitatGame).GetMethod("CaptureSnapshot",F).Invoke(g,null);bool pause=g.paused,session=g.SessionStarted;int tool=g.tool;
  var history=(Stack<HabitatState>)typeof(HabitatGame).GetField("history",F).GetValue(g);var historyCopy=history.ToArray();
  var saveField=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.NonPublic|BindingFlags.Static);var oldSaveDirectory=saveField.GetValue(null);
  saveField.SetValue(null,Path.GetFullPath(Path.Combine(HabitatStandaloneAcceptance.OutputDirectory,"utility/isolated-saves")));
  try{
   typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);
   Setup(7);g.State.launchers=1;Q(3,3).prop=PropKind.Crate;g.View.Initialize(g.State);await Frames();
   var preview=g.PreviewUtility(3,3,1,0,out bool valid);Check(valid&&Q(3,3).prop==PropKind.Crate&&g.State.launchers==1&&HabitatRules.TileAt(preview,5,3).prop==PropKind.Crate,"launcher previews two-cell destination without consumption");
   Check(g.ConfirmUtility(3,3,1,0)&&g.Busy&&g.State.launchers==1&&Q(3,3).prop==PropKind.Crate,"launcher waits for animation before authority changes");
   Check(!g.ConfirmUtility(3,3,1,0),"duplicate launch rejected while busy");g.CancelPendingAction();await Frames();Check(!g.Busy&&g.State.launchers==1&&Q(3,3).prop==PropKind.Crate&&g.ActionCount==0,"cancel launch preserves inventory and crate");
   g.ConfirmUtility(3,3,1,0);await Wait();Check(Q(5,3).prop==PropKind.Crate&&Q(3,3).prop==PropKind.None&&g.State.launchers==0&&g.ActionCount==1,"launch arrival commits exactly once");g.Undo();await Frames();Check(Q(3,3).prop==PropKind.Crate&&g.State.launchers==1,"undo restores launcher and original crate together");Q(4,3).prop=PropKind.Obstacle;Check(!g.ConfirmUtility(3,3,1,0)&&g.State.launchers==1,"blocked launcher never consumes inventory");
   Setup(5);g.State.shovels=1;Q(3,3).height=2;g.View.Initialize(g.State);await Frames();Check(g.ConfirmUtility(3,3,1,0)&&Q(3,3).height==2&&g.State.shovels==1,"shovel starts without premature terrain edit");g.CancelPendingAction();await Frames();Check(Q(3,3).height==2&&g.State.shovels==1,"cancel dig leaves terrain and shovel intact");g.ConfirmUtility(3,3,1,0);await Wait();Check(Q(3,3).height==1&&g.State.shovels==0&&g.ActionCount==1,"dig completes once after shovel motion");g.Undo();await Frames();Check(Q(3,3).height==2&&g.State.shovels==1,"undo restores dug height and shovel");
   Setup(0);g.State.playerX=2;g.State.playerZ=3;Q(2,3).height=2;Q(3,3).prop=PropKind.Tide;g.State.carryingProp=PropKind.Crate;g.View.Initialize(g.State);await Frames();g.Select(3,3);g.ApplyTool();await Frames();Check(g.State.tideActive&&g.State.waterLevel==1&&Q(3,3).coveredProp==PropKind.Tide,"crate on tide mechanism raises water exactly one level");g.Pull(-1,0);await Wait();Check(!g.State.tideActive&&g.State.waterLevel==0&&Q(2,3).prop==PropKind.Crate&&Q(3,3).prop==PropKind.Tide,"recovering lower tide crate restores water and preserves actual mechanism");
   return "PASS "+count+" utility/cancel/undo/tide checks";
  }finally{g.CancelPendingAction();g.Hud.ClosePanels();typeof(HabitatGame).GetMethod("RestoreSnapshot",F).Invoke(g,new[]{snapshot});history.Clear();for(int i=historyCopy.Length-1;i>=0;i--)history.Push(historyCopy[i]);g.paused=pause;g.tool=tool;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);saveField.SetValue(null,oldSaveDirectory);File.WriteAllText(Path.Combine(HabitatStandaloneAcceptance.OutputDirectory,"utility/utility-runtime.txt"),log.ToString());}
 }
}



