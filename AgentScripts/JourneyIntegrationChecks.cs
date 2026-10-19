using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Run serially in Play after the main map's natural opening has finished.
// Exercises the real Update loop; it never writes a save file or any asset.
public static class JourneyIntegrationChecks
{
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly StringBuilder report=new StringBuilder();
    static int assertions;
    static T Get<T>(object o,string field)=>(T)o.GetType().GetField(field,Fields).GetValue(o);
    static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,Fields).Invoke(o,args);
    static Dictionary<FieldInfo,object> Scalars(object o)=>o.GetType().GetFields(Fields)
        .Where(f=>!f.IsInitOnly&&!f.IsStatic&&(f.FieldType.IsPrimitive||f.FieldType.IsEnum||f.FieldType==typeof(string)))
        .ToDictionary(f=>f,f=>f.GetValue(o));
    static void RestoreScalars(object o,Dictionary<FieldInfo,object> saved)
    {foreach(var entry in saved)entry.Key.SetValue(o,entry.Value);}
    static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}
    static void Check(bool condition,string label)
    {
        report.AppendLine((condition?"PASS ":"FAIL ")+(++assertions)+": "+label);
        Require(condition,label);
    }
    static string Json(object o)=>JsonUtility.ToJson(o);
    static string Gameplay(HabitatState s)
    {var c=HabitatRules.Clone(s);c.message="";c.events.Clear();return Json(c);}
    [Serializable] sealed class MapData {public int width,depth;public Tile[] tiles;public List<Animal> animals;}
    static string Map(HabitatState s)=>Json(new MapData{width=s.width,depth=s.depth,tiles=s.tiles,animals=s.animals});
    static string Shared(HabitatState s)
    {var c=new HabitatState();HabitatJourneyRules.CopySharedProgress(s,c);return Json(c);}
    static async Task Frames(int count)
    {
        int start=Time.frameCount;double deadline=UnityEditor.EditorApplication.timeSinceStartup+8;
        while(Time.frameCount-start<count)
        {
            Require(Application.isPlaying&&UnityEditor.EditorApplication.timeSinceStartup<deadline,"Unity did not advance real Play frames.");
            await Task.Delay(12);
        }
    }
    static async Task Seconds(float seconds)
    {float start=Time.unscaledTime;while(Time.unscaledTime-start<seconds)await Frames(1);}
    static async Task Press(Keyboard keyboard,params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));await Frames(2);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Frames(1);
    }
    static async Task Place(HabitatGame game,Vector3 point)
    {
        game.Hud.ClosePanels();Require(game.View.Harbor.TryPlaceAt(game.State,point),"Harbor fixture placement failed.");
        await Frames(2);
    }
    static object Roundtrip(object snapshot)=>JsonUtility.FromJson(Json(snapshot),snapshot.GetType());

    public static async Task<string> Run()
    {
        assertions=0;report.Clear();
        var game=HabitatGame.Instance;
        Require(Application.isPlaying&&game&&game.State!=null&&game.State.levelIndex==HabitatFigJamLevel.Index,
            "Run in the main map's Play session.");
        Require(game.State.openingCompleted&&!game.Busy&&!game.Hud.ModalOpen&&!game.Fishing.IsOpen&&game.Story,
            "Finish the opening and close all panels before running journey checks.");
        Require(game.View.Harbor.Ready,"Imported main harbor is not ready.");
        Require(game.GetType().GetMethod("RestoreSnapshot",Fields)!=null,"RestoreSnapshot must be compiled first.");
        HabitatState original=game.State;string originalJson=Json(original);
        Tuning originalTuning=game.tuning;
        var history=Get<Stack<HabitatState>>(game,"history");var undo=history.ToArray();
        var visits=Get<List<HabitatGame.VisitedLevel>>(game,"visitedLevels");var originalVisits=visits.ToArray();
        string visitedJson=string.Join("|",originalVisits.Select(v=>v.index+":"+v.actions+":"+Json(v.state)));
        var gameScalars=Scalars(game);var hudScalars=Scalars(game.Hud);
        var uiActive=game.Hud.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.gameObject,t=>t.gameObject.activeSelf);
        string[] cameraFields={"cameraYaw","cameraSize","cameraTarget","cameraTargetSmooth","followPlayer","overviewCamera","overviewSize","framingAspect","framingDirty"};
        var camera=cameraFields.ToDictionary(n=>n,n=>typeof(HabitatView).GetField(n,Fields).GetValue(game.View));
        Vector3 cameraPosition=game.View.GameCamera.transform.position;Quaternion cameraRotation=game.View.GameCamera.transform.rotation;
        float cameraSize=game.View.GameCamera.orthographicSize;
        GameObject selectedUi=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
        var oldKeyboard=Keyboard.current;var oldMouse=Mouse.current;Keyboard keyboard=null;Mouse mouse=null;
        var background=InputSystem.settings.backgroundBehavior;var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        Exception failure=null;
        try
        {
            keyboard=InputSystem.AddDevice<Keyboard>("Journey acceptance keyboard");keyboard.MakeCurrent();
            mouse=InputSystem.AddDevice<Mouse>("Journey acceptance mouse");mouse.MakeCurrent();
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
            game.CancelPendingAction();game.Hud.ClosePanels();history.Clear();visits.Clear();
            game.tuning=JsonUtility.FromJson<Tuning>(Json(originalTuning));game.tuning.animalMovement=false;
            game.State=HabitatRules.Clone(original);game.State.day=4;game.State.openingCompleted=game.State.openingCatchGranted=true;
            game.State.fish=3;game.State.shrimp=2;game.State.coins=100;
            game.State.deliveredFish=game.State.deliveredShrimp=game.State.deliveryCount=game.State.deliveryIncome=0;
            game.State.carryingAnimal=-1;game.State.carryingProp=PropKind.None;game.State.onHarbor=false;
            foreach(var animal in game.State.animals)animal.carried=false;
            game.State.tiles[0].rotation=117;game.paused=true;game.ActionCount=0;game.tool=0;
            game.View.Initialize(game.State);await Frames(2);
            string before=Gameplay(game.State);
            game.RestAtHome();
            Check(!game.Busy&&Gameplay(game.State)==before&&game.UndoCount==0,"rest away from home is rejected without changing gameplay");
            game.SailToLevel(1);
            Check(game.State.levelIndex==HabitatFigJamLevel.Index&&visits.Count==0,"sailing away from the boat is rejected");
            game.State.carryingProp=PropKind.Crate;before=Gameplay(game.State);game.ReturnToHarbor();
            bool propReturnRejected=Gameplay(game.State)==before&&game.UndoCount==0;
            game.State.carryingProp=PropKind.None;game.State.carryingAnimal=99;before=Gameplay(game.State);game.ReturnToHarbor();
            Check(propReturnRejected&&Gameplay(game.State)==before&&game.UndoCount==0,
                "shortcut harbor return refuses both carried props and animals while retaining their position and hand state");
            game.State.carryingAnimal=-1;

            await Place(game,HabitatImportedDockLayout.HomeApproach);
            await Press(keyboard,Key.E);
            Check(game.Hud.HomeOpen,"actual E input beside home opens the rest panel");
            string mapBefore=Map(game.State),sharedBefore=Shared(game.State);
            game.RestAtHome();
            Check(game.StoryPlaying&&game.Busy&&!game.Hud.ModalOpen&&game.State.day==4&&game.UndoCount==0,
                "morning starts without committing the next day or an undo entry");
            game.Story.TogglePause();await Frames(3);
            before=Gameplay(game.State);Vector3 pausedCamera=game.View.GameCamera.transform.position;
            Quaternion pausedRotation=game.View.GameCamera.transform.rotation;float yaw=Get<float>(game.View,"cameraYaw");
            await Seconds(.25f);
            Check(game.Story.IsPaused&&Gameplay(game.State)==before&&Vector3.Distance(pausedCamera,game.View.GameCamera.transform.position)<.001f
                &&Quaternion.Angle(pausedRotation,game.View.GameCamera.transform.rotation)<.01f,"paused morning keeps state and cinematic camera stable on real frames");
            game.Move(1,0);game.Pull(0,1);game.ApplyTool();game.Buy("sell");game.Undo();game.Step();game.RotateTarget();
            game.Select(0,0);game.TogglePause();game.CycleSpeed();game.RestAtHome();game.ReturnToHarbor();game.SailToLevel(1);
            game.Hud.ToggleShop();game.Hud.ToggleSettings();game.Hud.OpenHome();game.Hud.OpenMapSelection();
            Check(Gameplay(game.State)==before&&game.Story.IsPaused&&!game.Hud.ModalOpen&&game.UndoCount==0&&game.ActionCount==0,
                "story blocks movement, tools, economy, undo, simulation, rest, travel and modal UI");
            int tool=game.tool;await Press(keyboard,Key.W,Key.Q,Key.Digit3,Key.E,Key.Z,Key.Tab);
            Check(Gameplay(game.State)==before&&game.tool==tool&&Mathf.Abs(Get<float>(game.View,"cameraYaw")-yaw)<.001f&&!game.Hud.ModalOpen,
                "real movement, camera, tool and UI hotkeys cannot leak through paused story input");
            game.Story.TogglePause();double morningDeadline=UnityEditor.EditorApplication.timeSinceStartup+9;
            while(game.StoryPlaying){await Frames(1);Require(UnityEditor.EditorApplication.timeSinceStartup<morningDeadline,"Morning did not finish naturally.");}
            Check(game.State.day==5&&game.UndoCount==1&&game.ActionCount==1,"natural morning completes exactly one day and one undo action");
            var expected=HabitatRules.Clone(game.State);expected.day=4;
            Check(Map(game.State)==mapBefore&&Shared(expected)==sharedBefore,"natural morning preserves all map cells, wildlife and inventory");
            Check(game.paused&&!game.Busy,"morning returns to the prior paused gameplay state");
            game.RestAtHome();await Frames(2);game.Story.Skip();await Frames(2);
            Check(game.State.day==6&&!game.Busy&&game.UndoCount==2&&Map(game.State)==mapBefore,"skipping another morning advances only its single day and preserves terrain");
            string afterSkip=Gameplay(game.State);game.Story.Skip();game.Story.Skip();await Frames(2);
            Check(Gameplay(game.State)==afterSkip&&game.UndoCount==2,"repeated Skip after completion cannot add days or undo entries");

            await Place(game,HabitatImportedDockLayout.ShopApproach);await Press(keyboard,Key.E);
            Check(game.Hud.ModalOpen&&!game.Hud.HomeOpen,"actual E input at the shop opens its panel");
            game.Hud.ClosePanels();int saleUndo=game.UndoCount;game.Buy("sell");
            Check(game.State.coins==134&&game.State.fish==0&&game.State.shrimp==0&&game.State.deliveryIncome==34
                &&game.State.deliveredFish==3&&game.State.deliveredShrimp==2&&game.State.deliveryCount==1&&game.UndoCount==saleUndo+1,
                "shop sells actual 3 fish and 2 shrimp for 34 and records one delivery");
            game.Buy("sell");
            Check(game.State.coins==134&&game.State.deliveryCount==1&&game.UndoCount==saleUndo+1,"empty repeated sale adds neither coins nor delivery nor undo");
            game.Undo();await Frames(2);
            Check(game.State.coins==100&&game.State.fish==3&&game.State.shrimp==2&&game.State.deliveryIncome==0
                &&game.State.deliveryCount==0&&game.State.deliveredFish==0&&game.State.deliveredShrimp==0,"sale Undo restores inventory and all delivery totals");
            game.Buy("sell");await Place(game,HabitatImportedDockLayout.BoatApproach);
            game.State.carryingProp=PropKind.Crate;game.SailToLevel(1);
            Check(game.State.levelIndex==HabitatFigJamLevel.Index&&visits.Count==0,"boat refuses departure with a carried prop");
            game.State.carryingProp=PropKind.None;game.State.carryingAnimal=99;game.SailToLevel(1);
            Check(game.State.levelIndex==HabitatFigJamLevel.Index&&visits.Count==0,"boat refuses departure with a carried animal");
            game.State.carryingAnimal=-1;string sharedTravel=Shared(game.State);
            game.SailToLevel(1);await Frames(3);
            Check(game.State.levelIndex==1&&Shared(game.State)==sharedTravel&&game.UndoCount==0&&game.HasVisitedLevel(HabitatFigJamLevel.Index),
                "boat reaches destination with shared progress and clears cross-map undo history");
            Check(!game.StoryPlaying&&!game.BeginOpening()&&game.State.fish==0&&game.State.shrimp==0,"destination does not replay opening or regenerate sold catch");
            game.State.tiles[0].rotation=119;game.ReturnToHarbor();await Frames(2);
            Check(game.View.Harbor.NearBoat(game.State)&&game.State.levelIndex==1&&game.State.tiles[0].rotation==119,"return command reaches the current map's boat without resetting its map");
            game.SailToLevel(HabitatFigJamLevel.Index);await Frames(2);
            Check(game.State.levelIndex==HabitatFigJamLevel.Index&&game.State.tiles[0].rotation==117&&Shared(game.State)==sharedTravel,
                "sailing back restores original map marker while preserving current shared inventory");
            game.ReturnToHarbor();game.SailToLevel(1);await Frames(2);
            Check(game.State.tiles[0].rotation==119&&Shared(game.State)==sharedTravel&&game.State.fish==0&&game.State.shrimp==0,
                "revisiting destination retains its marker and cannot revive sold inventory");

            object saved=Roundtrip(Call(game,"CaptureSnapshot"));string savedShared=Shared(game.State);
            game.State.day=99;game.State.fish=88;game.State.tiles[0].rotation=0;visits.Clear();
            Call(game,"RestoreSnapshot",saved);await Frames(2);
            Check(game.State.levelIndex==1&&Shared(game.State)==savedShared&&game.State.tiles[0].rotation==119&&game.HasVisitedLevel(HabitatFigJamLevel.Index),
                "in-memory snapshot JSON roundtrip restores day, inventory, current map and visited map list");
            game.ReturnToHarbor();game.SailToLevel(HabitatFigJamLevel.Index);await Frames(2);
            Check(game.State.tiles[0].rotation==117&&Shared(game.State)==savedShared,"snapshot roundtrip retains the other map's actual cell contents");

            await Place(game,HabitatImportedDockLayout.HomeApproach);
            object cancelSave=Roundtrip(Call(game,"CaptureSnapshot"));int savedDay=game.State.day;
            game.RestAtHome();await Frames(2);Require(game.StoryPlaying,"Load cancellation fixture did not start morning.");
            Call(game,"RestoreSnapshot",cancelSave);await Frames(2);
            Check(!game.StoryPlaying&&!game.Busy&&game.State.day==savedDay&&game.paused,"restoring a snapshot cancels an in-progress story without granting its next day");
            await Seconds(5.5f);game.Story.Skip();await Frames(2);
            Check(game.State.day==savedDay&&Shared(game.State)==savedShared&&game.UndoCount==0,"cancelled story cannot complete late or add a day through Skip");

            var legacy=HabitatRules.Clone(game.State);legacy.fish=7;legacy.shrimp=4;
            string legacyState=Regex.Replace(Json(legacy),",?\"(?:day|openingCompleted|openingCatchGranted|deliveredFish|deliveredShrimp|deliveryCount|deliveryIncome)\":[^,}]+","");
            string legacyJson="{\"version\":\"HabitatIsland/1\",\"state\":"+legacyState+",\"tuning\":"+Json(game.tuning)+",\"actions\":3}";
            object legacySnapshot=JsonUtility.FromJson(legacyJson,saved.GetType());Call(game,"RestoreSnapshot",legacySnapshot);await Frames(3);
            Check(game.State.day==1&&game.State.fish==7&&game.State.shrimp==4&&!game.State.openingCatchGranted&&!game.StoryPlaying,
                "legacy snapshot without journey fields normalizes day one without opening gifts");
            Check(visits.Count==0&&game.State.deliveryCount==0&&game.State.deliveryIncome==0,"legacy snapshot without visited maps or deliveries loads cleanly");
        }
        catch(Exception exception){failure=exception;report.AppendLine("ERROR: "+exception);}
        finally
        {
            // Cancel before swapping State, so no late story/movement completion can overwrite it.
            game.CancelPendingAction();game.Hud.ClosePanels();game.State=original;game.tuning=originalTuning;
            history.Clear();for(int i=undo.Length-1;i>=0;i--)history.Push(undo[i]);
            visits.Clear();visits.AddRange(originalVisits);RestoreScalars(game,gameScalars);
            game.View.Initialize(original);
            foreach(var entry in camera)typeof(HabitatView).GetField(entry.Key,Fields).SetValue(game.View,entry.Value);
            game.View.GameCamera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);game.View.GameCamera.orthographicSize=cameraSize;
            typeof(HabitatHud).GetField("nextRefresh",Fields).SetValue(game.Hud,0f);game.Hud.Refresh();
            foreach(var entry in uiActive)if(entry.Key)entry.Key.SetActive(entry.Value);RestoreScalars(game.Hud,hudScalars);
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(selectedUi);
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            oldKeyboard?.MakeCurrent();oldMouse?.MakeCurrent();
            InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            bool restored=ReferenceEquals(game.State,original)&&Json(original)==originalJson&&ReferenceEquals(game.tuning,originalTuning)
                &&history.SequenceEqual(undo)&&visits.SequenceEqual(originalVisits)
                &&string.Join("|",visits.Select(v=>v.index+":"+v.actions+":"+Json(v.state)))==visitedJson&&!game.Busy
                &&game.ActionCount==(int)gameScalars[typeof(HabitatGame).GetField("ActionCount",Fields)]
                &&game.paused==(bool)gameScalars[typeof(HabitatGame).GetField("paused",Fields)];
            report.AppendLine((restored?"PASS":"FAIL")+": original State reference, tuning, undo, visited maps, actions, pause, camera, UI and input restored; no save written.");
            if(!restored&&failure==null)failure=new InvalidOperationException("Original runtime state was not completely restored.");
        }
        report.AppendLine((failure==null?"PASS":"FAIL")+": "+assertions+" journey assertions on actual Play frames.");
        if(failure!=null)throw new InvalidOperationException(report.ToString(),failure);
        return report.ToString();
    }
}
