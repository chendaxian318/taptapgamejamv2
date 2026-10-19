using System;
using System.Collections.Generic;
using HabitatIsland;

// Standalone pure C# checks; no Unity assemblies, scene, game singleton or asset writes.
public static class TutorialRuleChecks
{
    public static readonly List<string> Passed=new List<string>();
    static void Check(bool condition,string label)
    {if(!condition)throw new InvalidOperationException(label);Passed.Add(label);}

    static HabitatState Fixture()
    {
        var s=new HabitatState{playerX=1,playerZ=2,dockX=1,dockZ=2,tiles=new Tile[81],
            goals=new[]{Species.Crab},tutorialOfferPending=true};
        for(int z=0;z<9;z++)for(int x=0;x<9;x++)s.tiles[z*9+x]=new Tile{height=x==0||z==0||x==8||z==8?-1:1};
        for(int z=3;z<=4;z++)for(int x=3;x<=4;x++)s.tiles[z*9+x].height=0;
        s.tiles[12].height=3;s.tiles[13].height=3;s.tiles[20].trap=true;s.tiles[28].trap=true;
        s.animals.Add(new Animal{id=1,species=Species.Crab,x=3,z=2,homeX=3,homeZ=2});
        s.animals.Add(new Animal{id=2,species=Species.Jellyfish,x=3,z=3,homeX=3,homeZ=3});
        s.animals.Add(new Animal{id=3,species=Species.Starfish,x=4,z=2,homeX=4,homeZ=2});
        s.animals.Add(new Animal{id=4,species=Species.Shrimp,x=2,z=2,homeX=2,homeZ=2});
        return s;
    }

    public static string Run()
    {
        Passed.Clear();var tuning=new Tuning();var s=Fixture();
        Check(HabitatTutorialRules.IsTutorial(s),"Only tutorial map is eligible");
        Check(HabitatTutorialRules.Evaluate(s,tuning,true)==HabitatTutorialStep.ObserveCrab,"Initial shore step observes crab");
        Check(!s.tutorialObservedCrab&&s.elapsed==0&&s.events.Count==0,"Evaluate leaves flags clock and logs unchanged");
        s.onHarbor=true;s.selectedX=3;s.selectedZ=2;
        Check(HabitatTutorialRules.Evaluate(s,tuning,true)==HabitatTutorialStep.Arrival,"Boat arrival has priority");
        Check(!HabitatTutorialRules.Observe(s,tuning)&&!s.tutorialObservedCrab,"Harbor observation cannot advance island lesson");
        s.onHarbor=false;s.selectedX=2;
        Check(!HabitatTutorialRules.Observe(s,tuning),"Selecting shrimp does not count as observing crab");
        s.selectedX=3;
        Check(HabitatTutorialRules.Observe(s,tuning)&&s.tutorialObservedCrab,"Selecting actual crab records observation");
        Check(!HabitatTutorialRules.Observe(s,tuning),"Repeated observation is idempotent");
        Check(HabitatTutorialRules.Evaluate(s,tuning,true)==HabitatTutorialStep.PickShrimp,"Observed crab leads to pickup");
        s.animals[0].x=5;
        Check(HabitatTutorialRules.TryTarget(HabitatTutorialStep.ObserveCrab,s,out int x,out int z)&&x==5&&z==2,"Observation target follows moved crab");
        s.animals[0].x=3;s.carryingProp=PropKind.Fan;
        Check(HabitatTutorialRules.Body(HabitatTutorialStep.PickShrimp,s,tuning).Contains("其他动物或道具"),"Wrong held item gives recovery instructions");
        s.carryingProp=PropKind.None;
        Check(HabitatRules.Interact(s,2,2),"Real rule can pick teaching shrimp");
        Check(HabitatTutorialRules.Evaluate(s,tuning,true)==HabitatTutorialStep.PlaceShrimp,"Carried shrimp takes priority");
        var beforePlacement=HabitatRules.Clone(s);
        Check(HabitatRules.Interact(s,1,3),"Real rule can place shrimp in target trap");
        Check(HabitatTutorialRules.Evaluate(s,tuning,true)==HabitatTutorialStep.ObserveHabitat,"Placement requires habitat observation");
        s.selectedX=1;s.selectedZ=3;
        Check(!HabitatTutorialRules.Observe(s,tuning)&&!s.tutorialObservedHabitat,"Selected trap cannot satisfy habitat observation");
        s.selectedX=3;s.selectedZ=2;
        Check(HabitatTutorialRules.Observe(s,tuning)&&s.tutorialObservedHabitat,"Selected suitable crab satisfies habitat observation");
        Check(HabitatTutorialRules.Evaluate(s,tuning,true)==HabitatTutorialStep.Resume,"Paused ecology explicitly asks to resume");
        Check(HabitatTutorialRules.Evaluate(s,tuning,false)==HabitatTutorialStep.Wait,"Unpaused suitable ecology waits for actual breeding");
        Check(HabitatTutorialRules.Body(HabitatTutorialStep.Wait,s,tuning).Contains("18"),"Wait text uses actual tuning duration");
        s.animals[0].x=7;
        Check(HabitatTutorialRules.Evaluate(s,tuning,false)==HabitatTutorialStep.ObserveHabitat&&HabitatTutorialRules.Body(HabitatTutorialStep.ObserveHabitat,s,tuning).Contains("不适居"),"Moved unsuitable crab cannot be told to keep waiting");
        s.animals[0].x=3;
        Check(HabitatTutorialRules.Evaluate(beforePlacement,tuning,true)==HabitatTutorialStep.PlaceShrimp&&!beforePlacement.tutorialObservedHabitat,"Undo snapshot restores held shrimp and pre-observation progress");
        HabitatRules.Tick(s,tuning,18.1f);
        Check(s.animals[0].spawnedTotal>0&&s.won&&HabitatTutorialRules.Evaluate(s,tuning,false)==HabitatTutorialStep.Complete,"Default live ecology reaches true completion");
        Check(HabitatTutorialRules.Observe(s,tuning)&&s.tutorialCompleted&&!s.tutorialOfferPending,"True completion records global result and clears offer");
        Check(!HabitatTutorialRules.Observe(s,tuning),"Repeated completion is idempotent");
        s.tutorialGuideDismissed=true;var copy=HabitatRules.Clone(s);
        Check(copy.tutorialCompleted&&copy.tutorialGuideDismissed&&copy.tutorialObservedCrab&&copy.tutorialObservedHabitat&&!copy.tutorialOfferPending,"Clone preserves all five tutorial fields");
        var destination=Fixture();var terrain=destination.tiles;var wildlife=destination.animals;
        HabitatJourneyRules.CopySharedProgress(s,destination);
        Check(destination.tutorialCompleted&&!destination.tutorialOfferPending&&!destination.tutorialGuideDismissed&&!destination.tutorialObservedCrab&&!destination.tutorialObservedHabitat,"Cross-map copy transfers only two global tutorial fields");
        Check(ReferenceEquals(terrain,destination.tiles)&&ReferenceEquals(wildlife,destination.animals),"Cross-map copy preserves destination map and wildlife");
        s=Fixture();s.won=true;
        Check(!HabitatTutorialRules.Observe(s,tuning)&&!s.tutorialCompleted,"Won flag without actual crab breeding cannot finish");
        s.won=false;s.animals[0].spawnedTotal=1;
        Check(!HabitatTutorialRules.Observe(s,tuning)&&!s.tutorialCompleted,"Spawn count without earned or goal proof cannot finish");
        s.earnedSpecies.Add(Species.Crab);
        Check(HabitatTutorialRules.Observe(s,tuning)&&s.tutorialCompleted,"Actual crab breeding plus earned proof permits completion");
        s=Fixture();s.levelIndex=1;s.won=true;s.animals[0].spawnedTotal=1;
        Check(!HabitatTutorialRules.Observe(s,tuning)&&!s.tutorialCompleted,"Other maps cannot complete introductory lesson");
        s=Fixture();s.animals.Clear();
        Check(HabitatTutorialRules.Body(HabitatTutorialStep.ObserveCrab,s,tuning).Contains("找不到螃蟹")&&!HabitatTutorialRules.TryTarget(HabitatTutorialStep.ObserveCrab,s,out x,out z),"Missing crab produces safe instructions without invented target");
        s=Fixture();s.tiles[28].trap=false;
        Check(HabitatTutorialRules.Body(HabitatTutorialStep.PlaceShrimp,s,tuning).Contains("已不在原处")&&!HabitatTutorialRules.TryTarget(HabitatTutorialStep.PlaceShrimp,s,out x,out z),"Missing trap reports recovery rather than false placement guidance");
        return "PASS "+Passed.Count+" pure tutorial checks; no Unity API or editor process used.";
    }
}
