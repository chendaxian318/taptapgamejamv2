using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace HabitatIsland
{
    public partial class HabitatHud
    {
        static readonly Color uiBlue=new Color32(36,161,184,255),uiTeal=new Color32(49,184,178,255),uiCoral=new Color32(236,106,110,255);
        TMP_Text deliveredMission;
        bool startSubmenu;
        HabitatMenuEntrance menuEntrance;
        Button DesignedMenuButton(Transform parent,string text,string tag,float y,System.Action action)
        {
            var b=Button(parent,"",new Vector2(-20,y),new Vector2(340,90),action,Color.clear);
            b.name="Menu "+text;
            int index=text=="开始游戏"?0:text=="继续游戏"?1:text=="游戏设置"?2:text=="特别收录"?3:4;
            HabitatFinalUi.ButtonArt(b,"R"+(5+index),"Hover"+(13+index),.8f);
            var colors=b.colors;colors.highlightedColor=new Color(1,.91f,.8f,1);colors.pressedColor=new Color(.8f,.94f,.94f,1);colors.disabledColor=new Color(1,1,1,.42f);b.colors=colors;
            return b;
        }
        void BuildDeliveredStartMenu()
        {
            var panel=Box("Start menu",modal.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,Vector2.zero,Color.white,true);
            panel.anchorMin=Vector2.zero;panel.anchorMax=Vector2.one;panel.offsetMin=panel.offsetMax=Vector2.zero;startPanel=panel.gameObject;
            var layout=Rect("Delivered main menu layout",panel,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(1480,820));
            panel.gameObject.AddComponent<CanvasGroup>();
            DesignedMenuButton(layout,"开始游戏","新的旅程",-54,RequestNewSession).name="Start new journey";
            continueSession=DesignedMenuButton(layout,"继续游戏","欢迎回来",-144,()=>OpenSaveSlots(startPanel,false));
            DesignedMenuButton(layout,"游戏设置","调调声音",-234,()=>OpenAudioSettings(startPanel));
            var collection=DesignedMenuButton(layout,"特别收录","待开放",-324,()=>{});collection.interactable=false;
            foreach(var label in collection.GetComponentsInChildren<TMP_Text>())label.alpha=1;
            DesignedMenuButton(layout,"退出游戏","下次再见",-414,RequestQuit);
            workflowFeedback=Text(layout,"",16,muted,new Vector2(60,-534),new Vector2(340,66));
            workflowFeedback.gameObject.SetActive(false);
            resumeSession=Button(layout,"返回当前游戏",new Vector2(460,-690),new Vector2(220,46),ClosePanelsFromInput,new Color32(232,246,246,255));
            HabitatFinalUi.Art(layout,"R12",new Vector2(460,-736),new Vector2(1020,35));
            var logo=HabitatFinalUi.Art(layout,"R10",new Vector2(10,-614),new Vector2(396,179));
            var ribbon=Rect("Opening ribbons",layout,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(1480,820));
            var ribbonGroup=ribbon.gameObject.AddComponent<CanvasGroup>();
            HabitatFinalUi.Art(ribbon,"R1",new Vector2(0,-360),new Vector2(370,34));
            HabitatFinalUi.Art(ribbon,"R3",new Vector2(1110,-360),new Vector2(370,34));
            menuEntrance=panel.gameObject.AddComponent<HabitatMenuEntrance>();menuEntrance.Configure(layout,logo.rectTransform,ribbonGroup);
        }
        void ApplyDeliveredInterface()
        {
            var header=gameplayInterface.transform.Find("Island header");
            header.GetComponent<Image>().color=Color.clear;header.GetComponent<Image>().raycastTarget=false;
            var shadow=header.GetComponent<Shadow>();if(shadow)shadow.enabled=false;
            foreach(var label in header.GetComponentsInChildren<TMP_Text>())if(label.text=="栖屿")label.gameObject.SetActive(false);
            header.Find("Brand divider").gameObject.SetActive(false);
            var info=Box("Island status backing",header,new Vector2(0,1),new Vector2(0,1),new Vector2(-8,6),new Vector2(430,84),new Color(1,1,1,.92f));info.SetAsFirstSibling();
            title.rectTransform.anchoredPosition=new Vector2(0,-2);title.rectTransform.sizeDelta=new Vector2(340,30);title.color=uiBlue;
            status.rectTransform.anchoredPosition=new Vector2(0,-34);
            var supplies=header.Find("Supplies").GetComponent<RectTransform>();supplies.anchorMin=supplies.anchorMax=supplies.pivot=new Vector2(0,1);supplies.anchoredPosition=new Vector2(0,-59);supplies.sizeDelta=new Vector2(420,26);
            stats.fontSize=13;stats.alignment=TextAlignmentOptions.MidlineLeft;stats.rectTransform.sizeDelta=new Vector2(420,26);
            var controls=header.Find("Observation controls").GetComponent<RectTransform>();controls.SetParent(gameplayInterface.transform,false);controls.anchorMin=controls.anchorMax=controls.pivot=Vector2.zero;controls.anchoredPosition=new Vector2(24,78);
            var banner=Box("Delivered task banner",gameplayInterface.transform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-24),new Vector2(650,52),uiTeal);
            deliveredMission=Text(banner,"",16,Color.white,new Vector2(28,-3),new Vector2(594,46),TextAlignmentOptions.Center,true);
            deliveredMission.enableAutoSizing=true;deliveredMission.fontSizeMin=12;deliveredMission.fontSizeMax=16;
            banner.GetComponent<Image>().color=Color.clear;HabitatFinalUi.Art(banner,"R19",new Vector2(-80,0),new Vector2(810,52)).transform.SetAsFirstSibling();
            
            var menu=Button(gameplayInterface.transform,"菜单",Vector2.zero,new Vector2(160,48),()=>Open(pausePanel),uiBlue,true);
            menu.name="Delivered game menu";menu.GetComponentInChildren<TMP_Text>().fontSize=25;
            var mr=menu.GetComponent<RectTransform>();mr.anchorMin=mr.anchorMax=mr.pivot=Vector2.one;mr.anchoredPosition=new Vector2(-24,-24);
            HabitatUiMotif.Add(menu.transform,new Vector2(124,-4),new Vector2(30,37),new Color32(105,226,193,255));
            foreach(Transform child in menu.transform)child.gameObject.SetActive(false);
            menu.GetComponent<Image>().color=Color.clear;
            var menuArt=HabitatFinalUi.Art(menu.transform,"R18",Vector2.zero,new Vector2(160,58));menu.targetGraphic=menuArt;
            var tr=toastPanel.GetComponent<RectTransform>();tr.anchorMin=tr.anchorMax=tr.pivot=new Vector2(.5f,0);tr.anchoredPosition=new Vector2(0,24);tr.sizeDelta=new Vector2(720,44);
            toastPanel.GetComponent<Image>().color=new Color32(80,122,140,245);toast.color=Color.white;toast.rectTransform.anchoredPosition=new Vector2(18,0);toast.rectTransform.sizeDelta=new Vector2(684,44);
            modal.transform.SetAsLastSibling();
            gameObject.AddComponent<HabitatSpecialPointer>();
        }
        void RefreshDeliveredInterface()
        {
            if(deliveredMission)deliveredMission.transform.parent.gameObject.SetActive(!game.State.onHarbor);
            if(deliveredMission)deliveredMission.text=game.ChapterActive?game.ChapterTitle+" · "+game.ChapterBody:game.State.won?"栖居达成 · 这一站的伙伴们已经安居":HabitatMechanicLevels.HabitatObjective(game.State);
            if(SessionMenuOpen)
            {
                foreach(var label in continueSession.GetComponentsInChildren<TMP_Text>())label.alpha=game.HasSavedSession?1f:.4f;
                resumeSession.gameObject.SetActive(game.SessionStarted);
            }
        }
    }
}
