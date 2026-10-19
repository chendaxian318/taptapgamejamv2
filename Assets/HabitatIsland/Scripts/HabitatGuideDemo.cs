using UnityEngine;using UnityEngine.UI;
namespace HabitatIsland
{
 public sealed class HabitatGuideDemo : MonoBehaviour
 {
  public int Mode;RectTransform hero,crate,fish,zone;RectTransform[] steps=new RectTransform[3];
  RectTransform Box(string name,Vector2 pos,Vector2 size,Color color)
  {var r=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();r.SetParent(transform,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=pos;r.sizeDelta=size;var image=r.GetComponent<Image>();image.color=color;image.sprite=HabitatHud.RoundedSprite;image.type=Image.Type.Sliced;image.raycastTarget=false;return r;}
  public void Build()
  {
   for(int i=0;i<6;i++)Box("Demo grid "+i,new Vector2(95+i*120,-234),new Vector2(110,12),new Color32(111,215,187,255));
   var portrait=HabitatUiAssets.Icon(transform,"Hero",new Vector2(140,-100),new Vector2(110,135));hero=portrait.rectTransform;
   if(!portrait.texture){portrait.texture=Resources.Load<Texture2D>("Art/Artist/HeroFront");portrait.material=new Material(Resources.Load<Shader>("UI/Delivered/Batch2/DraftPortrait"));portrait.material.SetFloat("_ReferenceNotes",1);}
   crate=Box("Demo crate",new Vector2(270,-170),new Vector2(66,64),new Color32(187,139,101,255));
   for(int i=0;i<3;i++)steps[i]=Box("Demo step "+i,new Vector2(350+i*110,-234+i*48),new Vector2(100,48+i*48),new Color32(111,215,187,255));
   zone=Box("Demo catch zone",new Vector2(250,-125),new Vector2(180,80),new Color32(111,215,187,255));fish=HabitatUiAssets.Icon(transform,"Goldfish",new Vector2(300,-130),new Vector2(90,90)).rectTransform;
  }
  void Update()
  {
   if(!hero)return;float t=Mathf.PingPong(Time.unscaledTime*.4f,1);
   hero.gameObject.SetActive(Mode>=0&&Mode<3);crate.gameObject.SetActive(Mode>=0&&Mode<2);zone.gameObject.SetActive(Mode==3);fish.gameObject.SetActive(Mode==3);
   for(int i=0;i<6;i++)transform.Find("Demo grid "+i).gameObject.SetActive(Mode>=0);
   foreach(var s in steps)s.gameObject.SetActive(Mode==2);
   if(Mode==0){hero.anchoredPosition=new Vector2(140+t*360,-100);crate.anchoredPosition=new Vector2(250+t*360,-170);}
   if(Mode==1){hero.anchoredPosition=new Vector2(520-t*320,-100);crate.anchoredPosition=new Vector2(630-t*320,-170);}
   if(Mode==2)hero.anchoredPosition=new Vector2(230+t*420,-100+Mathf.Clamp01((t-.15f)/.65f)*96);
   if(Mode==3){fish.anchoredPosition=new Vector2(240+t*380,-130);zone.anchoredPosition=new Vector2(180+t*380,-125);}
  }
 }
}
