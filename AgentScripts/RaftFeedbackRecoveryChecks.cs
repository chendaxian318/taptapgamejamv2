using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HabitatIsland;
public static class RaftFeedbackRecoveryChecks
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const string Dir="Verification/feedback-20261018-144126/recovery";
    static HabitatGame g;static StringBuilder log;static int count;
    static object Call(string name,params object[] args)=>typeof(HabitatGame).GetMethod(name,F).Invoke(g,args);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);log.AppendLine("PASS "+(++count)+" "+message);File.WriteAllText(Dir+"/runtime.txt",log.ToString());}
    static Tile Q(int x,int z)=>HabitatRules.TileAt(g.State,x,z);
    static async Task Frame(){int f=Time.frameCount;while(Time.frameCount==f)await Task.Delay(4);}
    static async Task Until(Func<bool> condition){float deadline=Time.realtimeSinceStartup+10;while(!condition()&&Time.realtimeSinceStartup<deadline)await Frame();if(!condition())throw new Exception("recovery timeout");}
    static void Setup(int height=1,PropKind kind=PropKind.Float)
    {
        g.CancelPendingAction();g.Hud.ClosePanels();g.LoadLevel(1);g.paused=true;g.tool=0;
        g.State=new HabitatState{levelIndex=1,width=7,depth=7,tiles=Enumerable.Range(0,49).Select(_=>new Tile{height=height}).ToArray(),playerX=3,playerZ=3,dockX=0,dockZ=0,facingX=1,facingZ=0,openingCompleted=true};
        Q(4,3).height=kind==PropKind.Float?0:height-1;Q(4,3).prop=kind;g.View.Initialize(g.State);g.Select(4,3);Call("CaptureRetryPoint");
    }
    public static async Task<string> Run()
    {
        g=HabitatGame.Instance;var original=Call("CaptureSnapshot");bool session=g.SessionStarted;
        var field=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);var folder=field.GetValue(null);
        field.SetValue(null,Path.GetFullPath(Dir+"/saves"));typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);
        count=0;log=new StringBuilder("Visible net recovery: actual render frames and controller, isolated fixture saves.\n");Directory.CreateDirectory(Dir+"/frames");
        try
        {
            Setup();g.View.FocusPlayer();await Frame();g.Pull(-1,0);
            Check(g.Busy&&g.IsRecovering&&g.View.RecoveryPresentationValid,"legal recovery starts visible net presentation");
            Check(g.State.playerX==3&&Q(4,3).prop==PropKind.Float&&Q(3,3).prop==PropKind.None&&g.ActionCount==0,"no authority or count changes at start");
            Check(g.View.PlayerVisual.RecoveryNetReady,"existing landing net model is visible");
            Vector3 start=g.View.RecoveryPropPosition;int frameIndex=0;bool raised=false,retreated=false;float beforeProgress=-1;
            float limit=Time.realtimeSinceStartup+12;
            while(g.Busy&&Time.realtimeSinceStartup<limit)
            {
                float p=g.View.RecoveryProgress;
                if(g.TransitionOpacity!=0||g.State.playerX!=3||Q(4,3).prop!=PropKind.Float||Q(3,3).prop!=PropKind.None||g.ActionCount!=0)throw new Exception("early commit or blackout");
                if(g.View.RecoveryPropPosition.y>start.y+.3f)raised=true;
                if(g.View.PlayerWorldPosition.x<2.6f)retreated=true;
                if(p>=frameIndex/24f)
                {
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/frames/"+frameIndex.ToString("D3")+".png"));frameIndex++;
                }
                if(p<beforeProgress)throw new Exception("animation progress regressed");beforeProgress=p;
                await Frame();
            }
            Check(!g.Busy&&raised&&retreated,"visible box rises and character retreats before completion");
            Check(g.State.playerX==2&&Q(3,3).prop==PropKind.Crate&&Q(4,3).prop==PropKind.None&&g.ActionCount==1,"completion commits recovered crate and retreat exactly once");
            Check(!g.IsRecovering&&!g.View.PlayerVisual.RecoveryNetReady&&g.State.carryingProp==PropKind.None,"net stowed and crate never becomes inventory");
            await Frame();ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/frames/024.png"));await Frame();
            g.Undo();Check(g.State.playerX==3&&Q(4,3).prop==PropKind.Float&&Q(3,3).prop==PropKind.None,"undo restores original float and position");
            foreach(float phase in new[]{.1f,.38f,.65f,.92f})
            {
                Setup();g.Pull(-1,0);await Until(()=>g.View.RecoveryProgress>=phase);
                g.Pull(-1,0);g.Move(0,1);Check(g.ActionCount==0&&g.State.playerX==3&&g.State.playerZ==3,"repeat input blocked at phase "+phase);
                g.CancelPendingAction();await Frame();
                Check(!g.Busy&&!g.IsRecovering&&!g.View.PlayerVisual.RecoveryNetReady&&g.State.playerX==3&&Q(4,3).prop==PropKind.Float&&Q(3,3).prop==PropKind.None&&g.ActionCount==0,"cancel restores original board at phase "+phase);
                Check(Vector3.Distance(g.View.PlayerWorldPosition,new Vector3(3,.755f,3))<.01f,"cancel restores visible player at phase "+phase);
            }
            Setup();Q(2,3).prop=PropKind.Obstacle;g.View.Initialize(g.State);g.Pull(-1,0);
            Check(!g.Busy&&!g.View.PlayerVisual.RecoveryNetReady&&g.ActionCount==0,"blocked retreat refuses animation and state changes");
            Setup(2,PropKind.Crate);Q(4,3).coveredProp=PropKind.Tide;g.State.tideActive=true;g.State.waterLevel=1;g.State.tideBaseLevel=0;g.View.Initialize(g.State);g.Pull(-1,0);
            await Until(()=>g.View.RecoveryProgress>.6f);Check(g.State.waterLevel==1&&g.State.tideActive&&Q(4,3).coveredProp==PropKind.Tide,"tide and covered mechanism remain authoritative during recovery");
            await Until(()=>!g.Busy);Check(g.State.waterLevel==0&&!g.State.tideActive&&Q(4,3).prop==PropKind.Tide&&Q(3,3).prop==PropKind.Crate,"tide change only commits with completed recovery");
            Setup(2,PropKind.ClimbBlock);Q(2,3).height=1;g.View.Initialize(g.State);g.Pull(-1,0);await Until(()=>!g.Busy);
            Check(g.State.playerX==3&&Q(4,3).prop==PropKind.ClimbBlock&&!g.IsRecovering,"fixed climb block rejects recovery and retreat");
            Setup();g.Pull(-1,0);await Until(()=>g.View.RecoveryProgress>.5f);g.LoadLevel(1);await Frame();
            Check(!g.Busy&&!g.IsRecovering&&!g.View.PlayerVisual.RecoveryNetReady,"level change cancels recovery and cleans net");
            Setup();Check(g.SaveSession(3),"save original fixture");g.Pull(-1,0);await Until(()=>g.View.RecoveryProgress>.5f);
            Check(g.LoadSession(3)&&!g.Busy&&!g.View.PlayerVisual.RecoveryNetReady&&Q(4,3).prop==PropKind.Float,"load cancels pending recovery without granting box");
            Setup();g.Pull(-1,0);await Until(()=>g.View.RecoveryProgress>.5f);g.RestartCurrentLevel();
            Check(g.Busy&&Q(4,3).prop==PropKind.Float&&g.ActionCount==0,"restart remains blocked during action without early settlement");
            g.CancelPendingAction();g.RestartCurrentLevel();Check(!g.Busy&&Q(4,3).prop==PropKind.Float&&!g.View.PlayerVisual.RecoveryNetReady,"cancel then restart restores checkpoint cleanly");
            Setup();g.Pull(-1,0);await Until(()=>g.View.RecoveryProgress>.3f);Call("SetBackgroundSuspended",true);float progress=g.View.RecoveryProgress;
            for(int i=0;i<4;i++)await Frame();Check(g.View.RecoveryProgress==progress&&g.State.playerX==3,"background suspends recovery without committing");
            Call("SetBackgroundSuspended",false);await Until(()=>!g.Busy);Check(g.ActionCount==1,"foreground resumes and commits once");
            return "PASS "+count+" visible recovery checks";
        }
        catch(Exception e){File.AppendAllText(Dir+"/runtime.txt","FAIL "+e+"\n");throw;}
        finally{Call("SetBackgroundSuspended",false);g.CancelPendingAction();g.Hud.ClosePanels();Call("RestoreSnapshot",original);typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);field.SetValue(null,folder);}
    }
}

