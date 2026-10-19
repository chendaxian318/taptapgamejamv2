using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;
using UnityEngine.UI;
public static class WorkflowAssetChecks
{
    static int checks;static StringBuilder report;static HabitatGame game;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string message){if(!ok)throw new Exception("FAIL "+message);report.AppendLine("PASS "+(++checks)+": "+message);}
    static async Task Frames(int n=3){for(int i=0;i<n;i++)await Task.Yield();}
    static async Task Wait(Func<bool> condition,float seconds=5){float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)await Task.Yield();Check(condition(),"wait completed within "+seconds+" seconds");}
    static HabitatState Board()
    {
        var s=new HabitatState{width=7,depth=7,playerX=2,playerZ=2,facingX=1,facingZ=0,levelIndex=5,authoredLevel=true,levelName="道具验证",objective="验证用场景",dockX=2,dockZ=2,openingCompleted=true,openingCatchGranted=true,chapterOneAccepted=true,chapterOneDelivered=true,chapterOneMorningSeen=true,shovels=2,magnets=2,launchers=2};
        s.tiles=Enumerable.Range(0,49).Select(i=>new Tile{height=1}).ToArray();return s;
    }
    static async Task Install(HabitatState s)
    {
        game.CancelPendingAction();game.Hud.ClosePanels();game.State=s;game.paused=true;game.ActionCount=0;
        ((System.Collections.Generic.Stack<HabitatState>)typeof(HabitatGame).GetField("history",Private).GetValue(game)).Clear();
        game.View.Initialize(s);game.View.Render(s,game.tuning,false);await Frames();
    }
    static void PureChecks()
    {
        var s=Board();s.coins=30;
        Check(HabitatRules.Shop(s,"crate")&&s.boatCrates==1&&s.carryingProp==PropKind.None&&s.coins==24,"formal shop puts purchased crate in boat, not hands");
        Check(HabitatRules.Shop(s,"crate")&&HabitatRules.Shop(s,"crate")&&s.boatCrates==3,"boat holds three crates");
        Check(!HabitatRules.Shop(s,"crate")&&s.coins==12&&s.boatCrates==3,"fourth crate rejected without charge");
        Check(HabitatRules.TransferBoatCrate(s,true)&&s.boatCrates==2&&s.carryingProp==PropKind.Crate,"unload exactly one crate");
        Check(!HabitatRules.TransferBoatCrate(s,true)&&s.boatCrates==2,"occupied hands cannot unload another");
        Check(HabitatRules.TransferBoatCrate(s,false)&&s.boatCrates==3&&s.carryingProp==PropKind.None,"reload held crate without duplication");
        s.carryingProp=PropKind.Fan;Check(HabitatRules.StoreHeld(s)&&s.bagProps.SequenceEqual(new[]{PropKind.Fan}),"store held equipment");
        var clone=HabitatRules.Clone(s);clone.bagProps.Clear();Check(s.bagProps.Count==1&&clone.boatCrates==3&&clone.magnets==2,"clone keeps new inventory with independent list");
        var dest=Board();HabitatJourneyRules.CopySharedProgress(s,dest);dest.bagProps.Clear();Check(s.bagProps.Count==1&&dest.boatCrates==3,"travel copies latest boat and bag without alias");
        Check(HabitatRules.TakeBagProp(s,0)&&s.carryingProp==PropKind.Fan&&s.bagProps.Count==0,"selecting bag item transfers exactly once");
        Check(!HabitatRules.TakeBagProp(s,0),"removed bag entry cannot be reused");
        s=Board();HabitatRules.TileAt(s,3,2).height=2;
        Check(!HabitatRules.UseUtility(s,5,3,3,1,0)&&s.shovels==2,"dig rejects non-forward target without spending");
        Check(HabitatRules.UseUtility(s,5,3,2,1,0)&&s.shovels==1&&HabitatRules.TileAt(s,3,2).height==1,"dig lowers exactly one layer and consumes once");
        Check(!HabitatRules.UseUtility(s,5,3,2,1,0)&&s.shovels==1,"cannot dig equal-height ground");
        s=Board();HabitatRules.TileAt(s,3,3).prop=PropKind.Crate;HabitatRules.TileAt(s,3,4).prop=PropKind.Tide;
        Check(HabitatRules.UseUtility(s,6,3,3,0,1)&&s.magnets==1&&s.tideActive&&HabitatRules.TileAt(s,3,4).coveredProp==PropKind.Tide,"magnet diagonal selection, cardinal move and pressure plate");
        s=Board();HabitatRules.TileAt(s,3,2).prop=PropKind.Crate;HabitatRules.TileAt(s,4,2).height=0;
        Check(HabitatRules.UseUtility(s,7,3,2,1,0)&&HabitatRules.TileAt(s,5,2).prop==PropKind.Crate&&s.launchers==1,"launcher crosses water to two-cell landing");
        s=Board();HabitatRules.TileAt(s,3,2).prop=PropKind.Crate;HabitatRules.TileAt(s,5,2).prop=PropKind.Obstacle;
        Check(HabitatRules.UseUtility(s,7,3,2,1,0)&&HabitatRules.TileAt(s,4,2).prop==PropKind.Crate,"launcher stops before wall at second cell");
        s=Board();HabitatRules.TileAt(s,3,2).prop=PropKind.Crate;HabitatRules.TileAt(s,4,2).prop=PropKind.Obstacle;
        Check(!HabitatRules.UseUtility(s,7,3,2,1,0)&&s.launchers==2&&HabitatRules.TileAt(s,3,2).prop==PropKind.Crate,"adjacent wall rejects without consumption or partial move");
        s=Board();s.onHarbor=true;Check(!HabitatRules.UseUtility(s,6,3,2,1,0),"harbor cannot use island utilities");
    }
    public static async Task<string> Run()
    {
        game=HabitatGame.Instance;checks=0;report=new StringBuilder("Workflow/assets verification: fixtures, not full campaign.\n");
        if(!game||game.Busy)throw new Exception("Requires idle main game");
        var capture=typeof(HabitatGame).GetMethod("CaptureSnapshot",Private);var restore=typeof(HabitatGame).GetMethod("RestoreSnapshot",Private);
        object snapshot=capture.Invoke(game,null);bool oldPause=game.paused;int oldTool=game.tool;
        string savePath=Path.Combine(Application.persistentDataPath,"Playtests","latest.json");byte[] existing=File.Exists(savePath)?File.ReadAllBytes(savePath):null;
        try
        {
            PureChecks();
            foreach(var pair in new[]{("Goldfish",4,12),("Cannon",3,5),("Magnet",0,4)})
            {
                var prefab=Resources.Load<GameObject>("Models/"+pair.Item1+"/"+pair.Item1);Check(prefab,"resource loads: "+pair.Item1);
                var anim=prefab.GetComponent<HabitatEquipmentAnimation>();Check(anim&&anim.Clips.Count==pair.Item2,"source animation count: "+pair.Item1);
                Check(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r=>r.bones).Distinct().Count()==pair.Item3,"source bone count: "+pair.Item1);
                Check(prefab.GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterials.All(m=>m&&m.shader.isSupported)),"valid PBR materials: "+pair.Item1);
                var instance=UnityEngine.Object.Instantiate(prefab);instance.transform.position=Vector3.one*-100;
                try
                {
                    var player=instance.GetComponent<HabitatEquipmentAnimation>();var skin=instance.GetComponentInChildren<SkinnedMeshRenderer>();
                    foreach(var clip in player.Clips.Where(c=>pair.Item1=="Goldfish"||c.name!="Idle"))
                    {
                        var mesh=new Mesh();
                        try
                        {
                            player.Sample(clip.name,0);skin.BakeMesh(mesh,true);var before=mesh.vertices;
                            player.Sample(clip.name,clip.length*.31f);skin.BakeMesh(mesh,true);var after=mesh.vertices;
                            Check(before.Zip(after,(a,b)=>(a-b).sqrMagnitude).Max()>.00000001f,"supplied skin actually deforms: "+pair.Item1+"/"+clip.name);
                        }
                        finally{UnityEngine.Object.Destroy(mesh);}
                    }
                }
                finally{UnityEngine.Object.Destroy(instance);}
            }
            game.Hud.ShowStartMenu();Check(game.Hud.SessionMenuOpen,"start menu opens");
            if(!game.SessionStarted){game.Hud.HandleEscape();Check(game.Hud.SessionMenuOpen,"Escape cannot bypass initial start menu");}
            game.NewSession();Check(game.SessionStarted&&game.State.boatCrates==0&&game.State.bagProps.Count==0,"new game resets added inventory");
            game.CancelPendingAction();game.Hud.ClosePanels();
            var s=Board();await Install(s);game.Hud.HandleEscape();Check(game.Hud.PauseMenuOpen&&game.paused,"Escape opens pause menu");
            game.Hud.HandleEscape();Check(!game.Hud.ModalOpen,"Escape resumes previous game state");
            game.Hud.ToggleInventory();Check(game.Hud.InventoryOpen,"inventory opens");game.SelectInventoryTool(7);Check(!game.Hud.ModalOpen&&game.tool==7,"inventory selects launcher and closes");
            game.SelectInventoryTool(6);await Frames(5);
            Check(game.View.PlayerVisual.EquippedTool=="Magnet"&&game.View.PlayerVisual.PrimaryGripError<.002f,"delivered magnet follows right palm within 2mm");
            s=Board();HabitatRules.TileAt(s,3,2).prop=PropKind.Crate;HabitatRules.TileAt(s,4,2).height=0;await Install(s);game.tool=7;
            game.Hud.OpenUtilityPreview();Check(game.Hud.UtilityPreviewOpen&&s.launchers==2,"preview does not spend inventory");
            Check(GameObject.Find("Utility model preview")!=null,"launcher model visible in preview");game.Hud.ClosePanels();await Frames();
            Check(GameObject.Find("Utility model preview")==null&&s.launchers==2,"preview cancel removes model without spending");
            Check(game.ConfirmUtility(3,2,1,0)&&game.Busy&&game.State==s&&s.launchers==2,"launch keeps source authoritative while animation is pending");
            Check(!game.ConfirmUtility(3,2,1,0),"Busy prevents double launch");await Frames(4);
            Check(game.View.GridStepMovingPropCount==1,"actual box is animated toward landing");
            var animation=(HabitatEquipmentAnimation)typeof(HabitatGame).GetField("utilityAnimation",Private).GetValue(game);
            Check(animation&&animation.CurrentClipName=="Fire"&&!animation.IsLooping,"actual launch plays supplied Fire once");
            game.CancelPendingAction();await Frames();Check(game.State==s&&s.launchers==2&&game.ActionCount==0,"cancel pending launch restores inventory and source");
            Check(game.ConfirmUtility(3,2,1,0),"second deliberate launch begins");await Wait(()=>!game.Busy);
            Check(game.State.launchers==1&&HabitatRules.TileAt(game.State,5,2).prop==PropKind.Crate&&game.ActionCount==1&&game.UndoCount==1,"arrival commits exactly once");
            game.Undo();Check(game.State.launchers==2&&HabitatRules.TileAt(game.State,3,2).prop==PropKind.Crate,"undo restores consumed tool and box together");
            s=Board();HabitatRules.TileAt(s,3,2).height=2;await Install(s);game.tool=5;
            Check(game.ConfirmUtility(3,2,1,0)&&s.shovels==2&&HabitatRules.TileAt(s,3,2).height==2,"dig remains uncommitted during shovel gesture");await Wait(()=>!game.Busy);
            Check(game.State.shovels==1&&HabitatRules.TileAt(game.State,3,2).height==1&&game.ActionCount==1,"dig finishes once after gesture");
            s=Board();HabitatRules.TileAt(s,3,3).prop=PropKind.Crate;await Install(s);game.tool=6;
            Check(game.ConfirmUtility(3,3,0,1)&&s.magnets==2,"magnet waits for actual arrival");await Wait(()=>!game.Busy);
            Check(game.State.magnets==1&&HabitatRules.TileAt(game.State,3,4).prop==PropKind.Crate,"magnet commits chosen direction");
            s=Board();s.boatCrates=3;s.bagProps.Add(PropKind.Fan);await Install(s);
            string json=JsonUtility.ToJson(capture.Invoke(game,null));var type=snapshot.GetType();restore.Invoke(game,new[]{JsonUtility.FromJson(json,type)});
            Check(game.State.boatCrates==3&&game.State.bagProps.Single()==PropKind.Fan&&game.State.magnets==2,"snapshot JSON roundtrip keeps new cargo and consumables");
            json=json.Replace("\"boatCrates\":3,","").Replace("\"shovels\":2,","").Replace("\"magnets\":2,","").Replace("\"launchers\":2,","");
            restore.Invoke(game,new[]{JsonUtility.FromJson(json,type)});Check(game.State.boatCrates==0&&game.State.magnets==0,"legacy save does not grant newly added supplies");
            s=Board();s.boatCrates=3;s.bagProps.Add(PropKind.Fan);await Install(s);
            Check(game.View.Harbor.TryPlaceAt(s,HabitatImportedDockLayout.BoatApproach),"cargo travel fixture reaches real boat approach");
            game.SailToLevel(6);Check(game.Busy&&game.State==s,"real sailing starts without early cargo transfer");
            Check(game.Story.Skip(),"actual departure skip completes once");await Frames(4);
            Check(game.State.levelIndex==6&&game.State.boatCrates==3&&game.State.magnets==2&&game.State.bagProps.Single()==PropKind.Fan,"real sailing preserves new cargo and tools");
            game.RestartCurrentLevel();await Frames();
            Check(game.State.boatCrates==3&&game.State.magnets==2&&game.State.bagProps.Single()==PropKind.Fan,"actual island restart retains new inventory");
            var fishObject=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Models/Goldfish/Goldfish"));
            try
            {
                fishObject.transform.position=Vector3.one*-100;var visual=fishObject.AddComponent<HabitatGoldfishVisual>();
                visual.Render(.1f,Vector3.zero,Vector3.zero,true);Check(visual.CurrentClip=="Idle","fish reset selects supplied idle");
                visual.Render(.1f,Vector3.forward*.03f,Vector3.zero,false);Check(visual.CurrentClip=="Swim","actual visual movement selects swim");
                visual.Render(.1f,Vector3.right*.03f,Vector3.zero,false);Check(visual.CurrentClip=="Turn_Right","rightward heading change selects right turn");
                for(int i=0;i<15;i++)visual.Render(.1f,Vector3.zero,Vector3.zero,false);
                Check(visual.CurrentClip=="Idle","one-shot fish turn returns to idle after stopping");
            }
            finally{UnityEngine.Object.Destroy(fishObject);}
            Check(existing==null?!File.Exists(savePath):existing.SequenceEqual(File.ReadAllBytes(savePath)),"tests and new game did not overwrite user latest save");
            string isolated=Path.GetFullPath("Verification/workflow-assets-20261016/atomic-test.json");
            File.WriteAllText(isolated,"previous");HabitatGame.WriteLatestSave(isolated,json);
            Check(File.ReadAllText(isolated)==json&&File.ReadAllText(isolated+".bak")=="previous","atomic save retains previous file as backup (isolated test path)");
        }
        catch(Exception e){report.AppendLine(e.ToString());throw;}
        finally
        {
            game.CancelPendingAction();restore.Invoke(game,new[]{snapshot});game.paused=oldPause;game.tool=oldTool;
            report.AppendLine("Restored snapshot; no user save written. Full four-stage solutions and final art acceptance are outside this test.");
            Directory.CreateDirectory("Verification/workflow-assets-20261016");File.WriteAllText("Verification/workflow-assets-20261016/checks.txt",report.ToString());
        }
        return report.ToString();
    }
}
