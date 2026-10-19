using System;using System.IO;using System.Linq;using System.Text;using System.Text.RegularExpressions;using System.Reflection;using System.Threading.Tasks;using System.Security.Cryptography;using UnityEngine;using HabitatIsland;
public static class Feedback221708FirstRouteChecks
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;const string Dir="Verification/feedback-221708";static HabitatGame g;static StringBuilder log;static int count;
 static object Call(string n,params object[] a)=>typeof(HabitatGame).GetMethod(n,F).Invoke(g,a);
 static void Check(bool b,string s){log.AppendLine((b?"PASS ":"FAIL ")+s);File.WriteAllText(Dir+"/route-runtime-"+g.State.levelIndex+".txt",log.ToString());if(!b)throw new Exception(s+" "+g.State.message);count++;}
 static async Task Frame(){int n=Time.frameCount;while(Time.frameCount==n)await Task.Delay(10);}
 static async Task Arrive(){await Frame();double end=Time.realtimeSinceStartupAsDouble+30;while(g.Busy||!g.View.ResidentsArrived(g.State)){if(Time.realtimeSinceStartupAsDouble>end)throw new Exception("arrival timeout");await Frame();}g.Hud.ClosePanels();}
 static string Hash(string p){using(var sha=SHA256.Create())return !Directory.Exists(p)?"":string.Join("\n",Directory.GetFiles(p,"*",SearchOption.AllDirectories).OrderBy(x=>x).Select(x=>x+Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(x)))));}
 public static Task<string> Run5()=>Run(5);public static Task<string> Run8()=>Run(8);
 static async Task<string> Run(int level)
 {
  g=HabitatGame.Instance;log=new StringBuilder("Controller route with declared equipment/money fixture; not a new-game campaign.\n");count=0;
  var sf=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);var old=sf.GetValue(null);string real=Path.Combine(Application.persistentDataPath,"Playtests"),hash=Hash(real);sf.SetValue(null,Path.GetFullPath(Dir+"/isolated-route-"+level+"-"+DateTime.Now.ToString("HHmmssfff")));object saved=Call("CaptureSnapshot");bool pause=g.paused,session=g.SessionStarted;int tool=g.tool;
  try
  {
   g.LoadLevel(level);g.paused=true;g.Hud.ClosePanels();g.State.chapterOneAccepted=true;g.tuning.animalMovement=false;g.View.Initialize(g.State);await Arrive();
   var route=Regex.Matches(File.ReadAllText(Dir+"/routes/"+level+"-route.json"),@"\[\s*(-?\d+(?:\s*,\s*-?\d+)*)\s*\]").Cast<Match>().Select(m=>m.Groups[1].Value.Split(',').Select(x=>int.Parse(x.Trim())).ToArray()).ToArray();int step=0;
   foreach(var a in route)
   {
    await Arrive();int before=g.ActionCount,delta=1;
    switch(a[0]){
     case 0:g.Move(a[1],a[2],true);break;
     case 1:case 11:g.tool=0;g.Select(a[1],a[2]);g.ApplyTool();break;
     case 2:g.tool=2;g.Select(a[1],a[2]);g.ApplyTool();break;
     case 3:g.Select(a[1],a[2]);g.RotateTarget();break;
     case 4:g.StoreInventoryItem();break;case 5:g.TakeInventoryItem(a[1]);break;
     case 6:Call("AdvanceSimulation",a[1]/10f);delta=0;break;
     case 7:g.tool=a[1];Check(g.ConfirmUtility(a[2],a[3],a[4],a[5]),"utility accepted "+step);break;
     case 8:g.Buy(a[1]==7?"launcher":a[1]==6?"magnet":a[1]==5?"shovel":a[1]==3?"fan":"crate");break;
     case 9:g.TransferCargo(a[1]!=0);break;
     case 10:g.Move(a[1],a[2],true);delta=0;break;
     case 12:g.Pull(a[1],a[2]);break;case 13:g.Pull(g.State.playerX-a[1],g.State.playerZ-a[2]);break;
     case 14:g.State.facingX=a[1];g.State.facingZ=a[2];delta=0;break;
     default:throw new Exception("unsupported action "+a[0]);
    }
    await Arrive();Check(g.ActionCount==before+delta,"step "+(++step)+" ["+string.Join(",",a)+"] commits once");
   }
   await Arrive();Check(g.State.won&&g.State.animals.All(a=>HabitatRules.Evaluate(g.State,a,g.tuning).suitable),"level "+level+" actual settled victory with all residents suitable");
   Check(g.State.specialRod&&g.State.firstClearCgSeen,"first level real chest reward and natural cg01 after clear");g.Hud.ClosePanels();ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/level-"+level+"-complete.png"));await Frame();await Frame();File.WriteAllText(Dir+"/level-"+level+"-complete.json",JsonUtility.ToJson(Call("CaptureSnapshot"),true));return "PASS level "+level+", "+step+" actions, "+count+" checks";
  }
  finally{g.Story.Cancel();Call("CancelPendingAction");if(g.Fishing)g.Fishing.Cancel(false);Call("RestoreSnapshot",saved);g.paused=pause;g.tool=tool;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);g.Hud.ClosePanels();sf.SetValue(null,old);log.AppendLine(Hash(real)==hash?"PASS real save SHA256 unchanged":"FAIL save modified");File.WriteAllText(Dir+"/route-runtime-"+level+".txt",log.ToString());if(Hash(real)!=hash)throw new Exception("save changed");}
 }
}
