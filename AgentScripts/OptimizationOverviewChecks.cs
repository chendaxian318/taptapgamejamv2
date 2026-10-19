using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Text;
using UnityEngine;
using HabitatIsland;
public static class OptimizationOverviewChecks
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    public static async Task<string> Run()
    {
        var g=HabitatGame.Instance;
        var original=typeof(HabitatGame).GetMethod("CaptureSnapshot",F).Invoke(g,null);
        bool session=g.SessionStarted;var log=new StringBuilder();
        try
        {
            typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,false);
            foreach(int level in new[]{5,6,7,8,10})
            {
                g.LoadLevel(level);g.Hud.ClosePanels();g.paused=true;g.View.FocusOverview();
                float deadline=Time.realtimeSinceStartup+1.5f;
                while(Time.realtimeSinceStartup<deadline)await Task.Delay(8);
                var v=g.View.GameCamera.WorldToViewportPoint(g.View.PlayerVisual.transform.position);
                if(v.z<=0||v.x<0||v.x>1||v.y<0||v.y>1)throw new Exception("Player outside overview on "+level);
                log.AppendLine("PASS level "+level+" overview player in viewport; size "+g.View.GameCamera.orthographicSize);
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/optimization-20261018/overview-"+level+".png"));
                int frame=Time.frameCount+3;while(Time.frameCount<frame)await Task.Delay(8);
            }
            return "PASS five overview cameras; actual editor window aspect only";
        }
        finally
        {
            typeof(HabitatGame).GetMethod("RestoreSnapshot",F).Invoke(g,new[]{original});
            typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);
            File.WriteAllText("Verification/optimization-20261018/overview-runtime.txt",log.ToString());
        }
    }
}
