using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HabitatIsland.Editor
{
    public static partial class HabitatEquipmentSetup
    {
        public const string TideEngineSource = "Assets/HabitatIsland/Art/TideEngine/TideEngine_GameReady.glb";
        public const string TideEnginePrefabPath = ModelsFolder + "/TideEngine/TideEngine.prefab";
        [MenuItem("Tools/Habitat Island/Art/Prepare Tide Engine")]
        public static void PrepareTideEngineMenu() { Debug.Log(PrepareTideEngine()); }
        public static string PrepareTideEngine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            string folder = ModelsFolder + "/TideEngine";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var report = new StringBuilder("Delivered rune tablet is the Tide Engine. Rigid pressure plate, no gameplay changes.\n");
            Prepare(TideEngineSource, TideEnginePrefabPath, "TideEngine", null, false,
                new[] { new ClipSpec("Idle", 2f, true), new ClipSpec("Press", .4f, false), new ClipSpec("Active", 2f, true), new ClipSpec("Release", .4f, false) }, report);
            var root = PrefabUtility.LoadPrefabContents(TideEnginePrefabPath);
            try
            {
                var pivot = root.transform.GetChild(0); pivot.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                Bounds raw = BakedBounds(root); float scale = .94f / Mathf.Max(raw.size.x, raw.size.z);
                pivot.localScale = Vector3.one * scale;
                pivot.localPosition = new Vector3(-raw.center.x * scale, .125f - raw.min.y * scale, -raw.center.z * scale);
                report.AppendLine("Flat rune faces +Y; footprint .94 m, original proportions; bounds=" + BakedBounds(root));
                var ringObject = new GameObject("Active tide indicator"); ringObject.transform.SetParent(root.transform, false);
                var ring = ringObject.AddComponent<LineRenderer>(); ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 64; ring.widthMultiplier = .018f;
                ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; ring.receiveShadows = false;
                for (int i = 0; i < 64; i++) { float a = i * Mathf.PI * 2f / 64; ring.SetPosition(i, new Vector3(Mathf.Cos(a) * .49f, .045f, Mathf.Sin(a) * .49f)); }
                string materialPath = folder + "/TideIndicator.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, materialPath); }
                mat.SetColor("_BaseColor", new Color(.08f, .78f, .86f, 1)); ring.sharedMaterial = mat; ring.enabled = false;
                root.AddComponent<HabitatTideEngineVisual>().Configure(ring);
                PrefabUtility.SaveAsPrefabAsset(root, TideEnginePrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets(); Directory.CreateDirectory("Verification/tide-engine-20261019");
            File.WriteAllText("Verification/tide-engine-20261019/import.txt", report.ToString()); return report.ToString();
        }
    }
}

