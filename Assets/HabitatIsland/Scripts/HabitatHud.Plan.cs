using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HabitatIsland
{
    public partial class HabitatHud
    {
        GameObject savesPanel, saveDetailsPanel, completionChoicesPanel, saveReturnPanel, audioReturnPanel, bagActionPanel, contextPanel;
        TMP_Text savesFeedback, saveDetails, completionMessage, bagActionTitle, contextText, guidePager;
        Button saveCurrent, loadCurrent, deleteCurrent, summaryNext;
        readonly Button[] saveRows = new Button[3];
        int selectedSaveSlot = 1, selectedBagTool = -1, selectedBagIndex = -1;
        bool automaticGuide; float nextGuideAt, nextSlotRefresh;
        RawImage guideItemImage;TMP_Text guideFallbackText;
        public bool PlayClockSuspended => ModalOpen && (startSubmenu || ClockMenu(ActiveModalPage())
            || (confirmPanel && confirmPanel.activeSelf && ClockMenu(confirmationOrigin)));
        bool ClockMenu(GameObject page) => page && (page==startPanel || page==pausePanel || page==settingsPanel
            || page==audioPanel || page==savesPanel || page==saveDetailsPanel);

        void BuildPlanInterface()
        {
            BuildSaveSlots(); BuildCompletionChoices(); BuildBagSelection(); BuildContextPrompt();StylePlanConfirmation();StylePlanDialogue();
            var dismiss = modal.AddComponent<HabitatUiBackdropDismiss>();
            dismiss.Clicked = () => { if (bookPanel && bookPanel.activeSelf) ClosePanelsFromInput(); };
            TextButton(inventoryPanel.transform,"图鉴",1130,-20,100,()=>OpenBook(inventoryPanel),18).name="Inventory creature book";
            TextButton(inventoryPanel.transform,"设置",1250,-20,100,()=>OpenAudioSettings(inventoryPanel),18).name="Inventory settings";
            TextButton(bookPanel.transform,"背包",1120,-36,100,ToggleInventory,18);
            TextButton(bookPanel.transform,"设置",1240,-36,100,()=>OpenAudioSettings(bookPanel),18);
            TextButton(audioPanel.transform,"存档管理",214,-568,180,()=>OpenSaveSlots(audioPanel,true),18).name="Settings save slots";
            guideItemImage=HabitatUiAssets.Icon(guidePanel.transform,"Rod",new Vector2(390,-196),new Vector2(370,330));
            guideItemImage.transform.SetSiblingIndex(guideDemo.transform.GetSiblingIndex()+1);
            guideFallbackText=Text(guidePanel.transform,"",28,Color.white,new Vector2(240,-260),new Vector2(670,235),TextAlignmentOptions.Center,true);
        }

        void StylePlanConfirmation()
        {
            var p=Skin(confirmPanel,new Vector2(800,390),"");
            DeliveredConfirmationSurface(p);
            Text(p,"确认操作",29,deepSea,new Vector2(180,-32),new Vector2(440,45),TextAlignmentOptions.Center,true);
            DeliveredDialogPiece(p,"R104",new Rect(.025f,.46f,.365f,.12f),new Vector2(75,-94),new Vector2(650,32));
            confirmText=Text(p,"",23,deepSea,new Vector2(65,-145),new Vector2(670,105),TextAlignmentOptions.Center);
            confirmText.enableAutoSizing=true;confirmText.fontSizeMin=19;confirmText.fontSizeMax=23;
            var cancel=Button(p,"取消",new Vector2(430,-296),new Vector2(168,69),CancelConfirmation,Color.clear);
            cancel.name="Cancel plan confirmation";DeliveredDialogButton(cancel,new Rect(.534f,.732f,.128f,.177f));
            var accept=Button(p,"确认",new Vector2(202,-296),new Vector2(168,69),()=>{var action=confirmAction;confirmAction=null;action?.Invoke();},Color.clear);
            accept.name="Accept plan confirmation";DeliveredDialogButton(accept,new Rect(.339f,.732f,.128f,.177f));
            foreach(var b in p.GetComponentsInChildren<Button>(true))if(b.name.StartsWith("Close "))b.transform.SetAsLastSibling();
        }
        void StylePlanDialogue()
        {
            var p=Skin(chapterDialoguePanel,new Vector2(1440,300),"");p.anchorMin=p.anchorMax=p.pivot=new Vector2(.5f,0);p.anchoredPosition=new Vector2(0,24);
            foreach(Transform child in p)
                if(child.name=="Close "+chapterDialoguePanel.name){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            // Portrait overlaps the upper edge in front of the dialogue surface.
            DeliveredDialogPiece(p,"R87",new Rect(.043f,.331f,.957f,.669f),Vector2.zero,new Vector2(1440,300));
            // Large foreground bust: the screen edge crops the lower body, like a dialogue illustration.
            HabitatUiAssets.Icon(p,"Shopkeeper",new Vector2(-80,340),new Vector2(600,924));
            DeliveredDialogPiece(p,"R87",new Rect(0,0,.60f,.30f),new Vector2(540,96),new Vector2(400,78));
            chapterSpeaker=Text(p,"商店老板",18,Color.white,Vector2.zero,Vector2.zero);
            chapterSpeaker.gameObject.SetActive(false);
            chapterDialogueBody=Text(p,"",23,Color.white,new Vector2(540,-32),new Vector2(850,180));chapterDialogueBody.enableAutoSizing=true;chapterDialogueBody.fontSizeMin=18;chapterDialogueBody.fontSizeMax=23;
            chapterDialoguePage=Text(p,"",16,Color.white,new Vector2(540,-244),new Vector2(150,36),TextAlignmentOptions.MidlineLeft);
            TextButton(p,"稍后再聊",860,-238,180,BackToGame,19).name="Close chapter dialogue";
            chapterDialogueNext=Button(p,"继续交谈",new Vector2(1080,-238),new Vector2(260,52),AdvanceChapterDialogue,Color.clear);chapterDialogueNext.name="Advance chapter dialogue";chapterDialogueNextLabel=chapterDialogueNext.GetComponentInChildren<TMP_Text>();
            chapterDialogueNextLabel.color=Color.white;chapterDialogueNextLabel.rectTransform.sizeDelta=new Vector2(205,52);
            foreach(var b in p.GetComponentsInChildren<Button>(true))if(b.name.StartsWith("Close "))b.transform.SetAsLastSibling();
        }

        // Sample the delivered sheet directly, preserving its corners and decoration.
        // Source rectangles use top-left normalized coordinates; no source pixels are rewritten.
        RawImage DeliveredDialogPiece(Transform parent,string key,Rect source,Vector2 position,Vector2 size)
        {
            var art=HabitatFinalUi.Art(parent,key,position,size);
            art.uvRect=new Rect(source.x,1-source.y-source.height,source.width,source.height);
            return art;
        }
        void DeliveredDialogButton(Button button,Rect source)
        {
            foreach(var label in button.GetComponentsInChildren<TMP_Text>())label.enabled=false;
            button.targetGraphic=DeliveredDialogPiece(button.transform,"R104",source,Vector2.zero,button.GetComponent<RectTransform>().sizeDelta);
            var colors=button.colors;colors.highlightedColor=new Color(1,.96f,.85f);colors.pressedColor=new Color(.8f,.9f,.88f);button.colors=colors;
        }
        void DeliveredConfirmationSurface(Transform parent)
        {
            const float w=800,h=390,b=46;
            DeliveredDialogPiece(parent,"R104",new Rect(.18f,.36f,.04f,.04f),new Vector2(b,-b),new Vector2(w-2*b,h-2*b));
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)
                DeliveredDialogPiece(parent,"R104",new Rect(x==0?0:.95f,y==0?0:.82f,.05f,.18f),new Vector2(x==0?0:w-b,y==0?0:-(h-b)),new Vector2(b,b));
            for(int y=0;y<2;y++)DeliveredDialogPiece(parent,"R104",new Rect(.2f,y==0?0:.82f,.02f,.18f),new Vector2(b,y==0?0:-(h-b)),new Vector2(w-2*b,b));
            for(int x=0;x<2;x++)DeliveredDialogPiece(parent,"R104",new Rect(x==0?0:.95f,.65f,.05f,.03f),new Vector2(x==0?0:w-b,-b),new Vector2(b,h-2*b));
        }

        GameObject ActiveModalPage()
        {
            if(!ModalOpen)return null;
            if(confirmPanel&&confirmPanel.activeSelf)return confirmPanel;
            foreach(Transform child in modal.transform)if(child.gameObject.activeSelf)return child.gameObject;
            return null;
        }
        void SetPlanPanel(GameObject panel)
        {
            foreach(var p in new[]{savesPanel,saveDetailsPanel,completionChoicesPanel})if(p)p.SetActive(p==panel);
            if(panel!=inventoryPanel && bagActionPanel)bagActionPanel.SetActive(false);
        }
        bool ReturnFromPlanPanel()
        {
            if(confirmPanel&&confirmPanel.activeSelf){CancelConfirmation();return true;}
            if(saveDetailsPanel&&saveDetailsPanel.activeSelf){Open(savesPanel);RefreshSaveSlots();return true;}
            if(savesPanel&&savesPanel.activeSelf){var origin=saveReturnPanel;saveReturnPanel=null;if(origin)Open(origin);else ClosePanels();return true;}
            if(completionChoicesPanel&&completionChoicesPanel.activeSelf){ClosePanels();return true;}
            return false;
        }

        void OpenAudioSettings(GameObject origin)
        {
            audioReturnPanel=origin;
            Open(audioPanel);
        }
        void ReturnFromAudioSettings()
        {
            var origin=audioReturnPanel;audioReturnPanel=null;
            if(startSubmenu){ShowStartMenu();return;}
            if(origin)Open(origin);else ClosePanels();
        }

        // No separate art was supplied for save management: use the planner's three-slot list and details dialog.
        void BuildSaveSlots()
        {
            savesPanel=ModalCard("Save slots",new Vector2(950,700),"存档管理","").gameObject;
            var p=Skin(savesPanel,new Vector2(950,700),"");
            Shape(p,"Save list background",0,0,950,700,aqua,36);
            Heading(p,"SaveHeading","存档管理",300,-26,350);
            for(int i=0;i<3;i++)
            {
                int slot=i+1;
                var row=SeaButton(p,"",new Vector2(76,-160-i*140),new Vector2(798,116),()=>ShowSaveDetails(slot));
                row.name="Select save slot "+slot;row.GetComponent<Image>().color=new Color32(231,248,240,255);
                var label=row.GetComponentInChildren<TMP_Text>();label.fontSize=22;label.alignment=TextAlignmentOptions.MidlineLeft;
                Place(label,24,-6,750,104);saveRows[i]=row;
            }
            savesFeedback=Text(p,"",18,deepSea,new Vector2(76,-586),new Vector2(798,70),TextAlignmentOptions.Center);
            savesPanel.SetActive(false);
            saveDetailsPanel=ModalCard("Save slot detail",new Vector2(850,490),"存档详情","").gameObject;
            var d=Skin(saveDetailsPanel,new Vector2(850,490),"");Shape(d,"Save detail background",0,0,850,490,aqua,36);
            saveDetails=Text(d,"",24,deepSea,new Vector2(56,-56),new Vector2(738,260),TextAlignmentOptions.Center);
            saveCurrent=SeaButton(d,"保存到这里",new Vector2(42,-340),new Vector2(180,54),()=>{
                var info=game.GetSaveSlotInfo(selectedSaveSlot);
                if(info.exists)AskConfirmation("覆盖槽位 "+selectedSaveSlot+"？该槽位原进度将被替换。",SaveSelectedSlot);
                else SaveSelectedSlot();
            });saveCurrent.name="Save selected slot";
            loadCurrent=SeaButton(d,"读取",new Vector2(238,-340),new Vector2(180,54),()=>AskConfirmation("读取槽位 "+selectedSaveSlot+"？当前未保存的进度会被替换。",()=>{if(!game.LoadSession(selectedSaveSlot)){Open(saveDetailsPanel);RefreshSaveDetail();}}));loadCurrent.name="Load selected slot";
            deleteCurrent=SeaButton(d,"删除",new Vector2(434,-340),new Vector2(180,54),()=>AskConfirmation("确定删除槽位 "+selectedSaveSlot+"？"+(game.GetSaveSlotInfo(selectedSaveSlot).legacy?"这里显示的是原有存档，删除后将不再从旧存档继续。":"只删除所选存档，当前游戏不变。"),()=>{game.DeleteSession(selectedSaveSlot);Open(savesPanel);RefreshSaveSlots();}));deleteCurrent.name="Delete selected slot";
            SeaButton(d,"取消",new Vector2(630,-340),new Vector2(180,54),()=>{Open(savesPanel);RefreshSaveSlots();});
            saveDetailsPanel.SetActive(false);
            foreach(var page in new[]{savesPanel,saveDetailsPanel})foreach(var b in page.GetComponentsInChildren<Button>(true))if(b.name.StartsWith("Close "))b.transform.SetAsLastSibling();
        }
        void OpenSaveSlots(GameObject origin,bool saving)
        {saveReturnPanel=origin;if(!savesPanel.activeInHierarchy)Open(savesPanel);RefreshSaveSlots();savesFeedback.text=saving?"选择槽位保存，或打开已有存档读取。":"选择一个已有存档继续旅程。";}
        void ShowSaveDetails(int slot){selectedSaveSlot=slot;Open(saveDetailsPanel);RefreshSaveDetail();}
        static string SlotDescription(HabitatGame.SaveSlotInfo info)
        {
            if(!info.exists)return "空槽位";
            if(!info.readable)return "存档不可读取\n"+info.error;
            var duration=TimeSpan.FromSeconds(info.elapsedSeconds);
            return info.mapName+" · 第 "+info.day+" 天"+"\n游戏时长 "+(int)duration.TotalHours+" 小时 "+duration.Minutes+" 分钟 · "+info.savedAt+(info.legacy?" · 原有存档":"");
        }
        void RefreshSaveSlots()
        {
            for(int i=0;i<3;i++)saveRows[i].GetComponentInChildren<TMP_Text>().text="槽位 "+(i+1)+"    "+SlotDescription(game.GetSaveSlotInfo(i+1));
            savesFeedback.text=game.State.message;
        }
        void RefreshSaveDetail()
        {
            var info=game.GetSaveSlotInfo(selectedSaveSlot);
            saveDetails.text="槽位 "+selectedSaveSlot+"\n\n"+SlotDescription(info);
            saveCurrent.interactable=game.SessionStarted&&!game.Busy&&!game.StoryPlaying;
            loadCurrent.interactable=info.readable;deleteCurrent.interactable=info.exists;
        }
        void SaveSelectedSlot(){game.SaveSession(selectedSaveSlot);Open(savesPanel);RefreshSaveSlots();}

        void BuildCompletionChoices()
        {
            completionChoicesPanel=ModalCard("Completion routes",new Vector2(900,500),"接下来","").gameObject;
            var p=Skin(completionChoicesPanel,new Vector2(900,500),"");Shape(p,"Route background",0,0,900,500,aqua,42);
            Heading(p,"CompletionRoutesHeading","接下来的旅程",220,-28,460);
            completionMessage=Text(p,"",23,deepSea,new Vector2(75,-145),new Vector2(750,154),TextAlignmentOptions.Center);
            summaryNext=SeaButton(p,"准备下一关",new Vector2(65,-350),new Vector2(240,58),()=>game.PrepareNextCampaignIsland());summaryNext.name="Summary prepare next chapter";
            SeaButton(p,"重试本关",new Vector2(330,-350),new Vector2(240,58),()=>AskConfirmation("重开本关地形和居民？共享装备、背包与其他岛屿进度保留。",()=>game.RestartCurrentLevel())).name="Summary retry level";
            SeaButton(p,"回码头",new Vector2(595,-350),new Vector2(240,58),()=>game.ReturnToHarbor()).name="Summary return to harbor";
            completionChoicesPanel.SetActive(false);
        }
        void ConfirmSummary()
        {
            if(!game.State.won){ClosePanelsFromInput();return;}
            ClosePanels();ShowCompletionReminder();
        }

        void BuildBagSelection()
        {
            var r=Box("Inventory item confirmation",inventoryPanel.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(185,0),new Vector2(570,245),new Color32(234,249,241,255),true);
            bagActionPanel=r.gameObject;
            bagActionTitle=Text(r,"",22,deepSea,new Vector2(25,-24),new Vector2(520,112),TextAlignmentOptions.Center);
            SeaButton(r,"手持 / 使用",new Vector2(35,-160),new Vector2(235,52),ConfirmBagSelection).name="Confirm inventory selection";
            SeaButton(r,"取消",new Vector2(300,-160),new Vector2(235,52),()=>bagActionPanel.SetActive(false));
            bagActionPanel.SetActive(false);
        }
        void SelectBagTool(int id)
        {if(!game.HasTool(id)){inventoryFeedback.text="尚未持有该工具，请先取得或购买。";return;}selectedBagTool=id;selectedBagIndex=-1;bagActionTitle.text=HabitatGame.ToolNames[id]+"\n"+ToolDescription(id);bagActionPanel.SetActive(true);bagActionPanel.transform.SetAsLastSibling();}
        void SelectBagItem(int index)
        {if(index<0||index>=game.State.bagProps.Count)return;selectedBagTool=-1;selectedBagIndex=index;bagActionTitle.text=PropName(game.State.bagProps[index])+"\n确认后从背包取到手中。";bagActionPanel.SetActive(true);bagActionPanel.transform.SetAsLastSibling();}
        void ConfirmBagSelection()
        {
            if(!bagActionPanel.activeSelf||!InventoryOpen)return;
            bagActionPanel.SetActive(false);
            if(selectedBagTool>=0)game.SelectInventoryTool(selectedBagTool);
            else if(selectedBagIndex>=0)game.TakeInventoryItem(selectedBagIndex);
            selectedBagTool=selectedBagIndex=-1;
        }
        static string ToolDescription(int id)
        {
            return new[]{"E 交互或推单个箱子；Ctrl + 后退方向拉箱，低处箱子会收网打捞。","把持有的土块放到合适地块。","使用鱼竿引导水中的动物。","放置虾笼，收集可收获的虾。","在满足条件的水边开始钓鱼。","一次性挖土：预览有效后才消耗。","选择对象、方向和落点，再确认牵引。","面向发射方向，预览落点后发射。"}[Mathf.Clamp(id,0,7)];
        }

        void BuildContextPrompt()
        {
            var r=Box("Nearby action prompt",gameplayInterface.transform,new Vector2(.5f,.5f),new Vector2(0,0),Vector2.zero,new Vector2(180,104),Color.clear,true);
            contextPanel=r.gameObject;var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();
            b.onClick.AddListener(()=>{if(!game.Busy&&!game.StoryPlaying&&!ModalOpen)game.ApplyTool();});
            HabitatFinalUi.Art(r,"R54",Vector2.zero,new Vector2(144,77));
            var caption=Shape(r,"Interaction key caption",8,-78,172,26,new Color32(14,79,99,235),13);
            contextText=Text(caption,"",14,Color.white,Vector2.zero,new Vector2(172,26),TextAlignmentOptions.Center,true);
            contextPanel.SetActive(false);
        }
        void RefreshPlanHud()
        {
            // Art page 3 has a mission ribbon, menu and transient prompt; old debugging cards are not part of this HUD.
            foreach(var name in new[]{"Island header","Observation controls","Open inventory"}){var t=gameplayInterface.transform.Find(name);if(t)t.gameObject.SetActive(false);}
            foreach(var p in new[]{tutorialFieldNote?.gameObject,tutorialPanel,chapterPanel,tutorialHandbook?.gameObject,tutorialToolsToggle?.gameObject,ecologyPanel,toolsPanel,inspectorPanel,eventPanel,nearbyPromptPanel})if(p)p.SetActive(false);
            string label=ContextActionLabel();contextPanel.SetActive(!string.IsNullOrEmpty(label)&&!game.Busy&&!game.StoryPlaying&&!ModalOpen);
            if(contextText.text!=label)
            {
                contextText.text=label;
                // Keep the smaller artwork without clipping longer two-line action labels.
                float height=Mathf.Max(26,Mathf.Ceil(contextText.GetPreferredValues(label??"",172,Mathf.Infinity).y)+6);
                contextText.rectTransform.sizeDelta=new Vector2(172,height);
                ((RectTransform)contextText.transform.parent).sizeDelta=new Vector2(172,height);
                ((RectTransform)contextPanel.transform).sizeDelta=new Vector2(180,78+height);
            }
            if(contextPanel.activeSelf)PositionContextPrompt();
        }
        void PositionContextPrompt()
        {
            var camera=Camera.main;if(!camera)return;
            var s=game.State;game.GetTarget(out int x,out int z);
            if(game.TryNearbyCatch(out int fishX,out int fishZ)){x=fishX;z=fishZ;}
            Vector3 world=s.onHarbor?game.View.PlayerWorldPosition+Vector3.up*.7f:new Vector3(x,Mathf.Max(s.waterLevel+1,HabitatRules.Height(s,x,z))*.7f+.6f,z);
            Vector3 screen=camera.WorldToScreenPoint(world);
            if(screen.z<=0){contextPanel.SetActive(false);return;}
            var parent=(RectTransform)gameplayInterface.transform;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,screen,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out Vector2 point))
            {
                var r=(RectTransform)contextPanel.transform;point+=new Vector2(-16,9);
                point.x=Mathf.Clamp(point.x,parent.rect.xMin+8,parent.rect.xMax-r.sizeDelta.x-8);
                point.y=Mathf.Clamp(point.y,parent.rect.yMin+38,parent.rect.yMax-r.sizeDelta.y-80);
                r.anchoredPosition=point;
            }
        }
        string ContextActionLabel()
        {
            var s=game.State;
            if(s.onHarbor)
            {
                var nearby=game.View.Harbor.NearestInteraction(s);
                return nearby==HabitatDockInteraction.Home?"E / 点击 · 回家休息":nearby==HabitatDockInteraction.Shop?"E / 点击 · 与老板交谈":nearby==HabitatDockInteraction.Boat?(HabitatLevels.IsHomeHarbor(s)?"E / 点击 · 选择目的地":"E / 点击 · 返航"):null;
            }
            game.GetTarget(out int x,out int z);
            if(!s.onHarbor&&game.tool==0&&HabitatRules.RaftDirection(HabitatRules.TileAt(s,s.playerX,s.playerZ),x-s.playerX,z-s.playerZ)&&HabitatRules.IsWater(s,x,z))return "方向键 / E · 搭乘竹排（可往返）";
            if(HabitatRules.TileAt(s,x,z)?.prop==PropKind.ClimbBlock)return "固定台阶 · Shift + 方向攀爬";
            if(HabitatRules.TileAt(s,x,z)?.prop==PropKind.Raft)return "走上竹排 · 沿水纹方向往返";
            if(game.TryNearbyCatch(out int fishX,out int fishZ))return "E / 点击 · "+(s.animals.Exists(a=>a.x==fishX&&a.z==fishZ&&a.species==Species.Jellyfish)?"钓起水母":"钓起小鱼");
            if(!HabitatRules.Inside(s,x,z))return null;
            var tile=HabitatRules.TileAt(s,x,z);var copy=HabitatRules.Clone(s);bool valid=false;
            switch(game.tool)
            {
                case 0:
                    if(HabitatRules.IsBox(tile.prop)&&s.carryingProp==PropKind.None&&s.carryingAnimal<0)
                        return Mathf.Abs(x-s.playerX)+Mathf.Abs(z-s.playerZ)==1?"E / 点击 · 推箱子":null;
                    valid=HabitatRules.Interact(copy,x,z);
                    if(!valid)return null;
                    return "E / 点击 · "+(HabitatRules.IsRecoveryTarget(s,x,z)?"打捞箱子":s.carryingAnimal>=0||s.carryingProp!=PropKind.None?"放下":tile.prop==PropKind.Chest?"打开宝箱":tile.prop==PropKind.Shell?"拾取蟹壳":"抱起 / 交互");
                case 1:valid=HabitatRules.PlaceSoil(copy,x,z);break;
                case 2:valid=HabitatRules.Lure(copy,x,z);break;
                case 3:valid=HabitatRules.PlaceTrap(copy,x,z);break;
                case 4:valid=HabitatRules.Fish(copy,x,z);break;
                default:game.PreviewUtility(x,z,s.facingX,s.facingZ,out valid);break;
            }
            return valid?"E / 点击 · "+HabitatGame.ToolNames[game.tool]:null;
        }

        void RefreshPlanInterface()
        {
            if(Time.unscaledTime>=nextSlotRefresh)
            {
                nextSlotRefresh=Time.unscaledTime+1;
                if(savesPanel&&savesPanel.activeInHierarchy)RefreshSaveSlots();
            }
            RefreshAutomaticGuide();
        }

        sealed class GuidePage
        {
            public readonly string key,title,body,icon;public readonly int demo;
            public GuidePage(string key,string title,string body,int demo=-1,string icon=null){this.key=key;this.title=title;this.body=body;this.demo=demo;this.icon=icon;}
        }
        readonly GuidePage[] GuidePages={
            new GuidePage("move","移动与推箱","WASD / 方向键移动；Z 撤销；空格暂停/继续生态。人物到达目标格后才结算。",0),
            new GuidePage("pull","推箱、后拉与海上铺路","箱子不能抱起；方向键或 E 推单个箱子，Ctrl + 后退方向拉箱。浮箱可支撑推箱铺路，低处打捞需留出退路。",1),
            new GuidePage("climb","指定方块攀爬","仅可对指定攀爬方块按 Shift + 方向，上攀最多两层。普通地块不能向上攀爬。",2),
            new GuidePage("rod","鱼竿与垂钓","取得鱼竿，让本岛动物全部安居后，靠近小鱼点击提示或按 E 钓鱼。第二条幼鱼需按空格继续生态等待；水母可在 3 格内牵引。",3,"Rod"),
            new GuidePage("basket","背篓与龙虾","取得背篓后才可以携带龙虾。携虾靠近其他动物，引导它们调整位置。",-1,"Basket"),
            new GuidePage("fan","风箱与漂浮板","面向风箱按 R 调整方向。风推动漂浮板，人物也能站在板上随它移动。",-1,"Bellows"),
            new GuidePage("crab","寄居蟹的家","把寄居蟹引导到邻水陆地；地图有水生动物时，它的 3×3 范围还需要水中金鱼或水母。",-1,"Crab"),
            new GuidePage("jellyfish","水母与外海","水母需要连通外海。新正式关使用鱼竿可在 3 格内引导；金鱼则需要围住的水域。",-1,"Jellyfish"),
            new GuidePage("tide","潮汐机关","将箱子压在潮汐机关上，让海面与漂浮板升高；留意高低变化和回程路线。"),
            new GuidePage("shovel","一次性铲子","在背包选择铲子，面向目标按 E。先检查预览，再确认挖土；取消不消耗。",-1,"Shovel"),
            new GuidePage("magnet","磁铁","选中目标，再选择方向。确认前检查落点；取消不消耗道具。",-1,"Magnet"),
            new GuidePage("launcher","发射器","面向发射方向，按 E 查看对象和落点。确认后等动画完成才结算，取消不消耗。",-1,"Cannon")
        };
        void RefreshPlanGuide()
        {
            guideIndex=Mathf.Clamp(guideIndex,0,GuidePages.Length-1);var page=GuidePages[guideIndex];
            guideTitle.text=page.title;guideText.text=page.body;guidePager.text=(guideIndex+1)+" / "+GuidePages.Length;
            guideDemo.gameObject.SetActive(true);guideDemo.Mode=page.demo;
            if(guideItemImage)
            {
                guideItemImage.texture=string.IsNullOrEmpty(page.icon)?null:HabitatUiAssets.IconTexture(page.icon);
                guideItemImage.enabled=page.demo<0&&guideItemImage.texture;
                guideFallbackText.gameObject.SetActive(page.demo<0&&!guideItemImage.texture);
                guideFallbackText.text=page.key=="tide"?"箱子压下机关\n↓\n海面与漂浮板升高一格":page.title;
            }
        }
        void AdvanceGuide(){guideIndex=(guideIndex+1)%GuidePages.Length;RefreshGuide();}
        void RefreshAutomaticGuide()
        {
            if(!game.SessionStarted||game.Busy||game.StoryPlaying||ModalOpen||game.State.onHarbor||game.State.won||Time.unscaledTime<nextGuideAt)return;
            var s=game.State;if(!s.authoredLevel||(s.levelIndex<5||s.levelIndex>8)&&s.levelIndex!=10)return;
            s.seenGuideKeys??=new System.Collections.Generic.List<string>();
            bool near(PropKind p)=>s.tiles.Where((tile,index)=>Mathf.Abs(index%s.width-s.playerX)<=2&&Mathf.Abs(index/s.width-s.playerZ)<=2).Any(t=>t.prop==p);
            bool animal(Species type)=>s.animals.Any(a=>!a.carried&&a.species==type&&Mathf.Abs(a.x-s.playerX)<=3&&Mathf.Abs(a.z-s.playerZ)<=3);
            bool[] eligible={true,near(PropKind.Crate),near(PropKind.ClimbBlock),s.rod,s.backBasketUnlocked,near(PropKind.Fan),animal(Species.Crab),animal(Species.Jellyfish),near(PropKind.Tide),s.shovels>0,s.magnets>0,s.launchers>0};
            for(int i=0;i<GuidePages.Length;i++)if(eligible[i]&&!s.seenGuideKeys.Contains(GuidePages[i].key))
            {
                s.seenGuideKeys.Add(GuidePages[i].key);guideIndex=i;automaticGuide=true;batchReturnPanel=null;
                Open(guidePanel);RefreshGuide();nextGuideAt=Time.unscaledTime+8;break;
            }
        }
    }
}
