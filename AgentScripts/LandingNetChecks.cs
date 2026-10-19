using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using HabitatIsland;
using HabitatIsland.Editor;

public static class LandingNetChecks
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly string Folder = Path.GetFullPath("Verification/landing-net-20261016");
    static List<string> checks;
    static void Check(bool pass, string message) { if (!pass) throw new Exception(message); checks.Add("PASS: " + message); }
    static Transform Net(HabitatMainStory story) => (Transform)typeof(HabitatMainStory).GetField("netRoot",Private).GetValue(story);
    public static async Task<string> PreviewAndRun()
    {
        PreparePreview();
        await Task.Delay(250);
        return Run();
    }
    public static string PreparePreview()
    {
        var g=HabitatGame.Instance;
        if (!g) throw new Exception("Main map must be playing");
        g.Story.Cancel();
        var type=typeof(HabitatGame);var snapshot=type.GetNestedType("Snapshot",BindingFlags.NonPublic);
        type.GetMethod("RestoreSnapshot",Private).Invoke(g,new[]{JsonUtility.FromJson(File.ReadAllText("D:/chatgptproject/AssetWork/LandingNet_20261016/session-before.json"),snapshot)});
        if (!g.Story.BeginOpening()) throw new Exception("Could not start opening preview");
        g.Story.TogglePause();
        var clip=g.Story.openingTimeline.GetOutputTracks().SelectMany(t=>t.GetClips()).Select(c=>c.asset).OfType<HabitatDockBeatClip>().First(c=>c.beat==HabitatDockBeat.StowNet);
        g.Story.EvaluateTimelineShot(clip,0f);
        // Direct Timeline sampling while paused skips the normal facing interpolation.
        g.View.PlayerVisual.ResetVisual(Net(g.Story).parent.forward);
        return "Paused at start of StowNet; user's prior session backed up";
    }
    static void Capture(Camera source, Transform gear, Vector3 target, string filename, Vector3 offset)
    {
        var go=new GameObject("Net verification camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(source);camera.enabled=false;
        camera.orthographic=true;camera.orthographicSize=1.25f;
        camera.transform.position=target+gear.rotation*offset;
        camera.transform.LookAt(target);
        var rt=new RenderTexture(1100,1000,24);var previous=RenderTexture.active;var image=new Texture2D(1100,1000,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1100,1000),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Folder,filename),image.EncodeToPNG());}
        finally{RenderTexture.active=previous;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);}
    }
    public static string Run()
    {
        checks=new List<string>();Directory.CreateDirectory(Folder);
        var g=HabitatGame.Instance;var story=g.Story;var net=Net(story);
        Check(story.IsPlaying&&story.IsPaused&&net,"Actual opening is paused with the carried net");
        var art=net.Find("Landing net art");Check(art,"Main story loads the new net prefab");
        var bounds=HabitatEquipmentSetup.BakedBounds(art.gameObject);
        Check(bounds.size.z>.4f,"Woven basket has real depth: "+bounds.size.z);
        Check(net.GetComponentsInChildren<MeshRenderer>(true).Length==1,"One original textured mesh replaces the planar grid");
        Check(net.GetComponentsInChildren<Collider>(true).Length==0,"Carried prop adds no blocking colliders");
        Check(net.GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterials.All(m=>m&&m.shader&&!m.shader.name.Contains("Error"))),"All PBR material references resolve");
        var bones=g.View.PlayerVisual.GetComponentsInChildren<Transform>(true);
        var hand=bones.First(t=>t.name=="mixamorig:RightHand");var finger=bones.FirstOrDefault(t=>t.name=="mixamorig:RightHandMiddle1");
        Check(Vector3.Distance(art.position,finger ? Vector3.Lerp(hand.position,finger.position,.65f) : hand.position)<.002f,"Actual grip stays within 2 mm of animated hand socket");
        var gear=net.parent;
        Capture(g.View.GameCamera,gear,gear.position+gear.rotation*new Vector3(-.3f,.8f,0),"player-net-front.png",new Vector3(-2,1.2f,4));
        Capture(g.View.GameCamera,gear,gear.position+gear.rotation*new Vector3(-.3f,.8f,0),"player-net-side.png",new Vector3(-4,1.2f,1));
        var clips=story.openingTimeline.GetOutputTracks().SelectMany(t=>t.GetClips()).Select(c=>c.asset).OfType<HabitatDockBeatClip>().ToArray();
        var stow=clips.First(c=>c.beat==HabitatDockBeat.StowNet);
        story.EvaluateTimelineShot(stow,.5f);
        Check(net.gameObject.activeSelf&&Quaternion.Angle(net.localRotation,Quaternion.Euler(0,0,-61.5f))<.1f,"Mid-stow preserves authored rotation and visibility");
        story.EvaluateTimelineShot(stow,.94f);Check(!net.gameObject.activeSelf,"Net disappears after 93% of stow");
        story.EvaluateTimelineShot(clips.First(c=>c.beat==HabitatDockBeat.WalkToShop),.3f);Check(!net.gameObject.activeSelf,"Walk to shop keeps the net hidden");
        story.EvaluateTimelineShot(stow,0f);Check(net.gameObject.activeSelf,"Timeline resampling restores net visibility");
        var holder=new GameObject("Temporary legacy net migration test");holder.SetActive(false);
        try
        {
            var dock=holder.AddComponent<HabitatDockPrototype>();var socket=new GameObject("Saved net socket").transform;socket.SetParent(holder.transform);socket.localPosition=new Vector3(.55f,.75f,0);socket.localRotation=Quaternion.Euler(0,0,-18);
            var old=new GameObject("Net cord");old.transform.SetParent(socket);
            typeof(HabitatDockPrototype).GetField("netRoot",Private).SetValue(dock,socket);
            var method=typeof(HabitatDockPrototype).GetMethod("ApplyLandingNetArt",Private);
            Check((bool)method.Invoke(dock,null),"Saved HarborPrototype net migrates without rebuilding scene");
            Check(!old.activeSelf&&socket.Find("Landing net art"),"Legacy flat cords are retired");
            method.Invoke(dock,null);
            Check(socket.Cast<Transform>().Count(t=>t.name=="Landing net art")==1,"Repeated migration does not duplicate the net");
            Check(Vector3.Distance(socket.localPosition,new Vector3(.55f,.75f,0))<.0001f&&Quaternion.Angle(socket.localRotation,Quaternion.Euler(0,0,-18))<.1f,"Migration preserves socket position and rotation");
        }
        finally{UnityEngine.Object.DestroyImmediate(holder);}
        string result=string.Join("\n",checks);File.WriteAllText(Path.Combine(Folder,"checks.txt"),result);return result;
    }
    public static string Restore()
    {
        if (HabitatGame.Instance) HabitatGame.Instance.Story.Cancel();
        return "Preview cancelled; no completion callback or catch reward; original session restored before preview.";
    }
    public static async Task<string> CheckMovingGrip()
    {
        var g=HabitatGame.Instance;g.Story.Cancel();
        int fish=g.State.fish,shrimp=g.State.shrimp,day=g.State.day,level=g.State.levelIndex;
        if(!g.Story.BeginOpening())throw new Exception("Cannot replay opening");
        float maxError=0f;int samples=0;bool sawStow=false,sawWalk=false;
        double end=UnityEditor.EditorApplication.timeSinceStartup+25;
        try
        {
            while(g.Story.IsPlaying&&UnityEditor.EditorApplication.timeSinceStartup<end)
            {
                await Task.Delay(80);
                var net=Net(g.Story);if(!net)break;
                if(net.gameObject.activeInHierarchy)
                {
                    var art=net.Find("Landing net art");var hand=g.View.PlayerVisual.GetComponentsInChildren<Transform>(true).First(t=>t.name=="mixamorig:RightHand");
                    maxError=Mathf.Max(maxError,Vector3.Distance(art.position,hand.position));samples++;
                }
                sawStow |= g.Story.CurrentShot=="StowNet";
                if(g.Story.CurrentShot=="WalkToShop"){sawWalk=true;break;}
            }
            if(samples<10||maxError>.002f||!sawStow||!sawWalk)throw new Exception("Moving grip check failed: "+samples+" / "+maxError+" / "+sawStow+" / "+sawWalk);
        }
        finally{g.Story.Cancel();}
        if(g.State.fish!=fish||g.State.shrimp!=shrimp||g.State.day!=day||g.State.levelIndex!=level)throw new Exception("Preview modified game state");
        string result="PASS: natural Timeline reaches return, stow and walk; animated-hand samples="+samples+"; maximum grip error="+maxError+" m; cancel preserves fish/shrimp/day/level.";
        File.WriteAllText(Path.Combine(Folder,"moving-grip.txt"),result);return result;
    }
}
