using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

// Execute BuildSeabird.Run() in Unity edit mode. This file stays outside Assets.
// Program-authored, replaceable scene art; it is not a delivered artist-approved design.
public static class BuildSeabird
{
    const string Folder="Assets/HabitatIsland/Resources/Models/Story/SeabirdAssets";
    const string PrefabPath="Assets/HabitatIsland/Resources/Models/Story/Seabird.prefab";
    const float Cycle=1.1f;
    static Material ivory,grey,ink,gold,amber;
    static readonly List<Mesh> meshes=new List<Mesh>();

    public static string Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Build the seabird in edit mode.");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();meshes.Clear();
        ivory=Material("Ivory",new Color(.96f,.975f,.91f));
        grey=Material("SeaGrey",new Color(.53f,.69f,.69f));
        ink=Material("Charcoal",new Color(.10f,.17f,.19f));
        gold=Material("BeakGold",new Color(1f,.65f,.19f));
        amber=Material("WarmAmber",new Color(.71f,.32f,.10f));
        var root=new GameObject("Seabird");
        try
        {
            // The existing main-story route travels along +X. Model anatomy faces local +Z.
            root.transform.localRotation=Quaternion.Euler(0,90,0);root.transform.localScale=Vector3.one*.56f;
            var rig=Child("FlightRig",root.transform,Vector3.zero);
            Part("Body",rig,Loft(new[]{
                new Vector4(-.46f,.025f,.045f,-.005f),new Vector4(-.31f,.12f,.095f,-.015f),
                new Vector4(-.12f,.19f,.15f,0),new Vector4(.10f,.19f,.15f,.02f),
                new Vector4(.26f,.105f,.12f,.055f),new Vector4(.35f,.06f,.07f,.075f)},14),ivory);
            Ellipsoid("Mantle",rig,new Vector3(0,.105f,-.03f),new Vector3(.16f,.065f,.255f),grey,12,6);
            Ellipsoid("Neck",rig,new Vector3(0,.11f,.29f),new Vector3(.10f,.12f,.14f),ivory,12,7);
            Ellipsoid("Head",rig,new Vector3(0,.17f,.41f),new Vector3(.12f,.115f,.155f),ivory,14,8);
            Part("UpperBeak",rig,Loft(new[]{new Vector4(.525f,.055f,.04f,.135f),new Vector4(.63f,.045f,.028f,.12f),new Vector4(.77f,.004f,.004f,.095f)},8),gold);
            Part("LowerBeak",rig,Loft(new[]{new Vector4(.53f,.049f,.017f,.101f),new Vector4(.65f,.032f,.012f,.094f),new Vector4(.745f,.003f,.002f,.09f)},8),amber);
            for(int side=-1;side<=1;side+=2)
            {
                string label=side<0?"Left":"Right";
                Ellipsoid(label+"EyeRim",rig,new Vector3(side*.107f,.198f,.464f),new Vector3(.015f,.025f,.025f),gold,8,5);
                Ellipsoid(label+"Eye",rig,new Vector3(side*.118f,.2f,.469f),new Vector3(.011f,.017f,.018f),ink,8,5);
                Ellipsoid(label+"EyeGlint",rig,new Vector3(side*.126f,.206f,.476f),new Vector3(.0035f,.005f,.005f),ivory,6,4);
                // Feet are tucked back while flying, with a small three-toed web silhouette.
                var foot=Child(label+"TuckedFoot",rig,new Vector3(side*.07f,-.105f,-.17f));
                foot.localRotation=Quaternion.Euler(0,180,0);
                Ellipsoid("Ankle",foot,new Vector3(0,0,.04f),new Vector3(.021f,.019f,.09f),amber,8,5);
                for(int toe=-1;toe<=1;toe++)
                {
                    var digit=Child("Toe"+toe,foot,new Vector3(toe*.014f,-.004f,.092f));
                    digit.localRotation=Quaternion.Euler(0,toe*18,0);
                    Ellipsoid("ToeMesh",digit,new Vector3(0,0,.045f),new Vector3(.017f,.012f,.06f),gold,8,5);
                }
                BuildWing(rig,side,label);
            }
            var tail=Child("Tail",rig,new Vector3(0,.015f,-.32f));
            for(int i=-2;i<=2;i++)
            {
                var feather=Child("TailFeather"+(i+2),tail,new Vector3(i*.036f,Math.Abs(i)*.003f,0));
                feather.localRotation=Quaternion.Euler(0,i*-8,0);
                Ellipsoid("Feather",feather,new Vector3(0,0,-.20f),new Vector3(.048f,.019f,.245f-Math.Abs(i)*.013f),i%2==0?ivory:grey,10,6);
            }

            var clip=MakeFlight();
            Save(clip,Folder+"/Flight_Loop.anim");
            var savedClip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/Flight_Loop.anim");
            string controllerPath=Folder+"/Seabird.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var sm=controller.layers[0].stateMachine;
            var state=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Flight_Loop")??sm.AddState("Flight_Loop");
            state.motion=savedClip;state.speed=1;sm.defaultState=state;EditorUtility.SetDirty(controller);
            var animator=root.AddComponent<Animator>();animator.runtimeAnimatorController=controller;
            animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            // MainStory already records Animator speeds and pauses/resumes them with its Timeline.
            Validate(root);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);AssetDatabase.SaveAssets();
            int triangles=meshes.Sum(m=>m.triangles.Length/3);
            return "PASS Seabird prefab: "+meshes.Count+" meshes, "+triangles+" triangles, 5 opaque materials; six articulated wing joints, 1.1-second loop, +X story-path facing; program-authored replaceable art. "+PrefabPath;
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }

    static void BuildWing(Transform rig,int side,string label)
    {
        var shoulder=Child(label+"Shoulder",rig,new Vector3(side*.115f,.075f,.06f));
        Part("InnerWing",shoulder,Wing(new[]{
            new Vector4(0,.20f,-.18f,.038f),new Vector4(.20f,.18f,-.26f,.04f),
            new Vector4(.43f,.10f,-.245f,.028f),new Vector4(.52f,.06f,-.18f,.017f)},side),grey);
        // Distinct feather scallops make the trailing silhouette read as plumage, not a slab.
        for(int i=0;i<4;i++)
        {
            var feather=Child("Secondary"+i,shoulder,new Vector3(side*(.15f+i*.085f),-.008f,-.19f));
            feather.localRotation=Quaternion.Euler(0,side*-10,0);
            Ellipsoid("Feather",feather,new Vector3(0,0,-.055f),new Vector3(.053f,.012f,.09f),ivory,8,5);
        }
        var elbow=Child(label+"Elbow",shoulder,new Vector3(side*.47f,0,-.035f));
        Part("MiddleWing",elbow,Wing(new[]{new Vector4(0,.13f,-.18f,.025f),
            new Vector4(.19f,.13f,-.24f,.022f),new Vector4(.39f,.07f,-.20f,.014f),
            new Vector4(.43f,.04f,-.17f,.009f)},side),grey);
        var wrist=Child(label+"Wrist",elbow,new Vector3(side*.37f,-.005f,-.015f));
        Part("WingHand",wrist,Wing(new[]{new Vector4(0,.105f,-.20f,.017f),
            new Vector4(.16f,.065f,-.21f,.015f),new Vector4(.34f,-.025f,-.18f,.01f),
            new Vector4(.48f,-.13f,-.14f,.003f)},side),ink);
        for(int i=0;i<4;i++)
        {
            var primary=Child("Primary"+i,wrist,new Vector3(side*(.105f+i*.079f),-.004f,-.14f-i*.017f));
            primary.localRotation=Quaternion.Euler(0,side*(45+i*10),0);
            Ellipsoid("BlackFeather",primary,new Vector3(0,0,-.075f),new Vector3(.038f,.010f,.125f-i*.01f),ink,8,5);
            Ellipsoid("WhiteTipMark",primary,new Vector3(0,.009f,-.15f+i*.01f),new Vector3(.019f,.006f,.027f),ivory,8,4);
        }
    }

    static AnimationClip MakeFlight()
    {
        var clip=new AnimationClip{name="Flight_Loop",frameRate=30,wrapMode=WrapMode.Loop};
        Curve(clip,"FlightRig","m_LocalPosition.y",p=>.018f*Mathf.Sin(p*2*Mathf.PI));
        Curve(clip,"FlightRig/Tail","localEulerAnglesRaw.x",p=>3*Mathf.Sin(p*2*Mathf.PI+.6f));
        for(int side=-1;side<=1;side+=2)
        {
            string label=side<0?"Left":"Right",shoulder="FlightRig/"+label+"Shoulder";
            // The wrist lags the shoulder, making the downstroke broad and the return slightly folded.
            Curve(clip,shoulder,"localEulerAnglesRaw.z",p=>side*(9+32*Mathf.Sin(p*2*Mathf.PI)));
            Curve(clip,shoulder+"/"+label+"Elbow","localEulerAnglesRaw.z",p=>side*(-4+17*Mathf.Sin(p*2*Mathf.PI-.55f)));
            Curve(clip,shoulder+"/"+label+"Elbow/"+label+"Wrist","localEulerAnglesRaw.z",p=>side*(-3+15*Mathf.Sin(p*2*Mathf.PI-1.0f)));
            Curve(clip,shoulder+"/"+label+"Elbow/"+label+"Wrist","localEulerAnglesRaw.y",p=>side*(6+7*Mathf.Sin(p*2*Mathf.PI-.8f)));
        }
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.loopBlend=true;
        AnimationUtility.SetAnimationClipSettings(clip,settings);return clip;
    }
    static void Curve(AnimationClip clip,string path,string property,Func<float,float> sample)
    {
        const int count=48;var keys=new Keyframe[count+1];
        for(int i=0;i<=count;i++)
        {float phase=i/(float)count,value=sample(phase),slope=(sample(phase+.0001f)-sample(phase-.0001f))/(.0002f*Cycle);keys[i]=new Keyframe(phase*Cycle,value,slope,slope);}
        var curve=new AnimationCurve(keys){preWrapMode=WrapMode.Loop,postWrapMode=WrapMode.Loop};
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),curve);
    }

    // Sections: z, horizontal radius, vertical radius, vertical centre.
    static Mesh Loft(Vector4[] sections,int sides)
    {
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        for(int r=0;r<sections.Length;r++)for(int j=0;j<sides;j++)
        {float a=j*Mathf.PI*2/sides;var s=sections[r];vertices.Add(new Vector3(Mathf.Cos(a)*s.y,s.w+Mathf.Sin(a)*s.z,s.x));uv.Add(new Vector2(j/(float)sides,r/(float)(sections.Length-1)));}
        for(int r=0;r<sections.Length-1;r++)for(int j=0;j<sides;j++)
        {int a=r*sides+j,c=r*sides+(j+1)%sides,b=a+sides,d=c+sides;triangles.AddRange(new[]{a,c,b,c,d,b});}
        int first=vertices.Count;vertices.Add(new Vector3(0,sections[0].w,sections[0].x));uv.Add(Vector2.zero);
        int last=vertices.Count;var e=sections[sections.Length-1];vertices.Add(new Vector3(0,e.w,e.x));uv.Add(Vector2.one);
        for(int j=0;j<sides;j++){triangles.AddRange(new[]{first,(j+1)%sides,j});int a=(sections.Length-1)*sides+j,b=(sections.Length-1)*sides+(j+1)%sides;triangles.AddRange(new[]{last,a,b});}
        return Mesh(vertices,uv,triangles);
    }
    // Wing sections: span, leading edge Z, trailing edge Z, thickness. Closed elliptical cross-section.
    static Mesh Wing(Vector4[] sections,int side)
    {
        const int sides=10;var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        for(int r=0;r<sections.Length;r++)for(int j=0;j<sides;j++)
        {var s=sections[r];float a=j*Mathf.PI*2/sides;vertices.Add(new Vector3(side*s.x,Mathf.Sin(a)*s.w,(s.y+s.z)*.5f+Mathf.Cos(a)*(s.y-s.z)*.5f));uv.Add(new Vector2(r/(float)(sections.Length-1),j/(float)sides));}
        for(int r=0;r<sections.Length-1;r++)for(int j=0;j<sides;j++)
        {int a=r*sides+j,b=a+sides,c=r*sides+(j+1)%sides,d=c+sides;triangles.AddRange(new[]{a,b,c,c,b,d});}
        int first=vertices.Count;vertices.Add(new Vector3(side*sections[0].x,0,(sections[0].y+sections[0].z)*.5f));uv.Add(Vector2.zero);
        int last=vertices.Count;var e=sections[sections.Length-1];vertices.Add(new Vector3(side*e.x,0,(e.y+e.z)*.5f));uv.Add(Vector2.one);
        for(int j=0;j<sides;j++){triangles.AddRange(new[]{first,j,(j+1)%sides});int a=(sections.Length-1)*sides+j,b=(sections.Length-1)*sides+(j+1)%sides;triangles.AddRange(new[]{last,b,a});}
        if(side<0)for(int i=0;i<triangles.Count;i+=3){int a=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=a;}
        return Mesh(vertices,uv,triangles);
    }
    static Mesh Mesh(List<Vector3> vertices,List<Vector2> uv,List<int> triangles)
    {var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
    static void Ellipsoid(string name,Transform parent,Vector3 center,Vector3 radii,Material material,int sides,int rings)
    {
        var sections=new Vector4[rings+1];
        for(int i=0;i<=rings;i++){float a=Mathf.Lerp(-Mathf.PI*.5f+.015f,Mathf.PI*.5f-.015f,i/(float)rings),radius=Mathf.Cos(a);sections[i]=new Vector4(center.z+Mathf.Sin(a)*radii.z,radius*radii.x,radius*radii.y,center.y);}
        var mesh=Loft(sections,sides);if(center.x!=0){var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i].x+=center.x;mesh.vertices=vertices;mesh.RecalculateBounds();}
        Part(name,parent,mesh,material);
    }
    static Transform Child(string name,Transform parent,Vector3 position)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
    static void Part(string name,Transform parent,Mesh mesh,Material material)
    {
        string id=meshes.Count.ToString("D2")+"_"+name;mesh.name="Seabird_"+id;
        Save(mesh,Folder+"/"+id+".asset");var saved=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+id+".asset");meshes.Add(saved);
        var part=Child(name,parent,Vector3.zero).gameObject;part.AddComponent<MeshFilter>().sharedMesh=saved;
        var renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
    }
    static Material Material(string name,Color color)
    {
        var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new InvalidOperationException("URP Lit shader unavailable.");
        var material=new Material(shader){name="Seabird_"+name};material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.14f);material.SetFloat("_Metallic",0);
        Save(material,Folder+"/"+name+".mat");return AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+name+".mat");
    }
    static void Save(UnityEngine.Object value,string path)
    {var existing=AssetDatabase.LoadMainAssetAtPath(path);if(existing){if(existing.GetType()!=value.GetType())throw new InvalidOperationException("Asset type conflict at "+path);EditorUtility.CopySerialized(value,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(value);}else AssetDatabase.CreateAsset(value,path);}
    static void Validate(GameObject root)
    {
        foreach(var mesh in meshes)
        {
            if(mesh.vertexCount<8||mesh.triangles.Length<12)throw new InvalidOperationException("Empty mesh "+mesh.name);
            foreach(var v in mesh.vertices)if(float.IsNaN(v.x)||float.IsInfinity(v.x)||float.IsNaN(v.y)||float.IsInfinity(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.z))throw new InvalidOperationException("Non-finite mesh "+mesh.name);
            if(mesh.normals.Any(n=>n.sqrMagnitude<.5f))throw new InvalidOperationException("Invalid normals "+mesh.name);
        }
        if(root.GetComponentsInChildren<Collider>().Length!=0)throw new InvalidOperationException("Story birds must not affect interaction or movement.");
        if(root.GetComponentsInChildren<Animator>().Length!=1)throw new InvalidOperationException("Expected one pause-aware Animator.");
    }
}
