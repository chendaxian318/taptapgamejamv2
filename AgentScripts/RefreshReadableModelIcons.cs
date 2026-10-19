using System;using System.IO;using System.Collections.Generic;using UnityEngine;using UnityEditor;
public static class RefreshReadableModelIcons
{
 public static string Run()
 {
  string root="Assets/HabitatIsland/Resources/UI/Delivered/Batch2/Icons";
  string[] names={"Shopkeeper","Goldfish","Crab","Shrimp","Rod","Shovel","Basket","Bellows","Trap","Magnet","Cannon","Boat","Jellyfish","Shell"};
  string[] paths={"Story/Shopkeeper","Goldfish/Goldfish","HermitCrab/HermitCrab","AnimatedShrimp/Shrimp","FishingRod/Rod","Shovel/Shovel","BackBasket/Basket","ShellBellows/Bellows","AnimatedTrap/Trap","Magnet/Magnet","Cannon/Cannon","Story/DepartureBoat","Jellyfish/Jellyfish","HermitCrab/ShedShell"};
  for(int i=0;i<names.Length;i++)
  {
   var prefab=Resources.Load<GameObject>("Models/"+paths[i]);if(!prefab)throw new Exception(paths[i]);
   var preview=new PreviewRenderUtility();var obj=UnityEngine.Object.Instantiate(prefab);var materials=new List<Material>();
   try
   {
    preview.AddSingleGO(obj);var skins=obj.GetComponentsInChildren<Renderer>();var bounds=skins[0].bounds;
    foreach(var renderer in skins)
    {
     bounds.Encapsulate(renderer.bounds);var source=renderer.sharedMaterials;var replacements=new Material[source.Length];
     for(int slot=0;slot<source.Length;slot++)
     {
      // Thumbnail only: preserve the delivered colour texture without the preview scene's missing PBR environment.
      var old=source[slot];var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
      mat.SetTexture("_BaseMap",old?old.mainTexture:null);mat.SetColor("_BaseColor",old&&old.HasProperty("baseColorFactor")?old.GetColor("baseColorFactor"):Color.white);
      mat.SetFloat("_Cull",0);replacements[slot]=mat;materials.Add(mat);
     }
     renderer.sharedMaterials=replacements;
    }
    var cam=preview.camera;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;cam.orthographic=true;
    float distance=bounds.extents.magnitude*4+1;
    cam.transform.position=bounds.center+new Vector3(.9f,.55f,1.5f).normalized*distance;cam.transform.LookAt(bounds.center);
    cam.nearClipPlane=.01f;cam.farClipPlane=distance+bounds.size.magnitude+20;cam.orthographicSize=Mathf.Max(bounds.extents.y,Mathf.Max(bounds.extents.x,bounds.extents.z))*1.15f;
    preview.BeginPreview(new Rect(0,0,512,512),GUIStyle.none);preview.Render(true);
    var rt=(RenderTexture)preview.EndPreview();var previous=RenderTexture.active;RenderTexture.active=rt;
    var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();RenderTexture.active=previous;
    string path=root+"/"+names[i]+".png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
    importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
   }
   finally{preview.Cleanup();foreach(var mat in materials)UnityEngine.Object.DestroyImmediate(mat);}
  }
  return "Rendered 14 source-model thumbnails with readable original colour maps, including jellyfish and shed shell.";
 }
}
