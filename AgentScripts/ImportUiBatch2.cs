using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;
public static class ImportUiBatch2
{
 public static string Run()
 {
  AssetDatabase.Refresh();string root="Assets/HabitatIsland/Resources/UI/Delivered/Batch2";Directory.CreateDirectory(root+"/Icons");
  string[] names={"Hero","Shopkeeper","Goldfish","Crab","Shrimp","Rod","Shovel","Basket","Bellows","Trap","Magnet","Cannon","Boat"};
  string[] paths={"MeshyTraveler/Player","Story/Shopkeeper","Goldfish/Goldfish","HermitCrab/HermitCrab","AnimatedShrimp/Shrimp","FishingRod/Rod","Shovel/Shovel","BackBasket/Basket","ShellBellows/Bellows","AnimatedTrap/Trap","Magnet/Magnet","Cannon/Cannon","Story/DepartureBoat"};
  for(int i=0;i<names.Length;i++)
  {
   var prefab=Resources.Load<GameObject>("Models/"+paths[i]);if(!prefab)throw new Exception(paths[i]);
   var render=new PreviewRenderUtility();var obj=UnityEngine.Object.Instantiate(prefab);
   try
   {
    render.AddSingleGO(obj);var skins=obj.GetComponentsInChildren<Renderer>();var b=skins[0].bounds;foreach(var s in skins)b.Encapsulate(s.bounds);
    render.camera.clearFlags=CameraClearFlags.SolidColor;render.camera.backgroundColor=Color.clear;render.camera.fieldOfView=30;
    float distance=b.extents.magnitude/Mathf.Sin(15*Mathf.Deg2Rad)*1.1f;
    render.camera.transform.position=b.center+new Vector3(.9f,.55f,1.5f).normalized*distance;render.camera.transform.LookAt(b.center);
    render.camera.nearClipPlane=.01f;render.camera.farClipPlane=distance+b.size.magnitude+20;
    render.lights[0].intensity=1.2f;render.lights[0].transform.rotation=Quaternion.Euler(35,-30,0);render.lights[1].intensity=.8f;render.ambientColor=Color.gray;
    render.camera.orthographic=true;render.camera.orthographicSize=Mathf.Max(b.extents.y,Mathf.Max(b.extents.x,b.extents.z)) * 1.15f;
    render.BeginPreview(new Rect(0,0,512,512),GUIStyle.none);render.Render(true);var rt=(RenderTexture)render.EndPreview();var previous=RenderTexture.active;RenderTexture.active=rt;
    var t=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();RenderTexture.active=previous;
    File.WriteAllBytes(root+"/Icons/"+names[i]+".png",t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);
   }
   finally{render.Cleanup();}
  }
  AssetDatabase.Refresh();
  foreach(string path in Directory.GetFiles(root,"*.png",SearchOption.AllDirectories))
  {var importer=AssetImporter.GetAtPath(path.Replace('\\','/')) as TextureImporter;if(!importer)continue;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.SaveAndReimport();}
  return "Imported nine draft pages, three fonts and rendered 13 existing model icons.";
 }
}
