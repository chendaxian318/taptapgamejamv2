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

public static class OptimizationInputChecks
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const string Dir="Verification/usability-audit-20261018";
    static HabitatGame g;static Keyboard keyboard;static Mouse mouse;static StringBuilder log;static int count;
    static object Call(string n,params object[] a)=>typeof(HabitatGame).GetMethod(n,F).Invoke(g,a);
    static void Check(bool okay,string message){if(!okay)throw new Exception(message);log.AppendLine("PASS "+(++count)+" "+message);File.WriteAllText(Dir+"/input-runtime.txt",log.ToString());}
    static async Task Frame(){int frame=Time.frameCount;while(Time.frameCount==frame)await Task.Delay(4);}
    static async Task Wait(Func<bool> test,string message){float deadline=Time.realtimeSinceStartup+8;while(!test()&&Time.realtimeSinceStartup<deadline)await Frame();Check(test(),message);}
    static async Task SendKeys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));await Frame();await Frame();}
    static async Task Setup()
    {
        g.CancelPendingAction();g.Hud.ClosePanels();g.LoadLevel(1);g.paused=true;g.tool=0;
        g.State=new HabitatState{levelIndex=1,width=7,depth=7,tiles=Enumerable.Range(0,49).Select(_=>new Tile{height=1}).ToArray(),playerX=3,playerZ=3,dockX=0,dockZ=0,facingZ=1,rod=true,specialRod=true,openingCompleted=true};
        g.View.Initialize(g.State);Call("CaptureRetryPoint");await SendKeys();g.Hud.ClosePanels();await Frame();
    }
    public static async Task<string> Run()
    {
        g=HabitatGame.Instance;if(!Application.isPlaying||!g||g.Busy)throw new Exception("idle play required");
        Directory.CreateDirectory(Dir);log=new StringBuilder("Queued real InputSystem tests on controlled fixtures. UI design unchanged.\n");count=0;
        var original=Call("CaptureSnapshot");bool session=g.SessionStarted;var oldKeyboard=Keyboard.current;var oldMouse=Mouse.current;
        var settings=InputSystem.settings.backgroundBehavior;var editorSettings=InputSystem.settings.editorInputBehaviorInPlayMode;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;var saveField=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);var saveDir=saveField.GetValue(null);
        saveField.SetValue(null,Path.GetFullPath(Dir+"/input-saves"));InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        keyboard=InputSystem.AddDevice<Keyboard>("OptimizationQAKeyboard");keyboard.MakeCurrent();mouse=InputSystem.AddDevice<Mouse>("OptimizationQAMouse");mouse.MakeCurrent();
        typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);
        try
        {
            await Setup();g.Move(0,1);Check(g.Busy&&g.State.playerZ==3,"movement authority waits for arrival");
            await Wait(()=>g.View.PlayerWorldPosition.z>3.78f,"reached movement tail");await SendKeys(Key.D);await SendKeys();
            await Wait(()=>!g.Busy&&g.State.playerX==4&&g.State.playerZ==4,"short tail input executes exactly one next grid step");
            Check(g.ActionCount==2,"buffered sequence commits twice, not a long queue");
            await Setup();g.Move(0,1);await SendKeys(Key.D);await SendKeys();await Wait(()=>!g.Busy,"first move after early tap finishes");await Frame();
            Check(g.State.playerX==3&&g.State.playerZ==4,"expired early tap does not cause unexpected later movement");
            await Setup();await SendKeys(Key.Digit6);Check(g.tool==5,"key6 selects shovel");await SendKeys(Key.Digit7);Check(g.tool==6,"key7 selects magnet");await SendKeys(Key.Digit8);Check(g.tool==7,"key8 selects launcher without consuming it");await SendKeys();
            await Setup();var before=g.State;await SendKeys(Key.Backspace);await SendKeys();Check(ReferenceEquals(g.State,before),"short Backspace does not restart");
            g.State.coins=52;Call("CaptureRetryPoint");g.State.coins=53;await SendKeys(Key.Backspace);
            await Wait(()=>g.State.coins==52,"held Backspace restores checkpoint");await SendKeys();Check(!g.Busy,"restart leaves no pending action");
            await Setup();g.Move(0,1);await SendKeys(Key.Escape);await SendKeys();await Wait(()=>!g.Busy&&g.Hud.ModalOpen,"Escape during action opens existing pause after arrival");g.Hud.ClosePanels();
            await Setup();HabitatRules.TileAt(g.State,3,4).prop=PropKind.Crate;g.View.Initialize(g.State);g.Select(3,4);await Frame();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(-500,-500)});await Frame();await Frame();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(-500,-500),buttons=2});await Frame();await Frame();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(-500,-500)});await Frame();
            Check(g.State.carryingProp==PropKind.None&&HabitatRules.TileAt(g.State,3,4).prop==PropKind.Crate,"right click outside board does not operate previous target");
            await Setup();Quaternion beforeRotation=g.View.GameCamera.transform.rotation;g.View.RotateCamera(90);
            var intended=HabitatGame.ScreenDirectionToGrid(0,1,g.View.InputCameraRotation);await Frame();
            Check(Quaternion.Angle(beforeRotation,g.View.GameCamera.transform.rotation)<89,"camera rotation is eased");
            Check(intended==HabitatGame.ScreenDirectionToGrid(0,1,g.View.InputCameraRotation),"grid mapping uses stable destination orientation during easing");
            g.LoadLevel(5);g.Hud.ClosePanels();
            var old=JObject.Parse(File.ReadAllText("Verification/latest-plan-20261018/full-campaign-first.json"));
            g.State=old["state"].ToObject<HabitatState>();g.View.Initialize(g.State);g.paused=true;
            var fish=g.State.animals.First(a=>a.species==Species.Goldfish);Check(g.Fishing.Begin(g,fish.x,fish.z),"valid completed-island fixture opens fishing");
            float bar=g.Fishing.Bar,progress=g.Fishing.Progress;Call("SetBackgroundSuspended",true);g.Fishing.Advance(1,true);await Frame();
            Check(g.Fishing.Bar==bar&&g.Fishing.Progress==progress&&g.Fishing.IsOpen,"background freezes fishing input and progress");
            Call("SetBackgroundSuspended",false);g.Fishing.Cancel(false);
            await Setup();g.State.playerX=3;g.State.playerZ=3;HabitatRules.TileAt(g.State,3,4).height=0;HabitatRules.TileAt(g.State,3,5).height=0;
            g.State.animals.Add(new Animal{id=1,x=3,z=5,homeX=3,homeZ=5,species=Species.Jellyfish});g.View.Initialize(g.State);g.Select(3,5);g.tool=2;await Frame();
            g.ApplyTool();Check(g.Busy&&g.State.animals[0].z==5,"lure previews without changing authoritative animal position");
            g.ApplyTool();Check(g.ActionCount==0,"repeated lure blocked while visual movement is pending");
            g.CancelPendingAction();Check(g.State.animals[0].z==5&&g.ActionCount==0,"cancel lure preserves original animal and count");
            g.Select(3,5);g.ApplyTool();await Wait(()=>!g.Busy,"lure arrives");Check(g.State.animals[0].z==4&&g.ActionCount==1,"lure commits once at visual arrival");
            g.Undo();Check(g.State.animals[0].z==5,"lure undo restores original resident position");
            return "PASS "+count+" input/focus/lure checks";
        }
        catch(Exception e){File.AppendAllText(Dir+"/input-runtime.txt","FAIL "+e+"\n");throw;}
        finally
        {
            Call("SetBackgroundSuspended",false);g.Fishing.Cancel(false);g.CancelPendingAction();g.Hud.ClosePanels();Call("RestoreSnapshot",original);
            typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);saveField.SetValue(null,saveDir);
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);oldKeyboard?.MakeCurrent();oldMouse?.MakeCurrent();InputSystem.settings.backgroundBehavior=settings;InputSystem.settings.editorInputBehaviorInPlayMode=editorSettings;
        }
    }
}





