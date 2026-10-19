using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;

public static class OptimizationCheckpointChecks
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const string Dir="Verification/release-playtest-20261018";
    static HabitatGame g;static StringBuilder log;static int count;
    static object Call(string n,params object[] a)=>typeof(HabitatGame).GetMethod(n,F).Invoke(g,a);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);log.AppendLine("PASS "+(++count)+" "+message);File.WriteAllText(Dir+"/checkpoint-runtime.txt",log.ToString());}
    static async Task Frame(){int f=Time.frameCount;while(Time.frameCount==f)await Task.Delay(8);}
    static async Task Arrive(){float deadline=Time.realtimeSinceStartup+8;while(g.Busy&&Time.realtimeSinceStartup<deadline)await Frame();Check(!g.Busy,"pending action arrives within deadline");}
    static void Setup(int coins)
    {
        g.CancelPendingAction();g.Hud.ClosePanels();g.LoadLevel(1);g.paused=true;g.tool=0;
        g.State=new HabitatState{width=7,depth=7,levelIndex=1,tiles=Enumerable.Range(0,49).Select(_=>new Tile{height=1}).ToArray(),playerX=2,playerZ=3,dockX=2,dockZ=3,facingX=1,facingZ=0,coins=coins,rod=true,specialRod=true,backBasketUnlocked=true,openingCompleted=true};
        g.View.Initialize(g.State);Call("CaptureRetryPoint");
    }
    static Tile Q(int x,int z)=>HabitatRules.TileAt(g.State,x,z);
    public static async Task<string> Run()
    {
        g=HabitatGame.Instance;var original=Call("CaptureSnapshot");bool session=g.SessionStarted;
        var field=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);var oldFolder=field.GetValue(null);
        field.SetValue(null,Path.GetFullPath(Dir+"/checkpoint-saves"));typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);
        count=0;log=new StringBuilder("Checkpoint inventory/economy tests with controlled board fixtures and actual controller commits.\n");
        try
        {
            Setup(400);Q(3,3).prop=PropKind.Crate;g.Buy("launcher");Check(g.State.launchers==1&&g.State.coins==0,"paid launcher purchase");
            g.View.Initialize(g.State);g.tool=7;Check(g.ConfirmUtility(3,3,1,0),"launch accepted");await Arrive();Check(g.State.launchers==0&&Q(5,3).prop==PropKind.Crate,"launch consumes and moves crate");
            g.RestartCurrentLevel();Check(g.State.launchers==1&&g.State.coins==0&&Q(3,3).prop==PropKind.Crate&&Q(5,3).prop==PropKind.None,"retry restores consumption and world together without refunding purchase");
            g.RestartCurrentLevel();Check(g.State.launchers==1&&g.State.tiles.Count(t=>t.prop==PropKind.Crate)==1,"repeated retry cannot duplicate consumable or native crate");
            Setup(300);Q(3,3).height=2;g.Buy("shovel");g.View.Initialize(g.State);g.tool=5;Check(g.ConfirmUtility(3,3,1,0),"shovel accepted");await Arrive();Check(g.State.shovels==0&&Q(3,3).height==1,"dig consumes shovel and changes terrain");
            g.RestartCurrentLevel();Check(g.State.shovels==1&&g.State.coins==0&&Q(3,3).height==2,"retry restores soil and paid shovel consistently");
            Setup(6);g.State.levelIndex=4;g.Buy("crate");Check(g.State.boatCrates==1&&g.State.coins==0,"cargo purchase at fixture legacy price");
            g.TransferCargo(true);g.Select(3,3);g.ApplyTool();Check(g.State.boatCrates==0&&Q(3,3).prop==PropKind.Crate,"cargo unloaded and placed through real actions");
            g.RestartCurrentLevel();Check(g.State.boatCrates==1&&Q(3,3).prop==PropKind.None&&g.State.carryingProp==PropKind.None,"retry returns cargo to checkpoint without duplicating ground crate");
            Setup(0);Q(3,3).prop=PropKind.Crate;Call("CaptureRetryPoint");g.View.Initialize(g.State);g.Select(3,3);g.ApplyTool();g.TransferCargo(false);
            Check(g.State.boatCrates==1&&Q(3,3).prop==PropKind.None,"native crate can enter boat cargo");g.RestartCurrentLevel();
            Check(g.State.boatCrates==0&&Q(3,3).prop==PropKind.Crate,"retry restores native crate without keeping exported copy");
            Setup(0);g.State.shells=1;g.Buy("sell");Check(g.State.shells==0&&g.State.coins==200&&g.State.deliveredShells==1,"earned-shell fixture sold through real transaction");
            g.RestartCurrentLevel();g.RestartCurrentLevel();Check(g.State.shells==0&&g.State.coins==200&&g.State.deliveredShells==1,"retry preserves sale exactly once");
            Setup(200);g.State.chapterOneDelivered=true;g.Buy("fan");g.Undo();Check(g.State.coins==200&&g.State.carryingProp==PropKind.None,"undo purchase restores money");
            g.RestartCurrentLevel();Check(g.State.coins==200&&g.State.carryingProp==PropKind.None,"retry after undo does not reapply cancelled purchase");
            return "PASS "+count+" checkpoint economy checks";
        }
        catch(Exception e){File.AppendAllText(Dir+"/checkpoint-runtime.txt","FAIL "+e+"\n");throw;}
        finally{g.CancelPendingAction();g.Hud.ClosePanels();Call("RestoreSnapshot",original);typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);field.SetValue(null,oldFolder);}
    }
}

