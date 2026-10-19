using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using HabitatIsland.EditorTools;

public static class PackageSizeAssets
{
 const string Dir="Verification/package-size-20261018";
 [Serializable] class Mapping { public HabitatTextureOptimization.Entry[] entries; }
 static HabitatTextureOptimization.Entry[] Entries => JsonUtility.FromJson<Mapping>(File.ReadAllText(Dir+"/new-textures.json")).entries;
 static readonly string[] Ui={"MainMenuExport","OpeningDesign","MapExport","HudExport","InteractionExport"};
 static void Write(string name,object value)=>File.WriteAllText(Dir+"/"+name+".json",JsonConvert.SerializeObject(value,Formatting.Indented));
 static void Preview(Texture2D t,string name)
 {
  int w=Math.Min(1024,t.width),h=Math.Max(1,Mathf.RoundToInt((float)t.height*w/t.width));
  var rt=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
  var prior=RenderTexture.active;bool srgb=GL.sRGBWrite;Texture2D copy=null;
  try { GL.sRGBWrite=false;Graphics.Blit(t,rt);RenderTexture.active=rt;copy=new Texture2D(w,h,TextureFormat.RGBA32,false,true);copy.ReadPixels(new Rect(0,0,w,h),0,0);copy.Apply();Directory.CreateDirectory(Dir+"/textures");File.WriteAllBytes(Dir+"/textures/"+name+".png",copy.EncodeToPNG()); }
  finally { RenderTexture.active=prior;GL.sRGBWrite=srgb;if(copy)UnityEngine.Object.DestroyImmediate(copy);RenderTexture.ReleaseTemporary(rt); }
 }
 public static string Baseline()
 {
  if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
  var report=UnityEditor.Build.Reporting.BuildReport.GetLatestReport();
  Write("build-assets-before",report.packedAssets.SelectMany(p=>p.contents.Select(c=>new{pack=p.shortPath,c.sourceAssetPath,c.packedSize})));
  int i=0;var rows=new List<object>();
  foreach(var e in Entries){var t=AssetDatabase.LoadAllAssetsAtPath(e.source).OfType<Texture2D>().Single(x=>x.name==e.name);Preview(t,"model-"+i+"-before");rows.Add(new{index=i++,e.source,e.name,t.width,t.height,format=t.format.ToString(),t.mipmapCount});}
  foreach(var n in Ui){var p="Assets/HabitatIsland/Resources/UI/Delivered/"+n+".png";var t=AssetDatabase.LoadAssetAtPath<Texture2D>(p);Preview(t,n+"-before");rows.Add(new{path=p,t.width,t.height,format=t.format.ToString(),t.mipmapCount});}
  Write("texture-baseline",rows);
  return "Captured original texture previews and exact previous build asset report";
 }
 public static string Apply()
 {
  if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
  AssetDatabase.Refresh();var entries=Entries;
  // Original GLB image bytes are extracted separately; preserve color/data encoding.
  foreach(var e in entries){var imp=AssetImporter.GetAtPath(e.target) as TextureImporter;if(!imp)throw new Exception(e.target);
   imp.textureType=TextureImporterType.Default;imp.sRGBTexture=!e.linear;imp.isReadable=false;imp.mipmapEnabled=true;
   imp.alphaSource=TextureImporterAlphaSource.FromInput;imp.alphaIsTransparency=false;imp.filterMode=FilterMode.Trilinear;imp.anisoLevel=4;
   imp.textureCompression=TextureImporterCompression.CompressedHQ;imp.maxTextureSize=4096;
   var s=imp.GetPlatformTextureSettings("Standalone");s.name="Standalone";s.overridden=true;s.maxTextureSize=4096;s.format=TextureImporterFormat.BC7;s.compressionQuality=100;
   imp.SetPlatformTextureSettings(s);imp.SaveAndReimport();
  }
  var changed=new List<string>();
  foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/HabitatIsland"})){
   string path=AssetDatabase.GUIDToAssetPath(guid);if(!path.EndsWith(".mat"))continue;
   var m=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(!m.GetTexturePropertyNames().Any(n=>m.GetTexture(n)&&entries.Any(e=>e.source==AssetDatabase.GetAssetPath(m.GetTexture(n)))))continue;
   if(HabitatTextureOptimization.Apply(m))changed.Add(path);
  }
  foreach(var n in Ui){string p="Assets/HabitatIsland/Resources/UI/Delivered/"+n+".png";var imp=(TextureImporter)AssetImporter.GetAtPath(p);
   var t=AssetDatabase.LoadAssetAtPath<Texture2D>(p);int size=Mathf.NextPowerOfTwo(Math.Max(t.width,t.height));
   // Screen-space sheets have fixed UV slices. NPOT mip chains forced an RGBA32 fallback.
   imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.CompressedHQ;
   var s=imp.GetPlatformTextureSettings("Standalone");s.name="Standalone";s.overridden=true;s.maxTextureSize=size;s.format=TextureImporterFormat.BC7;s.textureCompression=TextureImporterCompression.CompressedHQ;s.compressionQuality=100;
   imp.SetPlatformTextureSettings(s);imp.SaveAndReimport();
  }
  const string from="Assets/HabitatIsland/Resources/Models/MeshyShrimp",to="Assets/HabitatIsland/Art/Legacy/MeshyShrimp";
  if(AssetDatabase.IsValidFolder(from)){
   var root=Path.GetFullPath("Assets/HabitatIsland")+Path.DirectorySeparatorChar;
   if(!Path.GetFullPath(from).StartsWith(root,StringComparison.OrdinalIgnoreCase)||!Path.GetFullPath(to).StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new Exception("Archive outside workspace");
   if(!AssetDatabase.IsValidFolder("Assets/HabitatIsland/Art/Legacy"))AssetDatabase.CreateFolder("Assets/HabitatIsland/Art","Legacy");
   var error=AssetDatabase.MoveAsset(from,to);if(!string.IsNullOrEmpty(error))throw new Exception(error);
  }
  AssetDatabase.SaveAssets();
  if(File.Exists(Dir+"/changed-materials.json"))changed=changed.Union(JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Dir+"/changed-materials.json"))).ToList();
  Write("changed-materials",changed);
  return "Compressed "+entries.Length+" extracted model textures and 5 UI sheets; rebound "+changed.Count+" materials; archived old static shrimp with original GUIDs.";
 }
 public static string Validate()
 {
  var entries=Entries;var checks=new List<string>();int i=0;
  foreach(var e in entries){var a=AssetDatabase.LoadAllAssetsAtPath(e.source).OfType<Texture2D>().Single(t=>t.name==e.name);var b=AssetDatabase.LoadAssetAtPath<Texture2D>(e.target);
   if(!b||b.width!=a.width||b.height!=a.height||b.mipmapCount!=a.mipmapCount||b.format!=TextureFormat.BC7)throw new Exception("Texture format/size mismatch: "+e.target);
   var imp=(TextureImporter)AssetImporter.GetAtPath(e.target);if(imp.sRGBTexture==e.linear)throw new Exception("Color space mismatch");
   Preview(b,"model-"+i+++"-after");checks.Add(e.target+" preserved dimensions/mips/color space; BC7");
  }
  foreach(var n in Ui){var t=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/HabitatIsland/Resources/UI/Delivered/"+n+".png");if(t.format!=TextureFormat.BC7)throw new Exception(n);Preview(t,n+"-after");checks.Add(n+" BC7; source and UI layout unchanged");}
  foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/HabitatIsland/Resources"})){
   var p=AssetDatabase.GUIDToAssetPath(guid);if(!p.EndsWith(".mat"))continue;var m=AssetDatabase.LoadAssetAtPath<Material>(p);
   foreach(var n in m.GetTexturePropertyNames())if(m.GetTexture(n)&&entries.Any(e=>e.source==AssetDatabase.GetAssetPath(m.GetTexture(n))))throw new Exception("Original embedded texture still referenced: "+p+" "+n);
  }
  if(AssetDatabase.IsValidFolder("Assets/HabitatIsland/Resources/Models/MeshyShrimp"))throw new Exception("Legacy resource still ships");
  if(!AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HabitatIsland/Resources/Models/AnimatedShrimp/Shrimp.prefab"))throw new Exception("Missing animated shrimp");
  Write("asset-checks",checks);return "PASS "+checks.Count+" texture checks, bindings and legacy archive";
 }
}
