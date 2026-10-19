using System;
using System.Collections.Generic;
using System.Linq;
using HabitatIsland;
public static class CurrentLogicBoundaryChecks
{
 public static readonly List<string> Evidence=new List<string>();
 static readonly Tuning tuning=new Tuning();
 static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);Evidence.Add("PASS: "+label);}
 static HabitatState Board()=>new HabitatState{width=9,depth=7,tiles=Enumerable.Range(0,63).Select(_=>new Tile{height=1}).ToArray(),coins=0,levelIndex=10,authoredLevel=true,playerX=2,playerZ=3,dockX=2,dockZ=3,facingX=1,facingZ=0,rod=true,specialRod=true,backBasketUnlocked=true,magnets=1,launchers=1,shovels=1};
 static Tile Q(HabitatState s,int x,int z)=>HabitatRules.TileAt(s,x,z);
 static string InventoryAndBoard(HabitatState s)=>string.Join(",",s.magnets,s.launchers,s.shovels,s.boatCrates,s.carryingProp,s.carryingAnimal,s.playerX,s.playerZ,s.waterLevel)+"/"+string.Join(";",s.tiles.Select(t=>t.height+":"+t.prop+":"+t.coveredProp));
 static void Reject(HabitatState s,int tool,int x,int z,int dx,int dz,string label){string before=InventoryAndBoard(s);Check(!HabitatRules.UseUtility(s,tool,x,z,dx,dz)&&before==InventoryAndBoard(s),label+" leaves inventory and board unchanged");}
 static Animal A(HabitatState s,Species kind,int x,int z){var a=new Animal{id=s.animals.Count+1,species=kind,x=x,z=z};s.animals.Add(a);return a;}
 static HabitatState Habitat(){var s=Board();Q(s,4,3).height=0;for(int x=6;x<9;x++)Q(s,x,3).height=0;A(s,Species.Goldfish,4,3);A(s,Species.Jellyfish,6,3);A(s,Species.Crab,3,3);A(s,Species.Shrimp,2,2);return s;}
 public static string Run()
 {
  Evidence.Clear();var s=Board();Check(!HabitatMechanicLevels.CompleteHabitat(s,tuning)&&!s.won,"empty map cannot win");
  s=Habitat();Check(HabitatMechanicLevels.CompleteHabitat(s,tuning)&&s.won,"all four resident types suitable together win");Check(!HabitatMechanicLevels.CompleteHabitat(s,tuning)&&s.coins==0,"repeated completion cannot pay or settle again");
  s=Habitat();Q(s,5,3).height=0;Check(!HabitatMechanicLevels.CompleteHabitat(s,tuning)&&!s.won,"open fish pond prevents victory despite suitable crab and jellyfish");
  s=Habitat();Q(s,7,3).height=1;Check(!HabitatMechanicLevels.CompleteHabitat(s,tuning)&&!s.won,"enclosed jellyfish prevents victory despite suitable fish");
  s=Habitat();s.animals[2].x=1;s.animals[2].z=1;Check(!HabitatMechanicLevels.CompleteHabitat(s,tuning)&&!s.won,"inland crab prevents otherwise suitable residents winning");
  s=Habitat();A(s,Species.Goldfish,8,3);Check(!HabitatMechanicLevels.CompleteHabitat(s,tuning),"one unsuitable member prevents a whole species from counting complete");
  s=Board();Q(s,3,3).prop=PropKind.Crate;Reject(s,6,3,3,1,1,"diagonal magnet direction");Reject(s,6,3,3,0,0,"zero magnet direction");Reject(s,6,-1,3,1,0,"out-of-map target");
  Q(s,3,3).prop=PropKind.None;Q(s,5,3).prop=PropKind.Crate;Reject(s,6,5,3,1,0,"magnet target beyond 3x3");
  s=Board();Q(s,3,3).prop=PropKind.Crate;A(s,Species.Shrimp,3,3);Reject(s,6,3,3,1,0,"occupied source box");
  s=Board();Q(s,3,3).prop=PropKind.Crate;Q(s,4,3).height=2;Reject(s,6,3,3,1,0,"uphill destination");
  s=Board();Q(s,3,3).prop=PropKind.Crate;A(s,Species.Shrimp,4,3);Reject(s,6,3,3,1,0,"occupied destination");
  s=Board();Q(s,3,3).prop=PropKind.Crate;s.carryingProp=PropKind.Fan;Reject(s,6,3,3,1,0,"nonempty hands");
  s=Board();Q(s,3,3).prop=PropKind.Crate;s.magnets=0;Reject(s,6,3,3,1,0,"zero magnet stock");
  s=Board();Q(s,3,3).prop=PropKind.Crate;Q(s,4,3).height=0;Check(HabitatRules.UseUtility(s,6,3,3,1,0)&&s.magnets==0&&Q(s,3,3).prop==PropKind.None&&Q(s,4,3).prop==PropKind.Float,"magnet spends one and converts landed water crate to float");
  s=Board();Q(s,3,3).prop=PropKind.Crate;Q(s,4,3).prop=PropKind.Tide;Check(HabitatRules.UseUtility(s,6,3,3,1,0)&&s.tideActive&&s.waterLevel==1&&Q(s,4,3).coveredProp==PropKind.Tide,"magnet preserves and activates underlying tide mechanism");
  s=Board();Q(s,3,3).prop=PropKind.Crate;Q(s,5,3).prop=PropKind.Obstacle;Check(HabitatRules.UseUtility(s,7,3,3,1,0)&&Q(s,4,3).prop==PropKind.Crate&&Q(s,5,3).prop==PropKind.Obstacle&&s.launchers==0,"launcher stops at legal first cell before blocked second cell");
  s=Board();Q(s,2,4).prop=PropKind.Crate;Reject(s,7,2,4,0,1,"launcher side target instead of facing target");
  s=Board();Q(s,3,3).height=2;Q(s,3,3).prop=PropKind.Chest;Reject(s,5,3,3,1,0,"shovel cannot destroy a treasure chest");
  s=Board();Q(s,3,3).height=3;Reject(s,5,3,3,1,0,"shovel cannot remove two-height step");
  s=Board();s.boatCrates=3;Check(HabitatRules.TransferBoatCrate(s,true)&&s.boatCrates==2&&s.carryingProp==PropKind.Crate,"unload takes exactly one of three ship boxes");
  string state=InventoryAndBoard(s);Check(!HabitatRules.TransferBoatCrate(s,true)&&state==InventoryAndBoard(s),"repeat unload with full hands does not duplicate box");
  Check(HabitatRules.TransferBoatCrate(s,false)&&s.boatCrates==3&&s.carryingProp==PropKind.None,"loading restores same box to ship");s.carryingProp=PropKind.Crate;state=InventoryAndBoard(s);Check(!HabitatRules.TransferBoatCrate(s,false)&&state==InventoryAndBoard(s),"full ship preserves held fourth crate on rejected load");
  s=Board();s.boatCrates=1;s.playerX=7;state=InventoryAndBoard(s);Check(!HabitatRules.TransferBoatCrate(s,true)&&state==InventoryAndBoard(s),"remote ship unloading is rejected");
  s=Board();s.cargo=3;state=InventoryAndBoard(s);Check(!HabitatRules.TransferBoatCrate(s,true)&&state==InventoryAndBoard(s),"three soil units cannot substitute for three ship crates");
  return "PASS "+Evidence.Count+" current victory/tool/cargo boundary checks";
 }
}
