using System;

namespace HabitatIsland
{
    /// <summary>One journey clock. Island changes and undo cannot refund time; loading/new games explicitly reset it.</summary>
    public sealed class HabitatPlayClock
    {
        double seconds;
        bool initialized;
        HabitatState observed;
        public static float Normalize(float value)
            =>float.IsNaN(value)||float.IsInfinity(value)||value<0?0:value;
        public void Reset(HabitatState state)
        {
            seconds=state==null?0:Normalize(state.playSeconds);initialized=true;observed=state;
            if(state!=null)state.playSeconds=(float)seconds;
        }
        public void Sync(HabitatState state)
        {
            if(state==null)return;
            if(!initialized){Reset(state);return;}
            // Compare only when a new state object arrives, avoiding float round-up every frame.
            if(!ReferenceEquals(observed,state))seconds=Math.Max(seconds,Normalize(state.playSeconds));
            observed=state;state.playSeconds=(float)seconds;
        }
        public void Advance(HabitatState state,float unscaledDelta,bool active)
        {
            if(state==null)return;
            Sync(state);
            if(active&&unscaledDelta>0&&!float.IsNaN(unscaledDelta)&&!float.IsInfinity(unscaledDelta))
                seconds=Math.Min(float.MaxValue,seconds+unscaledDelta);
            state.playSeconds=(float)seconds;
        }
    }
}
