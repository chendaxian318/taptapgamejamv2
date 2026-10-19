using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using HabitatIsland;

// Genuine continuous campaign: no injected map, equipment, money, residents or completion flags.
// Input is driven through the same controller methods as the existing controls.
public static class FullCampaignRuntime
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 const string Dir="Verification/feedback-20261018-140142";
 static HabitatGame g; static StringBuilder log; static double deadline; static int routeStep;
 static double Now=>UnityEditor.EditorApplication.timeSinceStartup;
 static object Call(string name,params object[] args)=>typeof(HabitatGame).GetMethod(name,F).Invoke(g,args);
 static void Note(string s){log.AppendLine(s);File.WriteAllText(Dir+"/full-campaign-runtime.txt",log.ToString());}
 static void Check(bool ok,string text){Note((ok?"PASS ":"FAIL ")+text);if(!ok)throw new Exception(text+"; "+Describe());}
 static string Describe()=>"level="+g.State.levelIndex+" P="+g.State.playerX+","+g.State.playerZ+" harbor="+g.State.onHarbor+" held="+g.State.carryingProp+"/"+g.State.carryingAnimal+" money="+g.State.coins+" busy="+g.Busy+" "+g.State.message;
 static async Task Frame(){int n=Time.frameCount;while(Time.frameCount==n){if(Now>deadline||!Application.isPlaying)throw new TimeoutException("campaign deadline/play interrupted");await Task.Delay(8);}}
 static async Task Until(Func<bool> done,string label,double seconds=20){double end=Now+seconds;while(!done()){if(Now>end)throw new TimeoutException(label+"; "+Describe());await Frame();}}
 static async Task Arrive(){await Frame();await Until(()=>!g.Busy&&g.View.ResidentsArrived(g.State),"arrival");}
 static void Close(){g.Hud.ClosePanelsFromInput();}
 static async Task Dialogue(){for(int i=0;i<24&&g.Hud.ChapterDialogueOpen&&!g.Busy;i++){g.Hud.AdvanceChapterDialogue();await Frame();}}
 static async Task Ready(){await Arrive();if(g.Hud.ChapterDialogueOpen)await Dialogue();if(g.Hud.ModalOpen)Close();await Arrive();}
 static int[][] Route(string name)=>Regex.Matches(File.ReadAllText(Dir+"/"+name+"-route.json"),@"\[\s*(-?\d+(?:\s*,\s*-?\d+)*)\s*\]").Cast<Match>().Select(m=>m.Groups[1].Value.Split(',').Select(x=>int.Parse(x.Trim())).ToArray()).ToArray();
 static async Task Act(int[] a)
 {
  await Ready();int before=g.ActionCount,expected=1;
  switch(a[0]){
   case 11:g.tool=0;g.Select(a[1],a[2]);g.ApplyTool();break; case 12:g.Pull(a[1],a[2]);break; case 13:g.Pull(g.State.playerX-a[1],g.State.playerZ-a[2]);break; case 0:g.Move(a[1],a[2],true);break;
   case 1:g.tool=0;g.Select(a[1],a[2]);g.ApplyTool();break;
   case 2:g.tool=2;g.Select(a[1],a[2]);g.ApplyTool();break;
   case 3:g.Select(a[1],a[2]);g.RotateTarget();break;
   case 5:g.TakeInventoryItem(a[1]);break;
   case 6:Call("AdvanceSimulation",a[1]/10f);expected=0;break;
   case 7:g.tool=a[1];Check(g.ConfirmUtility(a[2],a[3],a[4],a[5]),"utility accepted with real inventory");break;
   case 9:g.TransferCargo(a[1]!=0);break;
   case 10:g.Move(a[1],a[2],true);expected=0;break;
   default:throw new Exception("Unsupported live action "+a[0]);
  }
  await Arrive();
  if(g.ActionCount!=before+expected)throw new Exception("route "+routeStep+" ["+string.Join(",",a)+"] expected action delta "+expected+" got "+(g.ActionCount-before)+"; "+Describe());
 }
 static async Task PlayRoute(string name)
 {
  Note("BEGIN ROUTE "+name+" "+Describe());routeStep=0;
  foreach(var a in Route(name)){routeStep++;if(a[0]==4||a[0]==8)continue;await Act(a);if(routeStep%25==0)Note("PROGRESS "+name+" step "+routeStep);}
  await Until(()=>g.State.won&&!g.Busy&&g.View.ResidentsArrived(g.State),"actual victory after route");
  Check(g.State.animals.All(a=>HabitatRules.Evaluate(g.State,a,g.tuning).suitable),name+" all residents suitable; real victory, "+routeStep+" encoded actions");
  File.WriteAllText(Dir+"/full-campaign-"+name+".json",JsonUtility.ToJson(Call("CaptureSnapshot"),true));
  Close();await Frame();
 }
 sealed class WalkNode{public HabitatState state;public List<int[]> moves;}
 static List<int[]> FindWalk(Func<HabitatState,bool> done)
 {
  var q=new Queue<WalkNode>();var seen=new HashSet<string>();q.Enqueue(new WalkNode{state=HabitatRules.Clone(g.State),moves=new List<int[]>()});
  int[] dx={0,1,0,-1},dz={1,0,-1,0};
  while(q.Count>0&&seen.Count<30000){var n=q.Dequeue();if(done(n.state))return n.moves;
   for(int d=0;d<4;d++){var s=HabitatRules.Clone(n.state);if(!HabitatRules.MovePlayer(s,dx[d],dz[d],true,g.tuning))continue;
    string key=s.playerX+","+s.playerZ+":"+string.Join(";",s.animals.Select(a=>a.x+","+a.z));if(!seen.Add(key))continue;
    var p=new List<int[]>(n.moves);p.Add(new[]{0,dx[d],dz[d]});q.Enqueue(new WalkNode{state=s,moves=p});}}
  throw new Exception("No legal walking path; "+Describe());
 }
 static async Task GridWalk(Func<HabitatState,bool> done){foreach(var a in FindWalk(done))await Act(a);}
 static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
 static async Task HarborTo(Vector3 target)
 {
  await Ready();Check(g.State.onHarbor,"continuous harbor walking starts on deck");
  var h=g.View.Harbor;Vector3 origin=h.WorldPosition(g.State);const float step=.23f;
  var ground=typeof(HabitatMapHarbor).GetMethod("TryGround",F);var cache=new Dictionary<Vector2Int,bool>();
  Func<Vector2Int,Vector3> point=k=>origin+new Vector3(k.x*step,0,k.y*step);
  Func<Vector2Int,bool> valid=k=>{if(cache.TryGetValue(k,out bool v))return v;object[] a={point(k),Vector3.zero};v=(bool)ground.Invoke(h,a);cache[k]=v;return v;};
  var start=Vector2Int.zero;var open=new List<Vector2Int>{start};var cost=new Dictionary<Vector2Int,float>{{start,0}};var parent=new Dictionary<Vector2Int,Vector2Int>();var closed=new HashSet<Vector2Int>();
  Vector2Int end=start;bool found=false;
  while(open.Count>0&&closed.Count<25000){int best=0;float score=float.MaxValue;for(int i=0;i<open.Count;i++){float f=cost[open[i]]+FlatDistance(point(open[i]),target);if(f<score){score=f;best=i;}}var k=open[best];open.RemoveAt(best);if(!closed.Add(k))continue;
   if(FlatDistance(point(k),target)<.28f){end=k;found=true;break;}
   for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++){if(x==0&&z==0)continue;var n=k+new Vector2Int(x,z);if(closed.Contains(n)||!valid(n)||x!=0&&z!=0&&(!valid(k+new Vector2Int(x,0))||!valid(k+new Vector2Int(0,z))))continue;float c=cost[k]+step*(x!=0&&z!=0?1.414214f:1);if(!cost.TryGetValue(n,out float old)||c<old){cost[n]=c;parent[n]=k;open.Add(n);}}}
  if(!found)throw new Exception("No collision-valid harbor path to "+target+" from "+origin+" visited="+closed.Count);
  var path=new List<Vector3>();for(var k=end;k!=start;k=parent[k])path.Add(point(k));path.Reverse();path.Add(target);
  foreach(var p in path){double endTime=Now+6;while(g.State.onHarbor&&FlatDistance(h.WorldPosition(g.State),p)>.075f){if(Now>endTime)throw new Exception("Harbor collision stalled "+h.WorldPosition(g.State)+" -> "+p);if(g.Hud.ModalOpen)Close();Vector3 d=p-h.WorldPosition(g.State);float dt=Mathf.Min(.04f,FlatDistance(p,h.WorldPosition(g.State))/2.5f);Call("MoveHarbor",(Func<bool>)(()=>h.MoveWorld(g.State,d,dt)));await Frame();}}
 }
 static async Task Shop(){await HarborTo(g.View.Harbor.ModelTransform.TransformPoint(HabitatImportedDockLayout.ShopApproach));Check(g.View.Harbor.NearShop(g.State),"walked to shop through collider-valid deck");}
 static async Task Boat(){await HarborTo(g.View.Harbor.ModelTransform.TransformPoint(HabitatImportedDockLayout.BoatApproach));Check(g.View.Harbor.NearBoat(g.State),"walked to boat");}
 static async Task Land()
 {
  await Ready();for(int i=0;i<4&&g.State.onHarbor;i++)await HarborTo(g.View.Harbor.LandingGuidePosition(g.State));
  if(g.State.onHarbor){var h=g.View.Harbor;var dock=(Transform)typeof(HabitatMapHarbor).GetField("dock",F).GetValue(h);for(int i=0;i<15&&g.State.onHarbor;i++){Call("MoveHarbor",(Func<bool>)(()=>h.MoveWorld(g.State,dock.TransformDirection(Vector3.forward),.04f)));await Frame();}}
  Check(!g.State.onHarbor&&g.State.playerX==g.State.dockX&&g.State.playerZ==g.State.dockZ,"walked off actual plank onto island spawn");await Ready();
 }
 static async Task Sail(int level)
 {
  await Boat();Close();Check(g.CanTravelToLevel(level,out _),"real prerequisites permit island "+level);g.SailToLevel(level);Check(g.StoryPlaying,"departure sequence started");
  await Until(()=>!g.StoryPlaying&&g.State.levelIndex==level,"natural departure completion",45);await Ready();await Land();
 }
 static async Task Catch(int id)
 {
  Func<HabitatState,bool> harvestReach=s=>{var a=s.animals.Find(v=>v.id==id);return HabitatRules.CanHarvest(s,a,g.tuning,out _);}; List<int[]> approach;try{approach=FindWalk(harvestReach);}catch{Note("Harvest requires returning through the harbor to the other shore");await CollectShrimp();await Return();await Land();approach=FindWalk(harvestReach);}foreach(var a in approach)await Act(a);
  await Ready();var target=g.State.animals.Find(a=>a.id==id);int count=g.State.fish+g.State.glassFish+g.State.jellyfish;
  g.Fishing.enabled=false;g.tool=4;g.Select(target.x,target.z);g.ApplyTool();Check(g.Fishing.IsOpen,"fishing challenge opened for "+target.species+"#"+id);
  double end=Now+38;float previous=g.Fishing.Fish;double last=Now;
  while(g.Fishing.IsOpen){if(Now>end)throw new TimeoutException("fishing challenge");float dt=(float)Math.Max(.005,Now-last);float derivative=(g.Fishing.Fish-previous)/dt;previous=g.Fishing.Fish;last=Now;
   float velocity=(float)typeof(HabitatFishing).GetField("velocity",F).GetValue(g.Fishing);float desired=Mathf.Clamp(derivative+5*(g.Fishing.Fish-g.Fishing.Bar),-.72f,.72f);bool held=velocity<desired;
   g.Fishing.Advance(Mathf.Min(Time.unscaledDeltaTime,.1f),held);await Frame();}
  g.Fishing.enabled=true;await Arrive();
  Check(g.State.fish+g.State.glassFish+g.State.jellyfish>count,"real hold/release fishing completed and paid catch once");
 }
 static async Task HarvestAll(){foreach(int id in g.State.animals.Where(a=>!a.carried&&(a.species==Species.Goldfish||a.species==Species.Jellyfish)&&!a.catchClaimed).OrderBy(a=>Math.Abs(a.x-g.State.playerX)+Math.Abs(a.z-g.State.playerZ)).Select(a=>a.id).ToArray())await Catch(id);}
 static async Task CollectShrimp(){if(g.State.carryingAnimal>=0||g.State.carryingProp!=PropKind.None)return;foreach(int id in g.State.animals.Where(a=>!a.carried&&a.species==Species.Shrimp).Select(a=>a.id).ToArray()){List<int[]> path;try{path=FindWalk(s=>{var a=s.animals.Find(v=>v.id==id);return Math.Abs(a.x-s.playerX)+Math.Abs(a.z-s.playerZ)<=1;});}catch{continue;}foreach(var a in path)await Act(a);var target=g.State.animals.Find(a=>a.id==id);await Act(new[]{1,target.x,target.z});Check(g.State.carryingAnimal==id,"picked up won-island shrimp for legitimate return sale");break;}}
 static async Task Return(){await Ready();g.ReturnToHarbor();await Arrive();Check(g.State.onHarbor,"return-to-harbor action; carried shrimp stored only after victory");}
 static async Task SellAndBrief(int stage)
 {
  await CollectShrimp();await Return();await Shop();Close();int money=g.State.coins,value=HabitatRules.CatchSaleValue(g.State);if(value>0){g.Buy("sell");await Arrive();Check(g.State.coins==money+value,"actual sale income "+value+", balance "+g.State.coins);}
  g.OpenShopInteraction();await Dialogue();await Arrive();Close();Check(HabitatChapterRules.HasBriefing(g.State,stage),"real shop dialogue unlock/ending stage "+stage);
 }
 static async Task Buy(string name,int cost){await Ready();int before=g.State.coins;g.Buy(name);await Arrive();Check(g.State.coins==before-cost,"bought "+name+" for "+cost+" earned coins");}
 static async Task Shell()
 {
  if(g.State.carryingAnimal>=0){await Return();await Land();}
  var s=g.State;int tile=Array.FindIndex(s.tiles,t=>t.prop==PropKind.Shell);Check(tile>=0,"shell exists after actual crab exit animation");int x=tile%s.width,z=tile/s.width;
  await GridWalk(q=>Math.Abs(q.playerX-x)+Math.Abs(q.playerZ-z)<=1);int n=g.State.shells;await Act(new[]{1,x,z});Check(g.State.shells==n+1,"collected real shed shell");
 }
 static async Task SaveRoundtrip(int slot)
 {
  await Ready();int coins=g.State.coins,stage=g.State.campaignUnlockedStage;var won=new[]{5,6,7,8,10}.Select(g.HasCompletedLevel).ToArray();
  Check(g.SaveSession(slot),"saved isolated slot "+slot);Check(g.LoadSession(slot),"loaded isolated slot "+slot);await Ready();Check(g.State.coins==coins&&g.State.campaignUnlockedStage==stage&&new[]{5,6,7,8,10}.Select(g.HasCompletedLevel).SequenceEqual(won),"save/load retains earned money, stage and island completion");
 }
 static Dictionary<string,string> Hashes(string folder){var d=new Dictionary<string,string>();if(Directory.Exists(folder))foreach(string p in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))using(var sha=SHA256.Create())d[p]=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p)));return d;}
 public static async Task<string> Run()
 {
  g=HabitatGame.Instance;if(!Application.isPlaying||!g||g.Busy)throw new Exception("idle Play required");
  Directory.CreateDirectory(Dir);log=new StringBuilder("Continuous new-game campaign, real Update frames; fishing hold/release uses production Advance entry once per frame; UI files untouched.\n");deadline=Now+1400;
  var undo=(Stack<HabitatState>)typeof(HabitatGame).GetField("history",F).GetValue(g);var oldUndo=undo.ToArray(); var presentation=new[]{"chapterGreetingPending","chapterChestPending","chapterMorningGreetingPending","chapterReturnReminderShown","campaignReturnReminderLevel"}.ToDictionary(n=>n,n=>typeof(HabitatGame).GetField(n,F).GetValue(g)); object original=Call("CaptureSnapshot");bool session=g.SessionStarted,pause=g.paused,opening=g.playOpeningOnNewGame;int tool=g.tool;
  var field=typeof(HabitatGame).GetField("SaveSlotDirectoryForTests",BindingFlags.Static|BindingFlags.NonPublic);object oldFolder=field.GetValue(null);string saves=Path.Combine(Application.persistentDataPath,"Playtests");var beforeHashes=Hashes(saves);field.SetValue(null,Path.GetFullPath(Dir+"/full-campaign-isolated-saves"));string result="FAIL incomplete";
  try{
   g.playOpeningOnNewGame=true;g.NewSession();Check(g.State.coins==0&&g.State.glassFish==0&&g.State.fish==0&&g.State.shrimp==0&&!g.State.rod&&!g.State.backBasketUnlocked,"new game has no injected inventory, equipment or money");
   await Until(()=>g.State.openingCompleted&&!g.StoryPlaying,"natural opening",60);await Ready();if(!g.State.chapterOneAccepted){g.OpenShopInteraction();await Dialogue();}Check(g.State.chapterOneAccepted,"accepted actual opening commission");
   await Sail(5);await PlayRoute("first");await Catch(g.State.animals.First(a=>a.species==Species.Goldfish).id);
   Close();g.TogglePause();Check(!g.paused,"resumed ecology for actual second fish breeding");await Until(()=>g.State.animals.Any(a=>a.species==Species.Goldfish&&a.offspring>0),"natural breeding",60);g.TogglePause();await Arrive();await Catch(g.State.animals.First(a=>a.species==Species.Goldfish).id);Check(g.State.glassFish>=2,"two real commission fish obtained");
   await Return();await Shop();g.OpenShopInteraction();await Dialogue();await Until(()=>g.State.chapterOneMorningSeen&&!g.StoryPlaying,"walk home and natural morning",60);await Ready();Check(g.State.coins==200&&g.State.day==2,"commission pays 200 exactly once; morning reaches day two");
   result="PASS new game, push-only first island, two real fish, 200 coins, morning";Note(result);
  }catch(Exception ex){result="FAIL "+(ex.InnerException??ex).Message;Note(result+"\n"+ex);File.WriteAllText(Dir+"/full-campaign-failure.json",JsonUtility.ToJson(Call("CaptureSnapshot"),true));}
  finally{InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());g.Fishing.Cancel(false);g.Fishing.enabled=true;g.CancelPendingAction();g.Hud.ClosePanels();Call("RestoreSnapshot",original);undo.Clear();for(int i=oldUndo.Length-1;i>=0;i--)undo.Push(oldUndo[i]);foreach(var p in presentation)typeof(HabitatGame).GetField(p.Key,F).SetValue(g,p.Value);g.playOpeningOnNewGame=opening;g.paused=pause;g.tool=tool;typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,session);field.SetValue(null,oldFolder);var after=Hashes(saves);Check(beforeHashes.Count==after.Count&&beforeHashes.All(p=>after.TryGetValue(p.Key,out string v)&&v==p.Value),"all original player saves unchanged SHA256; original in-memory state restored");}
  return result;
 }
}



