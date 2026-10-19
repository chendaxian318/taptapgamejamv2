using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using HabitatIsland;
using Newtonsoft.Json.Linq;
public static class RestoreMouseFacingSession
{
 public static string Run()
 {
  var t=typeof(HabitatGame);var g=HabitatGame.Instance;
  var f=BindingFlags.Instance|BindingFlags.NonPublic;
  g.CancelPendingAction();g.Hud.ClosePanels();
  var saved=t.GetMethod("ReadValidatedSnapshot",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"Verification/release-playtest-20261018/original-session.json"});
  t.GetMethod("RestoreSnapshot",f).Invoke(g,new[]{saved});
  var flags=JObject.Parse(File.ReadAllText("Verification/release-playtest-20261018/original-flags.json"));
  t.GetProperty("SessionStarted").SetValue(g,(bool)flags["session"]);g.paused=(bool)flags["paused"];g.tool=(int)flags["tool"];
  var h=(Stack<HabitatState>)t.GetField("history",f).GetValue(g);h.Clear();var items=flags["history"].ToObject<HabitatState[]>();
  for(int i=items.Length-1;i>=0;i--)h.Push(items[i]);
  t.GetField("retryHistoryDepth",f).SetValue(g,(int)flags["depth"]);
  t.GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);
  var result=new JObject{["sessionRestored"]=g.SessionStarted,["level"]=g.State.levelIndex,["playerX"]=g.State.playerX,["playerZ"]=g.State.playerZ,["paused"]=g.paused,["undoCount"]=h.Count,["compilationErrors"]=UnityEditor.EditorUtility.scriptCompilationFailed};
  File.WriteAllText("Verification/release-playtest-20261018/final-state.json",result.ToString());return result.ToString();
 }
}

