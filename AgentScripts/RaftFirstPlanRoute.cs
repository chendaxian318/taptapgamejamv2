using System;using System.Linq;using System.Collections.Generic;using System.IO;using HabitatIsland;
public static class RaftFirstPlanRoute {
 public static List<int[]> Route=new List<int[]>();public static HabitatState Result;static HabitatState s;static Tuning t=new Tuning{animalMovement=false};
 static int[] dx={0,1,0,-1},dz={1,0,-1,0};
 sealed class Node{public HabitatState s;public List<int[]> path;}
 static string Key(HabitatState p)=>p.playerX+","+p.playerZ+":"+string.Join(";",p.tiles.Select((q,i)=>new{q,i}).Where(a=>a.q.prop==PropKind.Raft).Select(a=>a.i+","+a.q.rotation));
 static void Nav(int x,int z){var queue=new Queue<Node>();queue.Enqueue(new Node{s=HabitatRules.Clone(s),path=new List<int[]>()});var seen=new HashSet<string>();while(queue.Count>0){var n=queue.Dequeue();if(!seen.Add(Key(n.s)))continue;if(n.s.playerX==x&&n.s.playerZ==z){foreach(var a in n.path)Act(a);return;}
 for(int d=0;d<4;d++){var next=HabitatRules.Clone(n.s);if(!HabitatRules.MovePlayer(next,dx[d],dz[d],true,t))continue;var path=new List<int[]>(n.path){new[]{0,dx[d],dz[d]}};queue.Enqueue(new Node{s=next,path=path});}}
 throw new Exception("No walking/raft path to "+x+","+z+" from "+s.playerX+","+s.playerZ);}
 static void Act(int[] a){bool ok;if(a[0]==6){HabitatRules.Tick(s,t,a[1]/10f);ok=true;}else ok=a[0]==0?HabitatRules.MovePlayer(s,a[1],a[2],true,t):a[0]==1?HabitatRules.Interact(s,a[1],a[2]):a[0]==3?HabitatRules.Rotate(s,a[1],a[2]):a[0]==5?HabitatRules.TakeBagProp(s,a[1]):a[0]==12?HabitatRules.PullPlayer(s,a[1],a[2]):HabitatRules.PushBox(s,a[1],a[2]);if(!ok)throw new Exception("action "+string.Join(",",a)+" failed: "+s.message);Route.Add(a);}
 public static string Run(){Route.Clear();s=HabitatLevels.Create(5);
 Nav(12,6);Act(new[]{1,12,5}); // Board bamboo and obtain the authored rod.
 Nav(10,8);Act(new[]{11,9,8});Nav(9,8);Act(new[]{11,8,8}); // North gap in PDF.
 Nav(7,4);Act(new[]{11,8,4});Nav(8,4);Act(new[]{11,9,4}); // First south-east sea box.
 Nav(7,2);Act(new[]{11,7,3});Nav(6,4);Act(new[]{11,7,4});Nav(7,4);Act(new[]{11,8,4}); // Second gap.
 if(!HabitatMechanicLevels.CompleteHabitat(s,t))throw new Exception("PDF end state is not suitable");
 Result=s;File.WriteAllText("Verification/feedback-20261018-144126/first-route.json","["+string.Join(",",Route.Select(a=>"["+string.Join(",",a)+"]"))+"]");
 return "PASS first PDF solution with new bamboo, original three boxes, no purchases: "+Route.Count+" actions";
 }
 public static string RunFourth(){Route.Clear();s=HabitatLevels.Create(8);
 Nav(7,13);Act(new[]{12,0,-1});Nav(6,13);Act(new[]{11,7,13});Nav(8,12);Act(new[]{12,0,-1});
 Nav(7,11);Act(new[]{11,7,10});Nav(7,12);Act(new[]{11,8,12});
 if(!s.tideActive||s.waterLevel!=1)throw new Exception("plate did not raise tide");
 Nav(8,13);Nav(11,13);Nav(8,13); // Board and return with elevated raft across east strait.
 Nav(8,12);Act(new[]{11,9,12});if(s.tideActive||s.waterLevel!=0)throw new Exception("plate did not release");
 Nav(5,10);Nav(4,5); // Lower bamboo to south bank after ebb.
 Result=s;File.WriteAllText("Verification/feedback-20261018-144126/fourth-transport-route.json","["+string.Join(",",Route.Select(a=>"["+string.Join(",",a)+"]"))+"]");
 return "PASS fourth fixed stair + rise + east raft round trip + ebb + southern raft landing: "+Route.Count+" actions";
 }
 public static string RunSecond(){Route.Clear();s=HabitatLevels.Create(6);s.bagProps.Add(PropKind.Fan);Act(new[]{5,0,0});
 Nav(3,8);Nav(4,8);Act(new[]{1,3,8});Act(new[]{3,3,8});Act(new[]{6,30,0});
 Nav(11,8);Act(new[]{1,12,8});Nav(4,8);Act(new[]{1,3,8});Nav(3,8);
 if(!s.backBasketUnlocked||s.carryingProp!=PropKind.Fan)throw new Exception("second roundtrip failed");
 Result=s;File.WriteAllText("Verification/feedback-20261018-144126/second-transport-route.json","["+string.Join(",",Route.Select(a=>"["+string.Join(",",a)+"]"))+"]");
 return "PASS second bamboo board, deploy/rotate bellows, east crossing, basket chest and return with recovered bellows: "+Route.Count+" actions";
 }
}
