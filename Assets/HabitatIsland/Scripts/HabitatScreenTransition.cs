using UnityEngine;
using UnityEngine.UI;

namespace HabitatIsland
{
    // Screen-space cover: salvage is committed only while this is fully opaque.
    public sealed class HabitatScreenTransition : MonoBehaviour
    {
        CanvasGroup group;
        public float Opacity => group ? group.alpha : 0f;
        public void SetOpacity(float alpha)
        {
            if (!group)
            {
                var root=new GameObject("Recovery blackout",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup),typeof(GraphicRaycaster));
                root.transform.SetParent(transform,false);
                var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder=30000;
                group=root.GetComponent<CanvasGroup>();
                var cover=new GameObject("Black",typeof(RectTransform),typeof(Image));
                cover.transform.SetParent(root.transform,false);
                var rect=cover.GetComponent<RectTransform>(); rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
                rect.offsetMin=rect.offsetMax=Vector2.zero;
                var image=cover.GetComponent<Image>(); image.color=Color.black; image.raycastTarget=true;
            }
            group.alpha=Mathf.Clamp01(alpha); group.blocksRaycasts=group.alpha>0f; group.interactable=false;
        }
    }
}
