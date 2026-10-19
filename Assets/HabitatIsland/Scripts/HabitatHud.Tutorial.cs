using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HabitatIsland
{
    public partial class HabitatHud
    {
        RectTransform tutorialFieldNote, tutorialHandbook, tutorialToolsToggle;
        GameObject tutorialPanel;
        TMP_Text tutorialProgress, tutorialTitle, tutorialBody, tutorialPrimaryLabel, tutorialDismissLabel;
        Button tutorialPrimary, tutorialTarget, tutorialDismiss;
        bool tutorialOffer;

        public bool TutorialCardVisible => tutorialPanel && tutorialPanel.activeInHierarchy;

        void BuildTutorialControls(Transform gameplayRoot)
        {
            // Replace the field note while teaching; the lower left controls move with it.
            var card=Card("Tutorial field note",gameplayRoot,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-108),new Vector2(380,280));
            tutorialPanel=card.gameObject;
            tutorialProgress=Text(card,"",13,mint,new Vector2(20,-16),new Vector2(340,24),emphasis:true);
            tutorialTitle=Text(card,"",21,ink,new Vector2(20,-48),new Vector2(340,34),emphasis:true);
            tutorialBody=Text(card,"",16,muted,new Vector2(20,-90),new Vector2(340,102));
            tutorialBody.enableAutoSizing=true;tutorialBody.fontSizeMin=14;tutorialBody.fontSizeMax=16;
            tutorialPrimary=Button(card,"",new Vector2(20,-202),new Vector2(166,38),TutorialPrimaryAction,primary:true);
            tutorialPrimary.name="Tutorial primary action";
            tutorialPrimaryLabel=tutorialPrimary.GetComponentInChildren<TMP_Text>();
            tutorialTarget=Button(card,"找到目标",new Vector2(194,-202),new Vector2(166,38),()=>{game.RevealTutorialTarget();nextRefresh=0;});
            tutorialTarget.name="Reveal tutorial target";
            tutorialDismiss=Button(card,"",new Vector2(20,-246),new Vector2(340,24),()=>{game.DismissTutorial();nextRefresh=0;},paper);
            tutorialDismiss.name="Dismiss tutorial guide";
            tutorialDismissLabel=tutorialDismiss.GetComponentInChildren<TMP_Text>();
            tutorialDismissLabel.fontSize=13;
            tutorialPanel.SetActive(false);
        }

        void TutorialPrimaryAction()
        {
            if(game.Busy||game.StoryPlaying)return;
            if(tutorialOffer)game.OpenTutorialDestination(0);
            else if(game.TutorialFinished)game.OpenTutorialDestination(HabitatFigJamLevel.Index);
            else if(game.TutorialCanResume)game.ResumeTutorial();
            nextRefresh=0;
        }

        void RefreshTutorialControls()
        {
            if(!tutorialPanel)return;
            var state=game.State;
            tutorialOffer=state.levelIndex==HabitatFigJamLevel.Index&&state.tutorialOfferPending&&!state.tutorialCompleted;
            bool show=(tutorialOffer||game.TutorialActive)&&!game.StoryPlaying&&!game.ChapterActive;
            tutorialPanel.SetActive(show);
            tutorialFieldNote.gameObject.SetActive(!show);
            SetGuideControlOffset(show?62f:0f);
            if(!show)return;

            bool finished=!tutorialOffer&&game.TutorialFinished;
            tutorialProgress.text=tutorialOffer?"新手教学 · 推荐":game.TutorialProgress;
            tutorialTitle.text=tutorialOffer?"先认识小岛，再出发":game.TutorialTitle;
            tutorialBody.text=tutorialOffer?"学习移动、搬运和安居条件。\n当前地图与背包进度会保留。\n选择教学地图后，确认出发即可。":game.TutorialBody;
            bool primary=tutorialOffer||finished||game.TutorialCanResume;
            bool targetAvailable=!tutorialOffer&&!finished&&game.TutorialAvailableTarget;
            tutorialPrimary.gameObject.SetActive(primary);
            tutorialTarget.gameObject.SetActive(targetAvailable);
            tutorialPrimaryLabel.text=tutorialOffer?"开始教学":finished?"返回正式岛":"继续生态模拟";
            tutorialDismissLabel.text=tutorialOffer?"稍后再学":finished?"继续自由练习":"收起提示 · 可在帮助中恢复";
            var primaryRect=tutorialPrimary.GetComponent<RectTransform>();
            primaryRect.sizeDelta=new Vector2(targetAvailable?166:340,38);
            var targetRect=tutorialTarget.GetComponent<RectTransform>();
            targetRect.anchoredPosition=new Vector2(primary?194:20,-202);
            targetRect.sizeDelta=new Vector2(primary?166:340,38);
            bool available=!game.Busy&&!ModalOpen;
            tutorialPrimary.interactable=available;
            tutorialTarget.interactable=available;
            tutorialDismiss.interactable=available;
        }

        void SetGuideControlOffset(float offset)
        {
            tutorialHandbook.anchoredPosition=new Vector2(24,-338-offset);
            tutorialToolsToggle.anchoredPosition=new Vector2(24,-392-offset);
            ecologyPanel.GetComponent<RectTransform>().anchoredPosition=new Vector2(24,-446-offset);
            toolsPanel.GetComponent<RectTransform>().anchoredPosition=new Vector2(24,-446-offset);
        }
    }
}
