using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HabitatIsland;
public static class CurrentMagnetRuntimeChecks
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
  g=HabitatGame.Instance;log=new StringBuilder("Controlled real magnet cancel/undo and blocked-input checks.\n");count=0;
  var snapshot=typeof(HabitatGame).GetMethod("CaptureSnapshot",F).Invoke(g,null);bool pause=g.paused,session=g.SessionStarted;int tool=g.tool;
  var history=(Stack<HabitatState>)typeof(HabitatGame).GetField("history",F).GetValue(g);var historyCopy=history.ToArray();
  try{
   typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);
   Setup(6);g.State.magnets=1;Q(3,3).prop=PropKind.Crate;g.View.Initialize(g.State);await Frames();
   var preview=g.PreviewUtility(3,3,0,1,out bool valid);Check(valid&&Q(3,3).prop==PropKind.Crate&&g.State.magnets==1&&HabitatRules.TileAt(preview,3,4).prop==PropKind.Crate,"magnet previews chosen perpendicular direction without spending");
   Check(g.ConfirmUtility(3,3,0,1)&&g.Busy&&g.State.magnets==1&&Q(3,3).prop==PropKind.Crate,"magnet starts motion before authoritative relocation");
   Check(!g.ConfirmUtility(3,3,0,1),"duplicate confirmation blocked during magnet motion");g.CancelPendingAction();await Frames();Check(!g.Busy&&g.State.magnets==1&&Q(3,3).prop==PropKind.Crate&&Q(3,4).prop==PropKind.None&&g.ActionCount==0,"cancel restores source box with no inventory or action consumption");
   g.ConfirmUtility(3,3,0,1);await Wait();Check(Q(3,4).prop==PropKind.Crate&&Q(3,3).prop==PropKind.None&&g.State.magnets==0&&g.ActionCount==1,"magnet settles one relocated box and one consumption after arrival");
   g.Undo();await Frames();Check(Q(3,3).prop==PropKind.Crate&&Q(3,4).prop==PropKind.None&&g.State.magnets==1,"undo restores magnet and original box location together");
   Q(3,4).prop=PropKind.Obstacle;int actions=g.ActionCount;Check(!g.ConfirmUtility(3,3,0,1)&&!g.Busy&&g.State.magnets==1&&g.ActionCount==actions,"blocked magnet does not start animation or spend inventory");
   Check(!g.ConfirmUtility(3,3,1,1)&&g.State.magnets==1&&g.ActionCount==actions,"invalid diagonal magnet input preserves inventory");
   return "PASS "+count+" magnet motion/cancel/undo checks";
  }finally{g.CancelPendingAction();g.Hud.ClosePanels();typeof(HabitatGame).GetMethod("RestoreSnapshot",F).Invoke(g,new[]{snapshot});history.Clear();for(int i=historyCopy.Length-1;i>=0;i--)history.Push(historyCopy[i]);g.paused=pause;g.tool=tool;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);File.WriteAllText("Verification/latest-plan-20261018/current-magnet-runtime.txt",log.ToString());}
 }
}
