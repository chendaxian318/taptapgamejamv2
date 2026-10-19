using System;using System.IO;using System.Linq;using System.Reflection;using System.Text;using System.Threading.Tasks;
using HabitatIsland;using TMPro;using UnityEngine;using UnityEngine.UI;
public static class UiBatch2Checks
{
 const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;static HabitatGame g;static int n;static StringBuilder log;
 static T Field<T>(string name)=>(T)typeof(HabitatHud).GetField(name,F).GetValue(g.Hud);
 static object Call(string name,params object[] args)=>typeof(HabitatHud).GetMethod(name,F).Invoke(g.Hud,args);
 static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);log.AppendLine("PASS "+(++n)+" "+label);}
 static async Task Frames(int count=5){int end=Time.frameCount+count;while(Time.frameCount<end)await Task.Yield();}
 static async Task Capture(string name){await Frames();Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/ui-batch2-20261017/"+name+".png"));await Frames(12);}
 static Button B(string name)=>g.Hud.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
 static void Open(string field){g.Hud.ClosePanels();Call("Open",Field<GameObject>(field),HabitatAudioCue.Open);g.Hud.Refresh();}
 public static async Task<string> Run()
 {
  await Frames(10);g=UnityEngine.Object.FindAnyObjectByType<HabitatGame>();n=0;log=new StringBuilder("2026-10-18 UI correction: controlled fixtures, not campaign completion.\n");
  Directory.CreateDirectory("Verification/ui-batch2-20261017");
  var old=typeof(HabitatGame).GetMethod("CaptureSnapshot",F).Invoke(g,null);bool paused=g.paused,session=g.SessionStarted;int tool=g.tool;
  string save=Path.Combine(Application.persistentDataPath,"Playtests/latest.json");byte[] saved=File.Exists(save)?File.ReadAllBytes(save):null;
  try
  {
   Check(g.Fishing!=null,"game initialization reaches fishing; font fallback does not abort Awake");
   Check(HabitatUiAssets.Body&&HabitatUiAssets.Bold&&HabitatUiAssets.Latin.fallbackFontAssetTable.Contains(HabitatUiAssets.Body),"three delivered fonts and Chinese fallback ready");
   g.Hud.ClosePanels();g.LoadLevel(5);g.paused=true;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);await Frames(8);
   g.State.playerX=g.State.dockX;g.State.playerZ=g.State.dockZ;g.State.coins=60;g.State.fish=3;g.State.shrimp=2;g.State.glassFish=1;g.State.rod=true;g.State.specialRod=true;g.State.traps=2;
   g.View.Initialize(g.State);await Frames(8);
   typeof(HabitatHud).GetField("inventoryToolPage",F).SetValue(g.Hud,0);Open("inventoryPanel");await Capture("backpack-corrected");
   Check(Field<TMP_Text>("inventorySummary").text.Contains("60")&&Field<TMP_Text>("bagCatch").text.Contains("3"),"bag shows actual money and catches");
   Check(!Field<GameObject>("inventoryPanel").GetComponentsInChildren<RawImage>().Any(i=>i.name=="BagEquipment"||i.name=="BagCreatures"),"incorrect screenshot label crops removed");
   var icon=Field<GameObject>("inventoryPanel").GetComponentsInChildren<RawImage>().Single(i=>i.name=="Icon_Hero");Check(icon.texture.name=="HeroFront"&&icon.material.shader.name=="Habitat/UI Draft Portrait","bag uses existing artist front illustration with runtime white backing removed");
   B("Next tool page").onClick.Invoke();await Frames();Check(B("Inventory tool 7").gameObject.activeInHierarchy&&!B("Inventory tool 0").gameObject.activeInHierarchy,"both four-tool pages reachable");
   B("Inventory tool 7").onClick.Invoke();Check(g.tool==7,"tool button selects existing launcher mechanic");
   g.State.bagProps.Clear();for(int i=0;i<7;i++)g.State.bagProps.Add(PropKind.Float);Open("inventoryPanel");B("Next stored page").onClick.Invoke();await Frames();
   Check(Field<int>("bagPage")==1&&Field<Button[]>("storedItems").Count(b=>b.gameObject.activeInHierarchy)==2,"five-cell storage paging retains seventh item without overlap");
   Open("pausePanel");await Capture("pause-corrected");
   B("Pause 生物图鉴").onClick.Invoke();await Capture("book-corrected");Check(Field<GameObject>("bookPanel").activeInHierarchy,"pause opens illustrated creature book");g.Hud.HandleEscape();Check(g.Hud.PauseMenuOpen,"book Escape returns to pause");
   Open("audioPanel");await Capture("settings-corrected");Check(Field<Slider>("ambienceSlider")&&Field<Slider>("brightnessSlider"),"settings has operational sound and brightness sliders");
   Open("guidePanel");Call("RefreshGuide");await Capture("guide-corrected");
   Open("summaryPanel");Call("RefreshSummary");await Capture("summary-corrected");Check(Field<TMP_Text>("summaryDetails").text.Contains("探索进行中")&&g.State.coins==60,"summary reports actual state without granting rewards");
   Open("shopPanel");Call("RefreshShopDelivery");await Capture("shop-corrected");
   Check(Field<GameObject>("shopPanel").GetComponentsInChildren<HabitatUiSurface>().Count(x=>x.name.StartsWith("Shop stock slot "))==12,"shop matches three rows of four stock cells");
   int beforeTraps=g.State.traps;g.PreviewPurchase("trap",2,out bool valid);Check(valid&&g.State.coins==60&&g.State.traps==beforeTraps,"purchase preview is read only");
   B("Buy trap").onClick.Invoke();await Capture("purchase-corrected");Check(Field<GameObject>("shopPanel").activeInHierarchy&&!Field<GameObject>("shopPanel").GetComponent<CanvasGroup>().interactable,"quantity dialog retains shop background and blocks underlying buttons");
   g.Hud.HandleEscape();Check(g.Hud.ShopOpen&&!Field<GameObject>("purchasePanel").activeInHierarchy&&Field<GameObject>("shopPanel").GetComponent<CanvasGroup>().interactable,"cancel quantity returns to interactive shop");
   int undo=g.UndoCount;Check(g.BuyQuantity("trap",2)&&g.State.coins==54&&g.State.traps==beforeTraps+2&&g.UndoCount==undo+1,"two-unit purchase charges once and records one undo");g.Undo();Check(g.State.coins==60&&g.State.traps==beforeTraps,"purchase undo restores money and inventory");
   g.State.coins=4;Check(!g.BuyQuantity("trap",2)&&g.State.coins==4&&g.State.traps==beforeTraps,"insufficient total balance prevents partial purchase");g.State.coins=60;
   g.State.boatCrates=2;Check(!g.BuyQuantity("crate",2)&&g.State.boatCrates==2&&g.State.coins==60,"boat capacity failure is atomic");
   g.Hud.ClosePanels();g.LoadLevel(1);g.paused=true;g.State.rod=true;var fish=g.State.animals.First(a=>a.species==Species.Goldfish);fish.offspring=1;g.State.playerX=fish.x;g.State.playerZ=fish.z;g.View.Initialize(g.State);await Frames();
   Check(g.Fishing.Begin(g,fish.x,fish.z),"fishing opens with eligible offspring");await Capture("fishing-corrected");
   var card=g.Fishing.GetComponentsInChildren<Image>().Single(i=>i.name=="Fishing card");Check(card.color.a==0,"large opaque fishing card removed");int caught=g.State.fish;g.Fishing.Cancel(false);Check(g.State.fish==caught&&fish.offspring==1,"cancel fishing does not collect or lose offspring");
   return "PASS "+n+" checks; nine captured UI screens.";
  }
  finally
  {
   if(g.Fishing)g.Fishing.Cancel(false);g.Hud.ClosePanels();g.CancelPendingAction();typeof(HabitatGame).GetMethod("RestoreSnapshot",F).Invoke(g,new[]{old});g.paused=paused;g.tool=tool;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);
   Check(saved==null?!File.Exists(save):saved.SequenceEqual(File.ReadAllBytes(save)),"persistent user save unchanged");
   File.WriteAllText("Verification/ui-batch2-20261017/checks.txt",log.ToString());
  }
 }
}
