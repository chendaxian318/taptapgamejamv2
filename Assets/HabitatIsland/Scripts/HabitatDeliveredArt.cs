using UnityEngine;
using UnityEngine.UI;
namespace HabitatIsland
{
    public static class HabitatDeliveredArt
    {
        // Regions are measured on the 2048 x 1152 preview of the 8000 x 4500 export.
        // UV slicing keeps the supplied RGBA artwork intact, including its white lettering.
        public static RawImage Add(Transform parent,string name,string asset,Rect region,Vector2 position,Vector2 size)
        {
            var r=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage)).GetComponent<RectTransform>();
            r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=size;
            var image=r.GetComponent<RawImage>();image.texture=Resources.Load<Texture2D>("UI/Delivered/"+asset);image.raycastTarget=false;
            image.uvRect=new Rect(region.x/2048f,1-(region.y+region.height)/1152f,region.width/2048f,region.height/1152f);
            return image;
        }
    }
    public sealed class HabitatUiBackdrop : MonoBehaviour
    {
        RawImage image;RectTransform rect;Canvas canvas;
        public void Initialize(Texture texture)
        {
            image=gameObject.AddComponent<RawImage>();image.texture=texture;image.raycastTarget=false;
            rect=(RectTransform)transform;canvas=GetComponentInParent<Canvas>();
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;
            LateUpdate();
        }
        void LateUpdate()
        {
            if(!image||!image.texture||!canvas)return;
            rect.sizeDelta=((RectTransform)canvas.transform).rect.size;
            float screen=rect.sizeDelta.x/Mathf.Max(1,rect.sizeDelta.y),source=(float)image.texture.width/image.texture.height;
            image.uvRect=source>screen?new Rect((1-screen/source)*.5f,0,screen/source,1):new Rect(0,(1-source/screen)*.5f,1,source/screen);
        }
    }
}
