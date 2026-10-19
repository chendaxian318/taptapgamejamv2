using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using HabitatIsland;

public static class ExportShopCrateIcon
{
 public static string Run()
 {
  const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var preview=new PreviewRenderUtility();
  var root=new GameObject("Shop crate export");
  preview.AddSingleGO(root);
  var view=root.AddComponent<HabitatView>();view.enabled=false;
  var mats=new List<Material>();Texture2D texture=null;
  try
  {
   // Use the game's own material factory and exact existing crate palette.
   var mat=typeof(HabitatView).GetMethod("Mat",flags);
   mats.Add((Material)mat.Invoke(view,new object[]{"wood",new Color(.53f,.31f,.18f),false}));
   mats.Add((Material)mat.Invoke(view,new object[]{"woodLight",new Color(.82f,.56f,.3f),false}));
   var prop=(Transform)typeof(HabitatView).GetMethod("BuildProp",flags).Invoke(view,new object[]{root.transform,PropKind.Crate,0f,0});
   var renderers=prop.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
   foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
   preview.ambientColor=new Color(.55f,.55f,.55f,1);
   preview.lights[0].intensity=1.1f;preview.lights[0].transform.rotation=Quaternion.Euler(45,-35,0);
   preview.lights[1].intensity=.4f;preview.lights[1].transform.rotation=Quaternion.Euler(25,145,0);
   var cam=preview.camera;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;
   cam.orthographic=true;cam.nearClipPlane=.01f;cam.farClipPlane=20;
   cam.transform.position=bounds.center+new Vector3(1,.85f,-1.4f).normalized*4;cam.transform.LookAt(bounds.center);
   cam.orthographicSize=.64f;cam.allowHDR=false;
   preview.BeginPreview(new Rect(0,0,1024,1024),GUIStyle.none);preview.Render(true);
   var rt=(RenderTexture)preview.EndPreview();var previous=RenderTexture.active;
   try{RenderTexture.active=rt;texture=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();}
   finally{RenderTexture.active=previous;}
   // PreviewRenderUtility supplies a linear UNorm target; PNG viewers expect sRGB.
   if(QualitySettings.activeColorSpace==ColorSpace.Linear&&!rt.sRGB)
   {
    var pixels=texture.GetPixels();for(int i=0;i<pixels.Length;i++)pixels[i]=pixels[i].gamma;
    texture.SetPixels(pixels);texture.Apply();
   }
   int transparent=0,solid=0;foreach(var pixel in texture.GetPixels32()){if(pixel.a==0)transparent++;if(pixel.a==255)solid++;}
   if(transparent==0||solid==0)throw new Exception("Expected transparent background and opaque crate pixels");
   string path=Path.GetFullPath("Exports/ShopCrate/商店木箱_透明底_"+texture.width+".png");Directory.CreateDirectory(Path.GetDirectoryName(path));
   File.WriteAllBytes(path,texture.EncodeToPNG());
   return path+" | "+renderers.Length+" original mesh parts | transparent="+transparent+" opaque="+solid+" | sRGB="+rt.sRGB+" format="+rt.graphicsFormat;
  }
  finally{if(texture)UnityEngine.Object.DestroyImmediate(texture);preview.Cleanup();foreach(var m in mats)UnityEngine.Object.DestroyImmediate(m);}
 }
}
