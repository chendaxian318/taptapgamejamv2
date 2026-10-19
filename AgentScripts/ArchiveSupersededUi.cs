using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text.RegularExpressions;using UnityEditor;using UnityEngine;
public static class ArchiveSupersededUi
{
 public static string Run()
 {
  if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
  var used=new HashSet<string>();foreach(var file in Directory.GetFiles("Assets/HabitatIsland/Scripts","*.cs"))foreach(Match m in Regex.Matches(File.ReadAllText(file),"\"((?:R|Hover|Clear|Background)[0-9]+)\""))used.Add(m.Groups[1].Value);
  foreach(int n in new[]{5,6,7,8,9,80,81,82,83,36,37,38,39,40,41,43,47,48,49,50,51})used.Add("R"+n);
  foreach(int n in new[]{13,14,15,16,17,80,81,82,83})used.Add("Hover"+n);
  foreach(int n in new[]{37,38,39,40,41,43})used.Add("Clear"+n);
  string archive="Assets/HabitatIsland/Art/UIArchive20261018";Directory.CreateDirectory(archive);AssetDatabase.Refresh();var moved=new List<string>();
  var files=Directory.GetFiles("Assets/HabitatIsland/Resources/UI/Final20261018","*.png").Where(p=>!used.Contains(Path.GetFileNameWithoutExtension(p))).ToList();
  foreach(string name in new[]{"HudExport.png","InteractionExport.png","MainMenuDesign.png","MainMenuExport.png","MapBeach.jpg","MapExport.png","OpeningDesign.png"})files.Add("Assets/HabitatIsland/Resources/UI/Delivered/"+name);
  files.AddRange(Directory.GetFiles("Assets/HabitatIsland/Resources/UI/Delivered/Batch2","*.png").Where(p=>int.TryParse(Path.GetFileNameWithoutExtension(p),out _)));
  foreach(var file in files){string path=file.Replace('\\','/');if(!File.Exists(path))continue;string dst=archive+"/"+(path.Contains("Final20261018")?"UnusedFinal_":path.Contains("Batch2")?"DraftPage_":"Superseded_")+Path.GetFileName(path);string error=AssetDatabase.MoveAsset(path,dst);if(!string.IsNullOrEmpty(error))throw new Exception(error);moved.Add(path+" -> "+dst);}
  File.WriteAllLines("Verification/ui-final-20261018/archived-assets.txt",moved);AssetDatabase.SaveAssets();return "Archived (GUID retained, outside Resources): "+moved.Count;
 }
}
