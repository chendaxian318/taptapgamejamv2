using System;
using System.Collections.Generic;
using HabitatIsland;
public static class LatestPlanRuleChecks
{
    public static readonly List<string> Evidence=new List<string>();
    static int n;
    static Tuning tuning=new Tuning{animalMovement=false};
    static void Check(bool ok,string text){if(!ok)throw new Exception("FAIL "+text);Evidence.Add("PASS "+(++n)+": "+text);}
    static HabitatState Board(int level=7)
    {
        var s=new HabitatState{width=9,depth=7,tiles=new Tile[63],levelIndex=level,authoredLevel=true,playerX=2,playerZ=2,dockX=2,dockZ=2,coins=5000,backBasketUnlocked=true,rod=true,chapterOneAccepted=true,chapterOneDelivered=true,chapterOneMorningSeen=true,campaignUnlockedStage=4};
        for(int i=0;i<s.tiles.Length;i++)s.tiles[i]=new Tile{height=1};return s;
    }
    static Tile Q(HabitatState s,int x,int z)=>HabitatRules.TileAt(s,x,z);
    static Animal Add(HabitatState s,Species sp,int x,int z)
    {var a=new Animal{id=s.animals.Count+1,species=sp,x=x,z=z,homeX=x,homeZ=z};s.animals.Add(a);return a;}
    static HabitatState Fear(bool secondBlocked=false,int level=7)
    {
        var s=Board(level);var held=Add(s,Species.Shrimp,2,2);held.carried=true;s.carryingAnimal=held.id;
        for(int x=4;x<=7;x++)Q(s,x,2).height=0;
        if(level==4)for(int x=4;x<=7;x++)Q(s,x,2).height=1;
        Add(s,Species.Goldfish,4,2);if(secondBlocked)Q(s,6,2).prop=PropKind.Obstacle;
        return s;
    }
    public static string Run()
    {
        n=0;Evidence.Clear();
        var s=Board();s.backBasketUnlocked=false;var shrimp=Add(s,Species.Shrimp,3,2);
        Check(!HabitatRules.Interact(s,3,2)&&!shrimp.carried&&s.carryingAnimal<0,"formal shrimp capture requires unlocked basket");
        s.backBasketUnlocked=true;Check(HabitatRules.Interact(s,3,2)&&shrimp.carried,"basket unlock permits shrimp carrying");
        Q(s,3,2).prop=PropKind.Crate;Check(!HabitatRules.MovePlayer(s,1,0)&&Q(s,3,2).prop==PropKind.Crate,"carrying shrimp cannot push a crate");
        Check(HabitatRules.Evaluate(s,shrimp,tuning).suitable,"confirmed carried shrimp satisfies its amphibious condition");
        HabitatRules.Tick(s,tuning,20);Check(shrimp.offspring==0&&shrimp.progress==0,"carried habitat qualification does not breed extra catch inside the basket");
        s.levelIndex=0;Check(!HabitatRules.Evaluate(s,shrimp,tuning).suitable,"legacy carried shrimp still suspends breeding");
        s=Board();shrimp=Add(s,Species.Shrimp,3,2);Add(s,Species.Crab,3,3);
        Check(HabitatRules.Evaluate(s,shrimp,tuning).suitable,"confirmed shrimp does not inherit legacy isolation rule");
        s.levelIndex=0;Check(!HabitatRules.Evaluate(s,shrimp,tuning).suitable,"legacy shrimp isolation rule is retained");
        s=Board(0);s.backBasketUnlocked=false;shrimp=Add(s,Species.Shrimp,3,2);Check(HabitatRules.Interact(s,3,2),"legacy tutorial carrying remains unchanged");
        s=Fear();Check(HabitatRules.MovePlayer(s,1,0,false,tuning)&&s.animals[1].x==6,"approaching held shrimp scares goldfish two cells");
        s=Fear(true);Check(HabitatRules.MovePlayer(s,1,0,false,tuning)&&s.animals[1].x==5,"two-cell scare stops before blocked second cell");
        s=Fear();Q(s,5,2).height=1;Check(HabitatRules.MovePlayer(s,1,0,false,tuning)&&s.animals[1].x==4,"goldfish never jumps a dry first cell");
        s=Board();for(int x=3;x<9;x++)Q(s,x,2).height=0;
        var jelly=Add(s,Species.Jellyfish,5,2);
        Check(HabitatRules.Lure(s,5,2)&&jelly.x==4,"jellyfish at distance three is pulled one cell");
        jelly.x=6;Check(!HabitatRules.Lure(s,6,2)&&jelly.x==6,"jellyfish beyond three cells is rejected");
        jelly.species=Species.Goldfish;jelly.x=5;Check(!HabitatRules.Lure(s,5,2)&&jelly.x==5,"formal rod cannot lure goldfish");
        s.levelIndex=0;Check(HabitatRules.Lure(s,5,2)&&jelly.x==4,"legacy lure remains available");
        s=Board();var crab=Add(s,Species.Crab,3,2);
        Check(!HabitatRules.TryShedCrabShell(s,crab,tuning)&&!crab.shellShed,"inland crab does not shed");
        Q(s,4,2).height=0;var fish=Add(s,Species.Goldfish,4,2);
        Check(HabitatRules.TryShedCrabShell(s,crab,tuning)&&crab.shellShed&&Q(s,3,2).prop==PropKind.Shell,"suitable crab sheds one collectible shell on its square");
        Check(!HabitatRules.TryShedCrabShell(s,crab,tuning),"crab cannot shed twice");
        Check(HabitatRules.Interact(s,3,2)&&s.shells==1&&s.carryingAnimal<0,"shell can be picked up while crab remains on same square");
        Check(!HabitatRules.Interact(s,3,2)&&s.shells==1,"repeated crab interaction cannot duplicate shell");
        HabitatRules.Tick(s,tuning,1);Check(Q(s,3,2).prop==PropKind.None&&s.shells==1,"later ecology tick cannot regenerate collected shell");
        s=Board();crab=Add(s,Species.Crab,3,2);Q(s,4,2).height=0;Add(s,Species.Goldfish,4,2);Q(s,3,2).prop=PropKind.Road;
        Check(HabitatRules.TryShedCrabShell(s,crab,tuning)&&Q(s,3,2).coveredProp==PropKind.Road,"shell preserves underlying road");
        Check(HabitatRules.Interact(s,3,2)&&Q(s,3,2).prop==PropKind.Road,"collecting shell restores underlying road");
        s=Board();Q(s,3,2).height=0;fish=Add(s,Species.Goldfish,3,2);fish.offspring=0;
        Check(!HabitatRules.Fish(s,3,2)&&!fish.catchClaimed,"formal harvest blocked before level completion");
        s.won=true;Check(HabitatRules.Fish(s,3,2)&&s.fish==1&&s.animals.Contains(fish)&&fish.catchClaimed,"completed suitable fish immediately yields first catch and retains parent");
        Check(!HabitatRules.Fish(s,3,2)&&s.fish==1,"same first catch cannot be claimed twice");
        fish.offspring=2;Check(HabitatRules.Fish(s,3,2)&&s.fish==3&&fish.offspring==0,"later genuine offspring can be harvested once");
        Check(!HabitatRules.Fish(s,3,2)&&s.fish==3,"consumed offspring cannot be claimed twice");
        fish.offspring=1;for(int x=4;x<9;x++)Q(s,x,2).height=0;
        Check(!HabitatRules.Fish(s,3,2)&&fish.offspring==1,"harvest rechecks fish enclosure after completion");
        s=Board(8);s.won=true;for(int x=3;x<9;x++)Q(s,x,2).height=0;jelly=Add(s,Species.Jellyfish,5,2);jelly.offspring=0;
        Check(HabitatRules.Fish(s,5,2)&&s.jellyfish==1&&s.animals.Contains(jelly),"completed sea-connected jelly first catch enters inventory without waiting for offspring");
        jelly.offspring=1;jelly.x=6;Check(!HabitatRules.Fish(s,6,2)&&jelly.offspring==1,"jelly harvest is also capped at three cells");
        s=Board();s.won=true;shrimp=Add(s,Species.Shrimp,2,2);shrimp.carried=true;s.carryingAnimal=shrimp.id;
        Check(HabitatRules.CanStoreCarriedShrimp(s)&&HabitatRules.CatchSaleValue(s)==150,"finished carried shrimp is valued for sale");
        Check(HabitatRules.StoreCarriedShrimp(s)&&s.shrimp==1&&s.carryingAnimal<0&&!s.animals.Contains(shrimp),"carried shrimp is transferred once, with no island duplicate");
        Check(!HabitatRules.StoreCarriedShrimp(s)&&s.shrimp==1,"repeated shrimp transfer cannot duplicate catch");
        s=Board();shrimp=Add(s,Species.Shrimp,2,2);shrimp.carried=true;s.carryingAnimal=shrimp.id;
        Check(!HabitatRules.StoreCarriedShrimp(s)&&s.shrimp==0&&s.animals.Contains(shrimp),"unfinished island cannot convert its puzzle shrimp to sale inventory");
        foreach(var pair in new[]{new KeyValuePair<string,int>("crate",150),new KeyValuePair<string,int>("fan",200),new KeyValuePair<string,int>("shovel",300),new KeyValuePair<string,int>("magnet",300),new KeyValuePair<string,int>("launcher",400)})
        {
            s=Board();int before=s.coins;
            Check(HabitatRules.ShopPrice(s,pair.Key)==pair.Value,"published price "+pair.Key);
            Check(HabitatRules.Shop(s,pair.Key)&&s.coins==before-pair.Value,"transaction charges published price "+pair.Key);
        }
        s=Board();s.coins=149;Check(!HabitatRules.Shop(s,"crate")&&s.coins==149&&s.boatCrates==0,"insufficient crate funds leave money and cargo unchanged");
        s.coins=1000;s.boatCrates=3;Check(!HabitatRules.Shop(s,"crate")&&s.coins==1000,"full ship cannot charge another crate");
        s=Board();s.coins=299;Check(!HabitatRules.Shop(s,"shovel")&&s.shovels==0&&s.coins==299,"insufficient shovel funds do not grant inventory");
        s=Board();s.fish=s.shrimp=s.shells=s.jellyfish=1;
        Check(HabitatRules.CatchSaleValue(s)==750,"all confirmed catches use 100/150/200/300 sale values");
        int balance=s.coins;Check(HabitatRules.Shop(s,"sell")&&s.coins==balance+750&&s.deliveredShells==1,"sale grants actual total and records sold crab shell");
        Check(s.fish+s.shrimp+s.shells+s.jellyfish==0&&!HabitatRules.Shop(s,"sell")&&s.coins==balance+750,"sale clears catches and cannot pay twice");
        s=Board();s.fish=3;s.glassFish=9;balance=s.coins;
        Check(HabitatRules.SellCatch(s,Species.Goldfish,2)&&s.fish==1&&s.glassFish==9&&s.coins==balance+200,"quantity sale respects selected ordinary fish and never consumes commission fish");
        balance=s.coins;int deliveries=s.deliveryCount;
        Check(!HabitatRules.SellCatch(s,Species.Goldfish,2)&&s.fish==1&&s.coins==balance&&s.deliveryCount==deliveries,"insufficient quantity rejects whole sale without partial transaction");
        Check(!HabitatRules.SellCatch(s,Species.Goldfish,0)&&!HabitatRules.SellCatch(s,Species.Goldfish,-1)&&s.fish==1,"zero and negative quantities cannot create inventory or money");
        s.shells=2;Check(HabitatRules.SellCatch(s,Species.Crab,1)&&s.shells==1&&s.deliveredShells==1,"individual shell sale records chapter unlock evidence");
        s.jellyfish=2;balance=s.coins;Check(HabitatRules.SellCatch(s,Species.Jellyfish,1)&&s.jellyfish==1&&s.coins==balance+300,"selected jellyfish quantity sells at confirmed price");
        s.playerX=6;balance=s.coins;Check(!HabitatRules.SellCatch(s,Species.Jellyfish,1)&&s.jellyfish==1&&s.coins==balance,"quantity sale requires actual dock grid anchor");
        s=Board();s.won=true;s.shrimp=1;shrimp=Add(s,Species.Shrimp,2,2);shrimp.carried=true;s.carryingAnimal=shrimp.id;balance=s.coins;
        Check(HabitatRules.CatchCount(s,Species.Shrimp)==2,"sale selector includes legitimately carried completed-island shrimp");
        Check(!HabitatRules.SellCatch(s,Species.Shrimp,3)&&s.carryingAnimal==shrimp.id&&s.shrimp==1&&s.coins==balance,"failed quantity does not prematurely store or remove carried shrimp");
        Check(HabitatRules.SellCatch(s,Species.Shrimp,2)&&s.shrimp==0&&s.carryingAnimal<0&&s.coins==balance+300,"valid combined shrimp quantity stores and sells once");
        s=Board();s.won=true;Q(s,3,2).height=0;fish=Add(s,Species.Goldfish,3,2);fish.offspring=2;
        Check(HabitatRules.Fish(s,3,2)&&s.fish==1&&fish.offspring==2,"first immediate catch preserves already earned offspring");
        Check(HabitatRules.Fish(s,3,2)&&s.fish==3&&fish.offspring==0,"subsequent catch collects those preserved offspring exactly once");
        s=Board(10);s.backBasketUnlocked=false;Add(s,Species.Shrimp,3,2);
        Check(HabitatRules.UsesConfirmedPlan(s)&&!HabitatRules.Interact(s,3,2),"new fifth map uses confirmed basket rules without old index-range bug");
        s=Board();s.jellyfish=2;s.deliveredShells=3;s.campaignBriefedMask=6;s.seenGuideKeys.Add("rod");crab=Add(s,Species.Crab,3,2);crab.shellShed=true;
        crab.catchClaimed=true;var copy=HabitatRules.Clone(s);copy.seenGuideKeys.Clear();copy.animals[0].shellShed=false;copy.animals[0].catchClaimed=false;
        Check(copy.jellyfish==2&&copy.deliveredShells==3&&copy.campaignUnlockedStage==4&&copy.campaignBriefedMask==6,"clone preserves new economy and campaign fields");
        Check(s.seenGuideKeys.Count==1&&crab.shellShed&&crab.catchClaimed,"clone isolates guide list, first-catch and shed state");
        return "Latest-plan rule checks: "+n+" passed (controlled fixtures; not full campaign).";
    }
}
