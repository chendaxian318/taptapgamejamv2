from pathlib import Path
import shutil
root=Path('Assets/HabitatIsland/Scripts')
backup=Path('ChangeBackups/20261018_IndependentHarbor');backup.mkdir(parents=True,exist_ok=True)
def edit(name,fn):
 p=root/name
 assert not (backup/name).exists(),name
 shutil.copy2(p,backup/name)
 s=p.read_text(encoding='utf-8-sig');n=fn(s);assert n!=s,name
 p.write_text(n,encoding='utf-8-sig')
def rep(s,a,b):
 assert a in s,a[:100]
 return s.replace(a,b)
def levels(s):
 s=rep(s,'"关卡五 · 磁铁与发射器"};','"关卡五 · 磁铁与发射器","家园港口"};\n        public const int HomeHarborIndex = 11;\n        public static bool IsHomeHarbor(HabitatState s) => s != null && s.levelIndex == HomeHarborIndex;')
 s=rep(s,'index>=4&&index<Names.Length','index>=4&&index<HomeHarborIndex')
 s=rep(s,'if(HabitatMechanicLevels.IsOptionalIndex(index))','if(index==HomeHarborIndex)return CreateHomeHarbor();\n            if(HabitatMechanicLevels.IsOptionalIndex(index))')
 pos='        static HabitatState Base(int index)'
 factory='''        static HabitatState CreateHomeHarbor()
        {
            // Same starting economy as the first island, without spawning its puzzle.
            var s=HabitatMechanicLevels.Create(HabitatChapterRules.MainLevel);
            s.levelIndex=HomeHarborIndex;s.levelName=Names[HomeHarborIndex];
            s.width=s.depth=1;s.tiles=new[]{new Tile{height=1}};
            s.playerX=s.playerZ=s.dockX=s.dockZ=0;s.dockRotation=0;
            s.animals.Clear();s.goals=new Species[0];s.earnedSpecies.Clear();
            s.onHarbor=true;s.won=false;s.authoredLevel=true;s.sourceNodeId="home-harbor";
            s.objective="在港口与老板交谈、回家休息，或乘小船前往岛屿。";
            s.message=s.objective;s.events.Clear();return s;
        }
'''
 return rep(s,pos,factory+pos)
edit('HabitatLevels.cs',levels)
edit('HabitatGame.Workflow.cs',lambda s:rep(rep(s,'State=HabitatLevels.Create(startLevel);','State=HabitatLevels.Create(HabitatLevels.HomeHarborIndex);'),'HabitatJourneyRules.Normalize(State);View.Initialize(State);','HabitatJourneyRules.Normalize(State);View.Initialize(State);\n            View.Harbor.TryPlaceAt(State,HabitatImportedDockLayout.BoatApproach);View.Render(State,tuning,overlays);View.FocusPlayer();'))
edit('HabitatGame.cs',lambda s:rep(rep(s,'State = HabitatLevels.Create(startLevel);','State = HabitatLevels.Create(HabitatLevels.HomeHarborIndex);'),'if (State.onHarbor && !View.Harbor.NearShop(State))','if (!View.Harbor.NearShop(State))'))
edit('HabitatChapterRules.cs',lambda s:rep(s,'((s.authoredLevel&&HabitatLevels.IsFormal(s.levelIndex))||s.chapterOneAccepted)','(HabitatLevels.IsHomeHarbor(s)||(s.authoredLevel&&HabitatLevels.IsFormal(s.levelIndex))||s.chapterOneAccepted)'))
def journey(s):
 start=s.index('            if (!View.Harbor.Ready) { State.message = "码头尚未准备好。"; return; }',s.index('public void ReturnToHarbor'))
 end=s.index('\n        public void SailToLevel',start)
 s=s[:start]+'''            if(HabitatLevels.IsHomeHarbor(State))
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
''' +s[end:]
 s=rep(s,'next.onHarbor = false; next.playerX','next.onHarbor = HabitatLevels.IsHomeHarbor(next); next.playerX')
 s=rep(s,'View.Harbor.TryPlaceAt(State, HabitatImportedDockLayout.BoatApproach);\n            State.message = destination', 'View.Harbor.TryPlaceAt(State, HabitatImportedDockLayout.BoatApproach);\n            State.message = destination')
 s=rep(s,'View.Render(State, tuning, overlays); View.FocusPlayer();\n            CaptureRetryPoint();','if(HabitatLevels.IsHomeHarbor(State))State.message="已返航至家园港口。岛屿进度与背包已保留，可到商店交易或回家休息。";\n            View.Render(State, tuning, overlays); View.FocusPlayer();\n            CaptureRetryPoint();')
 return s
edit('HabitatGame.Journey.cs',journey)
# Game was backed up above; combine second surgical change with no duplicate backup.
p=root/'HabitatGame.cs';s=p.read_text(encoding='utf-8-sig');s=rep(s,'if (nearby == HabitatDockInteraction.Boat) Hud.OpenMapSelection();','if (nearby == HabitatDockInteraction.Boat) InteractWithBoat();');p.write_text(s,encoding='utf-8-sig')
def chapter(s):
 s=rep(s,'(State.levelIndex==HabitatFigJamLevel.Index||HabitatMechanicLevels.Contains(State.levelIndex))','(HabitatLevels.IsHomeHarbor(State)||State.levelIndex==HabitatFigJamLevel.Index||HabitatMechanicLevels.Contains(State.levelIndex))')
 s=rep(s,'if (index == HabitatTutorialRules.LevelIndex) return true;','if (index == HabitatLevels.HomeHarborIndex || index == HabitatTutorialRules.LevelIndex) return true;')
 s=rep(s,'var model = View.Harbor.ModelTransform;','var model = View.Harbor.ModelTransform;\n            if(!HabitatLevels.IsHomeHarbor(State) && (State.onHarbor || ChapterStage==HabitatChapterStage.ReturnToShop || ChapterStage==HabitatChapterStage.RestAtHome))\n            { target=model.TransformPoint(HabitatImportedDockLayout.BoatApproach);return true; }')
 return s
edit('HabitatGame.Chapter.cs',chapter)
def harbor(s):
 s=rep(s,'float bridgeWidth;','float bridgeWidth;\n        bool landingOnly, homeHarbor;\n        public bool LandingOnly => landingOnly;')
 s=rep(s,'surfaces.Length > 0','(landingOnly || surfaces.Length > 0)')
 s=rep(s,'float bridgeWidth)\n        {','float bridgeWidth, bool landingOnly=false, bool homeHarbor=false)\n        {\n            this.landingOnly=landingOnly;this.homeHarbor=homeHarbor;')
 s=rep(s,'if (!Ready || s == null || !s.onHarbor) return false;\n            Vector3 target = model.TransformPoint(HabitatImportedDockLayout.ShopApproach);','if (landingOnly || !Ready || s == null || !s.onHarbor) return false;\n            Vector3 target = model.TransformPoint(HabitatImportedDockLayout.ShopApproach);')
 s=rep(s,'return Ready && s != null && s.onHarbor && Horizontal(WorldPosition(s)','return !landingOnly && Ready && s != null && s.onHarbor && Horizontal(WorldPosition(s)')
 s=rep(s,'Vector3 point = kind == HabitatDockInteraction.Home','if(landingOnly && kind!=HabitatDockInteraction.Boat)continue;\n                Vector3 point = kind == HabitatDockInteraction.Home')
 s=rep(s,'float scale = Mathf.Min(Mathf.Abs(model.lossyScale.x),','if(landingOnly)return false;\n            float scale = Mathf.Min(Mathf.Abs(model.lossyScale.x),')
 s=rep(s,'Tile tile = HabitatRules.TileAt(s, s.dockX, s.dockZ);','if(homeHarbor)return false;\n            Tile tile = HabitatRules.TileAt(s, s.dockX, s.dockZ);')
 s=rep(s,'float surface = HabitatRules.Height(s, s.dockX, s.dockZ) * GridStep;','float surface = Mathf.Max(HabitatRules.Height(s, s.dockX, s.dockZ) * GridStep,HabitatRules.WaterSurfaceY(s.waterLevel)+.025f);')
 s=rep(s,'// Old or invalid coordinates recover on the actual shore-side plank,','if(homeHarbor && TryPlaceAt(s,HabitatImportedDockLayout.BoatApproach))\n                return dock.TransformPoint(new Vector3(s.harborX,0f,s.harborZ)) + Vector3.up * (model.position.y-dock.position.y+FootOffset);\n            // Old or invalid coordinates recover on the actual shore-side plank,')
 return s
edit('HabitatMapHarbor.cs',harbor)
def view(s):
 s=rep(s,'bool layoutChanged = tiles == null','tileRoot.gameObject.SetActive(!HabitatLevels.IsHomeHarbor(s));\n            bool layoutChanged = tiles == null')
 s=rep(s,'if (BuildImportedMapDock(dock, s)) return;','if(!HabitatLevels.IsHomeHarbor(s)){BuildIslandLanding(dock,s);return;}\n            if (BuildImportedMapDock(dock, s)) return;')
 s=rep(s,'for (int i = 0; i <= planks; i++)','for (int i = 0; i <= planks && !HabitatLevels.IsHomeHarbor(s); i++)')
 s=rep(s,'Harbor.Bind(model, dock, shore, end, .94f);','Harbor.Bind(model, dock, shore, end, HabitatLevels.IsHomeHarbor(s)?0f:.94f, false, HabitatLevels.IsHomeHarbor(s));')
 s=rep(s,'        bool BuildImportedMapDock(Transform dock, HabitatState s)','''        void BuildIslandLanding(Transform dock, HabitatState s)
        {
            float top=Mathf.Max(HabitatRules.Height(s,s.dockX,s.dockZ)*Step,HabitatRules.WaterSurfaceY(s.waterLevel)+.025f)-dock.position.y;
            Vector3 shore=new Vector3(0,top,-.44f),end=new Vector3(0,top,-2.5f);
            for(int i=0;i<=13;i++)Box("Island landing plank",dock,new Vector3(0,top-.045f,-.4f-i*.17f),new Vector3(.94f,.09f,.18f),materials[i%2==0?"woodLight":"wood"]);
            for(int i=0;i<2;i++)for(int side=-1;side<=1;side+=2)
                Cylinder("Landing mooring post",dock,new Vector3(side*.42f,top-.1f,-.7f-i*1.6f),new Vector3(.08f,.32f,.08f),materials["wood"]);
            // Retain measured boat/story anchors, without instantiating the home or shop.
            Transform model=NewRoot("Island boat anchors",dock);
            model.localScale=Vector3.one*.7f;model.localRotation=Quaternion.Euler(0,-90,0);
            model.localPosition=new Vector3(0,top,-2f)-model.localRotation*(HabitatImportedDockLayout.BoatApproach*.7f);
            Harbor.Bind(model,dock,shore,end,.94f,true,false);
            model.gameObject.AddComponent<HabitatHarborBoat>().Initialize(s.waterLevel);
        }

        bool BuildImportedMapDock(Transform dock, HabitatState s)''')
 # Hub framing has no terrain extent: include actual deck rather than phantom grid.
 s=rep(s,'if (state.onHarbor) mapBounds.Encapsulate(PlayerWorldPosition);','if (state.onHarbor) mapBounds.Encapsulate(PlayerWorldPosition);\n            if(HabitatLevels.IsHomeHarbor(state)&&Harbor.ModelTransform)\n                foreach(var renderer in Harbor.ModelTransform.GetComponentsInChildren<Renderer>())mapBounds.Encapsulate(renderer.bounds);')
 return s
edit('HabitatView.cs',view)
edit('HabitatShopkeeperPlaceholder.cs',lambda s:rep(s,'owner && owner.View != null && owner.View.Harbor.Ready','owner && HabitatLevels.IsHomeHarbor(owner.State) && owner.View != null && owner.View.Harbor.Ready'))
edit('HabitatMainStory.Chapter.cs',lambda s:rep(s,'CurrentCaption = "解开缆绳，乘小船前往荒岛。";','CurrentCaption = HabitatLevels.IsHomeHarbor(game.State)?"解开缆绳，乘小船前往岛屿。":"带上收获，乘小船返回家园港口。";'))
edit('HabitatHud.cs',lambda s:rep(rep(rep(s,'if(game.State.onHarbor&&!game.View.Harbor.NearShop(game.State))','if(!game.View.Harbor.NearShop(game.State))'),'            mapSelection.ConfigureCampaign(index=>','            if(!HabitatLevels.IsHomeHarbor(game.State)){game.InteractWithBoat();return;}\n            mapSelection.ConfigureCampaign(index=>'),'suggestedLevel:game.State.levelIndex','suggestedLevel:game.RecommendedCampaignLevel'))
edit('HabitatHud.Harbor.cs',lambda s:rep(rep(s,'"E  小船 · 选择目的地"','(HabitatLevels.IsHomeHarbor(game.State)?"E  小船 · 选择目的地":"E  小船 · 返航至家园港口")'),'bool nearShop=s.onHarbor?game.View!=null&&game.View.Harbor.NearShop(s):HabitatRules.AtDock(s);','bool nearShop=game.View!=null&&game.View.Harbor.NearShop(s);'))
edit('HabitatHud.Plan.cs',lambda s:rep(s,'nearby==HabitatDockInteraction.Boat?"E / 点击 · 选择目的地":null','nearby==HabitatDockInteraction.Boat?(HabitatLevels.IsHomeHarbor(s)?"E / 点击 · 选择目的地":"E / 点击 · 返航"):null'))
for name in ['HabitatGame.TradeUi.cs','HabitatGame.Purchase.cs']:
 edit(name,lambda s:rep(s,'(State.onHarbor&&!View.Harbor.NearShop(State))','!View.Harbor.NearShop(State)'))
print('Updated',len(list(backup.glob('*.cs'))),'scripts; backups preserved')
