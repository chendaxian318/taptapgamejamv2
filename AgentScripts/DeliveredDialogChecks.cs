using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using HabitatIsland;
public static class DeliveredDialogChecks
{
 const string Dir="Verification/delivered-dialogs-20261019";
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static HabitatGame g;static StringBuilder log;static int count;
 static object Call(string n,params object[] args)=>typeof(HabitatGame).GetMethod(n,F).Invoke(g,args);
 static GameObject Panel(string n)=>(GameObject)typeof(HabitatHud).GetField(n,F).GetValue(g.Hud);
 static void Check(bool value,string text){log.AppendLine((value?"PASS ":"FAIL ")+(++count)+" "+text);if(!value)throw new Exception(text);}
 static async Task Wait(float seconds){double end=UnityEditor.EditorApplication.timeSinceStartup+seconds;while(UnityEditor.EditorApplication.timeSinceStartup<end)await Task.Delay(15);}
 static string Hashes(string d){if(!Directory.Exists(d))return "absent";using(var h=System.Security.Cryptography.SHA256.Create())return string.Join("\n",Directory.GetFiles(d,"*",SearchOption.AllDirectories).OrderBy(x=>x).Select(p=>p+":"+Convert.ToBase64String(h.ComputeHash(File.ReadAllBytes(p)))));}
 static void Click(string name){var b=g.Hud.GetComponentsInChildren<Button>(true).Single(x=>x.name==name);Check(b.IsActive()&&b.IsInteractable(),name+" is usable");b.onClick.Invoke();}
  static async Task Shot(string name){await Wait(.3f);Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/"+name+".png"));await Wait(.3f);}
 static void Hit(string name){Canvas.ForceUpdateCanvases();var b=g.Hud.GetComponentsInChildren<Button>(true).Single(x=>x.name==name);var position=RectTransformUtility.WorldToScreenPoint(null,b.GetComponent<RectTransform>().TransformPoint(b.GetComponent<RectTransform>().rect.center));var data=new PointerEventData(EventSystem.current){position=position};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,name+" receives pointer at visible center");}
 static async Task Title(bool session=false){g.Hud.ClosePanels();typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);g.Hud.ShowStartMenu();Panel("startPanel").GetComponent<HabitatMenuEntrance>().Skip();await Wait(.15f);}
 public static string Begin(){AppDomain.CurrentDomain.SetData("DeliveredDialogChecks","running");Run();return "started";}
 static async void Run()
 {
  g=HabitatGame.Instance;log=new StringBuilder();count=0;Directory.CreateDirectory(Dir);
  if(!Application.isPlaying||!g||g.Busy)throw new Exception("idle Play required");
  object original=Call("CaptureSnapshot");bool paused=g.paused,session=g.SessionStarted,opening=g.playOpeningOnNewGame;int tool=g.tool;
  var undo=(Stack<HabitatState>)typeof(HabitatGame).GetField("history",F).GetValue(g);var oldUndo=undo.ToArray();
  var flags=new[]{"chapterGreetingPending","chapterChestPending","chapterMorningGreetingPending","chapterReturnReminderShown","campaignReturnReminderLevel","autoSaveRequested"}.ToDictionary(n=>n,n=>typeof(HabitatGame).GetField(n,F).GetValue(g));
  var sf=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);object oldSave=sf.GetValue(null);
  string real=Path.Combine(Application.persistentDataPath,"Playtests"),hashes=Hashes(real),isolated=Path.GetFullPath(Dir+"/isolated-"+DateTime.Now.ToString("HHmmssfff"));
    sf.SetValue(null,isolated);string result="incomplete";
  var viewType=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");var view=UnityEditor.EditorWindow.GetWindow(viewType);var sizeProperty=viewType.GetProperty("selectedSizeIndex",F);int oldSize=(int)sizeProperty.GetValue(view);
  try{
   g.playOpeningOnNewGame=false;await Title();Check(!g.HasSavedSession,"empty folder has no save");Click("Start new journey");
   Check(g.SessionStarted&&!g.Hud.ModalOpen&&HabitatLevels.IsHomeHarbor(g.State),"no save enters home directly without confirmation");
   Check(g.SaveSession(1),"create isolated valid save");string json=File.ReadAllText(Path.Combine(isolated,"slot-1.json"));
   foreach(string file in new[]{"slot-1.json","slot-2.json","slot-3.json","latest.json","autosave.json","slot-2.json.bak"}){
    string folder=Path.Combine(isolated,file.Replace('.','-'));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,file),json);sf.SetValue(null,folder);
    await Title();g.playOpeningOnNewGame=true;var state=g.State;string saved=Hashes(folder);Click("Start new journey");await Wait(.2f);
    Check(g.HasSavedSession&&Panel("confirmPanel").activeInHierarchy,"existing "+file+" opens confirmation");
    Check(!g.SessionStarted&&!g.StoryPlaying&&ReferenceEquals(state,g.State),"no gameplay reset or opening before confirmation");
    Check(Panel("startPanel").activeInHierarchy&&!g.Hud.SessionMenuOpen&&g.Hud.PlayClockSuspended,"cover stays visible and confirmation is foreground");
    Check(!Panel("startPanel").GetComponent<CanvasGroup>().interactable&&!Panel("startPanel").GetComponent<CanvasGroup>().blocksRaycasts,"menu underneath cannot receive input");
    Check(Panel("confirmPanel").transform.GetSiblingIndex()>Panel("startPanel").transform.GetSiblingIndex(),"dialog draws above title cover");
    if(file=="slot-1.json"){ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/confirmation-before-game.png"));await Wait(.3f);}
    Click("Cancel plan confirmation");await Wait(.1f);
    Check(g.Hud.SessionMenuOpen&&!g.SessionStarted&&!g.StoryPlaying,"cancel stays on title");
    Check(Hashes(folder)==saved,"cancel preserves "+file);
   }
   await Title();Click("Start new journey");g.Hud.HandleEscape();Check(g.Hud.SessionMenuOpen&&!g.SessionStarted,"Escape cancels back to title");
   await Wait(.1f);Click("Start new journey");
   var close=Panel("confirmPanel").GetComponentsInChildren<Button>().Single(b=>b.name.StartsWith("Close "));close.onClick.Invoke();
   Check(g.Hud.SessionMenuOpen&&!g.SessionStarted,"X cancels back to title");
   await Wait(.1f);string before=Hashes((string)sf.GetValue(null));Click("Start new journey");Click("Accept plan confirmation");await Wait(.2f);
   Check(g.SessionStarted&&g.StoryPlaying&&!Panel("startPanel").activeInHierarchy&&!g.Hud.ModalOpen,"accept alone starts new game and opening");
   Check(Hashes((string)sf.GetValue(null))==before,"accept does not overwrite existing saves");
   Call("CancelJourneySequence");g.playOpeningOnNewGame=false;
   // Finish the accepted game's queued autosave before establishing a separate empty folder.
   await Wait(.4f);
   sf.SetValue(null,Path.Combine(isolated,"empty-return"));await Title(true);Click("Start new journey");
   Check(g.SessionStarted&&!g.Hud.ModalOpen,"returning to title without a disk save also enters directly");
   await Wait(.3f);await Title();
   typeof(HabitatHud).GetMethod("RequestQuit",F).Invoke(g.Hud,null);
   Check(Panel("confirmPanel").activeInHierarchy&&Panel("startPanel").activeInHierarchy,"quit confirmation also retains title background");
   Click("Cancel plan confirmation");Check(g.Hud.SessionMenuOpen,"quit cancel returns to title");
      foreach(int resolution in new[]{3,4}){
    sizeProperty.SetValue(view,resolution);await Wait(.5f);await Title();Click("Start new journey");await Wait(.2f);
    Check(Panel("confirmPanel").GetComponentsInChildren<RawImage>().All(x=>x.texture&&x.texture.name=="R104"),"confirmation uses supplied R104 artwork");
    Hit("Cancel plan confirmation");Hit("Accept plan confirmation");await Shot("confirmation-"+Screen.width);
    var message=(TMP_Text)typeof(HabitatHud).GetField("confirmText",F).GetValue(g.Hud);Check(!message.isTextOverflowing,"confirmation text fits");
    Click("Cancel plan confirmation");g.Hud.ClosePanels();int commits=0;
    string[] lines={"回来了！听说南边第一座小岛出现了一群琉璃鱼。你愿意帮我带两条回来吗？这次委托合计 100 贝币。","岛上的伙伴们需要合适的栖居环境。准备好钓竿后就去看看吧，完成委托再回来找我。","带两条琉璃鱼回来交给我，就能领取这次委托的报酬。"};
    g.Hud.ShowChapterDialogue("商店老板",lines,"接下委托",()=>commits++);await Wait(.2f);
    Check(Panel("chapterDialoguePanel").GetComponentsInChildren<RawImage>().Any(x=>x.texture&&x.texture.name=="R87"),"dialogue uses supplied R87 artwork");
    Hit("Advance chapter dialogue");Hit("Close chapter dialogue");await Shot("dialogue-"+Screen.width);
    var body=(TMP_Text)typeof(HabitatHud).GetField("chapterDialogueBody",F).GetValue(g.Hud);Check(!body.isTextOverflowing,"dialogue text fits");
    Click("Advance chapter dialogue");Check(commits==0&&body.text==lines[1],"next page does not commit quest");
    Click("Close chapter dialogue");Check(commits==0&&!g.Hud.ChapterDialogueOpen,"dismiss does not commit quest");
    g.Hud.ShowChapterDialogue("商店老板",lines,"接下委托",()=>commits++);
    Click("Advance chapter dialogue");Click("Advance chapter dialogue");await Shot("dialogue-final-"+Screen.width);Click("Advance chapter dialogue");
    g.Hud.AdvanceChapterDialogue();Check(commits==1&&!g.Hud.ChapterDialogueOpen,"final page commits exactly once");
   }
   result="PASS "+count+" checks";
  }catch(Exception ex){result="FAIL "+ex;}
  finally{
   sizeProperty.SetValue(view,oldSize);Call("CancelJourneySequence");g.CancelPendingAction();g.Hud.ClosePanels();Call("RestoreSnapshot",original);undo.Clear();for(int i=oldUndo.Length-1;i>=0;i--)undo.Push(oldUndo[i]);foreach(var p in flags)typeof(HabitatGame).GetField(p.Key,F).SetValue(g,p.Value);
   g.paused=paused;g.playOpeningOnNewGame=opening;g.tool=tool;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);
   bool unchanged=hashes==Hashes(real);sf.SetValue(null,oldSave);if(!session)g.Hud.ShowStartMenu();log.AppendLine(unchanged?"PASS original save SHA256 unchanged":"FAIL original saves changed");if(!unchanged)result="FAIL original saves changed";
   log.AppendLine(result);File.WriteAllText(Dir+"/checks.txt",log.ToString());AppDomain.CurrentDomain.SetData("DeliveredDialogChecks",result);
  }
 }
}
