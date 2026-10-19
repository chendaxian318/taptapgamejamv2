using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Newtonsoft.Json;
using HabitatIsland;

public static class OptimizationRenderComparison
{
    const string Dir="Verification/optimization-20261018";
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static async Task Frames(int n){int end=Time.frameCount+n;while(Time.frameCount<end)await Task.Delay(8);}
    public static async Task<string> Run()
    {
        var g=HabitatGame.Instance;var type=typeof(HabitatGame);var original=type.GetMethod("CaptureSnapshot",F).Invoke(g,null);
        bool session=g.SessionStarted;var saveField=type.GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);var saveDir=saveField.GetValue(null);
        var originalPipeline=QualitySettings.renderPipeline;var clone=UnityEngine.Object.Instantiate((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline);
        var camera=g.View.GameCamera.GetComponent<UniversalAdditionalCameraData>();var aa=camera.antialiasing;
        var samples=new List<object>();
        saveField.SetValue(null,Path.GetFullPath(Dir+"/render-saves"));type.GetProperty("SessionStarted").SetValue(g,true);
        try
        {
            QualitySettings.renderPipeline=clone;g.LoadLevel(8);g.paused=true;
            g.State.seenGuideKeys=new List<string>{"move","pull","climb","rod","basket","fan","crab","jellyfish","tide","shovel","magnet","launcher"};
            foreach(var n in new[]{"chapterGreetingPending","chapterChestPending","chapterMorningGreetingPending"})type.GetField(n,F).SetValue(g,false);
            g.Hud.ClosePanels();g.View.FocusPlayer();
            foreach(bool optimized in new[]{false,true})
            {
                clone.renderScale=optimized?1f:1.25f;clone.msaaSampleCount=4;
                camera.antialiasing=optimized?AntialiasingMode.None:AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                await Frames(60);var times=new List<float>();var draws=new List<int>();
                for(int i=0;i<150;i++){await Frames(1);times.Add(Time.unscaledDeltaTime*1000);draws.Add(UnityStats.drawCalls);}
                times.Sort();samples.Add(new{mode=optimized?"100percent-MSAA4":"125percent-MSAA4-SMAA",meanMilliseconds=times.Average(),p95Milliseconds=times[142],meanDrawCalls=draws.Average(),scope="Capped Editor frames, not GPU benchmark"});
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+(optimized?"/render-optimized.png":"/render-baseline.png")));await Frames(5);
            }
            var textures=new HashSet<Texture2D>();
            foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))foreach(var m in r.sharedMaterials)
                if(m)foreach(string property in m.GetTexturePropertyNames())if(m.GetTexture(property) is Texture2D t)textures.Add(t);
            File.WriteAllText(Dir+"/active-textures-after.json",JsonConvert.SerializeObject(textures.Select(t=>new{t.name,path=AssetDatabase.GetAssetPath(t),t.width,t.height,format=t.format.ToString(),t.mipmapCount,t.isReadable}),Formatting.Indented));
            File.WriteAllText(Dir+"/render-comparison.json",JsonConvert.SerializeObject(samples,Formatting.Indented));
            return "PASS rendering comparison and active texture inventory captured";
        }
        finally
        {
            QualitySettings.renderPipeline=originalPipeline;camera.antialiasing=aa;UnityEngine.Object.Destroy(clone);
            g.CancelPendingAction();type.GetMethod("RestoreSnapshot",F).Invoke(g,new[]{original});type.GetProperty("SessionStarted").SetValue(g,session);saveField.SetValue(null,saveDir);
        }
    }
}
