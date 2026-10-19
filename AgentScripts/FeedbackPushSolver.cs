using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using HabitatIsland;
public static class FeedbackPushSolver {
 public static List<int[]> Route; public static HabitatState Result;
 static int[] dx={0,1,0,-1},dz={1,0,-1,0}; static Tuning tuning=new Tuning{animalMovement=false};
 sealed class Node{public HabitatState s;public List<int[]> route;public int depth;}
 static Dictionary<int,List<int[]>> Walk(HabitatState s){var paths=new Dictionary<int,List<int[]>>();var queue=new Queue<int>();int start=s.playerZ*s.width+s.playerX;paths[start]=new List<int[]>();queue.Enqueue(start);
 while(queue.Count>0){int id=queue.Dequeue();for(int d=0;d<4;d++){var copy=HabitatRules.Clone(s);copy.playerX=id%s.width;copy.playerZ=id/s.width;if(!HabitatRules.MovePlayer(copy,dx[d],dz[d],true,tuning))continue;int n=copy.playerZ*s.width+copy.playerX;if(paths.ContainsKey(n))continue;var p=new List<int[]>(paths[id]);p.Add(new[]{0,dx[d],dz[d]});paths[n]=p;queue.Enqueue(n);}}return paths;}
 static string Key(HabitatState s,int region){var b=new System.Text.StringBuilder(s.rod+":"+region+":");for(int i=0;i<s.tiles.Length;i++)if(HabitatRules.IsBox(s.tiles[i].prop))b.Append(i+","+(int)s.tiles[i].prop+","+(int)s.tiles[i].coveredProp+";");return b.ToString();}
 static int Score(HabitatState s,Dictionary<int,List<int[]>> walk,int depth){int[,] targets=s.rod?new int[,]{{6,5},{7,6},{8,5}}:new int[,]{{9,4},{10,4},{11,4}};int score=depth;
 for(int i=0;i<3;i++){int x=targets[i,0],z=targets[i,1];int best=30;for(int j=0;j<s.tiles.Length;j++)if(HabitatRules.IsBox(s.tiles[j].prop))best=Math.Min(best,Math.Abs(j%s.width-x)+Math.Abs(j/s.width-z));score+=best*12;}
 if(!s.rod)score+=walk.Keys.Min(i=>Math.Abs(i%s.width-12)+Math.Abs(i/s.width-5))*30;else score-=150;
 return score;}
 public static string Run(int limit=60000){var start=HabitatLevels.Create(5);var open=new SortedDictionary<int,Queue<Node>>();var visited=new HashSet<string>();int count=0;
 Action<Node,int> add=(n,c)=>{if(!open.TryGetValue(c,out var q)){q=new Queue<Node>();open[c]=q;}q.Enqueue(n);};add(new Node{s=start,route=new List<int[]>()},0);
 while(open.Count>0&&count<limit){int k=open.First().Key;var node=open[k].Dequeue();if(open[k].Count==0)open.Remove(k);var walk=Walk(node.s);string key=Key(node.s,walk.Keys.Min());if(!visited.Add(key))continue;count++;
 if(count%500==0)File.WriteAllText("Verification/feedback-20261018-140142/solver-progress.txt","states="+count+" open="+open.Sum(p=>p.Value.Count)+" depth="+node.depth+" rod="+node.s.rod+" score="+k);
 if(!node.s.rod){foreach(var p in walk){int x=p.Key%node.s.width,z=p.Key/node.s.width;if(Math.Abs(x-12)+Math.Abs(z-5)>1)continue;var s=HabitatRules.Clone(node.s);s.playerX=x;s.playerZ=z;if(!HabitatRules.Interact(s,12,5))continue;var r=new List<int[]>(node.route);r.AddRange(p.Value);r.Add(new[]{1,12,5});add(new Node{s=s,route=r,depth=node.depth+1},Score(s,Walk(s),node.depth+1));}}
 if(node.s.rod&&HabitatMechanicLevels.CompleteHabitat(node.s,tuning)){Route=node.route;Result=node.s;return "PASS first push-only route actions="+Route.Count+" searched="+count;}
 foreach(var p in walk){int x=p.Key%node.s.width,z=p.Key/node.s.width;for(int d=0;d<4;d++){int bx=x+dx[d],bz=z+dz[d];var tile=HabitatRules.TileAt(node.s,bx,bz);if(tile==null||!HabitatRules.IsBox(tile.prop))continue;
 for(int action=0;action<3;action++){var s=HabitatRules.Clone(node.s);s.playerX=x;s.playerZ=z;s.events.Clear();bool ok=action==0?HabitatRules.PushBox(s,bx,bz):action==1?HabitatRules.PullPlayer(s,-dx[d],-dz[d]):HabitatRules.RecoverBox(s,bx,bz);if(!ok)continue;
 var r=new List<int[]>(node.route);r.AddRange(p.Value);r.Add(action==0?new[]{11,bx,bz}:action==1?new[]{12,-dx[d],-dz[d]}:new[]{13,bx,bz});var n=new Node{s=s,route=r,depth=node.depth+1};var nw=Walk(s);if(!visited.Contains(Key(s,nw.Keys.Min())))add(n,Score(s,nw,n.depth));}}}
 }return "FAIL no route in "+count+" searched states";}
}
