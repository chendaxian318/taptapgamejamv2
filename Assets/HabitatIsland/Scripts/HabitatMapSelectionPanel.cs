using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HabitatIsland
{
    /// <summary>A destination picker shared by both harbors. Selecting a card never starts travel.</summary>
    public sealed class HabitatMapSelectionPanel : MonoBehaviour
    {
        readonly Color ink = new Color32(23, 61, 58, 255);
        readonly Color paper = new Color32(248, 250, 247, 255);
        readonly Color muted = new Color32(97, 117, 111, 255);
        readonly Color mint = new Color32(36, 161, 184, 255);
        readonly Color surface = new Color32(232, 239, 235, 255);
        TMP_FontAsset font;
        TMP_Text destination, description, dimensions;
        Image[] rows;
        TMP_Text[] rowLabels;
        RawImage preview,finalTitle;RawImage[] finalCards;
        static int CardArt(int index){switch(index){case 5:return 40;case 6:return 37;case 7:return 38;case 8:return 43;case 9:return 41;case 10:return 39;default:return 0;}}
        static int TitleArt(int index){switch(index){case 5:return 36;case 6:return 47;case 7:return 48;case 8:return 49;case 9:return 50;case 10:return 51;default:return 0;}}
        Texture2D previewTexture;
        Action<int> onConfirm;
        Func<int,bool> campaignAllowed;
        Func<int,string> campaignReason;
        Button confirmButton;
        TMP_Text previewPlaceholder,pageLabel;
        int mapPage;
        readonly int[] destinations = HabitatLevels.CampaignDestinations;
        public int SelectedLevel { get; private set; }
        public bool SelectedDestinationAllowed => campaignAllowed==null||campaignAllowed(SelectedLevel);

        public static HabitatMapSelectionPanel Create(Transform parent, TMP_FontAsset font, Action<int> confirm, Action close)
        {
            var root = NewRect("Map selection", parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1440, 810));
            var panel = root.gameObject.AddComponent<HabitatMapSelectionPanel>();
            panel.font = font; panel.onConfirm = confirm;
            panel.Build(close);
            root.gameObject.SetActive(false);
            return panel;
        }

        GameObject[] completionBadges,lockedBadges,selectionMarks;
        Func<int,bool> completed;
        public void ConfigureCompletion(Func<int,bool> isComplete) { completed=isComplete; }
        static string ArtName(int index)
        {
            switch(index){case 5:return "钓钓小鱼";case 6:return "吹吹小虾";case 7:return "赶赶小蟹";case 8:return "抓抓水母";case 4:return "荒岛 · 连续关卡";case 9:return "群山之巅";case 10:return "得不到的箱箱";default:return "生态练习 · 可选";}
        }
        void Build(Action close)
        {
            Fill((RectTransform)transform,new Color32(236,247,244,255),true);
            var photo=HabitatFinalUi.Texture("Background03");
            if(photo)
            {
                var background=NewRect("Delivered beach background",transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
                background.gameObject.AddComponent<HabitatUiBackdrop>().Initialize(photo);
                var wash=NewRect("Map photo wash",background,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
                wash.anchorMin=Vector2.zero;wash.anchorMax=Vector2.one;wash.offsetMin=wash.offsetMax=Vector2.zero;Fill(wash,new Color(1,1,1,0),false);
            }
            // Page 4: large mint header, centered coral departure ribbon and two rows of floating labels.
            var header=NewRect("Sea route header",transform,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(1440,244));
            HabitatFinalUi.Art(header,"R33",Vector2.zero,new Vector2(1440,244));
            finalTitle=HabitatFinalUi.Art(header,"R36",new Vector2(38,-35),new Vector2(680,126),true);
            destination=Label(header,"",61,new Color32(25,25,24,255),new Vector2(38,-51),new Vector2(700,90),true);
            description=Label(header,"",18,ink,new Vector2(42,-174),new Vector2(495,65));
            dimensions=Label(header,"",14,muted,Vector2.zero,Vector2.zero);dimensions.gameObject.SetActive(false);
            var closeButton=Button(transform,"×",new Vector2(1370,-18),new Vector2(48,48),close,false,HabitatAudioCue.Close);closeButton.GetComponent<Image>().color=Color.clear;
            finalCards=new RawImage[destinations.Length];rows=new Image[destinations.Length];rowLabels=new TMP_Text[destinations.Length];completionBadges=new GameObject[destinations.Length];lockedBadges=new GameObject[destinations.Length];selectionMarks=new GameObject[destinations.Length];
            for(int i=0;i<destinations.Length;i++)
            {
                int index=destinations[i];bool tutorial=index==0;int cell=i%6;
                Vector2 pos=tutorial?new Vector2(36,-756):new Vector2(40+(cell%3)*480,-315-(cell/3)*246);
                Vector2 size=tutorial?new Vector2(280,40):new Vector2(400,172);
                var button=Button(transform,"",pos,size,()=>{if(SelectedLevel==index)return;SelectLevel(index);HabitatAudio.Play(HabitatAudioCue.Click);},false,null);
                button.name="Select destination "+index;rows[i]=button.GetComponent<Image>();
                rowLabels[i]=button.GetComponentInChildren<TMP_Text>();rowLabels[i].text=ArtName(index);rowLabels[i].fontStyle=FontStyles.Bold;rowLabels[i].fontSize=tutorial?18:34;rowLabels[i].color=mint;
                if(tutorial)continue;
                rowLabels[i].rectTransform.anchoredPosition=new Vector2(18,-40);rowLabels[i].rectTransform.sizeDelta=new Vector2(366,62);rowLabels[i].enabled=index==4;
                int artIndex=CardArt(index);
                if(artIndex>0){finalCards[i]=HabitatFinalUi.Art(button.transform,"R"+artIndex,Vector2.zero,new Vector2(400,116));finalCards[i].uvRect=new Rect(0,.35f,1,.65f);rowLabels[i].enabled=false;}
                else {rowLabels[i].enabled=true;rowLabels[i].color=mint;}
                completionBadges[i]=Label(button.transform,"CLEAR",19,new Color32(237,107,112,255),new Vector2(100,25),new Vector2(220,30),true).gameObject;
                completionBadges[i].SetActive(false);
                var locked=Label(button.transform,"未解锁",17,ink,new Vector2(110,-1),new Vector2(180,30),true);lockedBadges[i]=locked.gameObject;locked.gameObject.SetActive(false);
                Label(button.transform,index==10?"正式关卡 05":index>=5&&index<=8?"正式关卡 0"+(index-4):"FREE LEVEL · 自由探索",17,ink,new Vector2(12,-132),new Vector2(385,35));

                var selected=NewRect("Selected island underline",button.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(8,-169),new Vector2(378,4));Fill(selected,new Color32(237,107,112,255),false);selectionMarks[i]=selected.gameObject;
            }
            for(int col=0;col<3;col++)
            {
                var arrow=HabitatUiMotif.Add(transform,new Vector2(200+col*480,-518),new Vector2(28,25),mint);arrow.localRotation=Quaternion.Euler(0,0,-90);
                var arrow2=HabitatUiMotif.Add(transform,new Vector2(200+col*480,-536),new Vector2(28,25),mint);arrow2.localRotation=Quaternion.Euler(0,0,-90);
            }
            confirmButton=Button(transform,"出海  »",new Vector2(570,-190),new Vector2(300,76),ConfirmSelection,true);confirmButton.name="Confirm destination";
            HabitatFinalUi.ButtonArt(confirmButton,"R34");
            var previous=Button(header,"‹",new Vector2(1148,-187),new Vector2(46,40),()=>ChangePage(-1),false);previous.GetComponent<Image>().color=Color.clear;
            pageLabel=Label(header,"",17,ink,new Vector2(1197,-192),new Vector2(100,40),true);pageLabel.alignment=TextAlignmentOptions.Center;
            var next=Button(header,"›",new Vector2(1300,-187),new Vector2(46,40),()=>ChangePage(1),false);next.GetComponent<Image>().color=Color.clear;
            var footer=NewRect("Map hint pill",transform,new Vector2(0,1),new Vector2(0,1),new Vector2(436,-753),new Vector2(665,43));Fill(footer,new Color32(14,76,98,255),false);
            // Terrain data remains available to controller checks, without adding a preview card absent from the supplied art.
            var mapRect=NewRect("Terrain overview",transform,new Vector2(0,1),new Vector2(0,1),Vector2.zero,Vector2.zero);preview=mapRect.gameObject.AddComponent<RawImage>();preview.raycastTarget=false;
            previewPlaceholder=Label(transform,"",16,paper,Vector2.zero,Vector2.zero);previewPlaceholder.gameObject.SetActive(false);
        }

        // The standalone harbor keeps its original development destinations until
        // the main campaign explicitly supplies guards and names.
        public void ConfigureCampaign(Func<int,bool> allowed,Func<int,string> reason)
        {
            campaignAllowed=allowed;campaignReason=reason;
            if(rows!=null)SelectLevel(SelectedLevel);
        }

        string DestinationName(int index)
        {
            if(index==0)return "生态练习 · 可选";
            return ArtName(index);
        }

        public void Show(int initialLevel)
        {
            SelectLevel(Array.IndexOf(destinations,initialLevel)>=0?initialLevel:HabitatFigJamLevel.Index);
            gameObject.SetActive(true);
        }

        public void SelectLevel(int index)
        {
            if (Array.IndexOf(destinations,index)<0) return;
            SelectedLevel = index;
            if(index!=0)mapPage=Array.IndexOf(destinations,index)/6;
            for (int i = 0; i < rows.Length; i++)
            {
                int level=destinations[i];
                bool selected = level == index;
                rows[i].gameObject.SetActive(level==0||i/6==mapPage);
                bool allowed=campaignAllowed==null||campaignAllowed(level);
                rows[i].color = Color.clear;
                if(selectionMarks[i])selectionMarks[i].SetActive(selected);
                if(completionBadges[i])completionBadges[i].SetActive(completed!=null&&completed(level));
                if(lockedBadges[i])lockedBadges[i].SetActive(!allowed);
                foreach(var art in rows[i].GetComponentsInChildren<RawImage>())art.color=allowed?Color.white:new Color(.6f,.65f,.63f,.7f);
                rowLabels[i].color = allowed?mint:muted;
                if(finalCards[i]){bool clear=completed!=null&&completed(level);finalCards[i].texture=HabitatFinalUi.Texture((clear?"Clear":"R")+CardArt(level));if(completionBadges[i])completionBadges[i].SetActive(false);}
                rowLabels[i].text=DestinationName(level);
            }
            if(pageLabel)pageLabel.text=(mapPage+1)+" / "+Mathf.CeilToInt((destinations.Length-1)/6f);
            bool selectedAllowed=SelectedDestinationAllowed;
            confirmButton.interactable=selectedAllowed;
            confirmButton.GetComponentInChildren<TMP_Text>().text=selectedAllowed?"出海  »":"暂不可出发";
            destination.text=DestinationName(index);int titleId=TitleArt(index);destination.enabled=titleId==0;finalTitle.enabled=titleId>0;if(titleId>0)finalTitle.texture=HabitatFinalUi.Texture("R"+titleId);
            preview.gameObject.SetActive(false);
            previewPlaceholder.gameObject.SetActive(false);
            HabitatState state = HabitatLevels.Create(index);
            dimensions.text = $"{state.width} × {state.depth} 地块 · 地形概览";
            description.text = !selectedAllowed?campaignReason?.Invoke(index):state.objective;
            if (previewTexture) Destroy(previewTexture);
            // Draw only existing terrain data; the preview is not another authored map.
            const int cell = 4;
            previewTexture = new Texture2D(state.width * cell, state.depth * cell, TextureFormat.RGBA32, false)
            { name = "Destination terrain preview", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[previewTexture.width * previewTexture.height];
            for (int z = 0; z < state.depth; z++) for (int x = 0; x < state.width; x++)
            {
                Tile tile = HabitatRules.TileAt(state, x, z);
                Color color = tile.height <= state.waterLevel ? new Color32(67, 145, 153, 255)
                    : Color.Lerp(new Color32(121, 177, 113, 255), new Color32(222, 207, 139, 255), Mathf.Clamp01((tile.height - 1) / 3f));
                for (int py = 0; py < cell; py++) for (int px = 0; px < cell; px++)
                {
                    Color pixel = px == cell - 1 || py == cell - 1 ? color * .86f : color;
                    if (px == 1 && py == 1 && tile.prop != PropKind.None) pixel = new Color32(129, 90, 57, 255);
                    if (x == state.playerX && z == state.playerZ && px < 3 && py < 3) pixel = new Color32(250, 242, 205, 255);
                    pixel.a = 1;
                    pixels[(z * cell + py) * previewTexture.width + x * cell + px] = pixel;
                }
            }
            previewTexture.SetPixels32(pixels); previewTexture.Apply(false, true);
            preview.texture = previewTexture;
            float largest = Mathf.Max(state.width, state.depth);
            preview.rectTransform.sizeDelta = new Vector2(100 * state.width / largest, 100 * state.depth / largest);
        }

        void ChangePage(int direction)
        {
            int pages=Mathf.Max(1,Mathf.CeilToInt((destinations.Length-1)/6f));
            int next=(mapPage+direction+pages)%pages;
            int at=Mathf.Min(next*6,destinations.Length-2);SelectLevel(destinations[at]);
        }

        public void ConfirmSelection()
        {
            if (gameObject.activeInHierarchy && SelectedLevel >= 0 && SelectedLevel < HabitatLevels.Names.Length && SelectedDestinationAllowed)
                onConfirm?.Invoke(SelectedLevel);
        }

        void OnDestroy() { if (previewTexture) Destroy(previewTexture); }

        static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        static void Fill(RectTransform rect, Color color, bool raycast)
        {
            var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = raycast;
            image.sprite = HabitatHud.RoundedSprite; image.type = Image.Type.Sliced;
        }

        TMP_Text Label(Transform parent, string text, float size, Color color, Vector2 position, Vector2 dimensions, bool bold = false)
        {
            var rect = NewRect("Text", parent, new Vector2(0, 1), new Vector2(0, 1), position, dimensions);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = size; label.color = color;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Truncate;
            return label;
        }

        Button Button(Transform parent, string text, Vector2 position, Vector2 size, Action action, bool primary,HabitatAudioCue? cue=HabitatAudioCue.Click)
        {
            var rect = NewRect(text + " button", parent, new Vector2(0, 1), new Vector2(0, 1), position, size);
            Fill(rect, primary ? mint : surface, true);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var label = Label(rect, text, 18, primary ? paper : ink, new Vector2(12, 0), new Vector2(size.x - 24, size.y), primary);
            label.alignment = TextAlignmentOptions.Center;
            button.onClick.AddListener(() => {if(cue.HasValue)HabitatAudio.Play(cue.Value);action();});
            return button;
        }
    }
}
