using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;
public static class JellyfishChecks
{
    static StringBuilder log; static int count;
    static void Check(bool ok,string text){if(!ok)throw new Exception("FAIL "+text);log.AppendLine("PASS "+(++count)+" "+text);}
    static async Task Frames(int n=5){for(int i=0;i<n;i++)await Task.Yield();}
    public static string Begin()
    {
        if(!Application.isPlaying||!HabitatGame.Instance)throw new InvalidOperationException("Enter a fresh Play session and wait for HabitatGame before testing.");
        AppDomain.CurrentDomain.SetData("JellyfishChecks.Result","running");Run();return "started";
    }
    static async void Run()
    {
        log=new StringBuilder();count=0;var game=HabitatGame.Instance;
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var capture=typeof(HabitatGame).GetMethod("CaptureSnapshot",flags);
        var restore=typeof(HabitatGame).GetMethod("RestoreSnapshot",flags);
        var snapshot=capture.Invoke(game,null);bool paused=game.paused;
        string result="";GameObject model=null;
        string save=Path.Combine(Application.persistentDataPath,"Playtests","latest.json");
        byte[] previous=File.Exists(save)?File.ReadAllBytes(save):null;
        try
        {
            var prefab=Resources.Load<GameObject>("Models/Jellyfish/Jellyfish");Check(prefab,"prefab loads");
            var source=prefab.GetComponent<HabitatEquipmentAnimation>();
            Check(source.Clips.Count==2&&source.Clips.All(c=>c.isLooping),"two looping authored clips");
            Check(source.Clips.Single(c=>c.name=="Idle").length==3f&&Mathf.Abs(source.Clips.Single(c=>c.name=="Swim").length-1.6f)<.001f,"source clip durations retained");
            Check(prefab.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(s=>s.bones).Distinct().Count()==33,"all 33 bones imported");
            Check(prefab.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m&&m.shader.isSupported)),"PBR shaders supported");
            Check(prefab.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Any(m=>m.GetTexture("baseColorTexture").width==4096),"4K color texture retained");
            Check(!source.Animator.applyRootMotion,"root motion disabled");
            model=UnityEngine.Object.Instantiate(prefab);model.transform.position=Vector3.one*-100;
            var clips=model.GetComponent<HabitatEquipmentAnimation>();var visual=model.GetComponent<HabitatJellyfishVisual>();
            var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();
            try
            {
                foreach(var clip in clips.Clips)
                {
                    clips.Sample(clip.name,0);skin.BakeMesh(mesh,true);var start=mesh.vertices;
                    clips.Sample(clip.name,clip.length*.3f);skin.BakeMesh(mesh,true);
                    Check(start.Zip(mesh.vertices,(a,b)=>(a-b).sqrMagnitude).Max()>.000001,"real mesh deformation: "+clip.name);
                    clips.Sample(clip.name,clip.length);skin.BakeMesh(mesh,true);
                    Check(start.Zip(mesh.vertices,(a,b)=>(a-b).magnitude).Max()<.001,"seamless loop: "+clip.name);
                }
            }finally{UnityEngine.Object.Destroy(mesh);}
            visual.Render(.02f,Vector3.zero,Vector3.zero,true,false);Check(visual.CurrentClip=="Idle","reset idle");
            visual.Render(.02f,Vector3.right*.1f,Vector3.zero,false,false);Check(visual.CurrentClip=="Swim","travel selects swim");
            float time=clips.TimeSeconds;visual.Render(0,Vector3.right*.1f,Vector3.right*.1f,false,false);
            Check(visual.CurrentClip=="Swim"&&clips.TimeSeconds==time,"zero display dt freezes pose and selection");
            visual.Render(.2f,Vector3.zero,Vector3.zero,false,false);Check(visual.CurrentClip=="Idle","settling returns to idle");
            visual.Render(.02f,Vector3.right*.1f,Vector3.zero,false,true);Check(visual.CurrentClip=="Idle","carried motion uses idle");
            visual.Render(.02f,Vector3.right*10,Vector3.zero,false,false);Check(visual.CurrentClip=="Idle"&&clips.TimeSeconds==0,"teleport resets phase without false swim");
            UnityEngine.Object.Destroy(model);model=null;
            game.Hud.ClosePanels();
            foreach(int level in new[]{4,8,9})
            {
                game.LoadLevel(level);game.paused=true;await Frames(12);
                var residents=UnityEngine.Object.FindObjectsByType<HabitatJellyfishVisual>();
                Check(residents.Length==game.State.animals.Count(a=>a.species==Species.Jellyfish)&&(level==4||residents.Length>0),"all jellyfish replaced on level "+level);
                Check(residents.All(v=>v.GetComponentsInChildren<SpriteRenderer>().Length==0),"no old sprite on level "+level);
            }
            game.LoadLevel(8);game.paused=true;await Frames(12);
            var animal=game.State.animals.First(a=>a.species==Species.Jellyfish);
            var resident=UnityEngine.Object.FindObjectsByType<HabitatJellyfishVisual>().Single(v=>v.transform.parent.parent.name.EndsWith(" "+animal.id));
            Check(Mathf.Abs(resident.transform.parent.localPosition.y)<.001,"water placement uses bell origin without extra bob");
            game.View.FocusTutorialTarget(new Vector3(animal.x,.5f,animal.z));await Frames(15);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/jellyfish-20261017/fourth-level.png"));await Frames(15);
            // Controlled presentation fixtures; no habitat or carrying rules are changed.
            var s=game.State;animal.carried=true;s.carryingAnimal=animal.id;game.View.Initialize(s);await Frames(12);
            resident=UnityEngine.Object.FindObjectsByType<HabitatJellyfishVisual>().Single(v=>v.transform.parent.parent.name.EndsWith(" "+animal.id));
            Check(resident.CurrentClip=="Idle"&&Mathf.Abs(resident.transform.parent.localPosition.y-resident.GroundClearance*resident.transform.parent.localScale.y)<.001,"carry anchor clears tentacles and preserves idle");
            animal.carried=false;s.carryingAnimal=-1;
            bool found=false;for(int z=0;z<s.depth&&!found;z++)for(int x=0;x<s.width&&!found;x++)if(HabitatRules.Height(s,x,z)>s.waterLevel){animal.x=x;animal.z=z;found=true;}
            game.View.Initialize(s);await Frames(12);
            resident=UnityEngine.Object.FindObjectsByType<HabitatJellyfishVisual>().Single(v=>v.transform.parent.parent.name.EndsWith(" "+animal.id));
            Check(Mathf.Abs(resident.transform.parent.localPosition.y-resident.GroundClearance)<.001,"dry placement keeps full tentacles above ground");
            Check(previous==null?!File.Exists(save):previous.SequenceEqual(File.ReadAllBytes(save)),"user save unchanged");
            result="PASS "+count+" jellyfish checks";
        }
        catch(Exception e){result=e.ToString();log.AppendLine(result);}
        finally
        {
            if(model)UnityEngine.Object.Destroy(model);
            game.CancelPendingAction();restore.Invoke(game,new[]{snapshot});game.paused=paused;
            Directory.CreateDirectory("Verification/jellyfish-20261017");File.WriteAllText("Verification/jellyfish-20261017/checks.txt",log.ToString());
            AppDomain.CurrentDomain.SetData("JellyfishChecks.Result",result);
        }
    }
}

