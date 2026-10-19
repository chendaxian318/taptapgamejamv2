from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=root/'AgentScripts/FeedbackCampaign.cs'
s=p.read_text(encoding='utf-8-sig').replace('FullCampaignRuntime','RaftFirstCampaignRuntime').replace('feedback-20261018-140142','feedback-20261018-144126')
s=s.replace('g.State.coins==200&&g.State.day==2','g.State.coins==100&&g.State.day==2').replace('commission pays 200 exactly once','commission pays 100 exactly once')
s=s.replace('await Return();await Shop();g.OpenShopInteraction();await Dialogue();await Until(()=>g.State.chapterOneMorningSeen', '''Close();if(g.paused)g.TogglePause();await Until(()=>g.State.animals.Any(a=>a.species==Species.Goldfish&&a.offspring>0),"extra natural fish for bellows budget",60);g.TogglePause();await Catch(g.State.animals.First(a=>a.species==Species.Goldfish).id);
   await Return();await Shop();g.OpenShopInteraction();await Dialogue();await Until(()=>g.State.chapterOneMorningSeen''')
start=s.index('   await Sail(6);await PlayRoute');end=s.index('  }catch(Exception ex)',start)
s=s[:start]+'''   await Sail(6);Check(g.State.levelIndex==6,"second island reached with purchased bellows and earned funds");await SaveRoundtrip(2);result="PASS new game, natural opening, first PDF 49-action solution, three real catches, 100-coin commission, extra fish sale, morning, bellows purchase, second island and save/load";Note(result);
'''+s[end:]
s=s.replace('await Buy("fan",200);','await Buy("sell",-100);Check(g.State.coins==200,"extra bred fish sold for 100; no grant fills budget gap");await Buy("fan",200);')
(root/'AgentScripts/RaftFirstCampaign.cs').write_text(s,encoding='utf-8')
s=(root/'AgentScripts/FeedbackRecoveryChecks.cs').read_text(encoding='utf-8-sig').replace('VisibleRecoveryChecks','RaftFeedbackRecoveryChecks').replace('feedback-20261018-140142/recovery','feedback-20261018-144126/recovery')
s=s.replace('Check(g.State.playerX==2&&Q(3,3).prop==PropKind.ClimbBlock&&g.View.PlayerWorldPosition.y<.8f,"lower retreat and recovered climb block land correctly");','Check(g.State.playerX==3&&Q(4,3).prop==PropKind.ClimbBlock&&!g.IsRecovering,"fixed climb block rejects recovery and retreat");')
(root/'AgentScripts/RaftFeedbackRecoveryChecks.cs').write_text(s,encoding='utf-8')
for name in ['FeedbackUtilityChecks','FeedbackInputChecks']:
    s=(root/('AgentScripts/'+name+'.cs')).read_text(encoding='utf-8-sig').replace('feedback-20261018-140142','feedback-20261018-144126')
    (root/('AgentScripts/Raft'+name+'.cs')).write_text(s,encoding='utf-8')
print('Prepared isolated feedback regressions.')
