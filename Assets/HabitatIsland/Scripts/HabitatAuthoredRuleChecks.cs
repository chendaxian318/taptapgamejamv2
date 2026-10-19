using System;

namespace HabitatIsland
{
    /// <summary>Pure model checks for the authored FigJam map; no scene, build or fabricated victory condition.</summary>
    public static class HabitatAuthoredRuleChecks
    {
        static int assertions;
        static void Check(bool condition,string message)
        { assertions++;if(!condition)throw new InvalidOperationException("Authored rule check failed: "+message); }
        static HabitatState Flat(int height=1)
        {
            var s=new HabitatState{width=9,depth=9,playerX=1,playerZ=1,authoredLevel=true,sourceNodeId="0:1",rod=false};
            s.tiles=new Tile[s.width*s.depth];
            for(int i=0;i<s.tiles.Length;i++)s.tiles[i]=new Tile{height=height};
            return s;
        }
        static Tile Q(HabitatState s,int x,int z){return HabitatRules.TileAt(s,x,z);}
        static Animal Add(HabitatState s,Species species,int x,int z)
        {
            var a=new Animal{id=s.animals.Count+1,species=species,x=x,z=z,homeX=x,homeZ=z};
            s.animals.Add(a);return a;
        }
        static HabitatState Approach(out Animal shrimp,out Animal target,Species species=Species.Crab,int height=1,bool targetFirst=false)
        {
            var s=Flat(height);
            // The shrimp has exactly one legal autonomous move: (2,3) -> (3,3).
            Q(s,1,3).prop=Q(s,2,2).prop=Q(s,2,4).prop=PropKind.Obstacle;
            if(targetFirst){target=Add(s,species,4,3);shrimp=Add(s,Species.Shrimp,2,3);}
            else{shrimp=Add(s,Species.Shrimp,2,3);target=Add(s,species,4,3);}
            shrimp.moveClock=.99f;return s;
        }
        static void StepApproach(HabitatState s)
        { HabitatRules.Tick(s,new Tuning{animalMovement=true,moveSeconds=1,breedSeconds=100},.02f); }

        public static string Run()
        {
            assertions=0;
            Check((int)PropKind.Climb==3&&(int)PropKind.Chest==8&&(int)PropKind.ClimbBlock==9,"new block enum preserves saved prop values");
            Check(new Tile().chestReward==ChestReward.Coins,"legacy chests retain coin reward by default");
            Check(new HabitatState().dockRotation==-1,"legacy maps retain inferred dock orientation");

            var s=Flat();int initialCoins=s.coins;
            Q(s,2,1).prop=PropKind.Chest;Q(s,2,1).chestReward=ChestReward.FishingRod;
            Check(HabitatRules.Interact(s,2,1)&&s.rod&&s.rodFromChest&&s.openedChests==1&&s.coins==initialCoins,"rod chest unlocks equipment without invented coins");
            Check(!HabitatRules.Interact(s,2,1)&&s.openedChests==1,"opened chest cannot reward or count twice");
            Q(s,1,2).prop=PropKind.Chest;Q(s,1,2).chestReward=ChestReward.Unspecified;
            Check(HabitatRules.Interact(s,1,2)&&s.openedChests==2&&s.coins==initialCoins,"unspecified chest only records opening");
            Q(s,0,1).prop=PropKind.Chest;
            Check(HabitatRules.Interact(s,0,1)&&s.openedChests==3&&s.coins==initialCoins+20,"legacy coin chest still works");
            s.crabMovedByShrimp=true;s.tideEverActivated=true;s.dockRotation=3;
            HabitatRules.Tick(s,new Tuning{animalMovement=false},1);
            Check(!s.won&&s.goals.Length==0,"authored checkpoints do not invent an unspecified victory");
            var clone=HabitatRules.Clone(s);
            Check(clone.authoredLevel&&clone.sourceNodeId=="0:1"&&clone.openedChests==3&&clone.rodFromChest&&clone.tideEverActivated&&clone.crabMovedByShrimp,"clone preserves authored metadata and checkpoints");
            Check(clone.dockRotation==3,"clone preserves explicit dock orientation");
            Check(Q(clone,2,1).chestReward==ChestReward.FishingRod&&Q(clone,1,2).chestReward==ChestReward.Unspecified,"clone preserves per-chest reward types");
            Q(clone,2,1).chestReward=ChestReward.Coins;clone.openedChests=99;
            Check(Q(s,2,1).chestReward==ChestReward.FishingRod&&s.openedChests==3,"clone changes do not mutate source reward or counters");

            s=Flat();Q(s,2,1).prop=PropKind.ClimbBlock;
            Check(HabitatRules.Height(s,2,1)==2,"climbing block adds one support layer");
            Check(HabitatRules.MovePlayer(s,1,0)&&s.playerX==2&&Q(s,3,1).prop==PropKind.ClimbBlock,"ordinary directional action pushes climbing block");
            Check(HabitatRules.Interact(s,3,1)&&s.carryingProp==PropKind.ClimbBlock&&Q(s,3,1).height==1,"climbing block can be carried without consuming terrain");
            Check(HabitatRules.Interact(s,2,2)&&Q(s,2,2).prop==PropKind.ClimbBlock&&Q(s,2,2).height==1&&s.carryingProp==PropKind.None,"placing climbing block keeps it distinct from soil");

            s=Flat();Q(s,2,1).height=2;Q(s,2,1).prop=PropKind.Crate;Q(s,3,1).prop=PropKind.Obstacle;
            Check(!HabitatRules.MovePlayer(s,1,0,true)&&s.playerX==1&&Q(s,2,1).prop==PropKind.Crate,"ordinary crate cannot be climbed even with Shift");
            s=Flat();Q(s,2,1).height=2;Q(s,2,1).prop=PropKind.ClimbBlock;
            Check(HabitatRules.MovePlayer(s,1,0,true)&&s.playerX==2&&Q(s,2,1).prop==PropKind.ClimbBlock,"explicit climb reaches two-layer climbing block without moving it");
            s=Flat();Q(s,2,1).height=3;Q(s,2,1).prop=PropKind.ClimbBlock;
            Check(!HabitatRules.MovePlayer(s,1,0,true)&&s.playerX==1&&Q(s,2,1).prop==PropKind.ClimbBlock,"climbing more than two layers fails atomically");
            s=Flat();Q(s,2,1).height=3;
            Check(!HabitatRules.MovePlayer(s,1,0,true)&&s.playerX==1,"climb modifier does not bypass a bare two-layer cliff");
            Q(s,2,1).height=2;
            Check(!HabitatRules.MovePlayer(s,1,0)&&s.playerX==1,"ordinary one-layer upward traversal is now forbidden");
            Check(!HabitatRules.MovePlayer(s,1,0,true)&&s.playerX==1,"Shift does not bypass the one-layer ordinary terrain restriction");
            s=Flat();Q(s,2,1).prop=PropKind.ClimbBlock;Q(s,3,1).prop=PropKind.Obstacle;
            Check(!HabitatRules.MovePlayer(s,1,0)&&Q(s,2,1).prop==PropKind.ClimbBlock&&s.playerX==1,"blocked climbing-block push is atomic");

            s=Flat(2);Q(s,2,1).prop=PropKind.Crate;Q(s,3,1).prop=PropKind.Tide;
            Check(HabitatRules.MovePlayer(s,1,0)&&s.tideEverActivated&&s.tideActive&&s.waterLevel==1,"pressing plate records tide checkpoint");
            Check(HabitatRules.MovePlayer(s,1,0)&&!s.tideActive&&s.waterLevel==0&&s.tideEverActivated,"releasing plate retains historical tide checkpoint");

            Animal shrimp,target;
            s=Approach(out shrimp,out target);StepApproach(s);
            Check(shrimp.x==3&&target.x==5&&target.z==3&&s.crabMovedByShrimp,"active cardinal approach moves crab exactly one opposite step");
            Q(s,3,3).trap=true;
            HabitatRules.Tick(s,new Tuning{animalMovement=true,moveSeconds=.2f,breedSeconds=100},1);
            Check(target.x==5&&target.z==3,"stationary shrimp never repeats a fear reaction");
            s=Approach(out shrimp,out target,targetFirst:true);target.moveClock=.99f;StepApproach(s);
            Check(target.x==5&&target.z==3,"immediate response is independent of animal list order");
            s=Approach(out shrimp,out target);Q(s,5,3).prop=PropKind.Obstacle;StepApproach(s);
            Check(shrimp.x==3&&target.x==4&&target.z==3&&!s.crabMovedByShrimp,"blocked opposite tile causes no movement or random sideways escape");
            s=Approach(out shrimp,out target);Add(s,Species.Crab,5,3);StepApproach(s);
            Check(target.x==4&&target.z==3,"occupied opposite tile blocks forced displacement");
            s=Approach(out shrimp,out target);Q(s,5,3).height=0;StepApproach(s);
            Check(target.x==4&&target.z==3,"crab cannot be frightened into water");
            s=Approach(out shrimp,out target);target.x=4;target.z=4;StepApproach(s);
            Check(target.x==4&&target.z==4&&!HabitatRules.IsThreatened(s,target,new Tuning{shrimpRadius=3}),"diagonal shrimp does not trigger authored adjacency rule");
            s=Approach(out shrimp,out target);Q(s,4,3).height=2;StepApproach(s);
            Check(target.x==4&&target.z==3&&!s.crabMovedByShrimp,"different-height neighbor is never frightened");
            s=Approach(out shrimp,out target,height:2);Add(s,Species.Turtle,3,4);StepApproach(s);
            Check(target.x==4&&target.z==3&&!s.crabMovedByShrimp,"valid highland turtle suppresses active shrimp approach");
            s=Approach(out shrimp,out target,Species.Goldfish,0);StepApproach(s);
            Check(target.x==5&&target.z==3&&target.moveClock<.1f,"waterborne fish responds once and does not also take an autonomous step");

            s=Flat();s.playerX=3;s.playerZ=2;shrimp=Add(s,Species.Shrimp,3,3);target=Add(s,Species.Crab,4,3);
            Check(HabitatRules.Interact(s,3,3)&&HabitatRules.Interact(s,3,3),"shrimp can be picked up and placed beside crab");
            HabitatRules.Tick(s,new Tuning{animalMovement=false},.3f);
            Check(target.x==4&&target.z==3&&!s.crabMovedByShrimp,"placing shrimp does not count as active approach");

            s=Flat(0);Q(s,1,3).prop=PropKind.Fan;Q(s,1,3).rotation=1;Q(s,2,3).prop=PropKind.Float;Q(s,4,3).height=1;
            shrimp=Add(s,Species.Shrimp,2,3);target=Add(s,Species.Crab,4,3);
            HabitatRules.Tick(s,new Tuning{animalMovement=false,moveSeconds=.2f},.21f);
            Check(shrimp.x==3&&target.x==4&&!s.crabMovedByShrimp,"passive fan transport does not count as active shrimp approach");

            s=Flat(0);s.playerX=s.dockX=2;s.playerZ=s.dockZ=3;s.onHarbor=true;
            Q(s,1,3).prop=PropKind.Fan;Q(s,1,3).rotation=1;Q(s,2,3).prop=PropKind.Float;
            shrimp=Add(s,Species.Shrimp,2,3);shrimp.carried=true;s.carryingAnimal=shrimp.id;
            HabitatRules.Tick(s,new Tuning{animalMovement=false,moveSeconds=.2f},.21f);
            Check(Q(s,3,3).prop==PropKind.Float&&s.playerX==2&&shrimp.x==2,"fan moves floating block without moving harbor player's grid anchor or carried shrimp");
            s.onHarbor=false;s.playerX=3;
            HabitatRules.Tick(s,new Tuning{animalMovement=false,moveSeconds=.2f},.21f);
            Check(s.playerX==4&&shrimp.x==4,"grid passenger and carried shrimp still move with floating block");

            s=Flat();s.authoredLevel=false;shrimp=Add(s,Species.Shrimp,3,3);Q(s,3,3).trap=true;target=Add(s,Species.Crab,4,3);target.moveClock=.99f;
            StepApproach(s);
            Check(target.x!=4||target.z!=3,"legacy laboratory keeps its existing static-neighbor fear behavior");

            return "PASS: "+assertions+" authored rule assertions (directional fear, rewards, climbing, checkpoints, clone, legacy compatibility)";
        }
    }
}
