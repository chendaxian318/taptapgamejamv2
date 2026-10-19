#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HabitatIsland.Editor
{
    // Exercises the live input path and camera over rendered frames, never an exported player.
    public class HabitatAuthoredViewChecks : MonoBehaviour
    {
        public static string Status = "Not run";
        HabitatGame game;
        Keyboard keyboard;
        Mouse mouse;
        Keyboard previousKeyboard;
        Mouse previousMouse;
        InputSettings.BackgroundBehavior previousBackground;
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        HabitatState originalState;
        Tuning originalTuning;
        HabitatState[] originalHistory;
        string originalJson;
        Dictionary<FieldInfo,object> gameScalars,hudScalars;
        Dictionary<string,object> cameraFields;
        Dictionary<GameObject,bool> uiActive;
        Vector3 originalCameraPosition;
        Quaternion originalCameraRotation;
        float originalCameraSize;
        bool snapshotCaptured;
        int count;
        readonly StringBuilder report = new StringBuilder();
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        public static string Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play first");
            if (!HabitatGame.Instance || HabitatGame.Instance.Busy || HabitatGame.Instance.Hud.ModalOpen || (HabitatGame.Instance.Fishing && HabitatGame.Instance.Fishing.IsOpen))
                throw new InvalidOperationException("Finish movement/fishing and close any modal before starting checks");
            if (FindAnyObjectByType<HabitatAuthoredViewChecks>()) throw new InvalidOperationException("Checks already running");
            var runner = HabitatGame.Instance.gameObject.AddComponent<HabitatAuthoredViewChecks>();
            runner.StartCoroutine(runner.Guarded());
            return "Live input/camera checks started";
        }
        void Check(bool pass, string label)
        {
            count++;
            if (!pass) throw new InvalidOperationException(label);
            report.AppendLine("PASS " + count + ": " + label);
        }
        bool Flag(string name) { return (bool)typeof(HabitatView).GetField(name, Flags).GetValue(game.View); }
        static Dictionary<FieldInfo,object> Scalars(object target)
        {return target.GetType().GetFields(Flags).Where(f=>!f.IsInitOnly&&!f.IsStatic&&(f.FieldType.IsPrimitive||f.FieldType.IsEnum||f.FieldType==typeof(string))).ToDictionary(f=>f,f=>f.GetValue(target));}
        void SaveRuntime()
        {
            originalState=game.State;originalJson=JsonUtility.ToJson(originalState);originalTuning=game.tuning;
            originalHistory=((Stack<HabitatState>)typeof(HabitatGame).GetField("history",Flags).GetValue(game)).ToArray();
            gameScalars=Scalars(game);hudScalars=Scalars(game.Hud);
            string[] fields={"cameraYaw","cameraSize","cameraTarget","cameraTargetSmooth","followPlayer","overviewCamera","overviewSize","framingAspect","framingDirty"};
            cameraFields=fields.ToDictionary(n=>n,n=>typeof(HabitatView).GetField(n,Flags).GetValue(game.View));
            originalCameraPosition=game.View.GameCamera.transform.position;originalCameraRotation=game.View.GameCamera.transform.rotation;
            originalCameraSize=game.View.GameCamera.orthographicSize;
            uiActive=game.Hud.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.gameObject,t=>t.gameObject.activeSelf);
            previousKeyboard=Keyboard.current;previousMouse=Mouse.current;
            previousBackground=InputSystem.settings.backgroundBehavior;previousEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            snapshotCaptured=true;
        }
        void RestoreRuntime()
        {
            if(!game||!snapshotCaptured)return;
            game.CancelPendingAction();game.Hud.ClosePanels();game.State=originalState;game.tuning=originalTuning;
            var history=(Stack<HabitatState>)typeof(HabitatGame).GetField("history",Flags).GetValue(game);
            history.Clear();for(int i=originalHistory.Length-1;i>=0;i--)history.Push(originalHistory[i]);
            foreach(var field in gameScalars)field.Key.SetValue(game,field.Value);
            game.View.Initialize(originalState);
            foreach(var field in cameraFields)typeof(HabitatView).GetField(field.Key,Flags).SetValue(game.View,field.Value);
            game.View.GameCamera.transform.SetPositionAndRotation(originalCameraPosition,originalCameraRotation);
            game.View.GameCamera.orthographicSize=originalCameraSize;
            typeof(HabitatHud).GetField("nextRefresh",Flags).SetValue(game.Hud,0f);game.Hud.Refresh();
            foreach(var item in uiActive)if(item.Key)item.Key.SetActive(item.Value);
            foreach(var field in hudScalars)field.Key.SetValue(game.Hud,field.Value);
            Check(ReferenceEquals(game.State,originalState)&&JsonUtility.ToJson(game.State)==originalJson&&history.SequenceEqual(originalHistory)&&!game.Busy,
                "original runtime state reference, full history, pause, tools and camera restored after checking");
        }
        IEnumerator WaitForArrival(int x,int z,string label)
        {
            float deadline=Time.realtimeSinceStartup+8f;
            while(game.Busy)
            {
                if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Timed out waiting for "+label);
                yield return null;
            }
            Check(game.State.playerX==x&&game.State.playerZ==z&&game.View.GridStepComplete&&!game.View.GridStepActive,label);
        }
        IEnumerator Walk(int dx,int dz,bool climb=false)
        {
            int x=game.State.playerX+dx,z=game.State.playerZ+dz;
            game.Move(dx,dz,climb);
            Check(game.Busy,"legal authored setup step waits for its visual arrival");
            yield return WaitForArrival(x,z,"authored setup arrives at ("+x+","+z+")");
        }
        IEnumerator KeyPress(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }
        IEnumerator Run()
        {
            game = HabitatGame.Instance;
            SaveRuntime();
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            game.LoadLevel(HabitatFigJamLevel.Index); game.paused = true; game.tool = 0;
            keyboard = InputSystem.AddDevice<Keyboard>("Habitat camera audit keyboard");
            mouse = InputSystem.AddDevice<Mouse>("Habitat camera audit mouse");
            keyboard.MakeCurrent();mouse.MakeCurrent();
            yield return null;
            Check(Screen.width >= 640 && Screen.height >= 360, "preview has usable dimensions for input and camera checks");
            Check(game.paused && Flag("followPlayer"), "formal level starts paused with player follow");
            Check(Mathf.Abs(Mathf.DeltaAngle(game.View.GameCamera.transform.eulerAngles.x, 48)) < .1f && Mathf.Abs(Mathf.DeltaAngle(game.View.GameCamera.transform.eulerAngles.y, -14)) < .1f, "48-degree pitch with slight 14-degree side view");
            yield return KeyPress(Key.M);
            yield return new WaitForSecondsRealtime(.8f);
            Check(Flag("overviewCamera") && !Flag("followPlayer") && game.View.GameCamera.orthographicSize > 10, "M key opens full map");
            yield return KeyPress(Key.C);
            yield return new WaitForSecondsRealtime(.8f);
            Check(Mathf.Abs(Mathf.DeltaAngle(game.View.GameCamera.transform.eulerAngles.y, -104)) < .1f, "C rotates one cardinal quarter turn");
            yield return KeyPress(Key.Home);
            yield return new WaitForSecondsRealtime(.8f);
            Check(Flag("followPlayer") && !Flag("overviewCamera") && Mathf.Abs(game.View.GameCamera.orthographicSize - 6.5f) < .02f, "Home restores local player framing");
            yield return KeyPress(Key.Q);
            yield return new WaitForSecondsRealtime(.8f);
            Check(Mathf.Abs(Mathf.DeltaAngle(game.View.GameCamera.transform.eulerAngles.y, -14)) < .1f, "Q returns camera to initial slight yaw");
            yield return KeyPress(Key.W);
            yield return WaitForArrival(3,11,"W reaches adjacent authored land through HandleInput after visible arrival");
            game.LoadLevel(HabitatFigJamLevel.Index);
            yield return Walk(0,1);yield return Walk(1,0);yield return Walk(0,1);yield return Walk(1,0);
            yield return KeyPress(Key.LeftShift,Key.W);
            Check(!game.Busy&&game.State.playerX==5&&game.State.playerZ==12,
                "Shift+W cannot climb an ordinary one-layer terrain rise");
            yield return Walk(1,0);
            yield return KeyPress(Key.LeftShift,Key.W);
            yield return WaitForArrival(6,13,"Shift+W climbs the original dedicated block across a two-layer rise");
            Check(HabitatRules.TileAt(game.State,6,13).prop==PropKind.ClimbBlock,
                "the dedicated climbing block remains in place after climbing");
            yield return Walk(0,1);
            Check(game.State.playerX == 6 && game.State.playerZ == 14, "walking and the dedicated climb reach the original tide crate");
            yield return KeyPress(Key.LeftShift, Key.D);
            Check(!game.Busy&&game.State.playerX == 6 && game.State.playerZ == 14 && HabitatRules.TileAt(game.State,7,14).prop == PropKind.Crate && !game.State.tideActive,
                "Shift+D cannot climb the original ordinary crate even though its top is only one layer above the player");
            game.LoadLevel(HabitatFigJamLevel.Index);
            game.View.FocusOverview();
            yield return new WaitForSecondsRealtime(.8f);
            // A floating block top has no occluding highland above it; its back edge must still pick itself.
            var world = new Vector3(4, HabitatRules.Height(game.State,4,6)*.7f+.045f, 6.34f);
            Vector3 screen = game.View.GameCamera.WorldToScreenPoint(world);
            int x,z;
            Check(game.View.Pick(screen,out x,out z) && x==4 && z==6, "clicking the back of a float top selects that float");
            Vector3 before = game.View.GameCamera.transform.position;
            var cursor = new Vector2(Screen.width*.5f, Screen.height*.5f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position=cursor });
            yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position=cursor, delta=new Vector2(80,30), buttons=4 });
            yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position=cursor });
            yield return null;
            Check(!Flag("overviewCamera") && !Flag("followPlayer") && Vector3.Distance(before,game.View.GameCamera.transform.position)>.2f, "middle-button drag pans without moving player");
            Check(game.State.playerX==3 && game.State.playerZ==10, "camera drag preserves gameplay position");
            game.Hud.ToggleSettings();
            bool followBefore=Flag("followPlayer");
            float yawBefore=game.View.GameCamera.transform.eulerAngles.y;
            yield return KeyPress(Key.Home, Key.C);
            Check(game.Hud.ModalOpen && Flag("followPlayer")==followBefore && Mathf.Abs(Mathf.DeltaAngle(yawBefore,game.View.GameCamera.transform.eulerAngles.y))<.1f, "open modal blocks focus and rotation hotkeys");
            game.Hud.ClosePanels();
        }
        IEnumerator Guarded()
        {
            Status="Running";
            // Flatten nested iterators so failures in key sequences also run cleanup.
            var stack=new System.Collections.Generic.Stack<IEnumerator>(); stack.Push(Run());
            Exception failure=null;
            while(stack.Count>0)
            {
                object current=null;bool more=false;
                try { more=stack.Peek().MoveNext(); if(more)current=stack.Peek().Current; }
                catch(Exception e) { failure=e;break; }
                if(!more) { stack.Pop();continue; }
                if(current is IEnumerator nested)stack.Push(nested); else yield return current;
            }
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            if(mouse!=null)InputSystem.RemoveDevice(mouse);
            previousKeyboard?.MakeCurrent();previousMouse?.MakeCurrent();
            if(snapshotCaptured)
            {
                InputSystem.settings.backgroundBehavior=previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorInput;
            }
            try { RestoreRuntime(); }
            catch(Exception e) { report.AppendLine("RESTORE ERROR: "+e);if(failure==null)failure=e; }
            Status=(failure==null?"PASS: ":"FAIL: ")+count+" live input/camera checks"+(failure==null?"":"; "+failure.Message);
            report.Insert(0,Status+"\n");
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../Verification/authored-view-checks.txt")),report.ToString());
            Destroy(this);
        }
    }
}
#endif
