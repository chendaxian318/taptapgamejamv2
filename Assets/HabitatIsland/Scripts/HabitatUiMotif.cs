using UnityEngine;
using UnityEngine.UI;
namespace HabitatIsland
{
    // Resolution-independent UI decoration; no screenshots or editor guides are drawn over the game.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HabitatUiMotif : MaskableGraphic
    {
        public bool circle;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Rect r=rectTransform.rect;int count=circle?32:3;
            vh.AddVert(new Vector3(r.center.x,r.center.y),color,Vector2.zero);
            for(int i=0;i<=count;i++)
            {
                float a=i*2*Mathf.PI/count;
                vh.AddVert(new Vector3(r.center.x+Mathf.Cos(a)*r.width*.5f,r.center.y+Mathf.Sin(a)*r.height*.5f),color,Vector2.zero);
                if(i>0)vh.AddTriangle(0,i,i+1);
            }
        }
        public static RectTransform Add(Transform parent,Vector2 position,Vector2 size,Color color,bool circle=false)
        {
            var r=new GameObject(circle?"Sea bubble":"Coral arrow",typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=size;
            var graphic=r.gameObject.AddComponent<HabitatUiMotif>();graphic.circle=circle;graphic.color=color;graphic.raycastTarget=false;return r;
        }
    }
}
