using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace HabitatIsland
{
 public partial class HabitatHud
 {
  static readonly string[] FinalBuyCards={"R93","R103","R92","R90","R91",null,null,null,null,"R97","R95"};
  static readonly string[] FinalSellCards={"R98","R99","R125","R101"};
  RawImage[] finalShopCards;RawImage finalQuantityArt;
  void HideNamed(Transform root,params string[] names){foreach(var name in names){var t=root.Find(name);if(t)t.gameObject.SetActive(false);}}
  void ApplyFinalUiDelivery()
  {
   foreach(var entry in new[]{audioPanel,summaryPanel})foreach(var b in entry.GetComponentsInChildren<Button>())
    if(b.name=="Confirm island summary"||b.name=="Back from Audio Settings")HabitatFinalUi.ButtonArt(b,"R110");
   HabitatFinalUi.Art(toastPanel.transform,"R20",Vector2.zero,new Vector2(720,44)).transform.SetAsFirstSibling();toastPanel.GetComponent<Image>().color=Color.clear;
   var money=HabitatFinalUi.Art(shopPanel.transform,"R88",new Vector2(1000,-18),new Vector2(240,57));
   Place(shopBalance,1057,-27,133,40);shopBalance.color=Color.white;shopBalance.fontSize=20;shopBalance.enableAutoSizing=true;shopBalance.fontSizeMin=15;shopBalance.fontSizeMax=20;
   shopBalance.transform.SetAsLastSibling();
   foreach(var b in shopPanel.GetComponentsInChildren<Button>())if(b.name=="Back from shop"){Place(b,1260,-16,145,54);HabitatFinalUi.ButtonArt(b,"R89");}
   foreach(var panel in new[]{shopPanel,inventoryPanel,bookPanel})foreach(var b in panel.GetComponentsInChildren<Button>())if(b.name.StartsWith("Close "))b.GetComponentInChildren<TMP_Text>().color=deepSea;
   finalShopCards=new RawImage[shopEntries.Count];
   for(int i=0;i<shopEntries.Count;i++)
   {
    HideNamed(shopPanel.transform,"Shop stock slot "+i,"Stock inset "+i,"Stock notch "+i);
    var art=HabitatFinalUi.Art(shopEntries[i].transform,"R93",new Vector2(0,-20),new Vector2(143,145));
    // Exclude baked sample prices. Render the live rule price beneath each card.
    art.uvRect=new UnityEngine.Rect(0,.25f,1,.75f);art.transform.SetAsFirstSibling();finalShopCards[i]=art;
    Place(shopTitles[i],0,-1,143,27);shopTitles[i].fontSize=16;
    Place(shopPrices[i],-10,-167,163,44);
   }
   for(int i=shopEntries.Count;i<12;i++)HideNamed(shopPanel.transform,"Shop stock slot "+i,"Stock inset "+i,"Stock notch "+i);
   foreach(var label in audioPanel.GetComponentsInChildren<TMP_Text>())if(new[]{"声音","亮度","0","100"}.Contains(label.text))label.enabled=false;
   HabitatFinalUi.Art(audioPanel.transform,"R116",new Vector2(130,-150),new Vector2(340,88));
   HabitatFinalUi.Art(audioPanel.transform,"R115",new Vector2(130,-290),new Vector2(340,88));
   foreach(var slider in new[]{ambienceSlider,brightnessSlider}){HideNamed(slider.transform,"White scale");foreach(Transform t in slider.handleRect)t.gameObject.SetActive(false);slider.targetGraphic=HabitatFinalUi.Art(slider.handleRect,"R113",Vector2.zero,new Vector2(26,29),true);slider.transform.SetAsLastSibling();}
   foreach(var raw in summaryPanel.GetComponentsInChildren<RawImage>())if(raw.name.StartsWith("Icon_"))raw.gameObject.SetActive(false);
   var fishDecoration=HabitatFinalUi.Art(summaryPanel.transform,"R111",new Vector2(150,-150),new Vector2(310,100));fishDecoration.uvRect=new UnityEngine.Rect(0,.43f,1,.57f);
   HideNamed(guidePanel.transform,"Guide pager rail");
   foreach(var b in guidePanel.GetComponentsInChildren<Button>()){var t=b.GetComponentInChildren<TMP_Text>();if(t&&(t.text=="«"||t.text=="»")){Place(b,t.text=="«"?398:715,-674,65,50);t.enabled=false;}}

   HabitatFinalUi.Art(guidePanel.transform,"R119",new Vector2(340,-681),new Vector2(470,38)).transform.SetAsFirstSibling();
   // Layout artwork 68 contains production notes, so retain a clean panel and use the delivered creature tiles.
   string[] bookShapes={"Main creature tile","Upper creature tile","Small tile","Upper capsule","Dark square","Tall capsule","Wide capsule","Bottom capsule"};
   HideNamed(bookPanel.transform,bookShapes);
   foreach(var raw in bookPanel.GetComponentsInChildren<RawImage>())if(raw!=bookImage&&raw.name!="Final UI Background05")raw.gameObject.SetActive(false);
   bookImage.gameObject.SetActive(false);
   string[] bookArts={"R57","R65","R58","R60"};
   for(int i=0;i<4;i++)
   {
    int index=i;var button=TextButton(bookPanel.transform,"",110+i%2*420,-210-i/2*235,350,()=>{bookIndex=index;RefreshBook();});
    button.name="Final book species "+i;Place(button,110+i%2*420,-210-i/2*235,350,215);
    HabitatFinalUi.Art(button.transform,bookArts[i],Vector2.zero,new Vector2(350,215),true);
   }
   Shape(bookPanel.transform,"Book reading paper",1030,-108,370,623,new Color(1,1,1,.94f),12);
   bookName.transform.SetAsLastSibling();bookDetails.transform.SetAsLastSibling();bookCounts.transform.SetAsLastSibling();
   Text(bookPanel.transform,"生物图鉴",36,blue,new Vector2(340,-75),new Vector2(360,70),TextAlignmentOptions.Center,true);
   foreach(var motif in bookPanel.GetComponentsInChildren<HabitatUiMotif>())motif.gameObject.SetActive(false);
   // Quantity sheet provides artwork while transparent hit targets retain the existing transaction path.
   HideNamed(purchasePanel.transform,"Quantity bubble"); foreach(var motif in purchasePanel.GetComponentsInChildren<HabitatUiMotif>())motif.gameObject.SetActive(false);
   finalQuantityArt=HabitatFinalUi.Art(purchasePanel.transform,"R104",Vector2.zero,new Vector2(1000,297));finalQuantityArt.transform.SetAsFirstSibling();
   // The original sheet already includes its title, minus/plus and confirm/cancel lettering.
   purchaseHeading.gameObject.SetActive(false);
   foreach(var b in purchasePanel.GetComponentsInChildren<Button>())
   {
    var t=b.GetComponentInChildren<TMP_Text>();if(!t)continue;
    if(t.text=="−"){Place(b,548,-124,52,55);t.enabled=false;b.GetComponent<Image>().color=Color.clear;}
    else if(t.text=="+"){Place(b,400,-124,52,55);t.enabled=false;b.GetComponent<Image>().color=Color.clear;}
    else if(t.text=="确认"||t.text=="放弃"){Place(b,t.text=="确认"?340:535,-217,125,55);t.enabled=false;b.GetComponent<Image>().color=Color.clear;}
   }
   Place(purchaseDetails,90,-70,820,48);Place(purchaseCount,449,-126,100,48);
  }
  void RefreshFinalShopCard(int i)
  {
   if(finalShopCards==null||i>=finalShopCards.Length)return;
   string key=shopSelling?(i<FinalSellCards.Length?FinalSellCards[i]:null):FinalBuyCards[i];
   var art=finalShopCards[i];art.texture=key==null?null:HabitatFinalUi.Texture(key);art.enabled=art.texture;
   art.uvRect=key=="R125"?new UnityEngine.Rect(0,0,1,1):new UnityEngine.Rect(0,.25f,1,.75f);art.color=shopEntries[i].interactable?Color.white:new Color(.67f,.72f,.7f,.85f);
   if(art.enabled){shopImages[i].enabled=false;shopFallbacks[i].SetActive(false);}
  }
 }
}

