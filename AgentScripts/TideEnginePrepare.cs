using System;
using System.IO;
using UnityEditor;
using UnityEngine;
public static class TideEnginePrepare
{
 public static string Run()
 {
  AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
  string folder="Assets/HabitatIsland/Art/OptimizedTextures/TideEngine";
  foreach(string path in Directory.GetFiles(folder))
  {
   var imp=AssetImporter.GetAtPath(path) as TextureImporter;if(!imp)continue;imp.textureType=TextureImporterType.Default;imp.sRGBTexture=Path.GetFileNameWithoutExtension(path)=="Image_0";imp.mipmapEnabled=true;imp.isReadable=false;imp.maxTextureSize=4096;imp.filterMode=FilterMode.Trilinear;imp.anisoLevel=4;imp.textureCompression=TextureImporterCompression.CompressedHQ;
   var settings=imp.GetPlatformTextureSettings("Standalone");settings.name="Standalone";settings.overridden=true;settings.maxTextureSize=4096;settings.format=TextureImporterFormat.BC7;settings.compressionQuality=100;imp.SetPlatformTextureSettings(settings);imp.SaveAndReimport();
  }
  return HabitatIsland.Editor.HabitatEquipmentSetup.PrepareTideEngine();
 }
}
