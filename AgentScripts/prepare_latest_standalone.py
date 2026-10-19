from pathlib import Path
import shutil
root=Path('.')
base=root/'Assets/HabitatIsland/Scripts/StandaloneQA'
backup=root/'ChangeBackups/latest-cg-campaign/StandaloneQA'
backup.mkdir(parents=True,exist_ok=True)
for name in ['StandaloneCampaignChecks.cs','StandaloneInputChecks.cs','StandaloneRecoveryChecks.cs']:
    shutil.copy2(base/name,backup/name)
shutil.copyfile(root/'AgentScripts/LatestStandaloneCampaign.cs.pending',base/'StandaloneCampaignChecks.cs')
p=base/'StandaloneInputChecks.cs'
s=p.read_text(encoding='utf-8-sig')
old='await Setup();await SendKeys(Key.Digit6);Check(g.tool==5,"key6 selects shovel");'
new='await Setup();await SendKeys(Key.Digit6);Check(g.tool==0,"unowned shovel rejected");await SendKeys(Key.Digit7);Check(g.tool==0,"unowned magnet rejected");await SendKeys(Key.Digit8);Check(g.tool==0,"unowned launcher rejected");await SendKeys();g.State.shovels=g.State.magnets=g.State.launchers=1;await SendKeys(Key.Digit6);Check(g.tool==5,"key6 selects owned shovel");'
assert old in s
p.write_text(s.replace(old,new),encoding='utf-8')
p=base/'StandaloneRecoveryChecks.cs'
s=p.read_text(encoding='utf-8-sig')
old='g.StoreInventoryItem();g.Select(4,3);int beforeRide='
assert old in s
s=s.replace(old,'g.StoreInventoryItem();Q(5,3).prop=PropKind.Obstacle;g.View.Initialize(g.State);g.Select(4,3);int beforeRide=')
s=s.replace('empty-water raft ride still commits once on arrival','raft stops before obstacle and commits once on arrival')
p.write_text(s,encoding='utf-8')
