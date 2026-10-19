using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace HabitatIsland.Editor
{
    /// <summary>Creates the playable prefab from the supplied GLB without editing that source file.</summary>
    public static class HabitatTravelerSetup
    {
        public const string Source = "Assets/HabitatIsland/Art/MeshyTraveler/MeshyTraveler.glb";
        public const string EmptyHandsSource = "Assets/HabitatIsland/Art/MeshyTraveler/MeshyTraveler_EmptyHands.glb";
        public const string Folder = "Assets/HabitatIsland/Resources/Models/MeshyTraveler";
        public const string PrefabPath = Folder + "/Player.prefab";
        public const float PlayerHeight = 1.55f;

        public static Bounds BakedBounds(GameObject root)
        {
            bool initialized=false; var result=new Bounds();
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh=new Mesh();
                try
                {
                    // This GLB contains a .01 armature scale. Bake with scale, then transform
                    // vertices back to the prefab's space so bind-pose compensation is counted once.
                    skin.BakeMesh(mesh,true);
                    foreach(var vertex in mesh.vertices)
                    {
                        Vector3 p=root.transform.InverseTransformPoint(skin.transform.TransformPoint(vertex));
                        if(!initialized){result=new Bounds(p,Vector3.zero);initialized=true;}else result.Encapsulate(p);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
            if(!initialized)throw new InvalidOperationException("Traveler has no skinned vertices");
            return result;
        }

        static AnimationClip PrepareClip(AnimationClip source,float hipX,float hipZ)
        {
            string path=Folder+"/"+source.name+".anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(!clip){clip=UnityEngine.Object.Instantiate(source);clip.name=source.name;AssetDatabase.CreateAsset(clip,path);}
            else EditorUtility.CopySerialized(source,clip);
            clip.legacy=false;
            var settings=AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime=true;settings.loopBlend=true;settings.keepOriginalPositionXZ=true;
            AnimationUtility.SetAnimationClipSettings(clip,settings);
            foreach(var binding in AnimationUtility.GetCurveBindings(clip))
            {
                // Grid movement is authoritative. Preserve vertical gait and all joint rotations.
                if(!binding.path.EndsWith("mixamorig:Hips",StringComparison.Ordinal))continue;
                if(binding.propertyName=="m_LocalPosition.x"||binding.propertyName=="m_LocalPosition.z")
                {
                    float value=binding.propertyName.EndsWith(".x",StringComparison.Ordinal)?hipX:hipZ;
                    AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.Constant(0,source.length,value));
                }
            }
            EditorUtility.SetDirty(clip);return clip;
        }

        public static string Prepare()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before preparing persistent assets");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(EmptyHandsSource);
            if(!source)throw new InvalidOperationException("Empty-hands traveler GLB has not been imported: "+EmptyHandsSource);
            var sourceClips=AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>().ToArray();
            AnimationClip idleSource=sourceClips.Single(c=>c.name=="Idle_9");
            var instance=UnityEngine.Object.Instantiate(source);
            var root=new GameObject("Meshy Traveler");
            try
            {
                instance.name="Traveler rig";instance.transform.SetParent(root.transform,false);
                idleSource.SampleAnimation(instance,0);
                var hips=instance.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="mixamorig:Hips");
                float hipX=hips.localPosition.x,hipZ=hips.localPosition.z;
                var idle=PrepareClip(idleSource,hipX,hipZ);
                var walk=PrepareClip(sourceClips.Single(c=>c.name=="Walking"),hipX,hipZ);
                var run=PrepareClip(sourceClips.Single(c=>c.name=="Running"),hipX,hipZ);
                Bounds bounds=BakedBounds(root);
                if(bounds.size.y<.5f||bounds.size.y>3f)throw new InvalidOperationException("Unexpected rig height: "+bounds.size.y);
                float scale=PlayerHeight/bounds.size.y;
                instance.transform.localScale=Vector3.one*scale;
                // Keep the skeleton's X/Z origin as the grid pivot; align the lowest foot/cloth vertex.
                instance.transform.localPosition=Vector3.up*(-bounds.min.y*scale);

                string materialPath=Folder+"/Traveler.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,materialPath);}
                var original=instance.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
                material.SetTexture("_BaseMap",original.GetTexture("baseColorTexture"));
                material.SetTextureScale("_BaseMap",original.GetTextureScale("baseColorTexture"));
                material.SetTextureOffset("_BaseMap",original.GetTextureOffset("baseColorTexture"));
                material.SetColor("_BaseColor",Color.white);material.SetFloat("_Metallic",0);
                material.SetFloat("_Smoothness",.20f);material.SetFloat("_Cull",0);
                material.SetColor("_EmissionColor",Color.black);material.DisableKeyword("_EMISSION");
                material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                EditorUtility.SetDirty(material);
                foreach(var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.sharedMaterials=skin.sharedMaterials.Select(_=>material).ToArray();
                    skin.updateWhenOffscreen=true;skin.shadowCastingMode=ShadowCastingMode.On;
                    skin.receiveShadows=true;skin.skinnedMotionVectors=true;
                }

                string controllerPath=Folder+"/Traveler.controller";
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if(!controller)
                {
                    controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                    controller.AddParameter("MoveSpeed",AnimatorControllerParameterType.Float);
                    var machine=controller.layers[0].stateMachine;
                    var state=machine.AddState("Locomotion");machine.defaultState=state;
                    var tree=new BlendTree{name="Idle Walk Run",blendType=BlendTreeType.Simple1D,blendParameter="MoveSpeed",useAutomaticThresholds=false};
                    AssetDatabase.AddObjectToAsset(tree,controller);
                    tree.AddChild(idle,0);tree.AddChild(walk,1.6f);tree.AddChild(run,4.5f);
                    state.motion=tree;EditorUtility.SetDirty(controller);
                }
                foreach(var legacy in instance.GetComponentsInChildren<Animation>(true))UnityEngine.Object.DestroyImmediate(legacy);
                var animator=instance.GetComponent<Animator>()??instance.AddComponent<Animator>();
                animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
                animator.updateMode=AnimatorUpdateMode.UnscaledTime;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                AssetDatabase.SaveAssets();
                string report="Imported empty-hands GLB variant with "+sourceClips.Length+" original animation clips.\n"+
                    "Source idle baked bounds: "+bounds+"\nPlayable height: "+PlayerHeight+"; uniform scale: "+scale+
                    "\nPrefab: "+PrefabPath+"\nLocomotion: Idle_9 / Walking / Running; hips XZ locked to idle origin; no root motion.\n"+
                    "Material: URP Lit, original embedded base color, metallic 0, smoothness .20, no duplicated emission.\n"+
                    "Only exposed hook/handle triangles removed; grasp opening capped with existing glove UVs. Original GLB preserved.\n"+
                    "Runtime character scale: "+HabitatPlayerVisual.CharacterScale+"; effective height: "+PlayerHeight*HabitatPlayerVisual.CharacterScale+".\n";
                File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../Verification/meshy-traveler-import.txt")),report);
                return report;
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
