using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

// Read-only asset acceptance. Instances and baked meshes are temporary; no report/save/assets are written.
public static class EquipmentAssetChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly StringBuilder report=new StringBuilder();
    static int assertions;
    static readonly string[] ResourceNames={"FishingRod/Rod","Shovel/Shovel","ShellBellows/Bellows"};
    static readonly string[] SourcePaths={
        "Assets/HabitatIsland/Art/FishingRod/fishing_rod_unirig_animated.glb",
        "Assets/HabitatIsland/Art/Shovel/Shovel_PlayerReady.glb",
        "Assets/HabitatIsland/Art/ShellBellows/Shell_Bellows_Rigged.glb"};
    static void Require(bool okay,string label){if(!okay)throw new InvalidOperationException(label);}
    static void Check(bool okay,string label)
    {report.AppendLine((okay?"PASS ":"FAIL ")+(++assertions)+": "+label);Require(okay,label);}
    static GameObject Make(GameObject asset,bool active=false)
    {
        Require(asset,"Missing equipment asset.");
        var instance=UnityEngine.Object.Instantiate(asset);
        instance.name="Equipment acceptance temporary "+asset.name;
        instance.hideFlags=HideFlags.HideAndDontSave;
        instance.transform.position=new Vector3(1000,1000,1000);
        instance.SetActive(active);return instance;
    }
    static Transform Node(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
    static Animator Animator(GameObject root)=>root.GetComponent<HabitatEquipmentAnimation>().Animator;
    static AnimationClip Clip(GameObject root,string name)=>root.GetComponent<HabitatEquipmentAnimation>().Clips.Single(c=>c&&c.name==name);
    static void Sample(GameObject root,string name,float time)=>Clip(root,name).SampleAnimation(Animator(root).gameObject,time);
    static Vector3 Local(GameObject root,Transform node)=>root.transform.InverseTransformPoint(node.position);
    static Vector3[] Vertices(GameObject root)
    {
        var result=new List<Vector3>();
        foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh=new Mesh();
            try
            {
                skin.BakeMesh(mesh,true);
                foreach(Vector3 p in mesh.vertices)result.Add(root.transform.InverseTransformPoint(skin.transform.TransformPoint(p)));
            }
            finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        Require(result.Count>0,"No skinned geometry to measure.");return result.ToArray();
    }
    static Bounds BoundsOf(Vector3[] points)
    {var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);return bounds;}
    static float Delta(Vector3[] a,Vector3[] b,IEnumerable<int> subset=null)
    {
        Require(a.Length==b.Length,"Sampling changed vertex count.");float largest=0;
        foreach(int i in subset??Enumerable.Range(0,a.Length))largest=Mathf.Max(largest,Vector3.Distance(a[i],b[i]));
        return largest;
    }
    static void CheckMaterials(GameObject instance,GameObject source,string label)
    {
        Material[] actual=instance.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).ToArray();
        Material[] original=source.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).ToArray();
        bool valid=actual.Length==original.Length&&actual.Length>0;int textures=0;
        for(int i=0;valid&&i<actual.Length;i++)
        {
            Material a=actual[i],b=original[i];
            valid=a&&b&&a!=b&&a.shader==b.shader&&AssetDatabase.GetAssetPath(a).EndsWith(".mat");
            if(!valid)break;
            foreach(string property in b.GetTexturePropertyNames())
            {
                Texture texture=b.GetTexture(property);if(!texture)continue;textures++;
                valid&=a.HasProperty(property)&&a.GetTexture(property)==texture
                    &&(a.GetTextureScale(property)-b.GetTextureScale(property)).sqrMagnitude<.000001f
                    &&(a.GetTextureOffset(property)-b.GetTextureOffset(property)).sqrMagnitude<.000001f;
            }
        }
        Check(valid&&textures>=2,label+" retains original PBR shader, embedded color/packed textures and UV transforms in independent materials");
    }
    static void CheckWeights(GameObject instance,string label,int boneCount,bool rigid=false)
    {
        var skins=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        bool valid=skins.Length>0,oneRigidBone=true;int vertices=0;
        foreach(var skin in skins)
        {
            valid&=skin.sharedMesh&&skin.bones.Length==boneCount&&skin.bones.All(b=>b);
            if(!valid)break;
            var weights=skin.sharedMesh.boneWeights;valid&=weights.Length==skin.sharedMesh.vertexCount;vertices+=weights.Length;
            var used=new HashSet<int>();
            foreach(var w in weights)
            {
                float[] values={w.weight0,w.weight1,w.weight2,w.weight3};int[] ids={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};
                float sum=0;int influences=0;
                for(int i=0;i<4;i++)
                {
                    valid&=float.IsFinite(values[i])&&values[i]>=0;sum+=values[i];
                    if(values[i]>.00001f){influences++;valid&=ids[i]>=0&&ids[i]<skin.bones.Length;used.Add(ids[i]);}
                }
                valid&=Mathf.Abs(sum-1f)<.005f;
                if(rigid)oneRigidBone&=influences==1;
            }
            if(rigid)oneRigidBone&=used.Count==1&&skin.bones[used.Single()].name=="Tool_Rigid";
        }
        Check(valid&&vertices>1000&&(!rigid||oneRigidBone),label+" has valid normalized skin weights and "+boneCount+" bones"+(rigid?"; all shovel vertices follow one rigid tool bone":""));
    }
    static void CheckClip(GameObject instance,string name,float seconds,bool loop)
    {
        AnimationClip clip=Clip(instance,name);
        var bindings=AnimationUtility.GetCurveBindings(clip);
        float first=float.PositiveInfinity,last=0;
        foreach(var binding in bindings)
        {
            var curve=AnimationUtility.GetEditorCurve(clip,binding);if(curve==null||curve.length==0)continue;
            first=Mathf.Min(first,curve.keys[0].time);last=Mathf.Max(last,curve.keys[curve.length-1].time);
        }
        Check(!clip.legacy&&AssetDatabase.GetAssetPath(clip).EndsWith(".anim")&&Mathf.Abs(clip.length-seconds)<.015f
            &&Mathf.Abs(first)<.001f&&Mathf.Abs(last-seconds)<.015f&&clip.isLooping==loop,
            name+" is an independent zero-based "+seconds+" second clip with the requested loop setting");
        Sample(instance,name,0);var start=Vertices(instance);float motion=0;
        foreach(float fraction in new[]{.2f,.4f,.6f,.8f})
        {Sample(instance,name,seconds*fraction);motion=Mathf.Max(motion,Delta(start,Vertices(instance)));}
        Check(motion>.000015f,name+" actually deforms/moves rendered vertices (largest displacement "+motion.ToString("F6")+")");
    }
    public static string Run()
    {
        assertions=0;report.Clear();var instances=new List<GameObject>();
        try
        {
            var prefabs=ResourceNames.Select(n=>Resources.Load<GameObject>("Models/"+n)).ToArray();
            Check(prefabs.All(p=>p)&&prefabs.Distinct().Count()==3,"three requested resource prefabs exist and are distinct");
            var roots=new GameObject[3];var sources=new GameObject[3];
            for(int i=0;i<3;i++)
            {
                roots[i]=Make(prefabs[i]);instances.Add(roots[i]);
                sources[i]=Make(AssetDatabase.LoadAssetAtPath<GameObject>(SourcePaths[i]));instances.Add(sources[i]);
                CheckMaterials(roots[i],sources[i],ResourceNames[i]);
            }
            GameObject rod=roots[0],shovel=roots[1],bellows=roots[2];
            CheckWeights(rod,"Rod",19);CheckWeights(shovel,"Shovel",4,true);CheckWeights(bellows,"Bellows",6);
            Check(rod.GetComponent<HabitatEquipmentAnimation>().IsConfigured&&bellows.GetComponent<HabitatEquipmentAnimation>().IsConfigured
                &&shovel.GetComponent<HabitatEquipmentAnimation>().Animator&&!shovel.GetComponent<HabitatEquipmentAnimation>().IsConfigured
                &&shovel.GetComponent<HabitatEquipmentAnimation>().Clips.Count==0,
                "rod and bellows are animated; supplied shovel remains a static rig without invented clips");
            Sample(rod,"Idle_Sway",0);Clip(rod,"Idle_Sway").SampleAnimation(sources[0],0);
            Bounds rodBounds=BoundsOf(Vertices(rod)),rodSource=BoundsOf(Vertices(sources[0]));
            Bounds shovelBounds=BoundsOf(Vertices(shovel)),shovelSource=BoundsOf(Vertices(sources[1]));
            Check((rodBounds.size-rodSource.size).magnitude<.003f&&(rodBounds.center-rodSource.center).magnitude<.003f
                &&Local(rod,Node(rod,"Grip_CTRL")).magnitude<.002f,"rod preserves measured source dimensions and grip at zero");
            Check((shovelBounds.size-shovelSource.size).magnitude<.003f&&(shovelBounds.center-shovelSource.center).magnitude<.003f
                &&Local(shovel,Node(shovel,"Grip_Primary")).magnitude<.002f,"shovel preserves measured source dimensions and primary grip at zero");
            Transform primary=Node(shovel,"Grip_Primary"),secondary=Node(shovel,"Grip_Secondary"),tool=Node(shovel,"Tool_Rigid");
            Check(primary.IsChildOf(tool)&&secondary.IsChildOf(tool)&&Mathf.Abs(Vector3.Distance(primary.position,secondary.position)-.648f)<.003f,
                "shovel secondary grip is attached to the rigid tool and remains .648 source units from primary grip");
            var shovelRest=Vertices(shovel);tool.localRotation*=Quaternion.Euler(0,0,20);
            var shovelTurn=Vertices(shovel);float distortion=0;
            for(int i=1;i<shovelRest.Length;i+=137)distortion=Mathf.Max(distortion,Mathf.Abs(Vector3.Distance(shovelRest[0],shovelRest[i])-Vector3.Distance(shovelTurn[0],shovelTurn[i])));
            Check(Delta(shovelRest,shovelTurn)>.1f&&distortion<.002f,"rotating the shovel bone moves its visible mesh rigidly without stretching");

            CheckClip(rod,"Idle_Sway",4,true);CheckClip(rod,"Cast",3,false);CheckClip(rod,"Hook_Pull",3.5f,false);
            CheckClip(bellows,"Idle",2,true);CheckClip(bellows,"Pump_Loop",1.5f,true);CheckClip(bellows,"Pump_Once",1,false);
            Sample(bellows,"Idle",0);var bellowsRest=Vertices(bellows);Bounds bounds=BoundsOf(bellowsRest);
            Transform nozzle=Node(bellows,"root_nozzle"),upper=Node(bellows,"shell_upper"),lower=Node(bellows,"shell_lower");
            Vector3 nozzleAtRest=Local(bellows,nozzle);Quaternion nozzleRotation=nozzle.rotation;
            Check(Mathf.Abs(Mathf.Max(bounds.size.x,bounds.size.z)-.82f)<.002f&&Mathf.Abs(bounds.min.y)<.002f,
                "measured bellows mesh occupies .82 horizontal span and rests at ground zero");
            Check(nozzleAtRest.z>bounds.size.z*.2f&&Mathf.Abs(nozzleAtRest.x-bounds.center.x)<bounds.size.x*.15f
                &&Vector3.Dot((upper.position-lower.position).normalized,Vector3.up)>.99f,
                "evaluated world bones put nozzle toward +Z and upper shell above lower shell in +Y");
            int[] nozzleVertices=Enumerable.Range(0,bellowsRest.Length).Where(i=>bellowsRest[i].z>bounds.max.z-bounds.size.z*.12f).ToArray();
            Require(nozzleVertices.Length>30,"Could not identify a meaningful visible nozzle tip region.");
            foreach(string name in new[]{"Idle","Pump_Loop","Pump_Once"})
            {
                float duration=Clip(bellows,name).length;Sample(bellows,name,0);var first=Vertices(bellows);
                float nozzleDrift=0,boneDrift=0,boneTurn=0;
                for(int i=0;i<=8;i++)
                {
                    Sample(bellows,name,duration*i/8f);nozzleDrift=Mathf.Max(nozzleDrift,Delta(bellowsRest,Vertices(bellows),nozzleVertices));
                    boneDrift=Mathf.Max(boneDrift,Vector3.Distance(nozzleAtRest,Local(bellows,nozzle)));
                    boneTurn=Mathf.Max(boneTurn,Quaternion.Angle(nozzleRotation,nozzle.rotation));
                }
                Check(nozzleDrift<.0004f&&boneDrift<.0004f&&boneTurn<.02f,name+" holds nozzle bone and "+nozzleVertices.Length+" visible tip vertices fixed throughout pumping");
                Check(Delta(first,Vertices(bellows))<.0005f,name+" returns the full rendered mesh to its starting pose at the endpoint");
            }
            report.AppendLine("PASS: "+assertions+" equipment asset/geometry assertions; no assets or scene saved.");
            return report.ToString();
        }
        catch(Exception e){throw new InvalidOperationException(report+"ERROR: "+e,e);}
        finally{foreach(var instance in instances)if(instance)UnityEngine.Object.DestroyImmediate(instance);}
    }

    static PlayableGraph Graph(HabitatEquipmentAnimation driver)=>(PlayableGraph)typeof(HabitatEquipmentAnimation).GetField("graph",Private).GetValue(driver);
    static async Task Frames(int count)
    {
        int first=Time.frameCount;double limit=EditorApplication.timeSinceStartup+8;
        while(Time.frameCount-first<count)
        {Require(Application.isPlaying&&EditorApplication.timeSinceStartup<limit,"Real Play frames did not advance.");await Task.Delay(12);}
    }
    public static async Task<string> RunRuntime()
    {
        Require(Application.isPlaying,"RunRuntime needs Play mode.");assertions=0;report.Clear();
        var instances=new List<GameObject>();var graphs=new List<PlayableGraph>();
        try
        {
            GameObject rod=Make(Resources.Load<GameObject>("Models/FishingRod/Rod"),true);instances.Add(rod);
            GameObject bellows=Make(Resources.Load<GameObject>("Models/ShellBellows/Bellows"),true);instances.Add(bellows);
            var driver=rod.GetComponent<HabitatEquipmentAnimation>();var pump=bellows.GetComponent<HabitatEquipmentAnimation>();
            Check(driver.Play("Idle_Sway")&&pump.Play("Pump_Loop"),"manual runtime instances bind supplied clips");
            graphs.Add(Graph(driver));graphs.Add(Graph(pump));var first=Vertices(rod);await Frames(8);
            Check(driver.TimeSeconds==0f&&pump.TimeSeconds==0f&&Delta(first,Vertices(rod))<.0003f,
                "eight real frames without owner Tick do not advance time or geometry");
            driver.Tick(4.35f);await Frames(1);
            Check(Mathf.Abs(driver.TimeSeconds-.35f)<.001f&&!driver.Finished&&Delta(first,Vertices(rod))>.0001f,
                "explicit Tick wraps a loop and produces a different visible pose");
            float selectedTime=driver.TimeSeconds;driver.Play("Idle_Sway");await Frames(2);
            Check(driver.TimeSeconds==selectedTime,"same-name Play retains current time instead of restarting");
            driver.Play("Cast",false);driver.Tick(1.5f);var castMid=Vertices(rod);driver.Tick(3f);await Frames(1);var castEnd=Vertices(rod);
            Check(driver.Finished&&Mathf.Abs(driver.TimeSeconds-3f)<.001f&&Delta(castMid,castEnd)>.001f,"single Cast reaches its actual end pose and clamps to duration");
            driver.Tick(100f);await Frames(2);
            Check(driver.TimeSeconds==3f&&Delta(castEnd,Vertices(rod))<.0003f,"completed single clip remains at its terminal pose");
            driver.Play("Cast",false,1f,true);
            Check(!driver.Finished&&driver.TimeSeconds==0f,"explicit restart begins the same single action again");
            driver.Sample("Hook_Pull",1.2f);var held=Vertices(rod);driver.Tick(20f);await Frames(3);
            Check(Mathf.Abs(driver.TimeSeconds-1.2f)<.001f&&Delta(held,Vertices(rod))<.0003f,"Sample holds its exact pose across ticks and real frames");
            Check(!driver.Play("missing")&&!driver.Sample("Cast",float.NaN)&&driver.CurrentClipName=="Hook_Pull"&&Mathf.Abs(driver.TimeSeconds-1.2f)<.001f,
                "invalid clip/time requests preserve the currently sampled pose");
            for(int i=0;i<12;i++){driver.Play(i%2==0?"Idle_Sway":"Cast",i%2==0,1,true);driver.Tick(.1f);}
            Check(Graph(driver).GetPlayableCount()==1&&Graph(driver).GetOutputCount()==1,"repeated clip switches leave one playable and one output without layering");
            float time=driver.TimeSeconds;var visible=Vertices(rod);PlayableGraph old=Graph(driver);rod.SetActive(false);await Frames(2);
            Check(!old.IsValid()&&!Graph(driver).IsValid()&&driver.TimeSeconds==time,"deactivation releases the graph while preserving selected time");
            rod.SetActive(true);await Frames(2);graphs.Add(Graph(driver));
            Check(Graph(driver).IsValid()&&driver.TimeSeconds==time&&Delta(visible,Vertices(rod))<.0003f,"reactivation rebuilds and restores the saved visible pose");
            old=Graph(driver);driver.enabled=false;await Frames(1);
            Check(!old.IsValid()&&!Graph(driver).IsValid(),"disabling only the component also releases its playable graph");
            driver.enabled=true;await Frames(1);graphs.Add(Graph(driver));
            Animator animator=driver.Animator;AnimationClip[] clips=driver.Clips.ToArray();old=Graph(driver);driver.Configure(animator,clips);
            Check(!old.IsValid()&&!Graph(driver).IsValid()&&driver.CurrentClipName==""&&driver.TimeSeconds==0f&&driver.IsConfigured,
                "Configure resets presentation time and destroys the old graph without losing valid assets");
            driver.Play("Hook_Pull",false,2f);driver.Tick(.25f);graphs.Add(Graph(driver));
            Check(Mathf.Abs(driver.TimeSeconds-.5f)<.001f&&Mathf.Abs(driver.Duration-3.5f)<.001f,"requested playback speed advances the supplied clip in seconds");
            pump.Tick(.75f);var pumpMid=Vertices(bellows);pump.Tick(.75f);await Frames(1);
            Check(pump.TimeSeconds<.001f&&!pump.Finished&&Delta(pumpMid,Vertices(bellows))>.001f,"bellows Pump_Loop visibly compresses then wraps after 1.5 seconds");
            pump.Play("Pump_Once",false,1,true);pump.Tick(2f);await Frames(1);
            Check(pump.Finished&&Mathf.Abs(pump.TimeSeconds-1f)<.001f,"bellows Pump_Once completes once at one second");
        }
        catch(Exception e){throw new InvalidOperationException(report+"ERROR: "+e,e);}
        finally
        {
            foreach(var instance in instances)if(instance){instance.SetActive(false);UnityEngine.Object.Destroy(instance);}
            await Frames(2);
            Require(graphs.All(g=>!g.IsValid())&&instances.All(o=>!o),"Temporary equipment or playable graphs leaked after cleanup.");
        }
        report.AppendLine("PASS: "+assertions+" runtime assertions on real frames; all temporary instances/graphs destroyed; no game state or files modified.");
        return report.ToString();
    }
}
