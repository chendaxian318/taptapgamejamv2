using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEditor;
using UnityEngine;
public static class TideEngineChecks
{
 const string Dir="Verification/tide-engine-20261019";
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static StringBuilder log;static int count;static HabitatGame g;
 static void Check(bool ok,string message){if(!ok)throw new Exception("FAIL "+message);log.AppendLine("PASS "+(++count)+" "+message);}
 static async Task Frames(int n=3){int end=Time.frameCount+n;float limit=Time.realtimeSinceStartup+10;while(Time.frameCount<end&&Time.realtimeSinceStartup<limit)await Task.Yield();if(Time.frameCount<end)throw new Exception("Frame timeout");}
 static async Task UntilIdle(){float end=Time.realtimeSinceStartup+10;while(g.Busy&&Time.realtimeSinceStartup<end)await Task.Yield();Check(!g.Busy,"controller finishes pending action");await Frames(2);}
 static Dictionary<string,string> Hashes(string dir){return Directory.Exists(dir)?Directory.GetFiles(dir,"*",SearchOption.AllDirectories).ToDictionary(p=>p,p=>Convert.ToBase64String(SHA256.Create().ComputeHash(File.ReadAllBytes(p)))):new Dictionary<string,string>();}
 static Tile Q(int x,int z)=>HabitatRules.TileAt(g.State,x,z);
 static HabitatTideEngineVisual Plate(int x=3,int z=3)=>UnityEngine.Object.FindObjectsByType<HabitatTideEngineVisual>(FindObjectsSortMode.None).Single(v=>v.gameObject.activeInHierarchy&&Mathf.Abs(v.transform.position.x-x)<.1f&&Mathf.Abs(v.transform.position.z-z)<.1f);
 static void Setup()
 {
  g.CancelPendingAction();g.Hud.ClosePanels();g.LoadLevel(1);
  g.State=new HabitatState{width=7,depth=7,levelIndex=1,tiles=Enumerable.Range(0,49).Select(_=>new Tile{height=2}).ToArray(),playerX=1,playerZ=3,facingX=1,facingZ=0,dockX=0,dockZ=0,openingCompleted=true};
  g.State.seenGuideKeys=new List<string>{"move","pull","climb","rod","basket","fan","crab","jellyfish","tide","shovel","magnet","launcher"};
  Q(2,3).prop=PropKind.Crate;Q(3,3).prop=PropKind.Tide;Q(4,4).prop=PropKind.Tide;g.tool=0;g.paused=true;g.View.Initialize(g.State);
 }
 static void Capture(string name,Vector3 target)
 {
  var ob=new GameObject("Tide verification camera");var camera=ob.AddComponent<Camera>();camera.CopyFrom(g.View.GameCamera);camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1.65f;
  camera.transform.position=target+new Vector3(1.3f,3.3f,-2.4f);camera.transform.LookAt(target);var rt=new RenderTexture(1000,800,24);camera.targetTexture=rt;var old=RenderTexture.active;var tex=new Texture2D(1000,800,TextureFormat.RGB24,false);
  try{camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1000,800),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(Dir,name+".png"),tex.EncodeToPNG());}
  finally{RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.Destroy(tex);rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(ob);}
 }
 public static string Begin(){if(!Application.isPlaying)throw new Exception("Enter Play first");AppDomain.CurrentDomain.SetData("TideEngineChecks.Result","running");Run();return "started";}
 static async void Run()
 {
  g=HabitatGame.Instance;log=new StringBuilder("Tide Engine model and real-controller pressure regression. Controlled fixtures, not a full level playthrough.\n");count=0;Directory.CreateDirectory(Dir);
  var saveField=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.NonPublic|BindingFlags.Static);var oldSave=saveField.GetValue(null);var hashes=Hashes(Path.Combine(Application.persistentDataPath,"Playtests"));saveField.SetValue(null,Path.GetFullPath(Dir+"/isolated-saves"));
  var snapshot=typeof(HabitatGame).GetMethod("CaptureSnapshot",F).Invoke(g,null);bool paused=g.paused,session=g.SessionStarted;int tool=g.tool;
  var history=(Stack<HabitatState>)typeof(HabitatGame).GetField("history",F).GetValue(g);var oldHistory=history.ToArray();GameObject model=null;string result="FAIL incomplete";
  try
  {
   typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);
   var prefab=Resources.Load<GameObject>("Models/TideEngine/TideEngine");Check(prefab,"prefab loads");
   var skins=prefab.GetComponentsInChildren<SkinnedMeshRenderer>();Check(skins.Sum(s=>s.sharedMesh.triangles.Length/3)==12504,"12504 triangles, lower fragment absent");Check(skins.SelectMany(s=>s.bones).Distinct().Count()==3,"three rigid-prop bones");
   var clips=prefab.GetComponent<HabitatEquipmentAnimation>();Check(clips.Clips.Select(c=>c.name).OrderBy(n=>n).SequenceEqual(new[]{"Active","Idle","Press","Release"}),"four pressure-specific clips");Check(clips.Clips.All(c=>c.isLooping==(c.name=="Idle"||c.name=="Active")),"correct loop flags");Check(!clips.Animator.applyRootMotion,"no root motion");
   var mat=skins[0].sharedMaterial;foreach(string prop in new[]{"baseColorTexture","normalTexture","metallicRoughnessTexture"}){var tex=mat.GetTexture(prop);Check(tex&&AssetDatabase.GetAssetPath(tex).Contains("OptimizedTextures/TideEngine"),prop+" uses independent compressed texture");}
   Check(mat.GetTexture("baseColorTexture").width==4096&&mat.GetTexture("normalTexture").width==4096&&mat.GetTexture("metallicRoughnessTexture").width==2048,"original texture resolutions and channels retained");
   model=UnityEngine.Object.Instantiate(prefab);model.transform.position=Vector3.one*-100;var visual=model.GetComponent<HabitatTideEngineVisual>();visual.ResetVisual(false);var start=HabitatIsland.Editor.HabitatEquipmentSetup.BakedBounds(model);Check(start.size.y<.15f&&Mathf.Abs(start.size.x-.94f)<.01f,"flat pressure-plate proportions");
   visual.Render(true,.2f);Check(visual.Compression>.49f&&visual.Compression<.51f&&visual.CurrentClip=="Press","half press samples authored animation");var mid=HabitatIsland.Editor.HabitatEquipmentSetup.BakedBounds(model);visual.Render(false,0f);var reversed=HabitatIsland.Editor.HabitatEquipmentSetup.BakedBounds(model);Check(Vector3.Distance(mid.center,reversed.center)<.0001f,"interrupted press reverses without pose jump");
   visual.Render(true,.4f);var down=HabitatIsland.Editor.HabitatEquipmentSetup.BakedBounds(model);Check(down.center.y<start.center.y-.11f&&down.min.y>-.002f,"press depresses normal to face without going below ground");Check(Mathf.Abs(down.center.x-start.center.x)<.0001f&&Mathf.Abs(down.center.z-start.center.z)<.0001f,"no horizontal drift");Check(Vector3.Distance(start.size,down.size)<.0001f,"stone remains rigid");Check(visual.CurrentClip=="Active"&&model.GetComponentInChildren<LineRenderer>().enabled,"pressed hold and activation indicator");
   visual.Render(false,.4f);Check(visual.CurrentClip=="Idle"&&!model.GetComponentInChildren<LineRenderer>().enabled,"release restores idle and removes indicator");UnityEngine.Object.Destroy(model);model=null;
   Setup();await Frames(5);Check(!Plate().IsPressed&&!Plate(4,4).IsPressed,"both uncovered plates start dormant");Capture("unpressed",new Vector3(3,1.5f,3));
   g.Move(1,0);await Frames(2);Check(g.Busy&&!g.State.tideActive&&!Plate().IsPressed,"candidate push does not activate before commit");g.CancelPendingAction();await Frames(5);Check(Q(2,3).prop==PropKind.Crate&&Q(3,3).prop==PropKind.Tide&&!Plate().IsPressed,"cancel push preserves dormant plate");
   g.Move(1,0);await UntilIdle();await Frames(35);Check(g.State.tideActive&&g.State.waterLevel==1&&Q(3,3).coveredProp==PropKind.Tide,"real push raises tide exactly one level");Check(Plate().IsPressed&&Plate().Compression>.999f,"covered plate survives terrain rebuild in active pose");Check(!Plate(4,4).IsPressed,"uncovered second plate remains dormant while global tide is high");Capture("pressed",new Vector3(3,1.5f,3));
   g.Pull(-1,0);await UntilIdle();float releaseStart=Plate().Compression;for(int f=0;f<24;f++){Capture("release-"+f.ToString("D2"),new Vector3(3,1.55f,3));await Frames(1);}Check(releaseStart>.5f&&Plate().Compression<.01f,"actual pull visibly progresses from pressed to released over frames");await Frames(35);Check(!g.State.tideActive&&g.State.waterLevel==0&&Q(3,3).prop==PropKind.Tide,"real pull releases tide and uncovers plate");Check(!Plate().IsPressed&&Plate().Compression<.001f,"release animation settles after terrain rebuild");
   g.Undo();await Frames(35);Check(g.State.tideActive&&Plate().IsPressed&&Plate().Compression>.999f,"undo restores covered plate pose");g.View.Initialize(g.State);await Frames(5);Check(Plate().IsPressed&&Plate().Compression>.999f,"load/reinitialize restores active pose immediately");
   g.LoadLevel(8);g.paused=true;await Frames(8);int expected=g.State.tiles.Count(t=>t.prop==PropKind.Tide||t.coveredProp==PropKind.Tide);var plates=UnityEngine.Object.FindObjectsByType<HabitatTideEngineVisual>(FindObjectsSortMode.None);Check(expected>0&&plates.Length==expected,"all fourth-level tide engines use delivered model");var engine=plates[0];Capture("fourth-level",engine.transform.position+Vector3.up*.05f);
   var after=Hashes(Path.Combine(Application.persistentDataPath,"Playtests"));Check(hashes.Count==after.Count&&hashes.All(k=>after.TryGetValue(k.Key,out var h)&&h==k.Value),"all real player save hashes unchanged");result="PASS "+count+" Tide Engine checks";
  }
  catch(Exception e){result=e.ToString();log.AppendLine(result);}
  finally
  {
   if(model)UnityEngine.Object.Destroy(model);g.CancelPendingAction();g.Hud.ClosePanels();typeof(HabitatGame).GetMethod("RestoreSnapshot",F).Invoke(g,new[]{snapshot});history.Clear();for(int i=oldHistory.Length-1;i>=0;i--)history.Push(oldHistory[i]);g.paused=paused;g.tool=tool;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);saveField.SetValue(null,oldSave);File.WriteAllText(Dir+"/checks.txt",log.ToString()+"\n"+result);AppDomain.CurrentDomain.SetData("TideEngineChecks.Result",result);
  }
 }
}



