using UnityEngine;using UnityEngine.UI;
namespace HabitatIsland
{
 public sealed class HabitatBrightness : MonoBehaviour
 {
  static HabitatBrightness instance;Image overlay;
  public static float Value=>Mathf.Clamp01(PlayerPrefs.GetFloat("Habitat.DisplayBrightness",.5f));
  public static void Ensure()
  {if(instance)return;instance=new GameObject("Display brightness",typeof(RectTransform),typeof(Canvas),typeof(HabitatBrightness)).GetComponent<HabitatBrightness>();instance.Build();}
  void Build()
  {
   var canvas=GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=-10;
   var child=new GameObject("Scene brightness tint",typeof(RectTransform),typeof(Image));child.transform.SetParent(transform,false);
   overlay=child.GetComponent<Image>();overlay.raycastTarget=false;var r=overlay.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;Apply(Value);
  }
  void Apply(float v){overlay.color=v<.5f?new Color(0,0,0,(.5f-v)*.7f):new Color(1,1,1,(v-.5f)*.36f);}
  public static void Set(float v){Ensure();v=Mathf.Clamp01(v);PlayerPrefs.SetFloat("Habitat.DisplayBrightness",v);PlayerPrefs.Save();instance.Apply(v);}
 }
}
