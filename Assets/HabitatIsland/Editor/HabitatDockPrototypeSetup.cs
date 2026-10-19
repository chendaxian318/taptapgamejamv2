using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;

namespace HabitatIsland.Editor
{
    /// <summary>Creates only the independent harbor scene/assets. Does not rebuild the original island scene.</summary>
    public static class HabitatDockPrototypeSetup
    {
        public const string ScenePath = "Assets/HabitatIsland/Scenes/HarborPrototype.unity";
        public const string OpeningPath = "Assets/HabitatIsland/Timelines/HarborPrototype/DayOneOpening.playable";
        public const string MorningPath = "Assets/HabitatIsland/Timelines/HarborPrototype/MorningWindow.playable";
        const string MaterialDirectory = "Assets/HabitatIsland/Art/HarborPrototype";
        const string IslandScenePath = "Assets/HabitatIsland/Scenes/HabitatIsland.unity";

        // Build means author the prototype scene/assets; this never builds a player executable.
        public static void Build() => CreateScene();

        [MenuItem("Tools/Habitat Island/Harbor/Create Prototype Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before authoring the harbor scene.");
            var current = EditorSceneManager.GetActiveScene();
            if (current.IsValid() && current.isDirty)
                throw new InvalidOperationException("Save the current scene before creating HarborPrototype.");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/HabitatIsland/Resources/Fonts/HabitatChinese.asset");
            GameObject traveler = Resources.Load<GameObject>("Models/MeshyTraveler/Player");
            if (!font || !traveler) throw new InvalidOperationException("Existing Chinese font and MeshyTraveler prefab are required.");
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Directory.CreateDirectory(Path.GetDirectoryName(OpeningPath));
            Directory.CreateDirectory(MaterialDirectory);
            AssetDatabase.Refresh();
            TimelineAsset opening = LoadOrCreateOpening();
            TimelineAsset morning = LoadOrCreateMorning();
            Material[] palette = LoadOrCreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.72f, .81f, .82f);
            RenderSettings.ambientIntensity = .85f;
            RenderSettings.fog = false;
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(UniversalAdditionalCameraData));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.57f, .79f, .92f);
            camera.orthographic = true; camera.orthographicSize = 7.5f;
            camera.nearClipPlane = .1f; camera.farClipPlane = 300;
            camera.allowMSAA = true;
            var cameraData = cameraObject.GetComponent<UniversalAdditionalCameraData>();
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            var sunlight = new GameObject("Harbor sun", typeof(Light)).GetComponent<Light>();
            sunlight.type = LightType.Directional; sunlight.intensity = 1.35f; sunlight.shadows = LightShadows.Soft;
            sunlight.transform.rotation = Quaternion.Euler(48, -35, 0);
            var root = new GameObject("Harbor Prototype • first day", typeof(PlayableDirector), typeof(HabitatDockPrototype));
            var prototype = root.GetComponent<HabitatDockPrototype>();
            prototype.harborCamera = camera; prototype.sun = sunlight;
            prototype.director = root.GetComponent<PlayableDirector>();
            prototype.director.playOnAwake = false;
            prototype.director.extrapolationMode = DirectorWrapMode.None;
            prototype.director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            prototype.openingTimeline = opening; prototype.morningTimeline = morning;
            prototype.director.playableAsset = opening;
            foreach (TrackAsset track in opening.GetOutputTracks()) prototype.director.SetGenericBinding(track, prototype);
            foreach (TrackAsset track in morning.GetOutputTracks()) prototype.director.SetGenericBinding(track, prototype);
            prototype.SetSceneResources(palette, font);
            prototype.BuildPrototypeWorld();
            EditorUtility.SetDirty(prototype);
            EditorUtility.SetDirty(prototype.director);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Unable to save HarborPrototype scene.");
            EnsureSceneEntries();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("[Harbor] Scene authored. Timeline clips can be selected and edited in the Timeline window.\n" + InspectAssets());
        }

        static Material[] LoadOrCreateMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader is required.");
            var result = new Material[HabitatDockPrototype.PrototypeColors.Length];
            for (int i = 0; i < result.Length; i++)
            {
                string path = MaterialDirectory + "/Harbor_" + i.ToString("00") + ".mat";
                result[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (result[i]) continue;
                result[i] = new Material(shader) { name = "Harbor " + i.ToString("00"), color = HabitatDockPrototype.PrototypeColors[i] };
                result[i].SetFloat("_Smoothness", .18f);
                AssetDatabase.CreateAsset(result[i], path);
            }
            return result;
        }

        static TimelineAsset NewTimeline(string path, string title, out HabitatDockBeatTrack track)
        {
            var asset = ScriptableObject.CreateInstance<TimelineAsset>(); asset.name = title;
            AssetDatabase.CreateAsset(asset, path);
            asset.editorSettings.frameRate = 30;
            track = asset.CreateTrack<HabitatDockBeatTrack>(null, "镜头与主角调度");
            return asset;
        }

        static TimelineAsset LoadOrCreateOpening()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TimelineAsset>(OpeningPath);
            if (existing) return existing; // Preserve subsequent Timeline edits.
            TimelineAsset asset = NewTimeline(OpeningPath, "码头 · 首日归航", out HabitatDockBeatTrack track);
            AddShot(track, HabitatDockBeat.BlueSky, "01 蓝天与海鸟", "蓝天之下，海鸟从头顶掠过。", 0, 3.3,
                HabitatDockPrototype.BoatArrival, HabitatDockPrototype.BoatArrival);
            AddShot(track, HabitatDockBeat.ReturnToDock, "02 从小船回到码头", "带着满满的渔获，从小船回到码头。", 3.3, 4.8,
                HabitatDockPrototype.BoatArrival, HabitatDockPrototype.DockArrival, new Vector3(0, 0, -7.45f), .3f);
            AddShot(track, HabitatDockBeat.StowNet, "03 收起捞网", "收起捞网，把这一筐渔获带去商店。", 8.1, 1.8,
                HabitatDockPrototype.DockArrival, HabitatDockPrototype.DockArrival);
            AddShot(track, HabitatDockBeat.WalkToShop, "04 带渔获前往商店", "沿着岸边的小路，走向商店。", 9.9, 5.4,
                HabitatDockPrototype.DockArrival, HabitatDockPrototype.ShopApproach, new Vector3(0, 0, 1.25f), .34f);
            EditorUtility.SetDirty(track); EditorUtility.SetDirty(asset);
            return asset;
        }

        static TimelineAsset LoadOrCreateMorning()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TimelineAsset>(MorningPath);
            if (existing) return existing;
            TimelineAsset asset = NewTimeline(MorningPath, "家 · 窗台晨光", out HabitatDockBeatTrack track);
            AddShot(track, HabitatDockBeat.MorningWindow, "01 阳光照入窗台", "回家休息。清晨的阳光慢慢照入窗台……", 0, 5,
                HabitatDockPrototype.HomeApproach, HabitatDockPrototype.HomeApproach);
            EditorUtility.SetDirty(track); EditorUtility.SetDirty(asset);
            return asset;
        }

        static void AddShot(HabitatDockBeatTrack track, HabitatDockBeat beat, string title, string caption,
            double start, double duration, Vector3 from, Vector3 to, Vector3? waypoint = null, float waypointTime = .4f)
        {
            TimelineClip clip = track.CreateClip<HabitatDockBeatClip>();
            clip.displayName = title; clip.start = start; clip.duration = duration;
            var data = (HabitatDockBeatClip)clip.asset;
            data.beat = beat; data.caption = caption; data.actorStart = from; data.actorEnd = to;
            data.useWaypoint = waypoint.HasValue; data.waypoint = waypoint ?? Vector3.zero; data.waypointTime = waypointTime;
            if (beat == HabitatDockBeat.ReturnToDock) { data.pathPoints = HabitatImportedDockLayout.ReturnPath; data.useWaypoint = false; }
            else if (beat == HabitatDockBeat.WalkToShop) { data.pathPoints = HabitatImportedDockLayout.ShopPath; data.useWaypoint = false; }
            EditorUtility.SetDirty(data);
        }

        static void EnsureSceneEntries()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (string path in new[] { IslandScenePath, ScenePath })
            {
                int index = scenes.FindIndex(s => s.path == path);
                if (index >= 0) scenes[index] = new EditorBuildSettingsScene(path, true);
                else scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Read-only report, safe before or after entering Play mode.</summary>
        public static string InspectAssets()
        {
            var report = new StringBuilder();
            report.AppendLine("Scene exists: " + File.Exists(ScenePath));
            foreach (string path in new[] { OpeningPath, MorningPath })
            {
                TimelineAsset asset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
                report.AppendLine(path + ": " + (asset ? asset.duration.ToString("0.000") + " seconds" : "MISSING"));
                if (asset) foreach (TrackAsset track in asset.GetOutputTracks())
                {
                    report.AppendLine("  " + track.GetType().Name + ": " + track.GetClips().Count() + " clips");
                    foreach (TimelineClip clip in track.GetClips())
                        report.AppendLine("    " + clip.displayName + " @ " + clip.start.ToString("0.00") + " + " + clip.duration.ToString("0.00"));
                }
            }
            report.AppendLine("Harbor enabled in scene list: " + EditorBuildSettings.scenes.Any(s => s.path == ScenePath && s.enabled));
            var instance = UnityEngine.Object.FindFirstObjectByType<HabitatDockPrototype>();
            if (instance) report.AppendLine(instance.GetStatusJson());
            return report.ToString();
        }
    }
}
