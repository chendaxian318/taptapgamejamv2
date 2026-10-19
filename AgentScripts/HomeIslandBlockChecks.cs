using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using HabitatIsland;
public static class HomeIslandBlockChecks
{
 const string Dir="Verification/home-island-blocks-20261018";
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static HabitatGame g;static StringBuilder log;static int count;
 static object Call(string n,params object[] args)=>typeof(HabitatGame).GetMethod(n,F).Invoke(g,args);
 static void Check(bool value,string text){log.AppendLine((value?"PASS ":"FAIL ")+(++count)+" "+text);if(!value)throw new Exception(text);}
 static async Task Wait(float seconds){double end=UnityEditor.EditorApplication.timeSinceStartup+seconds;while(UnityEditor.EditorApplication.timeSinceStartup<end)await Task.Delay(15);}
 static string Hashes(){string d=Path.Combine(Application.persistentDataPath,"Playtests");if(!Directory.Exists(d))return "absent";using(var h=System.Security.Cryptography.SHA256.Create())return string.Join("\n",Directory.GetFiles(d,"*",SearchOption.AllDirectories).OrderBy(x=>x).Select(p=>p+":"+Convert.ToBase64String(h.ComputeHash(File.ReadAllBytes(p)))));}
 static async Task Shot(string name){g.Hud.ClosePanels();g.View.FocusOverview();await Wait(.8f);ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/"+name+".png"));await Wait(.2f);}
 static async Task Walk(Vector3 local)
 {
  Vector3 target=g.View.Harbor.ModelTransform.TransformPoint(local);int stalled=0;
  for(int i=0;i<350;i++){
   Vector3 pos=g.View.Harbor.WorldPosition(g.State),delta=target-pos;delta.y=0;
   if(delta.magnitude<.10f){Check(true,"walked to "+local);return;}
   bool moved=g.View.Harbor.MoveWorld(g.State,delta,Mathf.Min(.03f,delta.magnitude/2.5f));
   g.View.Render(g.State,g.tuning,g.overlays);
   if(!moved&&++stalled>8)throw new Exception("walk blocked at "+g.View.Harbor.ModelTransform.InverseTransformPoint(pos)+" toward "+local);
   if(i%4==0)await Wait(.01f);
  }throw new Exception("walk timeout toward "+local);
 }
 public static string Begin(){AppDomain.CurrentDomain.SetData("HomeIslandBlockChecks","running");Run();return "started";}
 static async void Run()
 {
  g=HabitatGame.Instance;log=new StringBuilder();count=0;Directory.CreateDirectory(Dir);
  if(!Application.isPlaying||!g||g.Busy)throw new Exception("idle Play required");
  object original=Call("CaptureSnapshot");bool paused=g.paused,session=g.SessionStarted,opening=g.playOpeningOnNewGame;int tool=g.tool;
  var undo=(Stack<HabitatState>)typeof(HabitatGame).GetField("history",F).GetValue(g);var oldUndo=undo.ToArray();
  var flags=new[]{"chapterGreetingPending","chapterChestPending","chapterMorningGreetingPending","chapterReturnReminderShown","campaignReturnReminderLevel"}.ToDictionary(n=>n,n=>typeof(HabitatGame).GetField(n,F).GetValue(g));
  var sf=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);object oldSave=sf.GetValue(null);string hashes=Hashes();sf.SetValue(null,Path.GetFullPath(Dir+"/isolated-saves"));string result="incomplete";
  try{
   g.playOpeningOnNewGame=false;g.NewSession();g.paused=true;g.Hud.ClosePanels();await Wait(.7f);
   Check(HabitatLevels.IsHomeHarbor(g.State)&&g.State.onHarbor,"new game starts at home harbor");
   var island=g.View.Harbor.ModelTransform.GetComponentInChildren<HabitatHomeIsland>();
   Check(island&&island.GetComponentsInChildren<MeshRenderer>().Length>3,"home island terrain and scenery generated");
   Check(g.State.animals.Count==0&&g.State.coins==0&&!g.State.won&&!g.HasVisitedLevel(5),"home island adds no puzzle rewards or completion");
   await Shot("home-island-overview");
   for(int turn=0;turn<4;turn++){
    if(turn>0)g.View.RotateCamera(90);g.View.FocusOverview();await Wait(.6f);
    var sea=UnityEngine.Object.FindObjectsByType<Renderer>().Single(r=>r.sharedMaterial&&r.sharedMaterial.shader.name=="Habitat/QuietPaintedWater");
    var plane=new Plane(Vector3.up,new Vector3(0,HabitatRules.WaterSurfaceY(g.State.waterLevel),0));bool covered=true;
    foreach(var uv in new[]{new Vector3(0,0),new Vector3(0,1),new Vector3(1,0),new Vector3(1,1)}){
     var ray=g.View.GameCamera.ViewportPointToRay(uv);if(!plane.Raycast(ray,out float distance)){covered=false;continue;}
     var point=sea.transform.InverseTransformPoint(ray.GetPoint(distance));covered&=Mathf.Abs(point.x)<30&&Mathf.Abs(point.z)<30;
    }Check(covered,"ocean covers overview corners at rotation "+turn);
   }g.View.RotateCamera(90);g.View.FocusOverview();await Wait(.6f);
   await Walk(HabitatImportedDockLayout.DockArrival);await Walk(HabitatImportedDockLayout.ShopWaypoint);
   await Walk(HabitatHomeIsland.Gate);await Walk(new Vector3(7.2f,0,-3.8f));await Walk(HabitatHomeIsland.Clearing);
   Check(g.State.onHarbor&&HabitatLevels.IsHomeHarbor(g.State),"walking onto island remains continuous harbor movement");
   Vector3 saved=g.View.Harbor.WorldPosition(g.State);Check(g.SaveSession(3),"save island position in isolated slot");Check(g.LoadSession(3),"load island position");await Wait(.3f);
   Check(Vector3.Distance(saved,g.View.Harbor.WorldPosition(g.State))<.01f,"island position survives save/load and rebind");
   await Walk(new Vector3(2f,0,-10.7f));await Walk(new Vector3(-5f,0,-11f));await Shot("home-island-walking");
   await Walk(new Vector3(2f,0,-10.7f));await Walk(HabitatHomeIsland.Clearing);await Walk(new Vector3(7.2f,0,-3.8f));await Walk(HabitatHomeIsland.Gate);
   await Walk(HabitatImportedDockLayout.ShopWaypoint);await Walk(HabitatImportedDockLayout.ShopApproach);
   Check(g.View.Harbor.NearShop(g.State),"merchant still reachable after island round trip");
   foreach(var p in HabitatImportedDockLayout.HomePath.Skip(1))await Walk(p);
   Check(g.View.Harbor.NearHome(g.State),"home interaction remains reachable");
   var m=g.View.Harbor.ModelTransform;
   Check(!g.View.Harbor.IsWalkable(m.TransformPoint(new Vector3(20,0,-8))),"open ocean rejected");
   Check(!g.View.Harbor.IsWalkable(m.TransformPoint(new Vector3(-8,0,-6))),"home building cannot be bypassed through island");
   Check(!g.View.Harbor.IsWalkable(m.TransformPoint(new Vector3(20,0,-7))),"block shoreline rejects ocean");
   Check(!g.View.Harbor.IsWalkable(m.TransformPoint(HabitatImportedDockLayout.BoatArrival)),"boat inlet remains water outside free walking");
   g.LoadLevel(5);g.paused=true;await Wait(.5f);
   Check(!UnityEngine.Object.FindObjectsByType<HabitatHomeIsland>().Any(),"home island removed on puzzle island");
   Check(g.View.Harbor.LandingOnly,"puzzle landing remains compact");
   g.LoadLevel(HabitatLevels.HomeHarborIndex);g.paused=true;await Wait(.5f);
   Check(UnityEngine.Object.FindObjectsByType<HabitatHomeIsland>().Length==1,"return creates exactly one home island");
   Check(g.View.Harbor.TryPlaceAt(g.State,HabitatImportedDockLayout.BoatApproach),"existing home boat anchor still valid");
   await Shot("home-island-final");result="PASS "+count+" checks";
  }catch(Exception ex){result="FAIL "+ex;await Shot("failure");}
  finally{
   g.CancelPendingAction();g.Hud.ClosePanels();Call("RestoreSnapshot",original);undo.Clear();for(int i=oldUndo.Length-1;i>=0;i--)undo.Push(oldUndo[i]);foreach(var p in flags)typeof(HabitatGame).GetField(p.Key,F).SetValue(g,p.Value);
   g.paused=paused;g.playOpeningOnNewGame=opening;g.tool=tool;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);
   bool unchanged=hashes==Hashes();sf.SetValue(null,oldSave);log.AppendLine(unchanged?"PASS original save SHA256 unchanged":"FAIL original saves changed");if(!unchanged)result="FAIL original saves changed";
   log.AppendLine(result);File.WriteAllText(Dir+"/checks.txt",log.ToString());AppDomain.CurrentDomain.SetData("HomeIslandBlockChecks",result);
  }
 }
}

