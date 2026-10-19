using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Text;
using UnityEngine;
using HabitatIsland;
public static class FacingSelectionChecks
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const string Dir="Verification/mouse-facing-20261018";
    static HabitatGame g;static StringBuilder log;static int count;
    static object Call(string n,params object[] a)=>typeof(HabitatGame).GetMethod(n,F).Invoke(g,a);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);log.AppendLine("PASS "+(++count)+" "+message);File.WriteAllText(Dir+"/facing-runtime.txt",log.ToString());}
    static bool Front()=>g.State.selectedX==g.State.playerX+g.State.facingX&&g.State.selectedZ==g.State.playerZ+g.State.facingZ;
    static Tile Q(int x,int z)=>HabitatRules.TileAt(g.State,x,z);
    static void Setup()
    {
        g.CancelPendingAction();g.Hud.ClosePanels();g.LoadLevel(1);g.paused=true;g.tool=0;
        g.State=new HabitatState{width=7,depth=7,levelIndex=1,tiles=Enumerable.Range(0,49).Select(_=>new Tile{height=1}).ToArray(),playerX=3,playerZ=3,dockX=0,dockZ=0,facingX=0,facingZ=1,openingCompleted=true,rod=true,specialRod=true};
        g.View.Initialize(g.State);g.Select(1,1);
    }
    static async Task Arrive(){float end=Time.realtimeSinceStartup+8;while((g.Busy||g.IsRecovering)&&Time.realtimeSinceStartup<end)await Task.Delay(8);Check(!g.Busy&&!g.IsRecovering,"visual action completes");}
    public static async Task<string> Run()
    {
        g=HabitatGame.Instance;var original=Call("CaptureSnapshot");bool session=g.SessionStarted;
        var field=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);var folder=field.GetValue(null);
        field.SetValue(null,Path.GetFullPath(Dir+"/saves"));typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);
        count=0;log=new StringBuilder("Facing selection: isolated fixtures, real controller and rendered frames.\n");
        try
        {
            foreach(var d in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})
            {
                Setup();Q(3+d.x,3+d.y).height=2;g.Move(d.x,d.y);
                Check(!g.Busy&&g.State.playerX==3&&g.State.playerZ==3&&g.ActionCount==0,"blocked turn does not move or count action "+d);
                Check(Front()&&g.State.facingX==d.x&&g.State.facingZ==d.y,"blocked turn selects facing cell "+d);
                g.GetTarget(out int x,out int z);Check(x==3+d.x&&z==3+d.y,"tool target agrees with facing selection "+d);
            }
            Setup();Q(4,3).prop=PropKind.Crate;Q(5,3).prop=PropKind.Obstacle;g.View.Initialize(g.State);g.Move(1,0);g.ApplyTool();
            Check(g.State.carryingProp==PropKind.Crate&&Q(4,3).prop==PropKind.None,"E after blocked push picks facing crate instead of stale target");
            Setup();Q(4,3).prop=PropKind.Fan;g.View.Initialize(g.State);g.Move(1,0);g.RotateTarget();
            Check(Q(4,3).rotation==1&&Front(),"R after blocked turn rotates facing fan");
            Setup();Q(4,3).height=0;g.Move(1,0);Check(!g.Busy&&Front(),"water edge turn updates selection without walking into water");
            Setup();g.State.playerX=6;g.View.Initialize(g.State);g.Move(1,0);g.GetTarget(out int ex,out int ez);
            Check(ex==7&&ez==3&&Front(),"out-of-map facing clears old usable target instead of clamping elsewhere");
            Setup();g.Move(1,0);Check(g.Busy&&g.State.playerX==3&&g.State.selectedX==1,"successful movement retains source authority until arrival");
            g.CancelPendingAction();Check(g.State.playerX==3&&g.State.selectedX==1&&g.ActionCount==0,"cancel restores original position and explicit selection");
            g.Move(1,0);await Arrive();Check(g.State.playerX==4&&Front(),"arrived movement selects front cell");g.Undo();
            Check(g.State.playerX==3&&g.State.selectedX==1,"undo restores original selection and position");
            Setup();g.Select(2,3);g.GetTarget(out int mx,out int mz);Check(mx==2&&mz==3,"explicit mouse selection remains usable while idle");
            Q(3,4).height=2;g.Move(0,1);Check(Front(),"keyboard intent retakes facing target even if direction unchanged");
            Setup();Q(4,3).prop=PropKind.Crate;g.View.Initialize(g.State);g.Pull(-1,0);await Arrive();
            Check(g.State.playerX==2&&g.State.facingX==1&&g.State.selectedX==3&&Front(),"backward pull selects pulled crate in front, not retreat direction");
            Setup();Q(4,3).height=0;Q(4,3).prop=PropKind.Float;g.View.Initialize(g.State);g.Select(4,3);g.ApplyTool();await Arrive();
            Check(g.State.playerX==2&&Q(3,3).prop==PropKind.Crate&&g.State.selectedX==3&&Front(),"recovery retreat keeps recovered crate as front target");
            Setup();for(int x=0;x<7;x++)Q(x,3).height=0;Q(1,3).height=1;Q(1,3).prop=PropKind.Fan;Q(1,3).rotation=1;Q(3,3).prop=PropKind.Float;
            g.State.facingX=0;g.State.facingZ=1;g.Select(3,4);g.tuning.moveSeconds=.2f;g.tuning.animalMovement=false;g.View.Initialize(g.State);
            Call("AdvanceSimulation",.2f);await Arrive();Check(g.State.playerX==4&&Front()&&g.State.selectedX==4&&g.State.selectedZ==4,"riding float keeps front selection aligned after arrival");
            return "PASS "+count+" facing-selection checks";
        }
        catch(Exception e){File.AppendAllText(Dir+"/facing-runtime.txt","FAIL "+e+"\n");throw;}
        finally{g.CancelPendingAction();g.Hud.ClosePanels();Call("RestoreSnapshot",original);typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);field.SetValue(null,folder);}
    }
}

