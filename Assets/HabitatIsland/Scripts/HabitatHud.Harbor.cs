using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HabitatIsland
{
    public partial class HabitatHud
    {
        GameObject homePanel, nearbyPromptPanel, storyPanel;
        TMP_Text homeDay, homeInventory, homeHint, homeRestLabel, nearbyPrompt;
        TMP_Text storyCaption, storyState, storyPauseLabel, mapProgressNote, sellTitle, sellCost;
        TMP_Text fanShopTitle, fanShopCost;
        Button homeRestButton, sellButton, fanShopButton;
        string mapTravelFeedback;
        int mapFeedbackSelection=-1;
        public bool HomeOpen => ModalOpen && homePanel && homePanel.activeSelf;

        void BuildNearbyPrompt(Transform parent)
        {
            var card=Card("Nearby harbor interaction",parent,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-176),new Vector2(440,44));
            nearbyPromptPanel=card.gameObject;
            nearbyPrompt=Text(card,"",17,ink,new Vector2(18,-5),new Vector2(404,34),TextAlignmentOptions.Center,true);
            nearbyPromptPanel.SetActive(false);
        }

        void BuildHomePanel()
        {
            homePanel=ModalCard("Home",new Vector2(680,442),"家 · 休息","留住今天的收获，等窗台亮起明天的晨光。").gameObject;
            homeDay=Text(homePanel.transform,"",24,ink,new Vector2(28,-132),new Vector2(624,40),emphasis:true);
            homeInventory=Text(homePanel.transform,"",18,ink,new Vector2(28,-187),new Vector2(624,70));
            homeHint=Text(homePanel.transform,"",16,muted,new Vector2(28,-276),new Vector2(624,55));
            homeRestButton=Button(homePanel.transform,"",new Vector2(28,-362),new Vector2(380,48),()=>game.RestAtHome(),primary:true);
            homeRestButton.name="Rest until tomorrow";
            homeRestLabel=homeRestButton.GetComponentInChildren<TMP_Text>();
            Button(homePanel.transform,"继续今天  Esc",new Vector2(424,-362),new Vector2(228,48),BackToGame,cue:null);
        }

        public void OpenHome()
        {
            if(game.Busy||game.StoryPlaying||HomeOpen)return;
            if(game.View==null||!game.View.Harbor.NearHome(game.State))
            {game.State.message="请先走到码头家门口，再按 E 休息。";return;}
            // The nearby E/click bubble is the confirmation; no second modal.
            game.RestAtHome();
        }

        void RefreshHome()
        {
            var s=game.State;
            int day=Mathf.Max(1,s.day);
            bool nearHome=game.View!=null&&game.View.Harbor.NearHome(s);
            homeDay.text=$"今天是第 {day} 天";
            homeInventory.text=$"保留渔获：鱼 {s.fish} 条 · 虾 {s.shrimp} 只 · 琉璃鱼 {s.glassFish} 条\n贝币 {s.coins} · 土块库存 {s.cargo}";
            homeHint.text=nearHome?"休息不会卖出渔获。背包、贝币和岛屿探索进度会保留。":"请回到家门口，才能休息到明天。";
            homeRestLabel.text=$"休息到第 {day+1} 天";
            homeRestButton.interactable=nearHome&&!game.Busy&&!game.StoryPlaying;
        }

        void RefreshHarborControls()
        {
            if(HomeOpen)RefreshHome();
            if(MapSelectionOpen)RefreshMapTravelNote();
            HabitatDockInteraction nearby=game.View!=null?game.View.Harbor.NearestInteraction(game.State):HabitatDockInteraction.None;
            bool showNearby=!game.Busy&&!game.StoryPlaying&&!ModalOpen&&nearby!=HabitatDockInteraction.None;
            nearbyPromptPanel.SetActive(showNearby);
            if(showNearby)
                nearbyPrompt.text=nearby==HabitatDockInteraction.Home?"E  家 · 休息到明天"
                    :nearby==HabitatDockInteraction.Shop?"E  商店 · 与老板交谈 / 交易":(HabitatLevels.IsHomeHarbor(game.State)?"E  小船 · 选择目的地":"E  小船 · 返航至家园港口");
        }

        void ConfirmMapTravel(int index)
        {
            if(game.Busy||game.StoryPlaying)return;
            if(!game.CanTravelToLevel(index,out string reason))
            {mapTravelFeedback=reason;mapFeedbackSelection=index;RefreshMapTravelNote();HabitatAudio.Play(HabitatAudioCue.Error);return;}
            game.SailToLevel(index);
            if(MapSelectionOpen)
            {
                mapTravelFeedback=game.State.message;mapFeedbackSelection=mapSelection.SelectedLevel;
                RefreshMapTravelNote();
            }
        }

        void RefreshMapTravelNote()
        {
            if(!mapProgressNote)return;
            if(mapFeedbackSelection!=mapSelection.SelectedLevel)
            {mapTravelFeedback=null;mapFeedbackSelection=mapSelection.SelectedLevel;}
            bool feedback=!string.IsNullOrEmpty(mapTravelFeedback);
            mapProgressNote.color=feedback?new Color32(255,205,169,255):Color.white;
            if(!game.CanTravelToLevel(mapSelection.SelectedLevel,out string lockedReason))
            {mapProgressNote.text=feedback?mapTravelFeedback:lockedReason;return;}
            string visit=game.HasVisitedLevel(mapSelection.SelectedLevel)?"已到访 · 继续上次的岛屿进度":"首次前往 · 开始探索这座岛屿";
            mapProgressNote.text=feedback?mapTravelFeedback:(mapSelection.SelectedLevel==0?"可选生态练习 · 学习搬运与安居\n":"")+visit;
        }

        void RefreshShopDelivery()
        {
            var s=game.State;
            int price=HabitatRules.CatchSaleValue(s);
            bool nearShop=game.View!=null&&(shopSelling?game.View.Harbor.NearShop(s):game.CanPurchaseHere);
            bool formal=HabitatChapterRules.IsCampaignShop(s);
            for(int i=0;i<shopEntries.Count;i++)
            {
                string item=shopItems[i];bool visible=shopSelling?i<saleSpecies.Length:!formal||i<5;
                shopEntries[i].gameObject.SetActive(visible);
                bool allowed=shopSelling?i<saleSpecies.Length&&HabitatRules.CatchCount(s,saleSpecies[i])>0:item=="sell"?price>0:HabitatChapterRules.CanBuyItem(s,item,out _);
                shopEntries[i].interactable=allowed&&nearShop&&!game.Busy&&!game.StoryPlaying;
                shopTitles[i].text=shopSelling&&i<saleNames.Length?saleNames[i]:shopNames[i];
                string icon=shopSelling&&i<saleIcons.Length?saleIcons[i]:shopIcons[i];shopImages[i].texture=icon==null?null:HabitatUiAssets.IconTexture(icon);shopImages[i].enabled=shopImages[i].texture;shopFallbacks[i].SetActive(!shopImages[i].texture);
                shopPrices[i].text=shopSelling&&i<saleSpecies.Length?(formal?HabitatRules.CatchSellPrice(saleSpecies[i]):saleSpecies[i]==Species.Goldfish?8:saleSpecies[i]==Species.Shrimp?5:HabitatRules.CatchSellPrice(saleSpecies[i]))+" 贝币 · 持有 "+HabitatRules.CatchCount(s,saleSpecies[i]):ShopPriceText(item);
                if(!shopSelling&&item!="sell"&&!HabitatChapterRules.CanBuyItem(s,item,out string reason))shopPrices[i].text=reason;
                RefreshFinalShopCard(i);
                shopPrices[i].enableAutoSizing=true;shopPrices[i].fontSizeMin=11;shopPrices[i].fontSizeMax=15;
            }

            string feedback=game.RemoteMerchantAvailable?(shopSelling?"通话只提供补给购买；出售渔获请返航。":"商人通话中：工具入背包，木箱入船舱，到岛屿渡口卸货。"):!nearShop?"请走到码头商店旁交易。":price==0?"还没有可以出售的收获。":"出售可获得 "+price+" 贝币。";
            shopMessage.text=(shopSelling?"出售 · 点选物品并确认数量\n":"买入 · 点选商品并确认数量\n")+"鱼 "+s.fish+" · 虾 "+s.shrimp+" · 蟹壳 "+s.shells+" · 水母 "+s.jellyfish
                +"\n委托琉璃鱼 "+s.glassFish+" 条：与老板交谈后交付。\n"+feedback
                +(string.IsNullOrEmpty(s.message)||s.message==s.objective?"":"\n"+s.message);
        }

        GameObject storyTask;
        void BuildStoryControls()
        {
            var root=Rect("Main story controls",canvas.transform,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);
            root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            storyPanel=root.gameObject;
            var layer=storyPanel.AddComponent<Canvas>();layer.overrideSorting=true;layer.sortingOrder=110;
            storyPanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var task=Rect("Story task prompt",root,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-22),new Vector2(760,100));
            storyTask=task.gameObject;
            HabitatFinalUi.Art(task,"R20",Vector2.zero,new Vector2(760,100));
            storyCaption=Text(task,"",19,Color.white,new Vector2(36,-22),new Vector2(688,60),TextAlignmentOptions.Center,true);
            var controls=Rect("Story playback buttons",root,new Vector2(1,0),new Vector2(1,0),new Vector2(-24,74),new Vector2(238,42));
            var pause=Button(controls,"暂停",Vector2.zero,new Vector2(110,38),()=>
            {if(game.Story&&game.Story.IsPlaying)game.Story.TogglePause();RefreshStoryControls();},storyControl:true);
            pause.name="Pause main story";storyPauseLabel=pause.GetComponentInChildren<TMP_Text>();storyPauseLabel.fontSize=14;
            var skip=Button(controls,"跳过",new Vector2(122,0),new Vector2(110,38),()=>
            {if(game.Story&&game.Story.IsPlaying)game.Story.Skip();RefreshStoryControls();nextRefresh=0;},storyControl:true);
            skip.name="Skip main story";skip.GetComponentInChildren<TMP_Text>().fontSize=14;
            storyPanel.SetActive(false);
        }

        bool RefreshStoryControls()
        {
            bool playing=game.StoryPlaying;
            if(gameplayInterface)gameplayInterface.SetActive(!playing&&!ModalOpen&&!(game.Fishing&&game.Fishing.IsOpen));
            if(storyPanel)storyPanel.SetActive(playing);
            if(!playing||game.Story==null)return false;
            storyCaption.text=game.Story.CurrentCaption;
            storyTask.SetActive(!game.Story.IsCgPlaying || !string.IsNullOrEmpty(game.Story.CurrentCaption));
            storyPauseLabel.text=game.Story.IsPaused?"继续":"暂停";
            return true;
        }
    }
}
