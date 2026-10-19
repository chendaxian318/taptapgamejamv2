using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HabitatIsland
{
    public partial class HabitatGame
    {
        [Serializable] sealed class RetryPoint
        {
            public HabitatState state;
            public Tuning tuning;
            public int actions;
            public List<VisitedLevel> visitedLevels;
        }
        RetryPoint retryPoint;
        int retryHistoryDepth;
        bool autoSaveRequested, backgroundSuspended, resumeStoryOnFocus;
        int foregroundFrameRate = 60;
        float restartHeldSince = -1, nextAutoSaveAttempt;
        bool restartFired, observedWin;
        int observedLevel = -1;
        public bool BackgroundSuspended => backgroundSuspended;

        void CaptureRetryPoint()
        {
            if (State == null) return;
            var frame = CaptureSnapshot();
            retryPoint = new RetryPoint { state=frame.state, tuning=frame.tuning, actions=frame.actions, visitedLevels=frame.visitedLevels };
            retryHistoryDepth=history.Count;
            autoSaveRequested = true;
        }

        static void ValidateLevels(List<VisitedLevel> levels)
        {
            if (levels == null) return;
            if (levels.Count > HabitatLevels.Names.Length) throw new InvalidDataException("岛屿缓存过多。");
            var seen = new HashSet<int>();
            foreach (var item in levels)
            {
                if (item == null || item.state == null || item.index != item.state.levelIndex || !seen.Add(item.index) || item.actions < 0)
                    throw new InvalidDataException("岛屿缓存无效。");
                HabitatSaveValidation.Validate(item.state);
            }
        }

        static void ValidateSnapshot(Snapshot candidate)
        {
            if (candidate == null || candidate.version != "HabitatIsland/1" || candidate.actions < 0)
                throw new InvalidDataException("存档版本或记录无效。");
            HabitatSaveValidation.Validate(candidate.state);
            if (candidate.tuning == null) candidate.tuning = new Tuning();
            HabitatSaveValidation.Validate(candidate.tuning);
            ValidateLevels(candidate.visitedLevels);
            if (candidate.retryPoint != null)
            {
                var r = candidate.retryPoint;
                HabitatSaveValidation.Validate(r.state);
                if (r.tuning == null) r.tuning = new Tuning();
                HabitatSaveValidation.Validate(r.tuning); ValidateLevels(r.visitedLevels);
                if (r.actions < 0 || r.state.levelIndex != candidate.state.levelIndex) throw new InvalidDataException("重试检查点与岛屿不匹配。");
            }
        }

        static Snapshot ReadValidatedSnapshot(string path)
        {
            if (new FileInfo(path).Length > 32L*1024*1024) throw new InvalidDataException("存档超出大小限制。");
            // JsonUtility silently turns null inline objects into default instances.
            // Preserve explicit nulls so malformed tiles cannot become valid empty terrain.
            var saved = Newtonsoft.Json.JsonConvert.DeserializeObject<Snapshot>(File.ReadAllText(path),
                new Newtonsoft.Json.JsonSerializerSettings { TypeNameHandling=Newtonsoft.Json.TypeNameHandling.None, MaxDepth=32 });
            ValidateSnapshot(saved); return saved;
        }

        static Snapshot ReadSaveWithBackup(string path, out bool recovered)
        {
            recovered = false;
            try { return ReadValidatedSnapshot(path); }
            catch (Exception) when (File.Exists(path + ".bak"))
            {
                var backup = ReadValidatedSnapshot(path + ".bak");
                recovered = true; return backup;
            }
        }

        static string AutomaticSavePath => Path.Combine(SaveSlotDirectory,"autosave.json");
        static bool SaveFileExists(string path)=>File.Exists(path)||File.Exists(path+".bak");
        static DateTime SaveFileTime(string path)=>File.GetLastWriteTimeUtc(File.Exists(path)?path:path+".bak");
        static string LatestAvailablePath()
        {
            string legacy = Path.Combine(SaveSlotDirectory,"latest.json"), automatic = AutomaticSavePath;
            if (SaveFileExists(automatic) && (!SaveFileExists(legacy) || SaveFileTime(automatic) > SaveFileTime(legacy))) return automatic;
            return legacy;
        }

        void TickSafety()
        {
            if (State == null) return;
            if (observedLevel != State.levelIndex) { observedLevel=State.levelIndex; observedWin=State.won; }
            else if (State.won != observedWin) { observedWin=State.won; if(State.won)autoSaveRequested=true; }
            if (!autoSaveRequested || !SessionStarted || Busy || backgroundSuspended || Fishing && Fishing.IsOpen || Time.unscaledTime < nextAutoSaveAttempt) return;
            // Independent from the three manual slots; .bak preserves the previous checkpoint.
            try
            {
                Directory.CreateDirectory(SaveSlotDirectory);
                WriteLatestSave(AutomaticSavePath,JsonUtility.ToJson(CaptureSnapshot(),true));
                autoSaveRequested=false;
            }
            catch (Exception e) { nextAutoSaveAttempt=Time.unscaledTime+30; Debug.LogWarning("[Habitat] Automatic save deferred: "+e.GetType().Name); }
        }

        void UpdateRestartHold(Keyboard keyboard)
        {
            if (keyboard == null || !keyboard.backspaceKey.isPressed || Hud.ModalOpen)
            { restartHeldSince=-1; restartFired=false; return; }
            if (restartHeldSince < 0) { restartHeldSince=Time.unscaledTime; State.message="按住退格键 0.8 秒可恢复最近一次登岛或交易检查点。"; }
            if (!restartFired && Time.unscaledTime-restartHeldSince >= .8f)
            { restartFired=true; RestartCurrentLevel(); }
        }

        void OnApplicationFocus(bool focused)
        {
            // Editor inspection/automation should keep running when a panel receives focus.
            // The explicit SetBackgroundSuspended entry is also exercised by runtime tests.
            if (!Application.isEditor) SetBackgroundSuspended(!focused);
        }
        internal void SetBackgroundSuspended(bool suspend)
        {
            if (backgroundSuspended == suspend) return;
            backgroundSuspended=suspend; ClearBufferedInput(); restartHeldSince=-1; restartFired=false;
            if (suspend)
            {
                foregroundFrameRate=Application.targetFrameRate; Application.targetFrameRate=15;
                resumeStoryOnFocus=StoryPlaying && !Story.IsPaused;
                if(resumeStoryOnFocus)Story.TogglePause();
            }
            else
            {
                Application.targetFrameRate=foregroundFrameRate;
                if(resumeStoryOnFocus && StoryPlaying && Story.IsPaused)Story.TogglePause();
                resumeStoryOnFocus=false; nextMove=Time.unscaledTime+.12f;
            }
        }

        void PushHistory(HabitatState before)
        {
            // Keep existing Stack semantics used by undo and verification tools.
            if (history.Count >= 200)
            {
                var states=history.ToArray(); history.Clear();
                for(int i=198;i>=0;i--)history.Push(states[i]);
                retryHistoryDepth=Math.Max(0,retryHistoryDepth-1);
            }
            history.Push(before);
        }
    }
}
