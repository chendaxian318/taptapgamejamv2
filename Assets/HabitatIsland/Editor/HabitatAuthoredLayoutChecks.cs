using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace HabitatIsland.Editor
{
    /// <summary>Invoked inside Unity only. Audits independent source data and replays legal model actions.</summary>
    public static class HabitatAuthoredLayoutChecks
    {
        [Serializable] public class SourceDocument { public SourceTable[] tables; }
        [Serializable] public class SourceTable { public string nodeId; public SourceCell[] cells; }
        [Serializable] public class SourceCell
        {
            public string nodeId,rawText,terrain,prop,animal,reward;
            public int mapColumn,mapRow,height;
            public bool hasExplicitHeight;
        }
        sealed class SearchNode
        {
            public HabitatState state;
            public List<int> steps;
        }
        static readonly int[] DX={0,1,0,-1},DZ={1,0,-1,0};
        static int assertions,actions;
        static StringBuilder report;
        static void Check(bool pass,string label)
        { assertions++;if(!pass)throw new InvalidOperationException("Authored layout check failed: "+label); }
        static Tile Q(HabitatState s,int x,int z){return HabitatRules.TileAt(s,x,z);}
        static int Distance(HabitatState s,int x,int z){return Math.Abs(s.playerX-x)+Math.Abs(s.playerZ-z);}
        static PropKind SourceProp(string value)
        {
            switch(value)
            {
                case null:case "":case "boat_marker":return PropKind.None;
                case "crate":return PropKind.Crate;
                case "floating_block":return PropKind.Float;
                case "climb_block":return PropKind.ClimbBlock;
                case "tide_engine":return PropKind.Tide;
                case "obstacle":return PropKind.Obstacle;
                case "treasure_chest":return PropKind.Chest;
                default:throw new InvalidOperationException("Unknown source prop: "+value);
            }
        }
        static Species SourceAnimal(string value)
        {
            switch(value)
            {
                case "fish":return Species.Goldfish;
                case "shrimp":return Species.Shrimp;
                case "crab":return Species.Crab;
                default:throw new InvalidOperationException("Unknown source animal: "+value);
            }
        }
        static void VerifyLayout(string projectRoot)
        {
            string path=Path.Combine(projectRoot,"Docs/SourceEvidence/LevelDesign/level-grid.json");
            Check(File.Exists(path),"independent source transcription exists");
            var source=JsonUtility.FromJson<SourceDocument>(File.ReadAllText(path));
            Check(source!=null&&source.tables!=null&&source.tables.Length==4,"four authored tables are present");
            var s=HabitatFigJamLevel.Create();
            Check(s.width==18&&s.depth==19&&s.tiles.Length==342,"assembled dimensions are 18 by 19");
            Check(s.authoredLevel&&s.sourceNodeId=="0:1"&&s.levelIndex==4,"authored map identity");
            Check(s.playerX==3&&s.playerZ==10&&s.dockX==3&&s.dockZ==10&&s.dockRotation==3,"spawn is on land east of original boat");
            Check(s.coins==0&&s.cargo==0&&s.traps==0&&!s.rod,"no unplanned starting equipment or currency");
            Check(Q(s,4,9).trap&&s.tiles.Count(t=>t.trap)==1,"one usable cage beside the landing, added for requested model placement");
            var seen=new HashSet<int>();int animalCount=0;
            foreach(var table in source.tables)foreach(var cell in table.cells)
            {
                int x=cell.mapColumn,z=18-cell.mapRow;
                string at=cell.nodeId+" source("+cell.mapColumn+","+cell.mapRow+") world("+x+","+z+")";
                Check(HabitatRules.Inside(s,x,z)&&seen.Add(z*s.width+x),"unique in-bounds source coordinate "+at);
                Tile tile=Q(s,x,z);
                int expectedHeight=cell.height;
                // These are the two documented interpretation choices, not extra source claims.
                if(cell.terrain=="boat_placeholder")expectedHeight=0;
                else if(cell.terrain=="ground"&&!cell.hasExplicitHeight)expectedHeight=1;
                Check(tile.height==expectedHeight,"terrain height "+at);
                Check(tile.prop==SourceProp(cell.prop),"prop kind "+at);
                string expectedLabel=string.IsNullOrEmpty(cell.rawText)?".":cell.rawText;
                Check(HabitatFigJamLevel.CellLabel(x,z)==expectedLabel,"raw cell label and world-axis mapping "+at);
                var residents=s.animals.FindAll(a=>a.x==x&&a.z==z);
                if(string.IsNullOrEmpty(cell.animal))Check(residents.Count==0,"no invented animal "+at);
                else
                {
                    animalCount++;
                    Check(residents.Count==1&&residents[0].species==SourceAnimal(cell.animal),"exact source animal "+at);
                }
                if(tile.prop==PropKind.Chest)
                    Check(tile.chestReward==(cell.reward=="fishing_rod"?ChestReward.FishingRod:ChestReward.Unspecified),"source chest reward "+at);
            }
            Check(seen.Count==342&&animalCount==4&&s.animals.Count==4,"every source cell and all four animals checked");
            Check(s.tiles.Count(t=>t.prop==PropKind.Crate)==5&&s.tiles.Count(t=>t.prop==PropKind.Float)==4&&s.tiles.Count(t=>t.prop==PropKind.ClimbBlock)==1,"source movable prop counts");
            Check(s.tiles.Count(t=>t.prop==PropKind.Tide)==1&&s.tiles.Count(t=>t.prop==PropKind.Obstacle)==2&&s.tiles.Count(t=>t.prop==PropKind.Chest)==2,"source fixed prop counts");
            Check(s.goals.Length==0&&!s.won,"unfinished authored task has no fabricated win condition");
            report.AppendLine("SOURCE: independently compared all 342 cells against level-grid.json: coordinates, height, raw labels, props, residents, and chest rewards.");
            report.AppendLine("INTERPRETATIONS: numerals are ground height; unnumbered final chest uses height 1; boat marker cells use water height 0. World Z = 18 - source row.");
        }

        // Every search edge calls the actual movement rule on a clone. Climb mode prevents
        // unintentional pushes, so position is sufficient for the visited key of this walk search.
        static List<int> FindWalk(HabitatState s,Func<HabitatState,bool> destination)
        {
            var queue=new Queue<SearchNode>();var visited=new HashSet<int>();
            queue.Enqueue(new SearchNode{state=HabitatRules.Clone(s),steps=new List<int>()});
            visited.Add(s.playerZ*s.width+s.playerX);
            while(queue.Count>0)
            {
                var current=queue.Dequeue();
                if(destination(current.state))return current.steps;
                for(int d=0;d<4;d++)
                {
                    var next=HabitatRules.Clone(current.state);
                    if(!HabitatRules.MovePlayer(next,DX[d],DZ[d],true))continue;
                    int key=next.playerZ*next.width+next.playerX;if(!visited.Add(key))continue;
                    var route=new List<int>(current.steps);route.Add(d);
                    queue.Enqueue(new SearchNode{state=next,steps=route});
                }
            }
            return null;
        }
        static void Walk(HabitatState s,Func<HabitatState,bool> destination,string label)
        {
            var route=FindWalk(s,destination);Check(route!=null,label+" has an actual legal path");
            var trace=new StringBuilder("("+s.playerX+","+s.playerZ+")");
            foreach(int d in route)
            {
                int beforeX=s.playerX,beforeZ=s.playerZ;
                Check(HabitatRules.MovePlayer(s,DX[d],DZ[d],true),label+" step accepted");
                Check(Math.Abs(s.playerX-beforeX)+Math.Abs(s.playerZ-beforeZ)==1,label+" advances exactly one cell");
                actions++;trace.Append(" -> ("+s.playerX+","+s.playerZ+")");
            }
            Check(destination(s),label+" endpoint reached");
            report.AppendLine("WALK "+label+": "+trace);
        }
        static void Act(HabitatState s,Func<bool> action,string label)
        { Check(action(),label);actions++;report.AppendLine("ACTION "+label+"; player=("+s.playerX+","+s.playerZ+"), sea="+s.waterLevel+", held="+s.carryingProp); }

        static void VerifyRoutes()
        {
            var original=HabitatFigJamLevel.Create();
            Check(FindWalk(original,p=>Distance(p,5,5)<=1)==null,"crab island has no initial unmodified walking connection");
            Check(FindWalk(original,p=>Distance(p,16,7)==1)==null,"final chest island has no initial unmodified walking connection");
            report.AppendLine("CONSTRAINT: initially, the crab island (crab world 5,5) and final chest island (16,7) cannot be reached by walking/climbing alone. Floating blocks or crate bridges are required.");
            var s=HabitatFigJamLevel.Create();
            Walk(s,p=>p.playerX==6&&p.playerZ==14,"spawn to west side of tide crate");
            Act(s,()=>HabitatRules.MovePlayer(s,1,0),"push original crate east onto original tide plate");
            Check(s.tideActive&&s.tideEverActivated&&s.waterLevel==1&&Q(s,8,14).coveredProp==PropKind.Tide,"tide triggered by original map crate");
            Check(FindWalk(s,p=>Distance(p,12,14)==1)==null,"raised sea cuts the walking route to rod chest");
            report.AppendLine("CONSTRAINT: after tide rises, height-1 connecting land is flooded; rod chest is not reachable by walking alone while the plate stays pressed.");
            Act(s,()=>HabitatRules.Interact(s,8,14),"lift crate from tide plate to restore low water");
            Check(!s.tideActive&&s.tideEverActivated&&s.waterLevel==0,"historical tide checkpoint survives release");
            Act(s,()=>HabitatRules.Interact(s,7,15),"place carried crate on adjacent original highland");
            Walk(s,p=>Distance(p,12,14)==1,"low-water route to fishing-rod chest");
            Act(s,()=>HabitatRules.Interact(s,12,14),"open source fishing-rod chest");
            Check(s.rod&&s.rodFromChest&&s.openedChests==1&&s.coins==0,"rod obtained from original chest without bonus funds");

            Walk(s,p=>Distance(p,9,14)==1,"return to original northern floating block");
            Act(s,()=>HabitatRules.Interact(s,9,14),"pick up existing float for southern crossing");
            Check(s.carryingProp==PropKind.Float,"existing float is carried");
            Walk(s,p=>p.playerX==10&&p.playerZ==9,"carry float to upper island southern bank");
            Act(s,()=>HabitatRules.Interact(s,10,8),"place float in one-cell gap between original islands");
            Check(Q(s,10,8).prop==PropKind.Float,"bridge occupies the original water cell");
            Walk(s,p=>Distance(p,5,5)<=1,"cross relocated float and walk to original crab");
            Check(s.coins==0&&s.cargo==0&&s.openedChests==1,"route used only original movable props, without shopping or terrain grants");
            Check(s.goals.Length==0&&!s.won,"route checkpoints do not fabricate an ending");
            report.AppendLine("ROUTE: spawn -> tide activation -> plate release -> original rod chest -> relocate existing float -> crab island, all via actual MovePlayer/Interact calls. No player-position assignment or added resources was used.");
            report.AppendLine("TEST MODE: paused animal simulation, matching planner pause/step controls; no Tick calls are required for these movement and interaction routes.");
            report.AppendLine("DESIGN LIMIT: source contains only fish, shrimp and crab. Fish provides one friendly species; shrimp does not provide crab diversity, so the legacy two-friendly-species breeding goal is not attainable without additional source-defined content. This authored map intentionally has no such victory goal.");
            report.AppendLine("DESIGN LIMIT: no starting fan or currency is supplied by the source. Fan purchase needs an earned economy path; this audit does not grant one or claim the final task is complete.");
        }
        public static string Run()
        {
            assertions=0;actions=0;report=new StringBuilder();
            string projectRoot=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            string output=Path.Combine(projectRoot,"Verification/authored-layout-checks.txt");
            try
            {
                VerifyLayout(projectRoot);VerifyRoutes();
                report.Insert(0,"PASS: "+assertions+" authored layout/path assertions; "+actions+" replayed legal actions.\n");
            }
            catch(Exception e)
            {
                report.Insert(0,"FAIL after "+assertions+" assertions / "+actions+" legal actions: "+e.Message+"\n");
                Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,report.ToString());throw;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,report.ToString());
            return report.ToString();
        }
    }
}
