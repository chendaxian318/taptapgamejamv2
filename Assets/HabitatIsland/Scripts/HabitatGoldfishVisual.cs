using UnityEngine;
namespace HabitatIsland
{
    // Root translation belongs to HabitatView; these supplied clips contain no root motion.
    public sealed class HabitatGoldfishVisual:MonoBehaviour
    {
        HabitatEquipmentAnimation clips;
        Vector3 heading;
        float still;
        public string CurrentClip=>clips?clips.CurrentClipName:"";
        public void Render(float dt,Vector3 position,Vector3 before,bool reset)
        {
            if(!clips)clips=GetComponent<HabitatEquipmentAnimation>();
            if(!clips)return;
            Vector3 delta=position-before;delta.y=0;
            if(reset||delta.sqrMagnitude>4){heading=Vector3.zero;still=1;clips.Play("Idle");}
            bool moving=dt>0&&delta.magnitude/dt>.08f;
            if(moving)
            {
                float turn=heading.sqrMagnitude>.1f?Vector3.SignedAngle(heading,delta,Vector3.up):0;
                if(Mathf.Abs(turn)>40)clips.Play(turn>0?"Turn_Right":"Turn_Left",false,1,true);
                heading=delta.normalized;still=0;
            }
            else still+=dt;
            bool turning=clips.CurrentClipName.StartsWith("Turn_")&&!clips.Finished;
            if(!turning)clips.Play(still<.12f?"Swim":"Idle");
            clips.Tick(dt);
        }
    }
}
