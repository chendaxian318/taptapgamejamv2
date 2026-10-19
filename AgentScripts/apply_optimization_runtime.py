from pathlib import Path
p=Path(__file__).resolve().parents[1]/'Assets/HabitatIsland/Scripts'
def edit(name, old, new):
    f=p/name;s=f.read_text(encoding='utf-8-sig')
    if s.count(old)!=1:raise RuntimeError((name,old[:100],s.count(old)))
    f.write_text(s.replace(old,new),encoding='utf-8')

# Bound undo memory without changing its external stack contract.
for f in p.glob('HabitatGame*.cs'):
    if f.name=='HabitatGame.Safety.cs':continue
    s=f.read_text(encoding='utf-8-sig').replace('history.Push(', 'PushHistory(')
    f.write_text(s,encoding='utf-8')

edit('HabitatGame.cs','Debug.Log("[Habitat] Game Awake complete");','CaptureRetryPoint();\n            Debug.Log("[Habitat] Game Awake complete");')
edit('HabitatGame.cs','if (State == null) return;\n            UpdatePlayTime();','if (State == null || backgroundSuspended) return;\n            UpdatePlayTime();')
edit('HabitatGame.cs','&& View.GridStepComplete && utilitySeconds<=0)', '&& View.GridStepComplete && utilitySeconds<=0 && (!pendingLure || View.ResidentsArrived(pendingState)))')
edit('HabitatGame.cs','            Hud.Refresh();\n        }','            Hud.Refresh();\n            TickSafety();\n        }')
edit('HabitatGame.cs','            if (Busy) return;\n            if (Fishing != null && Fishing.IsOpen) return;\n            var k = Keyboard.current;', '            var k = Keyboard.current;\n            if (Busy) { CaptureBufferedInput(k); HandleCameraInput(k); return; }\n            if (Fishing != null && Fishing.IsOpen) return;\n            if (ConsumeBufferedInput()) return;\n            HandleCameraInput(k);')
edit('HabitatGame.cs','if (k.backspaceKey.wasPressedThisFrame && !Hud.ModalOpen) RestartCurrentLevel();','UpdateRestartHold(k);')
edit('HabitatGame.cs','                float cameraTurn = (k.qKey.wasPressedThisFrame ? -90f : 0f) + (k.cKey.wasPressedThisFrame ? 90f : 0f);\n                if (cameraTurn != 0f && !Hud.ModalOpen) View.RotateCamera(cameraTurn);','')
edit('HabitatGame.cs','                if (k.digit5Key.wasPressedThisFrame) tool = 4;','                if (k.digit5Key.wasPressedThisFrame) tool = 4;\n                if (k.digit6Key.wasPressedThisFrame) tool = 5;\n                if (k.digit7Key.wasPressedThisFrame) tool = 6;\n                if (k.digit8Key.wasPressedThisFrame) tool = 7;')
f=p/'HabitatGame.cs';s=f.read_text(encoding='utf-8');start=s.index('                    if (k.mKey.wasPressedThisFrame)');end=s.index('                    if (State.onHarbor)',start)
s=s[:start]+'                    Vector2Int input = ReadDirection(k,out bool first);\n                    int dx=input.x,dz=input.y;\n'+s[end:];f.write_text(s,encoding='utf-8')
edit('HabitatGame.cs','Quaternion rotation = Quaternion.AngleAxis(-cameraTurn, Vector3.up) * View.GameCamera.transform.rotation;','Quaternion rotation = View.InputCameraRotation;')
edit('HabitatGame.cs','View.GameCamera.transform.rotation : Quaternion.Euler(HabitatView.DefaultCameraPitch, -HabitatView.DefaultCameraYaw, 0f);','View.InputCameraRotation : Quaternion.Euler(HabitatView.DefaultCameraPitch, -HabitatView.DefaultCameraYaw, 0f);')
edit('HabitatGame.cs','ScreenDirectionToGrid(dx, dz, cameraRotation, cameraTurn);','ScreenDirectionToGrid(dx, dz, cameraRotation);')
edit('HabitatGame.cs','            if (m.middleButton.isPressed) View.Pan(m.delta.ReadValue());\n','')
edit('HabitatGame.cs','                if (View.Pick(m.position.ReadValue(), out int tx, out int tz)) Select(tx, tz);\n                ApplyTool();','                if (View.Pick(m.position.ReadValue(), out int tx, out int tz)) { Select(tx, tz); ApplyTool(); }')
edit('HabitatGame.cs','            float scroll = m.scroll.ReadValue().y;\n            if (Mathf.Abs(scroll) > .01f) View.Zoom(scroll > 0 ? -1 : 1);','')
edit('HabitatGame.cs','            State=pendingState;','            State=pendingState;\n            if(pendingLure){HabitatAudio.PlayAction(HabitatActionCue.Lure);pendingLure=false;}')
edit('HabitatGame.cs','        public void CancelPendingAction()\n        {','        public void CancelPendingAction()\n        {\n            ClearBufferedInput(); pendingLure=false;')
start=(p/'HabitatGame.cs').read_text(encoding='utf-8').index('                case 2:');s=(p/'HabitatGame.cs').read_text(encoding='utf-8');end=s.index('                case 3:',start);s=s[:start]+'                case 2: BeginLureAction(x,z); break;\n'+s[end:];(p/'HabitatGame.cs').write_text(s,encoding='utf-8')
edit('HabitatGame.cs','            State = history.Pop(); paused = true;','            State = history.Pop(); paused = true;\n            ClearBufferedInput(); CaptureRetryPoint();')
edit('HabitatGame.cs','            if (View != null) View.Initialize(State);\n        }','            if (View != null) View.Initialize(State);\n            CaptureRetryPoint();\n        }')
edit('HabitatGame.cs','                if(State.waterLevel!=before.waterLevel)HabitatAudio.PlayAction(HabitatActionCue.Tide);','                if(State.waterLevel!=before.waterLevel)HabitatAudio.PlayAction(HabitatActionCue.Tide);\n                if(actionCue==HabitatActionCue.Trade)CaptureRetryPoint();')
edit('HabitatGame.cs','            public List<VisitedLevel> visitedLevels;','            public List<VisitedLevel> visitedLevels;\n            public RetryPoint retryPoint;')
edit('HabitatGame.cs','string folder = Path.Combine(Application.persistentDataPath, "Playtests");','string folder = SaveSlotDirectory;')
edit('HabitatGame.cs','string path = Path.Combine(Application.persistentDataPath, "Playtests", "latest.json");','string path = LatestAvailablePath();')
edit('HabitatGame.cs','var saved = JsonUtility.FromJson<Snapshot>(File.ReadAllText(path));\n                RestoreSnapshot(saved);','var saved = ReadSaveWithBackup(path,out bool recovered);\n                RestoreSnapshot(saved);\n                if(recovered)State.message="主存档不可用，已恢复上一份有效备份。原文件保留。";')

f=p/'HabitatGame.Tutorial.cs';s=f.read_text(encoding='utf-8');start=s.index('            if(!HabitatTutorialRules.IsTutorial(State)&&!HabitatLevels.IsFormal(State.levelIndex))');end=s.index('\n        }',start)
s=s[:start]+'''            if(retryPoint==null||retryPoint.state.levelIndex!=State.levelIndex)CaptureRetryPoint();
            float played=State.playSeconds;
            var saved=new Snapshot { state=retryPoint.state,tuning=retryPoint.tuning,actions=retryPoint.actions,visitedLevels=retryPoint.visitedLevels,retryPoint=retryPoint };
            // A full detached transaction prevents item duplication and currency/refund mismatches.
            saved=JsonUtility.FromJson<Snapshot>(JsonUtility.ToJson(saved));
            RestoreSnapshot(saved);
            State.playSeconds=Mathf.Max(played,State.playSeconds);ResetPlayTime();
            tool=0;View.FocusPlayer();autoSaveRequested=true;
            State.message="已恢复最近的登岛或交易检查点，物资与探索进度一起恢复。";'''+s[end:];f.write_text(s,encoding='utf-8')

edit('HabitatGame.Journey.cs','            nextMove = Time.unscaledTime + .15f;','            nextMove = Time.unscaledTime + .15f;\n            CaptureRetryPoint();')
edit('HabitatGame.Journey.cs','            View.Render(State, tuning, overlays); View.FocusPlayer();\n        }\n\n        Snapshot CaptureSnapshot()', '            View.Render(State, tuning, overlays); View.FocusPlayer();\n            CaptureRetryPoint();\n        }\n\n        Snapshot CaptureSnapshot()')
edit('HabitatGame.Journey.cs','actions = ActionCount, visitedLevels = levels };','actions = ActionCount, visitedLevels = levels, retryPoint=retryPoint };')
f=p/'HabitatGame.Journey.cs';s=f.read_text(encoding='utf-8');start=s.index('            if (saved == null || saved.version != "HabitatIsland/1"',s.index('void RestoreSnapshot'));end=s.index('\n        }',start)
s=s[:start]+'''            ValidateSnapshot(saved);
            var previous=CaptureSnapshot();var oldHistory=history.ToArray();bool oldPause=paused;
            try
            {
                CancelPendingAction();
                if (Fishing != null) Fishing.Cancel(false);
                if (Hud != null) Hud.ClosePanels();
                State = saved.state; ResetPlayTime();
                RestoreVisitedLevels(saved.visitedLevels);
                tuning = saved.tuning; ActionCount = saved.actions;
                retryPoint=saved.retryPoint;
                history.Clear(); paused = true; View.Initialize(State);
                if(retryPoint==null)CaptureRetryPoint();
                State.message = "已读取进度：第 " + State.day + " 天，渔获和各岛屿进度已恢复。";
            }
            catch
            {
                State=previous.state;tuning=previous.tuning;ActionCount=previous.actions;retryPoint=previous.retryPoint;
                RestoreVisitedLevels(previous.visitedLevels);ResetPlayTime();paused=oldPause;
                history.Clear();for(int i=oldHistory.Length-1;i>=0;i--)history.Push(oldHistory[i]);
                View.Initialize(State);throw;
            }'''+s[end:];f.write_text(s,encoding='utf-8')

edit('HabitatGame.Workflow.cs','            if(playOpeningOnNewGame)BeginOpening();','            CaptureRetryPoint();\n            if(playOpeningOnNewGame)BeginOpening();')
edit('HabitatGame.Workflow.cs','public bool HasSavedSession=>File.Exists(ExistingSlotPath(1))||File.Exists(ExistingSlotPath(2))||File.Exists(ExistingSlotPath(3));','public bool HasSavedSession=>File.Exists(ExistingSlotPath(1))||File.Exists(ExistingSlotPath(2))||File.Exists(ExistingSlotPath(3));')
edit('HabitatGame.SaveSlots.cs','string legacy = Path.Combine(SaveSlotDirectory, "latest.json");','string legacy = LatestAvailablePath();')
f=p/'HabitatGame.SaveSlots.cs';s=f.read_text(encoding='utf-8');a=s.index('                var saved = JsonUtility.FromJson<Snapshot>');b=s.index('                info.readable = true;',a);s=s[:a]+'                var saved = ReadSaveWithBackup(path,out _);\n'+s[b:];f.write_text(s,encoding='utf-8')
edit('HabitatGame.SaveSlots.cs','var saved = JsonUtility.FromJson<Snapshot>(File.ReadAllText(path));','var saved = ReadSaveWithBackup(path,out bool recovered);')
edit('HabitatGame.SaveSlots.cs','RestoreSnapshot(saved); SessionStarted = true; HabitatAudio.ClearTransient();','RestoreSnapshot(saved); SessionStarted = true; HabitatAudio.ClearTransient();\n                if(recovered)State.message="主存档不可用，已恢复上一份有效备份。原文件保留。";')
edit('HabitatFishing.cs','if (!IsOpen) return;\n            Keyboard keyboard', 'if (!IsOpen || game && game.BackgroundSuspended) return;\n            Keyboard keyboard')
edit('HabitatFishing.cs','if (!IsOpen || dt <= 0f || float.IsNaN(dt)', 'if (!IsOpen || game && game.BackgroundSuspended || dt <= 0f || float.IsNaN(dt)')
print('Runtime safety/input wiring applied')

