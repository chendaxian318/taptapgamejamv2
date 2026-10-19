using System;
using System.Linq;
using HabitatIsland;

// Replay exported player commands from a fresh level; no route-search candidate is reused.
public static class SecondDefaultMovementChecks
{
    static void Require(bool okay,string message)
    { if(!okay)throw new InvalidOperationException(message); }

    public static HabitatState Replay(int[][] commands,bool segmented)
    {
        var tuning=new Tuning();
        Require(tuning.animalMovement,"Verification must retain the default animal movement.");
        var state=HabitatLevels.Create(6);
        state.coins=200;state.rod=state.specialRod=true;
        state.chapterOneAccepted=state.chapterOneDelivered=state.chapterOneMorningSeen=true;
        state.campaignUnlockedStage=2;
        var heights=state.tiles.Select(t=>t.height).ToArray();
        int boxes=state.tiles.Count(t=>t.prop==PropKind.Crate||t.prop==PropKind.Float);
        int initialAnimals=state.animals.Count;
        for(int i=0;i<commands.Length;i++)
        {
            var a=commands[i];bool okay;
            switch(a[0])
            {
                case 0: okay=HabitatRules.MovePlayer(state,a[1],a[2],true,tuning);break;
                case 1: okay=HabitatRules.Interact(state,a[1],a[2]);break;
                case 3: okay=HabitatRules.Rotate(state,a[1],a[2]);break;
                case 4: okay=HabitatRules.Shop(state,"fan")&&HabitatRules.StoreHeld(state);break;
                case 5: okay=HabitatRules.TakeBagProp(state,a[1]);break;
                case 6:
                    float seconds=a[1]/10f;
                    int steps=segmented?Math.Max(1,(int)Math.Ceiling(seconds/.1f)):1;
                    for(int n=0;n<steps;n++)HabitatRules.Tick(state,tuning,seconds/steps);
                    okay=true;break;
                default: throw new InvalidOperationException("Unsupported route command "+a[0]);
            }
            Require(okay,"Replay command "+(i+1)+" ["+string.Join(",",a)+"] failed: "+state.message);
            HabitatMechanicLevels.CompleteHabitat(state,tuning);
        }
        Require(state.won&&state.animals.All(a=>HabitatRules.Evaluate(state,a,tuning).suitable),"All original residents must be suitable at completion.");
        Require(state.backBasketUnlocked,"Route must obtain the actual basket chest.");
        Require(state.coins==0&&state.magnets==0&&state.launchers==0&&state.shovels==0,"No invented balance or consumable grants.");
        Require(state.tiles.Select(t=>t.height).SequenceEqual(heights),"Terrain heights must be unchanged.");
        Require(state.animals.Count==initialAnimals,"No resident may disappear to manufacture completion.");
        Require(state.carryingProp==PropKind.None&&state.tiles.Count(t=>t.prop==PropKind.Crate||t.prop==PropKind.Float)==boxes,"Original box/float inventory must be conserved.");
        Require(Math.Abs(state.elapsed-10.1f)<.001f,"Only the explicit ferry tick advances ecology.");
        return state;
    }

    public static string Compare(int[][] commands)
    {
        var single=Replay(commands,false);var segmented=Replay(commands,true);
        Require(single.playerX==segmented.playerX&&single.playerZ==segmented.playerZ&&single.seed==segmented.seed&&single.tickCount==segmented.tickCount,"Single and controller-segmented ticks must agree on player, RNG and movement cycles.");
        for(int i=0;i<single.tiles.Length;i++)
        {
            var a=single.tiles[i];var b=segmented.tiles[i];
            Require(a.prop==b.prop&&a.coveredProp==b.coveredProp&&a.rotation==b.rotation,"Single and segmented ticks disagree on tile "+i);
        }
        foreach(var a in single.animals)
        {
            var b=segmented.animals.Find(v=>v.id==a.id);
            Require(b!=null&&a.x==b.x&&a.z==b.z&&a.carried==b.carried&&a.offspring==b.offspring&&Math.Abs(a.progress-b.progress)<.001f&&Math.Abs(a.moveClock-b.moveClock)<.001f,"Single and segmented ticks disagree on animal "+a.id);
        }
        return "PASS second default route independently replayed twice: "+commands.Length+" actions; animalMovement=true; single 10.1s Tick and actual controller .1s segmentation agree; original terrain/residents/boxes retained, real basket chest and 200-coin fan.";
    }
}
