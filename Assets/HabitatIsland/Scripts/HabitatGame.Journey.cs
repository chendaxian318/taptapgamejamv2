using System;
using System.Collections.Generic;
using UnityEngine;

namespace HabitatIsland
{
    public partial class HabitatGame
    {
        public HabitatMainStory Story;
        public bool playOpeningOnNewGame = true;
        public bool StoryPlaying => Story && Story.IsPlaying;
        readonly List<VisitedLevel> visitedLevels = new List<VisitedLevel>();
        HabitatState beforeStory;
        bool storyPriorPause;

        [Serializable]
        public sealed class VisitedLevel
        {
            public int index, actions;
            public HabitatState state;
        }

        void InitializeJourney()
        {
            HabitatJourneyRules.Normalize(State);
            Story = GetComponent<HabitatMainStory>();
            if (!Story) return;
            Story.Initialize(this);
            Story.Completed += CompleteJourneySequence;
        }

        void Start()
        {
            if(SessionStarted){if(playOpeningOnNewGame&&!State.openingCompleted)BeginOpening();}
            else Hud.ShowStartMenu();
        }

        public bool BeginOpening()
        {
            if (Busy || !Story || State.openingCompleted || !View.Harbor.Ready) return false;
            Hud.ClosePanels();
            beforeStory = HabitatRules.Clone(State); storyPriorPause = paused;
            if (!Story.BeginOpening()) { beforeStory = null; return false; }
            HabitatJourneyRules.BeginNewJourney(State);
            paused = true;
            return true;
        }

        public void RestAtHome()
        {
            if (Busy) return;
            if (!View.Harbor.NearHome(State)) { State.message = "请走到家门口再休息。"; return; }
            if (!Story) { State.message = "晨光片段暂不可用。"; return; }
            Hud.ClosePanels();
            beforeStory = HabitatRules.Clone(State); storyPriorPause = paused;
            if (!Story.BeginMorning()) { beforeStory = null; State.message = "晨光片段暂不可用。"; return; }
            paused = true;
        }

        void CompleteJourneySequence(bool morning)
        {
            if (beforeStory == null) return;
            if (morning)
            {
                PushHistory(beforeStory); ActionCount++;
                HabitatJourneyRules.AdvanceDay(State);
                if (State.chapterOneDelivered && !State.chapterOneMorningSeen && HasCompletedLevel(5))
                { HabitatChapterRules.CompleteMorning(State); chapterMorningGreetingPending=true; }
                View.Harbor.TryPlaceAt(State, HabitatImportedDockLayout.HomeApproach);
                State.message = "第 " + State.day + " 天，早上好。渔获、物资和岛屿进度已保留。";
            }
            else
            {
                HabitatJourneyRules.CompleteOpening(State);
                State.tutorialOfferPending=false;
                chapterGreetingPending=!State.chapterOneAccepted;
                View.Harbor.TryPlaceAt(State, HabitatImportedDockLayout.ShopApproach);
                State.message = "已带着渔获回到码头。与商店老板交谈，了解琉璃鱼委托。";
            }
            beforeStory = null;
            paused = storyPriorPause;
            View.Render(State, tuning, overlays);
            View.FocusPlayer();
            nextMove = Time.unscaledTime + .15f;
            CaptureRetryPoint();
        }

        void CancelJourneySequence()
        {
            if (Story) Story.Cancel();
            if (beforeStory != null)
            {
                State = beforeStory; beforeStory = null;
                paused = storyPriorPause;
            }
        }

        public bool HasVisitedLevel(int index) => State != null && State.levelIndex == index
            || visitedLevels.Exists(level => level.index == index);
        public bool HasCompletedLevel(int index) => State!=null&&State.levelIndex==index?State.won:
            visitedLevels.Exists(level=>level.index==index&&level.state!=null&&level.state.won);

        public void ReturnToHarbor()
        {
            if (Busy) return;
            if ((State.carryingAnimal >= 0&&!HabitatRules.CanStoreCarriedShrimp(State)) || State.carryingProp != PropKind.None)
            { State.message = "请先放下手中的道具或尚不可收获的动物。满足收获条件的大虾可直接收入背篓返航。"; HabitatAudio.Play(HabitatAudioCue.Error); return; }
            if (Fishing != null) Fishing.Cancel(false);
            Hud.ClosePanels();
            if(HabitatLevels.IsHomeHarbor(State))
            {
                View.Harbor.TryPlaceAt(State,HabitatImportedDockLayout.BoatApproach);
                View.Render(State,tuning,overlays);View.FocusPlayer();return;
            }
            CommitSailing(HabitatLevels.HomeHarborIndex);
        }

        public void InteractWithBoat()
        {
            if(Busy || !View.Harbor.NearBoat(State))return;
            if(HabitatLevels.IsHomeHarbor(State))Hud.OpenMapSelection(RecommendedCampaignLevel);
            else SailToLevel(HabitatLevels.HomeHarborIndex);
        }

        public void SailToLevel(int index)
        {
            if (Busy || index < 0 || index >= HabitatLevels.Names.Length) return;
            if (!CanTravelToLevel(index,out string lockedReason)) { State.message=lockedReason; HabitatAudio.Play(HabitatAudioCue.Error); return; }
            if (!View.Harbor.NearBoat(State)) { State.message = "请先走到小船旁选择目的地。"; HabitatAudio.Play(HabitatAudioCue.Error); return; }
            if ((State.carryingAnimal >= 0&&!HabitatRules.CanStoreCarriedShrimp(State)) || State.carryingProp != PropKind.None)
            { State.message = "出发前请放下道具或尚不可收获的动物；满足收获条件的大虾可以随背篓带走。"; HabitatAudio.Play(HabitatAudioCue.Error); return; }
            Hud.ClosePanels();
            if (!Story || !Story.BeginDeparture(()=>CommitSailing(index)))
            { State.message="出海片段暂不可用，请稍后再试。"; HabitatAudio.Play(HabitatAudioCue.Error); }
            else HabitatAudio.Play(HabitatAudioCue.Confirm);
        }

        void CommitSailing(int index)
        {
            HabitatAudio.ClearTransient(); harborStepDistance=0f;
            var source = State;
            if(HabitatRules.CanStoreCarriedShrimp(source))HabitatRules.StoreCarriedShrimp(source);
            bool firstChapterLanding=source.chapterOneAccepted&&!source.chapterOneSailed&&index==HabitatChapterRules.MainLevel;
            visitedLevels.RemoveAll(level => level.index == source.levelIndex);
            visitedLevels.Add(new VisitedLevel { index = source.levelIndex, state = HabitatRules.Clone(source), actions = ActionCount });
            VisitedLevel destination = visitedLevels.Find(level => level.index == index);
            var next = destination != null ? HabitatRules.Clone(destination.state) : HabitatLevels.Create(index);
            // A new visit replenishes the fish catch described by the campaign;
            // keep every changed tile and resident position, and never reissue shells.
            if(destination!=null&&next.won&&index!=source.levelIndex)
                foreach(Animal resident in next.animals)if(resident.species==Species.Goldfish)resident.catchClaimed=false;
            HabitatJourneyRules.CopySharedProgress(source, next);
            next.onHarbor = HabitatLevels.IsHomeHarbor(next); next.playerX = next.dockX; next.playerZ = next.dockZ;
            next.selectedX = next.selectedZ = -1;
            State = next; ActionCount = destination?.actions ?? 0;
            HabitatChapterRules.RecordSailing(State,index);
            chapterChestPending=firstChapterLanding&&!State.specialRod
                ||destination==null&&(index==6||index==7||index==8);
            if(index==HabitatTutorialRules.LevelIndex)State.tutorialGuideDismissed=false;
            history.Clear(); // Old map undo snapshots cannot refund shared purchases on another map.
            paused = true; View.Initialize(State);
            View.Harbor.TryPlaceAt(State, HabitatImportedDockLayout.BoatApproach);
            State.message = destination == null ? "已抵达 " + State.levelName + "。背包与天数已保留。"
                : "已返回 " + State.levelName + "，继续上次的探索。";
            if(HabitatLevels.IsHomeHarbor(State))State.message="已返航至家园港口。岛屿进度与背包已保留，可到商店交易或回家休息。";
            View.Render(State, tuning, overlays); View.FocusPlayer();
            CaptureRetryPoint();
        }

        Snapshot CaptureSnapshot()
        {
            SyncPlayTime();
            var levels = new List<VisitedLevel>();
            foreach (var item in visitedLevels)
                if (item.index != State.levelIndex)
                    levels.Add(new VisitedLevel { index = item.index, actions = item.actions, state = HabitatRules.Clone(item.state) });
            return new Snapshot { state = HabitatRules.Clone(State), tuning = JsonUtility.FromJson<Tuning>(JsonUtility.ToJson(tuning)),
                actions = ActionCount, visitedLevels = levels, retryPoint=retryPoint };
        }

        void RestoreVisitedLevels(List<VisitedLevel> saved)
        {
            visitedLevels.Clear();
            if (saved == null) return;
            foreach (var item in saved)
                if (item != null && item.index >= 0 && item.index < HabitatLevels.Names.Length && item.state != null
                    && item.state.tiles != null && item.state.animals != null && item.state.width > 0 && item.state.depth > 0
                    && item.state.tiles.Length == item.state.width * item.state.depth && !visitedLevels.Exists(x => x.index == item.index))
                {
                    HabitatJourneyRules.Normalize(item.state);
                    visitedLevels.Add(item);
                }
        }

        void RestoreSnapshot(Snapshot saved)
        {
            ValidateSnapshot(saved);
            var previous=CaptureSnapshot();var oldHistory=history.ToArray();bool oldPause=paused;int oldRetryDepth=retryHistoryDepth;
            try
            {
                CancelPendingAction();
                if (Fishing != null) Fishing.Cancel(false);
                if (Hud != null) Hud.ClosePanels();
                State = saved.state; ResetPlayTime();
                RestoreVisitedLevels(saved.visitedLevels);
                tuning = saved.tuning; ActionCount = saved.actions;
                retryPoint=saved.retryPoint;retryHistoryDepth=0;
                history.Clear(); paused = true; View.Initialize(State);
                if(retryPoint==null)CaptureRetryPoint();
                autoSaveRequested=false;
                observedLevel=State.levelIndex;observedWin=State.won;
                State.message = "已读取进度：第 " + State.day + " 天，渔获和各岛屿进度已恢复。";
            }
            catch
            {
                State=previous.state;tuning=previous.tuning;ActionCount=previous.actions;retryPoint=previous.retryPoint;retryHistoryDepth=oldRetryDepth;
                RestoreVisitedLevels(previous.visitedLevels);ResetPlayTime();paused=oldPause;
                history.Clear();for(int i=oldHistory.Length-1;i>=0;i--)history.Push(oldHistory[i]);
                View.Initialize(State);throw;
            }
        }
    }
}
