using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace HabitatIsland
{
    public sealed class HabitatMenuEntrance : MonoBehaviour
    {
        readonly List<CanvasGroup> groups=new List<CanvasGroup>();
        RectTransform logo;Vector2 finalPosition,finalSize;CanvasGroup ribbon;
        Texture2D cover;RawImage backdrop;RenderTexture blurred,horizontal;Material blur;
        bool configured,seen;float elapsed;int finishedFrame;
        const float Duration=2.8f;
        static string launchCover;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLaunchCover(){launchCover=null;}
        public bool IsPlaying=>configured&&elapsed<Duration;
        public bool HasCover=>cover;
        public void Configure(RectTransform layout,RectTransform mark,CanvasGroup openingRibbon)
        {
            logo=mark;finalPosition=logo.anchoredPosition;finalSize=logo.sizeDelta;ribbon=openingRibbon;
            foreach(Transform child in layout)
                if(child!=logo&&child!=openingRibbon.transform)groups.Add(child.gameObject.AddComponent<CanvasGroup>());
            // Pick once per application launch, keeping settings/return navigation stable.
            if(launchCover==null)launchCover=new System.Random().Next(2)==0?"Daylight":"Warm";
            cover=Resources.Load<Texture2D>("UI/Covers20261019/"+launchCover);
            if(!cover)cover=Resources.Load<Texture2D>("UI/Delivered/MainCover");
            if(cover)
            {
                var r=new GameObject("Main art cover",typeof(RectTransform));r.transform.SetParent(transform,false);r.transform.SetAsFirstSibling();
                r.AddComponent<HabitatUiBackdrop>().Initialize(cover);backdrop=r.GetComponent<RawImage>();
                var shader=Resources.Load<Shader>("UI/Delivered/CoverBlur");
                if(shader&&shader.isSupported)
                {
                    blur=new Material(shader);int w=768,h=Mathf.Max(1,Mathf.RoundToInt(w*(float)cover.height/cover.width));
                    horizontal=new RenderTexture(w,h,0){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                    blurred=new RenderTexture(w,h,0){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};horizontal.Create();blurred.Create();
                }
            }
            configured=true;elapsed=Duration;
        }
        void OnEnable(){if(!configured)return;if(!seen){seen=true;elapsed=0;}else elapsed=Duration;Apply();}
        public void Skip(){if(!IsPlaying)return;elapsed=Duration;finishedFrame=Time.frameCount;Apply();}
        void Update()
        {
            if(!configured)return;
            if(IsPlaying)
            {
                var key=Keyboard.current;var mouse=Mouse.current;
                if((key!=null&&(key.spaceKey.wasPressedThisFrame||key.enterKey.wasPressedThisFrame||key.escapeKey.wasPressedThisFrame))||(mouse!=null&&mouse.leftButton.wasPressedThisFrame))Skip();
                else{elapsed=Mathf.Min(Duration,elapsed+Time.unscaledDeltaTime);if(!IsPlaying)finishedFrame=Time.frameCount;Apply();}
            }
            else if(Time.frameCount>finishedFrame+1&&(Mouse.current==null||!Mouse.current.leftButton.isPressed))
                foreach(var group in groups){group.interactable=true;group.blocksRaycasts=true;}
        }
        void Apply()
        {
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-.7f)/(Duration-.7f)));
            logo.anchoredPosition=Vector2.Lerp(new Vector2(400,-200),finalPosition,t);
            logo.sizeDelta=Vector2.Lerp(new Vector2(680,388),finalSize,t);
            logo.localScale=Vector3.one*(1+.012f*Mathf.Sin(elapsed*3)*(1-t));
            ribbon.alpha=1-Mathf.Clamp01(t*2);ribbon.blocksRaycasts=ribbon.interactable=false;
            for(int i=0;i<groups.Count;i++)
            {
                groups[i].alpha=IsPlaying?Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.25f-Mathf.Min(i,4)*.025f)/.5f)):1;
                groups[i].interactable=false;groups[i].blocksRaycasts=false;
            }
            if(backdrop&&blur)
            {
                float radius=Mathf.Lerp(7,0,t);blur.SetVector("_Direction",new Vector4(radius/horizontal.width,0,0,0));Graphics.Blit(cover,horizontal,blur);
                blur.SetVector("_Direction",new Vector4(0,radius/horizontal.height,0,0));Graphics.Blit(horizontal,blurred,blur);backdrop.texture=IsPlaying?blurred:cover;
            }
        }
        void OnDisable(){if(configured){elapsed=Duration;Apply();}}
        void OnDestroy(){if(blur)Destroy(blur);if(horizontal){horizontal.Release();Destroy(horizontal);}if(blurred){blurred.Release();Destroy(blurred);}}
    }
}
