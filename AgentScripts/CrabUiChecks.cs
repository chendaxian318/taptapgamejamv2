using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public static class CrabUiChecks
{
    static int count;static StringBuilder log;static HabitatGame game;
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Check(bool value,string text){if(!value)throw new Exception("FAIL "+text);log.AppendLine("PASS "+(++count)+" "+text);}
    static async Task Frames(int n=3){for(int i=0;i<n;i++)await Task.Yield();}
    static async Task Until(Func<bool> condition){float end=Time.realtimeSinceStartup+8;while(!condition()&&Time.realtimeSinceStartup<end)await Task.Yield();Check(condition(),"async state arrived");}
    static Button Button(string name)=>game.Hud.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
    static T Field<T>(string name)=> (T)typeof(HabitatHud).GetField(name,Private).GetValue(game.Hud);
    static async Task Screenshot(string name){ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/crab-ui-20261016/"+name+".png"));await Frames(10);}
    public static async Task<string> Run()
    {
        game=HabitatGame.Instance;count=0;log=new StringBuilder("Crab asset and UI checks; test fixtures are not a campaign playthrough.\n");
        var capture=typeof(HabitatGame).GetMethod("CaptureSnapshot",Private);var restore=typeof(HabitatGame).GetMethod("RestoreSnapshot",Private);
        var old=capture.Invoke(game,null);bool paused=game.paused,session=game.SessionStarted;int tool=game.tool;
        string save=Path.Combine(Application.persistentDataPath,"Playtests","latest.json");byte[] previous=File.Exists(save)?File.ReadAllBytes(save):null;
        try
        {
            var prefab=Resources.Load<GameObject>("Models/HermitCrab/HermitCrab");Check(prefab,"prefab loads");
            var clips=prefab.GetComponent<HabitatEquipmentAnimation>();
            Check(clips.Clips.Count==3&&clips.Clips.Single(c=>c.name=="Idle").isLooping&&clips.Clips.Single(c=>c.name=="Walk").isLooping&&!clips.Clips.Single(c=>c.name=="Startle").isLooping,"three delivered clips and correct loop flags");
            Check(prefab.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(s=>s.bones).Distinct().Count()==27,"all 27 bones imported");
            Check(prefab.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m&&m.shader.isSupported)),"PBR shaders valid");
            Check(prefab.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).SelectMany(m=>m.GetTexturePropertyNames().Select(p=>m.GetTexture(p))).Any(t=>t&&t.width==4096),"source 4K texture retained");
            Check(!clips.Animator.applyRootMotion,"animation does not drive grid coordinates");
            var model=UnityEngine.Object.Instantiate(prefab);model.transform.position=Vector3.one*-100;
            try
            {
                var animation=model.GetComponent<HabitatEquipmentAnimation>();var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();
                foreach(var clip in animation.Clips)
                {
                    var mesh=new Mesh();
                    try{animation.Sample(clip.name,0);skin.BakeMesh(mesh,true);var a=mesh.vertices;animation.Sample(clip.name,clip.length*.3f);skin.BakeMesh(mesh,true);Check(a.Zip(mesh.vertices,(x,y)=>(x-y).sqrMagnitude).Max()>.000001,"actual bone deformation "+clip.name);}
                    finally{UnityEngine.Object.Destroy(mesh);}
                }
                var visual=model.AddComponent<HabitatCrabVisual>();
                visual.Render(.02f,Vector3.zero,Vector3.zero,true,false);Check(visual.CurrentClip=="Idle","reset starts idle");
                visual.Render(.02f,Vector3.right*.1f,Vector3.zero,false,false);Check(visual.CurrentClip=="Walk","ordinary visual movement plays walk");
                visual.Render(.02f,Vector3.right*.2f,Vector3.right*.1f,false,true);Check(visual.IsReacting&&visual.CurrentClip=="Startle","shrimp reaction plays single startle");
                float first=animation.TimeSeconds;visual.Render(.3f,Vector3.right*.2f,Vector3.right*.2f,false,false);Check(animation.TimeSeconds>first,"startle not restarted each render");
                visual.Render(1.3f,Vector3.right*.2f,Vector3.right*.2f,false,false);visual.Render(.1f,Vector3.right*.2f,Vector3.right*.2f,false,false);Check(visual.CurrentClip=="Idle","one-shot returns to idle");
                visual.Render(.02f,Vector3.zero,Vector3.zero,false,true);visual.Render(.02f,Vector3.zero,Vector3.zero,true,false);Check(!visual.IsReacting,"state replacement cancels reaction");
            }
            finally{UnityEngine.Object.Destroy(model);}
            game.Hud.ShowStartMenu();await Frames();
            Check(game.Hud.SessionMenuOpen,"new main menu opens");
            var modal=Field<GameObject>("modal");Check(modal.transform.GetSiblingIndex()==modal.transform.parent.childCount-1,"modal covers all HUD controls");
            Check(!Button("Menu 特别收录").interactable&&Button("Menu 特别收录").GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="待开放"),"collection visibly unavailable as requested");
            Check(Field<Button>("continueSession").interactable==game.HasSavedSession,"continue respects actual save availability");
            Check(game.Hud.GetComponentsInChildren<RawImage>(true).Any(i=>i.name=="Delivered logo"&&i.texture),"supplied logo region loaded");
            await Screenshot("main-menu");
            Button("Menu 游戏设置").onClick.Invoke();await Frames();Check(Field<GameObject>("audioPanel").activeInHierarchy,"main settings opens functional audio page");
            game.Hud.HandleEscape();await Frames();Check(game.Hud.SessionMenuOpen,"Escape returns audio submenu to main menu");
            Button("Menu 退出游戏").onClick.Invoke();await Frames();Check(Field<GameObject>("confirmPanel").activeInHierarchy,"quit requires existing explicit confirmation");
            game.Hud.HandleEscape();await Frames();Check(game.Hud.SessionMenuOpen,"cancel quit returns to main menu");
            game.Hud.ClosePanels();game.LoadLevel(7);game.paused=true;await Frames(8);
            var crabs=UnityEngine.Object.FindObjectsByType<HabitatCrabVisual>();
            Check(crabs.Length==game.State.animals.Count(a=>a.species==Species.Crab),"actual map crabs replaced by delivered models");
            Check(crabs.All(c=>c.GetComponentsInChildren<SpriteRenderer>().Length==0&&Mathf.Abs(c.transform.parent.localPosition.y)<.001f),"no old crab sprite or artificial vertical bob");
            game.View.FocusTutorialTarget(new Vector3(6,.8f,8));await Frames(6);await Screenshot("crab-on-map");
            game.Hud.OpenMapSelection();await Frames();var map=Field<HabitatMapSelectionPanel>("mapSelection");
            Check(map.GetComponentsInChildren<Button>(true).Count(b=>b.name.StartsWith("Select destination"))==7,"six formal destinations plus optional tutorial retained");
            Check(!map.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Completed badge"&&t.gameObject.activeSelf),"unfinished maps do not claim CLEAR");
            int level=game.State.levelIndex;map.SelectLevel(8);await Frames();Check(game.State.levelIndex==level&&!game.StoryPlaying,"selecting card does not launch travel");
            game.State.won=true;map.SelectLevel(7);await Frames();
            Check(map.transform.Find("Select destination 7/Completed badge").gameObject.activeSelf,"CLEAR reads actual current island flag");
            game.State.won=false;map.SelectLevel(7);await Frames();Check(!map.transform.Find("Select destination 7/Completed badge").gameObject.activeSelf,"CLEAR clears after undo-like state rollback");
            await Screenshot("map-selection");
            // Layouts fit the smallest Expand canvas (1600x900); 4:3 and ultrawide add safe space.
            var root=(RectTransform)map.transform;
            Check(root.sizeDelta.x<=1600&&root.sizeDelta.y<=900,"selection frame fits minimum responsive canvas");
            foreach(var b in map.GetComponentsInChildren<Button>())
            {
                Vector3[] corners=new Vector3[4];b.GetComponent<RectTransform>().GetWorldCorners(corners);
                Check(corners.All(c=>root.rect.Contains(root.InverseTransformPoint(c))),"map button inside panel: "+b.name);
            }
            Check(game.Hud.GetComponentsInChildren<HabitatUiMotif>(true).All(m=>m.GetComponent<CanvasRenderer>()),"vector motifs have renderers");
            game.Hud.ClosePanels();Button("Delivered game menu").onClick.Invoke();await Frames();Check(game.Hud.PauseMenuOpen,"in-game menu opens pause controls");
            game.Hud.HandleEscape();await Frames();Check(!game.Hud.ModalOpen,"pause closes back to gameplay");
            // Controlled approach fixture uses the real map and real movement API.
            game.LoadLevel(7);var fixture=game.State;
            var shrimp=fixture.animals.Single(a=>a.species==Species.Shrimp);
            fixture.playerX=6;fixture.playerZ=10;fixture.carryingAnimal=shrimp.id;
            shrimp.carried=true;shrimp.x=6;shrimp.z=10;game.View.Initialize(fixture);await Frames(6);
            game.Move(0,-1);await Frames(4);
            var reacting=UnityEngine.Object.FindFirstObjectByType<HabitatCrabVisual>();
            Check(game.Busy&&reacting.CurrentClip=="Startle","real carried-shrimp approach triggers supplied startle during preview");
            Check(game.State.animals.Single(a=>a.species==Species.Crab).z==8,"crab logic remains at source while movement pending");
            game.CancelPendingAction();await Frames(4);
            Check(!reacting.IsReacting&&reacting.CurrentClip=="Idle"&&game.State.animals.Single(a=>a.species==Species.Crab).z==8,"cancel restores source location and idle pose");
            game.Move(0,-1);await Until(()=>!game.Busy);
            Check(game.State.animals.Single(a=>a.species==Species.Crab).z==7,"visual clip never adds an extra logic step");
            await Until(()=>game.View.ResidentsArrived(game.State));
            Check(!reacting.IsReacting,"reaction ends without looping");
            game.Undo();await Frames(4);Check(!reacting.IsReacting&&game.State.animals.Single(a=>a.species==Species.Crab).z==8,"undo does not replay a false scare");
            return "PASS "+count+" crab and UI checks.";
        }
        finally
        {
            game.CancelPendingAction();restore.Invoke(game,new[]{old});game.paused=paused;game.tool=tool;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(game,session);
            Check(previous==null?!File.Exists(save):previous.SequenceEqual(File.ReadAllBytes(save)),"persistent user save unchanged");
            File.WriteAllText("Verification/crab-ui-20261016/checks.txt",log.ToString());
        }
    }
}
