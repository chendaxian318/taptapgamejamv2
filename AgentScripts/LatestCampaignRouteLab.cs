using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HabitatIsland;
public static class LatestCampaignRouteLab
{
 public static HabitatState State;
 public static readonly List<int[]> Route=new List<int[]>();
 public static readonly List<string> Evidence=new List<string>();
 static readonly Tuning tuning=new Tuning{animalMovement=false};
 static readonly int[] dx={0,1,0,-1},dz={1,0,-1,0};
 sealed class Node { public HabitatState s;public List<int> path; }
 public static string Begin(int index)
 {Route.Clear();Evidence.Clear();State=HabitatLevels.Create(index);State.rod=true;State.specialRod=true;State.backBasketUnlocked=true;return Dump();}
 public static void Step(int kind,int x,int z)
 {
  bool ok=kind==0?HabitatRules.MovePlayer(State,x,z,true,tuning):kind==1?HabitatRules.Interact(State,x,z):kind==2?HabitatRules.Lure(State,x,z):kind==3?HabitatRules.Rotate(State,x,z):false;
  if(!ok)throw new Exception("Step "+(Route.Count+1)+" ["+kind+","+x+","+z+"]: "+State.message+"\n"+Dump());Route.Add(new[]{kind,x,z});
 }
 public static void I(int x,int z)=>Step(1,x,z);
 public static void W(int x,int z)
 {
  var queue=new Queue<Node>();var seen=new HashSet<string>();queue.Enqueue(new Node{s=HabitatRules.Clone(State),path=new List<int>()});
  while(queue.Count>0&&seen.Count<100000)
  {
   var n=queue.Dequeue();if(n.s.playerX==x&&n.s.playerZ==z){foreach(int d in n.path)Step(0,dx[d],dz[d]);return;}
   for(int d=0;d<4;d++)
   {
    var next=HabitatRules.Clone(n.s);if(!HabitatRules.MovePlayer(next,dx[d],dz[d],true,tuning))continue;
    string key=next.playerX+","+next.playerZ;foreach(var a in next.animals)key+="/"+a.id+":"+a.x+","+a.z;
    // Walking may legally push a box; preserve those distinctions during search.
    for(int k=0;k<next.tiles.Length;k++)if(next.tiles[k].prop!=n.s.tiles[k].prop)key+="p"+k+":"+(int)next.tiles[k].prop;
    if(!seen.Add(key))continue;var p=new List<int>(n.path);p.Add(d);queue.Enqueue(new Node{s=next,path=p});
   }
  }
  throw new Exception("No walk to "+x+","+z+" after "+seen.Count+" nodes\n"+Dump());
 }
 public static string Dump()
 {
  var s=State;var b=new StringBuilder("P="+s.playerX+","+s.playerZ+" held="+s.carryingProp+"/"+s.carryingAnimal+"\n");
  foreach(var a in s.animals)b.AppendLine(a.species+"="+a.x+","+a.z+" carried="+a.carried+" "+HabitatRules.Evaluate(s,a,tuning).reason);
  b.Append("Props:");for(int z=0;z<s.depth;z++)for(int x=0;x<s.width;x++){var q=HabitatRules.TileAt(s,x,z);if(q.prop!=PropKind.None)b.Append(" "+q.prop+"@"+x+","+z+"h"+q.height);}return b.ToString();
 }
 public static string Third()
 {
  Begin(7);
  W(6,12);I(7,12);W(7,10);I(8,10);W(5,11);I(6,11);W(8,10);I(9,10);W(10,10);I(11,10);
  W(6,10);W(6,9);W(6,8);W(5,6);
  I(5,7); // Park the inherited basket's shrimp while arranging the right-hand approach.
  W(9,4);I(10,4);W(10,5);I(10,6);
  W(5,6);I(5,7);W(10,5);W(10,6);I(10,5);
  W(7,10);I(8,10);I(7,10);W(7,7);I(8,7);
  W(7,2);I(8,2);W(8,7);I(8,6);
  W(10,4);I(10,5);W(11,5);I(11,4);
  W(10,5);I(10,6);I(10,5);W(7,3);I(7,4);
  W(4,2);I(4,3);W(7,4);I(7,5);
  return CheckWon();
 }
 public static string CheckWon()
 {bool won=HabitatMechanicLevels.CompleteHabitat(State,tuning);return (won?"PASS":"FAIL")+" "+Route.Count+" legal actions\n"+Dump();}
 public static string Fourth()
 {
  Begin(8);
  W(5,10);I(4,10);I(5,10);W(3,11);I(3,10);
  W(6,10);I(7,10);W(3,10);I(3,9);
  W(7,13);I(7,14);W(3,9);I(3,8);
  W(5,12);I(5,13);W(3,8);I(3,7);
  W(5,2);I(4,2);W(6,1);I(7,1);
  W(5,5);I(5,4);W(9,2);I(9,3);
  W(10,4);I(11,4);W(9,3);I(8,3);
  W(8,1);I(7,1);I(8,1);W(8,3);I(8,4);
  return CheckWon();
 }
 public static string Fifth()
 {
  Begin(10);var s=State;
  s.coins=1150;s.chapterOneAccepted=s.chapterOneDelivered=s.chapterOneMorningSeen=true;s.campaignUnlockedStage=5;
  if(!HabitatRules.Shop(s,"magnet"))throw new Exception(s.message);Route.Add(new[]{8,6,0});
  if(!HabitatRules.Shop(s,"crate"))throw new Exception(s.message);Route.Add(new[]{8,1,0});
  if(!HabitatRules.TransferBoatCrate(s,true))throw new Exception(s.message);Route.Add(new[]{9,1,0});
  W(12,9);I(12,8);
  W(10,13);I(9,13);I(10,13);W(12,8);I(12,7);
  W(11,12);I(11,11);W(9,11);I(8,11);
  W(12,9);I(11,9);W(8,11);I(7,11);
  W(6,11);I(5,11);W(6,10);I(6,9);
  W(4,11);I(4,10);W(6,9);I(7,9);
  W(4,12);I(3,12);W(7,9);
  W(8,4);W(8,2);W(6,2);W(6,3);W(6,4);W(8,4);I(9,4);
  W(10,6);I(10,7);W(7,9);I(7,8);
  W(6,9);if(!HabitatRules.UseUtility(s,6,7,8,0,-1))throw new Exception(s.message);Route.Add(new[]{7,6,7,8,0,-1});
  return CheckWon()+"\nBought one magnet (300) and one crate (150) from inherited 1150; remaining="+s.coins;
 }
}
