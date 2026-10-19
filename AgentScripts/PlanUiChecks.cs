using System;using System.IO;using System.Linq;using System.Reflection;using System.Text;using System.Threading.Tasks;
using HabitatIsland;using TMPro;using UnityEngine;using UnityEngine.UI;
public static class PlanUiChecks
{
 const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;static HabitatGame g;static int count;static StringBuilder log;
 static T Field<T>(string name)=>(T)typeof(HabitatHud).GetField(name,F).GetValue(g.Hud);
 static object Call(string name,params object[] args)=>typeof(HabitatHud).GetMethod(name,F).Invoke(g.Hud,args);
 static void Set(string name,object value)=>typeof(HabitatHud).GetField(name,F).SetValue(g.Hud,value);
 static Button B(string name)=>g.Hud.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
 static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);log.AppendLine("PASS "+(++count)+" "+label);}
 static async Task Frames(int n=5){int end=Time.frameCount+n;while(Time.frameCount<end)await Task.Yield();}
 static async Task Capture(string name){await Frames();Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/plan-ui-20261018/"+name+".png"));await Frames(10);}
 static readonly string[] keys={"move","pull","climb","rod","basket","fan","crab","jellyfish","tide","shovel","magnet","launcher"};
 static void MarkGuides(){g.State.seenGuideKeys=keys.ToList();}
 static void Open(string name){g.Hud.ClosePanels();Call("Open",Field<GameObject>(name),HabitatAudioCue.Open);g.Hud.Refresh();}
 public static async Task<string> Run()
 {
  g=UnityEngine.Object.FindAnyObjectByType<HabitatGame>();log=new StringBuilder("Latest plan UI controlled verification; not a campaign playthrough.\n");count=0;
  Directory.CreateDirectory("Verification/plan-ui-20261018");
  var snapshot=typeof(HabitatGame).GetMethod("CaptureSnapshot",F).Invoke(g,null);bool paused=g.paused,session=g.SessionStarted;int tool=g.tool;string lastExport=g.LastExport;
  var overrideField=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.NonPublic|BindingFlags.Static);var oldDir=overrideField.GetValue(null);
  string isolated=Path.GetFullPath("Verification/plan-ui-20261018/save-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(isolated);
  bool muted=HabitatAudio.Ensure().Muted,hadMute=PlayerPrefs.HasKey("HabitatIsland.Audio.Muted");
  string player=Path.Combine(Application.persistentDataPath,"Playtests");
  string[] playerFiles={"latest.json","slot-1.json","slot-2.json","slot-3.json"};
  byte[][] originals=playerFiles.Select(f=>File.Exists(Path.Combine(player,f))?File.ReadAllBytes(Path.Combine(player,f)):null).ToArray();
  try
  {
   overrideField.SetValue(null,isolated);g.Hud.ClosePanels();g.LoadLevel(5);g.paused=true;g.tool=0;Set("inventoryToolPage",0);Set("bagPage",0);Set("shopSelling",false);
   typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);
   g.State.openingCompleted=true;g.State.chapterOneAccepted=g.State.chapterOneSailed=true;g.State.chapterOneDelivered=g.State.chapterOneMorningSeen=true;g.State.campaignUnlockedStage=5;g.State.campaignBriefedMask=60;
   MarkGuides();g.State.rod=g.State.specialRod=true;g.State.backBasketUnlocked=true;g.State.coins=1600;g.State.fish=2;g.State.shrimp=1;g.State.shells=1;g.State.jellyfish=1;
   g.State.playerX=g.State.dockX;g.State.playerZ=g.State.dockZ;g.View.Initialize(g.State);
   await Frames(10);g.Hud.ClosePanels();g.Hud.Refresh();await Capture("hud-clean");
   Check(!Field<GameObject>("settingsPanel").activeInHierarchy&&!Field<GameObject>("inspectorPanel").activeInHierarchy&&!Field<GameObject>("toolsPanel").activeInHierarchy,"old debugging cards hidden behind delivered HUD");
   int chest=Array.FindIndex(g.State.tiles,t=>t.prop==PropKind.Chest);Check(chest>=0,"first formal island retains its actual treasure chest");
   int px=g.State.playerX,pz=g.State.playerZ,sx=g.State.selectedX,sz=g.State.selectedZ;
   g.State.playerX=chest%g.State.width;g.State.playerZ=chest/g.State.width-1;g.State.selectedX=chest%g.State.width;g.State.selectedZ=chest/g.State.width;g.View.Initialize(g.State);
   await Frames(10);Call("RefreshPlanHud");await Capture("hud-click");
   Check(Field<GameObject>("contextPanel").activeInHierarchy&&Field<GameObject>("contextPanel").GetComponentInChildren<RawImage>().texture!=null,"legal nearby chest shows supplied transparent CLICK bubble");
   g.State.playerX=px;g.State.playerZ=pz;g.State.selectedX=sx;g.State.selectedZ=sz;g.View.Initialize(g.State);
   Open("inventoryPanel");B("Next tool page").onClick.Invoke();await Frames();
   Check(!g.Hud.PlayClockSuspended,"inventory remains active play time");
   B("Inventory tool 7").onClick.Invoke();Check(g.tool==0&&Field<GameObject>("bagActionPanel").activeSelf,"inventory selection requires explicit hand-held confirmation");await Capture("bag-selection");
   B("Confirm inventory selection").onClick.Invoke();Check(g.tool==7&&!g.Hud.InventoryOpen,"confirmed inventory tool uses existing game tool selector");
   int selected=g.tool;B("Confirm inventory selection").onClick.Invoke();Check(g.tool==selected,"hidden confirmation cannot replay");
   g.State.bagProps.Add(PropKind.Float);Open("inventoryPanel");B("Stored item 0").onClick.Invoke();Check(g.State.carryingProp==PropKind.None&&g.State.bagProps.Count==1,"stored prop selection leaves inventory unchanged until confirmation");
   g.Hud.ClosePanels();Open("pausePanel");await Capture("pause");B("Pause 保存进度").onClick.Invoke();Check(Field<GameObject>("savesPanel").activeInHierarchy,"pause save opens three-slot management");
   Check(g.Hud.PlayClockSuspended,"save subpage suspends play clock");
   Call("ShowSaveDetails",1);Check(B("Save selected slot").interactable&&!B("Load selected slot").interactable,"empty slot permits save but not read");
   B("Save selected slot").onClick.Invoke();Check(g.GetSaveSlotInfo(1).readable&&g.GetSaveSlotInfo(1).mapName==g.State.levelName,"slot 1 saves current island metadata in isolated test folder");
   g.State.coins=1777;Check(g.SaveSession(2),"second slot saves independently");g.State.coins=1888;Check(g.SaveSession(3),"third slot saves independently");
   Call("OpenSaveSlots",Field<GameObject>("pausePanel"),false);Check(Field<GameObject>("savesPanel").activeInHierarchy,"opening save management twice remains on the three-slot page");await Capture("save-slots");Call("ShowSaveDetails",2);await Capture("save-details");
   B("Delete selected slot").onClick.Invoke();Check(g.GetSaveSlotInfo(2).exists&&Field<GameObject>("confirmPanel").activeInHierarchy,"delete prompts before touching selected save");Call("CancelConfirmation");Check(g.GetSaveSlotInfo(2).exists,"cancel delete preserves slot");
   Check(g.LoadSession(1)&&g.State.coins==1600,"load restores selected slot rather than last-written slot");MarkGuides();
   Check(g.DeleteSession(2)&&!g.GetSaveSlotInfo(2).exists&&g.GetSaveSlotInfo(1).exists&&g.GetSaveSlotInfo(3).exists,"delete removes selected slot only");
   File.WriteAllText(Path.Combine(isolated,"slot-2.json"),"not json");int coins=g.State.coins;Check(!g.LoadSession(2)&&g.State.coins==coins,"invalid slot does not replace current state");
   g.State.message="";Call("OpenAudioSettings",Field<GameObject>("pausePanel"));bool beforeMute=HabitatAudio.Instance.Muted;B("Toggle Audio Mute").onClick.Invoke();Check(HabitatAudio.Instance.Muted!=beforeMute,"delivered settings adds working mute control");await Capture("settings");g.Hud.HandleEscape();Check(g.Hud.PauseMenuOpen,"settings returns to its originating pause panel");
   Open("shopPanel");Call("RefreshShopDelivery");await Capture("shop-current-prices");
   Check(Field<System.Collections.Generic.List<Button>>("shopEntries").Count(x=>x.gameObject.activeInHierarchy)==5,"formal shop shows five real goods within twelve-frame design");
   Check((string)Call("ShopPriceText","crate")=="150 贝币 · 船舱限三箱"&&(string)Call("ShopPriceText","launcher")=="400 贝币","shop uses unified confirmed prices");
   Check((string)Call("ShopPriceText","sell")=="合计 850 贝币","selling preview includes fish shrimp shell and jellyfish");
   B("Shop sell tab").onClick.Invoke();Check(Field<System.Collections.Generic.List<Button>>("shopEntries").Count(x=>x.gameObject.activeInHierarchy)==4,"SOLD switches shelf to four actual catch types");await Capture("shop-selling");
   Call("OpenSale",Species.Goldfish);int beforeSale=g.State.coins;B("Confirm purchase quantity").onClick.Invoke();Check(g.State.fish==1&&g.State.coins==beforeSale+100,"selected one-fish sale leaves other catches unchanged");g.Undo();Check(g.State.fish==2&&g.State.coins==beforeSale,"individual sale undo restores catches and money");B("Shop buy tab").onClick.Invoke();
   Call("OpenPurchase","shovel");Set("purchaseQuantity",2);Call("RefreshPurchase");await Capture("purchase-current");
   int shovels=g.State.shovels,action=g.ActionCount;B("Confirm purchase quantity").onClick.Invoke();Check(g.State.coins==coins-600&&g.State.shovels==shovels+2&&g.ActionCount==action+1,"bulk purchase commits once using confirmed per-item price");
   g.Undo();Check(g.State.coins==coins&&g.State.shovels==shovels,"bulk purchase undo restores price and item count");
   g.Hud.ClosePanels();Call("OpenGuide",Field<GameObject>("pausePanel"));Set("guideIndex",7);Call("RefreshGuide");await Capture("guide-jellyfish");Check(Field<TMP_Text>("guideText").text.Contains("3 格"),"guide reflects confirmed three-cell jellyfish reach");Check(Field<HabitatGuideDemo>("guideDemo").gameObject.activeInHierarchy,"model guide retains original dark demonstration stage");g.Hud.ClosePanels();
   g.State.seenGuideKeys.Clear();Set("nextGuideAt",0f);Call("RefreshAutomaticGuide");Check(Field<GameObject>("guidePanel").activeInHierarchy&&g.State.seenGuideKeys.SequenceEqual(new[]{"move"}),"first encounter guide opens once and records saveable key");g.Hud.ClosePanels();Call("RefreshAutomaticGuide");Check(!Field<GameObject>("guidePanel").activeInHierarchy,"guide cooldown does not reopen immediately");MarkGuides();
   Open("bookPanel");Set("bookIndex",0);Call("RefreshBook");await Capture("book-updated");Check(Field<TMP_Text>("bookDetails").text.Contains("3 格"),"creature book uses latest interaction rule");
   Open("summaryPanel");g.State.won=true;Call("RefreshSummary");Call("ConfirmSummary");Check(Field<GameObject>("completionChoicesPanel").activeInHierarchy,"summary confirmation offers planner follow-up routes");await Capture("completion-routes");g.State.won=false;g.Hud.ClosePanels();
   g.Hud.OpenMapSelection(10);await Capture("map-fifth");Check(Field<HabitatMapSelectionPanel>("mapSelection").SelectedLevel==10,"fifth authored level is selectable without changing previous indices");
   Check(B("Select destination 5").GetComponent<Image>().color.a==0&&B("Confirm destination").GetComponent<RectTransform>().anchoredPosition.y>-260,"map retains floating art labels and top-centered departure ribbon");
   return "PASS "+count+" UI/save-slot/transaction checks.";
  }
  finally
  {
   overrideField.SetValue(null,oldDir);HabitatAudio.Ensure().SetMuted(muted);if(!hadMute)PlayerPrefs.DeleteKey("HabitatIsland.Audio.Muted");
   g.Hud.ClosePanels();g.CancelPendingAction();typeof(HabitatGame).GetMethod("RestoreSnapshot",F).Invoke(g,new[]{snapshot});g.paused=paused;g.tool=tool;g.LastExport=lastExport;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);
   for(int i=0;i<playerFiles.Length;i++){string path=Path.Combine(player,playerFiles[i]);Check(originals[i]==null?!File.Exists(path):originals[i].SequenceEqual(File.ReadAllBytes(path)),"user save preserved: "+playerFiles[i]);}
   File.WriteAllText("Verification/plan-ui-20261018/checks.txt",log.ToString());
  }
 }
}
