from pathlib import Path
root=Path(__file__).resolve().parents[1]
s=(root/'AgentScripts/RaftFirstCampaign.cs').read_text(encoding='utf-8')
s=s.replace('RaftFirstCampaignRuntime','RaftTransportRuntime').replace('full-campaign-runtime.txt','transport-runtime.txt')
start=s.index('  try{',s.index(' public static async Task<string> Run'))
end=s.index('  }catch(Exception ex)',start)
s=s[:start]+'''  try{
   typeof(HabitatGame).GetProperty("SessionStarted").SetValue(g,true);
   Check(g.LoadSession(2),"loaded earned second-island checkpoint from this run's new-game test");await Ready();g.paused=true;g.tuning.animalMovement=false;
   Check(g.State.levelIndex==6&&g.State.coins==0&&g.State.bagProps.Contains(PropKind.Fan),"second transport uses actually purchased bellows, no injected inventory");
   foreach(var a in Route("second-transport"))await Act(a);
   Check(g.State.backBasketUnlocked&&g.State.carryingProp==PropKind.Fan&&g.State.playerX==3&&g.State.playerZ==8,"second island: deploy wind, cross bamboo, open basket chest, return and retrieve bellows");
   File.WriteAllText(Dir+"/second-transport-result.json",JsonUtility.ToJson(Call("CaptureSnapshot"),true));
   g.View.FocusOverview();Close();for(int i=0;i<15;i++)await Frame();ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/second-transport.png"));await Frame();
   g.LoadLevel(8);g.paused=true;g.tuning.animalMovement=false;await Ready();
   Check(g.State.coins==0&&g.State.magnets==0&&g.State.launchers==0,"fourth transport fixture uses original map items only");
   bool capturedHigh=false;foreach(var a in Route("fourth-transport")){await Act(a);if(g.State.tideActive&&!capturedHigh){capturedHigh=true;g.View.FocusOverview();Close();for(int i=0;i<15;i++)await Frame();ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/fourth-transport-high.png"));await Frame();}}
   Check(g.State.tideEverActivated&&!g.State.tideActive&&g.State.waterLevel==0,"fourth actual pressure plate completes rising and falling tide");
   Check(g.State.playerX==4&&g.State.playerZ==5&&HabitatRules.TileAt(g.State,5,13).prop==PropKind.ClimbBlock,"fourth: fixed stair, east shore round trip, southern raft landing, stair unmoved");
   Check(g.State.tiles.Count(t=>t.prop==PropKind.Raft)==2,"both fourth-map bamboo transports conserved");
   g.View.FocusOverview();Close();for(int i=0;i<15;i++)await Frame();ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/fourth-transport-ebb.png"));await Frame();
   File.WriteAllText(Dir+"/fourth-transport-result.json",JsonUtility.ToJson(Call("CaptureSnapshot"),true));
   result="PASS second 26-action wind/basket roundtrip using earned bellows; fourth 48-action fixed-stair/tide/two-raft transport branch (not full habitat victory)";Note(result);
'''+s[end:]
(root/'AgentScripts/RaftTransportRuntime.cs').write_text(s,encoding='utf-8')
# Avoid obscuring map captures with automatic tutorial dialogs, without changing player saves.
p=root/'AgentScripts/RaftFeedbackRuntimeChecks.cs';s=p.read_text(encoding='utf-8')
s=s.replace('for(int i=0;i<12;i++)await Frame();ScreenCapture', 'g.Hud.ClosePanelsFromInput();g.View.FocusOverview();for(int i=0;i<12;i++){await Frame();if(g.Hud.ModalOpen)g.Hud.ClosePanelsFromInput();}ScreenCapture')
p.write_text(s,encoding='utf-8')
