using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace HabitatIsland
{
    public static class HabitatUiAssets
    {
        static TMP_FontAsset body,bold,latin;static Material cutout;
        static TMP_FontAsset Make(string source)
        {
            var f=Resources.Load<Font>("Fonts/Delivered/"+source);if(!f)return Resources.Load<TMP_FontAsset>("Fonts/HabitatChinese");
            var a=TMP_FontAsset.CreateFontAsset(f,64,5,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
            a.name=source+" Runtime SDF";a.getFontFeatures=false;return a;
        }
        public static TMP_FontAsset Body=>body?body:(body=Make("SourceHanSansCN-Regular"));
        public static TMP_FontAsset Bold=>bold?bold:(bold=Make("SourceHanSansCN-Bold"));
        public static TMP_FontAsset Latin
        {get{if(!latin){latin=Make("Outfit-Bold");latin.fallbackFontAssetTable??=new System.Collections.Generic.List<TMP_FontAsset>();latin.fallbackFontAssetTable.Add(Body);}return latin;}}
        // Tomorrow's transparent parts override only the corresponding resource name.
        public static RawImage Part(Transform parent,string key,int page,Rect pixelRegion,Vector2 position,Vector2 size)
        {
            var r=new GameObject(key,typeof(RectTransform),typeof(RawImage)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=size;
            var art=r.GetComponent<RawImage>();art.raycastTarget=false;
            art.texture=Resources.Load<Texture2D>("UI/Delivered/Batch2/Parts/"+key);
            if(!art.texture)
            {
                art.texture=Resources.Load<Texture2D>("UI/Delivered/Batch2/"+page);
                if(art.texture)art.uvRect=new Rect(pixelRegion.x/art.texture.width,1-(pixelRegion.y+pixelRegion.height)/art.texture.height,pixelRegion.width/art.texture.width,pixelRegion.height/art.texture.height);
                if(!cutout){var shader=Resources.Load<Shader>("UI/Delivered/Batch2/DraftCutout");if(shader)cutout=new Material(shader);}
                art.material=cutout;
            }
            return art;
        }
        public static RawImage Icon(Transform parent,string key,Vector2 position,Vector2 size)
        {
            var art=Part(parent,"Icon_"+key,0,new Rect(0,0,1,1),position,size);
            art.texture=IconTexture(key);
            art.uvRect=new Rect(0,0,1,1);art.material=null;art.enabled=art.texture;return art;
        }
        public static Texture2D IconTexture(string key)=>Resources.Load<Texture2D>("UI/Delivered/Batch2/Parts/Icon_"+key)??Resources.Load<Texture2D>("UI/Delivered/Batch2/Icons/"+key);
    }
}
