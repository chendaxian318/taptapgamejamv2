using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;

public static class FormalCompletionRuntime
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static HabitatGame game;static StringBuilder report;
    static void Check(bool okay,string label){if(!okay)throw new Exception("FAIL "+label);report.AppendLine("PASS "+label);}
    static async Task Frames(int n=3){for(int i=0;i<n;i++)await Task.Yield();}
    static async Task Until(Func<bool> done,float timeout=8)
    {float end=Time.realtimeSinceStartup+timeout;while(!done()&&Time.realtimeSinceStartup<end)await Task.Yield();if(!done())throw new Exception("Timeout waiting for animation / completion");}
    static async Task ArrivalFixture()
    {
        var s=new HabitatState {levelIndex=7,authoredLevel=true,width=7,depth=7,tiles=Enumerable.Range(0,49).Select(i=>new Tile {height=1}).ToArray(),playerX=0,playerZ=3,carryingAnimal=2,levelName="到位验证",objective="验证"};
        s.tiles[3*7+4].height=0;
        s.animals.Add(new Animal {id=1,species=Species.Crab,x=2,z=3});
        s.animals.Add(new Animal {id=2,species=Species.Shrimp,x=0,z=3,carried=true});
        s.animals.Add(new Animal {id=3,species=Species.Goldfish,x=4,z=3});
        game.CancelPendingAction();game.State=s;game.paused=true;game.View.Initialize(s);await Frames();
        Check(!game.State.won,"fixture not initially complete");
        game.Move(1,0);Check(game.Busy&&!game.State.won&&game.State.animals[0].x==2,"candidate movement cannot complete or move authoritative crab early");
        game.CancelPendingAction();await Frames();
        Check(!game.State.won&&game.State.animals[0].x==2,"cancelled approach cannot win");
        game.Move(1,0);await Until(()=>!game.Busy&&game.State.won);
        Check(game.State.animals[0].x==3&&game.View.ResidentsArrived(game.State),"arrival commits crab and only then completes");
        var saved=typeof(HabitatGame).GetMethod("CaptureSnapshot",Private).Invoke(game,null);
        string json=JsonUtility.ToJson(saved);var snapshotType=typeof(HabitatGame).GetNestedType("Snapshot",BindingFlags.NonPublic);
        game.Undo();await Frames();Check(!game.State.won&&game.State.animals[0].x==2,"undo reverses completion with causal action");
        typeof(HabitatGame).GetMethod("RestoreSnapshot",Private).Invoke(game,new[]{JsonUtility.FromJson(json,snapshotType)});
        await Frames();Check(game.State.won,"snapshot JSON round trip preserves per-island completion");
        game.RestartCurrentLevel();await Frames();Check(!game.State.won,"restart clears island completion");
    }
    public static Task<string> Third(){return Run(7,"third-route.json");}
    public static Task<string> Fourth(){return Run(8,"fourth-route.json");}
    static async Task<string> Run(int level,string routeFile)
    {
        game=HabitatGame.Instance;if(!game||game.Busy)throw new Exception("Requires idle game");
        report=new StringBuilder("Actual Unity controller route; paused ecology; not full campaign.\n");
        var capture=typeof(HabitatGame).GetMethod("CaptureSnapshot",Private);var restore=typeof(HabitatGame).GetMethod("RestoreSnapshot",Private);
        var original=capture.Invoke(game,null);bool pause=game.paused;int tool=game.tool;
        string latest=Path.Combine(Application.persistentDataPath,"Playtests","latest.json");byte[] bytes=File.Exists(latest)?File.ReadAllBytes(latest):null;
        try
        {
            game.Hud.ClosePanels();if(level==7)await ArrivalFixture();
            game.LoadLevel(level);game.paused=true;
            if(level==7){game.State.rod=true;game.State.backBasketUnlocked=true;}
            await Frames();Check(!game.State.won,"authored initial state is incomplete");
            var source=File.ReadAllText("Verification/formal-completion-20261016/"+routeFile);
            var steps=Regex.Matches(source,@"\[\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*\]").Cast<Match>().Select(m=>new[]{int.Parse(m.Groups[1].Value),int.Parse(m.Groups[2].Value),int.Parse(m.Groups[3].Value)}).ToArray();
            Check(steps.Length>0,"recorded route loaded");
            for(int i=0;i<steps.Length;i++)
            {
                int[] a=steps[i];int actions=game.ActionCount;
                if(a[0]==0)game.Move(a[1],a[2],true);
                else{game.tool=a[0]==2?2:0;game.Select(a[1],a[2]);game.ApplyTool();}
                if(game.Busy)Check(!game.State.won,"no early win during pending action "+(i+1));
                await Until(()=>!game.Busy);await Until(()=>game.View.ResidentsArrived(game.State));await Frames();
                Check(game.ActionCount==actions+1,"normal controller action "+(i+1)+" committed exactly once");
                if(i<steps.Length-1)Check(!game.State.won,"goal not granted before route end "+(i+1));
            }
            await Until(()=>game.State.won);
            Check(HabitatMechanicLevels.HabitatProgress(game.State,game.tuning)==(level==7?1:2),"confirmed habitat conditions true at completion");
            Check(game.State.coins==0,"route injects no money or invented win reward");
            if(level==8)Check(!game.State.tideEverActivated,"alternate route bypasses tide with original boxes");
            game.tool=0;game.View.FocusPlayer();await Frames(6);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/formal-completion-20261016/stage-"+level+"-complete.png"));await Frames(12);
            string json=JsonUtility.ToJson(capture.Invoke(game,null),true);
            File.WriteAllText("Verification/formal-completion-20261016/stage-"+level+"-complete.json",json);
            game.Undo();await Frames();Check(!game.State.won,"last puzzle action undo revokes completion");
            return "PASS stage "+(level-4)+": "+steps.Length+" controller actions, completion and undo verified.";
        }
        finally
        {
            game.CancelPendingAction();restore.Invoke(game,new[]{original});game.paused=pause;game.tool=tool;game.View.FocusPlayer();
            Check(bytes==null?!File.Exists(latest):File.Exists(latest)&&bytes.SequenceEqual(File.ReadAllBytes(latest)),"user persistent latest save unchanged");
            File.WriteAllText("Verification/formal-completion-20261016/runtime-"+level+".txt",report.ToString());
        }
    }
}
