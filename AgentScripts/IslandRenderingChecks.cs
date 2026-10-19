using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
public static class IslandRenderingChecks
{
    static int count;static StringBuilder report;
    static void Check(bool value,string text){if(!value)throw new Exception("FAIL "+text);report.AppendLine("PASS "+(++count)+" "+text);}
    static async Task Frames(int n=8){for(int i=0;i<n;i++)await Task.Yield();}
    static async Task Capture(string name){ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/render-20261018/"+name+".png"));await Frames(12);}
    public static string Begin()
    {
        if(!Application.isPlaying||!HabitatGame.Instance)throw new Exception("Start a fresh Play session first.");
        AppDomain.CurrentDomain.SetData("IslandRenderingChecks","running");Run();return "started";
    }
    static async void Run()
    {
        var game=HabitatGame.Instance;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var capture=typeof(HabitatGame).GetMethod("CaptureSnapshot",flags);var restore=typeof(HabitatGame).GetMethod("RestoreSnapshot",flags);
        var snapshot=capture.Invoke(game,null);bool paused=game.paused;
        var story=game.GetComponent<HabitatMainStory>();
        string result="";report=new StringBuilder("Rendering checks, not a full campaign or GPU performance benchmark.\n");count=0;
        try
        {
            var profile=Resources.Load<HabitatLightingProfile>("Environment/IslandDaylight");Check(profile&&profile.skybox&&profile.atmosphere,"serialized lighting, sky and volume load");
            foreach(string name in new[]{"Habitat/QuietPaintedWater","Habitat/IslandSky"})
            {
                var shader=Shader.Find(name);Check(shader&&shader.isSupported,"supported shader "+name);
                Check(ShaderUtil.GetShaderMessages(shader).Length==0,"shader diagnostics empty "+name);
            }
            var sun=RenderSettings.sun;Check(sun&&Mathf.Abs(sun.intensity-profile.sunIntensity)<.001,"daylight main sun applied");
            Check(RenderSettings.ambientMode==AmbientMode.Trilight,"three-color ambient light applied");
            Check(game.View.GameCamera.clearFlags==CameraClearFlags.Skybox&&RenderSettings.skybox,"skybox active");
            Check(game.View.GameCamera.GetUniversalAdditionalCameraData().requiresDepthTexture,"camera supplies shore depth");
            var pipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            Check(pipeline.msaaSampleCount==4&&Mathf.Abs(pipeline.renderScale-1)<.001,"existing 100 percent resolution and 4x MSAA retained");
            game.Hud.ClosePanels();
            foreach(int level in new[]{5,8,10})
            {
                game.LoadLevel(level);game.paused=true;await Frames(12);
                Check(UnityEngine.Object.FindObjectsByType<HabitatLighting>().Length==1,"no duplicate lighting after level "+level);
                Check(UnityEngine.Object.FindObjectsByType<Volume>().Count(v=>v.name=="Island daylight grading")==1,"no duplicate grading after level "+level);
                var sea=GameObject.Find("Hona hand-painted ocean").GetComponent<Renderer>();
                Check(sea.sharedMaterial.shader.name=="Habitat/QuietPaintedWater"&&sea.sharedMaterial.renderQueue==2950,"depth-aware ocean render order on level "+level);
            }
            game.LoadLevel(8);game.paused=true;game.View.FocusOverview();await Frames(40);await Capture("after");
            var seaTransform=GameObject.Find("Hona hand-painted ocean").transform;
            float oldHeight=seaTransform.position.y;int oldWater=game.State.waterLevel;
            game.State.waterLevel=oldWater+1;await Frames();Check(Mathf.Abs(seaTransform.position.y-oldHeight-.7f)<.001,"ocean follows one-level rising tide");await Capture("high-tide");
            game.State.waterLevel=oldWater;await Frames();Check(Mathf.Abs(seaTransform.position.y-oldHeight)<.001,"ocean follows falling tide");
            Check(story.BeginOpening(),"opening accepts daylight environment");story.TogglePause();await Frames();
            Check(game.View.GameCamera.clearFlags==CameraClearFlags.Skybox,"opening blue-sky shot uses actual sky shader");await Capture("opening-sky");
            story.Cancel();await Frames();Check(Mathf.Abs(sun.intensity-profile.sunIntensity)<.001&&game.View.GameCamera.clearFlags==CameraClearFlags.Skybox,"opening cancel restores gameplay lighting and sky camera");
            Check(story.BeginMorning(),"morning timeline starts with new daylight");story.TogglePause();await Frames();
            Check(sun.intensity<profile.sunIntensity*.3f,"morning timeline can dim main light");
            float dimmed=sun.intensity;await Frames();Check(Mathf.Abs(sun.intensity-dimmed)<.001,"lighting component does not overwrite paused story sun");await Capture("morning");
            story.Cancel();await Frames();Check(Mathf.Abs(sun.intensity-profile.sunIntensity)<.001,"morning cancel restores daylight");
            foreach(string name in new[]{"Habitat/QuietPaintedWater","Habitat/IslandSky"})Check(ShaderUtil.GetShaderMessages(Shader.Find(name)).Length==0,"rendered shader variants clean "+name);
            result="PASS "+count+" rendering checks";
        }
        catch(Exception e){result=e.ToString();report.AppendLine(result);}
        finally
        {
            if(story.IsPlaying)story.Cancel();game.CancelPendingAction();restore.Invoke(game,new[]{snapshot});game.paused=paused;
            File.WriteAllText("Verification/render-20261018/checks.txt",report.ToString());AppDomain.CurrentDomain.SetData("IslandRenderingChecks",result);
        }
    }
}
