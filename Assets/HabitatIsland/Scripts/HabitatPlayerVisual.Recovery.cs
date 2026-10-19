using UnityEngine;

namespace HabitatIsland
{
    public sealed partial class HabitatPlayerVisual
    {
        Transform recoveryNet;
        bool recoveryPose;
        float recoveryPoseProgress;
        Vector3 recoveryNetTarget;
        public bool RecoveryNetReady => recoveryPose && recoveryNet && recoveryNet.gameObject.activeSelf;
        public Transform RecoveryNetRoot => recoveryNet;

        public bool BeginRecoveryPose()
        {
            if(!IsModelActive||!rightHand||!leftHand)return false;
            if(!recoveryNet)
            {
                var prefab=Resources.Load<GameObject>("Models/Story/LandingNet");
                if(!prefab)return false;
                recoveryNet=Instantiate(prefab,transform,false).transform;
                recoveryNet.name="Recovery landing net";
                foreach(var c in recoveryNet.GetComponentsInChildren<Collider>(true))c.enabled=false;
            }
            CancelEquipmentAction();HideEquipment();
            recoveryPose=true;recoveryPoseProgress=0;
            recoveryNet.gameObject.SetActive(true);
            return true;
        }

        public void SetRecoveryPose(float progress,Vector3 target)
        { recoveryPoseProgress=Mathf.Clamp01(progress);recoveryNetTarget=target; }

        public void EndRecoveryPose()
        {
            recoveryPose=false;
            if(recoveryNet)recoveryNet.gameObject.SetActive(false);
        }

        void PoseRecoveryNet()
        {
            if(!RecoveryNetReady||!facingRoot)return;
            float p=recoveryPoseProgress;
            float reach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.14f,p));
            float lift=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.14f,.40f,p));
            float stow=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.88f,1,p));
            poseRoot.localRotation=Quaternion.Euler(Mathf.Lerp(12f,-10f,lift)*(1f-stow),0,0);
            poseRoot.localPosition=Vector3.up*(-.035f*(1f-stow));
            float side=facingRoot.InverseTransformPoint(rightArm.position).x>=0?1:-1;
            Vector3 handLocal=new Vector3(side*.15f,Mathf.Lerp(.60f,.83f,lift),Mathf.Lerp(.34f,.18f,lift));
            handLocal=Vector3.Lerp(new Vector3(side*.2f,.65f,.12f),handLocal,reach*(1f-stow));
            PoseArm(rightArm,rightForearm,rightHand,handLocal,new Vector3(side*.43f,.45f,.12f),1);
            Vector3 palm=rightHand.position;
            Vector3 hoopDirection=recoveryNetTarget-palm;
            if(hoopDirection.sqrMagnitude<.01f)hoopDirection=facingRoot.forward;
            Quaternion scoop=Quaternion.FromToRotation(facingRoot.up,hoopDirection.normalized)*facingRoot.rotation;
            Quaternion stowed=facingRoot.rotation*Quaternion.Euler(12,180,side*22);
            recoveryNet.SetPositionAndRotation(palm,Quaternion.Slerp(stowed,scoop,reach*(1f-stow)));
            // The net is rigid: animate the grip and angle, never stretch its mesh.
            recoveryNet.localScale=Vector3.one*.95f;
            Vector3 support=recoveryNet.TransformPoint(new Vector3(0,.19f,0));
            PoseArmWorld(leftArm,leftForearm,leftHand,support,facingRoot.TransformPoint(new Vector3(-side*.40f,.45f,.10f)),1f);
        }
    }
}
