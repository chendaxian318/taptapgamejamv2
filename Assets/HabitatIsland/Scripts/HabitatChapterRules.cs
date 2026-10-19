namespace HabitatIsland
{
    public enum HabitatChapterStage
    { MeetShopkeeper, SailToIsland, FindRod, CatchFish, ReturnToShop, RestAtHome, Complete }

    /// <summary>First commission rules. Scene movement, dialogue and camera presentation stay in the controller.</summary>
    public static class HabitatChapterRules
    {
        public const int TargetFish=2,RewardCoins=100,MainLevel=5;
        public static bool IsMainIsland(HabitatState s) => s!=null&&s.authoredLevel&&s.levelIndex==MainLevel;
        public static bool IsCampaignShop(HabitatState s) => s!=null&&(HabitatLevels.IsHomeHarbor(s)||(s.authoredLevel&&HabitatLevels.IsFormal(s.levelIndex))||s.chapterOneAccepted);

        public static bool CanBuyItem(HabitatState s,string item,out string reason)
        {
            reason="";
            if(!IsCampaignShop(s))return true;
            item=(item??"").ToLowerInvariant();
            if(item=="fan"&&!s.chapterOneDelivered)
            {reason="交付第一关的两条琉璃鱼后，商店上架吹风机。";return false;}
            if(item=="launcher"&&UnlockedStage(s)<3)
            {reason="第二关通关后回商店交谈，上架发射器。";return false;}
            if(item=="magnet"&&UnlockedStage(s)<4)
            {reason="第三关通关、出售蟹壳并与老板交谈后，上架磁铁。";return false;}
            if(item=="rod"||item=="basket"||item=="float"||item=="tide"||item=="soil"||item=="trap"||item=="road")
            {reason=item=="rod"?"特制鱼竿在第一关的宝箱里。":item=="basket"?"背篓在第二关的宝箱里。":"当前正式航线只出售木箱、吹风机、铲子、磁铁和发射器；岛上的机关需要就地使用。";return false;}
            return true;
        }

        public static int UnlockedStage(HabitatState s)
            =>s==null?1:System.Math.Max(s.chapterOneMorningSeen?2:1,System.Math.Min(5,s.campaignUnlockedStage));

        public static int CampaignLevel(int stage) =>stage==5?10:stage+4;
        public static int CampaignNumber(int index) =>index==10?5:index>=5&&index<=8?index-4:0;

        public static bool HasBriefing(HabitatState s,int stage)
            =>s!=null&&(s.campaignBriefedMask&(1<<stage))!=0;

        public static bool RecordBriefing(HabitatState s,int stage,bool previousCompleted)
        {
            if(s==null||stage<2||stage>5||HasBriefing(s,stage))return false;
            if(stage==2&&!s.chapterOneMorningSeen)return false;
            if(stage>2&&(!previousCompleted||UnlockedStage(s)<stage-1))return false;
            if(stage==4&&s.deliveredShells<1)return false;
            s.campaignUnlockedStage=System.Math.Max(UnlockedStage(s),stage);
            s.campaignBriefedMask|=1<<stage;
            HabitatRules.Log(s,"正式第 "+stage+" 关航线已解锁。已探索的岛屿保持原有地形和居民位置。");
            return true;
        }

        public static int ShopPrice(HabitatState s,string item,int legacyPrice)
            =>IsCampaignShop(s)&&HabitatRules.ShopPrice(s,item)>=0?HabitatRules.ShopPrice(s,item):legacyPrice;

        public static HabitatChapterStage Stage(HabitatState s)
        {
            if(s==null||!s.chapterOneAccepted)return HabitatChapterStage.MeetShopkeeper;
            if(s.chapterOneDelivered)return s.chapterOneMorningSeen?HabitatChapterStage.Complete:HabitatChapterStage.RestAtHome;
            if(!s.chapterOneSailed)return HabitatChapterStage.SailToIsland;
            if(!s.specialRod)return HabitatChapterStage.FindRod;
            return s.glassFish>=TargetFish?HabitatChapterStage.ReturnToShop:HabitatChapterStage.CatchFish;
        }

        public static bool Accept(HabitatState s)
        {
            if(s==null||s.chapterOneAccepted)return false;
            s.chapterOneAccepted=true;
            s.campaignUnlockedStage=System.Math.Max(1,s.campaignUnlockedStage);
            HabitatRules.Log(s,"接下委托：乘小船前往第一关，找到特制鱼竿，带回 2 条琉璃鱼。");return true;
        }

        public static bool RecordSailing(HabitatState s,int index)
        {
            if(s==null||!s.chapterOneAccepted||index!=MainLevel||s.chapterOneSailed)return false;
            s.chapterOneSailed=true;return true;
        }

        public static bool Deliver(HabitatState s)
        {
            if(s==null)return false;
            if(s.chapterOneDelivered){HabitatRules.Log(s,"这份委托已交付，报酬已经领取。");return false;}
            if(Stage(s)!=HabitatChapterStage.ReturnToShop)
            {HabitatRules.Log(s,"委托需要带回 2 条琉璃鱼；普通鱼虾不能代替。");return false;}
            s.glassFish-=TargetFish;s.coins+=RewardCoins;s.chapterOneDelivered=true;
            if(s.glassFish>0){s.fish+=s.glassFish;s.glassFish=0;}
            HabitatRules.Log(s,"已交付 2 条琉璃鱼，获得 100 贝币。回家休息，迎接第二天。");return true;
        }

        public static bool CompleteMorning(HabitatState s)
        {
            if(s==null||!s.chapterOneDelivered||s.chapterOneMorningSeen)return false;
            s.chapterOneMorningSeen=true;s.campaignUnlockedStage=System.Math.Max(2,s.campaignUnlockedStage);return true;
        }

        public static bool CanCatchGlassFish(HabitatState s,Animal animal,out string reason)
        {
            reason="";
            if(!IsMainIsland(s)||animal==null||animal.species!=Species.Goldfish||animal.carried
                ||s.animals==null||!s.animals.Contains(animal))
            {reason="请选择第一关水中的琉璃鱼。";return false;}
            if(!s.rod||!s.specialRod)
            {reason="普通鱼竿钓不起琉璃鱼：先找到岛上宝箱里的特制鱼竿。";return false;}
            if(!HabitatRules.IsClosedWaterArea(s,animal.x,animal.z))
            {reason="琉璃鱼仍与外海相通：先用地块或箱子封住水路，把它们围起来。";return false;}
            if(animal.offspring<=0&&animal.catchClaimed)
            {reason="这里还没有可捕捞的琉璃鱼后代；保留亲本，等待它们在封闭水域繁衍。";return false;}
            return true;
        }
    }
}
