using UnityEngine;

namespace HabitatIsland
{
    public partial class HabitatView
    {
        public const float RecoveryDuration = 2.15f;
        bool recoveryPresentationActive;
        float recoveryElapsed;
        Transform recoveryProp,recoveryPropParent;
        Vector3 recoveryPropLocal,recoveryPropStart,recoveryPropEnd,recoveryPlayerStart,recoveryPlayerEnd,recoveryFacing;
        Quaternion recoveryPropRotation;
        public bool RecoveryPresentationValid => recoveryPresentationActive && recoveryProp && recoveryProp.gameObject.activeInHierarchy
            && player && player.gameObject.activeInHierarchy && PlayerVisual && PlayerVisual.IsModelActive && PlayerVisual.RecoveryNetReady;
        public bool RecoveryPresentationComplete => RecoveryPresentationValid && recoveryElapsed>=RecoveryDuration
            && (player.position-recoveryPlayerEnd).sqrMagnitude<.000001f
            && (recoveryProp.position-recoveryPropEnd).sqrMagnitude<.000001f;
        public float RecoveryProgress => recoveryPresentationActive ? Mathf.Clamp01(recoveryElapsed/RecoveryDuration) : 0f;
        public Vector3 RecoveryPropPosition => recoveryProp ? recoveryProp.position : Vector3.zero;

        public bool BeginRecoveryPresentation(HabitatState before,HabitatState after,int x,int z)
        {
            if(recoveryPresentationActive||gridStepActive||!player||tiles==null||!HabitatRules.Inside(before,x,z))return false;
            Transform prop=tiles[x+z*before.width].prop;
            if(!prop||!PlayerVisual||!PlayerVisual.BeginRecoveryPose())return false;
            recoveryProp=prop;recoveryPropParent=prop.parent;recoveryPropLocal=prop.localPosition;
            recoveryPropRotation=prop.rotation;recoveryPropStart=prop.position;
            recoveryPropEnd=PropSurfacePosition(after,before.playerX+before.playerZ*before.width);
            recoveryPlayerStart=player.position;recoveryPlayerEnd=PlayerPosition(after);
            recoveryFacing=new Vector3(after.facingX,0,after.facingZ);
            // Move the existing prop, not an extra logical or rendered crate. Keep
            // source terrain, water and mechanisms intact until the controller commits.
            prop.SetParent(world,true);
            recoveryElapsed=0;recoveryPresentationActive=true;
            PlayerVisual.SetRecoveryPose(0,prop.position+Vector3.up*.3f);
            return true;
        }

        static float RecoveryEase(float time,float start,float end)
            => Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(start,end,time));

        void RenderRecoveryPresentation(float dt)
        {
            if(!RecoveryPresentationValid)return;
            recoveryElapsed=Mathf.Min(RecoveryDuration,recoveryElapsed+dt);
            float t=recoveryElapsed;
            float lift=RecoveryEase(t,.28f,.82f),draw=RecoveryEase(t,.92f,1.58f),settle=RecoveryEase(t,1.58f,1.88f);
            float retreat=RecoveryEase(t,.72f,1.48f);
            Vector3 p=Vector3.Lerp(recoveryPlayerStart,recoveryPlayerEnd,retreat);
            // Leave the edge before descending onto a lower retreat cell.
            if(recoveryPlayerEnd.y<recoveryPlayerStart.y-.08f)
                p.y=Mathf.Lerp(recoveryPlayerStart.y,recoveryPlayerEnd.y,RecoveryEase(t,1.04f,1.55f));
            player.position=p;
            float raisedY=Mathf.Max(recoveryPropStart.y,recoveryPropEnd.y)+.30f;
            Vector3 box=Vector3.Lerp(recoveryPropStart,recoveryPropEnd,draw);
            box.y=Mathf.Lerp(Mathf.Lerp(recoveryPropStart.y,raisedY,lift),recoveryPropEnd.y,settle);
            recoveryProp.position=box;
            float tilt=Mathf.Sin(draw*Mathf.PI)*7f*(1f-settle);
            recoveryProp.rotation=Quaternion.AngleAxis(tilt,Vector3.Cross(Vector3.up,recoveryFacing))*recoveryPropRotation;
            if(t>=1.88f){recoveryProp.position=recoveryPropEnd;recoveryProp.rotation=recoveryPropRotation;}
            if(t>=1.55f)player.position=recoveryPlayerEnd;
            playerDestination=recoveryPlayerEnd;
            PlayerVisual.SetMotion(HabitatPlayerMotion.Pull);
            PlayerVisual.SetRecoveryPose(t/RecoveryDuration,box+Vector3.up*.30f);
        }

        public void EndRecoveryPresentation(bool cancelled)
        {
            if(!recoveryPresentationActive)return;
            recoveryPresentationActive=false;
            if(recoveryProp)
            {
                recoveryProp.SetParent(recoveryPropParent,true);
                recoveryProp.localPosition=recoveryPropLocal;
                recoveryProp.rotation=recoveryPropRotation;
            }
            if(cancelled&&player){player.position=recoveryPlayerStart;playerDestination=recoveryPlayerStart;}
            recoveryProp=null;recoveryPropParent=null;recoveryElapsed=0;
            if(PlayerVisual)
            {
                PlayerVisual.EndRecoveryPose();
                PlayerVisual.ResetVisual(state!=null?PlayerFacing(state):Vector3.forward);
            }
        }
    }
}
