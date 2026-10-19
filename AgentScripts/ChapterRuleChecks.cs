using System;
using System.Collections.Generic;
using HabitatIsland;

public static class ChapterRuleChecks
{
    public static readonly List<string> Passed=new List<string>();
    static void Check(bool condition,string label)
    {if(!condition)throw new InvalidOperationException(label);Passed.Add(label);}
    static HabitatState Pond(bool main=true)
    {
        var s=new HabitatState{width=7,depth=7,tiles=new Tile[49],levelIndex=main?4:1,authoredLevel=main,
            playerX=2,playerZ=3,dockX=2,dockZ=3,rod=true,coins=19,fish=3,shrimp=2};
        for(int i=0;i<s.tiles.Length;i++)s.tiles[i]=new Tile{height=1};
        for(int z=3;z<=4;z++)for(int x=3;x<=4;x++)s.tiles[z*7+x].height=0;
        s.animals.Add(new Animal{id=1,species=Species.Goldfish,x=3,z=3,homeX=3,homeZ=3,offspring=2});
        return s;
    }
    static bool FlagsEqual(HabitatState a,HabitatState b)
        =>a.chapterOneAccepted==b.chapterOneAccepted&&a.chapterOneSailed==b.chapterOneSailed
        &&a.chapterOneDelivered==b.chapterOneDelivered&&a.chapterOneMorningSeen==b.chapterOneMorningSeen
        &&a.specialRod==b.specialRod&&a.glassFish==b.glassFish&&a.glassFishCaught==b.glassFishCaught;

    public static string Run()
    {
        Passed.Clear();var s=Pond();var a=s.animals[0];
        Check(HabitatChapterRules.TargetFish==2&&HabitatChapterRules.RewardCoins==200,"PDF commission is exactly two glass fish for 200 coins");
        Check(HabitatChapterRules.IsMainIsland(s)&&!HabitatChapterRules.IsMainIsland(Pond(false)),"Special catch only applies to authored island four");
        Check(HabitatChapterRules.Stage(s)==HabitatChapterStage.MeetShopkeeper,"Ordinary opening fish do not create an accepted commission");
        Check(!HabitatChapterRules.RecordSailing(s,4)&&!s.chapterOneSailed,"Travel before acceptance cannot advance commission");
        Check(!HabitatChapterRules.Deliver(s)&&s.coins==19&&s.fish==3,"Opening fish cannot pay commission before acceptance");
        Check(!HabitatChapterRules.CompleteMorning(s)&&!s.chapterOneMorningSeen,"Rest before delivery cannot finish chapter");
        Check(HabitatChapterRules.Accept(s)&&!HabitatChapterRules.Accept(s)&&HabitatChapterRules.Stage(s)==HabitatChapterStage.SailToIsland,"Accept once then request real sailing");
        Check(!HabitatChapterRules.RecordSailing(s,0)&&!s.chapterOneSailed,"Travel to the ecology tutorial cannot satisfy chapter sailing");
        Check(HabitatChapterRules.RecordSailing(s,4)&&!HabitatChapterRules.RecordSailing(s,4)&&HabitatChapterRules.Stage(s)==HabitatChapterStage.FindRod,"Main-island sailing records once and requires rod");
        Check(!HabitatChapterRules.CanCatchGlassFish(s,a,out string reason)&&reason.Contains("特制鱼竿"),"Ordinary rod is rejected with the chest instruction");
        Check(!HabitatRules.Fish(s,3,3)&&a.offspring==2&&s.glassFish==0&&s.fish==3,"Direct catch bypass cannot use ordinary rod or consume offspring");
        s.rod=false;s.coins=100;
        Check(HabitatRules.Shop(s,"rod")&&s.rod&&!s.specialRod&&!HabitatRules.Fish(s,3,3)&&a.offspring==2,"Buying ordinary shop rod does not unlock special catch");
        s.tiles[3*7+1].prop=PropKind.Chest;s.tiles[3*7+1].chestReward=ChestReward.FishingRod;
        Check(HabitatRules.Interact(s,1,3)&&s.rod&&s.specialRod&&s.rodFromChest,"Main-island fishing-rod chest grants persistent special rod");
        Check(!HabitatRules.Interact(s,1,3)&&s.openedChests==1,"Chest cannot reward twice");
        Check(HabitatChapterRules.Stage(s)==HabitatChapterStage.CatchFish,"Special rod advances to capture without inventing fish");
        Check(HabitatRules.IsClosedWaterArea(s,3,3)&&HabitatChapterRules.CanCatchGlassFish(s,a,out reason),"Closed pond with offspring permits special catch");
        s.tiles[3*7+0].height=0;s.tiles[3*7+1].height=0;s.tiles[3*7+2].height=0;
        Check(!HabitatRules.IsClosedWaterArea(s,3,3)&&!HabitatChapterRules.CanCatchGlassFish(s,a,out reason)&&reason.Contains("外海"),"Opening waterway immediately invalidates enclosure");
        Check(!HabitatRules.Fish(s,3,3)&&a.offspring==2&&s.glassFishCaught==0&&s.glassFish==0,"Old offspring cannot be caught after reopening pond");
        s.tiles[3*7+1].prop=PropKind.Float;
        Check(HabitatRules.IsClosedWaterArea(s,3,3)&&HabitatChapterRules.CanCatchGlassFish(s,a,out reason),"Existing floating box can close the waterway");
        a.offspring=0;
        Check(!HabitatChapterRules.CanCatchGlassFish(s,a,out reason)&&!HabitatRules.Fish(s,3,3),"Parent cannot be caught while there are no offspring");
        a.offspring=3;int parentId=a.id;
        Check(HabitatRules.Fish(s,3,3)&&s.glassFish==3&&s.glassFishCaught==3&&s.fish==3&&a.offspring==0&&s.animals.Count==1&&a.id==parentId,"Actual catch creates distinct glass-fish inventory and retains parent");
        Check(HabitatChapterRules.Stage(s)==HabitatChapterStage.ReturnToShop,"Two or more distinct glass fish request return to shop");
        int beforeCoins=s.coins;var beforeDelivery=HabitatRules.Clone(s);
        Check(HabitatChapterRules.Deliver(s)&&s.glassFish==1&&s.coins==beforeCoins+200&&s.fish==3&&s.shrimp==2,"Delivery atomically consumes two glass fish and awards 200 coins once");
        Check(!HabitatChapterRules.Deliver(s)&&s.glassFish==1&&s.coins==beforeCoins+200,"Repeated delivery cannot double reward or consume surplus");
        Check(s.glassFishCaught==3&&HabitatChapterRules.Stage(s)==HabitatChapterStage.RestAtHome,"Delivery preserves lifetime catch count and waits for home morning");
        Check(HabitatChapterRules.CompleteMorning(s)&&!HabitatChapterRules.CompleteMorning(s)&&HabitatChapterRules.Stage(s)==HabitatChapterStage.Complete,"Only post-delivery morning completes once");
        Check(!beforeDelivery.chapterOneDelivered&&beforeDelivery.glassFish==3&&beforeDelivery.coins==beforeCoins,"Undo snapshot preserves full pre-delivery economics");
        var clone=HabitatRules.Clone(s);
        Check(FlagsEqual(s,clone),"Manual clone preserves all seven chapter fields");
        var dest=Pond(false);var tiles=dest.tiles;var animals=dest.animals;
        HabitatJourneyRules.CopySharedProgress(s,dest);
        Check(FlagsEqual(s,dest)&&ReferenceEquals(tiles,dest.tiles)&&ReferenceEquals(animals,dest.animals),"Travel copies all chapter progress without changing destination terrain or animals");
        var old=Pond();old.rodFromChest=true;old.rod=false;HabitatJourneyRules.Normalize(old);
        Check(old.rod&&old.specialRod&&!old.chapterOneAccepted&&old.glassFish==0,"Legacy main-island rod chest migrates only special equipment");
        old=Pond();HabitatJourneyRules.Normalize(old);
        Check(!old.specialRod&&old.glassFish==0&&!old.chapterOneAccepted,"Legacy ordinary rod and opening fish do not fabricate special equipment or quest progress");
        old=Pond(false);old.rodFromChest=true;HabitatJourneyRules.Normalize(old);
        Check(!old.specialRod,"A different island's rod chest is not proof of special rod");
        old=Pond(false);
        Check(HabitatRules.Fish(old,3,3)&&old.fish==5&&old.glassFish==0&&old.glassFishCaught==0,"Other practice maps retain ordinary fishing rules and inventory");
        old=Pond();old.specialRod=true;old.glassFish=1;old.chapterOneAccepted=old.chapterOneSailed=true;int coins=old.coins;
        Check(!HabitatChapterRules.Deliver(old)&&old.glassFish==1&&old.coins==coins&&!old.chapterOneDelivered,"Insufficient glass fish cause no partial delivery");
        old=Pond(false);old.glassFish=2;
        Check(HabitatRules.Shop(old,"sell")&&old.glassFish==2,"Ordinary sell-all cannot silently sell commission catch");
        old=Pond();old.specialRod=true;old.animals[0].carried=true;
        Check(!HabitatChapterRules.CanCatchGlassFish(old,old.animals[0],out reason),"Carried or invalid animal cannot be used as a glass-fish target");
        Check(!HabitatRules.IsClosedWaterArea(old,0,0)&&!HabitatRules.IsClosedWaterArea(null,0,0),"Dry and missing targets are not enclosed ponds");
        var real=HabitatFigJamLevel.Create();var realFish=real.animals.FindAll(f=>f.species==Species.Goldfish);
        Check(real.tiles.Length==342&&realFish.Count==2&&realFish[0].x==7&&realFish[0].z==9&&realFish[1].x==8&&realFish[1].z==9&&realFish.TrueForAll(f=>f.offspring==0),"Authored-map regression starts with its exact original two fish and no fabricated offspring");
        HabitatJourneyRules.BeginNewJourney(real);HabitatRules.Shop(real,"sell");
        Check(HabitatRules.Shop(real,"soil")&&real.carryingProp==PropKind.Climb,"Real opening catch can purchase the first soil block");
        real.playerX=6;real.playerZ=9;
        Check(HabitatRules.PlaceSoil(real,7,8),"Existing placement rule closes first authored waterway at 7,8");
        real.playerX=real.dockX;real.playerZ=real.dockZ;
        Check(HabitatRules.Shop(real,"soil"),"Remaining legitimate opening money buys second soil block");
        real.playerX=9;real.playerZ=9;
        Check(HabitatRules.PlaceSoil(real,8,8),"Existing placement rule closes second authored waterway at 8,8");
        var normalTuning=new Tuning();
        foreach(var originalFish in realFish)
        {
            var report=HabitatRules.Evaluate(real,originalFish,normalTuning);
            Check(report.areaClosed&&report.waterSize==2&&report.suitable,"Original authored fish can breed in its actual closed two-cell pond");
        }
        HabitatRules.Tick(real,normalTuning,18.1f);
        Check(realFish.TrueForAll(f=>f.offspring==1&&f.spawnedTotal==1)&&real.animals.FindAll(f=>f.species==Species.Goldfish).Count==2,"Default 18-second autonomous ecology produces one real offspring per original parent");
        Check(real.glassFish==0&&real.glassFishCaught==0,"Natural breeding alone does not fabricate caught commission inventory");
        real.playerX=12;real.playerZ=13;
        Check(HabitatRules.Interact(real,12,14)&&real.specialRod,"Actual authored chest grants special rod without map edits");
        real.playerX=9;real.playerZ=9;
        Check(HabitatRules.Fish(real,realFish[0].x,realFish[0].z)&&HabitatRules.Fish(real,realFish[1].x,realFish[1].z)&&real.glassFish==2,"The two naturally bred authored offspring can complete actual capture requirement");
        old=Pond(false);old.tiles[4*7+3].height=1;old.tiles[4*7+4].height=1;
        Check(!HabitatRules.Evaluate(old,old.animals[0],new Tuning()).suitable,"Other practice maps retain their four-cell pond tuning");
        old=Pond();old.tiles[3*7+4].height=1;old.tiles[4*7+3].height=1;old.tiles[4*7+4].height=1;
        Check(HabitatRules.Evaluate(old,old.animals[0],new Tuning{minPond=99}).suitable,"Campaign accepts even one enclosed fish cell without importing laboratory pond-size tuning");
        old=Pond();old.coins=250;
        Check(!HabitatChapterRules.CanBuyItem(old,"FAN",out reason)&&HabitatChapterRules.ShopPrice(old,"fan",10)==200,"Campaign fan is visibly locked before delivery and priced at 200");
        Check(!HabitatRules.Shop(old,"fan")&&old.coins==250&&old.carryingProp==PropKind.None,"Direct shop call cannot bypass campaign fan lock");
        old.chapterOneAccepted=old.chapterOneSailed=old.chapterOneDelivered=true;
        Check(HabitatChapterRules.CanBuyItem(old,"fan",out reason)&&HabitatRules.Shop(old,"fan")&&old.coins==50&&old.carryingProp==PropKind.Fan,"Delivered commission unlocks fan at exactly 200 coins");
        old=Pond(false);old.coins=50;
        Check(HabitatChapterRules.CanBuyItem(old,"fan",out reason)&&HabitatChapterRules.ShopPrice(old,"fan",10)==10&&HabitatRules.Shop(old,"fan")&&old.coins==40,"Standalone old practice retains its original fan economy");
        old=Pond(false);old.chapterOneAccepted=true;old.coins=250;
        Check(!HabitatRules.Shop(old,"fan")&&old.coins==250,"Travelling to optional practice cannot bypass active campaign fan lock");
        old.chapterOneDelivered=true;
        Check(HabitatRules.Shop(old,"fan")&&old.coins==50,"Unlocked campaign fan price persists across islands");
        old.carryingProp=PropKind.None;old.coins=199;
        Check(!HabitatRules.Shop(old,"fan")&&old.coins==199&&old.carryingProp==PropKind.None,"Unlocked fan still requires full 200-coin payment atomically");
        Check(HabitatChapterRules.ShopPrice(old,"crate",6)==6&&HabitatChapterRules.CanBuyItem(old,"crate",out reason),"Other item prices and availability remain unchanged");
        return "PASS "+Passed.Count+" chapter rule checks; distinct inventory, enforced enclosure, special rod, single delivery and persistent progression.";
    }
}
