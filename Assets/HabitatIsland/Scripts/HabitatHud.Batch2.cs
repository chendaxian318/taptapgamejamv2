using System;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace HabitatIsland
{
 public partial class HabitatHud
 {
  GameObject bookPanel,summaryPanel,guidePanel,purchasePanel,batchReturnPanel;
  TMP_Text bookName,bookDetails,bookCounts,summaryDetails,purchaseDetails,purchaseCount,shopBalance,guideTitle,guideText,bagCatch;
  RawImage bookImage;Button purchaseAccept;Slider ambienceSlider,effectsSlider,brightnessSlider;
  string purchaseItem;int purchaseQuantity=1,bookIndex,guideIndex;
  readonly HashSet<int> summarySeen=new HashSet<int>();
  readonly string[] shopItems={"fan","crate","shovel","magnet","launcher","sell","soil","float","tide","rod","trap"};
  readonly List<Button> shopEntries=new List<Button>();readonly List<TMP_Text> shopPrices=new List<TMP_Text>(),shopTitles=new List<TMP_Text>();
  readonly List<RawImage> shopImages=new List<RawImage>();readonly List<GameObject> shopFallbacks=new List<GameObject>();
  readonly Species[] saleSpecies={Species.Goldfish,Species.Shrimp,Species.Crab,Species.Jellyfish};
  readonly string[] saleNames={"普通鱼","龙虾","蟹壳","水母"},saleIcons={"Goldfish","Shrimp","Shell","Jellyfish"};
  bool shopSelling,purchaseSelling;Species purchaseSpecies;TMP_Text purchaseHeading;
  readonly string[] shopNames={"风箱","木箱","铲子","磁铁","发射器","出售渔获","土块","漂浮板","潮汐机关","钓竿","虾笼"};
  readonly string[] shopIcons={"Bellows",null,"Shovel","Magnet","Cannon","Goldfish",null,null,null,"Rod","Trap"};
  readonly Color ocean=new Color32(41,157,180,255),foam=new Color32(111,215,187,255),deepSea=new Color32(14,76,98,255);
  HabitatGuideDemo guideDemo;
  void Place(Component c,float x,float y,float w,float h)
  {var r=c.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
  RectTransform Skin(GameObject panel,Vector2 size,string title)
  {
   foreach(Transform child in panel.transform){child.gameObject.SetActive(false);Destroy(child.gameObject);}
   var r=panel.GetComponent<RectTransform>();r.sizeDelta=size;panel.GetComponent<Image>().color=Color.clear;
   var shadow=panel.GetComponent<Shadow>();if(shadow)shadow.enabled=false;
   if(!string.IsNullOrEmpty(title))Text(r,title,32,Color.white,new Vector2(32,-22),new Vector2(size.x-120,50),emphasis:true);
   TextButton(r,"×",size.x-62,-12,48,ClosePanelsFromInput,28).name="Close "+panel.name;
   return r;
  }
  Button SeaButton(Transform p,string label,Vector2 pos,Vector2 size,Action action)
  {var b=Button(p,label,pos,size,action,foam);b.GetComponentInChildren<TMP_Text>().color=deepSea;return b;}
  void ApplySecondDelivery()
  {
   HabitatBrightness.Ensure();
   BuildStyledPause();BuildStyledInventory();BuildStyledShop();BuildStyledAudio();BuildCreatureBook();BuildSummary();BuildGuide();BuildPurchase();BuildPlanInterface();ApplyFinalUiDelivery();
   foreach(var label in GetComponentsInChildren<TMP_Text>(true))if(label.font!=HabitatUiAssets.Latin)label.font=(label.fontStyle&FontStyles.Bold)!=0?HabitatUiAssets.Bold:HabitatUiAssets.Body;
   shopBalance.font=HabitatUiAssets.Latin;
   foreach(var panel in new[]{pausePanel,inventoryPanel,shopPanel,audioPanel,bookPanel,summaryPanel,guidePanel,purchasePanel})
    foreach(var b in panel.GetComponentsInChildren<Button>(true))if(b.name.StartsWith("Close "))b.transform.SetAsLastSibling();
   var handbook=GetComponentsInChildren<Button>(true).First(b=>b.name=="Toggle resident handbook");handbook.onClick.RemoveAllListeners();handbook.onClick.AddListener(()=>OpenBook());
   var help=helpPanel.GetComponentsInChildren<Button>(true).First(b=>b.name=="Show tutorial guide");help.onClick.RemoveAllListeners();help.onClick.AddListener(()=>OpenGuide(helpPanel));
  }
  void BuildStyledPause()
  {
   var p=BubblePanel(pausePanel,"菜单","PauseHeading",760);
   string[] labels={"继续游戏","保存进度","生物图鉴","退出游戏"};
   Action[] actions={ClosePanelsFromInput,()=>OpenSaveSlots(pausePanel,true),()=>OpenBook(pausePanel),RequestQuit};
   for(int i=0;i<labels.Length;i++){var b=TextButton(p,labels[i],174,-146-i*86,252,actions[i]);b.name="Pause "+labels[i];HabitatFinalUi.ButtonArt(b,"R"+(80+i),"Hover"+(80+i));}
   pauseFeedback=Text(p,"",17,Color.white,new Vector2(158,-510),new Vector2(290,74),TextAlignmentOptions.Center);
   TextButton(p,"背包 I",130,-653,100,ToggleInventory,18);
   TextButton(p,"设置",245,-653,100,()=>OpenAudioSettings(pausePanel),18);
   TextButton(p,"引导",360,-653,100,()=>OpenGuide(pausePanel),18);
   TextButton(p,"返回码头",210,-710,180,()=>game.ReturnToHarbor(),18).name="Pause return to harbor";
  }
  void BuildStyledInventory()
  {
   var p=Skin(inventoryPanel,new Vector2(1440,830),"");p.GetComponent<Image>().color=aqua;
   HabitatFinalUi.Art(p,"Background12",Vector2.zero,new Vector2(1440,830));
   Ribbon(p,"BagEquipment","TOOL",355,-80,242,red);
   Ribbon(p,"BagCreatures","CREATURE",200,-338,363,red);
   Ribbon(p,"BagItems","ITEM",200,-632,235,red);
   inventorySummary=Text(p,"",14,deepSea,new Vector2(630,-22),new Vector2(650,64));
   equippedIcon=HabitatUiAssets.Icon(p,"Rod",Vector2.zero,Vector2.zero);equippedIcon.gameObject.SetActive(false);
   int[] ids={0,3,2,6,7,5,1,4};
   string[] arts={"R127","R128","R126","R140","R141","R142",null,null};
   Vector2[] positions={new Vector2(640,-122),new Vector2(810,-122),new Vector2(980,-122),new Vector2(535,-650),new Vector2(705,-650),new Vector2(875,-650),new Vector2(640,-305),new Vector2(830,-305)};
   Vector2[] sizes={new Vector2(128,164),new Vector2(128,164),new Vector2(390,164),new Vector2(128,150),new Vector2(128,150),new Vector2(128,150),new Vector2(160,42),new Vector2(160,42)};
   for(int i=0;i<ids.Length;i++)
   {
    int id=ids[i];var b=TextButton(p,HabitatGame.ToolNames[id],positions[i].x,positions[i].y,sizes[i].x,()=>SelectBagTool(id),15);Place(b,positions[i].x,positions[i].y,sizes[i].x,sizes[i].y);b.name="Inventory tool "+id;inventoryTools[id]=b;
    if(arts[i]!=null){HabitatFinalUi.Art(b.transform,arts[i],Vector2.zero,sizes[i],true);Place(b.GetComponentInChildren<TMP_Text>(),0,-sizes[i].y+22,sizes[i].x,24);}
    b.GetComponentInChildren<TMP_Text>().color=deepSea;b.GetComponentInChildren<TMP_Text>().transform.SetAsLastSibling();if(arts[i]!=null)Place(b.GetComponentInChildren<TMP_Text>(),0,-sizes[i].y+1,sizes[i].x,23);
   }
   string[] cards={"R133","R134","R133","R125","R135"};string[] names={"普通鱼","龙虾","琉璃鱼","蟹壳","水母"};
   for(int i=0;i<5;i++){float x=535+i*170;HabitatFinalUi.Art(p,cards[i],new Vector2(x,-408),new Vector2(128,153),true);catchCounts[i]=Text(p,"",20,deepSea,new Vector2(x+7,-527),new Vector2(50,30),TextAlignmentOptions.Center,true);Text(p,names[i],15,deepSea,new Vector2(x,-571),new Vector2(128,26),TextAlignmentOptions.Center);}
   string[] storedArt={"R143","R144"};PropKind[] kinds={PropKind.Fan,PropKind.Crate};
   for(int i=0;i<2;i++){var kind=kinds[i];var b=TextButton(p,i==0?"风箱":"木箱",1045+i*170,-650,128,()=>{int index=game.State.bagProps.IndexOf(kind);if(index>=0)SelectBagItem(index);else inventoryFeedback.text=kind==PropKind.Crate?"船舱木箱请在渡口卸下。":"背包内没有已收起的风箱。";},15);Place(b,1045+i*170,-650,128,150);HabitatFinalUi.Art(b.transform,storedArt[i],Vector2.zero,new Vector2(128,150),true);b.GetComponentInChildren<TMP_Text>().color=deepSea;Place(b.GetComponentInChildren<TMP_Text>(),0,-150,128,24);b.GetComponentInChildren<TMP_Text>().transform.SetAsLastSibling();}
   bagCatch=Text(p,"",14,deepSea,new Vector2(535,-608),new Vector2(830,30));
   var stored=Shape(p,"Stored items drawer",500,-610,875,190,blue,24);stored.gameObject.SetActive(false);
   storedItems=new Button[5];for(int i=0;i<5;i++){int id=i;storedItems[i]=TextButton(stored,"",20+i*163,-35,150,()=>SelectBagItem(bagPage*5+id),17);storedItems[i].name="Stored item "+i;Place(storedItems[i],20+i*163,-35,150,95);}
   TextButton(stored,"下一页 ›",580,-137,140,()=>{bagPage=(bagPage+1)*5<game.State.bagProps.Count?bagPage+1:0;},18).name="Next stored page";
   TextButton(stored,"关闭",750,-137,95,()=>stored.gameObject.SetActive(false),18);
   TextButton(p,"收纳物品",1090,-305,160,()=>{stored.gameObject.SetActive(!stored.gameObject.activeSelf);stored.SetAsLastSibling();},18);
   Shape(p,"Bag feedback backing",20,-756,420,58,new Color32(105,209,181,245),12);
   inventoryFeedback=Text(p,"",15,deepSea,new Vector2(30,-764),new Vector2(400,45));
   cargoLoad=SeaButton(p,"装箱",new Vector2(28,-683),new Vector2(105,40),()=>game.TransferCargo(false));
   cargoUnload=SeaButton(p,"卸箱",new Vector2(145,-683),new Vector2(105,40),()=>game.TransferCargo(true));
   SeaButton(p,"收起手持",new Vector2(263,-683),new Vector2(148,40),()=>game.StoreInventoryItem());
  }

  void BuildStyledShop()
  {
   var p=Skin(shopPanel,new Vector2(1440,810),"");
   HabitatFinalUi.Art(p,"Background07",Vector2.zero,new Vector2(1440,810));
   Ribbon(p,"ShopBuy","BUY",88,-58,258,aqua);
   Ribbon(p,"ShopSold","SOLD",215,-330,245,blue);
   TextButton(p,"切换为出售",215,-380,240,()=>SetShopSelling(true),19).name="Sell delivered catch";
   var buyTab=TextButton(p,"",88,-58,258,()=>SetShopSelling(false));buyTab.name="Shop buy tab";
   var sellTab=TextButton(p,"",215,-330,245,()=>SetShopSelling(true));sellTab.name="Shop sell tab";

   shopBalance=Text(p,"",27,Color.white,new Vector2(641,-20),new Vector2(350,48),TextAlignmentOptions.Left,true);
   for(int i=0;i<12;i++)
   {
    float x=650+i%4*197,y=-130-i/4*219;Color frame=i<8?aqua:blue;
    Shape(p,"Shop stock slot "+i,x,y,143,165,frame,25);Shape(p,"Stock inset "+i,x+14,y-52,115,104,i<8?new Color32(170,230,213,255):new Color32(123,199,211,255),17);
    Shape(p,"Stock notch "+i,x+45,y-14,58,8,new Color32(172,230,213,255),4);
    if(i>=shopItems.Length)continue;
    int slot=i;string item=shopItems[i];var b=TextButton(p,"",x,y,143,()=>{if(shopSelling){if(slot<saleSpecies.Length)OpenSale(saleSpecies[slot]);}else if(item=="sell")SetShopSelling(true);else OpenPurchase(item);});Place(b,x,y,143,194);b.name="Buy "+item;shopEntries.Add(b);
    var label=Text(b.transform,shopNames[i],18,deepSea,new Vector2(4,-12),new Vector2(135,35),TextAlignmentOptions.Center,true);
    shopTitles.Add(label);var icon=HabitatUiAssets.Icon(b.transform,shopIcons[i]??"Missing",new Vector2(22,-53),new Vector2(100,100));shopImages.Add(icon);
    var fallback=Icon(b.transform,1,new Vector2(46,-75),52,deepSea);shopFallbacks.Add(fallback.gameObject);fallback.gameObject.SetActive(!icon.texture);
    var cost=Text(b.transform,ShopPriceText(item),15,deepSea,new Vector2(-9,-169),new Vector2(161,43),TextAlignmentOptions.Center);
    shopPrices.Add(cost);
    if(item=="fan"){fanShopButton=b;fanShopTitle=label;fanShopCost=cost;label.enableAutoSizing=true;label.fontSizeMin=12;label.fontSizeMax=18;cost.enableAutoSizing=true;cost.fontSizeMin=13;cost.fontSizeMax=15;}
    if(item=="sell"){sellButton=b;sellTitle=label;sellCost=cost;label.enableAutoSizing=true;label.fontSizeMin=12;label.fontSizeMax=18;cost.enableAutoSizing=true;cost.fontSizeMin=13;cost.fontSizeMax=15;}
   }
   HabitatFinalUi.Art(p,"R87",new Vector2(43,-555),new Vector2(515,220));
   shopMessage=Text(p,"",18,Color.white,new Vector2(65,-626),new Vector2(470,139));shopMessage.enableAutoSizing=true;shopMessage.fontSizeMin=15;shopMessage.fontSizeMax=18;
   TextButton(p,"交谈",435,-561,112,()=>game.OpenShopInteraction(),20).name="Talk to shopkeeper";
   TextButton(p,"菜单 ›",1270,-20,140,BackToGame,23).name="Back from shop";
  }
  string ShopPriceText(string item)
  {if(item=="sell")return "合计 "+HabitatRules.CatchSaleValue(game.State)+" 贝币";if(item=="soil"&&game.State.cargo>0)return "领取库存（不扣贝币）";return HabitatRules.ShopPrice(game.State,item)+" 贝币"+(item=="crate"?" · 船舱限三箱":"");}
  void BuildPurchase()
  {
   purchasePanel=ModalCard("Purchase quantity",new Vector2(1000,350),"购买数量","").gameObject;var p=Skin(purchasePanel,new Vector2(1000,350),"");
   Shape(p,"Quantity bubble",0,0,1000,350,turquoise,52);purchaseHeading=Text(p,"» 购买数量 «",29,deepSea,new Vector2(310,-26),new Vector2(380,55),TextAlignmentOptions.Center,true);
   purchaseDetails=Text(p,"",18,deepSea,new Vector2(90,-198),new Vector2(820,60),TextAlignmentOptions.Center);
   SeaButton(p,"−",new Vector2(361,-122),new Vector2(52,45),()=>{purchaseQuantity=Mathf.Max(1,purchaseQuantity-1);RefreshPurchase();});
   purchaseCount=Text(p,"1",28,deepSea,new Vector2(430,-120),new Vector2(140,48),TextAlignmentOptions.Center,true);
   SeaButton(p,"+",new Vector2(588,-122),new Vector2(52,45),()=>{purchaseQuantity=Mathf.Min(PurchaseLimit(),purchaseQuantity+1);RefreshPurchase();});
   for(int i=0;i<8;i++)HabitatUiMotif.Add(p,new Vector2(45+i*128,-137),new Vector2(14,18),i%2==0?red:new Color32(255,214,179,255));
   purchaseAccept=SeaButton(p,"确认",new Vector2(320,-280),new Vector2(155,44),()=>{if(!purchasePanel.activeInHierarchy)return;if(purchaseSelling?game.SellQuantity(purchaseSpecies,purchaseQuantity):game.BuyQuantity(purchaseItem,purchaseQuantity)){Open(shopPanel);RefreshShopDelivery();}else RefreshPurchase();});purchaseAccept.name="Confirm purchase quantity";
   SeaButton(p,"放弃",new Vector2(525,-280),new Vector2(155,44),()=>Open(shopPanel));purchasePanel.SetActive(false);
  }
  int PurchaseLimit()=>purchaseSelling?Mathf.Max(1,HabitatRules.CatchCount(game.State,purchaseSpecies)):purchaseItem=="trap"||purchaseItem=="shovel"||purchaseItem=="magnet"||purchaseItem=="launcher"?99:purchaseItem=="crate"&&HabitatLevels.IsFormal(game.State.levelIndex)?3:1;
  void OpenPurchase(string item){purchaseSelling=false;purchaseItem=item;purchaseQuantity=1;Open(purchasePanel);RefreshPurchase();}
  void RefreshPurchase()
  {
   if(finalQuantityArt)finalQuantityArt.texture=HabitatFinalUi.Texture(purchaseSelling?"R105":"R104");
   purchaseCount.text=purchaseQuantity.ToString();bool valid;var s=purchaseSelling?game.PreviewSale(purchaseSpecies,purchaseQuantity,out valid):game.PreviewPurchase(purchaseItem,purchaseQuantity,out valid);
   purchaseAccept.GetComponent<Image>().color=valid?Color.clear:new Color(.3f,.5f,.5f,.55f);
   purchaseHeading.text=purchaseSelling?"» 出售数量 «":"» 购买数量 «";
   if(purchaseSelling){purchaseDetails.text=saleNames[Array.IndexOf(saleSpecies,purchaseSpecies)]+" × "+purchaseQuantity+"\n"+(valid?"获得 "+(s.coins-game.State.coins)+" 贝币 · 剩余 "+HabitatRules.CatchCount(s,purchaseSpecies):s.message);purchaseAccept.interactable=valid;return;}
   int index=Array.IndexOf(shopItems,purchaseItem);purchaseDetails.text=(index>=0?shopNames[index]:purchaseItem)+" × "+purchaseQuantity+"\n"+(valid?"合计 "+(game.State.coins-s.coins)+" 贝币 · 余额 "+game.State.coins:s.message);purchaseAccept.interactable=valid;
  }
  void SetShopSelling(bool selling){shopSelling=selling;RefreshShopDelivery();}
  void OpenSale(Species species){purchaseSelling=true;purchaseSpecies=species;purchaseQuantity=1;Open(purchasePanel);RefreshPurchase();}
  void BuildStyledAudio()
  {
   var p=BubblePanel(audioPanel,"设置","SettingsHeading",700);
   ambienceSlider=VolumeSlider(p,"声音",-225,v=>{var sound=HabitatAudio.Ensure();sound.SetAmbienceVolume(v);sound.SetEffectsVolume(v);RefreshAudioControls();},out ambienceVolumeLabel);
   brightnessSlider=VolumeSlider(p,"亮度",-365,v=>HabitatBrightness.Set(v),out var brightnessLabel);brightnessSlider.SetValueWithoutNotify(HabitatBrightness.Value);brightnessLabel.text="";
   // Separate mix and mute remain in the existing detailed settings, outside this two-slider design.
   effectsVolumeLabel=Text(p,"",1,Color.clear,Vector2.zero,Vector2.one);muteAudioLabel=Text(p,"",1,Color.clear,Vector2.zero,Vector2.one);
   var confirm=SeaButton(p,"确认",new Vector2(236,-474),new Vector2(130,50),()=>{ReturnFromAudioSettings();});confirm.GetComponent<Image>().color=deepSea;confirm.GetComponentInChildren<TMP_Text>().color=Color.white;confirm.name="Back from Audio Settings";
   var mute=TextButton(p,"静音",390,-176,78,()=>{var sound=HabitatAudio.Ensure();sound.SetMuted(!sound.Muted);RefreshAudioControls();},16);mute.name="Toggle Audio Mute";muteAudioLabel=mute.GetComponentInChildren<TMP_Text>();
  }
  Slider VolumeSlider(Transform p,string title,float y,Action<float> change,out TMP_Text value)
  {
   Text(p,title,29,Color.white,new Vector2(180,y+72),new Vector2(240,50),TextAlignmentOptions.Center,true);
   value=Text(p,"",16,Color.clear,new Vector2(470,y+25),new Vector2(10,20));
   Text(p,"0",15,Color.white,new Vector2(139,y+12),new Vector2(26,30));Text(p,"100",15,Color.white,new Vector2(444,y+12),new Vector2(48,30));
   var track=Box(title+" slider",p,new Vector2(0,1),new Vector2(0,1),new Vector2(175,y),new Vector2(262,28),Color.clear,true);
   Shape(track,"White scale",0,-12,262,6,Color.white,3);
   var handle=Box("Handle",track,new Vector2(0,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(30,40),Color.clear);
   HabitatUiMotif.Add(handle,new Vector2(6,-7),new Vector2(22,23),red);
   var slider=track.gameObject.AddComponent<Slider>();slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.minValue=0;slider.maxValue=1;slider.onValueChanged.AddListener(v=>change(v));return slider;
  }
  void BuildCreatureBook()
  {
   bookPanel=ModalCard("Creature book",new Vector2(1440,810),"生物图鉴","").gameObject;var p=Skin(bookPanel,new Vector2(1440,810),"");
   HabitatFinalUi.Art(p,"Background05",Vector2.zero,new Vector2(1440,810));
   Shape(p,"Creature collage",36,-130,975,580,new Color32(139,203,182,255),45);
   Heading(p,"BookHeading","生物图鉴",335,-82,375);
   Shape(p,"Main creature tile",200,-315,400,350,turquoise,35);
   Shape(p,"Upper creature tile",110,-198,246,104,blue,0);
   Shape(p,"Small tile",100,-325,122,122,turquoise,0);
   Shape(p,"Upper capsule",380,-198,195,104,turquoise,50);
   Shape(p,"Dark square",602,-198,251,176,new Color32(3,121,135,255),0);
   Shape(p,"Tall capsule",877,-198,106,176,new Color32(3,121,135,255),30);
   Shape(p,"Wide capsule",602,-392,381,124,new Color32(15,101,129,255),62);
   Bubble(p,601,-543,147,new Color32(15,101,129,255));Shape(p,"Bottom capsule",767,-555,215,140,turquoise,70);
   bookImage=HabitatUiAssets.Icon(p,"Goldfish",new Vector2(203,-312),new Vector2(386,361));
   var shrimp=TextButton(p,"",777,-548,200,()=>{bookIndex=1;RefreshBook();});Place(shrimp,777,-548,200,143);HabitatUiAssets.Icon(shrimp.transform,"Shrimp",Vector2.zero,new Vector2(190,140));
   var fish=TextButton(p,"",77,-470,139,()=>{bookIndex=2;RefreshBook();});Place(fish,77,-470,139,200);HabitatUiAssets.Icon(fish.transform,"Goldfish",Vector2.zero,new Vector2(139,180));
   var crab=TextButton(p,"",105,-195,245,()=>{bookIndex=3;RefreshBook();});Place(crab,105,-195,245,110);HabitatUiAssets.Icon(crab.transform,"Crab",Vector2.zero,new Vector2(235,110));
   bookName=Text(p,"",26,deepSea,new Vector2(1050,-180),new Vector2(340,85),emphasis:true);
   bookDetails=Text(p,"",22,deepSea,new Vector2(1050,-286),new Vector2(340,335));bookDetails.enableAutoSizing=true;bookDetails.fontSizeMin=19;bookDetails.fontSizeMax=22;
   bookCounts=Text(p,"",18,deepSea,new Vector2(1045,-645),new Vector2(350,100));
   for(int i=0;i<6;i++){int id=i;var b=TextButton(p,new[]{"水母","龙虾","金鱼","寄居蟹","陆龟","海星"}[i],145+i*139,-735,130,()=>{bookIndex=id;RefreshBook();},18);b.GetComponentInChildren<TMP_Text>().color=deepSea;}
   bookPanel.SetActive(false);
  }
  void OpenBook(GameObject origin=null){batchReturnPanel=origin;Open(bookPanel);RefreshBook();}
  void RefreshBook()
  {
   Species species=(Species)bookIndex;bookName.text="我叫什么 · "+new[]{"水母","龙虾","金鱼","寄居蟹","陆龟","海星"}[bookIndex];
   string[] icons={"Jellyfish","Shrimp","Goldfish","Crab",null,null};bookImage.texture=icons[bookIndex]!=null?HabitatUiAssets.IconTexture(icons[bookIndex]):null;bookImage.material=null;bookImage.uvRect=new Rect(0,0,1,1);bookImage.enabled=bookImage.texture;
   if(bookIndex==0&&!bookImage.texture){bookImage.texture=Resources.Load<Texture2D>("Art/CreatureSheet");bookImage.uvRect=new Rect(.012f,.66f,.14f,.34f);bookImage.material=BookWhiteMaterial;bookImage.enabled=true;}
   string[] interact={"鱼竿可在 3 格内牵引。连通外海且目标适居后即可捕捞，不必等待整关完成。","取得背篓后可抱起；携虾会惊退相邻动物。","先围住水域，使目标金鱼适居后即可垂钓；遇携虾最多后退 2 格。","利用携带的龙虾调整位置；满足环境后可拾取脱下的壳。","为附近居民提供庇护。","观察并整理连续岸线。"};
   var animals=game.State.animals.Where(a=>a.species==species&&!a.carried).ToArray();
   string rule=bookIndex==0?"当前正式关：需要与地图边界外海连通。":bookIndex==2?"正式岛金鱼需要封闭水域。":bookIndex==3?"正式关：邻水陆地；存在水生动物时，3×3 内还需有水中金鱼或水母。":"具体条件以当前岛屿的环境检查为准。";
   bookDetails.text="交互方式\n"+interact[bookIndex]+"\n\n环境规则\n"+rule;
   if(animals.Length>0)bookDetails.text+="\n\n当前个体检查\n"+HabitatRules.Evaluate(game.State,animals[0],game.tuning).reason;
   bookCounts.text="本岛数量 "+animals.Length+" · 可收获后代 "+animals.Sum(a=>a.offspring)+(animals.Length==0?"\n当前岛屿没有该物种。":"");
  }
  void BuildSummary()
  {
   summaryPanel=ModalCard("Chapter summary",new Vector2(600,740),"本岛记录","").gameObject;var p=BubblePanel(summaryPanel,"SUMMARY","SummaryHeading",740);
   string[] icons={"Goldfish","Shrimp","Crab"};for(int i=0;i<3;i++)HabitatUiAssets.Icon(p,icons[i],new Vector2(164+i*95,-155),new Vector2(83,83));
   summaryDetails=Text(p,"",22,Color.white,new Vector2(145,-263),new Vector2(320,285),TextAlignmentOptions.Center);
   var b=SeaButton(p,"确认",new Vector2(235,-556),new Vector2(130,50),ConfirmSummary);b.name="Confirm island summary";b.GetComponent<Image>().color=deepSea;b.GetComponentInChildren<TMP_Text>().color=Color.white;summaryPanel.SetActive(false);
  }
  void OpenSummary(GameObject origin=null){batchReturnPanel=origin;Open(summaryPanel);RefreshSummary();}
  void RefreshSummary(){var s=game.State;summaryDetails.text=(s.won?"目标达成":"探索进行中")+"\n"+s.levelName+"\n\n操作次数  "+game.ActionCount+"\n开启宝箱  "+s.openedChests+"\n鱼  "+s.fish+"    虾  "+s.shrimp+"\n琉璃鱼  "+s.glassFish+"    贝币  "+s.coins;}
  void BuildGuide()
  {
   guidePanel=ModalCard("Visual tutorial",new Vector2(1150,760),"新手引导","").gameObject;var p=Skin(guidePanel,new Vector2(1150,760),"");
   HabitatFinalUi.Art(p,"R122",new Vector2(15,-20),new Vector2(1120,655));
   var stage=Shape(p,"Operation demonstration",155,-175,840,390,deepSea,0);guideDemo=stage.gameObject.AddComponent<HabitatGuideDemo>();guideDemo.Build();
   Ribbon(p,"GuideVideo","VIDEO",805,-155,200,aqua);
   guideTitle=Text(p,"",23,Color.white,new Vector2(155,-580),new Vector2(840,38),TextAlignmentOptions.Center,true);
   Shape(p,"Guide instruction backing",110,-623,930,50,deepSea,18);
   guideText=Text(p,"",18,Color.white,new Vector2(125,-625),new Vector2(900,44),TextAlignmentOptions.Center);
   Shape(p,"Guide pager rail",385,-680,380,38,Color.white,16);
   var prev=TextButton(p,"«",340,-671,80,()=>{guideIndex=(guideIndex+GuidePages.Length-1)%GuidePages.Length;RefreshGuide();},35);prev.GetComponentInChildren<TMP_Text>().color=blue;
   var next=TextButton(p,"»",730,-671,80,()=>AdvanceGuide(),35);next.GetComponentInChildren<TMP_Text>().color=blue;
   guidePager=Text(p,"",17,deepSea,new Vector2(448,-681),new Vector2(254,36),TextAlignmentOptions.Center,true);
   TextButton(p,"跳过 / 关闭",870,-670,200,ClosePanelsFromInput,18).name="Skip visual guide";guidePanel.SetActive(false);
  }
  void OpenGuide(GameObject origin){automaticGuide=false;batchReturnPanel=origin;Open(guidePanel);RefreshGuide();}
  void RefreshGuide(){RefreshPlanGuide();}
  bool ReturnFromBatchPanel()
  {
   if(!ModalOpen)return false;if(ReturnFromPlanPanel())return true;if(purchasePanel&&purchasePanel.activeSelf){Open(shopPanel);return true;}
   if(audioPanel&&audioPanel.activeSelf){ReturnFromAudioSettings();return true;}
   if((bookPanel&&bookPanel.activeSelf)||(guidePanel&&guidePanel.activeSelf)||(summaryPanel&&summaryPanel.activeSelf))
   {var back=batchReturnPanel;batchReturnPanel=null;if(back)Open(back);else ClosePanels();return true;}return false;
  }
  void RefreshSecondDelivery()
  {
   RefreshPlanInterface();
   if(shopBalance)shopBalance.text=game.State.coins+"  贝币";
   if(InventoryOpen)
   {
    for(int i=0;i<8;i++)inventoryTools[i].gameObject.SetActive(true);
    string[] keys={null,"Shovel","Rod","Trap","Goldfish","Shovel","Magnet","Cannon"};equippedIcon.texture=keys[game.tool]!=null?HabitatUiAssets.IconTexture(keys[game.tool]):null;equippedIcon.enabled=equippedIcon.texture;
    int[] counts={game.State.fish,game.State.shrimp,game.State.glassFish,game.State.shells,game.State.jellyfish};for(int i=0;i<5;i++)catchCounts[i].text=counts[i]>0?counts[i].ToString():"";
   }
   if(InventoryOpen&&bagCatch)bagCatch.text="普通鱼 "+game.State.fish+"   龙虾 "+game.State.shrimp+"   琉璃鱼 "+game.State.glassFish+"   蟹壳 "+game.State.shells+"   水母 "+game.State.jellyfish;
   if(bookPanel&&bookPanel.activeInHierarchy)RefreshBook();if(summaryPanel&&summaryPanel.activeInHierarchy)RefreshSummary();
   if(ambienceSlider&&HabitatAudio.Instance){ambienceSlider.SetValueWithoutNotify(HabitatAudio.Instance.AmbienceVolume);if(effectsSlider)effectsSlider.SetValueWithoutNotify(HabitatAudio.Instance.EffectsVolume);}
   if(!game.State.won)summarySeen.Remove(game.State.levelIndex);
   else if(game.SessionStarted&&!game.State.onHarbor&&!game.Busy&&!game.StoryPlaying&&!ModalOpen&&!(game.Fishing&&game.Fishing.IsOpen)&&summarySeen.Add(game.State.levelIndex))ShowCompletionReminder();
  }
  void ShowCompletionReminder()
  {
   string text=game.State.levelIndex==5&&!game.State.chapterOneDelivered&&game.State.glassFish<2
    ?"岛上的伙伴已经安居了！还要带回两条琉璃鱼。靠近小鱼，点击提示或按 E 开始钓鱼；钓到第一条后，按空格继续生态，等待下一条幼鱼。"
    :game.State.levelIndex==7&&game.State.shells==0&&game.State.deliveredShells==0
    ?"寄居蟹已经找到合适的家。别忘了捡起它脱下的壳，带回商店交付；其他渔获也可以一起带回来。"
    :game.State.levelIndex==10?"最后一座岛的伙伴们也安居了！收好渔获，回码头找我聊聊这段旅程吧。"
    :"岛上的伙伴已经安居了！收好渔获，回码头找我交谈，准备下一段旅程。";
   ShowChapterDialogue("商店老板",new[]{text},"知道了",()=>{});
  }
 }
}
