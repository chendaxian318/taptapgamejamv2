using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HabitatIsland.Editor
{
    /// <summary>Play-mode controller checks using original-map routes and reversible save-file fixtures.</summary>
    public static class HabitatAuthoredControllerChecks
    {
        sealed class SearchNode { public HabitatState state; public List<int> route; }
        static readonly int[] DX={0,1,0,-1},DZ={1,0,-1,0};
        static int assertions;
        static StringBuilder report;
        static void Check(bool pass,string label)
        {
            assertions++;if(!pass)throw new InvalidOperationException("Authored controller check failed: "+label);
            report.AppendLine("PASS "+assertions+": "+label);
        }
        static Tile Q(HabitatState s,int x,int z){return HabitatRules.TileAt(s,x,z);}
        static int Distance(HabitatState s,int x,int z){return Math.Abs(s.playerX-x)+Math.Abs(s.playerZ-z);}
        static string ComparableState(HabitatState s)
        {
            // The controller intentionally updates the transient message after save/load/undo.
            var copy=HabitatRules.Clone(s);copy.message="";return JsonUtility.ToJson(copy);
        }
        static List<int> FindWalk(HabitatState s,Func<HabitatState,bool> destination)
        {
            var queue=new Queue<SearchNode>();var seen=new HashSet<int>();
            queue.Enqueue(new SearchNode{state=HabitatRules.Clone(s),route=new List<int>()});seen.Add(s.playerZ*s.width+s.playerX);
            while(queue.Count>0)
            {
                var node=queue.Dequeue();if(destination(node.state))return node.route;
                for(int d=0;d<4;d++)
                {
                    var next=HabitatRules.Clone(node.state);
                    if(!HabitatRules.MovePlayer(next,DX[d],DZ[d],true)||!seen.Add(next.playerZ*next.width+next.playerX))continue;
                    var route=new List<int>(node.route);route.Add(d);queue.Enqueue(new SearchNode{state=next,route=route});
                }
            }
            return null;
        }
        static void Move(HabitatGame game,int dx,int dz,bool climb,string label)
        {
            int x=game.State.playerX,z=game.State.playerZ,actions=game.ActionCount,undo=game.UndoCount;
            game.Move(dx,dz,climb);
            Check(game.State.playerX==x+dx&&game.State.playerZ==z+dz,label+" moves one real controller cell");
            Check(game.ActionCount==actions+1&&game.UndoCount==undo+1,label+" creates one action and one undo snapshot");
        }
        static void Walk(HabitatGame game,Func<HabitatState,bool> destination,string label)
        {
            var route=FindWalk(game.State,destination);Check(route!=null,label+" has a legal route");
            var trace=new StringBuilder("("+game.State.playerX+","+game.State.playerZ+")");
            foreach(int d in route)
            {
                Move(game,DX[d],DZ[d],true,label);
                trace.Append(" -> ("+game.State.playerX+","+game.State.playerZ+")");
            }
            Check(destination(game.State),label+" endpoint reached");report.AppendLine("ROUTE "+label+": "+trace);
        }
        static void Interact(HabitatGame game,int x,int z,string label)
        {
            int actions=game.ActionCount,undo=game.UndoCount;
            game.tool=0;game.Select(x,z);game.GetTarget(out int selectedX,out int selectedZ);
            Check(selectedX==x&&selectedZ==z,label+" selection targets source cell");
            game.ApplyTool();
            Check(game.ActionCount==actions+1&&game.UndoCount==undo+1,label+" commits through ApplyTool with one snapshot");
        }
        static void EnterFromMenu(HabitatGame game)
        {
            game.Hud.ClosePanels();game.LoadLevel(0);game.paused=false;game.Hud.ToggleSettings();
            Check(game.Hud.ModalOpen&&game.paused,"opening the real lab menu pauses the previous level");
            Button entry=game.Hud.GetComponentsInChildren<Button>(true).FirstOrDefault(button=>
            {
                TMP_Text text=button.GetComponentInChildren<TMP_Text>(true);
                return text!=null&&text.text=="荒岛关卡";
            });
            Check(entry!=null&&entry.gameObject.activeInHierarchy&&entry.interactable,"original map has an active chapter button");
            entry.onClick.Invoke();
            Check(game.State.levelIndex==HabitatFigJamLevel.Index&&game.State.authoredLevel&&game.State.width==18&&game.State.depth==19,"chapter button loads the assembled original map");
            Check(!game.Hud.ModalOpen&&game.paused&&game.UndoCount==0&&game.ActionCount==0,"chapter entry closes menu and starts paused with fresh history");
            Check(game.State.playerX==3&&game.State.playerZ==10&&game.State.coins==0&&game.State.cargo==0&&!game.State.rod,"controller uses the unchanged original spawn and resources");
        }
        static void RouteThroughController(HabitatGame game)
        {
            Walk(game,s=>s.playerX==6&&s.playerZ==14,"spawn to tide crate");
            var beforePush=HabitatRules.Clone(game.State);int actionsBeforePush=game.ActionCount,undoBeforePush=game.UndoCount;
            Move(game,1,0,false,"push original crate onto tide plate");
            Check(game.State.tideActive&&game.State.tideEverActivated&&game.State.waterLevel==1&&Q(game.State,8,14).coveredProp==PropKind.Tide,"real controller push records tide checkpoint");
            game.Undo();
            Check(ComparableState(game.State)==ComparableState(beforePush),"Undo restores complete pre-push state, crate position and checkpoint");
            Check(game.paused&&game.UndoCount==undoBeforePush&&!game.State.tideActive&&!game.State.tideEverActivated,"Undo pauses and removes exactly the tide snapshot");
            Check(game.ActionCount==actionsBeforePush+1,"ActionCount remains cumulative for successful input actions after Undo");
            Move(game,1,0,false,"repeat tide push after Undo");
            Check(game.State.tideEverActivated&&game.State.waterLevel==1,"repeated push re-establishes tide checkpoint");

            Interact(game,8,14,"lift pressure-plate crate");
            Check(game.State.carryingProp==PropKind.Crate&&!game.State.tideActive&&game.State.waterLevel==0&&game.State.tideEverActivated,"lifting crate restores low tide while preserving observed checkpoint");
            Interact(game,7,15,"place crate on adjacent highland");
            Check(game.State.carryingProp==PropKind.None&&Q(game.State,7,15).prop==PropKind.Crate,"carried crate placed without adding inventory");
            Walk(game,s=>Distance(s,12,14)==1,"restored low-water path to rod chest");
            Interact(game,12,14,"open original rod chest");
            Check(game.State.rod&&game.State.rodFromChest&&game.State.openedChests==1&&game.State.coins==0,"chest unlocks rod and counters without invented coins");

            Walk(game,s=>Distance(s,9,14)==1,"return to northern floating block");
            Interact(game,9,14,"pick up original floating block");
            Check(game.State.carryingProp==PropKind.Float,"actual controller carries the original float");
            Walk(game,s=>s.playerX==10&&s.playerZ==9,"carry original float to southern bank");
            Interact(game,10,8,"bridge original one-cell water gap");
            Check(Q(game.State,10,8).prop==PropKind.Float&&game.State.carryingProp==PropKind.None,"bridge is placed through the normal interaction tool");
            Walk(game,s=>Distance(s,5,5)<=1,"cross bridge to original crab island");
            Check(game.State.coins==0&&game.State.cargo==0&&game.State.traps==0&&game.State.elapsed==0,"route uses no resource grants or unpaused simulation");
            Check(!game.State.won&&game.State.goals.Length==0,"mechanism checkpoints do not fabricate a final quest ending");
            game.View.Render(game.State,game.tuning,game.overlays);game.Hud.Refresh();
            Check(game.View.GameCamera!=null&&game.View.GameCamera.enabled,"real view and HUD accept the resulting authored state");
        }
        static bool IsOwnSavePath(string path,string folder)
        {
            if(string.IsNullOrEmpty(path))return false;
            string resolved=Path.GetFullPath(path),expectedFolder=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            return string.Equals(Path.GetDirectoryName(resolved),expectedFolder,StringComparison.OrdinalIgnoreCase)&&
                Path.GetFileName(resolved).StartsWith("playtest-",StringComparison.Ordinal)&&resolved.EndsWith(".json",StringComparison.OrdinalIgnoreCase);
        }
        public static string Run()
        {
            if(!Application.isPlaying||HabitatGame.Instance==null)throw new InvalidOperationException("Run authored controller checks inside Unity Play mode.");
            HabitatGame game=HabitatGame.Instance;
            string folder=Path.GetFullPath(Path.Combine(Application.persistentDataPath,"Playtests"));
            string latest=Path.Combine(folder,"latest.json");bool hadLatest=File.Exists(latest);
            byte[] originalLatest=hadLatest?File.ReadAllBytes(latest):null;
            string originalExport=game.LastExport;
            var originalTimestamped=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
            if(Directory.Exists(folder))foreach(string path in Directory.GetFiles(folder,"playtest-*.json"))originalTimestamped[Path.GetFullPath(path)]=File.ReadAllBytes(path);
            var writtenTimestamped=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool saveAttempted=false;Exception failure=null;
            assertions=0;report=new StringBuilder("Authored controller integration checks\nUTC: "+DateTimeOffset.UtcNow.ToString("O")+"\n");
            string reportPath=Path.GetFullPath(Path.Combine(Application.dataPath,"../Verification/authored-controller-checks.txt"));
            try
            {
                Check(game.Hud!=null&&game.View!=null&&game.Fishing!=null,"controller references are initialized");
                game.Fishing.Cancel();game.tuning=new Tuning();game.speed=1;
                EnterFromMenu(game);RouteThroughController(game);
                var savedState=HabitatRules.Clone(game.State);int savedActions=game.ActionCount;
                string savedTuning=JsonUtility.ToJson(game.tuning);
                saveAttempted=true;game.SaveSession();
                if(IsOwnSavePath(game.LastExport,folder))writtenTimestamped.Add(Path.GetFullPath(game.LastExport));
                if(Directory.Exists(folder))foreach(string path in Directory.GetFiles(folder,"playtest-*.json"))
                    if(!originalTimestamped.ContainsKey(Path.GetFullPath(path)))writtenTimestamped.Add(Path.GetFullPath(path));
                Check(File.Exists(latest)&&IsOwnSavePath(game.LastExport,folder)&&File.Exists(game.LastExport)&&game.State.message.StartsWith("已保存"),"SaveSession writes actual latest and timestamped snapshots");
                game.LoadLevel(0);game.paused=false;game.Hud.ToggleSettings();
                Check(!game.State.authoredLevel&&game.Hud.ModalOpen,"load fixture changed level through the controller without teleporting or editing resources");
                game.LoadSession();
                Check(ComparableState(game.State)==ComparableState(savedState),"LoadSession restores full original-map state including bridge, chest, metadata and checkpoints");
                Check(game.ActionCount==savedActions&&game.UndoCount==0&&game.paused&&!game.Hud.ModalOpen,"LoadSession restores action count, clears history, closes menu and remains paused");
                Check(JsonUtility.ToJson(game.tuning)==savedTuning&&game.State.dockRotation==3&&game.State.rodFromChest&&game.State.tideEverActivated,"roundtrip preserves tuning, dock orientation and new persistent flags");
                game.View.Render(game.State,game.tuning,game.overlays);game.Hud.Refresh();
            }
            catch(Exception e){failure=e;report.AppendLine("FAIL: "+e);}
            finally
            {
                try
                {
                    if(saveAttempted)
                    {
                        if(hadLatest)File.WriteAllBytes(latest,originalLatest);else if(File.Exists(latest))File.Delete(latest);
                        foreach(string path in writtenTimestamped)
                        {
                            if(!IsOwnSavePath(path,folder))throw new InvalidOperationException("Refusing unexpected save cleanup path: "+path);
                            if(originalTimestamped.TryGetValue(path,out byte[] priorBytes))File.WriteAllBytes(path,priorBytes);
                            else if(File.Exists(path))File.Delete(path);
                        }
                        // SaveSession names use one-second precision; preserve a same-name user save even if writing failed midway.
                        foreach(var pair in originalTimestamped)
                            if(!File.Exists(pair.Key)||!File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value))File.WriteAllBytes(pair.Key,pair.Value);
                    }
                    Check(hadLatest?File.Exists(latest)&&File.ReadAllBytes(latest).SequenceEqual(originalLatest):!File.Exists(latest),"cleanup restores the user's latest save exactly");
                    foreach(string path in writtenTimestamped)
                        Check(originalTimestamped.ContainsKey(path)?File.ReadAllBytes(path).SequenceEqual(originalTimestamped[path]):!File.Exists(path),"cleanup removes its own timestamped save or restores a collided original");
                }
                catch(Exception e){report.AppendLine("SAVE CLEANUP FAIL: "+e);if(failure==null)failure=e;}
                try
                {
                    game.Hud.ClosePanels();game.Fishing.Cancel();game.tuning=new Tuning();game.speed=1;game.tool=0;
                    game.LoadLevel(HabitatFigJamLevel.Index);game.paused=true;game.LastExport=originalExport;
                    game.View.Render(game.State,game.tuning,game.overlays);game.Hud.Refresh();
                    Check(game.State.authoredLevel&&game.State.levelIndex==4&&game.paused&&game.ActionCount==0&&game.UndoCount==0&&!game.Hud.ModalOpen,"cleanup leaves fresh original map paused for planner testing");
                }
                catch(Exception e){report.AppendLine("SCENE CLEANUP FAIL: "+e);if(failure==null)failure=e;}
                report.AppendLine((failure==null?"PASS: ":"FAILED after ")+assertions+" authored controller assertions.");
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));File.WriteAllText(reportPath,report.ToString());
            }
            if(failure!=null)throw new InvalidOperationException("Authored controller checks failed; see "+reportPath,failure);
            return "PASS: "+assertions+" authored controller assertions. Report: "+reportPath;
        }
    }
}
