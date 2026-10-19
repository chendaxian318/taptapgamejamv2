using UnityEngine;

namespace HabitatIsland
{
    public partial class HabitatGame
    {
        const float ChestOpenDuration = 1.35f;
        HabitatChestVisual openingChest;
        bool chestOpening;
        float chestOpenTime;
        public bool ChestOpening => chestOpening;

        // Preview with the same rules used by the other interactions. Rewards belong to
        // pendingState only, while the authoritative state and visible chest stay closed.
        bool BeginChestAction(int x, int z)
        {
            if (Busy) return false;
            var candidate = HabitatRules.Clone(State);
            if (!HabitatRules.Interact(candidate, x, z))
            {
                State.message = candidate.message;
                HabitatAudio.Play(HabitatAudioCue.Error);
                return false;
            }
            if (candidate.openedChests != State.openedChests + 1) return false;
            var visual = View ? View.ChestVisualAt(x, z) : null;
            if (!visual)
            {
                State.message = "宝箱正在准备，请稍后再试。";
                return false;
            }
            pendingBefore = HabitatRules.Clone(State);
            pendingState = candidate;
            pendingSimulation = false;
            pendingMotion = HabitatPlayerMotion.Walk;
            openingChest = visual;
            chestOpenTime = 0;
            chestOpening = true;
            openingChest.SetOpening(0);
            return true;
        }

        void TickChestPresentation()
        {
            if (!chestOpening) return;
            if (pendingState == null || pendingBefore == null || !openingChest)
            {
                // A replaced/destroyed view must never silently grant the pending reward.
                CancelPendingAction();
                return;
            }
            chestOpenTime = Mathf.Min(ChestOpenDuration, chestOpenTime + Mathf.Min(Time.unscaledDeltaTime, .08f));
            openingChest.SetOpening(chestOpenTime / ChestOpenDuration);
            if (chestOpenTime < ChestOpenDuration || !View.ResidentsArrived(State)) return;
            CommitPending();
            pendingState = pendingBefore = null;
            pendingSimulation = false;
            HabitatAudio.PlayAction(HabitatActionCue.Chest);
            FinishChestPresentation(false);
        }

        void FinishChestPresentation(bool cancelled = true)
        {
            if (cancelled && openingChest) openingChest.SetOpening(0);
            openingChest = null;
            chestOpening = false;
            chestOpenTime = 0;
        }
    }
}
