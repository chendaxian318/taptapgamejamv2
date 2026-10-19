using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace HabitatIsland
{
 public static class HabitatFinalUi
 {
  public static Texture2D Texture(string key)=>Resources.Load<Texture2D>("UI/Final20261018/"+key);
  public static Rect Region(string key)
  {
   if(key=="R80"||key=="R81"||key=="R82"||key=="R83")return new Rect(.09f,.49f,.82f,.5f);
   if(key=="Hover80"||key=="Hover81"||key=="Hover82"||key=="Hover83")return new Rect(.055f,.39f,.945f,.61f);
   if(key=="R110"||key=="R117")return new Rect(0,.31f,1,.69f);
   return new Rect(0,0,1,1);
  }
  public static RawImage Art(Transform parent,string key,Vector2 position,Vector2 size,bool fit=false)
  {
   var r=new GameObject("Final UI "+key,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
   r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=size;
   var image=r.gameObject.AddComponent<RawImage>();image.texture=Texture(key);image.uvRect=Region(key);image.raycastTarget=false;
   if(fit&&image.texture){var pixels=new Vector2(image.texture.width*image.uvRect.width,image.texture.height*image.uvRect.height);float scale=Mathf.Min(size.x/pixels.x,size.y/pixels.y);r.sizeDelta=pixels*scale;r.anchoredPosition+=new Vector2((size.x-r.sizeDelta.x)*.5f,-(size.y-r.sizeDelta.y)*.5f);}
   return image;
  }
  public static void ButtonArt(Button button,string normal,string hover=null,float visualScale=1f)
  {
   foreach(Transform child in button.transform){var label=child.GetComponent<TMPro.TMP_Text>();if(label)label.enabled=false;else child.gameObject.SetActive(false);}
   var size=button.GetComponent<RectTransform>().sizeDelta;
   var art=Art(button.transform,normal,Vector2.zero,size,true);
   button.GetComponent<Image>().color=Color.clear;button.targetGraphic=art;
   if(hover!=null){var state=button.gameObject.AddComponent<HabitatFinalUiHover>();state.Initialize(art,normal,hover,size,visualScale);}
  }
 }
 public sealed class HabitatFinalUiHover:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
 {
  RawImage art;Texture2D normal,hover;Vector2 size;float visualScale=1f;bool over,selected;string normalKey,hoverKey;
  public void Initialize(RawImage image,string a,string b,Vector2 bounds,float scale=1f){art=image;normalKey=a;hoverKey=b;normal=HabitatFinalUi.Texture(a);hover=HabitatFinalUi.Texture(b);size=bounds;visualScale=scale;Apply();}
  void Apply(){if(!art)return;bool active=(over||selected)&&GetComponent<Button>().IsInteractable();art.texture=active&&hover?hover:normal;art.uvRect=HabitatFinalUi.Region(active?hoverKey:normalKey);if(!art.texture)return;float height=size.y*(active?1:.82f)*visualScale;float width=height*art.texture.width*art.uvRect.width/(art.texture.height*art.uvRect.height);art.rectTransform.sizeDelta=new Vector2(width,height);art.rectTransform.anchoredPosition=new Vector2(size.x-width,-(size.y-height)*.5f);}
  public void OnPointerEnter(PointerEventData e){over=true;Apply();}public void OnPointerExit(PointerEventData e){over=false;Apply();}
  public void OnSelect(BaseEventData e){selected=true;Apply();}public void OnDeselect(BaseEventData e){selected=false;Apply();}
  void OnDisable(){over=selected=false;Apply();}
 }
}

