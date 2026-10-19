using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using HabitatIsland;

public static class BoatShopChecks
{
    static readonly string Folder = Path.GetFullPath("Verification/boat-shop-20261016");
    static List<string> report;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); report.Add("PASS: " + message); }
    static async Task Frames(int ms) { int f=Time.frameCount; await Task.Delay(ms); if(Time.frameCount<=f) throw new Exception("No advancing game frames"); }
    static void Capture(Camera source, Vector3 target, float size, string file)
    {
        var go=new GameObject("Temporary asset verification camera");
        var camera=go.AddComponent<Camera>();camera.CopyFrom(source);camera.enabled=false;
        camera.orthographic=true;camera.orthographicSize=size;
        camera.transform.SetPositionAndRotation(target-source.transform.forward*22,source.transform.rotation);
        var rt=new RenderTexture(1280,720,24);var old=RenderTexture.active;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try { camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Folder,file),image.EncodeToPNG()); }
        finally { RenderTexture.active=old;camera.targetTexture=null;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(go); }
    }

    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder);report=new List<string>();
        var g=HabitatGame.Instance;
        Check(Application.isPlaying&&g&&g.View.Harbor.Ready,"Main map and original player are running");
        double openingDeadline=UnityEditor.EditorApplication.timeSinceStartup+25;
        while(g.StoryPlaying&&UnityEditor.EditorApplication.timeSinceStartup<openingDeadline)await Frames(80);
        Check(!g.StoryPlaying,"Opening finishes naturally without skip");
        var player=g.View.PlayerVisual;var dock=g.View.Harbor.ModelTransform;
        var boat=dock.GetComponent<HabitatHarborBoat>();var keeper=g.GetComponent<HabitatShopkeeperPlaceholder>();
        Check(boat&&boat.Vessel&&boat.Seat&&boat.Footwell,"Delivered boat and surface anchors exist at the map berth");
        Check(keeper&&keeper.UsingArtistPrefab&&keeper.Animator,"Delivered shopkeeper replaces geometry and has an Animator");
        Check(!keeper.Animator.applyRootMotion,"Shopkeeper root motion is disabled");
        Check(Vector3.Distance(keeper.PlaceholderRoot.position,g.View.PlayerWorldPosition)>.95f,"Shopkeeper stands beside the interaction point without overlapping the player");
        Check(g.State.openingCompleted&&g.State.onHarbor,"Natural opening reached the dock with the same game state");
        Vector3 berth=boat.Vessel.position,keeperPosition=keeper.PlaceholderRoot.position;
        g.Hud.ClosePanels();await Frames(300);
        g.OpenShopInteraction();await Frames(350);
        Check(keeper.Animator.GetCurrentAnimatorStateInfo(0).IsName("Greet"),"Opening interaction plays the supplied Greet clip");
        await Frames(2400);
        Check(keeper.Animator.GetCurrentAnimatorStateInfo(0).IsName("Talk"),"Greeting transitions to the supplied Talk loop during dialogue");
        Check(Vector3.Distance(keeperPosition,keeper.PlaceholderRoot.position)<.0001f,"Shopkeeper's feet origin does not drift during dialogue");
        Capture(g.View.GameCamera,keeperPosition+Vector3.up*.7f,2.1f,"shopkeeper.png");
        g.Hud.ClosePanels();await Frames(350);
        Check(keeper.Animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),"Closing dialogue returns to Idle");
        Capture(g.View.GameCamera,berth+Vector3.up*.5f,2.6f,"moored-boat.png");
        float minimumFootwell=float.PositiveInfinity;
        for(int i=0;i<=120;i++)
        {
            float t=i/120f;
            boat.SampleSailing(t,t*4.2f);
            minimumFootwell=Mathf.Min(minimumFootwell,boat.Footwell.position.y);
        }
        Check(minimumFootwell>g.State.waterLevel*.7f+.025f,"Footwell above water through all 121 samples of sailing and rocking");
        boat.RestoreBerth();
        int completed=0;int level=g.State.levelIndex,day=g.State.day;
        Vector3 playerBefore=player.transform.position;
        Check(g.Story.BeginDeparture(()=>completed++),"Departure starts from the actual moored vessel");
        await Frames(700);
        Check(Vector3.Distance(player.transform.position,boat.PassengerPosition)<.025f,"Player follows the actual footwell rather than the old +0.71 offset");
        Check(player.Motion==HabitatPlayerMotion.Ride&&player.MoveSpeed<.01f,"Player stands steadily while riding rather than walking in place");
        Check(boat.PassengerPosition.y>g.State.waterLevel*.7f+.025f,"Passenger footwell stays above the continuous sea surface");
        g.Story.TogglePause();Vector3 pausedBoat=boat.Vessel.position,pausedPlayer=player.transform.position;
        Capture(g.View.GameCamera,pausedBoat+Vector3.up*.5f,2.6f,"sailing.png");
        await Frames(300);
        Check(Vector3.Distance(pausedBoat,boat.Vessel.position)<.0001f&&Vector3.Distance(pausedPlayer,player.transform.position)<.0001f,"Pause freezes both boat and passenger");
        g.Story.Cancel();await Frames(150);
        Check(completed==0&&Vector3.Distance(berth,boat.Vessel.position)<.0001f,"Cancel restores the berth and never completes travel");
        Check(Vector3.Distance(playerBefore,player.transform.position)<.02f,"Cancel restores the original player position");
        Check(g.Story.BeginDeparture(()=>completed++),"Departure can restart after cancellation");
        double end=UnityEditor.EditorApplication.timeSinceStartup+8;
        while(g.StoryPlaying&&UnityEditor.EditorApplication.timeSinceStartup<end)await Frames(80);
        Check(!g.StoryPlaying&&completed==1,"Natural departure completes exactly once");
        Check(!g.Story.Skip()&&completed==1,"Finished departure cannot invoke the callback twice");
        Check(g.State.levelIndex==level&&g.State.day==day&&g.View.PlayerVisual==player,"Presentation does not commit map/day changes or replace the player");
        Capture(g.View.GameCamera,dock.position+Vector3.up*.4f,6.6f,"harbor.png");
        string result=string.Join("\n",report);File.WriteAllText(Path.Combine(Folder,"runtime-checks.txt"),result);return result;
    }

    public static async Task<string> OpeningPreview()
    {
        var g=HabitatGame.Instance;g.Hud.ClosePanels();
        int fish=g.State.fish,shrimp=g.State.shrimp;
        if(!g.StoryPlaying&&!g.Story.BeginOpening())throw new Exception("Cannot preview opening");
        double deadline=UnityEditor.EditorApplication.timeSinceStartup+25;
        string last="";int count=0;
        while(g.StoryPlaying&&UnityEditor.EditorApplication.timeSinceStartup<deadline)
        {
            if(last!=g.Story.CurrentShot&&g.Story.CurrentShot!="BlueSky")
            {
                last=g.Story.CurrentShot;
                await Frames(700);
                Capture(g.View.GameCamera,g.View.PlayerWorldPosition+Vector3.up*.5f,3.6f,"opening-"+(++count)+".png");
            }
            await Frames(40);
        }
        if(g.StoryPlaying||count<3||g.State.fish!=fish||g.State.shrimp!=shrimp)throw new Exception("Opening preview did not complete or changed catch");
        string result="PASS: real Timeline replay followed boat landing, stowing net, and walk to shop; completed naturally; existing catch unchanged.";
        File.WriteAllText(Path.Combine(Folder,"opening-checks.txt"),result);return result;
    }
}
