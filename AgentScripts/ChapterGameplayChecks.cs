using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Run once, serially, in an idle main-scene Play session. This is not an authored
// puzzle solution: real opening/travel/dialogue use the untouched 342-cell island;
// fishing uses an explicitly labelled temporary closed pond with ready offspring.
// The fixture is always restored. No save, scene, prefab or source asset is written.
public static class ChapterGameplayChecks
{
    const string SessionKey="Habitat.ChapterGameplayChecks.20261016";
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly string Folder=Path.GetFullPath("Logs/chapter-20261016");
    static Dictionary<string,object> Session=>AppDomain.CurrentDomain.GetData(SessionKey) as Dictionary<string,object>;
    static HabitatGame Game=>(HabitatGame)Session["game"];
    static StringBuilder Report=>(StringBuilder)Session["report"];
    static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,F).GetValue(target);
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,F).SetValue(target,value);
    static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,F).Invoke(target,args);
    static string Json(object value)=>JsonUtility.ToJson(value);
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static void Check(bool value,string message)
    {int count=(int)Session["count"]+1;Session["count"]=count;Report.AppendLine((value?"PASS ":"FAIL ")+count+": "+message);Require(value,message);}
    static Stack<HabitatState> History(HabitatGame game)=>Get<Stack<HabitatState>>(game,"history");
    static List<HabitatGame.VisitedLevel> Visits(HabitatGame game)=>Get<List<HabitatGame.VisitedLevel>>(game,"visitedLevels");
    static string VisitsJson(IEnumerable<HabitatGame.VisitedLevel> values)=>string.Join("|",values.Select(v=>v.index+":"+v.actions+":"+Json(v.state)));
    static string Tiles(HabitatState state)=>state.width+"x"+state.depth+":"+string.Join("|",state.tiles.Select(Json));
    static string Flags(HabitatState state)=>string.Join("|",state.chapterOneAccepted,state.chapterOneSailed,state.chapterOneDelivered,
        state.chapterOneMorningSeen,state.specialRod,state.glassFish,state.glassFishCaught);
    static string Inventory(HabitatState state)=>string.Join("|",state.coins,state.cargo,state.fish,state.shrimp,state.rod,state.traps,
        state.day,state.openingCompleted,state.openingCatchGranted,state.deliveryCount,state.deliveryIncome,Flags(state));
    static string AllShared(HabitatState state)=>Inventory(state)+"|"+string.Join("|",state.deliveredFish,state.deliveredShrimp,
        state.tutorialOfferPending,state.tutorialCompleted);
    static Dictionary<FieldInfo,object> Scalars(object target)=>target.GetType().GetFields(F)
        .Where(f=>!f.IsInitOnly&&!f.IsStatic&&(f.FieldType.IsPrimitive||f.FieldType.IsEnum||f.FieldType==typeof(string)))
        .ToDictionary(f=>f,f=>f.GetValue(target));
    static void PutScalars(object target,Dictionary<FieldInfo,object> values){foreach(var value in values)value.Key.SetValue(target,value.Value);}
    static async Task Frames(int count)
    {
        int start=Time.frameCount;double deadline=UnityEditor.EditorApplication.timeSinceStartup+10;
        while(Time.frameCount-start<count)
        {Require(Application.isPlaying&&UnityEditor.EditorApplication.timeSinceStartup<deadline,"Real Play frames stopped.");await Task.Delay(12);}
    }
    static async Task Until(Func<bool> predicate,float timeout,string label)
    {
        double deadline=UnityEditor.EditorApplication.timeSinceStartup+timeout;
        while(!predicate()){Require(UnityEditor.EditorApplication.timeSinceStartup<deadline,"Timed out: "+label);await Frames(1);}
    }
    static async Task Seconds(float seconds){float start=Time.unscaledTime;while(Time.unscaledTime-start<seconds)await Frames(1);}
    static void Refresh(){Set(Game.Hud,"nextRefresh",0f);Game.Hud.Refresh();}
    static Button Button(string name)
    {
        Refresh();var found=Game.Hud.GetComponentsInChildren<Button>(true).FirstOrDefault(button=>button.name==name);
        Require(found,"Missing HUD button: "+name);return found;
    }
    static async Task Click(string name)
    {
        var button=Button(name);Require(button.gameObject.activeInHierarchy&&button.interactable,"Unavailable HUD button: "+name);
        Require(EventSystem.current,"No live EventSystem.");
        ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        await Frames(2);Refresh();
    }
    static async Task Shot(string name)
    {Refresh();await Frames(2);Directory.CreateDirectory(Folder);string path=Path.Combine(Folder,name+".png");ScreenCapture.CaptureScreenshot(path);await Frames(3);Report.AppendLine("SCREENSHOT "+path);}
    static void StartSession(HabitatGame game)
    {
        Require(Session==null,"A chapter fixture already exists; call Restore first.");
        Require(Application.isPlaying&&game&&game.State!=null&&game.State.levelIndex==HabitatFigJamLevel.Index
            &&!game.Busy&&!game.Hud.ModalOpen&&!game.Fishing.IsOpen&&game.Story&&game.View.Harbor.Ready,
            "Run in the idle formal main scene, after the opening, with panels closed.");
        string[] cameraNames={"cameraYaw","cameraSize","cameraTarget","cameraTargetSmooth","followPlayer","overviewCamera","overviewSize","framingAspect","framingDirty","nextVisibilityCheck"};
        var camera=game.View.GameCamera;
        var session=new Dictionary<string,object>{
            ["game"]=game,["original"]=game.State,["originalJson"]=Json(game.State),["tuning"]=game.tuning,
            ["undo"]=History(game).ToArray(),["undoJson"]=string.Join("|",History(game).Select(Json)),
            ["visits"]=Visits(game).ToArray(),["visitsJson"]=VisitsJson(Visits(game)),
            ["gameScalars"]=Scalars(game),["hudScalars"]=Scalars(game.Hud),["fishingScalars"]=Scalars(game.Fishing),
            ["ui"]=game.Hud.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.gameObject,t=>t.gameObject.activeSelf),
            ["camera"]=cameraNames.ToDictionary(n=>n,n=>typeof(HabitatView).GetField(n,F).GetValue(game.View)),
            ["cameraPosition"]=camera.transform.position,["cameraRotation"]=camera.transform.rotation,["cameraSize"]=camera.orthographicSize,
            ["cameraColor"]=camera.backgroundColor,["cameraClear"]=camera.clearFlags,["cameraOrtho"]=camera.orthographic,
            ["selected"]=EventSystem.current?EventSystem.current.currentSelectedGameObject:null,
            ["oldKeyboard"]=Keyboard.current,["oldMouse"]=Mouse.current,["timeScale"]=Time.timeScale,
            ["background"]=InputSystem.settings.backgroundBehavior,["editorInput"]=InputSystem.settings.editorInputBehaviorInPlayMode,
            ["report"]=new StringBuilder(),["count"]=0,["reportName"]="gameplay-checks.txt"
        };
        AppDomain.CurrentDomain.SetData(SessionKey,session);
        var keyboard=InputSystem.AddDevice<Keyboard>("Chapter acceptance keyboard");session["keyboard"]=keyboard;
        var mouse=InputSystem.AddDevice<Mouse>("Chapter acceptance mouse");session["mouse"]=mouse;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.EnableDevice(keyboard);InputSystem.EnableDevice(mouse);keyboard.MakeCurrent();mouse.MakeCurrent();
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
    }
    static async Task Install(HabitatState state)
    {
        var game=Game;game.CancelPendingAction();if(game.Fishing.IsOpen)game.Fishing.Cancel();game.Hud.ClosePanels();
        game.State=state;game.paused=true;game.speed=1;game.tool=0;game.ActionCount=0;History(game).Clear();
        game.View.Initialize(state);game.View.FocusPlayer();await Frames(3);Refresh();
    }
    static async Task FinishDialogue(Func<bool> finished,string label)
    {
        for(int pages=0;!finished();pages++)
        {
            Require(pages<16&&Game.Hud.ChapterDialogueOpen,"Missing/looping conversation: "+label);
            await Click("Advance chapter dialogue");
        }
    }
    static async Task Roundtrip(string label)
    {
        var game=Game;string flags=Flags(game.State),inventory=Inventory(game.State),tiles=Tiles(game.State);
        Check(Flags(HabitatRules.Clone(game.State))==flags,label+": manual State.Clone retains all chapter fields");
        object snapshot=Call(game,"CaptureSnapshot");string json=Json(snapshot);object copy=JsonUtility.FromJson(json,snapshot.GetType());
        Check(json.Contains("chapterOneAccepted")&&json.Contains("glassFishCaught")&&Flags(Get<HabitatState>(copy,"state"))==flags,
            label+": Snapshot JSON includes commission flags and distinct catch");
        Call(game,"RestoreSnapshot",copy);await Frames(3);Refresh();
        Check(Flags(game.State)==flags&&Inventory(game.State)==inventory&&Tiles(game.State)==tiles,
            label+": in-memory load restores quest, inventory and terrain without writing a save");
    }
    static HabitatState FishingFixture(HabitatState shared)
    {
        var state=HabitatRules.Clone(shared);state.width=state.depth=9;state.tiles=new Tile[81];
        for(int i=0;i<81;i++)state.tiles[i]=new Tile{height=1};
        state.animals.Clear();state.events.Clear();state.earnedSpecies.Clear();state.goals=Array.Empty<Species>();
        state.playerX=state.playerZ=4;state.facingX=0;state.facingZ=1;state.dockX=state.dockZ=1;state.dockRotation=-1;
        state.onHarbor=false;state.carryingAnimal=-1;state.carryingProp=PropKind.None;state.selectedX=4;state.selectedZ=6;
        state.waterLevel=state.tideBaseLevel=state.tickCount=0;state.tideActive=false;state.elapsed=state.movementClock=0;
        state.rod=true;state.specialRod=false;state.rodFromChest=false;state.glassFish=state.glassFishCaught=0;state.won=false;
        state.chapterOneAccepted=state.chapterOneSailed=true;state.chapterOneDelivered=state.chapterOneMorningSeen=false;
        state.levelName="主线钓鱼检查夹具";state.objective="临时封闭池，检查后恢复，不写入存档";
        for(int x=4;x<=5;x++)for(int z=5;z<=6;z++)HabitatRules.TileAt(state,x,z).height=0;
        var chest=HabitatRules.TileAt(state,5,4);chest.prop=PropKind.Chest;chest.chestReward=ChestReward.FishingRod;
        state.animals.Add(new Animal{id=83001,species=Species.Goldfish,x=4,z=6,homeX=4,homeZ=6,offspring=2});
        return state;
    }
    public static async Task<string> Run()
    {
        StartSession(HabitatGame.Instance);Exception failure=null;string result="";
        try
        {
            var game=Game;Visits(game).Clear();game.tuning=new Tuning();Time.timeScale=1;
            await Install(HabitatLevels.Create(HabitatFigJamLevel.Index));
            string authoredTiles=Tiles(game.State);Check(game.State.tiles.Length==342,"formal source still contains exactly 342 authored cells");
            Report.AppendLine("SECTION A — unchanged formal map; actual natural opening, UI callbacks and departure. No hand-authored puzzle solution is claimed.");
            Check(!game.CanTravelToLevel(4,out _)&&game.CanTravelToLevel(0,out _),"before accepting: formal expedition locked; optional ecology practice available");
            Check(game.BeginOpening()&&game.Busy&&game.StoryPlaying,"fresh fixture begins the actual opening Timeline");
            await Until(()=>!game.StoryPlaying,25,"natural opening");await Until(()=>game.Hud.ChapterDialogueOpen,3,"automatic shopkeeper conversation");Refresh();
            Check(game.State.openingCompleted&&game.State.fish==3&&game.State.shrimp==2&&game.State.glassFish==0
                &&!game.State.chapterOneAccepted&&game.View.Harbor.NearShop(game.State),"natural opening grants ordinary fish/shrimp once and opens the real commission dialogue at the shop");
            Check(game.Hud.ChapterCardVisible&&!game.Hud.TutorialCardVisible,"formal commission card replaces the debug objective and optional tutorial recommendation");
            await Shot("01-shopkeeper-dialogue");await Click("Close chapter dialogue");
            Check(!game.State.chapterOneAccepted&&!game.Hud.ChapterDialogueOpen,"closing the dialogue does not accept the commission");
            game.OpenShopInteraction();await Frames(2);
            int firstPage=game.Hud.ChapterDialoguePage;await Click("Advance chapter dialogue");
            Check(game.Hud.ChapterDialogueOpen&&game.Hud.ChapterDialoguePage==firstPage+1&&!game.State.chapterOneAccepted,"an intermediate dialogue page cannot accept early");
            await FinishDialogue(()=>game.State.chapterOneAccepted,"accept commission");
            Check(game.State.chapterOneAccepted&&game.State.glassFish==0&&game.State.fish==3&&game.CanTravelToLevel(4,out _),"explicit final acceptance unlocks the formal expedition without converting opening fish");
            game.Hud.ClosePanels();game.ReturnToHarbor();await Frames(2);game.Hud.OpenMapSelection(4);await Frames(2);
            var selector=Get<HabitatMapSelectionPanel>(game.Hud,"mapSelection");
            var before=game.State;
            foreach(int index in new[]{1,2,3})
            {
                selector.SelectLevel(index);Refresh();
                Check(!game.CanTravelToLevel(index,out string reason)&&!string.IsNullOrWhiteSpace(reason)
                    &&!selector.SelectedDestinationAllowed&&!Button("Confirm destination").interactable,"future chapter "+(index+1)+" is visibly locked and cannot be confirmed");
                selector.ConfirmSelection();await Frames(1);
                Check(ReferenceEquals(game.State,before)&&!game.StoryPlaying,"direct confirmation of locked card "+index+" cannot start travel");
            }
            selector.SelectLevel(0);Check(selector.SelectedDestinationAllowed,"ecology practice stays optional and selectable");
            selector.SelectLevel(4);await Shot("02-campaign-map");
            Check(ReferenceEquals(game.State,before)&&!game.State.chapterOneSailed,"selecting the formal card alone does not sail");
            await Click("Confirm destination");
            Check(game.Busy&&game.StoryPlaying&&ReferenceEquals(game.State,before)&&!game.State.chapterOneSailed,"confirmed departure locks input but does not commit destination during the animation");
            string inventory=Inventory(game.State),tiles=Tiles(game.State);int actions=game.ActionCount;
            game.ApplyTool();game.Move(1,0);game.DeliverChapterCommission();await Frames(1);
            Check(Inventory(game.State)==inventory&&Tiles(game.State)==tiles&&game.ActionCount==actions,"departure Busy rejects actions without early settlement");
            await Click("Pause main story");float storyTime=Get<float>(game.Story,"chapterTime");await Seconds(.24f);
            Check(game.Story.IsPaused&&Mathf.Abs(Get<float>(game.Story,"chapterTime")-storyTime)<.0001f&&ReferenceEquals(game.State,before),"departure pause freezes its actual cinematic clock and leaves state uncommitted");
            game.CancelPendingAction();await Frames(2);
            Check(!game.Busy&&!game.StoryPlaying&&!game.State.chapterOneSailed&&Inventory(game.State)==inventory&&Tiles(game.State)==tiles,"cancelling departure restores control without sailing or awarding anything");
            game.SailToLevel(4);Require(game.StoryPlaying,"retry departure did not begin");
            await Until(()=>game.State.chapterOneSailed,8,"natural departure completion");
            await Until(()=>game.StoryPlaying&&game.Story.CurrentShot=="ChestReveal",3,"first chest reveal");
            Check(game.State.levelIndex==4&&game.State.chapterOneSailed&&!game.State.specialRod&&!game.State.rodFromChest
                &&game.State.openedChests==0&&Tiles(game.State)==authoredTiles,"departure commits once, then chest close-up leaves every source cell and unopened chest intact");
            await Shot("03-rod-chest-hint");await Until(()=>!game.StoryPlaying,7,"natural chest hint end");
            Check(!game.Busy&&!game.State.specialRod&&Tiles(game.State)==authoredTiles,"chest hint ends without granting/opening the rod or changing the 342-cell terrain");
            var arrival=HabitatRules.Clone(game.State);
            await Roundtrip("formal arrival");

            Report.AppendLine("SECTION B — isolated 9x9 pond fixture, four enclosed water cells and two existing offspring. This tests fishing/settlement, not puzzle solving or natural reproduction.");
            await Install(FishingFixture(game.State));
            int ordinaryFish=game.State.fish;game.tool=4;game.Select(4,6);game.ApplyTool();await Frames(2);
            Check(!game.Fishing.IsOpen&&game.State.glassFish==0&&game.State.fish==ordinaryFish&&game.State.animals[0].offspring==2,"ordinary rod cannot start the main-island glass-fish minigame or convert ordinary opening fish");
            game.tool=0;game.Select(5,4);game.ApplyTool();await Frames(2);
            Check(game.State.specialRod&&game.State.rodFromChest&&game.State.rod&&HabitatRules.TileAt(game.State,5,4).prop==PropKind.None,"actual adjacent chest interaction awards the special rod");
            game.tool=4;game.Select(4,6);game.ApplyTool();await Frames(2);
            Check(game.Fishing.IsOpen&&game.View.PlayerVisual.EquipmentAction=="Cast","actual harvest tool opens the fishing challenge and rod cast");
            game.Fishing.Cancel();await Frames(2);
            Check(game.State.glassFish==0&&game.State.animals[0].offspring==2,"cancelling the challenge preserves offspring and commission catch");
            game.Fishing.BarSize=.8f;game.Fishing.CatchGainPerSecond=1f;game.Fishing.EscapeLossPerSecond=0f;
            game.ApplyTool();Require(game.Fishing.IsOpen,"assisted minigame did not begin");
            var keyboard=(Keyboard)Session["keyboard"];int fishingFrames=0;double deadline=UnityEditor.EditorApplication.timeSinceStartup+12;
            while(game.Fishing.IsOpen)
            {
                float velocity=Get<float>(game.Fishing,"velocity");bool held=game.Fishing.Bar+velocity*.15f<game.Fishing.Fish;
                InputSystem.QueueStateEvent(keyboard,held?new KeyboardState(Key.Space):new KeyboardState());await Frames(1);fishingFrames++;
                Require(UnityEditor.EditorApplication.timeSinceStartup<deadline,"assisted real-input fishing exceeded deadline");
            }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Frames(2);
            Check(fishingFrames>=3&&game.State.glassFish==2&&game.State.glassFishCaught==2&&game.State.fish==ordinaryFish
                &&game.State.animals[0].offspring==0&&game.View.PlayerVisual.EquipmentAction=="Hook_Pull",
                "assisted real Update/input completes fishing: exactly two distinct glass fish, ordinary inventory unchanged, offspring removed once");
            await Shot("04-glass-fish-caught");
            HabitatJourneyRules.CopySharedProgress(game.State,arrival);await Install(arrival);
            Report.AppendLine("SECTION C — restore the untouched formal island, carrying fixture-earned catch; shop/home travel uses fixture placement on the real measured deck.");
            int coins=game.State.coins,glass=game.State.glassFish;game.ReturnToHarbor();await Frames(2);game.DeliverChapterCommission();
            Check(!game.State.chapterOneDelivered&&game.State.coins==coins&&game.State.glassFish==glass,"delivery away from the shop is rejected");
            Require(game.View.Harbor.TryPlaceAt(game.State,HabitatImportedDockLayout.ShopApproach),"shop fixture placement failed");game.View.Render(game.State,game.tuning,game.overlays);game.View.FocusPlayer();await Frames(2);
            game.OpenShopInteraction();await Frames(2);Require(game.Hud.ChapterDialogueOpen,"shop delivery dialogue missing");
            game.Hud.ClosePanels();Check(!game.State.chapterOneDelivered&&game.State.coins==coins&&game.State.glassFish==2,"closing delivery dialogue does not consume fish or award currency");
            game.OpenShopInteraction();await FinishDialogue(()=>game.State.chapterOneDelivered,"deliver commission");
            Check(game.State.glassFish==glass-2&&game.State.coins==coins+200&&game.State.fish==ordinaryFish,"explicit near-shop delivery consumes two glass fish and awards exactly 200 coins");
            int day=game.State.day;game.DeliverChapterCommission();
            Check(game.State.glassFish==glass-2&&game.State.coins==coins+200,"duplicate delivery cannot settle the same commission twice");
            await Shot("05-commission-reward");
            if(game.Hud.ChapterDialogueOpen)await FinishDialogue(()=>!game.Hud.ChapterDialogueOpen,"finish reward dialogue");
            else game.FinishChapterRewardDialogue();
            await Until(()=>game.StoryPlaying,3,"return-home sequence");
            await Until(()=>game.Story.CurrentShot=="MorningWindow",22,"walk home into morning Timeline");
            Check(game.State.day==day&&!game.State.chapterOneMorningSeen&&game.Busy,"return-home flow reaches morning without advancing day before its completion");
            await Shot("06-next-morning");Check(game.Story.Skip(),"morning skip is accepted once");await Frames(3);
            Check(game.State.day==day+1&&game.State.chapterOneMorningSeen&&!game.StoryPlaying&&!game.Busy,"completed morning advances one day and completes the first commission");
            Check(!game.Story.Skip(),"repeated skip after completion has no callback");game.FinishChapterRewardDialogue();await Frames(4);
            Check(game.State.day==day+1&&!game.StoryPlaying&&game.State.coins==coins+200,"late reward completion does not replay morning or duplicate rewards");
            await Roundtrip("completed chapter");

            // A nonzero residual value makes shared-catch persistence observable.
            game.State.glassFish=1;string shared=Inventory(game.State);string formalMap=Tiles(game.State);
            game.ReturnToHarbor();await Frames(2);game.SailToLevel(0);Require(game.StoryPlaying,"optional practice departure missing");
            Check(game.Story.Skip(),"skipping actual departure commits its destination");await Frames(3);
            Check(game.State.levelIndex==0&&Inventory(game.State)==shared&&game.HasVisitedLevel(4),"cross-island sailing preserves all chapter flags and distinct catch while caching formal terrain");
            string once=Inventory(game.State);Check(!game.Story.Skip()&&Inventory(game.State)==once,"repeated departure skip cannot commit twice");
            await Roundtrip("cross-island chapter snapshot");
            Check(game.HasVisitedLevel(4),"cross-island JSON also restores the formal island cache");
            game.ReturnToHarbor();await Frames(2);game.SailToLevel(4);Require(game.StoryPlaying,"return departure missing");game.Story.Skip();await Frames(3);
            Check(game.State.levelIndex==4&&Inventory(game.State)==shared&&Tiles(game.State)==formalMap&&!game.StoryPlaying,
                "returning after completion preserves terrain/inventory and does not replay chest hint or morning");
            Report.AppendLine("PASS: authored-map entry and cinematics, actual UI/guard paths, isolated assisted fishing, settlement and cross-island persistence. Full authored puzzle traversal remains a separate check.");
        }
        catch(Exception exception){failure=exception;Report.AppendLine("ERROR "+exception);}
        finally{result=Restore();}
        if(failure!=null)throw new InvalidOperationException(result,failure);return result;
    }
    public static async Task<string> RunFollowup()
    {
        StartSession(HabitatGame.Instance);Session["reportName"]="followup-checks.txt";
        Exception failure=null;string result="";
        try
        {
            var game=Game;Visits(game).Clear();game.tuning=new Tuning();Time.timeScale=1;
            var source=HabitatLevels.Create(HabitatFigJamLevel.Index);
            source.openingCompleted=source.openingCatchGranted=true;source.chapterOneAccepted=source.chapterOneSailed=true;
            await Install(source);
            string originalTiles=Tiles(source),originalAnimals=string.Join("|",source.animals.Select(Json));
            int[] originalIds=source.animals.Where(a=>a.species==Species.Goldfish).Select(a=>a.id).ToArray();
            Check(source.tiles.Length==342&&originalIds.Length==2&&source.animals.Where(a=>a.species==Species.Goldfish)
                .All(a=>a.offspring==0&&a.spawnedTotal==0),"followup starts with the original 342-cell map and its two actual fish, no fabricated offspring");
            Report.AppendLine("SECTION D — original formal terrain and original animals. Only actor position/held soil are fixture setup; both terrain changes use the actual tool -> PlaceSoil. Breeding uses default real Update, no manual Tick, speed changes or animal edits.");
            int[] actorXs={6,9},gapXs={7,8};
            for(int i=0;i<2;i++)
            {
                source.playerX=actorXs[i];source.playerZ=9;source.onHarbor=false;source.carryingProp=PropKind.Climb;
                game.View.Initialize(source);await Frames(2);game.tool=1;game.Select(gapXs[i],8);game.ApplyTool();await Frames(2);
                Check(HabitatRules.TileAt(source,gapXs[i],8).height==1&&source.carryingProp==PropKind.None,
                    "actual soil tool closes authored gap "+gapXs[i]+",8 from fixture standing position "+actorXs[i]+",9");
            }
            var expected=HabitatLevels.Create(4);HabitatRules.TileAt(expected,7,8).height=1;HabitatRules.TileAt(expected,8,8).height=1;
            Check(Tiles(source)==Tiles(expected)&&string.Join("|",source.animals.Select(Json))==originalAnimals,
                "only the two requested gaps changed; all animals still retain original coordinates/progress/offspring");
            foreach(int id in originalIds)
            {
                var fish=source.animals.Single(a=>a.id==id);var habitat=HabitatRules.Evaluate(source,fish,game.tuning);
                Check(habitat.suitable&&habitat.areaClosed&&habitat.waterSize==2,"original fish "+id+" evaluates suitable in its real closed two-cell pool");
            }
            Check(Mathf.Abs(game.tuning.breedSeconds-18f)<.001f&&game.tuning.animalMovement&&Mathf.Abs(game.speed-1f)<.001f,
                "real breeding retains default 18 seconds, autonomous movement enabled and speed 1");
            float simulatedStart=source.elapsed,wallStart=Time.unscaledTime;game.paused=false;
            await Until(()=>originalIds.All(id=>source.animals.Single(a=>a.id==id).offspring>=1),27,"18-second natural authored fish breeding");
            game.paused=true;float simulated=source.elapsed-simulatedStart,wall=Time.unscaledTime-wallStart;await Frames(2);
            Check(originalIds.All(id=>source.animals.Single(a=>a.id==id).offspring==1&&source.animals.Single(a=>a.id==id).spawnedTotal==1)
                &&source.animals.Count(a=>a.species==Species.Goldfish)==2&&simulated>=17.99f&&wall>=17.5f,
                "both original parents naturally produce one offspring after "+simulated.ToString("F2")+" simulated / "+wall.ToString("F2")+" real seconds");
            Check(source.glassFish==0&&source.glassFishCaught==0,"natural breeding alone does not settle caught commission inventory");
            game.View.FocusTutorialTarget(new Vector3(7.5f,.2f,9));await Seconds(.4f);await Shot("followup-01-original-fish-breeding");

            Report.AppendLine("SECTION E — distinct fixture values test shared-progress preservation when resetting the formal island; all source terrain/animals must reset, the other island cache must remain.");
            source.coins=317;source.cargo=5;source.fish=11;source.shrimp=7;source.traps=3;source.rod=source.specialRod=true;
            source.day=9;source.chapterOneAccepted=source.chapterOneSailed=source.chapterOneDelivered=source.chapterOneMorningSeen=true;
            source.glassFish=3;source.glassFishCaught=5;source.deliveredFish=13;source.deliveredShrimp=17;source.deliveryCount=6;source.deliveryIncome=189;
            source.tutorialCompleted=source.tutorialOfferPending=true;
            var other=HabitatLevels.Create(0);other.tiles[0].height=2;other.tutorialObservedCrab=true;
            Visits(game).Add(new HabitatGame.VisitedLevel{index=0,actions=17,state=other});
            Visits(game).Add(new HabitatGame.VisitedLevel{index=4,actions=99,state=HabitatRules.Clone(source)});
            string shared=AllShared(source),otherCache=VisitsJson(Visits(game).Where(v=>v.index==0));
            game.RestartCurrentLevel();await Frames(3);Refresh();
            var reset=game.State;
            Check(!ReferenceEquals(reset,source)&&AllShared(reset)==shared,"formal restart preserves every shared commission/equipment/inventory/day/delivery/tutorial field");
            Check(Tiles(reset)==originalTiles&&string.Join("|",reset.animals.Select(Json))==originalAnimals&&game.UndoCount==0&&game.ActionCount==0,
                "formal restart restores source terrain and unbred animals and clears local undo/actions");
            Check(VisitsJson(Visits(game).Where(v=>v.index==0))==otherCache&&!Visits(game).Any(v=>v.index==4),
                "formal restart retains the other island cache and removes only its own obsolete cache");

            Report.AppendLine("SECTION F — shop fixture sets commission status, balance and measured shop position only; real buttons and transaction rules decide availability/cost.");
            var shop=HabitatLevels.Create(4);shop.openingCompleted=shop.openingCatchGranted=true;
            shop.chapterOneAccepted=shop.chapterOneSailed=true;shop.coins=250;
            await Install(shop);Require(game.View.Harbor.TryPlaceAt(shop,HabitatImportedDockLayout.ShopApproach),"shop followup placement failed");
            game.View.Render(shop,game.tuning,game.overlays);game.Hud.ToggleShop();await Frames(2);Refresh();
            var fan=Button("Buy fan");var fanCost=Get<TMPro.TMP_Text>(game.Hud,"fanShopCost");
            Check(!fan.interactable&&fanCost.text.Contains("委托"),"actual fan shop card is disabled and explains its commission unlock requirement");
            int oldActions=game.ActionCount;
            ExecuteEvents.Execute(fan.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);await Frames(2);
            Check(shop.coins==250&&shop.carryingProp==PropKind.None&&game.ActionCount==oldActions,"clicking the disabled fan card cannot buy early");
            shop.chapterOneDelivered=true;Refresh();
            Check(fan.interactable&&fanCost.text=="200 贝币","after fixture delivery flag the actual fan card shows the campaign price of 200");
            await Shot("followup-02-fan-unlocked");await Click("Buy fan");
            Check(shop.coins==50&&shop.carryingProp==PropKind.Fan&&game.ActionCount==oldActions+1,"actual fan purchase charges exactly 200 once and puts one fan in hand");
            await Click("Buy fan");
            Check(shop.coins==50&&shop.carryingProp==PropKind.Fan&&game.ActionCount==oldActions+1,"repeat purchase while holding the fan neither charges again nor duplicates it");
            shop.carryingProp=PropKind.None;shop.coins=10;await Click("Buy fan");
            Check(shop.coins==10&&shop.carryingProp==PropKind.None&&game.ActionCount==oldActions+1,"a campaign buyer with the legacy 10-coin balance cannot buy the 200-coin fan");

            Report.AppendLine("SECTION G — source-map presentation checks. Quest and actor location are fixture setup; short cinematics run for real before screenshots, then cancel without callbacks.");
            var cinematic=HabitatLevels.Create(4);cinematic.openingCompleted=cinematic.openingCatchGranted=true;
            cinematic.chapterOneAccepted=cinematic.chapterOneSailed=true;await Install(cinematic);
            int chestIndex=Array.FindIndex(cinematic.tiles,t=>t.prop==PropKind.Chest&&t.chestReward==ChestReward.FishingRod);
            Require(chestIndex>=0,"original special rod chest is missing");int chestX=chestIndex%cinematic.width,chestZ=chestIndex/cinematic.width;
            game.View.FocusPlayer();await Seconds(.5f);var camera=game.View.GameCamera;
            Vector3 cameraBefore=camera.transform.position;Quaternion angleBefore=camera.transform.rotation;
            float sizeBefore=camera.orthographicSize;int completed=0;
            Check(game.Story.BeginChestReveal(chestX,chestZ,()=>completed++),"real source chest close-up begins");
            await Until(()=>Get<float>(game.Story,"chapterTime")>=1.5f,5,"fully focused chest shot");
            Vector3 center=new Vector3(chestX,HabitatRules.Height(cinematic,chestX,chestZ)*.7f+.18f,chestZ);
            Vector3 viewport=camera.WorldToViewportPoint(center);
            Check(game.StoryPlaying&&Vector2.Distance(new Vector2(viewport.x,viewport.y),new Vector2(.5f,.5f))<.015f
                &&Mathf.Abs(camera.orthographicSize-2.8f)<.02f,"after 1.5 seconds the chest is centered and the intended close-up scale is reached");
            await Shot("followup-03-chest-fully-focused");game.CancelPendingAction();
            Check(!game.StoryPlaying&&!game.View.IsCinematic&&completed==0&&Vector3.Distance(camera.transform.position,cameraBefore)<.001f
                &&Quaternion.Angle(camera.transform.rotation,angleBefore)<.01f&&Mathf.Abs(camera.orthographicSize-sizeBefore)<.001f,
                "cancelling the close-up immediately restores its exact camera and never calls completion");
            Check(!cinematic.specialRod&&!cinematic.rodFromChest&&cinematic.openedChests==0&&Tiles(cinematic)==originalTiles,
                "viewing/cancelling a close-up does not grant the special rod or open/change the original chest");
            await Frames(2);game.ReturnToHarbor();await Frames(2);var beforeTravel=game.State;game.SailToLevel(0);
            Require(game.StoryPlaying,"departure followup did not start");await Until(()=>Get<float>(game.Story,"chapterTime")>=1f,4,"visible departure pose");
            Check(game.Busy&&ReferenceEquals(game.State,beforeTravel)&&game.State.levelIndex==4,"one-second departure still displays source state until travel finishes");
            await Shot("followup-04-departure-in-motion");game.CancelPendingAction();await Frames(2);
            Check(!game.Busy&&!game.StoryPlaying&&ReferenceEquals(game.State,beforeTravel)&&game.State.levelIndex==4,"cancelling screenshot departure leaves the original destination uncommitted");
            Report.AppendLine("PASS: real default authored-fish breeding, campaign reset preservation, fan availability/200-coin purchase, focused chest and in-motion departure captures. Full actor traversal of the puzzle is still outside this followup.");
        }
        catch(Exception exception){failure=exception;Report.AppendLine("ERROR "+exception);}
        finally{result=Restore();}
        if(failure!=null)throw new InvalidOperationException(result,failure);return result;
    }

    public static string Restore()
    {
        var session=Session;if(session==null)return "No chapter fixture is active.";
        var report=(StringBuilder)session["report"];var game=(HabitatGame)session["game"];
        try
        {
            // Cancel every callback before restoring the original State reference.
            game.CancelPendingAction();if(game.Fishing.IsOpen)game.Fishing.Cancel();game.Hud.ClosePanels();
            var original=(HabitatState)session["original"];game.State=original;game.tuning=(Tuning)session["tuning"];
            var history=History(game);var undo=(HabitatState[])session["undo"];history.Clear();for(int i=undo.Length-1;i>=0;i--)history.Push(undo[i]);
            var visits=Visits(game);visits.Clear();visits.AddRange((HabitatGame.VisitedLevel[])session["visits"]);
            PutScalars(game,(Dictionary<FieldInfo,object>)session["gameScalars"]);PutScalars(game.Fishing,(Dictionary<FieldInfo,object>)session["fishingScalars"]);
            game.View.Initialize(original);game.View.Render(original,game.tuning,game.overlays);
            foreach(var item in (Dictionary<string,object>)session["camera"])Set(game.View,item.Key,item.Value);
            var camera=game.View.GameCamera;camera.transform.SetPositionAndRotation((Vector3)session["cameraPosition"],(Quaternion)session["cameraRotation"]);
            camera.orthographicSize=(float)session["cameraSize"];camera.backgroundColor=(Color)session["cameraColor"];
            camera.clearFlags=(CameraClearFlags)session["cameraClear"];camera.orthographic=(bool)session["cameraOrtho"];Refresh();
            foreach(var item in (Dictionary<GameObject,bool>)session["ui"])if(item.Key)item.Key.SetActive(item.Value);
            PutScalars(game.Hud,(Dictionary<FieldInfo,object>)session["hudScalars"]);
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject((GameObject)session["selected"]);
            bool restored=ReferenceEquals(game.State,original)&&Json(original)==(string)session["originalJson"]
                &&history.SequenceEqual(undo)&&string.Join("|",history.Select(Json))==(string)session["undoJson"]
                &&VisitsJson(visits)==(string)session["visitsJson"]&&ReferenceEquals(game.tuning,session["tuning"])&&!game.Busy;
            report.AppendLine((restored?"PASS":"FAIL")+": original State/ref, history, visits, tuning, controls, camera and UI restored; no user save written.");
            Require(restored,"Original runtime restoration mismatch.");
        }
        finally
        {
            if(session.TryGetValue("keyboard",out object keyboard)&&keyboard is Keyboard k)InputSystem.RemoveDevice(k);
            if(session.TryGetValue("mouse",out object mouse)&&mouse is Mouse m)InputSystem.RemoveDevice(m);
            InputSystem.settings.backgroundBehavior=(InputSettings.BackgroundBehavior)session["background"];
            InputSystem.settings.editorInputBehaviorInPlayMode=(InputSettings.EditorInputBehaviorInPlayMode)session["editorInput"];
            (session["oldKeyboard"] as Keyboard)?.MakeCurrent();(session["oldMouse"] as Mouse)?.MakeCurrent();Time.timeScale=(float)session["timeScale"];
            string reportName=session.TryGetValue("reportName",out object name)?(string)name:"gameplay-checks.txt";
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,reportName),report.ToString());
            AppDomain.CurrentDomain.SetData(SessionKey,null);
        }
        return report.ToString();
    }
}
