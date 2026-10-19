using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
public static class DeliveredUiChecks
{
    static StringBuilder log;static int count;const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Check(bool b,string s){if(!b)throw new Exception("FAIL "+s);log.AppendLine("PASS "+(++count)+" "+s);}
    static async Task Frames(int n=4){for(int i=0;i<n;i++)await Task.Yield();}
    static async Task Until(Func<bool> p){float end=Time.realtimeSinceStartup+8;while(!p()&&Time.realtimeSinceStartup<end)await Task.Yield();Check(p(),"transition reached expected state");}
    static async Task Shot(string name){ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/crab-ui-20261016/"+name+".png"));await Frames(8);}
    public static async Task<string> Run()
    {
        var g=HabitatGame.Instance;if(!g)throw new Exception("Requires play mode");log=new StringBuilder();count=0;
        var oldFocus=EditorWindow.focusedWindow;var mouse=Mouse.current;Vector2 oldMouse=mouse!=null?mouse.position.ReadValue():Vector2.zero;
        var capture=typeof(HabitatGame).GetMethod("CaptureSnapshot",Private);string before=JsonUtility.ToJson(capture.Invoke(g,null));
        string latest=Path.Combine(Application.persistentDataPath,"Playtests","latest.json");byte[] bytes=File.Exists(latest)?File.ReadAllBytes(latest):null;
        try
        {
            g.Hud.ClosePanels();g.Hud.ShowStartMenu();await Frames();
            var entrance=g.Hud.GetComponentInChildren<HabitatMenuEntrance>(true);Check(entrance,"entrance component attached");
            entrance.gameObject.SetActive(false);typeof(HabitatMenuEntrance).GetField("seen",Private).SetValue(entrance,false);entrance.gameObject.SetActive(true);
            Check(entrance.IsPlaying,"first opening starts animation");
            var start=g.Hud.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Start new journey");
            Check(!start.GetComponent<CanvasGroup>().interactable,"menu cannot start game through opening");
            var logo=g.Hud.GetComponentsInChildren<RawImage>(true).Single(i=>i.name=="Delivered logo");
            Check(logo.texture.width==4096&&logo.texture.mipmapCount>1,"high resolution logo has filtered minification");
            Check(logo.rectTransform.sizeDelta.x>600,"opening logo centered and enlarged");await Shot("hd-opening");
            await Until(()=>!entrance.IsPlaying);await Frames();
            Check(start.GetComponent<CanvasGroup>().interactable&&logo.rectTransform.sizeDelta.x==316,"natural completion reveals interactive menu and final logo");
            float fadeEnd=Time.realtimeSinceStartup+.3f;while(Time.realtimeSinceStartup<fadeEnd)await Task.Yield();await Shot("hd-main-menu");
            entrance.gameObject.SetActive(false);entrance.gameObject.SetActive(true);Check(!entrance.IsPlaying,"return to menu does not replay entrance");
            entrance.gameObject.SetActive(false);typeof(HabitatMenuEntrance).GetField("seen",Private).SetValue(entrance,false);entrance.gameObject.SetActive(true);
            g.Hud.HandleEscape();Check(!entrance.IsPlaying&&g.Hud.SessionMenuOpen,"Escape skips animation without dismissing menu");await Frames();
            Check(start.GetComponent<CanvasGroup>().interactable,"skip enables menu after event frame");
            Check(!entrance.HasCover,"unfinished art cover is not replaced by beach photo");
            var shader=Resources.Load<Shader>("UI/Delivered/CoverBlur");Check(shader&&shader.isSupported,"Gaussian blur shader compiles on current graphics device");
            // Exercise blur on a disposable test texture; it is never saved as cover art.
            var source=new Texture2D(32,32);var pixels=Enumerable.Range(0,1024).Select(i=>(i%32)/8%2==0?Color.black:Color.white).ToArray();source.SetPixels(pixels);source.Apply();
            var target=new RenderTexture(32,32,0);var material=new Material(shader);var read=new Texture2D(32,32);var active=RenderTexture.active;
            try{material.SetVector("_Direction",new Vector4(3f/32,0,0,0));Graphics.Blit(source,target,material);RenderTexture.active=target;read.ReadPixels(new Rect(0,0,32,32),0,0);read.Apply();Check(read.GetPixels().Any(c=>c.r>.1f&&c.r<.9f),"Gaussian pass produces intermediate samples");}
            finally{RenderTexture.active=active;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(source);UnityEngine.Object.Destroy(material);UnityEngine.Object.Destroy(read);}
            g.Hud.ClosePanels();g.Hud.OpenMapSelection();await Frames();
            var map=g.Hud.GetComponentInChildren<HabitatMapSelectionPanel>();var photo=map.GetComponentsInChildren<RawImage>().Single(i=>i.name=="Delivered beach background");
            Check(photo.texture==Resources.Load<Texture2D>("UI/Delivered/MapBeach"),"map uses supplied beach image");
            Check(!photo.raycastTarget,"photo never intercepts destinations");
            Check(Mathf.Abs(photo.uvRect.width*photo.texture.width/(photo.uvRect.height*photo.texture.height)-Screen.width/(float)Screen.height)<.001f,"portrait photograph fills landscape without stretching");
            var card=map.GetComponentsInChildren<Button>().Single(b=>b.name=="Select destination 5");
            Vector2 point=RectTransformUtility.WorldToScreenPoint(null,card.GetComponent<RectTransform>().TransformPoint(card.GetComponent<RectTransform>().rect.center));
            var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==card,"pointer raycast hits actual destination button above photograph");
            int level=g.State.levelIndex;ExecuteEvents.Execute(card.gameObject,data,ExecuteEvents.pointerClickHandler);Check(map.SelectedLevel==5&&g.State.levelIndex==level,"click selects correct island without travel");
            var pointer=g.Hud.GetComponent<HabitatSpecialPointer>();var icon=pointer.GetComponentsInChildren<RawImage>(true).Single(i=>i.name=="Delivered special pointer");
            Check(icon.texture.name=="SpecialPointer"&&!icon.raycastTarget&&icon.canvas.sortingOrder==32000,"dedicated delivered cursor overlays UI without blocking clicks");
            Check(icon.GetComponentInParent<GraphicRaycaster>()==null,"pointer overlay has no input raycaster");
            Check(Vector2.Distance(icon.rectTransform.pivot,new Vector2(773f/1067f,1-448f/1067f))<.0001f,"hotspot matches main triangle tip");
            var gameViewType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");EditorWindow.GetWindow(gameViewType).Focus();await Frames();
            if(mouse!=null)
            {
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});await Frames();
                if(Application.isFocused&&EditorWindow.focusedWindow!=null&&EditorWindow.focusedWindow.GetType().Name=="GameView")
                {
                    Check(icon.enabled&&!Cursor.visible,"focused game view displays custom pointer and hides standard cursor");
                    Check(Vector2.Distance(icon.rectTransform.position,point)<1,"visual tip stays on pointer coordinates");
                    await Shot("hd-map-pointer");
                }
                else Check(!icon.enabled&&Cursor.visible,"unfocused editor preserves system cursor");
                pointer.enabled=false;await Frames();Check(Cursor.visible&&!icon.enabled,"disabling pointer restores system cursor");pointer.enabled=true;
            }
            Check(JsonUtility.ToJson(capture.Invoke(g,null))==before,"UI transitions and map selection leave gameplay snapshot untouched");
            return "PASS "+count+" delivered UI checks";
        }
        finally
        {
            g.Hud.ClosePanels();g.Hud.ShowStartMenu();
            if(mouse!=null)InputSystem.QueueStateEvent(mouse,new MouseState{position=oldMouse});if(oldFocus)oldFocus.Focus();
            Check(bytes==null?!File.Exists(latest):bytes.SequenceEqual(File.ReadAllBytes(latest)),"persistent save unchanged");
            File.WriteAllText("Verification/crab-ui-20261016/ui-checks.txt",log.ToString());
        }
    }
}
