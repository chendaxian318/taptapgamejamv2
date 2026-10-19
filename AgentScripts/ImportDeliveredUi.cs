using System.Linq;
using UnityEditor;
using UnityEngine;
public static class ImportDeliveredUi
{
    public static string Run()
    {
        AssetDatabase.Refresh();
        foreach(string file in new[]{"OpeningDesign.png","MainMenuExport.png","HudExport.png","MapExport.png","SpecialPointer.png","MapBeach.jpg"})
        {
            string path="Assets/HabitatIsland/Resources/UI/Delivered/"+file;
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=file!="MapBeach.jpg";importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=file=="SpecialPointer.png"?1024:4096;
            importer.filterMode=FilterMode.Trilinear;importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        return string.Join("\n",new[]{"OpeningDesign","MainMenuExport","HudExport","MapExport","SpecialPointer","MapBeach"}.Select(n=>{var t=Resources.Load<Texture2D>("UI/Delivered/"+n);return n+" "+t.width+"x"+t.height;}));
    }
}
