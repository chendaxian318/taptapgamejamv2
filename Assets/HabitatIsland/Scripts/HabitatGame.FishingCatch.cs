using UnityEngine;
namespace HabitatIsland
{
 public partial class HabitatGame
 {
  public const float FishingLandingDuration=2.65f;
  public bool FishingLanding {get;private set;}
  public float FishingLandingProgress=>FishingLanding?fishingCatchTime/FishingLandingDuration:0;
  float fishingCatchTime;
  Transform caughtFish;
  Vector3 caughtFishStart,caughtFishScale;
  HabitatEquipmentAnimation caughtFishAnimation;
  HabitatState fishingSourceState;
  public Transform FishingCatchVisual=>caughtFish;

  void BeginFishingCatch(int x,int z,Species species)
  {
   var candidate=HabitatRules.Clone(State);
   if(!HabitatRules.Fish(candidate,x,z,species,tuning)){State.message=candidate.message;return;}
   var prefab=Resources.Load<GameObject>(species==Species.Jellyfish?"Models/Jellyfish/Jellyfish":"Models/Goldfish/Goldfish");
   if(!prefab||!View||!View.PlayerVisual||!View.PlayerVisual.IsModelActive){State.message="收鱼动画暂不可用，请重新尝试。";return;}
   tool=4;PlayToolVisual("Hook_Pull",x,z);
   caughtFish=Instantiate(prefab).transform;caughtFish.name="Fishing catch · uncommitted offspring";
   foreach(var c in caughtFish.GetComponentsInChildren<Collider>(true))c.enabled=false;
   foreach(var body in caughtFish.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.useGravity=false;}
   foreach(var skin in caughtFish.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
   caughtFishAnimation=caughtFish.GetComponentInChildren<HabitatEquipmentAnimation>();if(caughtFishAnimation)caughtFishAnimation.Play("Swim",true);
   Bounds bounds=new Bounds();bool any=false;foreach(var renderer in caughtFish.GetComponentsInChildren<Renderer>()){if(!any){bounds=renderer.bounds;any=true;}else bounds.Encapsulate(renderer.bounds);}
   if(any)caughtFish.localScale*=.62f/Mathf.Max(.001f,Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z)));
   caughtFishScale=caughtFish.localScale;caughtFishStart=new Vector3(x,HabitatRules.WaterSurfaceY(State.waterLevel)-.18f,z);caughtFish.position=caughtFishStart;
   pendingBefore=HabitatRules.Clone(State);pendingState=candidate;pendingSimulation=false;pendingMotion=HabitatPlayerMotion.Walk;
   fishingSourceState=State;fishingCatchTime=0;FishingLanding=true;ClearBufferedInput();
   View.PlayerVisual.SetFishingPose(3,0,caughtFishStart);
  }
  void TickFishingCatch()
  {
   if(!FishingLanding)return;
   if(!ReferenceEquals(State,fishingSourceState)||pendingState==null||pendingBefore==null||!caughtFish||!caughtFish.gameObject.activeInHierarchy||!View||!View.PlayerVisual||!View.PlayerVisual.IsModelActive){CancelPendingAction();return;}
   // Hold the final pose for a rendered frame before committing the reward.
   if(fishingCatchTime>=FishingLandingDuration&&View.ResidentsArrived(State))
   {
    CommitPending();pendingState=pendingBefore=null;pendingSimulation=false;
    FinishFishingCatch();HabitatAudio.PlayAction(HabitatActionCue.Harvest);return;
   }
   fishingCatchTime=Mathf.Min(FishingLandingDuration,fishingCatchTime+Mathf.Min(Time.unscaledDeltaTime,.08f));float t=fishingCatchTime/FishingLandingDuration;
   Transform basket=View.PlayerVisual.BackBasketRim;
   Vector3 end=basket?basket.position:View.PlayerWorldPosition+Vector3.up*.75f;
   float draw=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.83f,t));
   Vector3 position=Vector3.Lerp(caughtFishStart,end,draw)+Vector3.up*Mathf.Sin(draw*Mathf.PI)*1.25f;
   caughtFish.position=position;caughtFish.rotation=Quaternion.Euler(-25+Mathf.Sin(t*16)*10,Mathf.Atan2(end.x-caughtFishStart.x,end.z-caughtFishStart.z)*Mathf.Rad2Deg,Mathf.Sin(t*18)*12);
   // Tuck down inside the basket, rather than disappearing while still suspended.
   float stow=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.83f,.94f,t));
   caughtFish.position-=Vector3.up*stow*.16f;caughtFish.localScale=caughtFishScale*Mathf.Lerp(1,.12f,stow);
   if(caughtFishAnimation)caughtFishAnimation.Tick(Mathf.Min(Time.unscaledDeltaTime,.08f));
   View.PlayerVisual.SetFishingPose(3,t,caughtFish.position);
  }
  void OnDisable(){if(FishingLanding)CancelPendingAction();}

  void FinishFishingCatch()
  {
   FishingLanding=false;fishingCatchTime=0;fishingSourceState=null;
   if(caughtFish){caughtFish.gameObject.SetActive(false);Destroy(caughtFish.gameObject);}caughtFish=null;caughtFishAnimation=null;
   if(View&&View.PlayerVisual)View.PlayerVisual.CancelEquipmentAction();
  }
 }
}
