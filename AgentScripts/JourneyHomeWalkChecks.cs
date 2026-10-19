using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Actual frame-by-frame collision walk on the live imported main-map dock.
// Only the starting shop position is installed; every route segment uses MoveWorld.
public static class JourneyHomeWalkChecks
{
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly StringBuilder report=new StringBuilder();
    static int assertions,samples;
    static T Get<T>(object o,string field)=>(T)o.GetType().GetField(field,Fields).GetValue(o);
    static Dictionary<FieldInfo,object> Scalars(object o)=>o.GetType().GetFields(Fields)
        .Where(f=>!f.IsInitOnly&&!f.IsStatic&&(f.FieldType.IsPrimitive||f.FieldType.IsEnum||f.FieldType==typeof(string)))
        .ToDictionary(f=>f,f=>f.GetValue(o));
    static void RestoreScalars(object o,Dictionary<FieldInfo,object> saved)
    {foreach(var e in saved)e.Key.SetValue(o,e.Value);}
    static void Require(bool okay,string label){if(!okay)throw new InvalidOperationException(label);}
    static void Check(bool okay,string label)
    {report.AppendLine((okay?"PASS ":"FAIL ")+(++assertions)+": "+label);Require(okay,label);}
    static async Task Frames(int count)
    {
        int first=Time.frameCount;double deadline=UnityEditor.EditorApplication.timeSinceStartup+8;
        while(Time.frameCount-first<count)
        {Require(Application.isPlaying&&UnityEditor.EditorApplication.timeSinceStartup<deadline,"Play frames stopped.");await Task.Delay(12);}
    }
    static Vector3 Flat(Vector3 v)=>new Vector3(v.x,0,v.z);
    static void Supported(HabitatGame game,Transform model,MeshCollider[] surfaces,string label)
    {
        Vector3 feet=game.View.Harbor.WorldPosition(game.State),local=model.InverseTransformPoint(feet);
        Require(game.State.onHarbor&&game.View.Harbor.IsWalkable(feet),"Left the walkable deck: "+label);
        // Direct mesh rays independently verify that the resolved foot point has physical support.
        Ray ray=new Ray(model.TransformPoint(new Vector3(local.x,.45f,local.z)),Vector3.down);
        bool found=false;float closest=float.PositiveInfinity;Vector3 ground=Vector3.zero;
        foreach(var surface in surfaces)
        {
            if(!surface||!surface.enabled||!surface.gameObject.activeInHierarchy||!surface.sharedMesh)continue;
            if(surface.Raycast(ray,out RaycastHit hit,.8f*Mathf.Abs(model.lossyScale.y))&&hit.normal.y>.5f&&hit.distance<closest)
            {found=true;closest=hit.distance;ground=hit.point;}
        }
        Require(found&&Mathf.Abs(feet.y-ground.y-.055f)<.008f,"Feet lost support from the actual imported mesh: "+label);
        Require(Vector3.Distance(feet,game.View.PlayerWorldPosition)<.015f,"Rendered player does not match the supported harbor position: "+label);
    }
    static async Task Walk(HabitatGame game,Vector3 normalized,Transform model,MeshCollider[] surfaces,string label)
    {
        Vector3 goal=model.TransformPoint(normalized);int stalled=0,steps=0;
        while(true)
        {
            Vector3 before=game.View.Harbor.WorldPosition(game.State),delta=Flat(goal-before);
            if(delta.magnitude<.025f)break;
            Require(++steps<900,"Walk sample limit exceeded: "+label);
            float dt=Mathf.Min(Mathf.Max(.001f,Time.unscaledDeltaTime),.04f,delta.magnitude/2.5f);
            game.View.Harbor.MoveWorld(game.State,delta,dt);await Frames(1);samples++;
            Supported(game,model,surfaces,label);
            Vector3 after=game.View.Harbor.WorldPosition(game.State);
            float moved=Flat(after-before).magnitude;
            Require(moved<=2.5f*dt+.003f,"Unexpected teleport during small-step walking: "+label);
            stalled=moved<.0005f?stalled+1:0;
            Require(stalled<24,"HomePath blocked at "+model.InverseTransformPoint(after)+" while approaching "+normalized+" ("+label+")");
        }
        Check(true,label+" reached through "+steps+" supported movement frames");
    }
    static async Task Screenshot(string filename)
    {
        string folder=Path.GetFullPath("Logs/journey-20261015");Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,filename);ScreenCapture.CaptureScreenshot(path);await Frames(3);
        report.AppendLine("SCREENSHOT: "+path);
    }
    public static Task<string> RunWithScreenshots() => Run(true);
    public static async Task<string> Run(bool captureScreenshots=false)
    {
        assertions=samples=0;report.Clear();var game=HabitatGame.Instance;
        Require(Application.isPlaying&&game&&game.State.levelIndex==HabitatFigJamLevel.Index&&game.State.openingCompleted
            &&!game.Busy&&!game.Hud.ModalOpen&&!game.Fishing.IsOpen&&game.View.Harbor.Ready,"Use the idle main map after its opening, with all panels closed.");
        HabitatState original=game.State;string originalJson=JsonUtility.ToJson(original);Tuning tuning=game.tuning;
        var history=Get<Stack<HabitatState>>(game,"history");var undo=history.ToArray();
        var gameScalars=Scalars(game);var hudScalars=Scalars(game.Hud);
        var ui=game.Hud.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.gameObject,t=>t.gameObject.activeSelf);
        string[] cameraFields={"cameraYaw","cameraSize","cameraTarget","cameraTargetSmooth","followPlayer","overviewCamera","overviewSize","framingAspect","framingDirty"};
        var camera=cameraFields.ToDictionary(n=>n,n=>typeof(HabitatView).GetField(n,Fields).GetValue(game.View));
        Vector3 cameraPosition=game.View.GameCamera.transform.position;Quaternion cameraRotation=game.View.GameCamera.transform.rotation;
        float cameraSize=game.View.GameCamera.orthographicSize;
        var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
        var oldKeyboard=Keyboard.current;var oldMouse=Mouse.current;Keyboard keyboard=null;Mouse mouse=null;
        Exception failure=null;
        try
        {
            keyboard=InputSystem.AddDevice<Keyboard>("Home walkway acceptance keyboard");keyboard.MakeCurrent();
            mouse=InputSystem.AddDevice<Mouse>("Home walkway acceptance mouse");mouse.MakeCurrent();
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
            game.State=HabitatRules.Clone(original);game.paused=true;game.tool=0;
            game.State.carryingAnimal=-1;game.State.carryingProp=PropKind.None;
            foreach(var animal in game.State.animals)animal.carried=false;
            game.View.Initialize(game.State);
            Require(game.View.Harbor.TryPlaceAt(game.State,HabitatImportedDockLayout.ShopApproach),"Could not install starting position at shop.");
            game.View.FocusPlayer();await Frames(3);
            Transform model=game.View.Harbor.ModelTransform;var surfaces=model.GetComponentsInChildren<MeshCollider>(true);
            Supported(game,model,surfaces,"shop start");
            Check(game.View.Harbor.NearShop(game.State),"start on supported shop deck");
            game.ApplyTool();await Frames(2);
            Check(game.Hud.ModalOpen&&!game.Hud.HomeOpen,"shop interaction opens from the supported start position");
            if(captureScreenshots)await Screenshot("shop-ui.png");game.Hud.ClosePanels();
            for(int i=1;i<HabitatImportedDockLayout.HomePath.Length;i++)
                await Walk(game,HabitatImportedDockLayout.HomePath[i],model,surfaces,"shop to home segment "+i);
            Check(game.View.Harbor.NearHome(game.State)&&game.View.Harbor.NearestInteraction(game.State)==HabitatDockInteraction.Home,
                "full HomePath reaches the home interaction radius without placement shortcuts");
            game.ApplyTool();await Frames(2);
            Check(game.Hud.HomeOpen,"home panel opens from the actual walked destination");
            if(captureScreenshots)await Screenshot("home-ui.png");game.Hud.ClosePanels();
            if(captureScreenshots)
            {
                game.RestAtHome();Require(game.StoryPlaying,"Morning screenshot could not begin.");
                float began=Time.unscaledTime;while(Time.unscaledTime-began<1f)await Frames(1);
                game.Story.TogglePause();await Frames(2);await Screenshot("morning-story-ui.png");
                game.CancelPendingAction();await Frames(2); // Cancel the screenshot fixture; do not add a day.
            }
            for(int i=HabitatImportedDockLayout.HomePath.Length-2;i>=0;i--)
                await Walk(game,HabitatImportedDockLayout.HomePath[i],model,surfaces,"home to shop segment "+i);
            Check(game.View.Harbor.NearShop(game.State)&&game.View.Harbor.NearestInteraction(game.State)==HabitatDockInteraction.Shop,
                "return walk reaches the shop interaction radius on the same imported deck");
            Check(game.State.day==original.day&&game.State.coins==original.coins&&game.State.fish==original.fish&&game.State.shrimp==original.shrimp
                &&game.UndoCount==undo.Length&&game.ActionCount==(int)gameScalars[typeof(HabitatGame).GetField("ActionCount",Fields)],
                "walking and optional cancelled preview preserve day, inventory and undo/actions");
        }
        catch(Exception error){failure=error;report.AppendLine("ERROR: "+error);}
        finally
        {
            game.CancelPendingAction();game.Hud.ClosePanels();game.State=original;game.tuning=tuning;
            history.Clear();for(int i=undo.Length-1;i>=0;i--)history.Push(undo[i]);RestoreScalars(game,gameScalars);
            game.View.Initialize(original);
            foreach(var entry in camera)typeof(HabitatView).GetField(entry.Key,Fields).SetValue(game.View,entry.Value);
            game.View.GameCamera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);game.View.GameCamera.orthographicSize=cameraSize;
            typeof(HabitatHud).GetField("nextRefresh",Fields).SetValue(game.Hud,0f);game.Hud.Refresh();
            foreach(var entry in ui)if(entry.Key)entry.Key.SetActive(entry.Value);RestoreScalars(game.Hud,hudScalars);
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(selected);
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            oldKeyboard?.MakeCurrent();oldMouse?.MakeCurrent();
            bool restored=ReferenceEquals(game.State,original)&&JsonUtility.ToJson(original)==originalJson&&history.SequenceEqual(undo)&&!game.Busy
                &&game.paused==(bool)gameScalars[typeof(HabitatGame).GetField("paused",Fields)];
            report.AppendLine((restored?"PASS":"FAIL")+": original State reference, history, pause, camera and UI restored; no save or asset written.");
            if(!restored&&failure==null)failure=new InvalidOperationException("Runtime restoration failed.");
        }
        report.AppendLine((failure==null?"PASS":"FAIL")+": "+assertions+" home walkway checks; "+samples+" collision-supported Play frames.");
        if(failure!=null)throw new InvalidOperationException(report.ToString(),failure);
        return report.ToString();
    }
}
