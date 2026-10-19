using System;using System.IO;using System.Linq;using System.Reflection;using HabitatIsland;using UnityEngine;
public static class PlanSaveCompatibilityChecks
{
 public static string Run()
 {
  var g=UnityEngine.Object.FindAnyObjectByType<HabitatGame>();var f=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);var old=f.GetValue(null);var prior=g.State.message;var export=g.LastExport;
  var session=g.SessionStarted;string path=Path.GetFullPath("Verification/plan-ui-20261018/legacy-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);int n=0;
  void Check(bool ok,string why){if(!ok)throw new Exception(why);n++;}
  try
  {
   f.SetValue(null,path);typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);
   Check(g.SaveSession(1),"fixture slot save");File.Copy(Path.Combine(path,"slot-1.json"),Path.Combine(path,"latest.json"));byte[] legacy=File.ReadAllBytes(Path.Combine(path,"latest.json"));
   Check(!g.GetSaveSlotInfo(1).legacy,"new slot takes priority over older legacy file");
   Check(g.DeleteSession(1),"delete explicit new slot");
   Check(File.Exists(Path.Combine(path,"latest.json"))&&legacy.SequenceEqual(File.ReadAllBytes(Path.Combine(path,"latest.json"))),"deleting new slot preserves original legacy bytes");
   Check(!g.GetSaveSlotInfo(1).exists,"delete marker prevents unintended old-save resurrection");
   Check(g.SaveSession(1)&&g.GetSaveSlotInfo(1).readable,"saving again replaces deleted slot even with legacy marker");
   File.Delete(Path.Combine(path,"slot-1.json"));File.Delete(Path.Combine(path,"slot-1.deleted"));
   Check(g.GetSaveSlotInfo(1).legacy&&g.GetSaveSlotInfo(1).readable,"untouched old latest save is offered as slot one");
   Check(g.DeleteSession(1)&&!File.Exists(Path.Combine(path,"latest.json")),"explicit legacy slot deletion deletes only represented legacy save");
   string result="PASS "+n+" isolated legacy slot compatibility checks.";File.WriteAllText("Verification/plan-ui-20261018/legacy-checks.txt",result);return result;
  }
  finally{f.SetValue(null,old);g.State.message=prior;g.LastExport=export;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);}
 }
}
