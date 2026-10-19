using TMPro;
using UnityEngine;

namespace HabitatIsland
{
    public partial class HabitatHud
    {
        void BuildMain()
        {
            var root=gameplayInterface.transform;
            var header=Card("Island header",root,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-24),new Vector2(0,64));
            header.anchorMin=new Vector2(0,1);header.anchorMax=new Vector2(1,1);
            header.offsetMin=new Vector2(24,-88);header.offsetMax=new Vector2(-24,-24);
            Text(header,"栖屿",26,ink,new Vector2(20,-12),new Vector2(76,40),emphasis:true);
            Box("Brand divider",header,new Vector2(0,1),new Vector2(0,1),new Vector2(112,-17),new Vector2(1,30),rule);
            title=Text(header,"",17,ink,new Vector2(132,-9),new Vector2(310,28),emphasis:true);
            status=Text(header,"",12,muted,new Vector2(132,-36),new Vector2(310,22));
            var supplies=Rect("Supplies",header,new Vector2(1,1),new Vector2(1,1),new Vector2(-368,-14),new Vector2(380,36));
            stats=Text(supplies,"",18,ink,Vector2.zero,new Vector2(380,36),TextAlignmentOptions.MidlineRight);
            var controls=Rect("Observation controls",header,new Vector2(1,1),new Vector2(1,1),new Vector2(-12,-14),new Vector2(332,36));
            pauseLabel=Button(controls,"暂停",Vector2.zero,new Vector2(90,36),()=>game.TogglePause()).GetComponentInChildren<TMP_Text>();
            speedLabel=Button(controls,"1×",new Vector2(96,0),new Vector2(48,36),()=>game.CycleSpeed()).GetComponentInChildren<TMP_Text>();
            Button(controls,"设置",new Vector2(162,0),new Vector2(76,36),ToggleSettings).name="Open Settings";
            Button(controls,"帮助",new Vector2(244,0),new Vector2(76,36),ToggleHelp).name="Open Help";

            var quest=Card("Field note",root,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-108),new Vector2(270,218));
            tutorialFieldNote=quest;
            questTitle=Text(quest,"当前目标",18,ink,new Vector2(20,-17),new Vector2(230,30),emphasis:true);
            Box("Objective accent",quest,new Vector2(1,1),new Vector2(1,1),new Vector2(-20,-25),new Vector2(24,3),coral);
            objective=Text(quest,"",16,ink,new Vector2(20,-61),new Vector2(230,103));
            questProgress=Text(quest,"",13,muted,new Vector2(20,-174),new Vector2(230,24));
            var progress=Box("Objective track",quest,new Vector2(0,0),Vector2.zero,new Vector2(20,12),new Vector2(230,3),surface);
            questFill=Box("Objective progress",progress,new Vector2(0,.5f),new Vector2(0,.5f),Vector2.zero,new Vector2(0,3),mint).GetComponent<UnityEngine.UI.Image>();
            var handbook=Button(root,"",new Vector2(24,-338),new Vector2(270,42),()=>{ecologyPanel.SetActive(!ecologyPanel.activeSelf);if(ecologyPanel.activeSelf&&toolsPanel)toolsPanel.SetActive(false);nextRefresh=0;},paper);
            handbook.name="Toggle resident handbook";
            tutorialHandbook=handbook.GetComponent<RectTransform>();
            Icon(handbook.transform,5,new Vector2(15,-11),20,muted);
            handbookLabel=Text(handbook.transform,"居民手册  +",15,ink,new Vector2(46,0),new Vector2(207,42),TextAlignmentOptions.MidlineLeft);
            ecologyPanel=Card("Resident handbook",root,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-446),new Vector2(270,286)).gameObject;
            Text(ecologyPanel.transform,"选择居民查看环境需求",13,muted,new Vector2(16,-13),new Vector2(238,24));
            for(int i=0;i<6;i++)
            {
                int species=i;
                var row=Button(ecologyPanel.transform,"",new Vector2(12,-45-i*38),new Vector2(246,32),()=>{var a=game.State.animals.Find(v=>(int)v.species==species&&!v.carried);if(a!=null)game.SelectAnimal(a.id);},surface);
                row.name="Resident "+i;
                animalLabels[i]=Text(row.transform,"",14,ink,new Vector2(10,0),new Vector2(226,30),TextAlignmentOptions.MidlineLeft);
                animalBars[i]=Box("Breeding progress",row.transform,new Vector2(0,0),Vector2.zero,new Vector2(10,0),new Vector2(0,2),mint).GetComponent<UnityEngine.UI.Image>();
            }
            ecologyPanel.SetActive(false);

            inspectorPanel=Card("Selected habitat",root,new Vector2(1,1),new Vector2(1,1),new Vector2(-24,-108),new Vector2(280,306)).gameObject;
            Text(inspectorPanel.transform,"地块详情",13,muted,new Vector2(20,-16),new Vector2(190,26));
            Button(inspectorPanel.transform,"×",new Vector2(232,-12),new Vector2(28,28),()=>{game.State.selectedX=-1;game.State.selectedZ=-1;nextRefresh=0;},surface);
            Rule(inspectorPanel.transform,20,-51,240);
            selection=Text(inspectorPanel.transform,"",15,ink,new Vector2(20,-63),new Vector2(240,139));
            target=Text(inspectorPanel.transform,"",13,muted,new Vector2(20,-210),new Vector2(240,24));
            useToolButton=Button(inspectorPanel.transform,"使用工具  E",new Vector2(20,-246),new Vector2(240,40),()=>game.ApplyTool(),primary:true);
            useToolButton.name="Use selected tool";
            rotateButton=Button(inspectorPanel.transform,"旋转道路 / 风机  R",new Vector2(20,-297),new Vector2(240,32),()=>game.RotateTarget(),surface);
            inspectorPanel.SetActive(false);

            eventPanel=Card("Island log",root,new Vector2(1,1),new Vector2(1,1),new Vector2(-24,-510),new Vector2(280,250)).gameObject;
            Text(eventPanel.transform,"岛屿日志",18,ink,new Vector2(20,-15),new Vector2(240,30),emphasis:true);
            events=Text(eventPanel.transform,"",14,muted,new Vector2(20,-59),new Vector2(240,176));
            eventPanel.SetActive(false);
            toastPanel=Card("Island message",root,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-108),new Vector2(580,58)).gameObject;
            toast=Text(toastPanel.transform,"",15,ink,new Vector2(20,-7),new Vector2(540,44),TextAlignmentOptions.Center);
            toastPanel.SetActive(false);

            var toolsToggle=Button(root,"工具 / 操作",new Vector2(24,-392),new Vector2(270,42),()=>{toolsPanel.SetActive(!toolsPanel.activeSelf);if(toolsPanel.activeSelf)ecologyPanel.SetActive(false);nextRefresh=0;},paper);
            toolsToggle.name="Toggle keeper tools";tutorialToolsToggle=toolsToggle.GetComponent<RectTransform>();
            var tools=Card("Keeper tools",root,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-446),new Vector2(270,366));
            toolsPanel=tools.gameObject;
            carrying=Text(tools,"",14,muted,new Vector2(14,-13),new Vector2(242,28));
            string[] names={"搬运","填土","引导","虾笼","收获"};
            for(int i=0;i<5;i++)
            {
                int id=i;
                var button=Button(tools,"",new Vector2(12+(i%2)*126,-48-(i/2)*60),new Vector2(120,54),()=>{if(game.tool==id)return;if(game.TrySelectTool(id)){nextRefresh=0;HabitatAudio.Play(HabitatAudioCue.Click);}},paper,cue:null);
                button.name="Tool "+(i+1);
                toolImages[i]=button.GetComponent<UnityEngine.UI.Image>();
                var icon=Icon(button.transform,i,new Vector2(10,-14),23,ink);
                toolIconImages[i]=icon.GetComponentsInChildren<UnityEngine.UI.Image>();
                toolLabels[i]=Text(button.transform,names[i],14,ink,new Vector2(43,-15),new Vector2(64,24),TextAlignmentOptions.MidlineLeft);
                toolKeys[i]=Text(button.transform,(i+1).ToString(),11,muted,new Vector2(98,-3),new Vector2(17,22),TextAlignmentOptions.Center);
            }
            var shop=Button(tools,"码头商店 B",new Vector2(138,-168),new Vector2(120,54),()=>game.OpenShopInteraction(),surface);
            shop.name="Open Shop";
            Rule(tools,12,-232,246);
            Button(tools,"撤销  Z",new Vector2(12,-242),new Vector2(120,32),()=>game.Undo());
            Button(tools,"显示范围",new Vector2(138,-242),new Vector2(120,32),()=>game.overlays=!game.overlays);
            Button(tools,"全图  M",new Vector2(12,-282),new Vector2(120,32),()=>game.View.FocusOverview());
            Button(tools,"回到人物",new Vector2(138,-282),new Vector2(120,32),()=>game.View.FocusPlayer());
            Button(tools,"岛屿日志",new Vector2(12,-322),new Vector2(246,32),()=>eventPanel.SetActive(!eventPanel.activeSelf)).name="Toggle Island log";
            toolsPanel.SetActive(false);
            BuildNearbyPrompt(root);
            BuildTutorialControls(root);
            BuildChapterControls(root);
        }

        void BuildModals()
        {
            modal=Box("Modal backdrop",gameplayInterface.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,Vector2.zero,new Color(.035f,.12f,.11f,.55f),true).gameObject;
            var shade=modal.GetComponent<RectTransform>();shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.offsetMin=shade.offsetMax=Vector2.zero;
            settingsPanel=ModalCard("Settings",new Vector2(760,692),"观察设置","调整生态规则与观察节奏，关闭后回到小岛。").gameObject;
            string[] labels={"繁衍等待时间","动物移动间隔","红虾威胁半径","友好生物半径","水母连通水域","金鱼封闭池塘","陆龟连通高地"};
            for(int i=0;i<labels.Length;i++)
            {
                int id=i;float y=-124-i*44;
                Text(settingsPanel.transform,labels[i],16,ink,new Vector2(28,y),new Vector2(330,34),TextAlignmentOptions.MidlineLeft);
                Text(settingsPanel.transform,i<2?"秒":"格",13,muted,new Vector2(392,y),new Vector2(64,34),TextAlignmentOptions.MidlineRight);
                var stepper=Box("Parameter "+i,settingsPanel.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(572,y),new Vector2(160,34),surface);
                Button(stepper,"−",Vector2.zero,new Vector2(38,34),()=>Adjust(id,-1),surface);
                parameterValues[i]=Text(stepper,"",17,ink,new Vector2(40,0),new Vector2(80,34),TextAlignmentOptions.Center,true);
                Button(stepper,"+",new Vector2(122,0),new Vector2(38,34),()=>Adjust(id,1),surface);
                if(i<6)Rule(settingsPanel.transform,28,y-40,704);
            }
            Rule(settingsPanel.transform,28,-442,704);
            movementLabel=Button(settingsPanel.transform,"自主移动：开",new Vector2(28,-460),new Vector2(220,36),()=>game.tuning.animalMovement=!game.tuning.animalMovement,cue:HabitatAudioCue.Confirm).GetComponentInChildren<TMP_Text>();
            Button(settingsPanel.transform,"恢复参数",new Vector2(260,-460),new Vector2(148,36),()=>game.tuning=new Tuning(),cue:HabitatAudioCue.Confirm);
            Button(settingsPanel.transform,"单步一秒",new Vector2(420,-460),new Vector2(148,36),()=>{game.Step();priorPause=true;});
            Button(settingsPanel.transform,"重开本关",new Vector2(580,-460),new Vector2(152,36),()=>game.RestartCurrentLevel());
            Button(settingsPanel.transform,"保存进度",new Vector2(28,-510),new Vector2(346,36),SaveProgress);
            Button(settingsPanel.transform,"读取进度",new Vector2(386,-510),new Vector2(346,36),LoadProgress);
            Text(settingsPanel.transform,"乘船探索",16,ink,new Vector2(28,-568),new Vector2(704,26),emphasis:true);
            Text(settingsPanel.transform,"到小船旁按 E 选择地图。已探索的岛屿进度会保留；出发前请放下手持物品。",14,muted,new Vector2(28,-602),new Vector2(704,28));
            Button(settingsPanel.transform,"返回码头",new Vector2(28,-642),new Vector2(156,36),()=>game.ReturnToHarbor());
            settingsFeedback=Text(settingsPanel.transform,"ESC 返回 · 观察已暂停",12,muted,new Vector2(200,-648),new Vector2(330,26));
            Button(settingsPanel.transform,"回到小岛",new Vector2(548,-642),new Vector2(184,36),BackToGame,primary:true,cue:null);

            shopPanel=ModalCard("Shop",new Vector2(760,752),"码头商店","出售实际背包里的鱼虾，或购买物资。鱼每条 8 贝币，虾每只 5 贝币。").gameObject;
            string[] items={"soil","crate","float","fan","tide","rod","trap","sell"};
            string[] names={"土块","木箱","漂浮板","风机","潮汐引擎","钓竿","虾笼","交付全部渔获"};
            string[] costs={"先取库存 · 补货 5 贝币","6 贝币","7 贝币","10 贝币","12 贝币","10 贝币","3 贝币","鱼 8 / 虾 5 贝币"};
            for(int i=0;i<items.Length;i++)
            {
                string item=items[i];var b=Button(shopPanel.transform,"",new Vector2(28+(i%2)*360,-132-(i/2)*88),new Vector2(344,76),()=>game.Buy(item),surface);
                b.name="Buy "+item;
                var itemTitle=Text(b.transform,names[i],18,ink,new Vector2(18,-10),new Vector2(275,30),emphasis:true);
                var itemCost=Text(b.transform,costs[i],14,muted,new Vector2(18,-43),new Vector2(308,25));
                Text(b.transform,"+",20,mint,new Vector2(302,-10),new Vector2(26,30),TextAlignmentOptions.Center);
                if(item=="sell"){sellButton=b;sellTitle=itemTitle;sellCost=itemCost;}
                if(item=="fan")
                {
                    fanShopButton=b;fanShopTitle=itemTitle;fanShopCost=itemCost;
                    fanShopCost.enableAutoSizing=true;fanShopCost.fontSizeMin=12;fanShopCost.fontSizeMax=14;
                }
            }
            Rule(shopPanel.transform,28,-493,704);
            shopMessage=Text(shopPanel.transform,"",15,ink,new Vector2(28,-512),new Vector2(704,148));
            Button(shopPanel.transform,"与老板交谈",new Vector2(28,-690),new Vector2(184,40),()=>game.OpenShopInteraction()).name="Talk to shopkeeper";
            Text(shopPanel.transform,"B / ESC 返回",12,muted,new Vector2(230,-703),new Vector2(280,24));
            Button(shopPanel.transform,"继续行动",new Vector2(548,-690),new Vector2(184,40),BackToGame,primary:true,cue:null);

            helpPanel=ModalCard("Help",new Vector2(820,674),"从观察开始","给每一种生命，找到适合它的栖居地。").gameObject;
            HelpSection(helpPanel.transform,"01  推拉与搬运","方向键或 E 推动单个箱子；Ctrl + 后退方向拉箱。箱子不能抱起，可推上浮箱继续铺路。水中或低处箱子收网出水、人物后退，到位后才结算。只有攀爬方块可按 Shift 向上爬。",128,133);
            HelpSection(helpPanel.transform,"02  让邻居安居","红虾会惊扰同层动物，高地陆龟提供庇护。水母喜欢连通水域，金鱼需要封闭池塘；寄居蟹需要朋友，海星需要连续岸线。",291,133);
            HelpSection(helpPanel.transform,"03  等待新生命","持续满足条件后会产出后代。选择「收获」工具收集，或用虾笼自动收集虾幼体；回到渡口出售鱼虾，换取改造物资。",470,84);
            Box("Help column divider",helpPanel.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(453,-132),new Vector2(1,447),rule);
            Text(helpPanel.transform,"常用操作",18,ink,new Vector2(480,-132),new Vector2(302,30),emphasis:true);
            string[] keys={"WASD / ↑↓←→","Ctrl + 方向","Shift + 方向","1 — 5","E / 右键","R","空格 / Z","Q / C","M / Home","滚轮 / 中键","Tab / B","F1 / ESC"};
            string[] actions={"按画面方向移动 / 推箱","后退并拉动身前箱子","攀爬指定攀爬方块","切换工具","工具 / 小船选关","旋转道路 / 风机","暂停继续 / 撤销","转动视角","全图 / 回到人物","缩放 / 拖动镜头","设置与码头 / 补给","帮助 / 关闭弹窗"};
            for(int i=0;i<keys.Length;i++)
            {
                float y=-176-i*32;
                var key=Box("Shortcut",helpPanel.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(480,y),new Vector2(96,26),surface);
                Text(key,keys[i],12,ink,Vector2.zero,new Vector2(96,26),TextAlignmentOptions.Center);
                Text(helpPanel.transform,actions[i],15,ink,new Vector2(592,y),new Vector2(202,26),TextAlignmentOptions.MidlineLeft);
            }
            Rule(helpPanel.transform,28,-602,764);
            Button(helpPanel.transform,"新手教学",new Vector2(28,-619),new Vector2(150,36),()=>{game.ShowTutorialGuide();nextRefresh=0;}).name="Show tutorial guide";
            Text(helpPanel.transform,"先选目标格再使用工具 · V 遮挡透明\nAlt + 点击选前方高层",12,muted,new Vector2(194,-619),new Vector2(394,36));
            Button(helpPanel.transform,"开始观察",new Vector2(608,-619),new Vector2(184,36),BackToGame,primary:true,cue:null);
            BuildHomePanel();
            BuildChapterDialogue();
            BuildAudioControls();
            mapSelection=HabitatMapSelectionPanel.Create(modal.transform,font,ConfirmMapTravel,()=>{if(!game.Busy&&!game.StoryPlaying)ClosePanels();});
            mapProgressNote=Text(mapSelection.transform,"",14,muted,new Vector2(451,-758),new Vector2(635,34),TextAlignmentOptions.Center);
            modal.SetActive(false);
        }

        void HelpSection(Transform parent,string heading,string body,float y,float height)
        {
            Text(parent,heading,18,ink,new Vector2(28,-y),new Vector2(392,31),emphasis:true);
            Text(parent,body,16,muted,new Vector2(28,-y-40),new Vector2(392,height));
        }
    }
}
