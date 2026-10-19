using System;
using System.Collections.Generic;
using HabitatIsland;

public static class FormalCompletionChecks
{
    public static readonly List<string> Evidence=new List<string>();
    static int checks;
    static Tuning tuning=new Tuning {animalMovement=false};
    static void Check(bool okay,string name) { if(!okay)throw new Exception(name);checks++;Evidence.Add("PASS "+name); }
    static HabitatState Board(int index=7)
    {
        var s=new HabitatState {width=7,depth=7,levelIndex=index,authoredLevel=true,tiles=new Tile[49],playerX=1,playerZ=1};
        for(int i=0;i<49;i++)s.tiles[i]=new Tile {height=1};return s;
    }
    static Animal Add(HabitatState s,Species species,int x,int z)
    {var a=new Animal {id=s.animals.Count+1,species=species,x=x,z=z,homeX=x,homeZ=z};s.animals.Add(a);return a;}
    static Tile Q(HabitatState s,int x,int z) {return HabitatRules.TileAt(s,x,z);}
    public static string Run()
    {
        checks=0;Evidence.Clear();
        foreach(int index in new[]{7,8})
        {
            var fresh=HabitatLevels.Create(index);
            Check(!HabitatMechanicLevels.CompleteHabitat(fresh,tuning),"map "+index+" does not complete on landing");
            Check(fresh.animals.Count>0&&!fresh.won,"source residents preserved");
        }
        var s=Board();var crab=Add(s,Species.Crab,3,3);
        Check(!HabitatRules.Evaluate(s,crab,tuning).suitable,"crab inland rejected");
        Q(s,4,3).height=0;
        Check(HabitatRules.Evaluate(s,crab,tuning).suitable,"shore crab without aquatic population follows conditional rule");
        var fish=Add(s,Species.Goldfish,5,5);Q(s,5,5).height=0;
        Check(!HabitatRules.Evaluate(s,crab,tuning).suitable,"existing aquatic resident must be nearby");
        fish.x=4;fish.z=3;
        Check(HabitatRules.Evaluate(s,crab,tuning).suitable,"one goldfish in 3x3 suffices");
        fish.carried=true;
        Check(!HabitatRules.Evaluate(s,crab,tuning).suitable,"carried aquatic resident cannot satisfy companion");
        fish.carried=false;Q(s,4,3).prop=PropKind.Float;
        Check(!HabitatRules.Evaluate(s,crab,tuning).suitable,"fish on dry support cannot count as aquatic companion");
        Q(s,4,3).prop=PropKind.None;
        var shrimp=Add(s,Species.Shrimp,3,2);
        Check(!HabitatRules.Evaluate(s,crab,tuning).suitable,"threatened crab cannot win");
        shrimp.carried=true;crab.carried=true;
        Check(!HabitatMechanicLevels.CompleteHabitat(s,tuning),"carried target cannot win");
        crab.carried=false;var before=HabitatRules.Clone(s);int coins=s.coins;
        Check(HabitatMechanicLevels.CompleteHabitat(s,tuning)&&s.won,"stage three completes from suitable habitat without offspring");
        Check(crab.spawnedTotal==0&&s.coins==coins,"no invented breeding or reward requirement");
        int events=s.events.Count;
        Check(!HabitatMechanicLevels.CompleteHabitat(s,tuning)&&s.events.Count==events,"completion logs only once");
        Check(!before.won&&HabitatRules.Clone(s).won,"undo snapshot and won clone are independent");
        s=Board(8);var jelly=Add(s,Species.Jellyfish,2,2);fish=Add(s,Species.Goldfish,4,4);
        Q(s,2,2).height=Q(s,4,4).height=0;
        Check(!HabitatRules.Evaluate(s,jelly,tuning).suitable,"closed pool rejects jelly despite water");
        Check(!HabitatMechanicLevels.CompleteHabitat(s,tuning),"fish condition alone cannot finish four");
        Q(s,2,1).height=Q(s,2,0).height=0;
        Check(HabitatRules.Evaluate(s,jelly,tuning).suitable,"cardinal water path to boundary satisfies jelly");
        Check(HabitatMechanicLevels.HabitatProgress(s,tuning)==2,"jelly open sea and fish sealed pond simultaneously ready");
        Q(s,2,1).prop=PropKind.Float;
        Check(!HabitatRules.Evaluate(s,jelly,tuning).suitable,"floating box closes sea channel");
        Q(s,2,1).prop=PropKind.None;Q(s,4,3).height=Q(s,4,2).height=Q(s,3,2).height=0;
        Check(!HabitatMechanicLevels.CompleteHabitat(s,tuning),"open fish pool rejects even when jelly satisfies");
        Q(s,4,3).height=1;
        Check(HabitatMechanicLevels.CompleteHabitat(s,tuning),"both stage four species complete together");
        s.won=false;fish.carried=true;
        Check(!HabitatMechanicLevels.CompleteHabitat(s,tuning),"carried fish cannot finish four");
        fish.carried=false;s.animals.Remove(fish);
        Check(!HabitatMechanicLevels.CompleteHabitat(s,tuning),"missing fish is not a vacuous success");
        s.animals.Clear();Check(!HabitatMechanicLevels.CompleteHabitat(s,tuning),"empty map cannot win");
        s=Board();crab=Add(s,Species.Crab,3,3);Q(s,4,3).height=0;Add(s,Species.Crab,1,1);
        Check(!HabitatMechanicLevels.CompleteHabitat(s,tuning),"all target crabs must be suitable");
        foreach(int index in new[]{0,4,5,6,9})
        {
            s=HabitatLevels.Create(index);
            Check(!HabitatMechanicLevels.HasHabitatGoal(s)&&!HabitatMechanicLevels.CompleteHabitat(s,tuning),"unconfirmed endings and old island unchanged "+index);
        }
        s=Board(4);crab=Add(s,Species.Crab,3,3);Q(s,4,3).height=0;Add(s,Species.Goldfish,4,3);
        Check(!HabitatRules.Evaluate(s,crab,tuning).suitable,"legacy crab requirements retained");
        return "PASS "+checks+" habitat-completion rule assertions (synthetic boundary fixtures; not campaign playthrough).";
    }
    sealed class WalkNode {public HabitatState state;public List<int> steps;}
    static int[] dx={0,1,0,-1},dz={1,0,-1,0};
    public static readonly List<int[]> Route=new List<int[]>();
    static void Walk(HabitatState s,int x,int z)
    {
        var queue=new Queue<WalkNode>();var seen=new HashSet<string>();
        queue.Enqueue(new WalkNode {state=HabitatRules.Clone(s),steps=new List<int>()});
        while(queue.Count>0)
        {
            var node=queue.Dequeue();
            if(node.state.playerX==x&&node.state.playerZ==z)
            {
                foreach(int d in node.steps){if(!HabitatRules.MovePlayer(s,dx[d],dz[d],true,tuning))throw new Exception("route replay failed");Route.Add(new[]{0,dx[d],dz[d]});}
                return;
            }
            for(int d=0;d<4;d++)
            {
                var next=HabitatRules.Clone(node.state);
                if(!HabitatRules.MovePlayer(next,dx[d],dz[d],true,tuning))continue;
                string key=next.playerX+","+next.playerZ;
                foreach(var a in next.animals)key+="/"+a.x+","+a.z;
                if(!seen.Add(key))continue;
                var path=new List<int>(node.steps);path.Add(d);queue.Enqueue(new WalkNode {state=next,steps=path});
            }
        }
        throw new Exception("no legal walk to "+x+","+z+" from "+s.playerX+","+s.playerZ);
    }
    static void Interact(HabitatState s,int x,int z)
    {if(!HabitatRules.Interact(s,x,z))throw new Exception("interaction "+x+","+z+": "+s.message);Route.Add(new[]{1,x,z});}
    static void Lure(HabitatState s,Animal a)
    {int x=a.x,z=a.z;if(!HabitatRules.Lure(s,x,z))throw new Exception(s.message);Route.Add(new[]{2,x,z});}
    public static string ThirdRoute()
    {
        // Starts at the authored spawn; rod and basket represent prerequisites from levels 1/2.
        // No terrain, residents, cash or boxes are injected. Ecology paused for manual puzzle play.
        Route.Clear();var s=HabitatLevels.Create(7);s.rod=true;s.backBasketUnlocked=true;
        Walk(s,6,12);Interact(s,7,12);Walk(s,7,10);Interact(s,8,10);
        Walk(s,5,11);Interact(s,6,11);Walk(s,8,10);Interact(s,9,10);
        Walk(s,10,10);Interact(s,11,10);
        Walk(s,6,10);Walk(s,6,9);Walk(s,6,8);
        var crab=s.animals.Find(a=>a.species==Species.Crab);
        Check(crab.x==6&&crab.z==6,"third route drives original crab two cells south");
        Walk(s,5,6);
        var fish=s.animals.Find(a=>a.species==Species.Goldfish);
        Lure(s,fish);Lure(s,fish);
        Check(HabitatMechanicLevels.CompleteHabitat(s,tuning),"third route from authored spawn satisfies confirmed goal");
        return "PASS stage three model route: "+Route.Count+" real actions, original terrain/animals/boxes; inherited rod+basket; no ecology ticks, not full campaign.";
    }
    public static string FourthRoute()
    {
        Route.Clear();var s=HabitatLevels.Create(8);
        Walk(s,5,9);Interact(s,4,9);Interact(s,5,9);
        Walk(s,3,10);Interact(s,3,9);
        Walk(s,6,9);Interact(s,7,9);Walk(s,3,9);Interact(s,3,8);
        Walk(s,7,12);Interact(s,7,13);Walk(s,3,8);Interact(s,3,7);
        Walk(s,5,2);Interact(s,4,2);Walk(s,6,2);Interact(s,7,2);
        Walk(s,5,5);Interact(s,5,4);Walk(s,8,2);Interact(s,8,3);
        Walk(s,10,4);Interact(s,11,4);Walk(s,8,3);Interact(s,8,4);
        Check(HabitatMechanicLevels.CompleteHabitat(s,tuning),"fourth route from authored spawn satisfies both species");
        Check(!s.tideEverActivated,"fourth route reveals original layout can be solved without tide");
        return "PASS stage four model route: "+Route.Count+" real actions, source crates/float only, no terrain edits or injected items; tide bypass is a design observation.";
    }
}
