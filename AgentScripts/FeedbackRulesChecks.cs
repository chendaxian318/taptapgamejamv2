using System;using System.Linq;using System.Collections.Generic;using HabitatIsland;
public static class FeedbackRulesChecks {
 public static readonly List<string> Evidence=new List<string>();
 static void Check(bool b,string m){Evidence.Add((b?"PASS ":"FAIL ")+m);if(!b)throw new Exception(m);}
 static HabitatState Board()=>new HabitatState{levelIndex=1,width=7,depth=7,tiles=Enumerable.Range(0,49).Select(_=>new Tile{height=1}).ToArray(),playerX=2,playerZ=3,dockX=2,dockZ=3,facingX=1,facingZ=0,carryingAnimal=-1};
 static Tile Q(HabitatState s,int x,int z)=>HabitatRules.TileAt(s,x,z);
 static int Boxes(HabitatState s)=>s.tiles.Sum(t=>(HabitatRules.IsBox(t.prop)?1:0)+(HabitatRules.IsBox(t.coveredProp)?1:0))+s.boatCrates;
 public static string Run(){Evidence.Clear();var s=Board();Q(s,3,3).prop=PropKind.Crate;Q(s,4,3).height=Q(s,5,3).height=0;Q(s,4,3).prop=PropKind.Float;
 Check(HabitatRules.Interact(s,3,3)&&s.carryingProp==PropKind.None,"E pushes a single box and never picks it up");Check(Boxes(s)==2&&Q(s,4,3).coveredProp==PropKind.Float,"push onto support conserves both boxes");
 s.playerX=3;Check(HabitatRules.MovePlayer(s,1,0,false)&&s.playerX==4&&!HabitatRules.IsWater(s,4,3)&&Boxes(s)==2,"push from floating support extends route with no box duplication");
 var clone=HabitatRules.Clone(s);Q(clone,4,3).prop=PropKind.None;Check(Q(s,4,3).prop==PropKind.Float,"clone tiles remain independent");
 s=Board();Q(s,2,3).height=Q(s,3,3).height=0;Q(s,2,3).prop=PropKind.Float;Q(s,3,3).prop=PropKind.Crate;Q(s,3,3).coveredProp=PropKind.Float;Check(HabitatRules.PullPlayer(s,-1,0)&&Q(s,2,3).coveredProp==PropKind.Float&&Q(s,3,3).prop==PropKind.Float&&Boxes(s)==3,"pull from float onto another float preserves both supports");
 s=Board();Q(s,3,3).prop=Q(s,4,3).prop=PropKind.Crate;Check(!HabitatRules.PushBox(s,3,3)&&Boxes(s)==2&&s.carryingProp==PropKind.None,"two-box chain rejected without mutation");
 Q(s,4,3).prop=PropKind.None;Q(s,4,3).trap=true;Check(!HabitatRules.PushBox(s,3,3),"push cannot cover a trap");
 s=Board();s.magnets=1;Q(s,3,3).height=Q(s,4,3).height=0;Q(s,3,3).prop=PropKind.Crate;Q(s,3,3).coveredProp=PropKind.Float;Q(s,4,3).prop=PropKind.Float;
 Check(HabitatRules.UseUtility(s,6,3,3,1,0)&&Q(s,3,3).prop==PropKind.Float&&Q(s,4,3).coveredProp==PropKind.Float&&Boxes(s)==3&&s.magnets==0,"magnet moves only top crate and preserves supports");
 s=Board();s.launchers=1;Q(s,3,3).prop=PropKind.Crate;Q(s,4,3).height=Q(s,5,3).height=0;Q(s,4,3).prop=PropKind.Float;
 Check(HabitatRules.UseUtility(s,7,3,3,1,0)&&Q(s,4,3).prop==PropKind.Float&&Q(s,5,3).prop==PropKind.Float&&Boxes(s)==2,"launcher crosses float support and preserves box count");
 s=Board();s.boatCrates=1;Q(s,3,3).height=0;Q(s,3,3).prop=PropKind.Float;
 Check(HabitatRules.TransferBoatCrate(s,true)&&Q(s,3,3).coveredProp==PropKind.Float&&Boxes(s)==2&&s.carryingProp==PropKind.None,"unload directly onto floating support");
 Check(HabitatRules.TransferBoatCrate(s,false)&&Q(s,3,3).prop==PropKind.Float&&s.boatCrates==1&&Boxes(s)==2,"load top crate without consuming supporting float");
 Q(s,3,3).trap=true;Check(!HabitatRules.TransferBoatCrate(s,false)&&Boxes(s)==2,"occupied trap support cannot be loaded");
 s=Board();s.coins=2000;Check(HabitatRules.Shop(s,"crate")&&s.boatCrates==1&&s.carryingProp==PropKind.None,"shop box goes to cargo even on older islands");s.bagProps.Add(PropKind.Crate);Check(!HabitatRules.TakeBagProp(s,0)&&s.bagProps.Count==1,"legacy backpack box is retained but cannot bypass no-carry rule");
 return "PASS "+Evidence.Count+" floating-route/cargo/utility rules";
 }
}
