using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace HabitatIsland
{
    public sealed class HabitatSpecialPointer : MonoBehaviour
    {
        RawImage graphic;RectTransform rect;bool ownsCursor,previousVisible;
        void Awake()
        {
            var layer=new GameObject("Special pointer overlay",typeof(RectTransform),typeof(Canvas));layer.transform.SetParent(transform,false);
            var canvas=layer.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
            var art=new GameObject("Delivered special pointer",typeof(RectTransform),typeof(RawImage));art.transform.SetParent(layer.transform,false);
            graphic=art.GetComponent<RawImage>();graphic.texture=Resources.Load<Texture2D>("UI/Delivered/SpecialPointer");graphic.raycastTarget=false;
            rect=graphic.rectTransform;rect.pivot=new Vector2(773f/1067f,1-448f/1067f);rect.sizeDelta=new Vector2(40,40);
            graphic.enabled=false;
        }
        void LateUpdate()
        {
            var mouse=Mouse.current;bool focused=Application.isFocused;
            #if UNITY_EDITOR
            focused=focused&&UnityEditor.EditorWindow.focusedWindow!=null&&UnityEditor.EditorWindow.focusedWindow.GetType().Name=="GameView";
            #endif
            Vector2 point=mouse!=null?mouse.position.ReadValue():Vector2.one*-1;
            bool show=focused&&mouse!=null&&graphic.texture&&point.x>=0&&point.y>=0&&point.x<Screen.width&&point.y<Screen.height&&Cursor.lockState==CursorLockMode.None;
            graphic.enabled=show;
            if(show)
            {
                if(!ownsCursor){previousVisible=Cursor.visible;ownsCursor=true;}
                Cursor.visible=false;rect.position=point;
                float scale=Mathf.Clamp(Screen.height/900f,.75f,1.6f);rect.localScale=Vector3.one*scale;
            }
            else Restore();
        }
        void Restore(){if(ownsCursor){Cursor.visible=previousVisible;ownsCursor=false;}}
        void OnApplicationFocus(bool focus){if(!focus){Restore();if(graphic)graphic.enabled=false;}}
        void OnDisable(){Restore();if(graphic)graphic.enabled=false;}
        void OnDestroy(){Restore();}
    }
}
