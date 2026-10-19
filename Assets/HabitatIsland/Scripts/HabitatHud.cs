using System;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace HabitatIsland
{
    public partial class HabitatHud : MonoBehaviour
    {
        HabitatGame game;
        Canvas canvas;
        TMP_FontAsset font;
        TMP_Text title, stats, objective, selection, events, toast, status, target, carrying, pauseLabel, speedLabel, shopMessage, movementLabel;
        TMP_Text[] animalLabels = new TMP_Text[6];
        UnityEngine.UI.Image[] animalBars = new UnityEngine.UI.Image[6];
        UnityEngine.UI.Image[] toolImages = new UnityEngine.UI.Image[5];
        GameObject modal, settingsPanel, shopPanel, helpPanel, ecologyPanel, inspectorPanel, eventPanel, toastPanel, toolsPanel, gameplayInterface;
        HabitatMapSelectionPanel mapSelection;
        TMP_Text handbookLabel, questProgress, questTitle, settingsFeedback;
        TMP_Text[] toolLabels = new TMP_Text[5], parameterValues = new TMP_Text[7];
        TMP_Text[] toolKeys = new TMP_Text[5];
        UnityEngine.UI.Image questFill;
        UnityEngine.UI.Button rotateButton, useToolButton;
        UnityEngine.UI.Image[][] toolIconImages = new UnityEngine.UI.Image[5][];
        float messageAt = -100;
        string lastMessage;
        bool priorPause;
        float nextRefresh;
        readonly Color ink = new Color32(23,61,58,255);
        readonly Color paper = new Color32(248,250,247,255);
        readonly Color muted = new Color32(97,117,111,255);
        readonly Color mint = new Color32(36,93,80,255);
        readonly Color surface = new Color32(232,239,235,255);
        readonly Color rule = new Color32(214,225,219,255);
        readonly Color ochre = new Color32(197,155,83,255);
        readonly Color coral = new Color32(216,117,88,255);
        static Sprite roundedSprite;
        public static Sprite RoundedSprite
        {
            get
            {
                if(roundedSprite!=null)return roundedSprite;
                const int size=32;const float radius=7;
                var pixels=new Color32[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float dx=Mathf.Max(Mathf.Abs(x+.5f-size*.5f)-(size*.5f-radius),0),dy=Mathf.Max(Mathf.Abs(y+.5f-size*.5f)-(size*.5f-radius),0);
                    pixels[y*size+x]=new Color32(255,255,255,(byte)(Mathf.Clamp01(radius-Mathf.Sqrt(dx*dx+dy*dy)+.5f)*255));
                }
                var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Habitat rounded UI",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};texture.SetPixels32(pixels);texture.Apply(false,true);
                roundedSprite=Sprite.Create(texture,new UnityEngine.Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(8,8,8,8));return roundedSprite;
            }
        }
        public bool ModalOpen => modal != null && modal.activeSelf;
        public bool ShopOpen => ModalOpen && shopPanel && shopPanel.activeSelf;
        public bool MapSelectionOpen => ModalOpen && mapSelection && mapSelection.gameObject.activeSelf;

        public void Initialize(HabitatGame controller)
        {
            Debug.Log("[HabitatHUD] Initialize start");
            game = controller;
            lastMessage = game.State.message;
            font = HabitatUiAssets.Body;
            Debug.Log("[HabitatHUD] Font resource load complete");
            if (font == null)
            {
                Font source = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                font = TMP_FontAsset.CreateFontAsset(source, 64, 5, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            }
            // Fixed Chinese interface text needs glyphs, not runtime OpenType table extraction.
            // Avoid native GSUB/GPOS work for each newly encountered CJK glyph.
            font.getFontFeatures = false;
            Debug.Log("[HabitatHUD] Font ready; runtime font features disabled");
            var go = new GameObject("Habitat HUD",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            go.transform.SetParent(transform,false);
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,900);
            // Keep the full layout visible in wide, tall, and docked Game windows.
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            var main=Rect("Gameplay interface",canvas.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
            main.anchorMin=Vector2.zero;main.anchorMax=Vector2.one;main.offsetMin=main.offsetMax=Vector2.zero;
            gameplayInterface=main.gameObject;
            Debug.Log("[HabitatHUD] Canvas and event system ready; building main UI");
            BuildMain();
            Debug.Log("[HabitatHUD] Main UI ready; building modal UI");
            BuildModals();
            BuildWorkflowPanels();
            BuildStoryControls();
            ApplyDeliveredInterface();
            ApplySecondDelivery();
            // Modal pages stay visible when the ordinary in-game HUD is hidden.
            modal.transform.SetParent(canvas.transform,false);modal.transform.SetAsLastSibling();
            Debug.Log("[HabitatHUD] Initialize complete");
        }

        RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent,false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin=r.anchorMax=anchor; r.pivot=pivot; r.anchoredPosition=pos; r.sizeDelta=size; return r;
        }
        RectTransform Box(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size,Color color,bool raycast=false)
        {
            var r=Rect(name,parent,anchor,pivot,pos,size); var im=r.gameObject.AddComponent<UnityEngine.UI.Image>(); im.color=color;im.raycastTarget=raycast;im.sprite=RoundedSprite;im.type=UnityEngine.UI.Image.Type.Sliced;return r;
        }
        TMP_Text Text(Transform parent,string value,int size,Color color,Vector2 pos,Vector2 dimensions,TextAlignmentOptions alignment=TextAlignmentOptions.TopLeft,bool emphasis=false)
        {
            var r=Rect("Text",parent,new Vector2(0,1),new Vector2(0,1),pos,dimensions);
            var t=r.gameObject.AddComponent<TextMeshProUGUI>(); t.font=font;t.text=value;t.fontSize=size;t.color=color;t.alignment=alignment;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Truncate;t.richText=true;
            // The regular-only CJK asset has no Medium/Semibold face: explicit Bold gives
            // key interface text a reliable SDF stroke weight without another font atlas.
            t.fontStyle=emphasis?FontStyles.Bold:FontStyles.Normal;t.enableAutoSizing=false;return t;
        }
        UnityEngine.UI.Button Button(Transform parent,string text,Vector2 pos,Vector2 size,Action action,Color? color=null,bool primary=false,bool storyControl=false,HabitatAudioCue? cue=HabitatAudioCue.Click)
        {
            var r=Box("Button "+text,parent,new Vector2(0,1),new Vector2(0,1),pos,size,color??(primary?mint:surface),true);
            var b=r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic=r.GetComponent<UnityEngine.UI.Image>();
            var colors=b.colors;colors.highlightedColor=new Color(.91f,.96f,.93f);colors.pressedColor=new Color(.74f,.85f,.79f);colors.selectedColor=Color.white;colors.fadeDuration=.12f;b.colors=colors;
            b.gameObject.AddComponent<HabitatUiButtonMotion>();
            b.navigation=new UnityEngine.UI.Navigation { mode=UnityEngine.UI.Navigation.Mode.None };
            var t=Text(r,text,16,primary?paper:ink,new Vector2(5,0),new Vector2(size.x-10,size.y),TextAlignmentOptions.Center,primary);
            // Pointer callbacks can arrive in the same frame that a move/salvage starts.
            // Guard centrally, including callbacks that directly change tuning or selection.
            b.onClick.AddListener(()=>{if(game&&(storyControl?game.StoryPlaying:!game.Busy&&!game.StoryPlaying))
                {if(cue.HasValue)HabitatAudio.Play(cue.Value);action();}});return b;
        }
        RectTransform Card(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size)
        {
            var r=Box(name,parent,anchor,pivot,pos,size,paper,true);
            var shadow=r.gameObject.AddComponent<UnityEngine.UI.Shadow>();shadow.effectColor=new Color(.03f,.13f,.12f,.10f);shadow.effectDistance=new Vector2(0,-3);shadow.useGraphicAlpha=true;return r;
        }

        void Rule(Transform parent, float x, float y, float width)
        { Box("Divider",parent,new Vector2(0,1),new Vector2(0,1),new Vector2(x,y),new Vector2(width,1),rule); }

        RectTransform ModalCard(string name, Vector2 size, string heading, string subtitle)
        {
            var card=Card(name,modal.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,size);
            Text(card,heading,26,ink,new Vector2(28,-24),new Vector2(size.x-100,40),emphasis:true);
            Text(card,subtitle,15,muted,new Vector2(28,-71),new Vector2(size.x-56,28));
            Button(card,"×",new Vector2(size.x-60,-24),new Vector2(32,32),ClosePanelsFromInput,surface,cue:null).name="Close "+name;
            Rule(card,28,-110,size.x-56);
            return card;
        }
        RectTransform Icon(Transform parent,int kind,Vector2 pos,float size,Color color)
        {
            var root=Rect("Tool icon",parent,new Vector2(0,1),new Vector2(0,1),pos,new Vector2(size,size));
            var drawing=Rect("Vector strokes",root,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(32,32));drawing.localScale=Vector3.one*(size/32);
            Action<Vector2,Vector2,float> line=(a,b,w)=>{var d=b-a;var r=Box("Stroke",drawing,new Vector2(.5f,.5f),new Vector2(.5f,.5f),(a+b)*.5f,new Vector2(d.magnitude,w),color);r.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);};
            if(kind==0){Box("Held object",drawing,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,-1),new Vector2(17,15),color);line(new Vector2(-13,10),new Vector2(-11,-9),3);line(new Vector2(13,10),new Vector2(11,-9),3);}
            else if(kind==1){line(new Vector2(-13,2),new Vector2(0,10),2.3f);line(new Vector2(0,10),new Vector2(13,2),2.3f);line(new Vector2(13,2),new Vector2(0,-6),2.3f);line(new Vector2(0,-6),new Vector2(-13,2),2.3f);line(new Vector2(-13,2),new Vector2(-13,-7),2.3f);line(new Vector2(13,2),new Vector2(13,-7),2.3f);line(new Vector2(-13,-7),new Vector2(0,-14),2.3f);line(new Vector2(13,-7),new Vector2(0,-14),2.3f);line(new Vector2(0,-6),new Vector2(0,-14),2.3f);}
            else if(kind==2){line(new Vector2(-12,-12),new Vector2(7,13),3);line(new Vector2(7,13),new Vector2(11,-7),1.6f);line(new Vector2(11,-7),new Vector2(6,-11),2);line(new Vector2(6,-11),new Vector2(3,-7),2);}
            else if(kind==3){line(new Vector2(-12,-11),new Vector2(12,-11),2);line(new Vector2(-12,8),new Vector2(12,8),2);for(int i=-1;i<=1;i++)line(new Vector2(i*10,-11),new Vector2(i*10,8),2);line(new Vector2(-12,8),new Vector2(0,14),2);line(new Vector2(0,14),new Vector2(12,8),2);}
            else if(kind==4){Box("Fish body",drawing,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-3,0),new Vector2(23,15),color);line(new Vector2(7,0),new Vector2(15,9),3);line(new Vector2(15,9),new Vector2(15,-9),3);line(new Vector2(15,-9),new Vector2(7,0),3);}
            else {line(new Vector2(-12,-12),new Vector2(-12,12),2);line(new Vector2(12,-12),new Vector2(12,12),2);line(new Vector2(-12,12),new Vector2(0,9),2);line(new Vector2(12,12),new Vector2(0,9),2);line(new Vector2(0,9),new Vector2(0,-14),2);line(new Vector2(-12,-12),new Vector2(0,-14),2);line(new Vector2(12,-12),new Vector2(0,-14),2);}
            return root;
        }
        void Open(GameObject panel,HabitatAudioCue cue=HabitatAudioCue.Open)
        {
            if(game.Busy||game.StoryPlaying)return;
            if(ModalOpen && ActiveModalPage()==panel && !(panel==shopPanel&&purchasePanel&&purchasePanel.activeSelf)){ClosePanelsFromInput();return;}
            if(panel==startPanel)startSubmenu=false;else if(SessionMenuOpen)startSubmenu=true;
            HabitatAudio.Play(cue);
            if(!ModalOpen) priorPause=game.paused;
            game.paused=true;modal.SetActive(true);settingsPanel.SetActive(panel==settingsPanel);shopPanel.SetActive(panel==shopPanel||panel==purchasePanel);helpPanel.SetActive(panel==helpPanel);
            var shopInput=shopPanel.GetComponent<UnityEngine.CanvasGroup>();if(!shopInput)shopInput=shopPanel.AddComponent<UnityEngine.CanvasGroup>();
            shopInput.interactable=shopInput.blocksRaycasts=panel!=purchasePanel;
            if(mapSelection)mapSelection.gameObject.SetActive(panel==mapSelection.gameObject);
            if(homePanel)homePanel.SetActive(panel==homePanel);
            if(audioPanel)audioPanel.SetActive(panel==audioPanel);
            foreach(var extra in new[]{bookPanel,summaryPanel,guidePanel,purchasePanel})if(extra)extra.SetActive(extra==panel);
            SetWorkflowPanel(panel);
            SetPlanPanel(panel);
            if(chapterDialoguePanel)
            {
                if(panel!=chapterDialoguePanel)ClearChapterDialogue();
                chapterDialoguePanel.SetActive(panel==chapterDialoguePanel);
            }
        }
        public void ToggleSettings()=>OpenAudioSettings(null);
        public void OpenRemoteShop(){shopSelling=false;ToggleShop();}
        public void ToggleShop()
        {
            if(game.Busy||game.StoryPlaying)return;
            if(!game.CanPurchaseHere)
            {game.State.message="请先走到码头商店旁，再按 E 或 B。";return;}
            Open(shopPanel);
            if(ModalOpen)RefreshShopDelivery();
        }
        public void ToggleHelp()=>OpenGuide(null);
        public void OpenMapSelection(int suggestedLevel=-1)
        {
            if(game.Busy||game.StoryPlaying||!mapSelection||MapSelectionOpen)return;
            if(!HabitatLevels.IsHomeHarbor(game.State)){game.InteractWithBoat();return;}
            mapSelection.ConfigureCampaign(index=>game.CanTravelToLevel(index,out _),index=>{game.CanTravelToLevel(index,out string reason);return reason;});
            mapSelection.ConfigureCompletion(game.HasCompletedLevel);
            Open(mapSelection.gameObject);
            mapSelection.Show(suggestedLevel>=0&&suggestedLevel<HabitatLevels.Names.Length?suggestedLevel:game.RecommendedCampaignLevel);
            mapTravelFeedback=null;mapFeedbackSelection=mapSelection.SelectedLevel;
            RefreshMapTravelNote();
        }
        // Loading/travel/story code closes silently. Only explicit user dismissal emits a cue.
        public void ClosePanels(){startSubmenu=false;confirmAction=null;confirmationOrigin=null;ClearUtilityPreview();ClearChapterDialogue();if(modal==null||!modal.activeSelf)return;modal.SetActive(false);game.paused=priorPause;}
        public void ClosePanelsFromInput(){if(SessionMenuOpen&&!game.SessionStarted)return;if(ReturnFromBatchPanel())return;if(startSubmenu){ShowStartMenu();return;}bool open=ModalOpen;ClosePanels();if(open)HabitatAudio.Play(HabitatAudioCue.Close);}
        void BackToGame(){if(startSubmenu){ShowStartMenu();return;}bool open=ModalOpen;ClosePanels();if(open)HabitatAudio.Play(HabitatAudioCue.Back);}
        void SaveProgress()
        {
            if(game.Busy||game.StoryPlaying)return;
            game.SaveSession();
            settingsFeedback.text=game.State.message.StartsWith("已保存")?"进度已保存，可随时读取。":"保存失败，进度未写入。";
        }
        void LoadProgress()
        {
            game.LoadSession();
            if(ModalOpen) settingsFeedback.text=game.State.message.StartsWith("还没有")?"还没有已保存的进度。":"读取失败，请检查存档后重试。";
        }
        void Adjust(int id,int sign)
        {
            if(game.Busy||game.StoryPlaying)return;
            var t=game.tuning;float before=ParameterValue(id);
            switch(id){case 0:t.breedSeconds=Mathf.Clamp(t.breedSeconds+sign*2,2,120);break;case 1:t.moveSeconds=Mathf.Clamp(t.moveSeconds+sign*.5f,.5f,10);break;case 2:t.shrimpRadius=Mathf.Clamp(t.shrimpRadius+sign,1,3);break;case 3:t.friendlyRadius=Mathf.Clamp(t.friendlyRadius+sign,1,3);break;case 4:t.minWater=Mathf.Clamp(t.minWater+sign,1,20);break;case 5:t.minPond=Mathf.Clamp(t.minPond+sign,1,20);break;case 6:t.minHighland=Mathf.Clamp(t.minHighland+sign,1,9);break;}
            if(!Mathf.Approximately(before,ParameterValue(id)))HabitatAudio.Play(HabitatAudioCue.Confirm);
        }
        float ParameterValue(int id)
        {
            var t=game.tuning;
            return id switch {0=>t.breedSeconds,1=>t.moveSeconds,2=>t.shrimpRadius,3=>t.friendlyRadius,4=>t.minWater,5=>t.minPond,6=>t.minHighland,_=>0};
        }
        public void Refresh()
        {
            RefreshWorkflow();
            RefreshSecondDelivery();
            HabitatAudio.SetDucked(ModalOpen||game.StoryPlaying||(game.Fishing&&game.Fishing.IsOpen));
            if(RefreshStoryControls())return;
            if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.12f;
            var s=game.State;var t=game.tuning;
            RefreshHarborControls();
            RefreshAudioControls();
            title.text=s.levelName;
            stats.text=$"<b>{s.coins}</b> <size=13>贝币</size>   <b>{s.cargo}</b> <size=13>土块</size>   <b>{s.fish}</b> <size=13>鱼</size>   <b>{s.shrimp}</b> <size=13>虾</size>   <b>{s.glassFish}</b> <size=13>琉璃鱼</size>";
            objective.text=HabitatMechanicLevels.HabitatObjective(s);
            int goalCount=s.goals==null?0:s.goals.Length;
            int earned=goalCount==0?0:s.goals.Count(g=>s.earnedSpecies.Contains(g));
            questTitle.text=goalCount==0?"探索提示":"当前目标";
            questProgress.text=s.won?"目标达成 · 继续自由观察":goalCount==0?$"自由观察    海平面 {s.waterLevel}":$"首次繁衍  {earned} / {goalCount}    海平面 {s.waterLevel}";
            questFill.rectTransform.sizeDelta=new Vector2(230*(s.won?1f:earned/(float)Mathf.Max(1,goalCount)),3);
            if(s.authoredLevel)
            {
                bool habitatGoal=HabitatMechanicLevels.HasHabitatGoal(s);
                questTitle.text=habitatGoal?(s.won?"正式关卡 · 已通关":"正式关卡 · 安居目标"):"正式关卡 · 探索记录";
                questProgress.text=habitatGoal?HabitatMechanicLevels.HabitatChecklist(s,t):HabitatLevels.Checklist(s);
                questProgress.fontSize=12;
                questProgress.rectTransform.anchoredPosition=new Vector2(20,-158);
                questProgress.rectTransform.sizeDelta=new Vector2(230,42);
                questFill.rectTransform.sizeDelta=habitatGoal?new Vector2(230*(s.won?1f:HabitatMechanicLevels.HabitatProgress(s,t)/Mathf.Max(1,HabitatMechanicLevels.HabitatGoalCount(s))),3):Vector2.zero;
            }
            else
            {
                questProgress.fontSize=13;
                questProgress.rectTransform.anchoredPosition=new Vector2(20,-174);
                questProgress.rectTransform.sizeDelta=new Vector2(230,24);
            }
            RefreshTutorialControls();
            RefreshChapterControls();
            RefreshDeliveredInterface();
            handbookLabel.text=ecologyPanel.activeSelf?"居民手册  −":"居民手册  +";
            for(int i=0;i<6;i++)
            {
                var a=s.animals.Find(v=>(int)v.species==i);
                if(a==null){animalLabels[i].text=ResidentName((Species)i)+"   <color=#61756F>尚未入住</color>";animalBars[i].rectTransform.sizeDelta=new Vector2(0,2);continue;}
                var report=HabitatRules.Evaluate(s,a,t);
                string state=a.carried?"搬运中":report.suitable?"安居中":!report.safe?"受惊":"待改善";
                animalLabels[i].text=$"{ResidentName(a.species)}   <color={(report.suitable?"#245D50":"#A4553F")}>{state}</color>   <size=12>幼体 {a.offspring}</size>";
                animalBars[i].rectTransform.sizeDelta=new Vector2(226*Mathf.Clamp01(a.progress/Mathf.Max(.1f,t.breedSeconds)),2);
            }
            string held="空手";
            if(s.carryingAnimal>=0){var a=s.animals.Find(v=>v.id==s.carryingAnimal);held=a==null?"动物":ResidentName(a.species);}
            else if(s.carryingProp!=PropKind.None)held=PropName(s.carryingProp);
            carrying.text=$"手持：{held}"+(s.onHarbor?"  ·  码头":HabitatRules.AtDock(s)?"  ·  渡口":"");
            game.GetTarget(out int x,out int z);var tile=HabitatRules.TileAt(s,x,z);
            inspectorPanel.SetActive(HabitatRules.Inside(s,s.selectedX,s.selectedZ));
            if(tile!=null)
            {
                var sb=new StringBuilder($"<b><size=21>地块 {x+1} · {z+1}</size></b>\n<size=13><color=#61756F>{(HabitatRules.IsWater(s,x,z)?"水域":"陆地")} · 高度 {HabitatRules.Height(s,x,z)}{(tile.prop==PropKind.None?"":" · "+PropName(tile.prop))}</color></size>");
                var a=s.animals.Find(v=>v.x==x&&v.z==z&&!v.carried);
                if(a!=null){var r=HabitatRules.Evaluate(s,a,t);sb.Append($"\n<b>{ResidentName(a.species)}</b>  <size=13>幼体 {a.offspring}</size>\n<size=14>{r.reason}</size>\n<size=13><color=#61756F>繁衍 {a.progress:0} / {t.breedSeconds:0} 秒</color></size>");}
                else sb.Append(tile.trap?"\n\n已布置虾笼，等待后代。":s.authoredLevel?"":"\n\n选择工具，改造这块栖地。");
                if(s.authoredLevel&&!game.ChapterActive)
                {
                    string raw=HabitatLevels.CellLabel(s,x,z);
                    sb.Append($"\n<size=12><color=#61756F>原图 {s.depth-z} 行 {x+1} 列 · {(raw=="."?"水域":raw)}</color></size>");
                }
                selection.text=sb.ToString();
                bool canRotate=tile.prop==PropKind.Road||tile.prop==PropKind.Fan;
                rotateButton.gameObject.SetActive(canRotate);
                float extra=s.authoredLevel?48:0;
                selection.rectTransform.sizeDelta=new Vector2(240,139+extra);
                target.rectTransform.anchoredPosition=new Vector2(20,-210-extra);
                useToolButton.GetComponent<RectTransform>().anchoredPosition=new Vector2(20,-246-extra);
                rotateButton.GetComponent<RectTransform>().anchoredPosition=new Vector2(20,-297-extra);
                inspectorPanel.GetComponent<RectTransform>().sizeDelta=new Vector2(280,(canRotate?344:306)+extra);
            }
            target.text="当前工具："+HabitatGame.ToolNames[game.tool];
            events.text=s.events==null?"":string.Join("\n\n",s.events.Skip(Math.Max(0,s.events.Count-3)));
            if(lastMessage!=s.message){lastMessage=s.message;messageAt=Time.unscaledTime;toast.text=s.message.StartsWith("已保存测试记录：")?"进度已保存。":s.message;}
            toastPanel.SetActive(Time.unscaledTime-messageAt<6f&&!ModalOpen&&s.message!=s.objective);
            if(shopPanel.activeSelf&&ModalOpen)
            {
                RefreshShopDelivery();
            }
            string clock=$"{(int)s.elapsed/60:00}:{(int)s.elapsed%60:00}";
            status.text=$"第 {Mathf.Max(1,s.day)} 天  ·  "+(s.won?"栖居达成":game.paused?"已暂停":"观察中")+"  ·  "+clock;
            pauseLabel.text=game.paused?"继续":"暂停";speedLabel.text=game.speed+"×";
            RefreshPlanHud();
            for(int i=0;i<5;i++)
            {
                bool selected=i==game.tool;toolImages[i].color=selected?mint:paper;toolLabels[i].color=selected?paper:ink;
                toolKeys[i].color=selected?paper:muted;
                foreach(var im in toolIconImages[i])im.color=selected?paper:ink;
            }
            if(settingsPanel.activeSelf&&ModalOpen)
            {
                string[] values={t.breedSeconds.ToString("0"),t.moveSeconds.ToString("0.0"),t.shrimpRadius.ToString(),t.friendlyRadius.ToString(),t.minWater.ToString(),t.minPond.ToString(),t.minHighland.ToString()};
                for(int i=0;i<7;i++)parameterValues[i].text=values[i];
                movementLabel.text="自主移动："+(t.animalMovement?"开":"关");
            }
        }

        public static string PropName(PropKind kind)
        {
            switch(kind){case PropKind.Crate:return "木箱";case PropKind.Float:return "水中浮箱";case PropKind.Raft:return "往返竹排";case PropKind.Climb:return "普通土块";case PropKind.ClimbBlock:return "攀爬方块";case PropKind.Road:return "道路";case PropKind.Fan:return "风机";case PropKind.Tide:return "潮汐引擎";case PropKind.Obstacle:return "障碍";case PropKind.Chest:return "宝箱";case PropKind.Shell:return "贝壳";default:return "无";}
        }
    }
}
