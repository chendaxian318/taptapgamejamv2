using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace HabitatIsland.EditorTools
{
    public static class HabitatTextureOptimization
    {
        const string MapPath="Assets/HabitatIsland/Editor/TextureOptimizationMap.json";
        [Serializable] public class Entry { public string source,name,target,sourceSha256;public int index;public bool linear; }
        [Serializable] class Mapping { public Entry[] entries; }
        static Entry[] Entries => File.Exists(MapPath)?JsonUtility.FromJson<Mapping>(File.ReadAllText(MapPath)).entries:new Entry[0];

        public static bool Apply(Material material)
        {
            if(!material)return false;bool changed=false;var entries=Entries;
            foreach(string property in material.GetTexturePropertyNames())
            {
                var texture=material.GetTexture(property);if(!texture)continue;
                string source=AssetDatabase.GetAssetPath(texture);
                var entry=entries.FirstOrDefault(e=>e.source==source && e.name==texture.name);
                if(entry==null)continue;
                using(var stream=File.OpenRead(source))
                using(var sha=SHA256.Create())
                {
                    var hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
                    if(hash!=entry.sourceSha256)
                    { Debug.LogWarning("Model changed; regenerate optimized textures before rebinding: "+source);continue; }
                }
                var replacement=AssetDatabase.LoadAssetAtPath<Texture2D>(entry.target);
                if(!replacement)throw new InvalidOperationException("Missing optimized texture: "+entry.target);
                material.SetTexture(property,replacement);changed=true;
            }
            if(changed)EditorUtility.SetDirty(material);
            return changed;
        }

        [MenuItem("Habitat/Optimization/Apply model texture compression")]
        public static string ApplyAll()
        {
            int textures=0,materials=0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach(var e in Entries)
                {
                    var importer=AssetImporter.GetAtPath(e.target) as TextureImporter;
                    if(!importer)throw new InvalidOperationException("Texture not imported: "+e.target);
                    importer.textureType=TextureImporterType.Default; // Preserve glTF channel packing, including normals.
                    importer.sRGBTexture=!e.linear;importer.isReadable=false;importer.mipmapEnabled=true;
                    importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=false;
                    importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.maxTextureSize=4096;
                    importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;
                    var platform=importer.GetPlatformTextureSettings("Standalone");
                    platform.name="Standalone";platform.overridden=true;platform.maxTextureSize=4096;
                    platform.format=TextureImporterFormat.BC7;platform.compressionQuality=100;
                    importer.SetPlatformTextureSettings(platform);importer.SaveAndReimport();textures++;
                }
            }
            finally{AssetDatabase.StopAssetEditing();}
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/HabitatIsland"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                if(!path.EndsWith(".mat",StringComparison.OrdinalIgnoreCase))continue;
                if(Apply(AssetDatabase.LoadAssetAtPath<Material>(path)))materials++;
            }
            AssetDatabase.SaveAssets();
            string result="Configured "+textures+" extracted textures; rebound "+materials+" standalone materials; original GLB/UV/animations retained.";
            File.WriteAllText("Verification/optimization-20261018/texture-application.txt",result);return result;
        }
    }
}
