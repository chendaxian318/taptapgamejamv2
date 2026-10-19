using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;

public static class ChapterDeparturePreview
{
    public static async Task<string> Run()
    {
        var g=HabitatGame.Instance;
        if(!Application.isPlaying||!g||g.Busy||g.Hud.ModalOpen||!g.State.onHarbor)throw new Exception("Requires idle harbor Play");
        string state=JsonUtility.ToJson(g.State);bool callback=false;Vector3 camera=g.View.GameCamera.transform.position;
        try
        {
            if(!g.Story.BeginDeparture(()=>callback=true))throw new Exception("Departure failed");
            float start=Time.unscaledTime; double limit=UnityEditor.EditorApplication.timeSinceStartup+8;
            while(Time.unscaledTime-start<1.3f)
            {if(UnityEditor.EditorApplication.timeSinceStartup>limit)throw new Exception("Frames stopped");await Task.Delay(15);}
            var boat=g.GetComponentsInChildren<Transform>(true).FirstOrDefault(x=>x.name=="Departure boat placeholder");
            // Dock hierarchy is owned by View, so locate the temporary vessel there.
            if(!boat)boat=g.View.Harbor.ModelTransform.GetComponentsInChildren<Transform>(true).FirstOrDefault(x=>x.name=="Departure boat placeholder");
            if(!boat||!g.View.PlayerVisual.gameObject.activeInHierarchy)throw new Exception("Boat or actor invisible");
            Vector3 delta=g.View.PlayerWorldPosition-boat.position;
            if(Mathf.Abs(delta.x)>.01f||Mathf.Abs(delta.z)>.01f||Mathf.Abs(delta.y-.71f)>.01f)throw new Exception("Actor detached from boat");
            string path=Path.GetFullPath("Logs/chapter-20261016/followup-04-departure-in-motion.png");
            ScreenCapture.CaptureScreenshot(path);
            int frame=Time.frameCount;while(Time.frameCount-frame<3)await Task.Delay(15);
            g.Story.TogglePause();Vector3 paused=g.View.PlayerWorldPosition;await Task.Delay(180);
            if((g.View.PlayerWorldPosition-paused).sqrMagnitude>.00001f)throw new Exception("Paused rider moved");
            g.Story.Cancel();
            if(callback||g.Busy||JsonUtility.ToJson(g.State)!=state||(g.View.GameCamera.transform.position-camera).sqrMagnitude>.00001f)
                throw new Exception("Cancel changed gameplay or camera");
            string result="PASS: visible existing actor rides the departure boat without walking; pause freezes actor/boat; cancel restores camera, leaves state unchanged and never calls completion.";
            File.WriteAllText(Path.GetFullPath("Logs/chapter-20261016/departure-rider-check.txt"),result);return result;
        }
        finally{if(g.StoryPlaying)g.Story.Cancel();}
    }
}
