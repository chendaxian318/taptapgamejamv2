using System;

namespace HabitatIsland
{
    // Journey progress belongs to the player; terrain and wildlife belong to each map.
    public static class HabitatJourneyRules
    {
        // The 2026-10-18 opening catch is a cinematic prop. The formal route's
        // economy starts with the two earned fish, not an extra saleable grant.
        public const int OpeningFish=0,OpeningShrimp=0;
        public const int FishSellPrice=8,ShrimpSellPrice=5;

        public static void Normalize(HabitatState s)
        {
            if(s==null)throw new ArgumentNullException(nameof(s));
            // Reading an older save must never award a new opening catch.
            if(s.day<1)s.day=1;
            if(float.IsNaN(s.playSeconds)||float.IsInfinity(s.playSeconds)||s.playSeconds<0)s.playSeconds=0;
            if(s.bagProps==null)s.bagProps=new System.Collections.Generic.List<PropKind>();
            if(s.seenGuideKeys==null)s.seenGuideKeys=new System.Collections.Generic.List<string>();
            s.campaignUnlockedStage=HabitatChapterRules.UnlockedStage(s);
            // A legacy main-island chest is proof of the special rod; an ordinary shop rod is not.
            if((HabitatChapterRules.IsMainIsland(s)||s.levelIndex==HabitatFigJamLevel.Index)&&s.rodFromChest){s.specialRod=true;s.rod=true;}
        }

        public static bool BeginNewJourney(HabitatState s)
        {
            Normalize(s);
            if(s.openingCatchGranted||s.openingCompleted)return false;
            s.fish+=OpeningFish;s.shrimp+=OpeningShrimp;s.openingCatchGranted=true;
            return true;
        }

        public static void CompleteOpening(HabitatState s)
        {Normalize(s);s.openingCompleted=true;}

        public static void AdvanceDay(HabitatState s)
        {Normalize(s);s.day++;}

        public static void CopySharedProgress(HabitatState source,HabitatState dest)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            if(dest==null)throw new ArgumentNullException(nameof(dest));
            dest.coins=source.coins;dest.cargo=source.cargo;dest.fish=source.fish;dest.shrimp=source.shrimp;
            dest.playSeconds=source.playSeconds;
            dest.rod=source.rod;dest.traps=source.traps;
            dest.day=source.day;dest.openingCompleted=source.openingCompleted;dest.openingCatchGranted=source.openingCatchGranted;
            dest.deliveredFish=source.deliveredFish;dest.deliveredShrimp=source.deliveredShrimp;
            dest.deliveryCount=source.deliveryCount;dest.deliveryIncome=source.deliveryIncome;
            dest.tutorialOfferPending=source.tutorialOfferPending;dest.tutorialCompleted=source.tutorialCompleted;
            dest.chapterOneAccepted=source.chapterOneAccepted;dest.chapterOneSailed=source.chapterOneSailed;
            dest.chapterOneDelivered=source.chapterOneDelivered;dest.chapterOneMorningSeen=source.chapterOneMorningSeen;
            dest.firstClearCgSeen=source.firstClearCgSeen;dest.specialRod=source.specialRod;dest.glassFish=source.glassFish;dest.glassFishCaught=source.glassFishCaught;
            dest.campaignUnlockedStage=source.campaignUnlockedStage;dest.campaignBriefedMask=source.campaignBriefedMask;
            dest.jellyfish=source.jellyfish;dest.deliveredShells=source.deliveredShells;
            dest.seenGuideKeys=source.seenGuideKeys==null?new System.Collections.Generic.List<string>():new System.Collections.Generic.List<string>(source.seenGuideKeys);
            dest.backBasketUnlocked=source.backBasketUnlocked;dest.shells=source.shells;
            dest.boatCrates=source.boatCrates;dest.shovels=source.shovels;dest.magnets=source.magnets;dest.launchers=source.launchers;
            dest.bagProps=source.bagProps==null?new System.Collections.Generic.List<PropKind>():new System.Collections.Generic.List<PropKind>(source.bagProps);
            Normalize(dest);
        }
    }
}
