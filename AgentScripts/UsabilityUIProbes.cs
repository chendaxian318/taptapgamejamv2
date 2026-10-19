using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HabitatIsland;
public static class UsabilityUIProbes {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 const string Dir="Verification/usability-audit-20261018";
 static HabitatGame g;
 static async Task Frame(){int f=Time.frameCount;while(Time.frameCount==f)await Task.Delay(10);}
 static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
 static async Task<string> Capture(string name){await Frame();Canvas.ForceUpdateCanvases();await Frame();var text=string.Join("\n",UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(t=>t.isActiveAndEnabled).Select(t=>t.text));File.WriteAllText(Dir+"/probe-"+name+".txt",text);ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/probe-"+name+".png"));await Frame();await Frame();return text;}
 public static async Task<string> Run(){
  g=HabitatGame.Instance;var original=Call(g,"CaptureSnapshot");bool session=g.SessionStarted,pause=g.paused;int tool=g.tool;
  var sf=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);var old=sf.GetValue(null);sf.SetValue(null,Path.GetFullPath(Dir+"/probe-saves"));
  try{
   var saved=typeof(HabitatGame).GetMethod("ReadValidatedSnapshot",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Dir+"/full-campaign-first.json"});Call(g,"RestoreSnapshot",saved);typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);g.Hud.ClosePanels();g.paused=true;
   for(int i=0;i<5;i++)await Frame();g.Hud.ClosePanels();
   string pausedText=await Capture("first-won-paused");g.paused=false;string runningText=await Capture("first-won-running");g.paused=true;
   File.WriteAllText(Dir+"/probe-results.txt","Ecology paused vs running visible TMP identical: "+(pausedText==runningText)+"\nPaused visible state contains 已暂停/暂停生态: "+(pausedText.Contains("已暂停")||pausedText.Contains("暂停生态"))+"\n");
   Call(g.Hud,"ConfirmSummary");await Capture("completion-routes");var retry=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="Summary retry level");retry.onClick.Invoke();await Capture("retry-warning");g.Hud.ClosePanels();
   g.Hud.ToggleInventory();await Capture("inventory");Call(g.Hud,"SelectBagTool",7);await Capture("empty-launcher-confirm");Call(g.Hud,"ConfirmBagSelection");await Capture("empty-launcher-selected");File.AppendAllText(Dir+"/probe-results.txt","Empty launcher selectable: inventory="+g.State.launchers+" selectedTool="+g.tool+" message="+g.State.message+"\n");
   g.Hud.ClosePanels();g.Hud.ToggleShop();await Capture("shop");g.Hud.ClosePanels();g.Hud.OpenMapSelection(6);await Capture("map-locked-second");g.Hud.ClosePanels();g.Hud.ToggleSettings();await Capture("settings");g.Hud.ClosePanels();Call(g.Hud,"OpenBook",new object[]{null});await Capture("creature-book");
   return "PASS UI probes captured: ecology status, completion, restart warning, empty tool selection, shop, map, settings, book";
  }finally{g.CancelPendingAction();g.Hud.ClosePanels();Call(g,"RestoreSnapshot",original);typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);g.paused=pause;g.tool=tool;sf.SetValue(null,old);}
 }
}
