using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HabitatIsland.Editor
{
    public static class HabitatLightingSetup
    {
        const string Folder="Assets/HabitatIsland/Resources/Environment";
        [MenuItem("Tools/Habitat Island/Art/Prepare Island Daylight")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Stop Play before preparing lighting.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var settings=AssetDatabase.LoadAssetAtPath<HabitatLightingProfile>(Folder+"/IslandDaylight.asset");
            if(!settings){settings=ScriptableObject.CreateInstance<HabitatLightingProfile>();AssetDatabase.CreateAsset(settings,Folder+"/IslandDaylight.asset");}
            var sky=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/IslandSky.mat");
            if(!sky){sky=new Material(Shader.Find("Habitat/IslandSky"));AssetDatabase.CreateAsset(sky,Folder+"/IslandSky.mat");}
            var volume=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder+"/IslandDaylightVolume.asset");
            if(!volume)
            {
                volume=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(volume,Folder+"/IslandDaylightVolume.asset");
                var tone=volume.Add<Tonemapping>(true);tone.mode.value=TonemappingMode.Neutral;
                var bloom=volume.Add<Bloom>(true);bloom.threshold.value=1.25f;bloom.intensity.value=.10f;bloom.scatter.value=.55f;
                var color=volume.Add<ColorAdjustments>(true);color.postExposure.value=.10f;color.contrast.value=6f;color.saturation.value=-5f;
                var vignette=volume.Add<Vignette>(true);vignette.intensity.value=.055f;vignette.smoothness.value=.75f;
                foreach(var component in volume.components)AssetDatabase.AddObjectToAsset(component,volume);
            }
            settings.skybox=sky;settings.atmosphere=volume;EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("Island daylight assets ready. Existing scenes, UI and render-pipeline quality settings retained.");
        }
    }
}
