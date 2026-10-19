using UnityEngine;

namespace HabitatIsland
{
    public partial class HabitatGame
    {
        bool chapterGreetingPending, chapterChestPending, chapterMorningGreetingPending;
        bool chapterReturnReminderShown;
        int campaignReturnReminderLevel=-1;
        LineRenderer chapterMarker;
        Material chapterMarkerMaterial;
        public HabitatChapterStage ChapterStage => HabitatChapterRules.Stage(State);
        public bool ChapterActive => State != null && State.authoredLevel && !HabitatMechanicLevels.IsNewOptional(State) && (HabitatLevels.IsHomeHarbor(State)||State.levelIndex==HabitatFigJamLevel.Index||HabitatMechanicLevels.Contains(State.levelIndex));
        bool LaterCampaign => State!=null&&State.chapterOneMorningSeen;
        int FocusedCampaignStage => !State.onHarbor&&HabitatMechanicLevels.Contains(State.levelIndex)&&State.levelIndex>5?HabitatChapterRules.CampaignNumber(State.levelIndex):HabitatChapterRules.UnlockedStage(State);
        public int RecommendedCampaignLevel => State!=null&&State.chapterOneMorningSeen?HabitatChapterRules.CampaignLevel(HabitatChapterRules.UnlockedStage(State)):HabitatChapterRules.MainLevel;
        public bool CampaignComplete => HasCompletedLevel(5)&&HasCompletedLevel(6)&&HasCompletedLevel(7)&&HasCompletedLevel(8)&&HasCompletedLevel(10);
        public float ChapterProgress => LaterCampaign?Mathf.Clamp01((FocusedCampaignStage-1+(HasCompletedLevel(HabitatChapterRules.CampaignLevel(FocusedCampaignStage))? .9f : .2f))/5f):((int)ChapterStage)/30f;
        public bool ChapterCanResume => ChapterActive && (LaterCampaign?!State.won:ChapterStage == HabitatChapterStage.CatchFish) && paused && !State.onHarbor;
        public bool ChapterTargetAvailable => ChapterActive && TryChapterTarget(out _);
        public string ChapterTitle
        {
            get
            {
                if(LaterCampaign)
                {
                    if(CampaignComplete)return "五岛探索完成 · 返回码头";
                    int stage=FocusedCampaignStage;
                    if(HasCompletedLevel(HabitatChapterRules.CampaignLevel(stage)))return "第 "+stage+" 关完成 · 返航交谈";
                    return stage==2?"第二关 · 背篓与火爆大虾":stage==3?"第三关 · 安置寄居蟹":stage==4?"第四关 · 水母与潮汐":"第五关 · 综合挑战";
                }
                switch (ChapterStage)
                {
                    case HabitatChapterStage.MeetShopkeeper: return "归航 · 与老板交谈";
                    case HabitatChapterStage.SailToIsland: return "第一关 · 乘船出发";
                    case HabitatChapterStage.FindRod: return "第一关 · 寻找特制鱼竿";
                    case HabitatChapterStage.CatchFish: return "第一关 · 围住琉璃鱼";
                    case HabitatChapterStage.ReturnToShop: return "第一关 · 把渔获带回商店";
                    case HabitatChapterStage.RestAtHome: return "第一关 · 回家休息";
                    default: return "荒岛委托完成 · 自由探索";
                }
            }
        }
        public string ChapterBody
        {
            get
            {
                if(LaterCampaign)return LaterCampaignBody();
                switch (ChapterStage)
                {
                    case HabitatChapterStage.MeetShopkeeper: return "走到码头商店，按 E 与老板交谈，接取第一关的琉璃鱼委托。";
                    case HabitatChapterStage.SailToIsland: return "走到小船旁按 E，选择「"+HabitatLevels.Names[HabitatChapterRules.MainLevel]+"」，确认出海。背包和岛屿进度会保留。";
                    case HabitatChapterStage.FindRod: return "沿木桥上岸，探索并靠近标记的宝箱。选中宝箱，按 1、E 打开，取得特制鱼竿。";
                    case HabitatChapterStage.CatchFish:
                        return "用箱子封住通向外海的缺口，保留鱼所在的水格。所有动物同时适居后完成本关；继续生态模拟等待幼鱼，选中鱼按 5、E 垂钓。\n琉璃鱼 " + State.glassFish + " / 2 · 两条合计 100 贝币";
                    case HabitatChapterStage.ReturnToShop: return "已收集 2 条琉璃鱼。回到码头商店按 E 交付，领取 100 贝币。也可从设置选择「返回码头」。";
                    case HabitatChapterStage.RestAtHome: return HasCompletedLevel(5)?"委托已交付，100 贝币已到账。走到家门口按 E 休息，或与老板继续交谈后回家。":"委托报酬已到账。第一关动物尚未全部适居，请补给后重返岛屿；完成环境目标后休息才会开放第二关。";
                    default: return "晨光带来新的一天。可以继续探索荒岛、捕捞和交易，也可到小船选择其他正式岛屿。";
                }
            }
        }

        string LaterCampaignBody()
        {
            if(CampaignComplete)return "五张正式关卡都已达成动物同时适居。可以返航出售收获，并与老板完成探索记录；之后仍可重访各岛。";
            int stage=FocusedCampaignStage;
            if(!HabitatChapterRules.HasBriefing(State,2))return "第二天到了。先与老板交谈，了解火爆大虾；商店已上架吹风机（200 贝币）。若贝币不足，可继续捕获幼鱼并出售，每条 100 贝币，再出发。";
            if(HasCompletedLevel(HabitatChapterRules.CampaignLevel(stage)))
            {
                if(stage==3&&State.deliveredShells==0)return State.shells>0?"第三关已完成。带着蟹壳返航，在商店出售（200 贝币）后与老板交谈，解锁第四关和磁铁。":"第三关已完成。寄居蟹已离壳；空手选中邻格蟹壳按 E 拾取，带回商店出售，再与老板交谈。";
                return "本关动物已同时适居。可收获鱼、水母的后代；携带的大虾会在返航时收入背篓。回商店出售收获并与老板交谈，了解下一座岛。";
            }
            if(State.levelIndex!=HabitatChapterRules.CampaignLevel(stage))return "准备好工具后，到小船旁按 E，选择第 "+stage+" 关并确认出海。已完成的岛屿仍可重访，地形与居民位置保留。";
            if(stage==2)return !State.backBasketUnlocked?"走上竹排沿水纹往返，用吹风机改变航向，探索宝箱取得背篓。背篓能携带大虾；普通箱子不能爬高。":"使用背篓携带大虾引导动物，将两处金鱼水域封闭，同时让所有动物适居。通关后可携虾返航出售。";
            if(stage==3)return "用背篓携虾，将寄居蟹引到邻水的陆地，3×3 范围内有水中金鱼陪伴；也要封闭金鱼水域，让全体动物同时适居。蟹离壳后可拾取并出售蟹壳。";
            if(stage==5)return "综合运用已获得的工具与机关，让岛上所有动物同时适居。可选择发射器路线，或使用磁铁调整箱子；完成后带回收获，在商店按价出售。";
            return "利用潮汐与箱子，让水母所在水域连通外海，同时把金鱼围在封闭水域。鱼竿可在三格内牵引水母；所有动物同时适居才完成。";
        }

        public bool CanTravelToLevel(int index, out string reason)
        {
            reason = "";
            if (index < 0 || index >= HabitatLevels.Names.Length) { reason = "目的地不存在。"; return false; }
            if (index == HabitatLevels.HomeHarborIndex || index == HabitatTutorialRules.LevelIndex) return true;
            if(index==HabitatChapterRules.MainLevel&&State.levelIndex==HabitatTutorialRules.LevelIndex)return true;
            if(HabitatMechanicLevels.Contains(index))
            {
                if(!State.chapterOneAccepted){reason="先到码头商店与老板交谈，接取第一关委托。";return false;}
                int stage=HabitatChapterRules.CampaignNumber(index);
                if(stage<=HabitatChapterRules.UnlockedStage(State)&&(stage==1||HasCompletedLevel(HabitatChapterRules.CampaignLevel(stage-1))))return true;
                reason=stage==2?"先完成第一关委托并回家休息，第二天开放第二关。":stage==3?"先完成第二关，返航与老板交谈解锁第三关。":stage==4?"先完成第三关，出售蟹壳并与老板交谈，解锁第四关。":"先完成第四关，返航与老板交谈，解锁综合挑战。";
                return false;
            }
            if(HabitatLevels.IsFormal(index))
            {
                if(CampaignComplete)return true;
                reason="先完成五张正式关卡，再开放额外岛屿探索。";return false;
            }
            if (index != HabitatChapterRules.MainLevel)
            { reason = "这是开发练习场，请从帮助菜单进入；正式航线请在船只地图中选择。"; return false; }
            // The optional ecosystem practice must always offer a route back to the main harbor.
            if (State.chapterOneAccepted || State.levelIndex == HabitatTutorialRules.LevelIndex || State.levelIndex != HabitatChapterRules.MainLevel) return true;
            reason = "先到码头商店与老板交谈，接取琉璃鱼委托。";
            return false;
        }

        public void OpenShopInteraction()
        {
            if (Busy || (Fishing && Fishing.IsOpen) || State == null) return;
            if(RemoteMerchantAvailable){Hud.OpenRemoteShop();State.message="已接通商人。工具放入背包，木箱送入船舱；交付和出售请返航。";return;}
            if (!State.onHarbor || !View.Harbor.NearShop(State))
            { State.message = "请先走到码头商店旁，再按 E 或 B 与老板交谈。"; return; }
            chapterGreetingPending = false;
            if (!State.chapterOneAccepted)
                Hud.ShowChapterDialogue("商店老板", new[] {
                    "回来了！听说南边第一座小岛出现了一群琉璃鱼。你愿意帮我带两条回来吗？这次委托合计 100 贝币。",
                    "这些小家伙很会跑。先用箱子把通向外海的缺口围住，保留水域。目标鱼满足繁衍条件就能先捕捞、出售补给；全体动物同时安居后才算通关。",
                    "普通鱼竿可钓不上它们。岛上有前人留下的宝箱，里面装着特制鱼竿，先去找到它吧！"
                }, "接下委托", AcceptChapterCommission);
            else if (!State.chapterOneDelivered && State.glassFish >= HabitatChapterRules.TargetFish)
                Hud.ShowChapterDialogue("商店老板", new[] {
                    "哦吼，多美丽的琉璃鱼！两条正好。确认交付后，我会付给你这次帮忙的报酬：100 贝币。"
                }, "交付 2 条琉璃鱼", DeliverChapterCommission);
            else if (State.chapterOneDelivered && !State.chapterOneMorningSeen)
                ShowChapterRewardDialogue();
            else if(TryShowCampaignConversation())return;
            else Hud.ToggleShop();
        }

        bool TryShowCampaignConversation()
        {
            if(!LaterCampaign)return false;
            if(!HabitatChapterRules.HasBriefing(State,2))
            {
                Hud.ShowChapterDialogue("商店老板",new[]{
                    "更南边的小岛盛产火红色的大虾。它们脾气似乎不太好，没有趁手的工具可不行。",
                    "商店的吹风机售价 200 贝币，能改变竹排的往返航向，也能推动水中浮箱。岛上还有前人留下的背篓，先找到它，再试着携带大虾。",
                    "购买好需要的东西后就出发吧。第一关的地形和生物位置会保留，之后仍然可以回来钓鱼。"
                },"查看商店",()=>FinishCampaignBriefing(2));return true;
            }
            if(HasCompletedLevel(6)&&!HabitatChapterRules.HasBriefing(State,3))
            {
                Hud.ShowChapterDialogue("商店老板",new[]{
                    "这次商店又上了新的货物：发射器。或许会对你有用。",
                    "最近北方城市的人流行用寄居蟹的壳做装饰。它们常不离开自己的壳；把它安置在邻水陆地、金鱼附近，也许就会安心离壳。",
                    "第三关的航线准备好了。带上背篓，用大虾引导寄居蟹；取得蟹壳后可以带回来出售。"
                },"解锁第三关",()=>FinishCampaignBriefing(3));return true;
            }
            if(HasCompletedLevel(7)&&!HabitatChapterRules.HasBriefing(State,4))
            {
                if(State.deliveredShells<1)
                {
                    Hud.ShowChapterDialogue("商店老板",new[]{State.shells>0?"你带回了寄居蟹的壳！商店每枚收购 200 贝币。先出售这次收获，再来聊聊下一座小岛。":"把第三关寄居蟹留下的壳带回来吧。空手选中邻格蟹壳按 E 拾取；我每枚出 200 贝币收购。"},"查看商店",()=>Hud.ToggleShop());return true;
                }
                Hud.ShowChapterDialogue("商店老板",new[]{
                    "寄居蟹的壳已经收到了。这次商店也上架了磁铁，所有工具现在都能买到了。",
                    "听说下一座岛上有神秘的水母。你可以用特制鱼竿在三格内牵引它们，但它们需要与外海连通的水域。",
                    "岛上还有喜欢封闭水域的金鱼。让它们各得其所，准备好后再出海探索吧！"
                },"解锁第四关",()=>FinishCampaignBriefing(4));return true;
            }
            if(HasCompletedLevel(8)&&!HabitatChapterRules.HasBriefing(State,5))
            {
                Hud.ShowChapterDialogue("商店老板",new[]{
                    "前四座岛的机制你已经熟悉了。接下来是第五关综合挑战。",
                    "你可以在商店准备发射器，或用磁铁配合箱子探索另一条路线。购买前看看说明，选择自己需要的工具。"
                },"开放综合挑战",()=>FinishCampaignBriefing(5));return true;
            }
            if(CampaignComplete&&!HabitatChapterRules.HasBriefing(State,6))
            {
                Hud.ShowChapterDialogue("商店老板",new[]{
                    "五座小岛的动物都安居了，辛苦了！鱼、水母的后代、背篓里的大虾和蟹壳都可以在这里出售。",
                    "这段探索告一段落。已完成岛屿的道路、机关与居民位置都会保留，你也可以继续出海探索其他岛屿。"
                },"完成探索记录",()=>{if(Busy)return;Mutate(()=>{if(!CampaignComplete||HabitatChapterRules.HasBriefing(State,6))return false;State.campaignBriefedMask|=1<<6;HabitatRules.Log(State,"五岛探索记录完成。可继续探索和交易。");return true;});Hud.ToggleShop();});return true;
            }
            return false;
        }

        void FinishCampaignBriefing(int stage)
        {
            if(Busy||!State.onHarbor||!View.Harbor.NearShop(State))return;
            if(Mutate(()=>HabitatChapterRules.RecordBriefing(State,stage,stage==2||HasCompletedLevel(HabitatChapterRules.CampaignLevel(stage-1)))))Hud.ToggleShop();
        }

        public void PrepareNextCampaignIsland()
        {
            if(Busy||State==null)return;
            if(State.levelIndex==HabitatChapterRules.MainLevel&&!State.chapterOneDelivered&&State.glassFish<HabitatChapterRules.TargetFish)
            {
                Hud.ClosePanels();
                RevealChapterTarget();
                State.message="第一关环境目标已完成。先钓到两条琉璃鱼：首次可以直接捕获一条，继续生态模拟，等亲本刷新幼鱼后再钓第二条。";return;
            }
            ReturnToHarbor();
            if(!State.onHarbor)return;
            bool needsShop=!State.chapterOneMorningSeen||!HabitatChapterRules.HasBriefing(State,2)
                ||(HasCompletedLevel(6)&&!HabitatChapterRules.HasBriefing(State,3))
                ||(HasCompletedLevel(7)&&!HabitatChapterRules.HasBriefing(State,4))
                ||(HasCompletedLevel(8)&&!HabitatChapterRules.HasBriefing(State,5));
            if(needsShop){State.message="先到码头商店与老板交谈，完成交付或解锁下一关。";View.FocusTutorialTarget(View.Harbor.ModelTransform.TransformPoint(HabitatImportedDockLayout.ShopApproach));}
            else Hud.OpenMapSelection(RecommendedCampaignLevel);
        }

        public void AcceptChapterCommission()
        {
            if (Busy || !View.Harbor.NearShop(State)) return;
            Mutate(() => HabitatChapterRules.Accept(State));
            State.message = "已接取委托：乘小船前往第一关，找到特制鱼竿并带回 2 条琉璃鱼。";
        }

        public void DeliverChapterCommission()
        {
            if (Busy || !State.onHarbor || !View.Harbor.NearShop(State)) return;
            if (!Mutate(() => HabitatChapterRules.Deliver(State),HabitatActionCue.Trade)) return;
            ShowChapterRewardDialogue();
        }

        void ShowChapterRewardDialogue()
        {
            Hud.ShowChapterDialogue("商店老板", new[] {
                "两条琉璃鱼的委托报酬 100 贝币已经付给你了。吹风机也上架了，之后探索时或许能派上用场。",
                "天色不早了，今天先回家好好休息吧。明早再来看看这片海。"
            }, "回家休息", FinishChapterRewardDialogue);
        }

        public void FinishChapterRewardDialogue()
        {
            if (Busy || !State.chapterOneDelivered || State.chapterOneMorningSeen || !View.Harbor.NearShop(State)) return;
            Hud.ClosePanels();
            if (!Story || !Story.BeginWalkHome(() => {
                View.Harbor.TryPlaceAt(State, HabitatImportedDockLayout.HomeApproach);
                View.Render(State, tuning, overlays);
                RestAtHome();
            })) State.message = "请走到家门口按 E，休息到明天。";
        }

        public void ResumeChapterEcology()
        {
            if (Busy || Hud.ModalOpen || (Fishing && Fishing.IsOpen) || !ChapterCanResume) return;
            paused = false;
            State.message = "生态模拟已继续。让岛上动物同时适居，继续观察繁衍与渔获刷新。";
        }

        bool TryChapterChest(out int x, out int z)
        {
            ChestReward reward=State.levelIndex==6?ChestReward.BackBasket:ChestReward.FishingRod;
            for (int i = 0; i < State.tiles.Length; i++)
                if (State.tiles[i].prop == PropKind.Chest && State.tiles[i].chestReward == reward)
                { x = i % State.width; z = i / State.width; return true; }
            x = z = -1; return false;
        }

        bool TryChapterTarget(out Vector3 target)
        {
            target = Vector3.zero;
            if (!ChapterActive || !View || !View.Harbor.Ready) return false;
            var model = View.Harbor.ModelTransform;
            if(!HabitatLevels.IsHomeHarbor(State) && (State.onHarbor || ChapterStage==HabitatChapterStage.ReturnToShop || ChapterStage==HabitatChapterStage.RestAtHome))
            { target=model.TransformPoint(HabitatImportedDockLayout.BoatApproach);return true; }
            if(LaterCampaign)
            {
                int stage=FocusedCampaignStage;
                if(State.onHarbor&&(State.levelIndex!=HabitatChapterRules.CampaignLevel(stage)||HasCompletedLevel(HabitatChapterRules.CampaignLevel(stage))||!HabitatChapterRules.HasBriefing(State,stage)))
                {
                    bool shop=HasCompletedLevel(HabitatChapterRules.CampaignLevel(stage))||!HabitatChapterRules.HasBriefing(State,stage);
                    target=model.TransformPoint(shop?HabitatImportedDockLayout.ShopApproach:HabitatImportedDockLayout.BoatApproach);return true;
                }
                if(State.won)
                {
                    if(State.levelIndex==7&&State.shells==0&&State.deliveredShells==0)
                        for(int i=0;i<State.tiles.Length;i++)if(State.tiles[i].prop==PropKind.Shell)
                        {int x=i%State.width,z=i/State.width;target=new Vector3(x,HabitatRules.Height(State,x,z)*.7f+.15f,z);return true;}
                    target=model.TransformPoint(HabitatImportedDockLayout.BoatApproach);return true;
                }
                if(stage==2&&!State.backBasketUnlocked&&TryChapterChest(out int cx,out int cz))
                {target=new Vector3(cx,HabitatRules.Height(State,cx,cz)*.7f+.15f,cz);return true;}
                Species species=stage==2?Species.Goldfish:stage==3||stage==5?Species.Crab:Species.Jellyfish;
                Animal resident=State.animals.Find(a=>!a.carried&&a.species==species&&!HabitatRules.Evaluate(State,a,tuning).suitable)
                    ??State.animals.Find(a=>!a.carried&&a.species==species);
                if(resident==null)return false;
                target=new Vector3(resident.x,Mathf.Max(State.waterLevel+1,HabitatRules.Height(State,resident.x,resident.z))*.7f+.15f,resident.z);return true;
            }
            switch (ChapterStage)
            {
                case HabitatChapterStage.MeetShopkeeper:
                case HabitatChapterStage.ReturnToShop:
                    target = model.TransformPoint(HabitatImportedDockLayout.ShopApproach); return true;
                case HabitatChapterStage.SailToIsland:
                    target = model.TransformPoint(HabitatImportedDockLayout.BoatApproach); return true;
                case HabitatChapterStage.RestAtHome:
                    target = model.TransformPoint(HabitatImportedDockLayout.HomeApproach); return true;
                case HabitatChapterStage.FindRod:
                    if (TryChapterChest(out int x, out int z)) { target = new Vector3(x, HabitatRules.Height(State,x,z)*.7f+.15f,z); return true; }
                    return false;
                case HabitatChapterStage.CatchFish:
                    Animal fish = State.animals.Find(a => !a.carried && a.species == Species.Goldfish);
                    if (fish != null) { target = new Vector3(fish.x, HabitatRules.WaterSurfaceY(State.waterLevel)+.095f,fish.z); return true; }
                    return false;
                default: return false;
            }
        }

        public void RevealChapterTarget()
        {
            if (Busy || Hud.ModalOpen || (Fishing && Fishing.IsOpen) || !TryChapterTarget(out Vector3 target)) return;
            View.FocusTutorialTarget(target);
            State.message = "金色标记指向当前目标。按 Home 可把镜头移回角色。";
        }

        void UpdateChapter()
        {
            HabitatShopkeeperPlaceholder.Ensure(this);
            bool available = !Busy && !Hud.ModalOpen && !(Fishing && Fishing.IsOpen);
            if (available && chapterGreetingPending)
            { chapterGreetingPending = false; OpenShopInteraction(); }
            else if (available && chapterChestPending)
            {
                chapterChestPending = false;
                if(ChapterActive&&Story)
                {
                    if((State.levelIndex==5&&!State.specialRod||State.levelIndex==6&&!State.backBasketUnlocked)&&TryChapterChest(out int x,out int z))
                        Story.BeginChestReveal(x,z,()=>State.message=State.levelIndex==6?"宝箱里有背篓。到宝箱旁按 1、E 打开，即可携带火爆大虾。":"宝箱里有特制鱼竿。沿路探索，到宝箱旁按 1、E 打开。");
                    else if(State.levelIndex==7||State.levelIndex==8)
                    {
                        Animal animal=State.animals.Find(a=>!a.carried&&a.species==(State.levelIndex==7?Species.Shrimp:Species.Jellyfish));
                        if(animal!=null)Story.BeginResidentReveal(animal,State.levelIndex==7?"用背篓携带大虾，引导寄居蟹到适合的地方。":"用特制鱼竿牵引水母。它需要连通外海，金鱼则需要封闭水域。",()=>State.message=ChapterBody);
                    }
                }
            }
            else if (available && chapterMorningGreetingPending)
            {
                chapterMorningGreetingPending = false;
                Hud.ShowChapterDialogue("新的一天", new[] {
                    "南边的海上还有很多神奇的小岛。第二关航线已经开放，岛上有火爆大虾和前人留下的背篓。",
                    "先与老板交谈、准备工具，再乘船出发。探索过的岛屿会保留变化，刷新后的鱼仍可捕捞出售。"
                }, "开始新的一天", ()=>{});
            }
            else if (available && ChapterActive && ChapterStage == HabitatChapterStage.ReturnToShop && !chapterReturnReminderShown)
            {
                chapterReturnReminderShown = true;
                Hud.ShowChapterDialogue("商店老板", new[] { "已经钓到两条琉璃鱼了，回码头把它们带来给我吧。" }, "知道了", ()=>{});
            }
            if (State.glassFish < HabitatChapterRules.TargetFish && !State.chapterOneDelivered) chapterReturnReminderShown = false;
            if(available&&!Hud.ModalOpen&&!Busy&&LaterCampaign&&State.won&&!State.onHarbor&&HabitatMechanicLevels.Contains(State.levelIndex)&&State.levelIndex>5&&campaignReturnReminderLevel!=State.levelIndex)
            {
                campaignReturnReminderLevel=State.levelIndex;
                // The HUD emits one shopkeeper reminder after the settled victory.
            }
            if(!State.won)campaignReturnReminderLevel=-1;
            UpdateChapterMarker(available && !Busy && !Hud.ModalOpen && ChapterActive);
        }

        void ResetChapterPresentation()
        { chapterGreetingPending=chapterChestPending=chapterMorningGreetingPending=chapterReturnReminderShown=false;campaignReturnReminderLevel=-1; }

        void UpdateChapterMarker(bool show)
        {
            if (!show || !TryChapterTarget(out Vector3 target)) { if(chapterMarker)chapterMarker.enabled=false; return; }
            if (!chapterMarker)
            {
                var go=new GameObject("Chapter target outline");go.transform.SetParent(transform,false);
                chapterMarker=go.AddComponent<LineRenderer>();chapterMarker.useWorldSpace=true;chapterMarker.loop=true;
                chapterMarker.positionCount=4;chapterMarker.widthMultiplier=.055f;chapterMarker.numCornerVertices=4;
                chapterMarker.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;chapterMarker.receiveShadows=false;
                chapterMarkerMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));chapterMarkerMaterial.color=new Color(1f,.80f,.22f,1f);
                chapterMarker.sharedMaterial=chapterMarkerMaterial;
            }
            target.y+=.05f; float r=.43f+Mathf.Sin(Time.unscaledTime*3)*.025f;
            chapterMarker.SetPosition(0,target+new Vector3(-r,0,-r));chapterMarker.SetPosition(1,target+new Vector3(-r,0,r));
            chapterMarker.SetPosition(2,target+new Vector3(r,0,r));chapterMarker.SetPosition(3,target+new Vector3(r,0,-r));
            chapterMarker.enabled=true;
        }

        void DisposeChapter()
        { if(chapterMarker)Destroy(chapterMarker.gameObject);if(chapterMarkerMaterial)Destroy(chapterMarkerMaterial); }
    }
}
