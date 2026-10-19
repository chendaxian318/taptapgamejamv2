using System;
using System.Collections.Generic;
using HabitatIsland;
public static class PlayTimeChecks
{
    public static readonly List<string> Evidence=new List<string>();
    static int n;
    static void Check(bool ok,string text){if(!ok)throw new Exception("FAIL "+text);Evidence.Add("PASS "+(++n)+": "+text);}
    static HabitatState State(float seconds=0)=>new HabitatState{width=1,depth=1,tiles=new[]{new Tile{height=1}},playSeconds=seconds};
    static bool Near(float a,float b)=>Math.Abs(a-b)<.002f;
    public static string Run()
    {
        n=0;Evidence.Clear();var clock=new HabitatPlayClock();var state=State();state.elapsed=999;
        clock.Sync(state);Check(state.playSeconds==0,"new/old states without playSeconds start at zero, not ecological elapsed");
        clock.Advance(state,1,true);Check(Near(state.playSeconds,1)&&state.elapsed==999,"active unscaled time advances independent of simulation time");
        clock.Advance(state,100,false);Check(Near(state.playSeconds,1),"inactive pause/background samples add no time");
        clock.Advance(state,float.NaN,true);clock.Advance(state,float.PositiveInfinity,true);clock.Advance(state,-1,true);
        Check(Near(state.playSeconds,1),"invalid or negative frame deltas cannot poison play time");
        var before=HabitatRules.Clone(state);var candidate=HabitatRules.Clone(state);
        clock.Advance(state,3,true);clock.Sync(candidate);Check(Near(candidate.playSeconds,4),"animated candidate arrival preserves time spent since preview");
        clock.Advance(candidate,2,true);clock.Sync(before);Check(Near(before.playSeconds,6),"undo/cancel cannot rewind real play time");
        var island=State(900);HabitatJourneyRules.CopySharedProgress(before,island);
        Check(Near(island.playSeconds,6),"travel copies shared clock rather than adding cached island time");
        clock.Sync(island);clock.Advance(island,1,true);Check(Near(island.playSeconds,7),"travel continues one clock, without double counting");
        var copy=HabitatRules.Clone(island);Check(Near(copy.playSeconds,7),"snapshot clone contains real play seconds");
        var newGame=State();clock.Reset(newGame);clock.Advance(newGame,1,true);
        Check(Near(newGame.playSeconds,1),"new game reset cannot inherit previous session's maximum");
        var saved=State(123);clock.Reset(saved);clock.Advance(saved,2,true);
        Check(Near(saved.playSeconds,125),"loading preserves saved duration then continues counting");
        var earlierSave=State(4);clock.Reset(earlierSave);clock.Sync(earlierSave);
        Check(Near(earlierSave.playSeconds,4),"loading an earlier save explicitly replaces a higher active clock");
        var oldSave=State();oldSave.elapsed=86400;clock.Reset(oldSave);HabitatJourneyRules.Normalize(oldSave);
        Check(oldSave.playSeconds==0,"legacy save defaults to zero rather than inventing day-long play time");
        foreach(float bad in new[]{-10f,float.NaN,float.PositiveInfinity})
        {var s=State(bad);HabitatJourneyRules.Normalize(s);Check(s.playSeconds==0,"invalid saved duration normalizes to zero: "+bad);}
        var longSession=State();clock.Reset(longSession);for(int i=0;i<60000;i++)clock.Advance(longSession,1f/60f,true);
        Check(Math.Abs(longSession.playSeconds-1000)<.01f,"high-frequency accumulation retains accuracy over 1000 active seconds");
        return "PASS "+n+" real-play clock checks (pure state fixtures; Unity focus/menu wiring still verified separately).";
    }
}
