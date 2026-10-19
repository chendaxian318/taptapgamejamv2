using System;using System.IO;using System.Linq;using System.Reflection;using System.Text;using System.Threading.Tasks;using UnityEngine;using UnityEngine.UI;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;using HabitatIsland;
public static class CloseExtraMouseChecks
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 const string Dir="Verification/close-panels-20261019";static HabitatGame g;static Mouse mouse;static StringBuilder log;static int count;
 static object Call(string name,params object[] args)=>typeof(HabitatHud).GetMethod(name,F).Invoke(g.Hud,args);
 static GameObject Panel(string name)=>(GameObject)typeof(HabitatHud).GetField(name,F).GetValue(g.Hud);
 static void Check(bool ok,string name){log.AppendLine((ok?"PASS ":"FAIL ")+(++count)+" "+name);if(!ok)throw new Exception(name);}
 static async Task Frames(int n=4){int end=Time.frameCount+n;double timeout=UnityEditor.EditorApplication.timeSinceStartup+8;while(Time.frameCount<end){if(UnityEditor.EditorApplication.timeSinceStartup>timeout)throw new Exception("frames stalled");await Task.Delay(15);}Canvas.ForceUpdateCanvases();}
 static async Task Click(Button b){await Frames();var rect=b.GetComponent<RectTransform>();Vector2 point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));InputSystem.QueueStateEvent(mouse,new MouseState{position=point});await Frames();InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});await Frames();InputSystem.QueueStateEvent(mouse,new MouseState{position=point});await Frames();}
 static Button X(GameObject p)=>p.GetComponentsInChildren<Button>().Single(b=>b.name=="Close "+p.name);
 static Button Button(string name)=>g.Hud.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
 static string Hashes(string p){if(!Directory.Exists(p))return "absent";using(var h=System.Security.Cryptography.SHA256.Create())return string.Join("\n",Directory.GetFiles(p,"*",SearchOption.AllDirectories).OrderBy(x=>x).Select(x=>x+Convert.ToBase64String(h.ComputeHash(File.ReadAllBytes(x)))));}
 public static string Begin(){AppDomain.CurrentDomain.SetData("CloseExtraMouseChecks","running");Run();return "started";}
 static async void Run()
 {
  g=HabitatGame.Instance;log=new StringBuilder();count=0;Directory.CreateDirectory(Dir);if(!g||g.Busy){AppDomain.CurrentDomain.SetData("CloseExtraMouseChecks","idle Play required");return;}
  var sf=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);var oldSave=sf.GetValue(null);string real=Path.Combine(Application.persistentDataPath,"Playtests"),hash=Hashes(real);sf.SetValue(null,Path.GetFullPath(Dir+"/mouse-isolated"));
  bool paused=g.paused,session=g.SessionStarted;var af=typeof(HabitatGame).GetField("autoSaveRequested",F);var oldAuto=af.GetValue(g);
  var ui=UnityEngine.Object.FindAnyObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();var oldDevices=ui.actionsAsset.devices;var oldEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;var oldMouse=Mouse.current;var oldBackground=InputSystem.settings.backgroundBehavior;
  var vt=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");var view=UnityEditor.EditorWindow.GetWindow(vt);var size=vt.GetProperty("selectedSizeIndex",F);var oldSize=size.GetValue(view);string result="incomplete";
  try{
   InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;mouse=InputSystem.AddDevice<Mouse>("ClosePanelTestMouse");mouse.MakeCurrent();ui.actionsAsset.devices=new UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>(new InputDevice[]{mouse});view.Focus();
   foreach(int resolution in new[]{3,4}){
    size.SetValue(view,resolution);await Frames(15);
    foreach(string name in new[]{"completionChoicesPanel"}){
     g.Hud.ClosePanels();var panel=Panel(name);Call("Open",panel,HabitatAudioCue.Open);await Frames();Check(panel.activeInHierarchy,"opened "+name+" at "+Screen.width);
     await Click(X(panel));Check(!panel.activeInHierarchy,"mouse down/up closes "+name);
    }
    g.Hud.ClosePanels();var map=g.Hud.GetComponentInChildren<HabitatMapSelectionPanel>(true);Call("Open",map.gameObject,HabitatAudioCue.Open);map.Show(5);await Frames();var x=map.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<TMPro.TMP_Text>()?.text=="×");await Click(x);Check(!g.Hud.ModalOpen,"map X closes at "+Screen.width);
   }result="PASS "+count+" checks";
  }catch(Exception ex){result="FAIL "+ex;}
  finally{ui.actionsAsset.devices=oldDevices;InputSystem.RemoveDevice(mouse);if(oldMouse!=null&&oldMouse.added)oldMouse.MakeCurrent();InputSystem.settings.editorInputBehaviorInPlayMode=oldEditorInput;InputSystem.settings.backgroundBehavior=oldBackground;size.SetValue(view,oldSize);g.Hud.ClosePanels();g.paused=paused;af.SetValue(g,oldAuto);typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);sf.SetValue(null,oldSave);if(!session)g.Hud.ShowStartMenu();if(hash!=Hashes(real))result="FAIL real saves changed";else log.AppendLine("PASS real saves SHA256 unchanged");log.AppendLine(result);File.WriteAllText(Dir+"/extra-mouse-checks.txt",log.ToString());AppDomain.CurrentDomain.SetData("CloseExtraMouseChecks",result);}
 }
}
