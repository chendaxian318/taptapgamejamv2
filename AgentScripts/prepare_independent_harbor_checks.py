from pathlib import Path
s=Path('AgentScripts/RaftFirstCampaign.cs').read_text(encoding='utf-8-sig');s=s[:s.index(' public static async Task<string> Run()')]
s=s.replace('RaftFirstCampaignRuntime','IndependentHarborChecks').replace('const string Dir="Verification/feedback-20261018-144126"','const string Dir="Verification/independent-harbor-20261018"')
s+=''' public static async Task<string> Run()
 {
  g=HabitatGame.Instance;if(!Application.isPlaying||!g||g.Busy)throw new Exception("idle Play required");
  Directory.CreateDirectory(Dir);log=new StringBuilder("Harbor/remote merchant/roaming regression; fixtures are NOT a full campaign.\\n");deadline=Now+700;
  var undo=(Stack<HabitatState>)typeof(HabitatGame).GetField("history",F).GetValue(g);var oldUndo=undo.ToArray();
  var presentation=new[]{"chapterGreetingPending","chapterChestPending","chapterMorningGreetingPending","chapterReturnReminderShown","campaignReturnReminderLevel"}.ToDictionary(n=>n,n=>typeof(HabitatGame).GetField(n,F).GetValue(g));
  object original=Call("CaptureSnapshot");bool session=g.SessionStarted,pause=g.paused,opening=g.playOpeningOnNewGame;int tool=g.tool;
  var field=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);object oldFolder=field.GetValue(null);string saves=Path.Combine(Application.persistentDataPath,"Playtests");var beforeHashes=Hashes(saves);field.SetValue(null,Path.GetFullPath(Dir+"/isolated-saves"));string result="FAIL incomplete";
  try{
   g.playOpeningOnNewGame=false;g.NewSession();await Ready();
   Check(HabitatLevels.IsHomeHarbor(g.State)&&g.State.onHarbor&&g.State.animals.Count==0,"new session is an independent home harbor with no puzzle residents");
   Check(!GameObject.Find("Habitat • Island presentation/Terrain"),"home grid terrain hidden");
   Check(g.View.Harbor.Ready&&!g.View.Harbor.LandingOnly&&g.View.Harbor.NearBoat(g.State),"actual imported harbor and boat available");
   Check(!g.View.Harbor.CanEnter(g.State,0,1),"home cannot walk into a puzzle map");
   Check(!g.State.won&&g.State.coins==0&&!g.HasVisitedLevel(5),"home is not a completed or previsited first island; original zero economy");
   g.View.FocusOverview();await Shot("home-harbor");await SaveRoundtrip(1);
   await Shop();Check(g.View.Harbor.NearShop(g.State),"home merchant remains walkable");
   g.OpenShopInteraction();await Dialogue();Check(g.State.chapterOneAccepted,"commission accepted at actual home shop");
   await Boat();g.InteractWithBoat();Check(g.Hud.MapSelectionOpen,"home boat opens destination map");Close();
   g.SailToLevel(5);Check(g.StoryPlaying,"home departure starts");g.Story.Cancel();await Ready();
   Check(HabitatLevels.IsHomeHarbor(g.State)&&!g.HasVisitedLevel(5),"cancelled departure leaves hub and island cache unchanged");
   await Sail(5);g.paused=true;g.tuning.animalMovement=false;await Ready();
   Check(g.View.Harbor.LandingOnly&&!g.View.Harbor.NearHome(g.State)&&!g.View.Harbor.NearShop(g.State),"island has only landing/boat, no home/shop interactions");
   Check(!g.View.Harbor.ModelTransform.GetComponentsInChildren<Transform>().Any(t=>t.name=="Harbor shopkeeper"||t.name=="Wooden harbor on island map"),"island does not instantiate the complete home harbor or merchant");
   g.View.FocusOverview();await Shot("first-island-landing");
   var initial=HabitatRules.Clone(g.State);HabitatRules.Tick(initial,new Tuning(),60f);
   Check(initial.animals.Select(a=>a.x+","+a.z).SequenceEqual(g.State.animals.Select(a=>a.x+","+a.z)),"first island animals do not wander before victory over 60 simulated seconds");
   // Controlled transaction fixture: explicit budget/briefing setup, no campaign pass claim.
   g.State.coins=2000;g.State.chapterOneMorningSeen=true;g.State.campaignUnlockedStage=5;g.State.campaignBriefedMask=62;
   g.OpenShopInteraction();Check(g.Hud.ShopOpen,"island B/controller entry opens remote merchant");Close();
   foreach(var item in new[]{"fan","crate","shovel","magnet","launcher"})
   {int price=HabitatRules.ShopPrice(g.State,item),money=g.State.coins;Check(g.BuyQuantity(item,1),"remote purchase succeeds: "+item);Check(g.State.coins==money-price,"one exact remote debit: "+item);}
   Check(g.State.bagProps.Contains(PropKind.Fan)&&g.State.carryingProp==PropKind.None,"remote bellows goes to inventory without blocking sailing");
   Check(g.State.boatCrates==1&&g.State.shovels==1&&g.State.magnets==1&&g.State.launchers==1,"remote purchases use normal inventory and ship cargo");
   int cash=g.State.coins;g.State.fish=1;Check(!g.SellQuantity(Species.Goldfish,1)&&g.State.coins==cash&&g.State.fish==1,"radio cannot silently sell catch instead of returning");
   var gridFingerprint=JsonUtility.ToJson(g.State.tiles[0]);var animals=g.State.animals.Select(a=>a.x+","+a.z).ToArray();
   int px=g.State.playerX,pz=g.State.playerZ;g.State.facingX=1;g.State.facingZ=0;
   g.TransferCargo(true);await Arrive();Check(g.State.boatCrates==0,"purchased cargo unloads on island shore");g.TransferCargo(false);await Arrive();Check(g.State.boatCrates==1,"shore cargo reloads without loss");
   g.State.facingX=0;g.State.facingZ=1;
   var dock=(Transform)typeof(HabitatMapHarbor).GetField("dock",F).GetValue(g.View.Harbor);Vector3 outward=dock.TransformDirection(Vector3.back);int dx=Mathf.RoundToInt(outward.x),dz=Mathf.RoundToInt(outward.z);
   g.Move(dx,dz,false);await Arrive();Check(g.State.onHarbor,"walked from grid back onto actual island landing");await Boat();
   g.InteractWithBoat();Check(g.StoryPlaying,"island boat starts visible return sailing");
   await Until(()=>!g.StoryPlaying&&HabitatLevels.IsHomeHarbor(g.State),"natural return",20);await Ready();
   Check(g.State.coins==cash&&g.State.boatCrates==1&&g.State.bagProps.Contains(PropKind.Fan)&&g.HasVisitedLevel(5),"return preserves inventory and caches first island");
   Check(!g.State.won,"return does not copy island victory onto harbor");await SaveRoundtrip(3);await Shop();
   Check(g.SellQuantity(Species.Goldfish,1)&&g.State.coins==cash+100,"catch sells at actual home merchant");
   await Sail(5);Check(g.State.animals.Select(a=>a.x+","+a.z).SequenceEqual(animals)&&JsonUtility.ToJson(g.State.tiles[0])==gridFingerprint,"revisit keeps previous island layout/residents");
   Check(g.State.boatCrates==1&&g.State.magnets==1,"revisit carries current shared equipment");
   // All authored destinations must have a working compact landing, including rotated shores.
   foreach(int level in new[]{6,7,8,10,4,9,0}){
    g.LoadLevel(level);g.paused=true;g.tuning.animalMovement=false;await Ready();
    Check(g.View.Harbor.TryPlaceAt(g.State,HabitatImportedDockLayout.BoatApproach),"landing anchor resolves at level "+level);g.View.Render(g.State,g.tuning,g.overlays);await Land();
    Check(!g.State.onHarbor&&g.State.playerX==g.State.dockX&&g.State.playerZ==g.State.dockZ,"correct shore exit at level "+level);
   }
   foreach(int level in new[]{5,6,7,8,10,4,9}){
    var s=HabitatLevels.Create(level);var before=s.animals.Select(a=>a.x+","+a.z).ToArray();HabitatRules.Tick(s,new Tuning(),30);
    Check(s.animals.Select(a=>a.x+","+a.z).SequenceEqual(before),"pre-victory autonomous movement frozen for level "+level);
   }
   var pond=HabitatLevels.Create(5);pond.width=pond.depth=7;pond.tiles=Enumerable.Range(0,49).Select(i=>new Tile{height=1}).ToArray();
   for(int z=2;z<=4;z++)for(int x=2;x<=4;x++)pond.tiles[z*7+x].height=0;
   pond.animals.Clear();pond.animals.Add(new Animal{id=1,species=Species.Goldfish,x=3,z=3,homeX=3,homeZ=3});pond.won=true;pond.playerX=pond.playerZ=1;int moved=0,ox=3,oz=3;
   for(int i=0;i<60;i++){HabitatRules.Tick(pond,new Tuning(),2.6f);var a=pond.animals[0];if(a.x!=ox||a.z!=oz)moved++;ox=a.x;oz=a.z;Check(Math.Abs(a.x-3)+Math.Abs(a.z-3)<=1&&HabitatRules.Evaluate(pond,a,new Tuning()).suitable,"post-victory roam remains within one grid radius and suitable #"+i);}
   Check(moved>10,"completed fish actually roams back and forth");var clone=HabitatRules.Clone(pond);var json=JsonUtility.FromJson<HabitatState>(JsonUtility.ToJson(pond));
   Check(clone.animals[0].roamAnchored&&json.animals[0].roamX==3&&json.animals[0].roamZ==3,"roaming anchor survives Clone and JSON");
   result="PASS independent harbor, real travel/cancel, all eight island landings, remote merchant/cargo, save/revisit, bounded animal movement";Note(result);
  }catch(Exception ex){result="FAIL "+(ex.InnerException??ex).Message;Note(result+"\\n"+ex);}
  finally{g.CancelPendingAction();g.Hud.ClosePanels();Call("RestoreSnapshot",original);undo.Clear();for(int i=oldUndo.Length-1;i>=0;i--)undo.Push(oldUndo[i]);foreach(var p in presentation)typeof(HabitatGame).GetField(p.Key,F).SetValue(g,p.Value);g.playOpeningOnNewGame=opening;g.paused=pause;g.tool=tool;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);field.SetValue(null,oldFolder);var after=Hashes(saves);Check(beforeHashes.Count==after.Count&&beforeHashes.All(p=>after.TryGetValue(p.Key,out string v)&&v==p.Value),"original player save SHA256 unchanged; memory restored");}
  return result;
 }
 static async Task Shot(string name){Close();for(int i=0;i<25;i++)await Frame();ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/"+name+".png"));await Frame();}
}
'''
# inherited classname uses different spelling in some revisions
s=s.replace('public static class RaftFirstCampaign','public static class IndependentHarborChecks')
Path('AgentScripts/IndependentHarborChecks.cs').write_text(s,encoding='utf8')
