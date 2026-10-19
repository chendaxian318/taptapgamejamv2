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
public static class WaterSoftFlowChecks
{
    const string Dir="Verification/water-soft-flow-20261018";
    static string SaveHashes()
    {
        string dir=Path.Combine(Application.persistentDataPath,"Playtests");
        if(!Directory.Exists(dir))return "absent";
        using(var hash=System.Security.Cryptography.SHA256.Create())
            return string.Join("\n",Directory.GetFiles(dir,"*",SearchOption.AllDirectories).OrderBy(p=>p).Select(p=>p+":"+Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(p)))));
    }
    static StringBuilder report;static int count;
    static void Check(bool value,string text){if(!value)throw new Exception("FAIL "+text);report.AppendLine("PASS "+(++count)+" "+text);}
    static async Task Wait(float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)await Task.Delay(15);}
    static Color32[] Capture(Camera camera,RenderTexture rt,Texture2D image,string name)
    {
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        var old=RenderTexture.active;RenderTexture.active=rt;
        image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();RenderTexture.active=old;
        File.WriteAllBytes(Dir+"/"+name+".png",image.EncodeToPNG());return image.GetPixels32();
    }
    static double Difference(Color32[] a,Color32[] b)=>a.Zip(b,(x,y)=>(Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b))/3d).Average();
    public static string Begin(){if(!Application.isPlaying||!HabitatGame.Instance)throw new Exception("Fresh Play required");AppDomain.CurrentDomain.SetData("WaterSoftFlowChecks","running");Run();return "started";}
    static async void Run()
    {
        var game=HabitatGame.Instance;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var capture=typeof(HabitatGame).GetMethod("CaptureSnapshot",flags);var restore=typeof(HabitatGame).GetMethod("RestoreSnapshot",flags);
        var original=capture.Invoke(game,null);bool paused=game.paused;var cam=game.View.GameCamera;int mask=cam.cullingMask;string saveHashes=SaveHashes();
        var saveField=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);var oldSave=saveField.GetValue(null);
        saveField.SetValue(null,Path.GetFullPath(Dir+"/isolated-saves"));
        Renderer sea=null;int layer=0;Material material=null;float speed=0;string result="";
        var rt=new RenderTexture(960,540,24);rt.Create();var image=new Texture2D(960,540,TextureFormat.RGB24,false);
        report=new StringBuilder();count=0;
        try
        {
            var shader=Shader.Find("Habitat/QuietPaintedWater");Check(shader&&shader.isSupported&&ShaderUtil.GetShaderMessages(shader).Length==0,"water shader compiles cleanly");
            game.Hud.ClosePanels();game.LoadLevel(8);game.paused=true;await Wait(.7f);
            sea=UnityEngine.Object.FindObjectsByType<Renderer>().Single(r=>r.sharedMaterial&&r.sharedMaterial.shader==shader);
            material=sea.sharedMaterial;layer=sea.gameObject.layer;speed=material.GetFloat("_MotionSpeed");
            var profile=Resources.Load<HabitatLightingProfile>("Environment/IslandDaylight");
            Check(speed==profile.waterMotionSpeed&&speed>0,"configured animation speed applied");
            Check(material.GetColor("_BaseColor")==profile.deepWater&&material.GetColor("_ShallowColor")==profile.shallowWater,"existing water palette retained");
            Check(material.GetTexture("_BaseMap")==Resources.Load<Texture2D>("Art/Artist/Water"),"original painted texture retained");
            var mesh=sea.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;float y=sea.transform.position.y;
            Check(game.View.BeginCinematic(),"isolate water for pixel measurement");
            game.View.SetCinematicCamera(new Vector3(25,y,24),Quaternion.Euler(48,-14,0),3.5f);
            sea.gameObject.layer=31;cam.cullingMask=1<<31;await Wait(.2f);
            var a=Capture(cam,rt,image,"moving-a");await Wait(1.2f);var b=Capture(cam,rt,image,"moving-b");
            double moving=Difference(a,b);report.AppendLine("Moving pixel mean delta (0..255): "+moving);
            Check(moving>.15,"original paint moves over time (visual quality checked separately)");
            material.SetFloat("_MotionSpeed",0);await Wait(.2f);a=Capture(cam,rt,image,"still-a");await Wait(.5f);b=Capture(cam,rt,image,"still-b");
            double still=Difference(a,b);report.AppendLine("Stopped pixel mean delta (0..255): "+still);
            Check(still<.01,"zero speed stops shader movement");material.SetFloat("_MotionSpeed",speed);
            Check(vertices.SequenceEqual(mesh.vertices)&&sea.transform.position.y==y,"animation never displaces water geometry or tide height");
            sea.gameObject.layer=layer;cam.cullingMask=mask;game.View.EndCinematic();game.View.FocusOverview();await Wait(.8f);
            int originalWater=game.State.waterLevel;game.State.waterLevel=originalWater+1;await Wait(.2f);
            Check(Mathf.Abs(sea.transform.position.y-y-.7f)<.001f,"animated surface follows rising tide");
            game.State.waterLevel=originalWater;await Wait(.2f);Check(Mathf.Abs(sea.transform.position.y-y)<.001f,"falling tide restores surface height");
            for(int i=0;i<32;i++){Capture(cam,rt,image,"frame-"+i.ToString("D2"));await Wait(.125f);}
            Check(ShaderUtil.GetShaderMessages(shader).Length==0,"rendered shader variants clean");
            result="PASS "+count+" water motion checks";
        }
        catch(Exception e){result=e.ToString();report.AppendLine(result);}
        finally
        {
            if(sea)sea.gameObject.layer=layer;if(material)material.SetFloat("_MotionSpeed",speed);cam.cullingMask=mask;
            game.View.EndCinematic();game.CancelPendingAction();restore.Invoke(game,new[]{original});game.paused=paused;
            bool savesUnchanged=saveHashes==SaveHashes();report.AppendLine(savesUnchanged?"PASS player save file hashes unchanged":"FAIL player save hashes changed");
            if(!savesUnchanged)result="FAIL player save hashes changed";
            saveField.SetValue(null,oldSave);
            rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(image);
            File.WriteAllText(Dir+"/checks.txt",report.ToString());AppDomain.CurrentDomain.SetData("WaterSoftFlowChecks",result);
        }
    }
}

