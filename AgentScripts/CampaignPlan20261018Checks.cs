using System;
using System.Collections.Generic;
using HabitatIsland;

public static class CampaignPlan20261018Checks
{
    public static readonly List<string> Passed=new List<string>();
    static void Check(bool condition,string label)
    {if(!condition)throw new InvalidOperationException(label);Passed.Add(label);}
    static HabitatState Pond()
    {
        var s=new HabitatState{width=7,depth=7,tiles=new Tile[49],levelIndex=5,authoredLevel=true,
            playerX=2,playerZ=3,dockX=2,dockZ=3,rod=true,specialRod=true,coins=0,cargo=0,traps=0,won=true};
        for(int i=0;i<s.tiles.Length;i++)s.tiles[i]=new Tile{height=1};
        s.tiles[3*7+3].height=0;
        s.animals.Add(new Animal{id=1,species=Species.Goldfish,x=3,z=3,homeX=3,homeZ=3});
        return s;
    }
    public static string Run()
    {
        Passed.Clear();var s=Pond();var a=s.animals[0];
        Check(HabitatChapterRules.MainLevel==5,"First commission belongs to new formal island five");
        Check(HabitatChapterRules.CampaignLevel(5)==10&&HabitatChapterRules.CampaignNumber(10)==5,"Fifth island keeps stable saved index ten");
        HabitatJourneyRules.Normalize(s);
        Check(s.campaignUnlockedStage==1&&s.day==1,"New route starts with first island only");
        Check(HabitatJourneyRules.BeginNewJourney(s)&&s.fish==0&&s.shrimp==0&&s.coins==0,"Opening is presentation and grants no extra 600-coin catch");
        Check(!HabitatJourneyRules.BeginNewJourney(s),"Opening cannot replay a grant");
        Check(!HabitatChapterRules.CompleteMorning(s),"Rest before commission cannot unlock island two");
        Check(HabitatChapterRules.Accept(s)&&!HabitatChapterRules.Accept(s),"Commission acceptance is idempotent");
        Check(!HabitatChapterRules.RecordSailing(s,4)&&HabitatChapterRules.RecordSailing(s,5),"Old island does not satisfy new first departure");
        Check(!HabitatChapterRules.Deliver(s)&&s.coins==0,"Commission cannot complete without its catch");
        Check(HabitatChapterRules.CanCatchGlassFish(s,a,out string reason),"First formal fish is immediately available after habitat victory");
        Check(HabitatRules.Fish(s,3,3)&&s.glassFish==1&&a.catchClaimed&&s.animals.Count==1,"First harvest takes one fish and preserves parent");
        Check(!HabitatRules.Fish(s,3,3)&&s.glassFish==1,"Repeat without refresh cannot mint fish");
        HabitatRules.Tick(s,new Tuning{animalMovement=false,breedSeconds=1},1.1f);
        Check(a.offspring>0&&HabitatRules.Fish(s,3,3)&&s.glassFish==2,"Ecological refresh supplies second commission fish");
        Check(HabitatChapterRules.Deliver(s)&&s.coins==200&&s.glassFish==0,"Two fish pay exactly 200 once without a second bonus");
        Check(!HabitatChapterRules.Deliver(s)&&s.coins==200,"Duplicate delivery cannot pay again");
        Check(HabitatChapterRules.CompleteMorning(s)&&!HabitatChapterRules.CompleteMorning(s)&&HabitatChapterRules.UnlockedStage(s)==2,"Morning opens second island once");
        Check(HabitatChapterRules.RecordBriefing(s,2,true)&&!HabitatChapterRules.RecordBriefing(s,2,true),"Second briefing recorded once");
        Check(HabitatChapterRules.CanBuyItem(s,"fan",out reason)&&HabitatRules.ShopPrice(s,"fan")==200,"First delivery supplies exactly the required 200-coin fan");
        Check(HabitatChapterRules.ShopPrice(s,"crate",6)==150,"Legacy chapter price adapter uses the authoritative formal price table");
        Check(!HabitatChapterRules.CanBuyItem(s,"launcher",out reason)&&!HabitatChapterRules.CanBuyItem(s,"magnet",out reason),"Later tools remain locked");
        foreach(string item in new[]{"soil","float","tide","rod","trap","road","basket"})
            Check(!HabitatChapterRules.CanBuyItem(s,item,out reason),"Formal shop rejects undocumented purchase: "+item);
        Check(!HabitatChapterRules.RecordBriefing(s,3,false),"Third island cannot skip second victory");
        Check(HabitatChapterRules.RecordBriefing(s,3,true)&&HabitatChapterRules.CanBuyItem(s,"launcher",out reason),"Second victory and briefing open third island and launcher");
        Check(!HabitatChapterRules.RecordBriefing(s,4,true),"Fourth island requires sold crab shell");
        s.shells=1;int coins=s.coins;
        Check(HabitatRules.Shop(s,"sell")&&s.coins==coins+200&&s.deliveredShells==1&&s.shells==0,"Shell sale earns 200 and records proof exactly once");
        Check(!HabitatRules.Shop(s,"sell")&&s.deliveredShells==1,"Empty repeated shell sale pays nothing");
        Check(HabitatChapterRules.RecordBriefing(s,4,true)&&HabitatChapterRules.CanBuyItem(s,"magnet",out reason),"Sold shell and third victory open fourth island and magnet");
        Check(!HabitatChapterRules.RecordBriefing(s,5,false)&&HabitatChapterRules.RecordBriefing(s,5,true)&&HabitatChapterRules.UnlockedStage(s)==5,"Fourth victory opens stable fifth challenge");
        a.offspring=1;coins=s.coins;
        Check(HabitatRules.Fish(s,3,3)&&s.fish==1&&s.glassFish==0&&HabitatRules.Shop(s,"sell")&&s.coins==coins+100,"Revisited first-island fish can fund later tools after commission");
        s.jellyfish=2;s.seenGuideKeys.Add("rod");var clone=HabitatRules.Clone(s);
        Check(clone.campaignUnlockedStage==5&&clone.campaignBriefedMask==s.campaignBriefedMask&&clone.deliveredShells==1&&clone.jellyfish==2,"Clone preserves campaign and catch fields");
        clone.seenGuideKeys.Add("new");Check(s.seenGuideKeys.Count==1,"Clone guide state is independent");
        var destination=Pond();destination.tiles[0].height=4;HabitatJourneyRules.CopySharedProgress(s,destination);
        Check(destination.campaignUnlockedStage==5&&destination.campaignBriefedMask==s.campaignBriefedMask&&destination.deliveredShells==1&&destination.jellyfish==2,"Travel carries campaign, shell proof and jellyfish inventory");
        Check(destination.tiles[0].height==4&&destination.animals[0].x==3,"Travel does not reset destination terrain or residents");
        destination.seenGuideKeys.Add("boat");Check(s.seenGuideKeys.Count==1,"Travel guide state is independent");
        var legacy=Pond();legacy.levelIndex=4;legacy.rodFromChest=true;legacy.specialRod=false;legacy.day=0;legacy.fish=7;legacy.shrimp=4;legacy.chapterOneMorningSeen=true;legacy.campaignUnlockedStage=0;
        HabitatJourneyRules.Normalize(legacy);
        Check(legacy.specialRod&&legacy.day==1&&legacy.fish==7&&legacy.shrimp==4&&legacy.campaignUnlockedStage==2,"Old save normalizes route and proven rod without deleting inventory");
        return "PASS "+Passed.Count+" campaign plan checks";
    }
}
