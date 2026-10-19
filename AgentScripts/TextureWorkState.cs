using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HabitatIsland;
using UnityEngine;

// Preserve the user's current run across the editor-only importer change.
public static class TextureWorkState
{
    const string FilePath = "Logs/texture-upscale-20261015/user-session.json";
    static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    [Serializable] public class Saved
    {
        public HabitatState state;
        public Tuning tuning;
        public HabitatState[] history;
        public bool paused, overlays, follow, overview;
        public float speed, yaw, size;
        public int actions, tool;
        public Vector3 target, targetSmooth;
    }
    static T Get<T>(HabitatView view, string name) => (T)typeof(HabitatView).GetField(name, Fields).GetValue(view);
    static void Set(HabitatView view, string name, object value) => typeof(HabitatView).GetField(name, Fields).SetValue(view, value);
    static Stack<HabitatState> History(HabitatGame game) => (Stack<HabitatState>)typeof(HabitatGame).GetField("history", Fields).GetValue(game);
    public static string SaveAndStop()
    {
        string result = Save();
        UnityEditor.EditorApplication.isPlaying = false;
        return result;
    }
    public static string Save()
    {
        var game = HabitatGame.Instance;
        if (!Application.isPlaying || !game) throw new InvalidOperationException("Run must be active before saving.");
        var view = game.View;
        var saved = new Saved { state = game.State, tuning = game.tuning, history = History(game).ToArray(),
            paused = game.paused, overlays = game.overlays, speed = game.speed, actions = game.ActionCount, tool = game.tool,
            yaw = Get<float>(view,"cameraYaw"), size = Get<float>(view,"cameraSize"),
            follow = Get<bool>(view,"followPlayer"), overview = Get<bool>(view,"overviewCamera"),
            target = Get<Vector3>(view,"cameraTarget"), targetSmooth = Get<Vector3>(view,"cameraTargetSmooth") };
        if (File.Exists(FilePath)) throw new IOException("A session backup already exists; do not overwrite it blindly.");
        File.WriteAllText(FilePath, JsonUtility.ToJson(saved));
        return "Saved user run: actions="+saved.actions+", undo="+saved.history.Length+", onHarbor="+saved.state.onHarbor;
    }
    public static string Restore()
    {
        var game = HabitatGame.Instance;
        if (!Application.isPlaying || !game) throw new InvalidOperationException("Enter Play before restoring.");
        var saved = JsonUtility.FromJson<Saved>(File.ReadAllText(FilePath));
        game.State = saved.state; game.tuning = saved.tuning;
        game.paused = saved.paused; game.overlays = saved.overlays; game.speed = saved.speed;
        game.ActionCount = saved.actions; game.tool = saved.tool;
        var history = History(game); history.Clear();
        for (int i=saved.history.Length-1;i>=0;i--) history.Push(saved.history[i]);
        game.View.Initialize(game.State);
        Set(game.View,"cameraYaw",saved.yaw); Set(game.View,"cameraSize",saved.size);
        Set(game.View,"followPlayer",saved.follow); Set(game.View,"overviewCamera",saved.overview);
        Set(game.View,"cameraTarget",saved.target); Set(game.View,"cameraTargetSmooth",saved.targetSmooth);
        typeof(HabitatView).GetMethod("UpdateCamera",Fields).Invoke(game.View,new object[]{true});
        if (JsonUtility.ToJson(game.State)!=JsonUtility.ToJson(saved.state)||game.UndoCount!=saved.history.Length)
            throw new Exception("Session restoration mismatch");
        return "Restored user run: actions="+game.ActionCount+", undo="+game.UndoCount+", onHarbor="+game.State.onHarbor;
    }
}
