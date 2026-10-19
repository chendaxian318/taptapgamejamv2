using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEditor;
using UnityEngine;

// Run in an idle main-map Play session. Uses real frames, the real opening and real pause API.
// Does not save game files or modify art/production assets; cancels rather than completes the story.
public static class VerifySeabirdRuntime
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const string Output="Verification/latest-plan-20261018/seabird/runtime.txt";
    static bool running;
    static double deadline;
    static StringBuilder report;
    static void Check(bool okay,string message)
    {report.AppendLine((okay?"PASS ":"FAIL ")+message);if(!okay)throw new InvalidOperationException(message);}
    static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,F).GetValue(owner);
    static object GameCall(HabitatGame game,string name,params object[] args)=>typeof(HabitatGame).GetMethod(name,F).Invoke(game,args);
    static async Task Frames(int count=1)
    {
        int goal=Time.frameCount+count;
        while(Time.frameCount<goal)
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Play mode stopped during seabird verification.");
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Seabird verification exceeded 15 real seconds.");
            await Task.Delay(8);
        }
    }
    static async Task Observe(float seconds,Action sample)
    {
        double until=EditorApplication.timeSinceStartup+seconds;
        while(EditorApplication.timeSinceStartup<until){await Frames();sample?.Invoke();}
    }
    static Quaternion[] Poses(Transform[] joints)=>joints.Select(t=>t.localRotation).ToArray();
    static void CheckBounds(GameObject art)
    {
        foreach(var renderer in art.GetComponentsInChildren<MeshRenderer>())
        {
            Bounds b=renderer.bounds;b.Expand(.003f);
            foreach(var vertex in renderer.GetComponent<MeshFilter>().sharedMesh.vertices)
                if(!b.Contains(renderer.transform.TransformPoint(vertex)))throw new InvalidOperationException("Animated renderer bounds exclude geometry: "+renderer.name);
        }
    }
    public static async Task<string> Run()
    {
        if(running)throw new InvalidOperationException("A seabird check is already running.");
        var game=HabitatGame.Instance;
        if(!Application.isPlaying||!game||game.Busy||!game.Story||game.Fishing&&game.Fishing.IsOpen)
            throw new InvalidOperationException("Requires an idle main-map Play session with Harbor and Story ready.");
        if(Time.timeScale<=0)throw new InvalidOperationException("Time.timeScale must be positive for a real-frame animation check.");
        running=true;deadline=EditorApplication.timeSinceStartup+15;report=new StringBuilder("Seabird real-frame opening verification. No Animator.Update/SampleAnimation/manual time stepping.\n");
        var story=game.Story;
        object saved=GameCall(game,"CaptureSnapshot");string savedMessage=game.State.message;
        bool savedPause=game.paused;int savedTool=game.tool;float savedSpeed=game.speed;
        var history=Field<Stack<HabitatState>>(game,"history");var undo=history.ToArray();
        var cameraFields=game.View.GetType().GetFields(F).Where(f=>!f.IsInitOnly&&!f.IsStatic&&(f.FieldType.IsPrimitive||f.FieldType.IsEnum||f.FieldType==typeof(Vector3))).ToDictionary(f=>f,f=>f.GetValue(game.View));
        var chapterFields=new[]{"chapterGreetingPending","chapterChestPending","chapterMorningGreetingPending","chapterReturnReminderShown","campaignReturnReminderLevel"}
            .ToDictionary(name=>typeof(HabitatGame).GetField(name,F),name=>typeof(HabitatGame).GetField(name,F).GetValue(game));
        Transform[] gulls=null;string outcome="FAIL verification did not finish";
        try
        {
            game.paused=true;
            var prefab=Resources.Load<GameObject>("Models/Story/Seabird");
            Check(prefab,"Seabird loads through the unchanged MainStory Resources path");
            Check(Vector3.Dot(prefab.transform.localRotation*Vector3.forward,Vector3.right)>.999f,"Prefab anatomy faces +X in the story-parent frame");
            Check(story.BeginOpening(),"Actual MainStory opening started");await Frames(3);
            gulls=Field<Transform[]>(story,"gulls");
            Check(gulls!=null&&gulls.Length==3&&!Field<bool>(story,"proceduralGulls"),"Three resource birds instantiated; geometric fallback disabled");
            var registered=Field<IDictionary>(story,"storyAnimatorSpeeds");
            var arts=new List<GameObject>();var animators=new List<Animator>();var joints=new List<Transform>();
            foreach(var gull in gulls)
            {
                Check(gull.childCount==1,"Story bird has one imported art root");var art=gull.GetChild(0).gameObject;arts.Add(art);
                Check(Quaternion.Angle(art.transform.localRotation,prefab.transform.localRotation)<.01f,"InstantiateStoryVisual preserves the prefab +X heading");
                Vector3 flightDirection=gull.parent.TransformDirection(Vector3.right).normalized;
                Check(Vector3.Dot(art.transform.forward,flightDirection)>.98f,"Live bird faces along its +X travel path despite story banking");
                var animator=art.GetComponent<Animator>();Check(animator&&registered.Contains(animator),"Live Animator registered for story pause/resume");animators.Add(animator);
                Check(animator.runtimeAnimatorController&&!animator.applyRootMotion&&animator.cullingMode==AnimatorCullingMode.AlwaysAnimate,"Loop controller is active, no root motion, no off-camera animation culling");
                foreach(string side in new[]{"Left","Right"})
                {
                    string path="FlightRig/"+side+"Shoulder";
                    foreach(string joint in new[]{path,path+"/"+side+"Elbow",path+"/"+side+"Elbow/"+side+"Wrist"})
                    {var t=art.transform.Find(joint);Check(t,"Bound wing joint exists: "+joint);joints.Add(t);}
                }
                CheckBounds(art);
            }
            var clip=animators[0].runtimeAnimatorController.animationClips.First(c=>c.name=="Flight_Loop");
            Check(AnimationUtility.GetAnimationClipSettings(clip).loopTime&&Math.Abs(clip.length-1.1f)<.001f,"Flight_Loop is a real 1.1-second looping clip");
            var bindings=AnimationUtility.GetCurveBindings(clip);Check(bindings.Length>=10,"All flight curves are present");
            foreach(var binding in bindings)
            {
                Check(arts[0].transform.Find(binding.path),"Curve resolves to live hierarchy: "+binding.path+" "+binding.propertyName);
                var curve=AnimationUtility.GetEditorCurve(clip,binding);
                Check(Math.Abs(curve.Evaluate(0)-curve.Evaluate(clip.length))<.001f,"Curve has matching loop endpoints: "+binding.propertyName);
            }
            var jointArray=joints.ToArray();var initial=Poses(jointArray);var maxAngles=new float[jointArray.Length];
            Vector3 routeStart=gulls[0].localPosition;float loopStart=animators[0].GetCurrentAnimatorStateInfo(0).normalizedTime;
            await Observe(1.22f,()=>
            {
                for(int i=0;i<jointArray.Length;i++)maxAngles[i]=Math.Max(maxAngles[i],Quaternion.Angle(initial[i],jointArray[i].localRotation));
                foreach(var art in arts)CheckBounds(art);
            });
            Check(maxAngles.All(v=>v>8f),"All 18 live shoulder/elbow/wrist joints change pose through real frames");
            Check(animators[0].GetCurrentAnimatorStateInfo(0).normalizedTime-loopStart>.9f,"Animator advances through a complete cycle without manual sampling");
            Check((gulls[0].localPosition-routeStart).sqrMagnitude>.1f,"Existing MainStory flight path advances while wings animate");
            report.AppendLine("Measured joint angle changes: "+string.Join(",",maxAngles.Select(v=>v.ToString("F2"))));
            story.TogglePause();await Frames(2);
            Check(story.IsPaused&&animators.All(a=>a.speed==0),"Real story pause stops all three seabird Animators");
            var frozen=Poses(jointArray);Vector3 pausedRoute=gulls[0].localPosition;double pausedTimeline=story.director.time;
            await Observe(.22f,null);
            Check(jointArray.Select((t,i)=>Quaternion.Angle(t.localRotation,frozen[i])).All(v=>v<.02f),"Paused wing poses remain frozen across real frames");
            Check(Math.Abs(story.director.time-pausedTimeline)<.003&&(gulls[0].localPosition-pausedRoute).sqrMagnitude<.00001f,"Paused Timeline and existing travel path remain frozen");
            story.TogglePause();
            var resumedMax=new float[jointArray.Length];
            await Observe(.38f,()=>{for(int i=0;i<jointArray.Length;i++)resumedMax[i]=Math.Max(resumedMax[i],Quaternion.Angle(frozen[i],jointArray[i].localRotation));});
            Check(!story.IsPaused&&animators.All(a=>a.speed>0)&&resumedMax.All(v=>v>2f),"Resume restores all three birds and all wing joints continue");
            story.Cancel();await Frames(2);
            Check(!story.IsPlaying&&gulls.All(g=>!g)&&registered.Count==0,"Cancel destroys temporary birds and clears Animator registration");
            outcome="PASS seabird real opening: three instantiated birds, heading, real-frame full-loop flight, animated bounds, pause/resume and cancel cleanup.";
            report.AppendLine(outcome);
        }
        catch(Exception error)
        {
            var actual=error is TargetInvocationException&&error.InnerException!=null?error.InnerException:error;
            outcome="FAIL seabird: "+actual.Message;report.AppendLine(outcome);report.AppendLine(actual.ToString());
        }
        finally
        {
            try
            {
                if(story&&story.IsPlaying)story.Cancel();
                GameCall(game,"RestoreSnapshot",saved);game.State.message=savedMessage;
                history.Clear();for(int i=undo.Length-1;i>=0;i--)history.Push(undo[i]);
                foreach(var item in chapterFields)item.Key.SetValue(game,item.Value);
                foreach(var item in cameraFields)item.Key.SetValue(game.View,item.Value);
                game.paused=savedPause;game.tool=savedTool;game.speed=savedSpeed;
                report.AppendLine("Original in-memory snapshot, undo history, chapter flags, camera values and game pause restored; no game saves or production assets written.");
            }
            catch(Exception error){outcome="FAIL restore: "+error.Message+"; "+outcome;report.AppendLine(outcome);}
            finally{Directory.CreateDirectory(Path.GetDirectoryName(Output));File.WriteAllText(Output,report.ToString());running=false;}
        }
        return outcome;
    }
}
