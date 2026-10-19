using UnityEngine;

namespace HabitatIsland
{
    public partial class HabitatGame
    {
        LineRenderer tutorialMarker;
        Material tutorialMarkerMaterial;
        public HabitatTutorialStep TutorialStep => HabitatTutorialRules.Evaluate(State,tuning,paused);
        public bool TutorialActive => State!=null && (HabitatTutorialRules.IsTutorial(State)
            ? !State.tutorialGuideDismissed : State.levelIndex==HabitatFigJamLevel.Index&&State.tutorialOfferPending&&!State.tutorialCompleted);
        public bool TutorialFinished => HabitatTutorialRules.IsTutorial(State)&&TutorialStep==HabitatTutorialStep.Complete;
        public string TutorialTitle => HabitatTutorialRules.Title(TutorialStep);
        public string TutorialBody => HabitatTutorialRules.Body(TutorialStep,State,tuning);
        public string TutorialProgress
        {
            get
            {
                int n=TutorialStep==HabitatTutorialStep.Arrival?1:TutorialStep==HabitatTutorialStep.ObserveCrab?2
                    :TutorialStep==HabitatTutorialStep.PickShrimp?3:TutorialStep==HabitatTutorialStep.PlaceShrimp?4
                    :TutorialStep==HabitatTutorialStep.ObserveHabitat?5:6;
                return TutorialFinished?"新手教学 · 已完成":"新手教学 · "+n+" / 6";
            }
        }
        public bool TutorialCanResume => HabitatTutorialRules.IsTutorial(State)&&TutorialStep==HabitatTutorialStep.Resume;
        public bool TutorialAvailableTarget => HabitatTutorialRules.IsTutorial(State)
            && HabitatTutorialRules.TryTarget(TutorialStep,State,out _,out _);
        public bool TutorialMarkerVisible => tutorialMarker&&tutorialMarker.enabled;

        // Buttons enter the same boat selection/confirmation flow as ordinary travel.
        public void OpenTutorialDestination(int index)
        {
            if(Busy||State==null||index<0||index>=HabitatLevels.Names.Length)return;
            if(State.carryingAnimal>=0||State.carryingProp!=PropKind.None)
            {State.message="请先放下手中的动物或道具，再乘船前往教学或正式岛。";return;}
            ReturnToHarbor();
            if(!View.Harbor.NearBoat(State))return;
            Hud.OpenMapSelection(index);
        }

        public void ShowTutorialGuide()
        {
            if(Busy||State==null)return;
            Hud.ClosePanels();
            if(HabitatTutorialRules.IsTutorial(State))
            {State.tutorialGuideDismissed=false;View.FocusPlayer();}
            else OpenTutorialDestination(HabitatTutorialRules.LevelIndex);
        }

        public void DismissTutorial()
        {
            if(Busy||State==null||Hud.ModalOpen)return;
            if(HabitatTutorialRules.IsTutorial(State))State.tutorialGuideDismissed=true;
            else State.tutorialOfferPending=false;
            UpdateTutorialMarker(false);
        }

        public void ResumeTutorial()
        {
            if(Busy||Hud.ModalOpen||(Fishing&&Fishing.IsOpen)||!TutorialCanResume)return;
            paused=false;State.message="生态时间已继续，保持环境适宜，等待螃蟹首次繁衍。";
        }

        public void RestartCurrentLevel()
        {
            if(Busy||State==null)return;
            if(retryPoint==null||retryPoint.state.levelIndex!=State.levelIndex)CaptureRetryPoint();
            float played=State.playSeconds;
            var saved=new Snapshot { state=retryPoint.state,tuning=retryPoint.tuning,actions=retryPoint.actions,visitedLevels=retryPoint.visitedLevels,retryPoint=retryPoint };
            // A full detached transaction prevents item duplication and currency/refund mismatches.
            saved=JsonUtility.FromJson<Snapshot>(JsonUtility.ToJson(saved));
            RestoreSnapshot(saved);
            State.playSeconds=Mathf.Max(played,State.playSeconds);ResetPlayTime();
            tool=0;View.FocusPlayer();autoSaveRequested=true;
            State.message="已恢复最近的登岛或交易检查点，物资与探索进度一起恢复。";
        }

        Vector3 TutorialTargetPosition(int x,int z)
        {
            if(TutorialStep==HabitatTutorialStep.Arrival&&View.Harbor.Ready)
                return View.Harbor.LandingGuidePosition(State);
            return new Vector3(x,HabitatRules.Height(State,x,z)*.7f+.09f,z);
        }

        public void RevealTutorialTarget()
        {
            if(Busy||Hud.ModalOpen||(Fishing&&Fishing.IsOpen)||!TutorialAvailableTarget)return;
            if(!HabitatTutorialRules.TryTarget(TutorialStep,State,out int x,out int z))return;
            View.FocusTutorialTarget(TutorialTargetPosition(x,z));
            // Only points the camera. The player still clicks/uses the real target.
            State.message=TutorialStep==HabitatTutorialStep.Arrival?"沿黄色标记走向木桥和岸边。":"黄色边框标出了当前教学目标。";
        }

        void UpdateTutorial()
        {
            bool acceptsObservation=!Busy&&!StoryPlaying&&!Hud.ModalOpen&&!(Fishing&&Fishing.IsOpen);
            if(acceptsObservation)HabitatTutorialRules.Observe(State,tuning);
            UpdateTutorialMarker(acceptsObservation&&TutorialActive&&HabitatTutorialRules.IsTutorial(State));
        }

        void UpdateTutorialMarker(bool show)
        {
            if(!show||!HabitatTutorialRules.TryTarget(TutorialStep,State,out int x,out int z))
            {if(tutorialMarker)tutorialMarker.enabled=false;return;}
            if(!tutorialMarker)
            {
                var marker=new GameObject("Tutorial target outline");marker.transform.SetParent(transform,false);
                tutorialMarker=marker.AddComponent<LineRenderer>();tutorialMarker.useWorldSpace=true;
                tutorialMarker.loop=true;tutorialMarker.positionCount=4;tutorialMarker.widthMultiplier=.045f;
                tutorialMarker.numCornerVertices=4;tutorialMarker.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                tutorialMarker.receiveShadows=false;
                tutorialMarkerMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                tutorialMarkerMaterial.name="Tutorial target yellow";tutorialMarkerMaterial.color=new Color(1f,.83f,.24f,1f);
                tutorialMarker.sharedMaterial=tutorialMarkerMaterial;
            }
            Vector3 p=TutorialTargetPosition(x,z);float radius=.43f+Mathf.Sin(Time.unscaledTime*3f)*.025f;
            tutorialMarker.SetPosition(0,p+new Vector3(-radius,0,-radius));
            tutorialMarker.SetPosition(1,p+new Vector3(-radius,0,radius));
            tutorialMarker.SetPosition(2,p+new Vector3(radius,0,radius));
            tutorialMarker.SetPosition(3,p+new Vector3(radius,0,-radius));
            tutorialMarker.enabled=true;
        }

        void DisposeTutorial()
        {if(tutorialMarkerMaterial)Destroy(tutorialMarkerMaterial);}
    }
}
