using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace HabitatIsland
{
    public partial class HabitatGame
    {
        Vector2Int bufferedDirection, preferredDirection;
        float bufferedUntil;
        bool bufferedPull, bufferedClimb, pauseAfterAction, pendingLure;

        void ClearBufferedInput()
        { bufferedDirection=preferredDirection=Vector2Int.zero; bufferedUntil=0; pauseAfterAction=false; }

        static bool Pressed(Keyboard k,int x,int z) => z>0 ? k.wKey.wasPressedThisFrame||k.upArrowKey.wasPressedThisFrame
            : z<0 ? k.sKey.wasPressedThisFrame||k.downArrowKey.wasPressedThisFrame
            : x<0 ? k.aKey.wasPressedThisFrame||k.leftArrowKey.wasPressedThisFrame : k.dKey.wasPressedThisFrame||k.rightArrowKey.wasPressedThisFrame;
        static bool Held(Keyboard k,Vector2Int d) => d.y>0 ? k.wKey.isPressed||k.upArrowKey.isPressed
            : d.y<0 ? k.sKey.isPressed||k.downArrowKey.isPressed
            : d.x<0 ? k.aKey.isPressed||k.leftArrowKey.isPressed : d.x>0 && (k.dKey.isPressed||k.rightArrowKey.isPressed);
        Vector2Int ReadDirection(Keyboard k,out bool first)
        {
            first=false;
            if(Pressed(k,0,1)){preferredDirection=Vector2Int.up;first=true;}
            if(Pressed(k,0,-1)){preferredDirection=Vector2Int.down;first=true;}
            if(Pressed(k,-1,0)){preferredDirection=Vector2Int.left;first=true;}
            if(Pressed(k,1,0)){preferredDirection=Vector2Int.right;first=true;}
            if(preferredDirection!=Vector2Int.zero && Held(k,preferredDirection))return preferredDirection;
            if(Held(k,Vector2Int.up))return Vector2Int.up;
            if(Held(k,Vector2Int.down))return Vector2Int.down;
            if(Held(k,Vector2Int.left))return Vector2Int.left;
            if(Held(k,Vector2Int.right))return Vector2Int.right;
            return Vector2Int.zero;
        }
        void CaptureBufferedInput(Keyboard k)
        {
            if(k==null||Hud.ModalOpen||StoryPlaying)return;
            var direction=ReadDirection(k,out bool first);
            if(first && !State.onHarbor)
            { bufferedDirection=direction;bufferedUntil=Time.unscaledTime+.15f;bufferedPull=k.ctrlKey.isPressed;bufferedClimb=k.shiftKey.isPressed; }
            if(k.escapeKey.wasPressedThisFrame)pauseAfterAction=true;
        }
        bool ConsumeBufferedInput()
        {
            if(pauseAfterAction){pauseAfterAction=false;bufferedDirection=Vector2Int.zero;Hud.HandleEscape();return true;}
            if(bufferedDirection==Vector2Int.zero)return false;
            var d=bufferedDirection;bufferedDirection=Vector2Int.zero;
            if(Time.unscaledTime>bufferedUntil||State.onHarbor||Hud.ModalOpen)return false;
            var step=ScreenDirectionToGrid(d.x,d.y,View.InputCameraRotation);
            if(bufferedPull)Pull(step.x,step.y);else Move(step.x,step.y,bufferedClimb);
            nextMove=Time.unscaledTime+.12f;return true;
        }
        void HandleCameraInput(Keyboard k)
        {
            if(Hud.ModalOpen||!View)return;
            if(k!=null)
            {
                float turn=(k.qKey.wasPressedThisFrame?-90f:0)+(k.cKey.wasPressedThisFrame?90f:0);
                if(turn!=0)View.RotateCamera(turn);
                if(k.mKey.wasPressedThisFrame)View.FocusOverview();
                if(k.homeKey.wasPressedThisFrame)View.FocusPlayer();
            }
            var m=Mouse.current;
            if(m==null||EventSystem.current && EventSystem.current.IsPointerOverGameObject())return;
            if(m.middleButton.isPressed)View.Pan(m.delta.ReadValue());
            float scroll=m.scroll.ReadValue().y;if(Mathf.Abs(scroll)>.01f)View.Zoom(scroll>0?-1:1);
        }
        void BeginLureAction(int x,int z)
        {
            BeginGridAction(s=>HabitatRules.Lure(s,x,z),HabitatPlayerMotion.Walk);
            if(pendingState==null)return;
            pendingLure=true;PlayToolVisual("Cast",x,z);
        }
    }
}
