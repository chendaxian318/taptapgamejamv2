using System.IO;
using UnityEngine;
namespace HabitatIsland
{
    public partial class HabitatGame
    {
        public bool SessionStarted { get; private set; }
        public bool HasSavedSession=>SaveFileExists(ExistingSlotPath(1))||SaveFileExists(ExistingSlotPath(2))||SaveFileExists(ExistingSlotPath(3));
        float utilitySeconds;
        bool utilityDigPending;
        GameObject utilityModel;
        HabitatEquipmentAnimation utilityAnimation;
        public void NewSession()
        {
            CancelPendingAction();if(Fishing)Fishing.Cancel(false);Hud.ClosePanels();
            visitedLevels.Clear();history.Clear();State=HabitatLevels.Create(HabitatLevels.HomeHarborIndex);
            ResetPlayTime();
            tuning=new Tuning();ActionCount=0;tool=0;speed=1;paused=true;SessionStarted=true;
            HabitatJourneyRules.Normalize(State);View.Initialize(State);
            View.Harbor.TryPlaceAt(State,HabitatImportedDockLayout.BoatApproach);View.Render(State,tuning,overlays);View.FocusPlayer();
            CaptureRetryPoint();
            if(playOpeningOnNewGame)BeginOpening();
        }
        public void SelectInventoryTool(int selected)
        {
            if(!TrySelectTool(selected))return;
            Hud.ClosePanels();
        }
        public void StoreInventoryItem(){if(!Busy)Mutate(()=>HabitatRules.StoreHeld(State));}
        public void TakeInventoryItem(int index)
        {if(!Busy&&Mutate(()=>HabitatRules.TakeBagProp(State,index))){tool=0;Hud.ClosePanels();}}
        public bool NearCargoBoat=>State.onHarbor?View.Harbor.NearBoat(State):HabitatRules.AtDock(State);
        public void TransferCargo(bool unload)
        {
            if(Busy)return;
            if(!NearCargoBoat){State.message="请到小船旁装卸木箱";return;}
            if(Mutate(()=>HabitatRules.TransferBoatCrate(State,unload))&&unload){tool=0;Hud.ClosePanels();}
        }
        public HabitatState PreviewUtility(int x,int z,int dx,int dz,out bool valid)
        {
            var preview=HabitatRules.Clone(State);valid=HabitatRules.UseUtility(preview,tool,x,z,dx,dz);return preview;
        }
        public bool ConfirmUtility(int x,int z,int dx,int dz)
        {
            if(Busy||Fishing&&Fishing.IsOpen)return false;
            var candidate=PreviewUtility(x,z,dx,dz,out bool valid);
            if(!valid){State.message=candidate.message;HabitatAudio.Play(HabitatAudioCue.Error);return false;}
            Hud.ClosePanels();
            pendingBefore=HabitatRules.Clone(State);pendingState=candidate;pendingSimulation=false;pendingMotion=HabitatPlayerMotion.Walk;
            utilityDigPending=tool==5;utilitySeconds=tool==7?1.2f:tool==5?.72f:.45f;
            if(tool==5)PlayToolVisual("Dig",x,z);
            else
            {
                View.BeginGridStep(State,candidate,HabitatPlayerMotion.Walk);
                var prefab=tool==7?Resources.Load<GameObject>("Models/Cannon/Cannon"):null;
                if(prefab)
                {
                    utilityModel=Instantiate(prefab);
                    Vector3 direction=new Vector3(tool==7?State.facingX:dx,0,tool==7?State.facingZ:dz);
                    utilityModel.transform.position=new Vector3(State.playerX,HabitatRules.Height(State,State.playerX,State.playerZ)*.7f+.05f,State.playerZ)+direction*.45f;
                    utilityModel.transform.rotation=Quaternion.LookRotation(direction);
                    utilityModel.transform.localScale=Vector3.one*(tool==7?.6f:.7f);
                    if(tool==6){utilityModel.transform.position+=Vector3.up*.7f;utilityModel.transform.rotation*=Quaternion.Euler(-90,0,0);}
                    utilityAnimation=utilityModel.GetComponent<HabitatEquipmentAnimation>();
                    if(utilityAnimation&&tool==7)utilityAnimation.Play("Fire",false,1,true);
                }
            }
            return true;
        }
        void TickUtilityPresentation()
        {
            if(utilitySeconds<=0)return;
            float dt=Mathf.Min(Time.unscaledDeltaTime,.08f);
            utilitySeconds=Mathf.Max(0,utilitySeconds-dt);
            if(utilityAnimation)utilityAnimation.Tick(dt);
        }
        void FinishUtilityPresentation()
        {
            utilitySeconds=0;utilityDigPending=false;utilityAnimation=null;
            if(utilityModel)Destroy(utilityModel);utilityModel=null;
        }
    }
}
