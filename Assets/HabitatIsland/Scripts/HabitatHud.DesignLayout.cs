using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace HabitatIsland
{
 public partial class HabitatHud
 {
  int inventoryToolPage;RawImage equippedIcon;TMP_Text[] catchCounts=new TMP_Text[5];
  Material bookWhiteMaterial;Material BookWhiteMaterial=>bookWhiteMaterial?bookWhiteMaterial:(bookWhiteMaterial=new Material(Resources.Load<Shader>("UI/Delivered/Batch2/DraftPortrait")));
  readonly Color aqua=new Color32(105,209,181,255),blue=new Color32(43,158,180,255),turquoise=new Color32(55,185,184,255),red=new Color32(234,97,101,255);
  RectTransform Shape(Transform parent,string name,float x,float y,float w,float h,Color color,float radius=24)
  {var r=Rect(name,parent,new Vector2(0,1),new Vector2(0,1),new Vector2(x,y),new Vector2(w,h));var g=r.gameObject.AddComponent<HabitatUiSurface>();g.color=color;g.Radius=radius;g.raycastTarget=false;return r;}
  void Bubble(Transform parent,float x,float y,float size,Color color)=>HabitatUiMotif.Add(parent,new Vector2(x,y),new Vector2(size,size),color,true);
  void Accent(Transform parent,float x,float y,float scale=1)
  {
   for(int i=0;i<2;i++){var r=HabitatUiMotif.Add(parent,new Vector2(x+i*8*scale,y-i*31*scale),new Vector2(42*scale,40*scale),aqua);r.localRotation=Quaternion.Euler(0,0,90);var inner=HabitatUiMotif.Add(r,new Vector2(8,-8),new Vector2(25*scale,23*scale),turquoise);inner.localRotation=Quaternion.identity;}
  }
  void Ribbon(Transform parent,string key,string caption,float x,float y,float width,Color color)
  {
   string finalKey=key=="BagEquipment"?"R130":key=="BagCreatures"?"R131":key=="BagItems"?"R132":key=="ShopBuy"?"R85":key=="ShopSold"?"R86":key=="GuideVideo"?"R120":null;
   if(finalKey!=null){HabitatFinalUi.Art(parent,finalKey,new Vector2(x,y+27),new Vector2(width,key=="GuideVideo"?58:width*HabitatFinalUi.Texture(finalKey).height/HabitatFinalUi.Texture(finalKey).width));return;}
   var tex=Resources.Load<Texture2D>("UI/Delivered/Batch2/Parts/"+key);
   if(tex){HabitatUiAssets.Part(parent,key,0,default,new Vector2(x,y),new Vector2(width,52));return;}
   Shape(parent,key+" tape",x,y,width,43,color,18);
   var label=Text(parent,caption,32,Color.white,new Vector2(x+4,y-1),new Vector2(width-54,52),TextAlignmentOptions.Left,true);label.font=HabitatUiAssets.Latin;
   for(int i=0;i<3;i++)Bubble(parent,x+width-65+i*13,y-18,8,blue);
   Accent(parent,x+width-19,y-12,.62f);
  }
  void Heading(Transform p,string key,string text,float x,float y,float width)
  {
   var tex=Resources.Load<Texture2D>("UI/Delivered/Batch2/Parts/"+key);
   if(tex)HabitatUiAssets.Part(p,key,0,default,new Vector2(x,y),new Vector2(width,65));
   else{var t=Text(p,text,36,Color.white,new Vector2(x,y),new Vector2(width,60),TextAlignmentOptions.Center,true);t.outlineColor=blue;t.outlineWidth=.17f;Text(p,"»",42,aqua,new Vector2(x-52,y-2),new Vector2(50,60));Text(p,"«",42,aqua,new Vector2(x+width+2,y-2),new Vector2(50,60));}
   for(int i=0;i<3;i++)Bubble(p,x+width*.5f-22+i*18,y-63,9,new Color32(255,160,124,255));
  }
  RectTransform BubblePanel(GameObject panel,string title,string key,float height)
  {
   var p=Skin(panel,new Vector2(600,height),"");
   string finalKey=key=="PauseHeading"?"R84":key=="SettingsHeading"?"R118":key=="SummaryHeading"?"R112":null;
   if(finalKey!=null&&HabitatFinalUi.Texture(finalKey)){HabitatFinalUi.Art(p,finalKey,Vector2.zero,new Vector2(600,height),true);return p;}
   Shape(p,"Rounded bubble body",110,-55,380,height-190,blue,68);
   Bubble(p,33,-height+315,170,blue);Bubble(p,83,-height+256,78,turquoise);
   Bubble(p,180,-height+160,162,blue);Bubble(p,318,-height+206,170,blue);Bubble(p,385,-height+146,112,turquoise);
   Bubble(p,469,-height+258,65,blue);Bubble(p,458,-height+79,76,blue);
   Accent(p,102,-155,.85f);Accent(p,477,-310,.8f);
   Heading(p,key,title,165,-22,270);
   return p;
  }
  Button TextButton(Transform p,string text,float x,float y,float w,Action action,int size=29)
  {
   var b=SeaButton(p,text,new Vector2(x,y),new Vector2(w,54),action);b.GetComponent<Image>().color=Color.clear;var label=b.GetComponentInChildren<TMP_Text>();label.color=Color.white;label.fontSize=size;label.font=HabitatUiAssets.Bold;return b;
  }
  void InventoryRow(Transform p,string name,int count,float y,Color border,Color fill)
  {
   for(int i=0;i<count;i++){float x=493+i*173;Shape(p,name+" slot "+i,x,y,128,153,border,22);Shape(p,name+" inset "+i,x+13,y-46,102,94,fill,14);Shape(p,name+" notch "+i,x+40,y-12,53,7,aqua,4);Bubble(p,x+107,y+5,35,blue);}
  }
 }
}
