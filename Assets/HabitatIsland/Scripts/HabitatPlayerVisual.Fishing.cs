using UnityEngine;
namespace HabitatIsland
{
 public sealed partial class HabitatPlayerVisual
 {
  int fishingPoseStage;
  float fishingPoseTime;
  Vector3 fishingFacingTarget, fishingSmoothedHand;
  float fishingSmoothedPitch=37f, fishingSmoothedLean;
  bool fishingPoseInitialized;
  public int FishingPoseStage => fishingPoseStage;
  public void SetFishingPose(int stage,float time,Vector3 target,bool held=false)
  {
   if(fishingPoseStage==0)fishingPoseInitialized=false;
   if(fishingPoseStage!=stage)fishingFacingTarget=target;
   fishingPoseStage=stage;fishingPoseTime=time;
   equipmentTarget=target;hasEquipmentTarget=true;equipmentFishing=true;
  }
  void ApplyFishingBodyPose(float side,ref Vector3 hand,ref float pitch)
  {
   if(fishingPoseStage==0)return;
   float lean=0;
   if(fishingPoseStage==1)
   {
    float wind=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.30f,fishingPoseTime));
    float cast=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.30f,.70f,fishingPoseTime));
    float settle=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.70f,1,fishingPoseTime));
    pitch=Mathf.Lerp(Mathf.Lerp(37,-55,wind),72,cast);pitch=Mathf.Lerp(pitch,37,settle);
    hand=new Vector3(side*.17f,Mathf.Lerp(.78f,.94f,wind)*(1-cast)+.68f*cast,Mathf.Lerp(-.12f,.42f,cast));
    hand=Vector3.Lerp(hand,new Vector3(side*.12f,.78f,.18f),settle);
    lean=Mathf.Lerp(-16*wind,19,cast)*(1-settle);
   }
   else if(fishingPoseStage==2)
   {
    float pull=(Mathf.Sin(fishingPoseTime*4.8f)+1)*.5f;
    lean=-6-pull*9;pitch=18+pull*25;
    hand=new Vector3(side*.15f,.77f+pull*.09f,.26f-pull*.13f);
   }
   else if(fishingPoseStage==3)
   {
    float lift=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.36f,fishingPoseTime));
    float settle=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.78f,1,fishingPoseTime));
    lean=-22*lift*(1-settle);pitch=Mathf.Lerp(Mathf.Lerp(37,-32,lift),37,settle);
    hand=Vector3.Lerp(new Vector3(side*.17f,.78f+.18f*lift,.27f-.22f*lift),new Vector3(side*.12f,.78f,.18f),settle);
   }
   if(!fishingPoseInitialized){fishingSmoothedHand=new Vector3(side*.12f,.78f,.18f);fishingSmoothedPitch=37;fishingSmoothedLean=0;fishingPoseInitialized=true;}
   float blend=1-Mathf.Exp(-Mathf.Min(Time.unscaledDeltaTime,.08f)*18f);
   fishingSmoothedHand=Vector3.Lerp(fishingSmoothedHand,hand,blend);
   fishingSmoothedPitch=Mathf.Lerp(fishingSmoothedPitch,pitch,blend);fishingSmoothedLean=Mathf.Lerp(fishingSmoothedLean,lean,blend);
   hand=fishingSmoothedHand;pitch=fishingSmoothedPitch;lean=fishingSmoothedLean;
   poseRoot.localRotation=Quaternion.Euler(lean,0,0);
   poseRoot.localPosition=Vector3.up*(-Mathf.Abs(lean)*.002f);
  }
 }
}
