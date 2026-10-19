using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HabitatIsland.Editor
{
    public static class HabitatSetup
    {
        public const string ScenePath = "Assets/HabitatIsland/Scenes/HabitatIsland.unity";
        [MenuItem("Tools/Habitat Island/1. Create Playtest Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode before generating the scene.");
            EditorSettings.enterPlayModeOptionsEnabled = false;
            Directory.CreateDirectory("Assets/HabitatIsland/Scenes");
            Directory.CreateDirectory("Assets/HabitatIsland/Resources/Materials");
            Directory.CreateDirectory("Assets/HabitatIsland/Resources/Fonts");
            Directory.CreateDirectory("Assets/HabitatIsland/Settings");
            if(!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset")) TMP_PackageResourceImporter.ImportResources(true,false,false);
            AssetDatabase.Refresh();
            ConfigureArt();
            var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/HabitatIsland/Resources/Fonts/NotoSansCJKsc-Regular.otf");
            string fontPath="Assets/HabitatIsland/Resources/Fonts/HabitatChinese.asset";
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if(font==null)
            {
                font=TMP_FontAsset.CreateFontAsset(source,64,5,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
                font.name="Habitat Chinese"; font.isMultiAtlasTexturesEnabled=true;
                AssetDatabase.CreateAsset(font,fontPath);
                if(font.material!=null)AssetDatabase.AddObjectToAsset(font.material,font);
                foreach(var texture in font.atlasTextures) if(texture!=null)AssetDatabase.AddObjectToAsset(texture,font);
            }
            font.getFontFeatures=false;
            EditorUtility.SetDirty(font);
            foreach(string shaderName in new[]{"Universal Render Pipeline/Lit","Universal Render Pipeline/Unlit","Universal Render Pipeline/2D/Sprite-Unlit-Default","Habitat/WhiteKeySprite"})
            {
                Shader shader=Shader.Find(shaderName);if(shader==null)throw new Exception("Missing shader: "+shaderName);
                string path="Assets/HabitatIsland/Resources/Materials/"+shaderName.Replace('/','_')+".mat";
                if(AssetDatabase.LoadAssetAtPath<Material>(path)==null)AssetDatabase.CreateAsset(new Material(shader),path);
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var game=new GameObject("Habitat Island • Game",typeof(HabitatGame));
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener),typeof(UniversalAdditionalCameraData));
            camera.tag="MainCamera";var cam=camera.GetComponent<Camera>();cam.orthographic=true;cam.orthographicSize=6;
            camera.transform.position=new Vector3(-8,11,-8);camera.transform.LookAt(new Vector3(4,0,4));
            var cameraData=camera.GetComponent<UniversalAdditionalCameraData>();cameraData.renderPostProcessing=true;cameraData.volumeLayerMask=1;
            var light=new GameObject("Sun",typeof(Light));light.transform.rotation=Quaternion.Euler(48,-35,0);
            var sun=light.GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.4f;sun.shadows=LightShadows.Soft;
            string profilePath="Assets/HabitatIsland/Settings/IslandAtmosphere.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if(profile==null)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,profilePath);
                var tone=profile.Add<Tonemapping>(true);tone.mode.value=TonemappingMode.Neutral;AssetDatabase.AddObjectToAsset(tone,profile);
                var bloom=profile.Add<Bloom>(true);bloom.threshold.value=1.1f;bloom.intensity.value=.15f;AssetDatabase.AddObjectToAsset(bloom,profile);
                var vignette=profile.Add<Vignette>(true);vignette.intensity.value=.13f;vignette.smoothness.value=.7f;AssetDatabase.AddObjectToAsset(vignette,profile);
            }
            var volume=new GameObject("Island atmosphere",typeof(Volume)).GetComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
            int quality=QualitySettings.names.Length-1;QualitySettings.SetQualityLevel(quality,true);
            if(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp){urp.supportsHDR=true;EditorUtility.SetDirty(urp);}
            PlayerSettings.companyName="HabitatLab";PlayerSettings.productName="栖屿 · 玩法实验室";
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
            PlayerSettings.resizableWindow=true;PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            ConfigureVisualClarity();
            AssetDatabase.SaveAssets();Selection.activeGameObject=game;
            Debug.Log("[HabitatIsland] Playtest scene and art resources created.");
        }
        [MenuItem("Tools/Habitat Island/5. Configure Visual Clarity")]
        public static void ConfigureVisualClarity()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode before configuring visual quality.");
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (pipeline != null)
            {
                pipeline.renderScale = 1f;
                pipeline.msaaSampleCount = 4;
                QualitySettings.renderPipeline = pipeline;
                EditorUtility.SetDirty(pipeline);
            }
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.allowMSAA = true;
                camera.allowDynamicResolution = false;
                var data = camera.GetComponent<UniversalAdditionalCameraData>();
                if (data != null)
                {
                    data.antialiasing = AntialiasingMode.None;
                    data.antialiasingQuality = AntialiasingQuality.High;
                }
                EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
                EditorSceneManager.SaveScene(camera.gameObject.scene);
            }
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.allowFullscreenSwitch = true;
            AssetDatabase.SaveAssets();
            Debug.Log("[HabitatIsland] Visual quality: native scene sampling, 4x MSAA, native-size overlay UI.");
        }
        [MenuItem("Tools/Habitat Island/4. Import Artist Resources")]
        public static void ConfigureArt()
        {
            foreach(string sourcePath in Directory.GetFiles("Assets/HabitatIsland/Resources/Art", "*", SearchOption.AllDirectories))
            {
                string path=sourcePath.Replace('\\','/');
                if(!(AssetImporter.GetAtPath(path) is TextureImporter ti))continue;
                ti.textureType=TextureImporterType.Default;ti.alphaIsTransparency=true;
                ti.alphaSource=TextureImporterAlphaSource.FromInput;ti.sRGBTexture=true;
                ti.mipmapEnabled=false;ti.filterMode=FilterMode.Bilinear;ti.wrapMode=TextureWrapMode.Clamp;
                ti.textureCompression=TextureImporterCompression.Uncompressed;ti.maxTextureSize=4096;
                ti.npotScale=TextureImporterNPOTScale.None;
                ti.SaveAndReimport();
            }
        }
        [MenuItem("Tools/Habitat Island/2. Run Rule Checks")]
        public static void Checks()
        {
            string result=HabitatModelChecks.Run();Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/rule-checks.txt",DateTime.Now.ToString("s")+"\n"+result);
            Debug.Log("[HabitatIsland] "+result);
        }
        [MenuItem("Tools/Habitat Island/3. Build Windows Playtest")]
        public static void BuildWindows()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play mode before building.");
            Directory.CreateDirectory("Builds/Windows");Directory.CreateDirectory("Verification");
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/Windows/HabitatIsland.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            string report=result.summary.result+"\nErrors: "+result.summary.totalErrors+"\nWarnings: "+result.summary.totalWarnings+"\nBytes: "+result.summary.totalSize;
            File.WriteAllText("Verification/build-report.txt",report);
            if(result.summary.result!=BuildResult.Succeeded)throw new Exception(report);
            Debug.Log("[HabitatIsland] Windows build ready. "+report);
        }
    }
}
