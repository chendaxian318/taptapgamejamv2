using UnityEngine;
using UnityEngine.UI;
namespace HabitatIsland
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HabitatUiSurface : MaskableGraphic
    {
        public float Radius=24;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float radius=Mathf.Min(Radius,Mathf.Min(r.width,r.height)*.5f);
            vh.AddVert(r.center,color,Vector2.zero);
            for(int corner=0;corner<4;corner++)for(int step=0;step<=12;step++)
            {
                float angle=(corner*90+step*7.5f)*Mathf.Deg2Rad;
                Vector2 center=new Vector2(corner==0||corner==3?r.xMax-radius:r.xMin+radius,corner<2?r.yMax-radius:r.yMin+radius);
                vh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,color,Vector2.zero);
                int index=corner*13+step+1;if(index>1)vh.AddTriangle(0,index-1,index);
            }
            vh.AddTriangle(0,52,1);
        }
    }
}
