using System;using System.IO;using System.Linq;using System.Reflection;using System.Text;using System.Threading.Tasks;using System.Collections.Generic;using UnityEngine;using UnityEngine.UI;using UnityEngine.EventSystems;using HabitatIsland;
public static class ClosePanelAudit
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 public static string Begin(){AppDomain.CurrentDomain.SetData("ClosePanelAudit","running");Run();return "started";}
 static async Task Wait(){await Task.Delay(180);Canvas.ForceUpdateCanvases();}
 static async void Run(){var g=HabitatGame.Instance;var log=new StringBuilder();if(!g){AppDomain.CurrentDomain.SetData("ClosePanelAudit","missing game");return;}
 var hud=g.Hud;var sf=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);var old=sf.GetValue(null);sf.SetValue(null,Path.GetFullPath("Verification/close-panels-20261019/isolated"));bool pause=g.paused;
 try{
 foreach(string field in new[]{"pausePanel","inventoryPanel","shopPanel","audioPanel","bookPanel","summaryPanel","guidePanel","purchasePanel","savesPanel","saveDetailsPanel","confirmPanel","chapterDialoguePanel","settingsPanel","helpPanel","homePanel","utilityPanel"}){
 var panel=(GameObject)typeof(HabitatHud).GetField(field,F).GetValue(hud);if(!panel)continue;hud.ClosePanels();typeof(HabitatHud).GetMethod("Open",F).Invoke(hud,new object[]{panel,HabitatAudioCue.Open});await Wait();
 var b=panel.GetComponentsInChildren<Button>().FirstOrDefault(x=>x.name=="Close "+panel.name);if(!b){log.AppendLine(field+" missing X");continue;}
 var rect=b.GetComponent<RectTransform>();var pos=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));var pointer=new PointerEventData(EventSystem.current){position=pos,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);var hit=hits.FirstOrDefault().gameObject;
 log.Append(field+" X="+b.name+" at="+pos+" hit="+(hit?hit.name:"none")+" busy="+g.Busy+" story="+g.StoryPlaying+" interact="+b.IsInteractable());
 if(hit)ExecuteEvents.ExecuteHierarchy(hit,pointer,ExecuteEvents.pointerClickHandler);await Wait();log.AppendLine(" closed="+!panel.activeInHierarchy+" modal="+hud.ModalOpen);
 }
 }catch(Exception ex){log.AppendLine(ex.ToString());}finally{hud.ClosePanels();g.paused=pause;sf.SetValue(null,old);if(!g.SessionStarted)hud.ShowStartMenu();Directory.CreateDirectory("Verification/close-panels-20261019");File.WriteAllText("Verification/close-panels-20261019/audit.txt",log.ToString());AppDomain.CurrentDomain.SetData("ClosePanelAudit",log.ToString());}
 }
}
