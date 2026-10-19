using System.IO;
using UnityEngine;
using UnityEditor;
using HabitatIsland;
public static class TideGif {
 public static string Run(){
 var root=new GameObject("Temporary Tide GIF");root.hideFlags=HideFlags.HideAndDontSave;root.transform.position=new Vector3(200,200,200);
 var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HabitatIsland/Resources/Models/TideEngine/TideEngine.prefab");
 var model=Object.Instantiate(prefab,root.transform);var visual=model.GetComponent<HabitatTideEngineVisual>();
 var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();var baked=new Mesh[skins.Length];for(int k=0;k<skins.Length;k++){var ob=new GameObject("Baked frame");ob.transform.SetParent(skins[k].transform,false);baked[k]=new Mesh();ob.AddComponent<MeshFilter>().sharedMesh=baked[k];ob.AddComponent<MeshRenderer>().sharedMaterials=skins[k].sharedMaterials;skins[k].enabled=false;} var camObj=new GameObject("GIF Camera");camObj.transform.SetParent(root.transform,false);var cam=camObj.AddComponent<Camera>();cam.enabled=false;cam.orthographic=true;cam.orthographicSize=.64f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.065f,.09f,.12f);cam.nearClipPlane=.01f;cam.farClipPlane=10;
 cam.transform.position=root.transform.position+new Vector3(.95f,.9f,-1.4f);cam.transform.LookAt(root.transform.position+Vector3.up*.045f);
 var rt=new RenderTexture(800,700,24);cam.targetTexture=rt;var old=RenderTexture.active;var tex=new Texture2D(800,700,TextureFormat.RGB24,false);string dir="Verification/tide-engine-20261019/gif-frames";Directory.CreateDirectory(dir);
 try{visual.ResetVisual(false);for(int i=0;i<72;i++){float t=i/25f;visual.Render(t>=.6f&&t<1.8f,i==0?0:1f/25f);for(int k=0;k<skins.Length;k++)skins[k].BakeMesh(baked[k],true);cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,800,700),0,0);tex.Apply();File.WriteAllBytes(dir+"/"+i.ToString("D3")+".png",tex.EncodeToPNG());}return "Rendered 72 frames at 25fps";}
 finally{RenderTexture.active=old;cam.targetTexture=null;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(root);foreach(var mesh in baked)Object.DestroyImmediate(mesh);}
 }
}


