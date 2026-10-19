using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using HabitatIsland;
public static class MouseFacingChecks
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 const string Dir="Verification/release-playtest-20261018";
 static HabitatGame g;static Mouse mouse;static StringBuilder log;static int count;
 static object Call(string n,params object[] a)=>typeof(HabitatGame).GetMethod(n,F).Invoke(g,a);
 static void Check(bool ok,string msg){if(!ok)throw new Exception(msg);log.AppendLine("PASS "+(++count)+" "+msg);File.WriteAllText(Dir+"/mouse-runtime.txt",log.ToString());}
 static async Task Frames(int n=3){int end=Time.frameCount+n;while(Time.frameCount<end)await Task.Delay(4);}
 static async Task Wait(Func<bool> condition){float end=Time.realtimeSinceStartup+8;while(!condition()&&Time.realtimeSinceStartup<end)await Frames(1);if(!condition())throw new Exception("wait timeout");}
 static async Task Setup(){g.CancelPendingAction();g.Hud.ClosePanels();g.LoadLevel(1);g.paused=true;g.tool=0;g.State=new HabitatState{levelIndex=1,width=7,depth=7,tiles=Enumerable.Range(0,49).Select(_=>new Tile{height=1}).ToArray(),playerX=3,playerZ=3,dockX=0,dockZ=0,facingZ=1,openingCompleted=true};g.View.Initialize(g.State);g.Hud.ClosePanels();await Frames();}
 static async Task Click(int x,int z,bool right=false)
 {
  await Task.Delay(400); // Let camera easing and occlusion settle before choosing a screen pixel.
  Vector2 pixel=g.View.GameCamera.WorldToScreenPoint(new Vector3(x,HabitatRules.Height(g.State,x,z)*.7f+.05f,z));
  bool hit=g.View.Pick(pixel,out int px,out int pz)&&px==x&&pz==z;
  // A faded occluding top can intentionally pick through its center. Find a visible edge.
  if(!hit)foreach(float ox in new[]{-.4f,0f,.4f})foreach(float oz in new[]{-.4f,0f,.4f}){
   if(hit)continue;var p=(Vector2)g.View.GameCamera.WorldToScreenPoint(new Vector3(x+ox,HabitatRules.Height(g.State,x,z)*.7f+.01f,z+oz));
   if(g.View.Pick(p,out px,out pz)&&px==x&&pz==z){pixel=p;hit=true;}
  }
  if(!hit)throw new Exception("ray did not hit fixture tile "+x+","+z+" actual "+px+","+pz);
  InputSystem.QueueStateEvent(mouse,new MouseState{position=pixel});await Frames();
  File.AppendAllText(Dir+"/mouse-diagnostic.txt","click "+x+","+z+" pixel="+pixel+" modal="+g.Hud.ModalOpen+" busy="+g.Busy+" bg="+g.BackgroundSuspended+" session="+g.SessionStarted+" menu="+g.Hud.SessionMenuOpen+" mouse="+Mouse.current?.name+" overUI="+(UnityEngine.EventSystems.EventSystem.current?.IsPointerOverGameObject())+"\n");
  Action trace=()=>{if(mouse.leftButton.wasPressedThisFrame||mouse.rightButton.wasPressedThisFrame)File.AppendAllText(Dir+"/mouse-diagnostic.txt","input frame="+Time.frameCount+" type="+InputState.currentUpdateType+" current="+Mouse.current?.name+" down="+mouse.leftButton.isPressed+" fishing="+g.Fishing.IsOpen+" enabled="+g.enabled+"\n");};
  InputSystem.onAfterUpdate+=trace;
  InputSystem.QueueStateEvent(mouse,new MouseState{position=pixel,buttons=(ushort)(right?2:1)});await Frames(2);
  InputSystem.onAfterUpdate-=trace;
  InputSystem.QueueStateEvent(mouse,new MouseState{position=pixel});await Frames(2);
  File.AppendAllText(Dir+"/mouse-diagnostic.txt","after heading="+g.State.facingX+","+g.State.facingZ+" selected="+g.State.selectedX+","+g.State.selectedZ+" modal="+g.Hud.ModalOpen+"\n");
 }
 static bool Facing(int x,int z)=>g.State.facingX==x&&g.State.facingZ==z;
 public static async Task<string> Run()
 {
  g=HabitatGame.Instance;log=new StringBuilder("Actual InputSystem mouse clicks, camera ray picking, character rotation and tool authority checks.\n");count=0;
  var original=Call("CaptureSnapshot");bool session=g.SessionStarted;var oldMouse=Mouse.current;var settings=InputSystem.settings.backgroundBehavior;var editorSettings=InputSystem.settings.editorInputBehaviorInPlayMode;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
  var field=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);var oldDir=field.GetValue(null);field.SetValue(null,Path.GetFullPath(Dir+"/mouse-saves"));
  InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;mouse=InputSystem.AddDevice<Mouse>("FacingQAMouse");mouse.MakeCurrent();typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);
  try{
   await Setup();foreach(var d in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
    await Click(3+d.x,3+d.y);Check(Facing(d.x,d.y),"mouse changes cardinal heading "+d);
    Check(g.State.selectedX==3+d.x&&g.State.selectedZ==3+d.y&&g.State.playerX==3&&g.State.playerZ==3&&g.ActionCount==0,"click selects without movement or action count "+d);
    var visual=UnityEngine.Object.FindAnyObjectByType<HabitatPlayerVisual>();var root=(Transform)typeof(HabitatPlayerVisual).GetField("facingRoot",F).GetValue(visual);
    await Wait(()=>Vector3.Dot(root.forward,new Vector3(d.x,0,d.y))>.99f);Check(true,"visible model finishes turning "+d);
   }
   await Click(4,4);Check(Math.Abs(g.State.facingX)+Math.Abs(g.State.facingZ)==1&&g.State.selectedX==4&&g.State.selectedZ==4,"diagonal keeps exact tool cell and cardinal heading");
   int fx=g.State.facingX,fz=g.State.facingZ;g.Select(3,3);Check(Facing(fx,fz),"same-cell selection preserves heading");g.Select(-1,3);Check(g.State.selectedX==3&&Facing(fx,fz),"invalid tile is ignored");
   g.View.RotateCamera(90);await Frames(40);await Click(2,3);Check(Facing(-1,0),"camera orbit still turns toward clicked world tile");
   await Setup();HabitatRules.TileAt(g.State,4,3).height=2;g.State.shovels=1;g.tool=5;g.View.Initialize(g.State);await Frames();await Click(4,3,true);
   Check(Facing(1,0)&&g.Hud.ModalOpen,"right-click turns before opening shovel preview; heading="+g.State.facingX+","+g.State.facingZ+" selected="+g.State.selectedX+","+g.State.selectedZ+" modal="+g.Hud.ModalOpen);g.PreviewUtility(4,3,1,0,out bool valid);Check(valid&&g.State.shovels==1&&HabitatRules.TileAt(g.State,4,3).height==2,"shovel preview is valid and does not consume or dig");
   await Click(2,3);Check(Facing(1,0)&&g.State.selectedX==4,"modal blocks world retargeting");g.Hud.ClosePanels();
   await Setup();g.tool=7;g.State.launchers=1;HabitatRules.TileAt(g.State,4,3).prop=PropKind.Crate;g.View.Initialize(g.State);await Frames();await Click(4,3,true);
   Check(Facing(1,0)&&g.Hud.ModalOpen,"right-click launcher faces clicked crate");var candidate=g.PreviewUtility(4,3,0,1,out valid);Check(valid&&HabitatRules.TileAt(candidate,6,3).prop==PropKind.Crate,"launcher preview follows clicked heading");
   Check(g.ConfirmUtility(4,3,0,1)&&g.Busy&&g.State.launchers==1,"launcher keeps authority until motion ends");await Click(2,3);Check(Facing(1,0)&&g.State.selectedX==4,"click while launching cannot turn or replace target");await Wait(()=>!g.Busy);Check(g.State.launchers==0&&HabitatRules.TileAt(g.State,6,3).prop==PropKind.Crate&&g.ActionCount==1,"launch commits clicked direction once");
   await Setup();g.tool=7;g.State.launchers=1;HabitatRules.TileAt(g.State,5,3).prop=PropKind.Crate;g.View.Initialize(g.State);await Frames();await Click(5,3);g.PreviewUtility(5,3,1,0,out valid);Check(Facing(1,0)&&!valid&&g.State.launchers==1,"far click turns but never bypasses launcher range");
   InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(-500,-500),buttons=1});await Frames();InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(-500,-500)});await Frames();Check(Facing(1,0)&&g.State.selectedX==5,"off-board click preserves heading and target");
   return "PASS "+count+" mouse-facing checks";
  }catch(Exception e){File.AppendAllText(Dir+"/mouse-runtime.txt","FAIL "+e+"\n");throw;}
  finally{g.CancelPendingAction();g.Hud.ClosePanels();Call("RestoreSnapshot",original);typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);field.SetValue(null,oldDir);InputSystem.RemoveDevice(mouse);oldMouse?.MakeCurrent();InputSystem.settings.backgroundBehavior=settings;InputSystem.settings.editorInputBehaviorInPlayMode=editorSettings;}
 }
}


