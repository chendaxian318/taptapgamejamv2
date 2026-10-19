using UnityEngine;using UnityEngine.UI;
namespace HabitatIsland
{
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class HabitatUiRing:MaskableGraphic
 {
  float value;public float Value{get=>value;set{this.value=Mathf.Clamp01(value);SetVerticesDirty();}}
  protected override void OnPopulateMesh(VertexHelper vh)
  {
   vh.Clear();const int steps=64;float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.5f;var center=rectTransform.rect.center;
   for(int i=0;i<steps;i++)
   {float a=Mathf.PI*.5f-i*Mathf.PI*2/steps,b=Mathf.PI*.5f-(i+1)*Mathf.PI*2/steps;Color c=i/(float)steps<Value?color:new Color(.2f,.5f,.6f,.24f);int n=vh.currentVertCount;
    vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,c,Vector2.zero);vh.AddVert(center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,c,Vector2.zero);
    vh.AddVert(center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*(radius-12),c,Vector2.zero);vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(radius-12),c,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
  }
 }
}
