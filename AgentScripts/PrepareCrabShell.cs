using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using HabitatIsland;

public static class PrepareCrabShell
{
    public static string Run()
    {
        const string folder="Assets/HabitatIsland/Resources/Models/HermitCrab/";
        var prefab=Resources.Load<GameObject>("Models/HermitCrab/HermitCrab");
        var instance=UnityEngine.Object.Instantiate(prefab);
        try
        {
            var renderer=instance.GetComponentInChildren<SkinnedMeshRenderer>();
            var source=renderer.sharedMesh;
            int shell=Array.FindIndex(renderer.bones,b=>b.name=="Shell");
            if(shell<0)throw new Exception("Missing supplied shell bone");
            var weights=source.boneWeights;
            Func<int,bool> isShell=i=>{var w=weights[i];return (w.boneIndex0==shell?w.weight0:0)+(w.boneIndex1==shell?w.weight1:0)+(w.boneIndex2==shell?w.weight2:0)+(w.boneIndex3==shell?w.weight3:0)>.5f;};
            var body=UnityEngine.Object.Instantiate(source);body.name="HermitCrab without shed shell";
            var shellIndices=new List<int>();
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                var tris=source.GetTriangles(sub);var keep=new List<int>();
                for(int i=0;i<tris.Length;i+=3)
                {
                    var destination=isShell(tris[i])&&isShell(tris[i+1])&&isShell(tris[i+2])?shellIndices:keep;
                    destination.Add(tris[i]);destination.Add(tris[i+1]);destination.Add(tris[i+2]);
                }
                body.SetTriangles(keep,sub);
            }
            if(shellIndices.Count<300)throw new Exception("Too few shell faces to split safely");
            var baked=new Mesh();renderer.BakeMesh(baked);
            var used=shellIndices.Distinct().ToArray();var map=new Dictionary<int,int>();
            var vertices=new Vector3[used.Length];var uv=new Vector2[used.Length];
            var bakedV=baked.vertices;var sourceUV=source.uv;
            for(int i=0;i<used.Length;i++){map[used[i]]=i;vertices[i]=instance.transform.InverseTransformPoint(renderer.transform.TransformPoint(bakedV[used[i]]));uv[i]=sourceUV[used[i]];}
            var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);
            var center=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            for(int i=0;i<vertices.Length;i++)vertices[i]=(vertices[i]-center)*(.48f/Mathf.Max(bounds.size.x,bounds.size.z));
            var mesh=new Mesh {name="Shed shell from supplied crab",vertices=vertices,uv=uv,triangles=shellIndices.Select(i=>map[i]).ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();
            SaveMesh(body,folder+"BodyWithoutShell.asset");SaveMesh(mesh,folder+"ShedShellMesh.asset");
            var go=new GameObject("Shed Hermit Crab Shell",typeof(MeshFilter),typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"ShedShellMesh.asset");
            go.GetComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
            PrefabUtility.SaveAsPrefabAsset(go,folder+"ShedShell.prefab");
            UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(baked);
            AssetDatabase.SaveAssets();return "Shell faces="+shellIndices.Count/3+", body faces="+body.triangles.Length/3;
        }
        finally{UnityEngine.Object.DestroyImmediate(instance);}
    }
    static void SaveMesh(Mesh mesh,string path)
    {var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);}else AssetDatabase.CreateAsset(mesh,path);}
}
