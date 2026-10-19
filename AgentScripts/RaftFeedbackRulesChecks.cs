using System;using System.Linq;using System.Collections.Generic;using HabitatIsland;
public static class RaftFeedbackRulesChecks {
 public static List<string> Evidence=new List<string>();
 static void C(bool b,string m){Evidence.Add((b?"PASS ":"FAIL ")+m);if(!b)throw new Exception(m);}
 static HabitatState Board()=>new HabitatState{width=7,depth=7,levelIndex=1,playerX=2,playerZ=3,dockX=2,dockZ=3,facingX=1,facingZ=0,tiles=Enumerable.Range(0,49).Select(_=>new Tile{height=1}).ToArray()};
 static Tile Q(HabitatState s,int x,int z)=>HabitatRules.TileAt(s,x,z);
 static HabitatState Raft(){var s=Board();for(int x=2;x<=5;x++)Q(s,x,3).height=0;Q(s,2,3).prop=PropKind.Raft;Q(s,2,3).rotation=1;return s;}
 public static string Run(){Evidence.Clear();var t=new Tuning{animalMovement=false};var s=Board();Q(s,3,3).prop=PropKind.ClimbBlock;s.magnets=s.launchers=1;
 C(!HabitatRules.Interact(s,3,3)&&s.carryingProp==PropKind.None,"fixed stairs cannot be carried");
 C(!HabitatRules.PushBox(s,3,3)&&!HabitatRules.PullPlayer(s,-1,0),"fixed stairs cannot be pushed or pulled");
 C(!HabitatRules.IsRecoveryTarget(s,3,3),"fixed stairs cannot be net-recovered");
 C(!HabitatRules.UseUtility(s,6,3,3,1,0)&&!HabitatRules.UseUtility(s,7,3,3,1,0)&&s.magnets==1&&s.launchers==1,"magnet and launcher reject stairs without consuming");
 C(!HabitatRules.TransferBoatCrate(s,false),"fixed stairs cannot enter ship cargo");
 C(!HabitatRules.MovePlayer(s,1,0,false)&&Q(s,3,3).prop==PropKind.ClimbBlock,"walking cannot push fixed stair");
 C(HabitatRules.MovePlayer(s,1,0,true)&&s.playerX==3&&Q(s,3,3).prop==PropKind.ClimbBlock,"Shift climbs fixed stair without moving it");
 s=Board();Q(s,3,3).height=2;Q(s,3,3).prop=PropKind.ClimbBlock;C(HabitatRules.MovePlayer(s,1,0,true),"fixed stair permits two-layer ascent");
 s=Board();Q(s,3,3).height=3;Q(s,3,3).prop=PropKind.ClimbBlock;C(!HabitatRules.MovePlayer(s,1,0,true),"fixed stair rejects over-two-layer ascent");
 s=Board();Q(s,3,3).height=2;C(!HabitatRules.MovePlayer(s,1,0,true),"ordinary land retains confirmed no-upward-climb rule");
 s=Raft();C(!HabitatRules.IsWater(s,2,3)&&HabitatRules.Height(s,2,3)==1,"bamboo deck supports player at height one");
 C(HabitatRules.MovePlayer(s,1,0)&&s.playerX==3&&Q(s,2,3).prop==PropKind.None&&Q(s,3,3).prop==PropKind.Raft,"raft travels with passenger, without leaving duplicate support");
 C(HabitatRules.MovePlayer(s,-1,0)&&s.playerX==2&&Q(s,2,3).prop==PropKind.Raft,"raft returns along same route");
 Q(s,2,4).height=0;C(!HabitatRules.MovePlayer(s,0,1)&&s.playerZ==3,"raft rejects off-axis travel into sea");
 C(HabitatRules.MovePlayer(s,-1,0)&&s.playerX==1&&Q(s,2,3).prop==PropKind.Raft,"dismount leaves raft at shore for return trip");
 C(HabitatRules.MovePlayer(s,1,0)&&s.playerX==2,"same raft can be boarded again");
 var copy=HabitatRules.Clone(s);C(Q(copy,2,3).prop==PropKind.Raft&&Q(copy,2,3).rotation==1,"clone retains raft identity and direction");
 Q(s,3,3).prop=PropKind.Obstacle;C(!HabitatRules.MovePlayer(s,1,0)&&Q(s,2,3).prop==PropKind.Raft,"reef blocks raft without consuming it");
 s=Raft();s.animals.Add(new Animal{id=1,x=3,z=3,homeX=3,homeZ=3,species=Species.Goldfish});C(!HabitatRules.MovePlayer(s,1,0),"raft cannot cover a water animal");
 s=Raft();Q(s,3,3).trap=true;C(!HabitatRules.MovePlayer(s,1,0),"raft cannot cover cage");
 s=Raft();Q(s,3,3).prop=PropKind.Shell;C(HabitatRules.MovePlayer(s,1,0)&&Q(s,3,3).coveredProp==PropKind.Shell,"raft preserves collectible below deck");
 C(HabitatRules.MovePlayer(s,1,0)&&Q(s,3,3).prop==PropKind.Shell,"departing raft restores collectible");
 s=Raft();s.playerX=1;s.magnets=s.launchers=1;C(!HabitatRules.Interact(s,2,3)&&!HabitatRules.PushBox(s,2,3)&&!HabitatRules.IsRecoveryTarget(s,2,3),"bamboo is not box, pickup or recovery target");
 C(!HabitatRules.UseUtility(s,6,2,3,1,0)&&!HabitatRules.UseUtility(s,7,2,3,1,0),"box tools cannot relocate bamboo");
 s=Raft();Q(s,2,3).rotation=0;Q(s,1,3).prop=PropKind.Fan;Q(s,1,3).rotation=1;HabitatRules.Tick(s,t,2.6f);C(Q(s,2,3).rotation==1&&s.playerX==2&&Q(s,2,3).prop==PropKind.Raft,"wind redirects raft without automatic empty drift");
 C(HabitatRules.MovePlayer(s,1,0),"wind redirected rail is rideable");
 s=Raft();s.waterLevel=1;HabitatRules.Tick(s,t,.1f);C(Q(s,2,3).prop==PropKind.Raft&&HabitatRules.Height(s,2,3)==2,"rising tide lifts raft instead of converting it to cargo");
 s.waterLevel=0;HabitatRules.Tick(s,t,.1f);C(Q(s,2,3).prop==PropKind.Raft,"falling tide preserves bamboo");
 C(Math.Abs(HabitatRules.WaterSurfaceY(0)-.725f)<.0001&&Math.Abs(HabitatRules.WaterSurfaceY(1)-1.425f)<.0001,"visible water level matches first terrain layer and rises one layer");
 s=Board();C(!HabitatRules.IsWater(s,2,3),"height-one ground remains dry at normal tide");Q(s,3,3).height=0;C(HabitatRules.IsWater(s,3,3)&&!HabitatRules.MovePlayer(s,1,0),"flush sea remains impassable without support");
 foreach(int level in new[]{5,6,7,8,10}){s=HabitatLevels.Create(level);C(s.tiles.All(a=>a.prop!=PropKind.Float),"map "+level+" authored rafts are distinct from sea crates");}
 foreach(int level in new[]{4,9}){s=HabitatLevels.Create(level);
 C(s.width==(level==4?16:19)&&s.depth==(level==4?17:21),"optional "+level+" latest PDF grid dimensions");
 C(s.tiles.Count(q=>q.prop==PropKind.Crate)==(level==4?8:7),"optional "+level+" authored box count");
 C(s.tiles.Count(q=>q.prop==PropKind.Raft)==2&&s.tiles.Count(q=>q.prop==PropKind.Tide)==1,"optional "+level+" two bamboo and one tide mechanism");
 C(HabitatMechanicLevels.IsNewOptional(s)&&HabitatMechanicLevels.HasHabitatGoal(s)&&HabitatRules.UsesConfirmedPlan(s),"optional "+level+" uses current habitat rules and completion");
 C(!HabitatRules.IsWater(s,s.playerX,s.playerZ)&&HabitatLevels.CellLabel(s,s.playerX,s.playerZ).Contains("入口"),"optional "+level+" dry landing corresponds to marked entry");}
 s=HabitatLevels.Create(5);s.playerX=5;s.playerZ=6;C(HabitatRules.MovePlayer(s,1,0),"map1 boards authored raft from west");for(int i=0;i<4;i++)C(HabitatRules.MovePlayer(s,1,0),"map1 east passage step "+i);C(s.playerX==10&&HabitatRules.MovePlayer(s,1,0),"map1 reaches east shore");C(HabitatRules.MovePlayer(s,-1,0),"map1 reboards east end");for(int i=0;i<4;i++)C(HabitatRules.MovePlayer(s,-1,0),"map1 return passage step "+i);C(HabitatRules.MovePlayer(s,-1,0)&&s.playerX==5,"map1 returns to original west shore");
 s=HabitatLevels.Create(6);s.playerX=3;s.playerZ=8;C(HabitatRules.MovePlayer(s,1,0)&&HabitatRules.MovePlayer(s,0,-1)&&HabitatRules.MovePlayer(s,0,-1)&&HabitatRules.MovePlayer(s,0,-1),"map2 authored vertical raft reaches southern island");
 s=HabitatLevels.Create(5);s.chapterOneAccepted=true;s.chapterOneSailed=true;s.specialRod=s.rod=true;s.glassFish=2;s.openingCompleted=true;C(HabitatChapterRules.Deliver(s)&&s.coins==100&&s.glassFish==0,"first commission awards confirmed 100 coins");C(!HabitatChapterRules.Deliver(s)&&s.coins==100,"commission cannot pay twice");s.chapterOneMorningSeen=true;s.onHarbor=true;s.fish=1;C(HabitatRules.SellCatch(s,Species.Goldfish,1)&&s.coins==200&&HabitatRules.Shop(s,"fan"),"one additional real fish funds 200-coin bellows after revised reward");
 return "PASS "+Evidence.Count+" raft/fixed-stair/water/reward rules";
 }
}
