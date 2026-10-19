using System;

namespace HabitatIsland
{
    public static class HabitatJourneyChecks
    {
        static int assertions;
        static void Check(bool condition,string label)
        {assertions++;if(!condition)throw new InvalidOperationException("Journey check failed: "+label);}
        static HabitatState Map()
        {
            HabitatState s=new HabitatState{width=3,depth=3,playerX=1,playerZ=1,dockX=1,dockZ=1,tiles=new Tile[9]};
            for(int i=0;i<s.tiles.Length;i++)s.tiles[i]=new Tile{height=1};
            return s;
        }
        static bool SameProgress(HabitatState a,HabitatState b)
        {
            return a.coins==b.coins&&a.cargo==b.cargo&&a.fish==b.fish&&a.shrimp==b.shrimp&&a.rod==b.rod&&a.traps==b.traps
                &&a.day==b.day&&a.openingCompleted==b.openingCompleted&&a.openingCatchGranted==b.openingCatchGranted
                &&a.deliveredFish==b.deliveredFish&&a.deliveredShrimp==b.deliveredShrimp&&a.deliveryCount==b.deliveryCount&&a.deliveryIncome==b.deliveryIncome;
        }
        public static string Run()
        {
            assertions=0;
            HabitatState s=Map();s.fish=7;s.shrimp=4;
            Check(s.day==1,"new state starts on day one");
            Check(HabitatJourneyRules.BeginNewJourney(s)&&s.fish==10&&s.shrimp==6&&s.openingCatchGranted,"opening adds catch without replacing existing stock");
            Check(!HabitatJourneyRules.BeginNewJourney(s)&&s.fish==10&&s.shrimp==6,"opening catch is granted once");
            HabitatJourneyRules.CompleteOpening(s);
            Check(s.openingCompleted&&!HabitatJourneyRules.BeginNewJourney(s),"completed opening cannot grant again");
            s=Map();s.openingCompleted=true;
            Check(!HabitatJourneyRules.BeginNewJourney(s)&&s.fish==0&&s.shrimp==0,"completed legacy opening cannot award missing catch");
            s=Map();s.day=0;s.fish=4;
            HabitatJourneyRules.Normalize(s);
            Check(s.day==1&&s.fish==4&&s.shrimp==0&&!s.openingCatchGranted&&!s.openingCompleted,"normalization changes only missing day");

            s=Map();HabitatJourneyRules.BeginNewJourney(s);int coins=s.coins;
            Check(HabitatRules.Shop(s,"sell")&&s.coins==coins+34&&s.fish==0&&s.shrimp==0,"opening catch uses unchanged fish8 shrimp5 prices");
            Check(s.deliveredFish==3&&s.deliveredShrimp==2&&s.deliveryCount==1&&s.deliveryIncome==34,"successful sale records exact delivery");
            Check(!HabitatRules.Shop(s,"sell")&&s.coins==coins+34&&s.deliveryCount==1&&s.deliveryIncome==34,"empty repeated sale cannot earn or count twice");
            s.fish=1;s.shrimp=3;s.playerX=0;s.playerZ=0;
            Check(!HabitatRules.Shop(s,"sell")&&s.fish==1&&s.shrimp==3&&s.deliveryCount==1,"remote rejected sale preserves stock and counters");
            s.playerX=1;s.playerZ=1;
            Check(HabitatRules.Shop(s,"sell")&&s.deliveredFish==4&&s.deliveredShrimp==5&&s.deliveryCount==2&&s.deliveryIncome==57,"later delivery adds cumulative counts and income");

            s.fish=9;s.shrimp=6;s.cargo=8;s.traps=5;s.rod=false;HabitatJourneyRules.CompleteOpening(s);
            HabitatState before=HabitatRules.Clone(s);
            HabitatJourneyRules.AdvanceDay(s);
            Check(s.day==2,"sleep advances exactly one day");
            before.day=s.day;
            Check(SameProgress(s,before),"next day retains all inventory and delivery history");
            HabitatState clone=HabitatRules.Clone(s);
            Check(SameProgress(s,clone),"clone preserves every shared journey field");
            clone.tiles[0].height=8;clone.day++;clone.deliveryCount++;
            Check(s.tiles[0].height==1&&s.day==2&&s.deliveryCount==2,"clone map and journey mutations remain independent");

            HabitatState dest=Map();dest.playerX=0;dest.dockX=2;dest.levelIndex=4;dest.tiles[0].prop=PropKind.Chest;
            dest.tiles[0].height=5;dest.carryingProp=PropKind.Fan;dest.carryingAnimal=42;
            dest.animals.Add(new Animal{id=42,species=Species.Crab,x=2,z=0});
            var tiles=dest.tiles;var animals=dest.animals;
            s.carryingProp=PropKind.Crate;s.carryingAnimal=7;s.animals.Add(new Animal{id=7,species=Species.Shrimp,carried=true});
            HabitatJourneyRules.CopySharedProgress(s,dest);
            Check(SameProgress(s,dest),"cross-map copy carries economy and journey progress");
            Check(ReferenceEquals(dest.tiles,tiles)&&dest.tiles[0].height==5&&dest.tiles[0].prop==PropKind.Chest,"cross-map copy preserves destination terrain reference and content");
            Check(ReferenceEquals(dest.animals,animals)&&dest.animals.Count==1&&dest.animals[0].id==42,"cross-map copy does not copy wildlife or carried animals");
            Check(dest.playerX==0&&dest.dockX==2&&dest.levelIndex==4&&dest.carryingProp==PropKind.Fan&&dest.carryingAnimal==42,"cross-map copy preserves destination anchors and hand state");
            s.day=0;HabitatJourneyRules.CopySharedProgress(s,dest);
            Check(dest.day==1&&s.day==0&&dest.fish==9&&dest.shrimp==6,"legacy copy normalizes destination without gifting or mutating source");
            return "PASS "+assertions+" journey rule checks";
        }
    }
}
