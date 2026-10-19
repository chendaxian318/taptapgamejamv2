using System;
using System.Collections.Generic;
using System.Linq;
using HabitatIsland;
public static class LatestPlanMapChecks
{
    static Tuning t=new Tuning{animalMovement=false};
    static int[] dx={0,1,0,-1},dz={1,0,-1,0};
    public static readonly List<string> Evidence=new List<string>();
    public static readonly List<int[]> Route=new List<int[]>();
    static int Z(HabitatState s,int row)=>s.depth-1-row;
    static Tile Q(HabitatState s,int x,int row)=>HabitatRules.TileAt(s,x,Z(s,row));
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Evidence.Add("PASS "+text);}
    sealed class Node{public HabitatState s;public List<int> path;}
    static void Walk(HabitatState s,int x,int row)
    {
        int targetZ=Z(s,row);var queue=new Queue<Node>();var seen=new HashSet<string>();queue.Enqueue(new Node{s=HabitatRules.Clone(s),path=new List<int>()});
        while(queue.Count>0)
        {
            var n=queue.Dequeue();if(n.s.playerX==x&&n.s.playerZ==targetZ)
            {foreach(int d in n.path){if(!HabitatRules.MovePlayer(s,dx[d],dz[d],true,t))throw new Exception("replay failed");Route.Add(new[]{0,dx[d],dz[d]});}return;}
            for(int d=0;d<4;d++)
            {
                var next=HabitatRules.Clone(n.s);if(!HabitatRules.MovePlayer(next,dx[d],dz[d],true,t))continue;
                string key=next.playerX+","+next.playerZ;foreach(var a in next.animals)key+="/"+a.id+","+a.x+","+a.z;
                if(!seen.Add(key))continue;var p=new List<int>(n.path);p.Add(d);queue.Enqueue(new Node{s=next,path=p});
            }
        }
        throw new Exception("Cannot walk to source ("+x+","+row+") from ("+s.playerX+","+(s.depth-1-s.playerZ)+")");
    }
    static void Interact(HabitatState s,int x,int row)
    {int z=Z(s,row);if(!HabitatRules.Interact(s,x,z))throw new Exception("Interact source "+x+","+row+": "+s.message);Route.Add(new[]{1,x,z});}
    static string Reports(HabitatState s)=>string.Join("; ",s.animals.Select(a=>a.species+"@"+a.x+","+(s.depth-1-a.z)+":"+HabitatRules.Evaluate(s,a,t).suitable+"/"+HabitatRules.Evaluate(s,a,t).reason));
    static void ClearMovables(HabitatState s)
    {foreach(var q in s.tiles)if(q.prop==PropKind.Crate||q.prop==PropKind.Float){q.prop=PropKind.None;q.coveredProp=PropKind.None;}}
    static void Box(HabitatState s,int x,int row)
    {var q=Q(s,x,row);q.coveredProp=q.prop;q.prop=q.height<=s.waterLevel?PropKind.Float:PropKind.Crate;}
    public static string FirstRoute()
    {
        Route.Clear();var s=HabitatLevels.Create(5);int boxes=s.tiles.Count(q=>q.prop==PropKind.Crate||q.prop==PropKind.Float);
        Walk(s,6,7);Interact(s,7,7);Walk(s,7,7);Interact(s,7,8);
        Walk(s,8,7);Interact(s,8,6);Walk(s,8,6);Interact(s,9,6);
        Walk(s,7,7);Interact(s,7,8);Walk(s,9,6);Interact(s,10,6);
        Walk(s,6,3);Interact(s,6,4);Interact(s,6,3);Walk(s,10,6);Interact(s,11,6);
        Walk(s,11,5);Interact(s,12,5);
        Walk(s,9,3);Interact(s,9,2);Walk(s,8,2);Interact(s,7,2);
        Check(s.specialRod&&s.rod&&s.openedChests==1,"first legal route opens real rod chest");
        Check(s.tiles.Count(q=>q.prop==PropKind.Crate||q.prop==PropKind.Float)==boxes,"first route preserves original three crates plus float");
        Check(HabitatMechanicLevels.CompleteHabitat(s,t),"first legal route from original spawn satisfies all animals: "+Reports(s));
        Walk(s,8,6);Check(HabitatRules.Fish(s,7,Z(s,5),null,t)&&s.glassFish==1,"first route immediately catches one commission fish");
        HabitatRules.Tick(s,t,18.1f);Check(HabitatRules.Fish(s,7,Z(s,5),null,t)&&s.glassFish==2,"genuine ecological refresh supplies the second commission fish");
        return "PASS first map pure legal route "+Route.Count+" actions; 3 original crates + 1 float, rod chest, no injected tools or money; ecology tick only for second fish. Alternative legal route, not the draft's exact float-arrow route.";
    }
    public static string SecondRoute() => SecondRouteCore(false,false);
    public static string SecondDefaultRoute(bool segmented) => SecondRouteCore(true,segmented);
    static string SecondRouteCore(bool defaultMovement,bool segmented)
    {
        t=new Tuning{animalMovement=defaultMovement};
        Route.Clear();var s=HabitatLevels.Create(6);
        // Shared prerequisites from the first commission: exactly 200 coins, the earned rod, no basket yet.
        s.coins=200;s.rod=true;s.specialRod=true;s.chapterOneAccepted=true;s.chapterOneDelivered=true;s.chapterOneMorningSeen=true;s.campaignUnlockedStage=2;
        Check(HabitatRules.Shop(s,"fan")&&s.coins==0&&HabitatRules.StoreHeld(s),"second route buys one real 200-coin fan and stores it");
        Route.Add(new[]{4,0,0});
        Walk(s,4,5);Interact(s,4,6);Interact(s,4,5);Walk(s,5,4);Interact(s,6,4);
        Walk(s,6,4);Interact(s,7,4);Walk(s,7,4);Interact(s,7,5);
        Walk(s,8,4);Interact(s,9,4);Walk(s,9,4);Interact(s,10,4);
        Walk(s,11,4);Interact(s,12,4);Walk(s,12,3);Interact(s,11,3);
        Walk(s,12,5);Interact(s,12,6);Check(s.backBasketUnlocked,"second route opens actual basket chest before shrimp pickup");
        Walk(s,13,4);Interact(s,13,3);Walk(s,12,3);Walk(s,11,3);
        var upper=s.animals.Find(a=>a.species==Species.Goldfish&&a.z==Z(s,3));
        Check(upper.x==8,"second route scares upper goldfish two cells into its target pocket");
        Walk(s,13,4);Interact(s,13,3);
        Walk(s,11,4);Interact(s,11,5);Walk(s,9,4);Interact(s,9,3);
        Walk(s,7,4);Interact(s,7,5);Interact(s,7,4);Walk(s,9,5);Interact(s,9,6);
        Walk(s,7,4);Interact(s,6,4);Interact(s,7,4);Walk(s,11,5);Interact(s,10,5);
        Walk(s,9,5);Check(HabitatRules.TakeBagProp(s,0),"take bought fan for actual ferry");Route.Add(new[]{5,0,0});Interact(s,9,4);
        for(int i=0;i<2;i++){Check(HabitatRules.Rotate(s,9,Z(s,4)),"rotate real fan toward lower island");Route.Add(new[]{3,9,Z(s,4)});}
        Walk(s,13,4);Interact(s,13,3);Walk(s,9,5);Walk(s,9,6);
        if(segmented)
        {
            // Match HabitatGame.AdvanceSimulation, including its exact float division.
            int steps=Math.Max(1,(int)Math.Ceiling(10.1f/.1f));float dt=10.1f/steps;
            for(int i=0;i<steps;i++)HabitatRules.Tick(s,t,dt);
        }
        else HabitatRules.Tick(s,t,10.1f);
        Route.Add(new[]{6,101,0});
        Check(s.playerX==9&&s.playerZ==Z(s,9),"real fan carries original floating box, player and shrimp to lower island");
        Evidence.Add("FERRY "+(segmented?"controller-segmented":"single-tick")+" defaultMovement="+defaultMovement+" "+Reports(s));
        Interact(s,9,10);Walk(s,9,11);Interact(s,8,11);Walk(s,5,9);Interact(s,6,9);
        if(defaultMovement)
        {
            // The lower fish can wander onto the final seal while the ferry is running.
            // Retrieve the earned shrimp and approach from below to drive it back north.
            Walk(s,9,11);Interact(s,9,10);Walk(s,8,11);Walk(s,7,11);Interact(s,8,11);
            Evidence.Add("DRIVEN "+Reports(s));
        }
        Walk(s,7,11);Interact(s,7,12);Interact(s,7,10);
        Check(HabitatMechanicLevels.CompleteHabitat(s,t),"second legal route satisfies all three original animals: "+Reports(s));
        Check(s.coins==0&&s.magnets==0&&s.launchers==0&&s.shovels==0,"second route adds no unearned economy or tools");
        t=new Tuning{animalMovement=false};
        return "PASS second map pure legal route "+Route.Count+" actions, real 200-coin fan prerequisite, original crate/float inventory, actual basket chest, no added terrain or animals; ecology simulated only for fan ride; animalMovement="+defaultMovement+", segmented="+segmented+". Alternate route, not exact PDF standard route.";
    }
    public static string FirstTerminal()
    {
        var s=HabitatLevels.Create(5);ClearMovables(s);Box(s,7,2);Box(s,9,6);Box(s,10,6);Q(s,6,4).prop=PropKind.Float;
        Check(HabitatMechanicLevels.CompleteHabitat(s,t),"PDF map1 illustrated endpoint ecology: "+Reports(s));
        return "First terminal ecology checked only; props assigned to illustrated endpoint, not a legal route proof.";
    }
    public static string SecondTerminal()
    {
        var s=HabitatLevels.Create(6);ClearMovables(s);
        // Source-row coordinates from the final solution panel. The upper goldfish was chased left twice.
        Box(s,6,4);Box(s,9,3);Box(s,11,3);Box(s,10,5);Box(s,6,9);Box(s,9,9);Box(s,7,10);
        var fishes=s.animals.Where(a=>a.species==Species.Goldfish).OrderByDescending(a=>a.z).ToArray();
        fishes[0].x=8;fishes[0].z=Z(s,3);
        var shrimp=s.animals.Find(a=>a.species==Species.Shrimp);shrimp.carried=true;s.carryingAnimal=shrimp.id;s.backBasketUnlocked=true;shrimp.x=s.playerX;shrimp.z=s.playerZ;
        Q(s,4,6).prop=PropKind.Float;
        Check(HabitatMechanicLevels.CompleteHabitat(s,t),"PDF map2 endpoint ecology: "+Reports(s));
        return "PASS map2 illustrated endpoint ecology only; seven original boxes and float retained, not a legal route proof.";
    }
    static void HoldShrimp(HabitatState s)
    {var a=s.animals.Find(x=>x.species==Species.Shrimp);s.backBasketUnlocked=true;a.carried=true;s.carryingAnimal=a.id;a.x=s.playerX;a.z=s.playerZ;}
    static void Resident(HabitatState s,Species sp,int x,int row)
    {var a=s.animals.Find(v=>v.species==sp);a.x=x;a.z=Z(s,row);}
    public static string ThirdTerminal()
    {
        var s=HabitatLevels.Create(7);ClearMovables(s);
        foreach(var p in new[]{new[]{8,6},new[]{9,6},new[]{7,10},new[]{10,10},new[]{10,14}})Box(s,p[0],p[1]);
        Resident(s,Species.Crab,6,12);Resident(s,Species.Goldfish,7,12);HoldShrimp(s);
        Check(HabitatMechanicLevels.CompleteHabitat(s,t),"PDF map3 endpoint ecology: "+Reports(s));
        return "PASS map3 illustrated endpoint ecology only; five original boxes retained, not a legal route proof.";
    }
    public static string FourthTerminal()
    {
        var s=HabitatLevels.Create(8);ClearMovables(s);
        foreach(var p in new[]{new[]{7,2},new[]{13,5},new[]{12,6},new[]{7,6},new[]{7,11},new[]{8,11},new[]{7,15}})Box(s,p[0],p[1]);
        Q(s,9,3).prop=Q(s,4,6).prop=PropKind.Float;
        Resident(s,Species.Goldfish,9,13);Resident(s,Species.Jellyfish,8,10);HoldShrimp(s);
        Check(HabitatMechanicLevels.CompleteHabitat(s,t),"PDF map4 final endpoint ecology: "+Reports(s));
        return "PASS map4 illustrated endpoint ecology only; seven drawn original boxes and two floats retained, not a legal route proof.";
    }
    public static string FifthTerminal()
    {
        var s=HabitatLevels.Create(10);ClearMovables(s);
        foreach(var p in new[]{new[]{6,8},new[]{7,8},new[]{7,10},new[]{12,9},new[]{12,10}})Box(s,p[0],p[1]);
        Q(s,9,4).prop=PropKind.Float;
        Resident(s,Species.Goldfish,7,11);Resident(s,Species.Crab,6,11);HoldShrimp(s);
        Check(HabitatMechanicLevels.CompleteHabitat(s,t),"PDF map5 launcher endpoint ecology: "+Reports(s));
        s.won=false;Box(s,8,10);Check(HabitatMechanicLevels.CompleteHabitat(s,t),"PDF map5 magnet-plus-purchased-box endpoint ecology: "+Reports(s));
        return "PASS map5 both illustrated endpoint ecology only; additional box only in explicit purchased-box variant, not a legal route proof.";
    }

}

