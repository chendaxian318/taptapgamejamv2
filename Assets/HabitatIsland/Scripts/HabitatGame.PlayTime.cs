using UnityEngine;

namespace HabitatIsland
{
    public partial class HabitatGame
    {
        readonly HabitatPlayClock playClock=new HabitatPlayClock();
        bool playClockWasActive;

        void ResetPlayTime()
        {
            playClock.Reset(State);
            playClockWasActive=false;
        }
        void SyncPlayTime()
        {
            playClock.Sync(State);
            // Animated candidates were cloned before the animation. Commit must keep the latest clock.
            if(State!=null&&pendingState!=null)pendingState.playSeconds=State.playSeconds;
        }
        void UpdatePlayTime()
        {
            bool active=SessionStarted&&Application.isFocused&&(!Hud||!Hud.PlayClockSuspended);
            // The first resumed frame may include a focus/menu interruption; do not charge that gap.
            playClock.Advance(State,Time.unscaledDeltaTime,active&&playClockWasActive);
            playClockWasActive=active;
            SyncPlayTime();
        }
    }
}
