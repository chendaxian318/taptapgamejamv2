using System;
using System.Collections.Generic;

namespace HabitatIsland
{
    /// <summary>Checks starter routes under the exclusive climbing rule, without inventing a formal-map ending.</summary>
    public static class HabitatLevelAccessChecks
    {
        static int assertions;
        static void Check(bool condition,string label)
        { assertions++;if(!condition)throw new InvalidOperationException("Level access check failed: "+label); }
        static void Step(HabitatState state,int dx,int dz,bool climb=false)
        {
            int x=state.playerX+dx,z=state.playerZ+dz;
            Check(HabitatRules.MovePlayer(state,dx,dz,climb)&&state.playerX==x&&state.playerZ==z,
                state.levelName+" reaches ("+x+","+z+")"+(climb?" via climbing block":""));
        }
        // This examines the INITIAL dry layout only: no pushing, tide activation,
        // recovery, carrying, animal motion or boat travel. Unreachable cells are
        // not evidence that the whole puzzle is unsolvable.
        static HashSet<int> InitialDryReach(HabitatState state)
        {
            var reached=new HashSet<int>();var pending=new Queue<int>();
            int start=state.playerZ*state.width+state.playerX;
            reached.Add(start);pending.Enqueue(start);
            int[] dx={1,-1,0,0},dz={0,0,1,-1};
            while(pending.Count>0)
            {
                int id=pending.Dequeue(),x=id%state.width,z=id/state.width;
                for(int i=0;i<4;i++)
                {
                    int nx=x+dx[i],nz=z+dz[i];
                    if(!HabitatRules.Inside(state,nx,nz))continue;
                    var probe=HabitatRules.Clone(state);probe.playerX=x;probe.playerZ=z;
                    // Shift suppresses pushing; only player traversal on the
                    // original layout is tested, using the real movement rule.
                    if(!HabitatRules.MovePlayer(probe,dx[i],dz[i],true)||probe.playerX!=nx||probe.playerZ!=nz)continue;
                    int next=nz*state.width+nx;
                    if(reached.Add(next))pending.Enqueue(next);
                }
            }
            return reached;
        }
        static bool Reaches(HabitatState state,HashSet<int> reached,int x,int z)
        {return reached.Contains(z*state.width+x);}

        public static string Run()
        {
            assertions=0;
            Check(HabitatLevels.Names[0].StartsWith("教学")&&HabitatLevels.Names[1].StartsWith("练习")&&
                HabitatLevels.Names[2].StartsWith("练习")&&HabitatLevels.Names[3].StartsWith("沙盒"),
                "prototype maps are labelled as teaching, practice and sandbox rather than four finished story chapters");
            HabitatState state=HabitatLevels.Create(0);
            Check(HabitatRules.Interact(state,2,2)&&HabitatRules.Interact(state,1,3),
                "the introductory shrimp relocation still works directly from spawn without climbing");
            HabitatRules.Tick(state,new Tuning{animalMovement=true,breedSeconds=1},1.1f);
            Check(state.won,"the introductory crab objective remains achievable with the new player traversal rule");

            state=HabitatLevels.Create(1);
            Check(HabitatRules.Shop(state,"soil"),"pond practice grants the first soil block at its dock");
            Step(state,1,0);Step(state,1,0);
            Check(HabitatRules.PlaceSoil(state,4,2),"the pond can be closed from a reachable bank");
            Step(state,-1,0);Step(state,-1,0);
            Check(HabitatRules.Shop(state,"soil"),"the second soil block remains reachable after closing the pond");
            Step(state,1,0);Step(state,0,1);Step(state,0,1);Step(state,0,1);
            Step(state,1,0);Step(state,1,0);Step(state,1,0);
            Check(HabitatRules.PlaceSoil(state,6,5),"the turtle's second highland cell can be filled from low ground");
            HabitatRules.Tick(state,new Tuning{animalMovement=false,breedSeconds=1},1.1f);
            Check(state.won,"pond and highland practice can achieve both goals without climbing ordinary terrain");

            state=HabitatLevels.Create(2);
            Check(HabitatRules.TileAt(state,2,3).prop==PropKind.ClimbBlock,
                "the tide practice spawn pocket has a dedicated climbing block");
            Step(state,1,0);Step(state,0,1,true);Step(state,0,1);Step(state,1,0);
            Check(state.tideActive&&state.waterLevel==1,"the actual spawn-to-plate route activates tide without teleporting the player");
            HabitatRules.Tick(state,new Tuning{animalMovement=false,breedSeconds=1},1.1f);
            Check(state.won,"tide practice still completes both breeding goals after its reachable crate push");

            state=HabitatLevels.Create(3);
            HabitatRules.Tick(state,new Tuning{animalMovement=false,breedSeconds=1},1.1f);
            Check(state.won,"the sandbox's initial ecology remains a demonstration and requires no inaccessible climb");

            state=HabitatLevels.Create(HabitatFigJamLevel.Index);
            HashSet<int> reached=InitialDryReach(state);
            Check(state.width==18&&state.depth==19&&state.tiles.Length==342,"formal source-map dimensions remain unchanged");
            Check(Reaches(state,reached,6,12)&&Reaches(state,reached,6,13)&&Reaches(state,reached,6,14),
                "the formal map's existing climbing block still connects the starting island to its upper tide platform");
            Check(Reaches(state,reached,12,13),"a bank adjacent to the fishing-rod chest remains initially reachable");
            Check(!Reaches(state,reached,7,10),"ordinary climbing no longer directly reaches the high shrimp pedestal");
            Check(!Reaches(state,reached,8,5)&&!Reaches(state,reached,15,7),
                "the lower and eastern islands require puzzle actions rather than an invented dry route");
            Check(state.goals.Length==0&&!state.won,"the formal map still has no fabricated final objective or completion claim");
            return "PASS: "+assertions+" level access assertions (teaching route, soil practice, tide practice, sandbox and initial formal-map reachability only)";
        }
    }
}
