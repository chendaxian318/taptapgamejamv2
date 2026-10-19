using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace HabitatIsland
{
    public partial class HabitatGame
    {
        public sealed class SaveSlotInfo
        {
            public int slot, day, actions;
            public bool exists, readable, legacy;
            public float elapsedSeconds;
            public string mapName, savedAt, error;
        }

        // Editor verification can use an isolated directory without touching player saves.
        internal static string SaveSlotDirectoryForTests;
        sealed class SaveInfoCache
        {
            public DateTime mainTime,backupTime;
            public long mainLength,backupLength;
            public SaveSlotInfo info;
        }
        readonly Dictionary<string,SaveInfoCache> saveInfoCache=new Dictionary<string,SaveInfoCache>();
        static string SaveSlotDirectory => string.IsNullOrEmpty(SaveSlotDirectoryForTests)
            ? Path.Combine(Application.persistentDataPath, "Playtests") : SaveSlotDirectoryForTests;
        static string SlotPath(int slot)
        {
            if (slot < 1 || slot > 3) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(SaveSlotDirectory, "slot-" + slot + ".json");
        }
        static string ExistingSlotPath(int slot)
        {
            string path = SlotPath(slot);
            if (slot == 1 && !SaveFileExists(path))
            {
                string legacy = LatestAvailablePath();
                bool hiddenLegacy=File.Exists(Path.Combine(SaveSlotDirectory,"slot-1.deleted"));
                if(hiddenLegacy && SaveFileExists(AutomaticSavePath))return AutomaticSavePath;
                if(!hiddenLegacy && SaveFileExists(legacy))return legacy;
            }
            return path;
        }
        public SaveSlotInfo GetSaveSlotInfo(int slot)
        {
            var info = new SaveSlotInfo { slot = slot };
            try
            {
                string path = ExistingSlotPath(slot);
                info.exists = File.Exists(path)||File.Exists(path+".bak");
                if (!info.exists) return info;
                var main=new FileInfo(path);var backup=new FileInfo(path+".bak");
                var mainTime=main.Exists?main.LastWriteTimeUtc:DateTime.MinValue;
                var backupTime=backup.Exists?backup.LastWriteTimeUtc:DateTime.MinValue;
                long mainLength=main.Exists?main.Length:0,backupLength=backup.Exists?backup.Length:0;
                if(saveInfoCache.TryGetValue(path,out var cached)&&cached.mainTime==mainTime&&cached.backupTime==backupTime
                    &&cached.mainLength==mainLength&&cached.backupLength==backupLength)return cached.info;
                info.legacy = Path.GetFileName(path) == "latest.json";
                var saved = ReadSaveWithBackup(path,out _);
                info.readable = true;
                info.day = Mathf.Max(1, saved.state.day); info.mapName = saved.state.levelName;
                info.actions = saved.actions;
                info.elapsedSeconds = HabitatPlayClock.Normalize(saved.state.playSeconds);
                info.savedAt = (main.Exists?main.LastWriteTime:backup.LastWriteTime).ToString("yyyy-MM-dd HH:mm");
                saveInfoCache[path]=new SaveInfoCache{mainTime=mainTime,backupTime=backupTime,mainLength=mainLength,backupLength=backupLength,info=info};
            }
            catch (Exception) { info.error = "文件损坏、无法访问或版本不兼容。"; }
            return info;
        }
        public bool SaveSession(int slot)
        {
            if (!SessionStarted || Busy || StoryPlaying || Fishing && Fishing.IsOpen)
            { State.message = "开始旅程并等当前动作结束后，才能保存。"; HabitatAudio.Play(HabitatAudioCue.Error); return false; }
            try
            {
                string path = SlotPath(slot); Directory.CreateDirectory(SaveSlotDirectory);
                WriteLatestSave(path, JsonUtility.ToJson(CaptureSnapshot(), true));
                LastExport = path; State.message = "已保存到槽位 " + slot + "。";
                HabitatAudio.Play(HabitatAudioCue.Confirm); return true;
            }
            catch (Exception) { State.message = "保存失败，请检查磁盘空间或目录权限后重试。"; HabitatAudio.Play(HabitatAudioCue.Error); return false; }
        }
        public bool LoadSession(int slot)
        {
            try
            {
                string path = ExistingSlotPath(slot);
                if (!File.Exists(path)&&!File.Exists(path+".bak")) { State.message = "这个槽位还没有存档。"; return false; }
                var saved = ReadSaveWithBackup(path,out bool recovered);
                // RestoreSnapshot validates first, then cancels the previous action/story and restores shared island data.
                RestoreSnapshot(saved); SessionStarted = true; HabitatAudio.ClearTransient();
                if(recovered)State.message="主存档不可用，已恢复上一份有效备份。原文件保留。";
                HabitatAudio.Play(HabitatAudioCue.Confirm); return true;
            }
            catch (Exception) { State.message = "读取失败：存档损坏、无法访问或版本不兼容，当前进度未改变。"; HabitatAudio.Play(HabitatAudioCue.Error); return false; }
        }
        public bool DeleteSession(int slot)
        {
            if (Busy || StoryPlaying) return false;
            try
            {
                string path = ExistingSlotPath(slot);
                if (!SaveFileExists(path)) { State.message = "这个槽位没有存档。"; return false; }
                File.Delete(path);
                if(File.Exists(path+".bak"))File.Delete(path+".bak");
                // A newer slot 1 can replace the legacy entry in the list, but deleting it must not delete the original legacy file too.
                // This marker only suppresses fallback; the old file remains recoverable outside the normal slot list.
                if(slot==1)File.WriteAllText(Path.Combine(SaveSlotDirectory,"slot-1.deleted"),"Legacy fallback hidden after explicit slot deletion.");
                State.message = "已删除槽位 " + slot + " 的存档；当前游戏未改变。";
                HabitatAudio.Play(HabitatAudioCue.Confirm); return true;
            }
            catch (Exception) { State.message = "暂时无法删除该存档，请稍后重试。"; HabitatAudio.Play(HabitatAudioCue.Error); return false; }
        }
    }
}
