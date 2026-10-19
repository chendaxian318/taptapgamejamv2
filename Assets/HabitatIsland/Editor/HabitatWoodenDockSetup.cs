using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Timeline;

namespace HabitatIsland.Editor
{
    /// <summary>Reusable whole harbor, with a reversible stairs-removed mesh variant.</summary>
    public static class HabitatWoodenDockSetup
    {
        public const string OriginalSource = "Assets/HabitatIsland/Art/WoodenDock/WoodenDock.glb";
        public const string Source = "Assets/HabitatIsland/Art/WoodenDock/WoodenDock_NoStairs_NoBoat.glb";
        public const string Folder = "Assets/HabitatIsland/Resources/Models/WoodenDock";
        public const string PrefabPath = Folder + "/Harbor.prefab";
        public const string EnhancedColorPath = "Assets/HabitatIsland/Art/WoodenDock/Enhanced/WoodenDock_BaseColor_RealESRGAN_8k.png";

        static void ApplyEnhancedColor(Material material)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(EnhancedColorPath);
            if (!texture || !material.HasProperty("baseColorTexture")) return;
            material.SetTexture("baseColorTexture", texture);
            material.name = "Hona harbor enhanced color and original roughness";
        }

        /// <summary>Apply the optional 8K color atlas without rebuilding geometry or changing UVs.</summary>
        public static string ApplyEnhancedTexture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play before importing the enhanced texture.");
            AssetDatabase.ImportAsset(EnhancedColorPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(EnhancedColorPath) as TextureImporter;
            if (!importer) throw new FileNotFoundException("Enhanced harbor color texture is missing.", EnhancedColorPath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.maxTextureSize = 8192;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            // BC7 retains the 8K image while avoiding a 341 MiB RGBA32 allocation with mipmaps.
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                name = "Standalone", overridden = true, maxTextureSize = 8192,
                format = TextureImporterFormat.BC7, compressionQuality = 100
            });
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(EnhancedColorPath);
            if (!texture || texture.width != 8192 || texture.height != 8192)
                throw new InvalidOperationException("The enhanced atlas must import at 8192 x 8192.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Harbor0.mat");
            if (!material) throw new InvalidOperationException("Prepare the harbor material first.");
            ApplyEnhancedColor(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return "Harbor color: " + texture.width + "x" + texture.height + ", " + texture.graphicsFormat
                + "; source geometry, UVs and metallic/roughness texture preserved.";
        }

        public static string Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play before preparing the harbor prefab.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (!source) throw new InvalidOperationException("WoodenDock.glb is not imported.");
            var root = new GameObject("Hona wooden harbor");
            try
            {
                var model = UnityEngine.Object.Instantiate(source, root.transform, false);
                model.name = "Harbor mesh - stairs removed";
                model.transform.localScale = Vector3.one * HabitatImportedDockLayout.SourceScale;
                model.transform.localPosition = HabitatImportedDockLayout.SourceOffset;
                int index = 0;
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        if (!slots[i]) throw new InvalidOperationException("Harbor material is missing.");
                        string path = Folder + "/Harbor" + index++ + ".mat";
                        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!material) { material = new Material(slots[i]); AssetDatabase.CreateAsset(material, path); }
                        else EditorUtility.CopySerialized(slots[i], material);
                        HabitatIsland.EditorTools.HabitatTextureOptimization.Apply(material);
                        material.name = "Hona harbor original color and roughness";
                        if (material.HasProperty("metallicFactor")) material.SetFloat("metallicFactor", .05f);
                        ApplyEnhancedColor(material);
                        material.enableInstancing = true;
                        EditorUtility.SetDirty(material);
                        slots[i] = material;
                    }
                    renderer.sharedMaterials = slots;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    var collider = filter.GetComponent<MeshCollider>();
                    if (!collider) collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                string report = "Source: " + Source + "\nPrefab: " + PrefabPath
                    + "\nScale: " + HabitatImportedDockLayout.SourceScale + "; offset: " + HabitatImportedDockLayout.SourceOffset
                    + "\nBounds: " + HabitatShrimpSetup.MeshBounds(root)
                    + "\nAnimation clips: " + AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>().Count()
                    + "\nWhole merged scene: homes, platform, boat, foliage and props remain together.\n";
                Directory.CreateDirectory("Logs/models-20261015");
                File.WriteAllText("Logs/models-20261015/dock-import.txt", report);
                return report;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static string ApplyToHarborScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play before migrating the harbor scene.");
            var current = EditorSceneManager.GetActiveScene();
            if (current.IsValid() && current.isDirty) throw new InvalidOperationException("Save the current scene first.");
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) throw new InvalidOperationException("Prepare the harbor prefab first.");
            var scene = EditorSceneManager.OpenScene(HabitatDockPrototypeSetup.ScenePath, OpenSceneMode.Single);
            var dock = UnityEngine.Object.FindFirstObjectByType<HabitatDockPrototype>();
            if (!dock) throw new InvalidOperationException("Harbor component is missing.");
            dock.RebuildImportedWorld();
            foreach (TimelineAsset timeline in new[] { dock.openingTimeline, dock.morningTimeline })
            {
                foreach (TrackAsset track in timeline.GetOutputTracks()) foreach (TimelineClip item in track.GetClips())
                {
                    if (!(item.asset is HabitatDockBeatClip shot)) continue;
                    shot.useWaypoint = false; shot.pathPoints = null;
                    switch (shot.beat)
                    {
                        case HabitatDockBeat.BlueSky:
                            shot.actorStart = shot.actorEnd = HabitatImportedDockLayout.BoatArrival; break;
                        case HabitatDockBeat.ReturnToDock:
                            shot.actorStart = HabitatImportedDockLayout.BoatArrival;
                            shot.actorEnd = HabitatImportedDockLayout.DockArrival;
                            shot.pathPoints = HabitatImportedDockLayout.ReturnPath;
                            break;
                        case HabitatDockBeat.StowNet:
                            shot.actorStart = shot.actorEnd = HabitatImportedDockLayout.DockArrival; break;
                        case HabitatDockBeat.WalkToShop:
                            shot.actorStart = HabitatImportedDockLayout.DockArrival;
                            shot.actorEnd = HabitatImportedDockLayout.ShopApproach;
                            shot.pathPoints = HabitatImportedDockLayout.ShopPath;
                            if (shot.caption == "沿着岸边的小路，走向商店。") shot.caption = "沿着木栈道，走向商店。";
                            break;
                        case HabitatDockBeat.MorningWindow:
                            shot.actorStart = shot.actorEnd = HabitatImportedDockLayout.HomeApproach; break;
                    }
                    EditorUtility.SetDirty(shot);
                }
                EditorUtility.SetDirty(timeline);
            }
            EditorUtility.SetDirty(dock);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save imported harbor scene.");
            AssetDatabase.SaveAssets();
            return "Imported harbor replaces blockout geometry. Existing Timeline durations/captions are retained; actor routes follow the real deck and boarding plank.";
        }
    }
}

