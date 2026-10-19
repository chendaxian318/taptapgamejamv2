using System;
using System.Linq;
using System.Collections.Generic;
using HabitatIsland;
public static class TideRegressionChecks
{
 public static readonly List<string> Evidence=new List<string>();
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Evidence.Add("PASS "+text);}
 static HabitatState Board()=>new HabitatState{width=7,depth=7,tiles=Enumerable.Range(0,49).Select(_=>new Tile{height=0}).ToArray(),playerX=2,playerZ=3,carryingProp=PropKind.Crate};
 static Tile Q(HabitatState s,int x,int z)=>HabitatRules.TileAt(s,x,z);
 public static string Run()
 {
  Evidence.Clear();var s=Board();s.waterLevel=1;s.tideActive=true;Q(s,2,3).prop=PropKind.Float;
  Check(HabitatRules.Height(s,2,3)==2,"raised floating support stands at height two");
  Check(HabitatRules.Interact(s,3,3)&&Q(s,3,3).prop==PropKind.Float&&s.carryingProp==PropKind.None,"one-level drop to raised water places box despite deeper seabed");
  s=Board();Q(s,2,3).height=2;Check(!HabitatRules.Interact(s,3,3)&&s.carryingProp==PropKind.Crate,"normal sea still rejects two-level drop");
  s=Board();Q(s,2,3).height=1;Q(s,3,3).height=2;Check(HabitatRules.Interact(s,3,3)&&Q(s,3,3).prop==PropKind.Crate,"ordinary adjacent dry placement retained");
  s=Board();Q(s,2,3).height=2;Q(s,3,3).height=2;Q(s,3,3).prop=PropKind.Tide;Q(s,5,3).height=1;Q(s,5,3).prop=PropKind.Crate;Q(s,5,4).prop=PropKind.Float;
  Check(HabitatRules.Interact(s,3,3)&&s.tideActive&&s.waterLevel==1,"weighted tide plate raises actual sea");
  Check(Q(s,5,3).prop==PropKind.Float&&Q(s,5,4).prop==PropKind.Float,"flooded land crate converts and original water float remains");
  Check(HabitatRules.Interact(s,3,3)&&!s.tideActive&&s.waterLevel==0,"lifting plate weight lowers sea");
  Check(Q(s,5,3).prop==PropKind.Crate&&Q(s,5,4).prop==PropKind.Float,"dry former crate restored; permanent water float retained");
  Check(Q(s,3,3).prop==PropKind.Tide&&s.carryingProp==PropKind.Crate,"tide plate and held weight conserved");
  Check(HabitatRules.Interact(s,3,3)&&HabitatRules.Interact(s,3,3)&&Q(s,5,3).prop==PropKind.Crate,"second tide cycle remains reversible");
  return "PASS "+Evidence.Count+" tide regression checks";
 }
}
