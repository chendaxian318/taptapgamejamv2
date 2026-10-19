using System;
using System.Collections.Generic;
using HabitatIsland;

// Pure rule checks. These replay selected reachable actions, not an end-to-end campaign solution.
public static class FormalLevelChecks
{
    public static readonly List<string> Passed = new List<string>();
    public static readonly List<string> Routes = new List<string>();
    static readonly int[] DX = { 0, 1, 0, -1 }, DZ = { 1, 0, -1, 0 };
    sealed class Node { public HabitatState state; public List<int> steps; }
    static void Check(bool value, string label)
    { if (!value) throw new InvalidOperationException(label); Passed.Add(label); }
    static Tile Q(HabitatState s, int x, int z) { return HabitatRules.TileAt(s, x, z); }
    static int Distance(HabitatState s, int x, int z) { return Math.Abs(s.playerX-x)+Math.Abs(s.playerZ-z); }
    static void Prop(int index, int x, int z, PropKind prop)
    { Check(Q(HabitatLevels.Create(index),x,z).prop==prop,"map "+index+" prop "+prop+" at ("+x+","+z+")"); }
    static void AnimalAt(int index, int x, int z, Species species)
    { Check(HabitatLevels.Create(index).animals.Exists(a=>a.x==x&&a.z==z&&a.species==species),"map "+index+" resident "+species+" at ("+x+","+z+")"); }

    static void VerifyMap(int index, int width, int depth, int dockX, int dockZ, string[] rows)
    {
        var s=HabitatLevels.Create(index);
        Check(s.levelIndex==index&&s.authoredLevel&&s.width==width&&s.depth==depth&&s.tiles.Length==width*depth,"map "+index+" dimensions and formal identity");
        Check(s.playerX==dockX&&s.playerZ==dockZ&&s.dockX==dockX&&s.dockZ==dockZ,"map "+index+" source landing coordinate");
        Check(!HabitatRules.IsWater(s,dockX,dockZ),"map "+index+" dry landing");
        bool step=false;
        for(int d=0;d<4;d++)
        {
            var p=HabitatRules.Clone(s);
            if(HabitatRules.MovePlayer(p,DX[d],DZ[d],true)&&Distance(p,dockX,dockZ)==1)step=true;
        }
        Check(step,"map "+index+" landing has a legal one-cell exit");
        Check(rows.Length==depth,"map "+index+" source row count");
        for(int row=0;row<depth;row++)
        {
            string[] labels=rows[row].Split('|');
            Check(labels.Length==width,"map "+index+" source row "+row+" width");
            for(int x=0;x<width;x++)
            {
                int z=depth-1-row;var tile=Q(s,x,z);
                Check(tile!=null&&tile.height>=0&&tile.height<=4&&Enum.IsDefined(typeof(PropKind),tile.prop),"map "+index+" valid tile ("+x+","+z+")");
                Check(HabitatLevels.CellLabel(s,x,z)==labels[x],"map "+index+" north/south source mapping ("+x+","+z+")");
                if(labels[x].Length>0&&char.IsDigit(labels[x][0]))
                    Check(tile.height==labels[x][0]-'0',"map "+index+" explicit ground height ("+x+","+z+")");
            }
        }
        var ids=new HashSet<int>();
        foreach(var a in s.animals)
        {
            Check(ids.Add(a.id)&&HabitatRules.Inside(s,a.x,a.z)&&HabitatRules.Inside(s,a.homeX,a.homeZ)&&Enum.IsDefined(typeof(Species),a.species),"map "+index+" resident "+a.id+" has unique ID and in-bounds positions");
            if(a.species==Species.Goldfish||a.species==Species.Jellyfish)
                Check(HabitatRules.IsWater(s,a.x,a.z),"map "+index+" aquatic resident "+a.id+" starts in water");
        }
        Check(HabitatLevels.CellLabel(s,-1,0)==""&&HabitatLevels.CellLabel(s,width,0)==""&&HabitatLevels.CellLabel(s,0,depth)=="","map "+index+" source-label bounds");
    }

    static List<int> FindWalk(HabitatState state, Func<HabitatState,bool> destination)
    {
        var queue=new Queue<Node>();var seen=new HashSet<int>();
        queue.Enqueue(new Node {state=HabitatRules.Clone(state),steps=new List<int>()});
        seen.Add(state.playerZ*state.width+state.playerX);
        while(queue.Count>0)
        {
            var n=queue.Dequeue();if(destination(n.state))return n.steps;
            for(int d=0;d<4;d++)
            {
                var next=HabitatRules.Clone(n.state);int oldX=next.playerX,oldZ=next.playerZ;
                if(!HabitatRules.MovePlayer(next,DX[d],DZ[d],true)||Distance(next,oldX,oldZ)!=1)continue;
                if(!seen.Add(next.playerZ*next.width+next.playerX))continue;
                var steps=new List<int>(n.steps);steps.Add(d);queue.Enqueue(new Node {state=next,steps=steps});
            }
        }
        return null;
    }
    static void Walk(HabitatState s, Func<HabitatState,bool> destination, string label)
    {
        var path=FindWalk(s,destination);Check(path!=null,label+" has a legal dry/float path");
        string trace="("+s.playerX+","+s.playerZ+")";
        foreach(int d in path)
        {
            int x=s.playerX,z=s.playerZ;
            if(!HabitatRules.MovePlayer(s,DX[d],DZ[d],true)||Distance(s,x,z)!=1)throw new InvalidOperationException(label+" replay failed");
            trace+=" -> ("+s.playerX+","+s.playerZ+")";
        }
        Check(destination(s),label+" replay reaches target");Routes.Add(label+": "+trace);
    }
    static void Interact(HabitatState s, int x, int z, string label)
    { Check(HabitatRules.Interact(s,x,z),label);Routes.Add(label+": player=("+s.playerX+","+s.playerZ+") target=("+x+","+z+")"); }

    static void VerifyRoutes()
    {
        var original=HabitatLevels.Create(4);
        Walk(original,s=>Distance(s,12,14)==1,"revised continuous map spawn to original rod chest");
        Interact(original,12,14,"open continuous-map rod through reachable normal interaction");
        Check(original.rod&&original.specialRod,"continuous-map rod remains the special commission rod");

        var first=HabitatLevels.Create(5);
        Check(FindWalk(first,s=>Distance(s,12,5)==1)==null,"first screenshot rod initially requires puzzle actions across water");
        Walk(first,s=>Distance(s,8,4)==1,"first map reach lower eastern crate");
        Interact(first,8,4,"pick original lower eastern crate");
        Walk(first,s=>s.playerX==8&&s.playerZ==4,"carry first crate to eastern bank");
        Interact(first,9,4,"place first crate as float in existing water gap");
        Walk(first,s=>Distance(s,7,3)==1,"walk back for original southern crate");
        Interact(first,7,3,"pick original southern crate");
        Walk(first,s=>s.playerX==9&&s.playerZ==4,"carry second crate onto first float");
        Interact(first,10,4,"place second float to complete original two-cell water crossing");
        Walk(first,s=>Distance(s,12,5)==1,"cross two placed floats to first-map rod");
        Interact(first,12,5,"open first-map fishing-rod chest");
        Check(first.rod&&first.rodFromChest&&first.openedChests==1&&first.coins==0&&first.cargo==0,"first-map rod acquired with original crates and no resource grants");

        // Independent puzzle route from a fresh map. This verifies enclosure, not the whole campaign.
        first=HabitatLevels.Create(5);
        Walk(first,s=>Distance(s,7,3)==1,"enclosure route reach remaining southern crate");
        Interact(first,7,3,"pick southern crate for north opening");
        Walk(first,s=>s.playerX==6&&s.playerZ==6,"carry crate onto original floating block");
        Interact(first,7,6,"place crate at fish's north opening");
        Walk(first,s=>s.playerX==8&&s.playerZ==3,"enclosure route approach east crate through cleared southern cell");
        Check(HabitatRules.MovePlayer(first,0,1)&&Q(first,8,5).prop==PropKind.Float,"push original east crate north to close fish's east opening");
        Walk(first,s=>s.playerX==6&&s.playerZ==7,"reach dry recovery position north of original float");
        Interact(first,6,6,"recover original float to bank with normal backward retreat");
        Check(first.playerX==6&&first.playerZ==8&&Q(first,6,7).prop==PropKind.Crate&&first.carryingProp==PropKind.None,"float recovery uses bank placement and retreat, never a carried float");
        Interact(first,6,7,"pick recovered crate from level bank");
        Walk(first,s=>s.playerX==6&&s.playerZ==4,"carry recovered crate to fish's southwest bank");
        Interact(first,6,5,"place recovered crate at fish's west opening");
        Check(HabitatRules.IsClosedWaterArea(first,7,5),"first-map original fish is enclosed using two original crates and original float");
        Check(first.animals.Count==1&&first.animals[0].x==7&&first.animals[0].z==5&&first.coins==0&&first.cargo==0,"enclosure preserves original fish and grants no resources");
    }

    static HabitatState Flat(int index=5)
    {
        var s=new HabitatState {width=7,depth=7,tiles=new Tile[49],levelIndex=index,authoredLevel=index>=4,playerX=2,playerZ=3,dockX=2,dockZ=3,rod=false,coins=0};
        for(int i=0;i<s.tiles.Length;i++)s.tiles[i]=new Tile {height=1};return s;
    }
    static HabitatState CarriedApproach(int index,Species target,bool blocked=false)
    {
        var s=Flat(index);s.animals.Add(new Animal {id=1,species=Species.Shrimp,x=2,z=4,homeX=2,homeZ=4});
        s.animals.Add(new Animal {id=2,species=target,x=4,z=3,homeX=4,homeZ=3});
        if(target==Species.Goldfish){Q(s,4,3).height=0;Q(s,5,3).height=0;}
        if(blocked)Q(s,5,3).prop=PropKind.Obstacle;
        Check(HabitatRules.Interact(s,2,4)&&s.carryingAnimal==1,"synthetic carried-shrimp fixture picks up shrimp normally");return s;
    }
    static void VerifyFormalShop()
    {
        for(int index=4;index<=9;index++)
        {
            var s=HabitatLevels.Create(index);
            Check(!s.chapterOneAccepted&&!s.chapterOneDelivered&&HabitatChapterRules.IsCampaignShop(s),"formal map "+index+" new unaccepted journey uses campaign shop");
            Check(!HabitatChapterRules.CanBuyItem(s,"fan",out string reason)&&!string.IsNullOrEmpty(reason)&&HabitatChapterRules.ShopPrice(s,"fan",10)==200,"formal map "+index+" fan remains locked and priced 200 before accepting commission");
            s.coins=200;
            Check(!HabitatRules.Shop(s,"fan")&&s.coins==200&&s.carryingProp==PropKind.None,"formal map "+index+" cannot bypass locked fan with sufficient funds");

            // Synthetic shared commission checkpoint; delivery itself uses the real settlement rule.
            s.coins=0;s.chapterOneAccepted=true;s.chapterOneSailed=true;s.specialRod=true;s.glassFish=2;
            Check(HabitatChapterRules.Deliver(s)&&s.chapterOneDelivered&&s.coins==200&&s.glassFish==0,"formal map "+index+" commission settlement pays exactly 200 once");
            Check(HabitatChapterRules.CanBuyItem(s,"fan",out reason)&&HabitatChapterRules.ShopPrice(s,"fan",10)==200,"formal map "+index+" delivery unlocks fan while retaining campaign price");
            s.coins=199;
            Check(!HabitatRules.Shop(s,"fan")&&s.coins==199&&s.carryingProp==PropKind.None,"formal map "+index+" insufficient funds cannot buy unlocked fan");
            s.coins=200;
            Check(HabitatRules.Shop(s,"fan")&&s.coins==0&&s.carryingProp==PropKind.Fan,"formal map "+index+" unlocked fan purchase costs exactly 200");
            Check(!HabitatChapterRules.Deliver(s)&&s.coins==0&&s.glassFish==0&&s.chapterOneDelivered,"formal map "+index+" repeated commission settlement cannot refund fan or duplicate reward");
        }
    }
    static void VerifyInteractionBoundaries()
    {
        var wind=Flat();Q(wind,1,1).prop=PropKind.Fan;Q(wind,1,1).rotation=1;
        for(int x=2;x<=5;x++)Q(wind,x,1).height=0;
        Q(wind,2,1).prop=PropKind.Shell;Q(wind,3,1).prop=PropKind.Float;Q(wind,4,1).prop=PropKind.Shell;
        var still=new Tuning {animalMovement=false,moveSeconds=.2f};
        HabitatRules.Tick(wind,still,.21f);
        Check(Q(wind,4,1).prop==PropKind.Float&&Q(wind,4,1).coveredProp==PropKind.Shell&&Q(wind,2,1).prop==PropKind.Shell,"synthetic fan wind passes shell and floats over another shell without deleting it");
        HabitatRules.Tick(wind,still,.21f);
        Check(Q(wind,5,1).prop==PropKind.Float&&Q(wind,4,1).prop==PropKind.Shell&&Q(wind,4,1).coveredProp==PropKind.None&&wind.shells==0,"synthetic departing float restores covered shell without awarding or swallowing it");
        var s=Flat();Q(s,3,3).height=0;Q(s,4,3).height=0;Q(s,5,3).height=0;Q(s,6,3).height=0;Q(s,3,3).prop=PropKind.Shell;
        Check(HabitatRules.IsWater(s,3,3)&&!HabitatRules.IsClosedWaterArea(s,3,3),"synthetic shell remains water and does not seal an ocean-connected channel");
        Check(HabitatRules.Interact(s,3,3)&&s.shells==1&&Q(s,3,3).prop==PropKind.None,"synthetic adjacent shell pickup increments inventory and removes shell");
        Check(!HabitatRules.Interact(s,3,3)&&s.shells==1,"synthetic repeated shell interaction cannot duplicate inventory");
        Q(s,3,3).prop=PropKind.Chest;Q(s,3,3).height=1;Q(s,3,3).chestReward=ChestReward.BackBasket;
        Check(HabitatRules.Interact(s,3,3)&&s.backBasketUnlocked&&s.openedChests==1,"synthetic back-basket chest grants permanent pickup flag once");
        Check(!HabitatRules.Interact(s,3,3)&&s.backBasketUnlocked&&s.openedChests==1&&s.coins==0,"synthetic repeated back-basket pickup cannot duplicate counters or grant coins");
        var clone=HabitatRules.Clone(s);clone.shells++;clone.backBasketUnlocked=false;clone.tiles[0].height=3;
        Check(s.shells==1&&s.backBasketUnlocked&&s.tiles[0].height==1,"clone basket/shell fields and terrain remain independent");
        clone=HabitatRules.Clone(s);Check(clone.shells==1&&clone.backBasketUnlocked,"clone preserves basket and shell inventory");
        var destination=HabitatLevels.Create(6);var tiles=destination.tiles;var animals=destination.animals;var firstHeight=tiles[0].height;
        HabitatJourneyRules.CopySharedProgress(s,destination);
        Check(destination.backBasketUnlocked&&destination.shells==1,"journey copy carries basket and shells");
        Check(ReferenceEquals(tiles,destination.tiles)&&ReferenceEquals(animals,destination.animals)&&destination.levelIndex==6&&destination.tiles[0].height==firstHeight,"journey copy preserves destination map and residents");
        destination.shells++;Check(s.shells==1,"destination shell inventory does not alias source");

        s=CarriedApproach(7,Species.Crab);Check(HabitatRules.MovePlayer(s,1,0)&&s.playerX==3&&s.animals[1].x==5&&s.crabMovedByShrimp,"new formal map successful carried-shrimp approach drives crab exactly one cell");
        s=CarriedApproach(7,Species.Goldfish);Check(HabitatRules.MovePlayer(s,1,0)&&s.animals[1].x==5&&s.animals[1].z==3,"new formal map bank approach drives fish one cell in lower water");
        s=CarriedApproach(7,Species.Crab,true);Check(HabitatRules.MovePlayer(s,1,0)&&s.animals[1].x==4,"blocked escape leaves crab in place");
        s=CarriedApproach(7,Species.Crab);Q(s,3,3).prop=PropKind.Obstacle;Check(!HabitatRules.MovePlayer(s,1,0)&&s.playerX==2&&s.animals[1].x==4,"rejected player move does not trigger carried-shrimp fear");
        s=CarriedApproach(4,Species.Crab);Check(HabitatRules.MovePlayer(s,1,0)&&s.animals[1].x==4,"legacy continuous map preserves prior carried-shrimp behavior");
        s=CarriedApproach(0,Species.Crab);Check(HabitatRules.MovePlayer(s,1,0)&&s.animals[1].x==4,"legacy teaching map preserves prior carried-shrimp behavior");
    }

    public static string Run()
    {
        Passed.Clear();Routes.Clear();
        VerifyMap(4,18,19,3,10,HabitatFigJamLevel.SourceRows);
        VerifyMap(5,15,11,3,1,HabitatMechanicLevels.SourceRows[0]);
        VerifyMap(6,15,15,1,10,HabitatMechanicLevels.SourceRows[1]);
        VerifyMap(7,16,17,3,10,HabitatMechanicLevels.SourceRows[2]);
        VerifyMap(8,16,16,2,10,HabitatMechanicLevels.SourceRows[3]);
        VerifyMap(9,19,21,2,15,HabitatMountainLevel.SourceRows);
        var original=HabitatLevels.Create(4);
        Check(Q(original,8,14).height==1&&Q(original,6,13).height==1&&Q(original,8,5).height==2&&Q(original,9,4).height==2&&Q(original,9,3).height==3,"continuous-map five screenshot height revisions");
        Prop(4,8,14,PropKind.Tide);Prop(4,6,13,PropKind.ClimbBlock);Prop(4,12,14,PropKind.Chest);Prop(4,16,7,PropKind.Chest);
        Prop(5,12,5,PropKind.Chest);Prop(5,6,6,PropKind.Float);Prop(5,9,8,PropKind.Crate);Prop(5,8,4,PropKind.Crate);Prop(5,7,3,PropKind.Crate);AnimalAt(5,7,5,Species.Goldfish);
        Prop(6,12,8,PropKind.Chest);Prop(6,4,8,PropKind.Float);Prop(6,7,6,PropKind.Obstacle);Prop(6,8,6,PropKind.Obstacle);Prop(6,6,4,PropKind.Obstacle);AnimalAt(6,13,11,Species.Shrimp);AnimalAt(6,10,11,Species.Goldfish);AnimalAt(6,8,5,Species.Goldfish);
        Check(Q(HabitatLevels.Create(6),12,8).chestReward==ChestReward.BackBasket,"second map marked basket has actual basket reward");
        Prop(7,6,2,PropKind.ClimbBlock);Prop(7,4,3,PropKind.Crate);AnimalAt(7,11,10,Species.Shrimp);AnimalAt(7,6,8,Species.Crab);AnimalAt(7,9,6,Species.Goldfish);
        Prop(8,5,12,PropKind.ClimbBlock);Prop(8,9,11,PropKind.Tide);Prop(8,9,12,PropKind.Float);Prop(8,4,9,PropKind.Float);AnimalAt(8,13,9,Species.Shrimp);AnimalAt(8,6,4,Species.Jellyfish);AnimalAt(8,9,4,Species.Goldfish);
        Prop(9,7,15,PropKind.Float);Prop(9,8,17,PropKind.Fan);Prop(9,7,16,PropKind.ClimbBlock);Prop(9,6,13,PropKind.ClimbBlock);Prop(9,9,11,PropKind.ClimbBlock);Prop(9,10,12,PropKind.Chest);Prop(9,13,11,PropKind.Shell);AnimalAt(9,10,8,Species.Jellyfish);
        Check(Q(HabitatLevels.Create(9),10,12).chestReward==ChestReward.BackBasket,"mountain marked basket has actual basket reward");
        VerifyRoutes();VerifyInteractionBoundaries();VerifyFormalShop();
        return "PASS: "+Passed.Count+" formal-map rule assertions; "+Routes.Count+" selected route records. Six map layouts and legal first-map rod/enclosure routes, not all-map completion.";
    }
}
