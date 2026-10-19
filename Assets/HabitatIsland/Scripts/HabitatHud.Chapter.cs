using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HabitatIsland
{
    public partial class HabitatHud
    {
        GameObject chapterPanel, chapterDialoguePanel;
        TMP_Text chapterTitle, chapterBody, chapterProgressLabel;
        TMP_Text chapterSpeaker, chapterDialogueBody, chapterDialoguePage, chapterDialogueNextLabel;
        Image chapterProgressFill;
        Button chapterTarget, chapterResume, chapterDialogueNext;
        string[] chapterDialogueLines;
        string chapterDialogueFinalLabel;
        int chapterDialogueIndex;
        Action chapterDialogueCompleted;

        public bool ChapterCardVisible => chapterPanel && chapterPanel.activeInHierarchy;
        public bool ChapterDialogueOpen => ModalOpen && chapterDialoguePanel && chapterDialoguePanel.activeSelf;
        public bool ShopkeeperSpeaking => ChapterDialogueOpen && chapterSpeaker && chapterSpeaker.text == "商店老板";
        public int ChapterDialoguePage => chapterDialogueIndex;

        void BuildChapterControls(Transform parent)
        {
            var card=Card("Chapter field note",parent,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-108),new Vector2(380,280));
            chapterPanel=card.gameObject;
            chapterProgressLabel=Text(card,"航海任务",13,mint,new Vector2(20,-16),new Vector2(340,24),emphasis:true);
            chapterTitle=Text(card,"",21,ink,new Vector2(20,-48),new Vector2(340,34),emphasis:true);
            chapterBody=Text(card,"",16,muted,new Vector2(20,-90),new Vector2(340,116));
            chapterBody.enableAutoSizing=true;chapterBody.fontSizeMin=14;chapterBody.fontSizeMax=16;
            chapterTarget=Button(card,"找到目标",new Vector2(20,-218),new Vector2(166,38),()=>{game.RevealChapterTarget();nextRefresh=0;});
            chapterTarget.name="Reveal chapter target";
            chapterResume=Button(card,"继续生态模拟",new Vector2(194,-218),new Vector2(166,38),()=>{game.ResumeChapterEcology();nextRefresh=0;},primary:true);
            chapterResume.name="Resume chapter ecology";
            var track=Box("Chapter progress track",card,new Vector2(0,0),Vector2.zero,new Vector2(20,12),new Vector2(340,3),surface);
            chapterProgressFill=Box("Chapter progress",track,new Vector2(0,.5f),new Vector2(0,.5f),Vector2.zero,new Vector2(0,3),mint).GetComponent<Image>();
            chapterPanel.SetActive(false);
        }

        void RefreshChapterControls()
        {
            if(!chapterPanel)return;
            bool show=game.ChapterActive&&!game.StoryPlaying;
            chapterPanel.SetActive(show);
            if(!show)return;
            tutorialFieldNote.gameObject.SetActive(false);
            if(tutorialPanel)tutorialPanel.SetActive(false);
            SetGuideControlOffset(62f);
            chapterTitle.text=game.ChapterTitle;
            chapterBody.text=game.ChapterBody;
            chapterProgressFill.rectTransform.sizeDelta=new Vector2(340*Mathf.Clamp01(game.ChapterProgress),3);
            bool targetAvailable=game.ChapterTargetAvailable,resumeAvailable=game.ChapterCanResume;
            chapterTarget.gameObject.SetActive(targetAvailable);
            chapterResume.gameObject.SetActive(resumeAvailable);
            chapterTarget.GetComponent<RectTransform>().sizeDelta=new Vector2(resumeAvailable?166:340,38);
            var resumeRect=chapterResume.GetComponent<RectTransform>();
            resumeRect.anchoredPosition=new Vector2(targetAvailable?194:20,-218);
            resumeRect.sizeDelta=new Vector2(targetAvailable?166:340,38);
            chapterTarget.interactable=chapterResume.interactable=!game.Busy&&!ModalOpen;
        }

        void BuildChapterDialogue()
        {
            var card=ModalCard("Chapter dialogue",new Vector2(900,380),"码头商店","与老板交谈，接取或交付委托。");
            chapterDialoguePanel=card.gameObject;
            var portrait=Box("Shopkeeper portrait",card,new Vector2(0,1),new Vector2(0,1),new Vector2(28,-132),new Vector2(124,136),new Color32(218,231,218,255));
            var portraitSprite=Resources.Load<Sprite>("Story/ShopkeeperPortrait");
            if(portraitSprite)
            {
                var portraitArt=Rect("Shopkeeper portrait art",portrait,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-8),new Vector2(108,85));
                var portraitImage=portraitArt.gameObject.AddComponent<Image>();
                portraitImage.sprite=portraitSprite;portraitImage.preserveAspect=true;portraitImage.raycastTarget=false;
            }
            else Text(portrait,"店",48,mint,new Vector2(0,-8),new Vector2(124,85),TextAlignmentOptions.Center,true);
            Text(portrait,"商店老板",15,ink,new Vector2(6,-97),new Vector2(112,26),TextAlignmentOptions.Center,true);
            chapterSpeaker=Text(card,"",18,mint,new Vector2(180,-130),new Vector2(692,28),emphasis:true);
            chapterDialogueBody=Text(card,"",22,ink,new Vector2(180,-170),new Vector2(692,126));
            chapterDialogueBody.enableAutoSizing=true;chapterDialogueBody.fontSizeMin=18;chapterDialogueBody.fontSizeMax=22;
            chapterDialoguePage=Text(card,"",14,muted,new Vector2(28,-326),new Vector2(150,30),TextAlignmentOptions.MidlineLeft);
            Button(card,"稍后再聊  Esc",new Vector2(180,-318),new Vector2(210,42),BackToGame,cue:null).name="Close chapter dialogue";
            chapterDialogueNext=Button(card,"继续交谈",new Vector2(650,-318),new Vector2(222,42),AdvanceChapterDialogue,primary:true);
            chapterDialogueNext.name="Advance chapter dialogue";
            chapterDialogueNextLabel=chapterDialogueNext.GetComponentInChildren<TMP_Text>();
            chapterDialoguePanel.SetActive(false);
        }

        public void ShowChapterDialogue(string heading,string[] lines,string finalLabel,Action completed)
        {
            if(game.Busy||game.StoryPlaying||lines==null||lines.Length==0)return;
            if(!ChapterDialogueOpen)Open(chapterDialoguePanel);
            chapterSpeaker.text=string.IsNullOrWhiteSpace(heading)?"商店老板":heading;
            chapterDialogueLines=(string[])lines.Clone();
            chapterDialogueIndex=0;
            chapterDialogueFinalLabel=string.IsNullOrWhiteSpace(finalLabel)?"结束交谈":finalLabel;
            chapterDialogueCompleted=completed;
            RefreshChapterDialogue();nextRefresh=0;
        }

        void RefreshChapterDialogue()
        {
            if(chapterDialogueLines==null||chapterDialogueLines.Length==0)return;
            chapterDialogueBody.text=chapterDialogueLines[chapterDialogueIndex]??"";
            chapterDialoguePage.text=$"交谈 {chapterDialogueIndex+1} / {chapterDialogueLines.Length}";
            chapterDialogueNextLabel.text=chapterDialogueIndex==chapterDialogueLines.Length-1?chapterDialogueFinalLabel:"继续交谈";
        }

        public void AdvanceChapterDialogue()
        {
            if(!ChapterDialogueOpen||game.Busy||game.StoryPlaying||chapterDialogueLines==null)return;
            if(chapterDialogueIndex<chapterDialogueLines.Length-1)
            {chapterDialogueIndex++;RefreshChapterDialogue();return;}
            // Only this final deliberate action completes a conversation. Closing,
            // navigating to another panel, or loading a save never invokes it.
            var completed=chapterDialogueCompleted;
            ClosePanels();
            completed?.Invoke();nextRefresh=0;
        }

        void ClearChapterDialogue()
        {
            chapterDialogueCompleted=null;chapterDialogueLines=null;chapterDialogueIndex=0;
            if(chapterDialoguePanel)chapterDialoguePanel.SetActive(false);
        }

        string ResidentName(Species species)
        {
            return HabitatChapterRules.IsMainIsland(game.State)&&species==Species.Goldfish?"琉璃鱼":HabitatRules.Name(species);
        }
    }
}
