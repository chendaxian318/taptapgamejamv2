using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

// Read-only asset checks plus serial, reversible acceptance checks of the real player in Play.
// No import/Prepare/Save/scene mutation API is called. Only reports and screenshots are written.
public static class BackBasketChecks
{
    const string Source = "Assets/HabitatIsland/Art/BackBasket/BackBasket_v2.glb";
    const string Prefab = "Assets/HabitatIsland/Resources/Models/BackBasket/Basket.prefab";
    const string Delivery = "C:/Users/admin/Desktop/聚光灯草案/part3/hona/背篓/3d/模型/背篓_静态_v2.glb";
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly StringBuilder report = new StringBuilder();
    static int assertions;
    static string Folder => Path.GetFullPath("Logs/back-basket-20261016");
    static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Fields).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Fields).SetValue(o, value);
    static string Json(object o) => JsonUtility.ToJson(o);
    static void Require(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); }
    static void Check(bool condition, string label)
    { report.AppendLine((condition ? "PASS " : "FAIL ") + (++assertions) + ": " + label); Require(condition, label); }
    static Dictionary<FieldInfo, object> Scalars(object o) => o.GetType().GetFields(Fields)
        .Where(f => !f.IsInitOnly && !f.IsStatic && (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string)))
        .ToDictionary(f => f, f => f.GetValue(o));
    static void RestoreScalars(object o, Dictionary<FieldInfo, object> values)
    { foreach (var item in values) item.Key.SetValue(o, item.Value); }
    static string Hash(string path)
    { using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
    static Transform Named(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == name);
    static Bounds MeshBounds(Transform root)
    {
        bool found = false; Bounds bounds = default;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                Vector3 point = root.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                Require(float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z), "Nonfinite basket geometry.");
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
            }
        Require(found, "No static basket vertices."); return bounds;
    }
    static string TextureDescription(Texture texture)
        => texture ? texture.name + " [" + AssetDatabase.GetAssetPath(texture) + "] " + texture.width + "x" + texture.height : "null";
    static string Number(float value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    static List<string> MaterialDifferences(Material original, Material prepared, List<string> metadata = null)
    {
        var differences = new List<string>();
        if (!original || !prepared) { differences.Add("Missing source/prepared material: " + original + " / " + prepared); return differences; }
        if (original == prepared) differences.Add("Prepared material is the original reference, not an independent copy.");
        if (original.shader != prepared.shader)
        { differences.Add("shader: source=" + original.shader + "; prepared=" + prepared.shader); return differences; }
        if (!AssetDatabase.GetAssetPath(prepared).StartsWith("Assets/HabitatIsland/Resources/Models/BackBasket/", StringComparison.Ordinal))
            differences.Add("Unexpected prepared material path: " + AssetDatabase.GetAssetPath(prepared));
        if (original.renderQueue != prepared.renderQueue) differences.Add("effective renderQueue: source=" + original.renderQueue + "; prepared=" + prepared.renderQueue);
        string originalRenderType = original.GetTag("RenderType", false, ""), preparedRenderType = prepared.GetTag("RenderType", false, "");
        if (originalRenderType != preparedRenderType) differences.Add("RenderType: source=" + originalRenderType + "; prepared=" + preparedRenderType);
        var shader = original.shader;
        for (int i = 0; i < shader.GetPropertyCount(); i++)
        {
            string property = shader.GetPropertyName(i);
            switch (shader.GetPropertyType(i))
            {
                case ShaderPropertyType.Texture:
                    if (original.GetTexture(property) != prepared.GetTexture(property)) differences.Add(property + " texture: source="
                        + TextureDescription(original.GetTexture(property)) + "; prepared=" + TextureDescription(prepared.GetTexture(property)));
                    if (original.GetTextureScale(property) != prepared.GetTextureScale(property)) differences.Add(property + " UV scale: source="
                        + original.GetTextureScale(property).ToString("G9") + "; prepared=" + prepared.GetTextureScale(property).ToString("G9"));
                    if (original.GetTextureOffset(property) != prepared.GetTextureOffset(property)) differences.Add(property + " UV offset: source="
                        + original.GetTextureOffset(property).ToString("G9") + "; prepared=" + prepared.GetTextureOffset(property).ToString("G9"));
                    break;
                case ShaderPropertyType.Color:
                    if (original.GetColor(property) != prepared.GetColor(property)) differences.Add(property + " color: source="
                        + original.GetColor(property).ToString("G9") + "; prepared=" + prepared.GetColor(property).ToString("G9")); break;
                case ShaderPropertyType.Vector:
                    if (original.GetVector(property) != prepared.GetVector(property)) differences.Add(property + " vector: source="
                        + original.GetVector(property).ToString("G9") + "; prepared=" + prepared.GetVector(property).ToString("G9")); break;
                case ShaderPropertyType.Float: case ShaderPropertyType.Range:
                    float sourceValue = original.GetFloat(property), preparedValue = prepared.GetFloat(property);
                    if (!Mathf.Approximately(sourceValue, preparedValue))
                    {
                        // Unity initializes this editor-only queue-control flag when saving
                        // the copied material. Permit only the observed -1 -> 1 transition,
                        // and only when the effective draw queue and render type are unchanged.
                        if (property == "_BUILTIN_QueueControl" && sourceValue == -1f && preparedValue == 1f
                            && original.renderQueue == prepared.renderQueue && originalRenderType == preparedRenderType)
                            metadata?.Add(property + ": -1 -> 1 (saved-material editor initialization); effective renderQueue="
                                + original.renderQueue + ", RenderType=" + originalRenderType + " are equal.");
                        else differences.Add(property + " float: source=" + Number(sourceValue) + "; prepared=" + Number(preparedValue));
                    }
                    break;
                case ShaderPropertyType.Int:
                    if (original.GetInteger(property) != prepared.GetInteger(property)) differences.Add(property + " int: source="
                        + original.GetInteger(property) + "; prepared=" + prepared.GetInteger(property)); break;
            }
        }
        if (!original.shaderKeywords.OrderBy(k => k).SequenceEqual(prepared.shaderKeywords.OrderBy(k => k)))
            differences.Add("keywords: source=[" + string.Join(",", original.shaderKeywords.OrderBy(k => k))
                + "]; prepared=[" + string.Join(",", prepared.shaderKeywords.OrderBy(k => k)) + "]");
        return differences;
    }
    static bool SameMaterial(Material original, Material prepared)
    {
        var metadata = new List<string>(); var differences = MaterialDifferences(original, prepared, metadata);
        foreach (string item in metadata) report.AppendLine("MATERIAL EDITOR METADATA " + original.name + ": " + item);
        foreach (string difference in differences) report.AppendLine("MATERIAL DIFF " + original.name + ": " + difference);
        return differences.Count == 0;
    }
    public static string DiagnoseMaterials()
    {
        var result = new StringBuilder();
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        Require(original && prefab, "Source and prepared basket must exist for material diagnostics.");
        var seen = new HashSet<Material>(); var preparedRenderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var renderer in original.GetComponentsInChildren<MeshRenderer>(true))
        {
            var preparedRenderer = preparedRenderers.Single(r => r.name == renderer.name);
            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                Material source = renderer.sharedMaterials[i]; if (!seen.Add(source)) continue;
                Material prepared = preparedRenderer.sharedMaterials[i];
                result.AppendLine("MATERIAL " + source.name + " -> " + prepared.name + "; shader=" + source.shader.name);
                result.AppendLine("INFO intentional name and instancing differences: sourceInstancing=" + source.enableInstancing + "; preparedInstancing=" + prepared.enableInstancing);
                result.AppendLine("INFO renderQueue=" + source.renderQueue + "/" + prepared.renderQueue + "; doubleSidedGI=" + source.doubleSidedGI + "/" + prepared.doubleSidedGI
                    + "; GIFlags=" + source.globalIlluminationFlags + "/" + prepared.globalIlluminationFlags);
                var metadata = new List<string>(); var differences = MaterialDifferences(source, prepared, metadata);
                foreach (string item in metadata) result.AppendLine("EDITOR METADATA " + item);
                foreach (string difference in differences) result.AppendLine("DIFF " + difference);
                if (differences.Count == 0) result.AppendLine("MATCH all compared PBR and shader properties.");
            }
        }
        Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "material-differences.txt"), result.ToString(), new UTF8Encoding(false));
        return result.ToString();
    }
    public static string RunAssets(string deliveredPath = null)
    {
        assertions = 0; report.Clear(); Directory.CreateDirectory(Folder);
        try
        {
            string delivery = string.IsNullOrEmpty(deliveredPath) ? Delivery : deliveredPath;
            Check(File.Exists(delivery) && File.Exists(Source), "delivered v2 and project source GLBs are present");
            string deliveredHash = Hash(delivery), importedHash = Hash(Source);
            report.AppendLine("SHA256 delivered=" + deliveredHash + "; project=" + importedHash);
            Check(deliveredHash == importedHash, "project source is byte-identical to the supplied v2 GLB");
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            Check(original && prefab && Resources.Load<GameObject>("Models/BackBasket/Basket") == prefab, "the actual Resources basket prefab is available");
            var sourceMeshes = original.GetComponentsInChildren<MeshFilter>(true);
            var preparedMeshes = prefab.GetComponentsInChildren<MeshFilter>(true);
            Check(sourceMeshes.Length == 11 && preparedMeshes.Length == 11 && preparedMeshes.All(f => f.sharedMesh), "source and prepared prefab contain all 11 static mesh parts");
            Check(AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>().Count() == 0
                && original.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0
                && prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0
                && prefab.GetComponentsInChildren<Animator>(true).Length == 0 && prefab.GetComponentsInChildren<Animation>(true).Length == 0,
                "static v2 has no clips, skin or independent animation driver");
            Check(prefab.GetComponentsInChildren<Collider>(true).Length == 0 && prefab.GetComponentsInChildren<Rigidbody>(true).Length == 0,
                "equipped basket introduces no movement-blocking collider or physics body");
            Vector3? translation = null; int triangles = 0; float maxError = 0f;
            foreach (var filter in sourceMeshes)
            {
                var prepared = preparedMeshes.Single(f => f.name == filter.name);
                Check(prepared.sharedMesh == filter.sharedMesh, "source mesh/UV/normals retained: " + filter.name);
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 a = original.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    Vector3 b = prefab.transform.InverseTransformPoint(prepared.transform.TransformPoint(vertex));
                    if (!translation.HasValue) translation = b - a;
                    maxError = Mathf.Max(maxError, Vector3.Distance(b - a, translation.Value));
                }
                triangles += filter.sharedMesh.triangles.Length / 3;
            }
            Check(maxError < .00001f, "all source vertices differ only by the same attachment-pivot translation");
            var sourceRenderers = original.GetComponentsInChildren<MeshRenderer>(true);
            var preparedRenderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            var sourceMaterials = sourceRenderers.SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            var preparedMaterials = preparedRenderers.SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            Check(sourceMaterials.Length == 6 && preparedMaterials.Length == 6, "all six distinct source PBR materials remain independent");
            foreach (var renderer in sourceRenderers)
            {
                var prepared = preparedRenderers.Single(r => r.name == renderer.name);
                Check(renderer.sharedMaterials.Length == prepared.sharedMaterials.Length
                    && renderer.sharedMaterials.Select((m, i) => SameMaterial(m, prepared.sharedMaterials[i])).All(value => value),
                    "PBR shader, textures, UV transform and scalar/color channels preserved: " + renderer.name);
            }
            Bounds sourceBounds = MeshBounds(original.transform), bounds = MeshBounds(prefab.transform);
            var mount = Named(prefab.transform, "BackMount"); var rim = Named(prefab.transform, "BasketRim");
            Check(mount && rim && Named(prefab.transform, "SourceBaseOrigin") && mount.localPosition.sqrMagnitude < .000001f,
                "back attachment, rim and source-origin markers are present with BackMount at origin");
            Check(Mathf.Abs(sourceBounds.size.y - .65f) < .002f && Mathf.Abs(bounds.size.y - sourceBounds.size.y) < .00001f
                && bounds.Contains(rim.localPosition) && rim.localPosition.y > bounds.center.y, "source units are preserved and the rim lies in the upper basket");
            report.AppendLine("MEASURE triangles=" + triangles + ", sourceBounds=" + sourceBounds + ", prefabBounds=" + bounds
                + ", pivotTranslation=" + translation + ", maximumVertexError=" + maxError.ToString("F8") + ", rim=" + rim.localPosition.ToString("F6"));
            report.AppendLine("PASS: " + assertions + " read-only asset assertions; no assets were imported or modified.");
            return report.ToString();
        }
        catch (Exception exception) { report.AppendLine("FAIL " + exception); throw new InvalidOperationException(report.ToString(), exception); }
        finally { File.WriteAllText(Path.Combine(Folder, "asset-checks.txt"), report.ToString(), new UTF8Encoding(false)); }
    }
    static async Task Frames(int count)
    {
        int start = Time.frameCount; double deadline = EditorApplication.timeSinceStartup + 10;
        while (Time.frameCount - start < count)
        { Require(Application.isPlaying && EditorApplication.timeSinceStartup < deadline, "Play loop stopped advancing."); await Task.Delay(12); }
    }
    static async Task<T> AfterCamera<T>(Camera camera, Func<T> sample)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<ScriptableRenderContext, Camera> handler = (context, rendered) =>
        {
            if (rendered != camera || completion.Task.IsCompleted) return;
            try { completion.TrySetResult(sample()); } catch (Exception error) { completion.TrySetException(error); }
        };
        RenderPipelineManager.endCameraRendering += handler;
        try { Require(await Task.WhenAny(completion.Task, Task.Delay(8000)) == completion.Task, "Main camera did not render."); return await completion.Task; }
        finally { RenderPipelineManager.endCameraRendering -= handler; }
    }
    static async Task Press(Keyboard keyboard, Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); await Frames(2);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
    }
    static HabitatState Flat(HabitatState source, int height = 1)
    {
        var state = HabitatRules.Clone(source); state.width = state.depth = 7; state.tiles = new Tile[49];
        for (int i = 0; i < 49; i++) state.tiles[i] = new Tile { height = height };
        state.animals.Clear(); state.events.Clear(); state.goals = Array.Empty<Species>();
        state.playerX = state.playerZ = 3; state.facingX = 0; state.facingZ = 1; state.dockX = state.dockZ = 1; state.dockRotation = -1;
        state.selectedX = 4; state.selectedZ = 3; state.onHarbor = false; state.carryingAnimal = -1; state.carryingProp = PropKind.None;
        state.waterLevel = state.tideBaseLevel = state.tickCount = 0; state.tideActive = false; state.movementClock = state.elapsed = 0f;
        state.openingCompleted = state.openingCatchGranted = true; state.won = false; state.rod = true;
        state.levelName = "背篓实帧夹具"; state.objective = "仅检查，不保存"; return state;
    }
    static async Task Install(HabitatGame game, HabitatState state)
    {
        if (game.Fishing.IsOpen) game.Fishing.Cancel(); game.CancelPendingAction(); game.Hud.ClosePanels();
        game.State = state; game.paused = true; game.tool = 0; game.ActionCount = 0; Get<Stack<HabitatState>>(game, "history").Clear();
        game.View.Initialize(state); game.View.FocusPlayer(); Set(game.View, "cameraSize", 2.8f); await Frames(4);
    }
    static void Persistent(HabitatPlayerVisual visual, Transform expected, string label)
    {
        Check(visual.BackBasketRoot && visual.BackBasketRoot == expected && visual.BackBasketRim
            && visual.BackBasketRim.IsChildOf(expected) && visual.BackBasketAttachmentBone
            && visual.BackBasketAttachmentBone.name == "mixamorig:Spine2"
            && visual.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Traveler back basket v2") == 1
            && visual.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Traveler back basket socket") == 1,
            label + ": exactly one permanent v2 basket remains attached to Spine2");
    }
    sealed class Fit
    {
        public Vector3 bonePosition, scale, worldPosition;
        public Quaternion boneRotation, worldRotation;
        public bool visible, falling, landing; public float handling;
    }
    static Fit ReadFit(HabitatPlayerVisual visual) => new Fit
    {
        bonePosition = visual.BackBasketAttachmentBone.InverseTransformPoint(visual.BackBasketRoot.position),
        boneRotation = Quaternion.Inverse(visual.BackBasketAttachmentBone.rotation) * visual.BackBasketRoot.rotation,
        scale = visual.BackBasketRoot.lossyScale, worldPosition = visual.BackBasketRoot.position, worldRotation = visual.BackBasketRoot.rotation,
        visible = visual.BackBasketRoot.gameObject.activeInHierarchy, falling = visual.IsFalling, landing = visual.IsLanding, handling = visual.HandlingPoseWeight
    };
    static async Task Motion(HabitatGame game, Action action, string label, bool handling = false, bool fall = false, bool turn = false)
    {
        var visual = game.View.PlayerVisual; var root = visual.BackBasketRoot;
        Fit start = await AfterCamera(game.View.GameCamera, () => ReadFit(visual)); action(); Require(game.Busy, label + " did not start a real transaction.");
        float positionError = 0f, rotationError = 0f, scaleError = 0f, displacement = 0f, rotation = 0f;
        bool sawHandling = false, sawFall = false, sawLanding = false; int frames = 0;
        double deadline = EditorApplication.timeSinceStartup + 12;
        do
        {
            Fit current = await AfterCamera(game.View.GameCamera, () => ReadFit(visual)); frames++;
            Require(current.visible && visual.BackBasketRoot == root, label + ": basket disappeared/recreated during motion.");
            positionError = Mathf.Max(positionError, Vector3.Distance(current.bonePosition, start.bonePosition));
            rotationError = Mathf.Max(rotationError, Quaternion.Angle(current.boneRotation, start.boneRotation));
            scaleError = Mathf.Max(scaleError, Vector3.Distance(current.scale, start.scale));
            displacement = Mathf.Max(displacement, Vector3.Distance(current.worldPosition, start.worldPosition));
            rotation = Mathf.Max(rotation, Quaternion.Angle(current.worldRotation, start.worldRotation));
            sawHandling |= current.handling > .15f; sawFall |= current.falling; sawLanding |= current.landing;
            Require(EditorApplication.timeSinceStartup < deadline, label + " transaction timed out.");
        } while (game.Busy);
        report.AppendLine("MEASURE " + label + ": frames=" + frames + ", boneOffsetError=" + positionError.ToString("F6")
            + ", boneRotationError=" + rotationError.ToString("F4") + ", worldScaleError=" + scaleError.ToString("F6")
            + ", traveled=" + displacement.ToString("F4") + ", turned=" + rotation.ToString("F3"));
        Check(frames >= 3 && displacement > .2f && positionError < .003f && rotationError < .3f && scaleError < .003f
            && (!handling || sawHandling) && (!fall || sawFall && sawLanding) && (!turn || rotation > 30f),
            label + ": rendered basket follows the animated chest and remains rigid through the actual movement/pose");
        Persistent(visual, root, label);
    }
    static async Task Shot(string name)
    { string path = Path.Combine(Folder, name + ".png"); ScreenCapture.CaptureScreenshot(path); await Frames(3); report.AppendLine("SCREENSHOT " + path); }
    static async Task Portrait(HabitatGame game, string name, bool side)
    {
        var camera = game.View.GameCamera; Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
        float size = camera.orthographicSize; bool enabled = game.enabled;
        try
        {
            // Freeze Game.Update only for a diagnostic photograph; all acceptance observations above use live frames.
            game.enabled = false; Transform facing = Get<Transform>(game.View.PlayerVisual, "facingRoot");
            Vector3 forward = facing.forward; forward.y = 0f; forward.Normalize(); Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 target = game.View.PlayerWorldPosition + Vector3.up * .65f - forward * .12f;
            camera.transform.position = target + (side ? right * 3.2f - forward * .4f : -forward * 3.2f + right * .4f) + Vector3.up * 1.1f;
            camera.transform.LookAt(target); camera.orthographicSize = 1.05f; await Shot(name);
        }
        finally { camera.transform.SetPositionAndRotation(position, rotation); camera.orthographicSize = size; game.enabled = enabled; }
    }
    static void CleanStory(HabitatGame game, Transform permanent, string label)
    {
        Persistent(game.View.PlayerVisual, permanent, label);
        Check(!Get<Transform>(game.Story, "catchRoot") && !Get<Transform>(game.Story, "effectsRoot")
            && !game.View.PlayerVisual.BackBasketRim.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Opening catch in back basket")
            && !game.View.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Main story temporary effects" || t.name == "Full catch basket"),
            label + ": temporary fish and old substitute basket leave no scene remnants");
    }
    public static async Task<string> Run()
    {
        assertions = 0; report.Clear(); Directory.CreateDirectory(Folder);
        var game = HabitatGame.Instance;
        Require(Application.isPlaying && game && game.State != null && game.State.openingCompleted && !game.Busy
            && !game.Hud.ModalOpen && !game.Fishing.IsOpen && game.Story && game.View.Harbor.Ready, "Run after opening, idle, with the main-map player and no modal.");
        var visual = game.View.PlayerVisual; Require(visual && visual.BackBasketRoot, "Compile and prepare the permanent basket first.");
        HabitatState original = game.State; string originalJson = Json(original); Tuning tuning = game.tuning;
        var history = Get<Stack<HabitatState>>(game, "history"); var savedHistory = history.ToArray();
        var visits = Get<List<HabitatGame.VisitedLevel>>(game, "visitedLevels"); var savedVisits = visits.ToArray();
        string historyJson = string.Join("|", savedHistory.Select(Json)), visitsJson = string.Join("|", savedVisits.Select(Json));
        var gameScalars = Scalars(game); var hudScalars = Scalars(game.Hud);
        var ui = game.Hud.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.gameObject, t => t.gameObject.activeSelf);
        string[] cameraNames = { "cameraYaw", "cameraSize", "cameraTarget", "cameraTargetSmooth", "followPlayer", "overviewCamera", "overviewSize", "framingAspect", "framingDirty", "nextVisibilityCheck" };
        var cameraFields = cameraNames.ToDictionary(n => n, n => typeof(HabitatView).GetField(n, Fields).GetValue(game.View));
        var camera = game.View.GameCamera; Vector3 cameraPosition = camera.transform.position; Quaternion cameraRotation = camera.transform.rotation;
        float cameraSize = camera.orthographicSize; bool cameraOrtho = camera.orthographic; Color cameraColor = camera.backgroundColor; var clearFlags = camera.clearFlags;
        var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        Keyboard previousKeyboard = Keyboard.current, keyboard = null; Mouse previousMouse = Mouse.current, mouse = null;
        var background = InputSystem.settings.backgroundBehavior; var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        Transform permanent = visual.BackBasketRoot; Exception failure = null;
        try
        {
            keyboard = InputSystem.AddDevice<Keyboard>("Basket acceptance keyboard"); keyboard.MakeCurrent();
            mouse = InputSystem.AddDevice<Mouse>("Basket acceptance mouse"); mouse.MakeCurrent();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.EnableDevice(keyboard); InputSystem.EnableDevice(mouse);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            visits.Clear(); game.tuning = JsonUtility.FromJson<Tuning>(Json(tuning)); game.tuning.animalMovement = false; game.tuning.breedSeconds = 9999f;
            await Install(game, Flat(original)); Persistent(visual, permanent, "initial fixture");
            Fit fitted = await AfterCamera(camera, () => ReadFit(visual));
            Check(fitted.visible && fitted.scale.x > 0f && Mathf.Abs(fitted.scale.x - fitted.scale.y) < .001f
                && Mathf.Abs(fitted.scale.y - fitted.scale.z) < .001f, "permanent basket is visible with a rigid uniform world scale");
            report.AppendLine("FIT boneOffset=" + fitted.bonePosition.ToString("F6") + ", scale=" + fitted.scale.ToString("F6")
                + ", rootInFacing=" + Get<Transform>(visual, "facingRoot").InverseTransformPoint(permanent.position).ToString("F6"));
            await Shot("basket-normal-view"); await Portrait(game, "basket-back-close", false); await Portrait(game, "basket-side-close", true);
            await Motion(game, () => game.Move(1, 0), "walk and turn", turn: true);
            game.Undo(); await Frames(4); Persistent(visual, permanent, "Undo after walking");
            var fixture = Flat(original); HabitatRules.TileAt(fixture, 4, 3).prop = PropKind.Crate;
            await Install(game, fixture); await Motion(game, () => game.Move(1, 0), "push", handling: true);
            await Install(game, Flat(original)); HabitatRules.TileAt(game.State, 4, 3).prop = PropKind.Crate; await Frames(3);
            await Motion(game, () => game.Pull(-1, 0), "pull", handling: true);
            fixture = Flat(original, 3); HabitatRules.TileAt(fixture, 4, 3).height = 1;
            await Install(game, fixture); await Motion(game, () => game.Move(1, 0), "fall and land", fall: true);
            await Portrait(game, "basket-after-landing", true);
            foreach (Key key in new[] { Key.Digit2, Key.Digit3, Key.Digit5, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit1 })
            { await Press(keyboard, key); Persistent(visual, permanent, "tool switch " + key); }
            game.View.Initialize(game.State); await Frames(4); Persistent(visual, permanent, "View.Initialize reset");
            game.LoadLevel(1); game.paused = true; await Frames(4); Persistent(visual, permanent, "LoadLevel");

            fixture = Flat(original); fixture.openingCompleted = fixture.openingCatchGranted = false;
            await Install(game, fixture); Require(game.BeginOpening(), "Actual main opening could not begin."); await Frames(3);
            Transform contents = Get<Transform>(game.Story, "catchRoot");
            Check(contents && contents.parent == visual.BackBasketRim && contents.name == "Opening catch in back basket"
                && contents.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Fish ", StringComparison.Ordinal)) == 4
                && !Get<Transform>(game.Story, "effectsRoot").GetComponentsInChildren<Transform>(true).Any(t => t.name == "Full catch basket" || t.name == "Basket"),
                "actual opening puts four temporary fish in the permanent rim and creates no old cube basket");
            Check(contents.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Fish ", StringComparison.Ordinal))
                .All(t => visual.BackBasketRim.InverseTransformPoint(t.position).magnitude < .26f), "opening fish stay immediately around the rim rather than at the former waist prop location");
            double visibleDeadline = EditorApplication.timeSinceStartup + 10;
            while (game.StoryPlaying && !visual.IsModelActive)
            { await Frames(1); Require(EditorApplication.timeSinceStartup < visibleDeadline, "Opening never revealed the existing player."); }
            Check(game.StoryPlaying && permanent.gameObject.activeInHierarchy && contents.gameObject.activeInHierarchy,
                "opening's visible character uses the same basket instance and fish");
            await Shot("basket-opening-catch"); game.Story.Skip(); await Frames(4); CleanStory(game, permanent, "opening Skip");
            Check(game.State.openingCompleted && !game.StoryPlaying && !game.View.IsCinematic, "Skip finishes opening and restores ordinary presentation");
            game.State.openingCompleted = false; string beforeCancel = Json(game.State);
            Require(game.BeginOpening(), "Second opening did not start for cancellation."); await Frames(3); game.CancelPendingAction(); await Frames(4);
            CleanStory(game, permanent, "opening Cancel"); Check(Json(game.State) == beforeCancel, "cancelling opening preserves the pre-opening gameplay state");
            game.State.openingCompleted = true;
            Require(game.View.Harbor.TryPlaceAt(game.State, HabitatImportedDockLayout.HomeApproach), "Could not place at the actual home.");
            int day = game.State.day; game.RestAtHome(); await Frames(3); Require(game.StoryPlaying, "Actual home morning did not begin.");
            contents = Get<Transform>(game.Story, "catchRoot");
            Check(!contents || !contents.gameObject.activeInHierarchy, "morning does not display opening fish in the permanent basket");
            double morningDeadline = EditorApplication.timeSinceStartup + 9;
            while (game.StoryPlaying) { await Frames(1); Require(EditorApplication.timeSinceStartup < morningDeadline, "Morning did not finish naturally."); }
            await Frames(3); CleanStory(game, permanent, "natural Morning completion");
            Check(game.State.day == day + 1 && permanent.gameObject.activeInHierarchy, "natural morning grants one day and retains the wearable basket");
        }
        catch (Exception exception) { failure = exception; report.AppendLine("ERROR " + exception); }
        finally
        {
            if (game.Fishing.IsOpen) game.Fishing.Cancel(); game.CancelPendingAction(); game.Hud.ClosePanels();
            game.State = original; game.tuning = tuning;
            history.Clear(); for (int i = savedHistory.Length - 1; i >= 0; i--) history.Push(savedHistory[i]);
            visits.Clear(); visits.AddRange(savedVisits); RestoreScalars(game, gameScalars);
            game.View.Initialize(original); game.View.Render(original, tuning, game.overlays); // Restoration only, not an acceptance sample.
            foreach (var item in cameraFields) Set(game.View, item.Key, item.Value);
            camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation); camera.orthographicSize = cameraSize;
            camera.orthographic = cameraOrtho; camera.backgroundColor = cameraColor; camera.clearFlags = clearFlags;
            Set(game.Hud, "nextRefresh", 0f); game.Hud.Refresh(); foreach (var item in ui) if (item.Key) item.Key.SetActive(item.Value); RestoreScalars(game.Hud, hudScalars);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(selected);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
            previousKeyboard?.MakeCurrent(); previousMouse?.MakeCurrent();
            InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            bool restored = ReferenceEquals(game.State, original) && Json(original) == originalJson && ReferenceEquals(game.tuning, tuning)
                && history.SequenceEqual(savedHistory) && string.Join("|", history.Select(Json)) == historyJson
                && visits.SequenceEqual(savedVisits) && string.Join("|", visits.Select(Json)) == visitsJson
                && gameScalars.All(item => Equals(item.Value, item.Key.GetValue(game))) && !game.Busy
                && visual.BackBasketRoot == permanent && Keyboard.current == previousKeyboard && Mouse.current == previousMouse;
            report.AppendLine((restored ? "PASS" : "FAIL") + ": original State, tuning, history, visited maps, tool/pause, camera, UI and input restored; permanent basket retained; no save written.");
            if (!restored && failure == null) failure = new InvalidOperationException("Original runtime snapshot was not completely restored.");
            report.AppendLine((failure == null ? "PASS" : "FAIL") + ": " + assertions + " basket acceptance assertions on actual Play frames.");
            File.WriteAllText(Path.Combine(Folder, "runtime-checks.txt"), report.ToString(), new UTF8Encoding(false));
        }
        if (failure != null) throw new InvalidOperationException(report.ToString(), failure); return report.ToString();
    }
}
