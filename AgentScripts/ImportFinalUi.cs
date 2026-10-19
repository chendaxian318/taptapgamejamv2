using System;using System.IO;using UnityEditor;using UnityEngine;
public static class ImportFinalUi
{
 public static string Run()
 {
  AssetDatabase.Refresh();int count=0;
  foreach(var file in Directory.GetFiles("Assets/HabitatIsland/Resources/UI/Final20261018","*.png"))
  {
   var path=file.Replace('\\','/');var t=(TextureImporter)AssetImporter.GetAtPath(path);
   t.textureType=TextureImporterType.Default;t.mipmapEnabled=false;t.alphaIsTransparency=true;t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;t.npotScale=TextureImporterNPOTScale.None;t.isReadable=false;
   t.maxTextureSize=Path.GetFileName(path).StartsWith("Background")?4096:2048;
   string key=Path.GetFileNameWithoutExtension(path);int id;
   if(key.StartsWith("R")&&int.TryParse(key.Substring(1),out id)&&((id>=80&&id<=83)||(id>=90&&id<=103&&id!=97)||(id>=123&&id<=144&&id!=126&&id!=129&&id!=130&&id!=131&&id!=132)))t.maxTextureSize=512;
   if(key.StartsWith("Hover")||key=="R110"||key=="R117")t.maxTextureSize=1024;
   t.textureCompression=TextureImporterCompression.CompressedHQ;
   var platform=t.GetPlatformTextureSettings("Standalone");platform.overridden=true;platform.maxTextureSize=t.maxTextureSize;platform.format=TextureImporterFormat.BC7;platform.compressionQuality=100;t.SetPlatformTextureSettings(platform);t.SaveAndReimport();count++;
  }
  AssetDatabase.SaveAssets();return "Imported "+count+" parts with transparent alpha / BC7 / no mipmaps";
 }
}
