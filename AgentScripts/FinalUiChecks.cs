using System;using System.IO;using System.Linq;using System.Reflection;using System.Security.Cryptography;using System.Text;using System.Threading.Tasks;using HabitatIsland;using TMPro;using UnityEngine;using UnityEngine.UI;using UnityEngine.EventSystems;
public static class FinalUiChecks
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;const string Dir="Verification/ui-final-20261018";
 static HabitatGame g;static StringBuilder log;static int count;
 static T Field<T>(string name)=>(T)typeof(HabitatHud).GetField(name,F).GetValue(g.Hud);
 static object Call(string name,params object[] args)=>typeof(HabitatHud).GetMethod(name,F).Invoke(g.Hud,args);
 static void Check(bool ok,string name){log.AppendLine((ok?"PASS ":"FAIL ")+name);if(!ok)throw new Exception(name);count++;}
 static async Task Frames(int n=4){int until=Time.frameCount+n;double limit=UnityEditor.EditorApplication.timeSinceStartup+15;while(Time.frameCount<until){if(UnityEditor.EditorApplication.timeSinceStartup>limit)throw new Exception("Frame timeout");await Task.Delay(10);}}
 static async Task Shot(string name){await Frames();Canvas.ForceUpdateCanvases();var path=Path.GetFullPath(Dir+"/"+name+".png");ScreenCapture.CaptureScreenshot(path);await Frames(10);Check(File.Exists(path),"capture "+name);}
 static Button B(string name)=>g.Hud.GetComponentsInChildren<Button>(true).Last(b=>b.name==name);
 static void Open(string field){g.Hud.ClosePanels();Call("Open",Field<GameObject>(field),HabitatAudioCue.Open);g.Hud.Refresh();if(field=="shopPanel")Call("RefreshShopDelivery");}
 static string Hashes(string dir){using(var sha=SHA256.Create())return !Directory.Exists(dir)?"":string.Join("\n",Directory.GetFiles(dir,"*",SearchOption.AllDirectories).OrderBy(p=>p).Select(p=>p+":"+Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)))));}
 public static async Task<string> Run()
 {
  g=HabitatGame.Instance;log=new StringBuilder();count=0;Directory.CreateDirectory(Dir);if(!g||!Application.isPlaying)throw new Exception("Idle Play required");
  var sf=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);object oldSave=sf.GetValue(null);string real=Path.Combine(Application.persistentDataPath,"Playtests"),hash=Hashes(real);sf.SetValue(null,Path.GetFullPath(Dir+"/isolated-"+DateTime.Now.ToString("HHmmssfff")));
  object snapshot=typeof(HabitatGame).GetMethod("CaptureSnapshot",F).Invoke(g,null);bool paused=g.paused,session=g.SessionStarted;int tool=g.tool;
  var viewType=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");var view=UnityEditor.EditorWindow.GetWindow(viewType);var sizeProperty=viewType.GetProperty("selectedSizeIndex",F);int oldSize=(int)sizeProperty.GetValue(view);sizeProperty.SetValue(view,3);
  try
  {
   await Frames(20);Check(Screen.width==1920&&Screen.height==1080,"1920x1080 game view");Check(g.Fishing!=null,"HUD initialization completed");
   g.Hud.ShowStartMenu();Field<HabitatMenuEntrance>("menuEntrance").Skip();await Shot("main-menu");
   var start=B("Start new journey");Check(start.targetGraphic is RawImage&&((RawImage)start.targetGraphic).texture.name=="R5","main normal art");
   ExecuteEvents.Execute<IPointerEnterHandler>(start.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);Check(((RawImage)start.targetGraphic).texture.name=="Hover13","main hover art");await Shot("main-hover");ExecuteEvents.Execute<IPointerExitHandler>(start.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
   foreach(var raw in g.Hud.GetComponentsInChildren<RawImage>(true).Where(r=>r.name.StartsWith("Final UI")))Check(raw.texture,"texture loaded "+raw.name);
   g.Hud.ClosePanels();g.LoadLevel(5);g.paused=true;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);
   g.State.coins=1200;g.State.rod=true;g.State.specialRod=true;g.State.traps=2;g.State.fish=3;g.State.glassFish=1;g.State.shells=2;g.State.jellyfish=1;g.State.shrimp=2;g.View.Initialize(g.State);await Frames();
   foreach(var p in new[]{"pausePanel","inventoryPanel","shopPanel","audioPanel","guidePanel","bookPanel","summaryPanel"}){Open(p);if(p=="guidePanel")Call("RefreshGuide");await Shot(p);}
   Open("inventoryPanel");await Frames();Check(B("Inventory tool 7").gameObject.activeInHierarchy,"tool page 2 reachable");B("Inventory tool 7").onClick.Invoke();B("Confirm inventory selection").onClick.Invoke();Check(g.tool==7,"launcher selection retains binding");
   Open("pausePanel");B("Pause 生物图鉴").onClick.Invoke();Check(Field<GameObject>("bookPanel").activeInHierarchy,"pause book navigation");g.Hud.HandleEscape();Check(g.Hud.PauseMenuOpen,"book escape returns pause");
   Open("shopPanel");Call("OpenPurchase","magnet");await Shot("quantity");Check(Field<GameObject>("purchasePanel").activeInHierarchy,"quantity modal opens");Check(Field<GameObject>("shopPanel").activeInHierarchy&&!Field<GameObject>("shopPanel").GetComponent<CanvasGroup>().interactable,"quantity blocks underlying shop");
   var amount=Field<TMP_Text>("purchaseCount");Check(amount.text=="1","quantity initial value");g.Hud.HandleEscape();Check(g.Hud.ShopOpen&&!Field<GameObject>("purchasePanel").activeInHierarchy,"quantity cancel restores shop");
   g.State.campaignUnlockedStage=5;g.State.chapterOneDelivered=true;g.State.chapterOneAccepted=true;g.State.coins=1200;
   Open("shopPanel");Call("OpenPurchase","magnet");await Frames();
   var quantity=Field<GameObject>("purchasePanel");var plus=quantity.GetComponentsInChildren<Button>().Single(b=>{var label=b.GetComponentInChildren<TMP_Text>();return label&&label.text=="+";});var minus=quantity.GetComponentsInChildren<Button>().Single(b=>{var label=b.GetComponentInChildren<TMP_Text>();return label&&label.text=="−";});
   Check(plus.GetComponent<RectTransform>().anchoredPosition.x<minus.GetComponent<RectTransform>().anchoredPosition.x,"delivered left plus / right minus hit targets");
   var cursor=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,plus.transform.TransformPoint(plus.GetComponent<RectTransform>().rect.center))};var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(cursor,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==plus,"quantity plus is top raycast target");
   ExecuteEvents.Execute(plus.gameObject,cursor,ExecuteEvents.pointerClickHandler);Check(amount.text=="2","plus increments live quantity");ExecuteEvents.Execute(minus.gameObject,cursor,ExecuteEvents.pointerClickHandler);Check(amount.text=="1","minus decrements live quantity");ExecuteEvents.Execute(plus.gameObject,cursor,ExecuteEvents.pointerClickHandler);
   int magnets=g.State.magnets,coins=g.State.coins;var accept=Field<Button>("purchaseAccept");Check(accept.interactable,"valid two magnet purchase enabled");ExecuteEvents.Execute(accept.gameObject,cursor,ExecuteEvents.pointerClickHandler);Check(g.State.magnets==magnets+2&&g.State.coins==coins-600,"quantity confirm charges once and delivers two magnets");
   Open("shopPanel");Call("OpenPurchase","launcher");await Shot("quantity-valid");Check(Field<TMP_Text>("purchaseDetails").text.Contains("400"),"live launcher price 400 visible in confirmation");g.Hud.HandleEscape();Check(g.State.coins==coins-600,"cancel does not charge");
   var map=g.Hud.GetComponentInChildren<HabitatMapSelectionPanel>(true);Check(map,"map exists");g.Hud.ClosePanels();Call("Open",map.gameObject,HabitatAudioCue.Open);map.ConfigureCampaign(i=>i==5,i=>"完成上一关后解锁");map.Show(5);Check(map.gameObject.activeInHierarchy,"map visible through modal parent");await Shot("map");int level=g.State.levelIndex;map.SelectLevel(10);Check(g.State.levelIndex==level,"map selection never travels");await Shot("map-fifth");map.gameObject.SetActive(false);
   Open("guidePanel");Call("RefreshGuide");int page=Field<int>("guideIndex");Call("AdvanceGuide");Check(Field<int>("guideIndex")!=page,"guide next page functional");
   Check(HabitatRules.ShopPrice(g.State,"launcher")==400,"launcher price remains real 400, not artwork 150");
   g.Hud.ClosePanels();g.LoadLevel(1);g.paused=true;g.State.rod=true;var fish=g.State.animals.First(a=>a.species==Species.Goldfish);fish.offspring=1;g.State.playerX=fish.x;g.State.playerZ=fish.z;g.View.Initialize(g.State);await Frames();
   Check(g.Fishing.Begin(g,fish.x,fish.z),"fishing fixture opens real minigame");for(int i=0;i<14&&g.Fishing.IsCasting;i++)g.Fishing.Advance(.1f,false);Check(!g.Fishing.IsCasting,"cast completes before fishing UI capture");await Shot("fishing");int beforeCatch=g.State.fish;g.Fishing.Cancel(false);Check(g.State.fish==beforeCatch&&fish.offspring==1,"cancel fishing preserves catch and offspring");
   sizeProperty.SetValue(view,4);await Frames(20);Check(Screen.width==1366&&Screen.height==768,"1366x768 game view");Open("inventoryPanel");await Shot("inventory-1366");Open("audioPanel");await Shot("settings-1366");
   return "PASS "+count+" UI checks and captures. Controlled UI fixture, not campaign replay.";
  }
  finally
  {
   sizeProperty.SetValue(view,oldSize);g.Hud.ClosePanels();g.CancelPendingAction();typeof(HabitatGame).GetMethod("RestoreSnapshot",F).Invoke(g,new[]{snapshot});g.paused=paused;g.tool=tool;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);sf.SetValue(null,oldSave);Check(hash==Hashes(real),"real saves SHA256 unchanged");File.WriteAllText(Dir+"/checks.txt",log.ToString());
  }
 }
}
