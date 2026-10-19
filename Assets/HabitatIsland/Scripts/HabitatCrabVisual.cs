using UnityEngine;
namespace HabitatIsland
{
    // Supplied root stays fixed. Grid movement and gameplay remain owned by Game/View.
    public sealed class HabitatCrabVisual : MonoBehaviour
    {
        HabitatEquipmentAnimation clips;
        bool initialized;
        float still=1f;
        bool shellKnown,shellWasShed;
        float exitTime;
        Vector3 restPosition;
        SkinnedMeshRenderer bodyRenderer;
        Mesh originalMesh,withoutShell;
        public string CurrentClip=>exitTime>0?"ShellExit":clips?clips.CurrentClipName:"";
        public bool IsReacting=>exitTime>0 || clips&&clips.CurrentClipName=="Startle"&&!clips.Finished;
        public void Render(float dt,Vector3 position,Vector3 before,bool reset,bool startled,bool shed=false)
        {
            if(!clips)clips=GetComponent<HabitatEquipmentAnimation>();
            if(!clips)return;
            Vector3 delta=position-before;delta.y=0;
            if(!initialized||reset||delta.sqrMagnitude>4)
            {initialized=true;still=1;clips.Play("Idle",true,1,true);clips.Tick(dt);RenderShell(dt,shed);return;}
            bool moving=dt>0&&delta.magnitude/dt>.08f;
            still=moving?0:still+dt;
            if(startled)clips.Play("Startle",false,1,true);
            if(!IsReacting)clips.Play(still<.12f?"Walk":"Idle");
            clips.Tick(dt);
            RenderShell(dt,shed);
        }
        void RenderShell(float dt,bool shed)
        {
            if(!shellKnown)
            {
                shellKnown=true;shellWasShed=shed;restPosition=transform.localPosition;
                bodyRenderer=GetComponentInChildren<SkinnedMeshRenderer>();
                if(bodyRenderer)originalMesh=bodyRenderer.sharedMesh;
                withoutShell=Resources.Load<Mesh>("Models/HermitCrab/BodyWithoutShell");
            }
            if(shed&&!shellWasShed){exitTime=1.1f;clips.Play("Walk",true,1.25f,true);}
            if(!shed)exitTime=0;
            shellWasShed=shed;
            if(bodyRenderer)bodyRenderer.sharedMesh=shed&&withoutShell?withoutShell:originalMesh;
            exitTime=Mathf.Max(0,exitTime-dt);
            // A short step out of the original rig shell; state is settled by Game, never here.
            float t=1-exitTime/1.1f;
            transform.localPosition=restPosition+Vector3.forward*(exitTime>0?Mathf.Sin(t*Mathf.PI)*.16f:0);
        }
    }
}
