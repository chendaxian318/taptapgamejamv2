using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;
public static class WaterMotionPreview
{
    static string SaveHashes()
    {
        string dir=Path.Combine(Application.persistentDataPath,"Playtests");
        if(!Directory.Exists(dir))return "absent";
        using(var hash=System.Security.Cryptography.SHA256.Create())
            return string.Join("\n",Directory.GetFiles(dir,"*",SearchOption.AllDirectories).OrderBy(p=>p).Select(p=>p+":"+Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(p)))));
    }
    static async Task Wait(float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)await Task.Delay(20);}
    public static string Begin(){Run();return "recording";}
    static async void Run()
    {
        string dir="Verification/water-motion-v2-20261018/";
        var g=HabitatGame.Instance;Material m=null;Shader current=null;string saveHashes=SaveHashes();
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var snapshot=typeof(HabitatGame).GetMethod("CaptureSnapshot",flags).Invoke(g,null);bool paused=g.paused;
        var saveField=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var oldSave=saveField.GetValue(null);saveField.SetValue(null,Path.GetFullPath(dir+"isolated-preview-saves"));
        try
        {
            g.Hud.ClosePanels();g.LoadLevel(8);g.paused=true;g.View.FocusOverview();await Wait(1);
            m=UnityEngine.Object.FindObjectsByType<Renderer>().Single(r=>r.sharedMaterial&&r.sharedMaterial.shader.name=="Habitat/QuietPaintedWater").sharedMaterial;
            current=m.shader;var baseline=Shader.Find("Habitat/WaterBeforeMotion");
            if(baseline)
            {
                m.shader=baseline;await Wait(.3f);
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(dir+"before-current.png"));await Wait(.3f);
            }
            m.shader=current;await Wait(.3f);
            for(int i=0;i<32;i++){ScreenCapture.CaptureScreenshot(Path.GetFullPath(dir+"screen-"+i.ToString("D2")+".png"));await Wait(.16f);}
            AppDomain.CurrentDomain.SetData("WaterMotionPreview","captured");
        }
        catch(Exception e){AppDomain.CurrentDomain.SetData("WaterMotionPreview",e.ToString());}
        finally
        {
            if(m&&current)m.shader=current;
            typeof(HabitatGame).GetMethod("RestoreSnapshot",flags).Invoke(g,new[]{snapshot});g.paused=paused;
            bool unchanged=saveHashes==SaveHashes();
            File.WriteAllText(dir+"preview-save-check.txt",unchanged?"PASS player save hashes unchanged":"FAIL player save hashes changed");
            if(!unchanged)AppDomain.CurrentDomain.SetData("WaterMotionPreview","FAIL player save hashes changed");
            saveField.SetValue(null,oldSave);
        }
    }
}
