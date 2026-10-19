using System;
using System.Collections.Generic;
using System.Reflection;
using HabitatIsland;
using UnityEngine;

// Controlled runtime API fixtures, not a claim that the authored puzzle routes were played.
public static class CampaignController20261018Checks
{
    static readonly List<string> passed=new List<string>();
    static void Check(bool yes,string name){if(!yes)throw new Exception(name);passed.Add(name);}
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Close(HabitatGame game){game.Hud.ClosePanels();}
    static void FinishDialogue(HabitatGame game)
    {int guard=0;while(game.Hud.ChapterDialogueOpen&&guard++<12)game.Hud.AdvanceChapterDialogue();}
    static void Shop(HabitatGame game)
    {Close(game);Check(game.View.Harbor.TryPlaceAt(game.State,HabitatImportedDockLayout.ShopApproach),"fixture enters actual shop approach");game.View.Render(game.State,game.tuning,true);}
    public static string Run()
    {
        var game=HabitatGame.Instance;if(!game)throw new Exception("Run in Play mode");
        var capture=typeof(HabitatGame).GetMethod("CaptureSnapshot",Private);
        var restore=typeof(HabitatGame).GetMethod("RestoreSnapshot",Private);
        object original=capture.Invoke(game,null);
        bool paused=game.paused;int tool=game.tool;
        var visited=(List<HabitatGame.VisitedLevel>)typeof(HabitatGame).GetField("visitedLevels",Private).GetValue(game);
        passed.Clear();
        try
        {
            Close(game);visited.Clear();game.paused=true;game.State=HabitatLevels.Create(5);
            game.State.chapterOneAccepted=game.State.chapterOneSailed=game.State.specialRod=game.State.rod=true;
            game.State.won=true;game.State.glassFish=2;game.State.coins=0;game.State.openingCompleted=true;
            game.View.Initialize(game.State);Shop(game);
            Check(!game.CanTravelToLevel(6,out _)&&!game.CanTravelToLevel(7,out _)&&!game.CanTravelToLevel(10,out _),"new route rejects later islands before delivery");
            game.OpenShopInteraction();Check(game.Hud.ChapterDialogueOpen,"first catch opens real delivery dialogue");
            game.Hud.AdvanceChapterDialogue();
            Check(game.State.chapterOneDelivered&&game.State.coins==200&&game.State.glassFish==0,"delivery UI commits two fish for exactly 200");
            Close(game);game.DeliverChapterCommission();Check(game.State.coins==200,"duplicate controller delivery pays nothing");
            HabitatChapterRules.CompleteMorning(game.State);Close(game);game.OpenShopInteraction();FinishDialogue(game);
            Check(HabitatChapterRules.HasBriefing(game.State,2)&&game.CanTravelToLevel(6,out _),"second-day dialogue and route open island two");
            Close(game);
            foreach(int index in new[]{5,6}){var state=HabitatLevels.Create(index);state.won=true;visited.Add(new HabitatGame.VisitedLevel{index=index,state=state});}
            game.OpenShopInteraction();FinishDialogue(game);
            Check(game.CanTravelToLevel(7,out _)&&HabitatChapterRules.CanBuyItem(game.State,"launcher",out _),"second victory dialogue opens third island and launcher");
            Close(game);var third=HabitatLevels.Create(7);third.won=true;visited.Add(new HabitatGame.VisitedLevel{index=7,state=third});
            game.OpenShopInteraction();Check(game.Hud.ChapterDialogueOpen&&!game.CanTravelToLevel(8,out _),"third victory still requires shell delivery");Close(game);
            game.State.shells=1;game.Buy("sell");Check(game.State.deliveredShells==1,"actual shop records sold shell");
            Close(game);game.OpenShopInteraction();FinishDialogue(game);
            Check(game.CanTravelToLevel(8,out _)&&HabitatChapterRules.CanBuyItem(game.State,"magnet",out _),"sold shell dialogue opens fourth island and magnet");
            Close(game);var fourth=HabitatLevels.Create(8);fourth.won=true;visited.Add(new HabitatGame.VisitedLevel{index=8,state=fourth});
            game.OpenShopInteraction();FinishDialogue(game);
            Check(game.CanTravelToLevel(10,out _)&&game.RecommendedCampaignLevel==10,"fourth victory opens fifth challenge at stable index ten");
            Close(game);var fifth=HabitatLevels.Create(10);fifth.won=true;visited.Add(new HabitatGame.VisitedLevel{index=10,state=fifth});
            int coins=game.State.coins;game.OpenShopInteraction();FinishDialogue(game);
            Check(game.CampaignComplete&&HabitatChapterRules.HasBriefing(game.State,6)&&game.State.coins==coins,"fifth completion final dialogue records once without invented bonus");
            Check(game.CanTravelToLevel(4,out _)&&game.CanTravelToLevel(9,out _),"finished campaign opens existing extra islands");
            Close(game);game.PrepareNextCampaignIsland();Check(game.State.onHarbor,"next-island action uses harbor travel instead of resetting map");
            string report="PASS "+passed.Count+" controlled campaign controller checks\n"+string.Join("\n",passed);
            System.IO.Directory.CreateDirectory("Verification");System.IO.File.WriteAllText("Verification/campaign-controller-20261018-checks.txt",report);
            return report;
        }
        finally
        {
            Close(game);restore.Invoke(game,new[]{original});game.paused=paused;game.tool=tool;game.View.Render(game.State,game.tuning,true);
        }
    }
}
