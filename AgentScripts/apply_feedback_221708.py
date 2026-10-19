from pathlib import Path
import shutil
root=Path('Assets/HabitatIsland/Scripts')
def edit(name,a,b):
 p=root/name;s=p.read_text(encoding='utf-8-sig');assert a in s,(name,a[:80]);p.write_text(s.replace(a,b),encoding='utf-8')
edit('HabitatModel.cs','                if(!s.won){reason="先使本关所有动物同时满足繁衍条件，通关后才能捕捞。";return false;}\n','')
edit('HabitatModel.cs','满足繁衍条件并通关后再','满足自身繁衍条件后再')
a='''            int direction=raft.rotation;RemoveProp(raft);PutProp(dest,PropKind.Raft);dest.rotation=direction;
            s.playerX=nx;s.playerZ=nz;SyncCarried(s);'''
b='''            // A single input sails the whole unobstructed lane. Candidate state is
            // committed only after the rider and raft arrive at this final cell.
            while(true)
            {
                int tx=nx+dx,tz=nz+dz;Tile next=TileAt(s,tx,tz);
                if(!CanReceive(next)||!IsWater(s,tx,tz)||next.trap||Occupied(s,tx,tz))break;
                nx=tx;nz=tz;dest=next;
            }
            int direction=raft.rotation;RemoveProp(raft);PutProp(dest,PropKind.Raft);dest.rotation=direction;
            s.playerX=nx;s.playerZ=nz;SyncCarried(s);'''
edit('HabitatModel.cs',a,b)
edit('HabitatModel.cs','搭乘竹排：沿水纹方向可继续前进或原路返回。','竹排沿航道滑至障碍物前停下；可反向返回。')
# Preserve the shrimp parent during pre-clear harvest, so harvesting cannot delete a puzzle goal.
edit('HabitatModel.cs','''=>UsesConfirmedPlan(s)&&s.won&&s.carryingAnimal>=0
                &&s.animals.Exists(a=>a.id==s.carryingAnimal&&a.carried&&a.species==Species.Shrimp);''','''=>UsesConfirmedPlan(s)&&s.backBasketUnlocked&&s.carryingAnimal>=0
                &&s.animals.Exists(a=>a.id==s.carryingAnimal&&a.carried&&a.species==Species.Shrimp
                    &&(s.won||(!a.catchClaimed||a.offspring>0)&&!Blocked(s,a.x,a.z)));''')
edit('HabitatModel.cs','''            s.animals.Remove(shrimp);s.carryingAnimal=-1;s.shrimp++;
            Log(s,"已将通关后携带的红虾收为渔获，可带回商店出售。");return true;''','''            if(s.won){s.animals.Remove(shrimp);s.shrimp++;}
            else
            {
                int amount=shrimp.catchClaimed?shrimp.offspring:1;
                if(shrimp.catchClaimed)shrimp.offspring=0;else shrimp.catchClaimed=true;
                s.shrimp+=amount;shrimp.carried=false;shrimp.roamAnchored=false;
            }
            s.carryingAnimal=-1;
            Log(s,"已收取适居红虾渔获，可返航出售；未通关时亲本留在岛上继续解谜。");return true;''')
edit('HabitatView.cs','            if(HabitatLevels.IsHomeHarbor(s))BuildHomeIsland(model);','            // Home harbor now contains only the delivered wooden dock (feedback 22:17).')
edit('HabitatView.cs','cameraSize = LocalCameraSize;\n            cameraTarget', 'cameraSize = HabitatLevels.IsHomeHarbor(state) ? 4.8f : LocalCameraSize;\n            cameraTarget') if False else None
edit('HabitatView.cs','followPlayer = true; overviewCamera = false; cameraSize = LocalCameraSize;','followPlayer = true; overviewCamera = false; cameraSize = HabitatLevels.IsHomeHarbor(state) ? 4.8f : LocalCameraSize;')
# tools: one shared ownership predicate across hotkeys and both UI paths
edit('HabitatGame.Workflow.cs','''            if(Busy||selected<0||selected>=ToolNames.Length)return;
            tool=selected;Hud.ClosePanels();State.message="已选择"+ToolNames[selected]+"，选择目标后按 E";''','''            if(!TrySelectTool(selected))return;
            Hud.ClosePanels();''')
p=root/'HabitatGame.Tools.cs';p.write_text('''namespace HabitatIsland
{
    public partial class HabitatGame
    {
        public bool HasTool(int selected)
        {
            if(State==null)return false;
            switch(selected)
            {
                case 0:return true;
                case 1:return State.cargo>0||State.carryingProp==PropKind.Climb;
                case 2:case 4:return State.rod;
                case 3:return State.traps>0;
                case 5:return State.shovels>0;
                case 6:return State.magnets>0;
                case 7:return State.launchers>0;
                default:return false;
            }
        }
        public bool TrySelectTool(int selected)
        {
            if(Busy||Fishing&&Fishing.IsOpen||selected<0||selected>=ToolNames.Length)return false;
            if(!HasTool(selected)){State.message="尚未持有"+ToolNames[selected]+"，请先取得或购买。";HabitatAudio.Play(HabitatAudioCue.Error);return false;}
            tool=selected;State.message="已选择"+ToolNames[selected]+"，选择目标后按 E";return true;
        }
    }
}
''',encoding='utf-8')
for i in range(8):edit('HabitatGame.cs',f'if (k.digit{i+1}Key.wasPressedThisFrame) tool = {i};',f'if (k.digit{i+1}Key.wasPressedThisFrame) TrySelectTool({i});')
edit('HabitatGame.cs','            UpdatePlayTime();','            if(!Busy && !Fishing.IsOpen && !HasTool(tool))tool=0;\n            UpdatePlayTime();')
edit('HabitatHud.Layout.cs','game.tool=id;nextRefresh=0;HabitatAudio.Play(HabitatAudioCue.Click);','if(game.TrySelectTool(id)){nextRefresh=0;HabitatAudio.Play(HabitatAudioCue.Click);}')
edit('HabitatHud.Workflow.cs','''            inventoryTools[2].interactable=inventoryTools[4].interactable=s.rod;
            inventoryTools[3].interactable=s.traps>0;
            // Empty consumables remain selectable so their supplied model and requirements can be inspected.''','''            for(int i=0;i<inventoryTools.Length;i++)if(inventoryTools[i])inventoryTools[i].interactable=game.HasTool(i);''')
edit('HabitatHud.Plan.cs','{selectedBagTool=id;selectedBagIndex=-1;','{if(!game.HasTool(id)){inventoryFeedback.text="尚未持有该工具，请先取得或购买。";return;}selectedBagTool=id;selectedBagIndex=-1;')
# Selected harvest tool should not keep the rod in the hand after the animation/cancel.
edit('HabitatPlayerVisual.Equipment.cs','(selectedTool == 2 || selectedTool == 4) && state.rod','(selectedTool == 2 || selectedTool == 4 && equipmentFishing) && state.rod')
edit('HabitatGame.Journey.cs','State.chapterOneDelivered && !State.chapterOneMorningSeen','State.chapterOneDelivered && !State.chapterOneMorningSeen && HasCompletedLevel(5)')
edit('HabitatGame.Chapter.cs','if(stage<=HabitatChapterRules.UnlockedStage(State))return true;','if(stage<=HabitatChapterRules.UnlockedStage(State)&&(stage==1||HasCompletedLevel(HabitatChapterRules.CampaignLevel(stage-1))))return true;')
edit('HabitatGame.Chapter.cs','岛上所有动物都安居后，就能钓取它们的后代了。','目标鱼满足繁衍条件就能先捕捞、出售补给；全体动物同时安居后才算通关。')
edit('HabitatHud.Batch2.cs','连通外海，并在本岛目标全部安居后捕捞。','连通外海且目标适居后即可捕捞，不必等待整关完成。')
edit('HabitatHud.Batch2.cs','先围住水域，使本岛目标全部安居后再垂钓；','先围住水域，使目标金鱼适居后即可垂钓；')
for f in ['HabitatGame.Journey.cs']:
 edit(f,'尚未通关岛屿的动物','尚不可收获的动物');edit(f,'通关后携带的大虾','满足收获条件的大虾')
# Shared save flag for first clear CG, default false on old saves.
edit('HabitatModel.cs','public int glassFish,glassFishCaught;','public int glassFish,glassFishCaught; public bool firstClearCgSeen;')
edit('HabitatModel.cs','n.chapterOneMorningSeen=s.chapterOneMorningSeen;','n.firstClearCgSeen=s.firstClearCgSeen;n.chapterOneMorningSeen=s.chapterOneMorningSeen;')
edit('HabitatJourneyRules.cs','dest.specialRod=source.specialRod;','dest.firstClearCgSeen=source.firstClearCgSeen;dest.specialRod=source.specialRod;')
# Stream original MP4s without transcoding or duplicate MP3 tracks.
out=Path('Assets/StreamingAssets/StoryCG');out.mkdir(parents=True,exist_ok=True)
for n in ['00','01','03']:
 src=next(Path('Docs/SourceEvidence/CG20261018').rglob('cg'+n+'*.mp4'));shutil.copy2(src,out/('cg'+n+'.mp4'))
print('Applied gameplay updates and copied three original MP4 files.')
