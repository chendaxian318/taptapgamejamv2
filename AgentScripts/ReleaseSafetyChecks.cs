using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using HabitatIsland;
using Newtonsoft.Json.Linq;

public static class OptimizationSafetyChecks
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const string Dir="Verification/release-playtest-20261018";
    static HabitatGame g; static StringBuilder log;static int count;
    static object Call(string n,params object[] args)=>typeof(HabitatGame).GetMethod(n,F).Invoke(g,args);
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);log.AppendLine("PASS "+(++count)+" "+text);File.WriteAllText(Dir+"/safety-runtime.txt",log.ToString());}
    static async Task Frames(int n){int end=Time.frameCount+n;while(Time.frameCount<end)await Task.Delay(8);}
    static int Fans()=>g.State.tiles.Count(t=>t.prop==PropKind.Fan)+g.State.bagProps.Count(p=>p==PropKind.Fan)+(g.State.carryingProp==PropKind.Fan?1:0);
    public static async Task<string> Run()
    {
        g=HabitatGame.Instance;if(!Application.isPlaying||!g||g.Busy)throw new Exception("Idle play required");
        Directory.CreateDirectory(Dir);log=new StringBuilder("Optimization safety runtime checks. Isolated save directory; controlled fixtures, not complete campaign.\n");count=0;
        var original=Call("CaptureSnapshot");bool session=g.SessionStarted,opening=g.playOpeningOnNewGame;
        var field=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);var oldFolder=field.GetValue(null);
        string folder=Path.GetFullPath(Dir+"/safety-saves");field.SetValue(null,folder);Directory.CreateDirectory(folder);
        try
        {
            g.playOpeningOnNewGame=false;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);
            for(int i=0;i<HabitatLevels.Names.Length;i++){HabitatSaveValidation.Validate(HabitatLevels.Create(i));Check(true,"valid initial map "+i);}
            g.LoadLevel(6);g.Hud.ClosePanels();g.State.chapterOneDelivered=true;g.State.coins=200;g.State.playerX=g.State.dockX;g.State.playerZ=g.State.dockZ;
            g.Buy("fan");Check(g.State.coins==0&&g.State.carryingProp==PropKind.Fan,"normal purchase creates retry checkpoint after payment");
            g.StoreInventoryItem();Check(g.State.bagProps.Contains(PropKind.Fan),"fan stored through real inventory action");
            g.RestartCurrentLevel();Check(g.State.coins==0&&Fans()==1&&g.State.carryingProp==PropKind.Fan,"restart restores one paid fan and paid balance");
            bool placed=false;for(int z=0;z<g.State.depth&&!placed;z++)for(int x=0;x<g.State.width&&!placed;x++)
            {var test=HabitatRules.Clone(g.State);if(HabitatRules.Interact(test,x,z)&&test.carryingProp==PropKind.None){g.Select(x,z);g.tool=0;g.ApplyTool();placed=true;}}
            Check(placed&&Fans()==1&&g.State.carryingProp==PropKind.None,"paid fan deployed through real tool action");
            g.RestartCurrentLevel();Check(g.State.coins==0&&Fans()==1&&g.State.carryingProp==PropKind.Fan,"deployed fan recovered without duplication");
            g.RestartCurrentLevel();Check(Fans()==1&&g.State.coins==0,"repeated restart cannot duplicate fan or refund payment");
            Check(g.SaveSession(1),"save retry checkpoint into isolated manual slot");
            g.State.coins=17;Check(g.SaveSession(1),"second manual save creates previous backup");
            string file=Path.Combine(folder,"slot-1.json");File.WriteAllText(file,"{broken");
            Check(g.LoadSession(1)&&g.State.coins==0&&Fans()==1,"corrupt main save loads validated backup");
            Check(File.ReadAllText(file)=="{broken","backup recovery preserves damaged main for diagnosis");
            File.WriteAllText(file+".bak","{broken too");var before=g.State;
            Check(!g.LoadSession(1)&&ReferenceEquals(before,g.State),"two damaged files do not replace live state");
            string good=JsonUtility.ToJson(Call("CaptureSnapshot"));
            foreach(string invalid in new[]{"tile","animal","dimension","cache"})
            {
                var j=JObject.Parse(good);
                if(invalid=="tile")j["state"]["tiles"][0]=null;
                if(invalid=="animal")j["state"]["animals"][0]["x"]=999;
                if(invalid=="dimension")j["state"]["width"]=int.MaxValue;
                if(invalid=="cache")j["visitedLevels"]=new JArray(new JObject{{"index",6},{"actions",0},{"state",JValue.CreateNull()}});
                File.WriteAllText(file,j.ToString());before=g.State;
                Check(!g.LoadSession(1)&&ReferenceEquals(before,g.State),"invalid "+invalid+" rejected before state replacement");
            }
            File.WriteAllText(file,good);Check(g.LoadSession(1),"valid save restores after malformed attempts");
            g.RestartCurrentLevel();Check(Fans()==1&&g.State.coins==0,"retry survives save/load");
            g.paused=false;float elapsed=g.State.elapsed;Call("SetBackgroundSuspended",true);await Frames(20);
            Check(g.State.elapsed==elapsed&&g.BackgroundSuspended,"background suspends ecological time");
            Call("SetBackgroundSuspended",false);Check(!g.paused&&!g.BackgroundSuspended,"foreground preserves prior running choice");g.paused=true;
            Call("SetBackgroundSuspended",true);Call("SetBackgroundSuspended",false);Check(g.paused,"foreground preserves prior paused choice");
            var same=HabitatRules.Clone(g.State);for(int i=0;i<250;i++)Call("PushHistory",same);Check(g.UndoCount==200,"undo history bounded at 200");
            Call("CaptureRetryPoint");await Frames(5);Check(File.Exists(Path.Combine(folder,"autosave.json")),"independent automatic save written at stable checkpoint");
            Check(File.ReadAllText(file)==good,"automatic save does not overwrite manual slot");
            string third=Path.Combine(folder,"slot-3.json");File.WriteAllText(third+".bak",good);if(File.Exists(third))File.Delete(third);
            var info=g.GetSaveSlotInfo(3);Check(info.exists&&info.readable&&!info.savedAt.StartsWith("1601"),"backup-only slot has valid date and metadata");
            Check(g.LoadSession(3),"backup-only slot can load");
            Check(g.DeleteSession(3)&&!File.Exists(third+".bak"),"backup-only slot can be deleted explicitly");
            Check(!g.GetSaveSlotInfo(3).exists,"deleted backup does not reappear");
            return "PASS "+count+" optimization safety checks";
        }
        catch(Exception e){File.AppendAllText(Dir+"/safety-runtime.txt","FAIL "+e+"\n");throw;}
        finally
        {
            Call("SetBackgroundSuspended",false);g.CancelPendingAction();Call("RestoreSnapshot",original);g.playOpeningOnNewGame=opening;
            typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);field.SetValue(null,oldFolder);
        }
    }
}

