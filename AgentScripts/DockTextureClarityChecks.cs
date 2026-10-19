using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Unity CLI run_script entry: DockTextureClarityChecks.Run(). Keep this outside Assets.
// Configure the two PNG paths below. This is a capture/diagnostic tool, not an asset importer.
public static class DockTextureClarityChecks
{
    public static string OriginalPng = "Logs/texture-upscale-20261015/WoodenDock_BaseColor_original_4k.png";
    public static string UpscaledPng = "Logs/texture-upscale-20261015/WoodenDock_BaseColor_RealESRGAN_8k.png";
    // Optional: leave empty to skip the project-imported texture comparison.
    public static string ImportedTexturePath = "Assets/HabitatIsland/Art/WoodenDock/Enhanced/WoodenDock_BaseColor_RealESRGAN_8k.png";
    public static string OutputFolder = "Logs/texture-upscale-20261015";
    const string MaterialPath = "Assets/HabitatIsland/Resources/Models/WoodenDock/Harbor0.mat";
    const string SourcePath = "Assets/HabitatIsland/Art/WoodenDock/WoodenDock.glb";
    const int Width = 2560, Height = 1440;
    static readonly int BaseColor = Shader.PropertyToID("baseColorTexture");
    static bool running;

    sealed class GpuFlag
    {
        public PropertyInfo property;
        public bool original;
    }

    sealed class Slot
    {
        public Renderer renderer;
        public int index;
        public readonly MaterialPropertyBlock original = new MaterialPropertyBlock();
        public readonly MaterialPropertyBlock working = new MaterialPropertyBlock();
        public bool wasEmpty;
        Material originalMaterial;

        public void Capture()
        {
            renderer.GetPropertyBlock(original, index);
            originalMaterial = renderer.sharedMaterials[index];
            wasEmpty = original.isEmpty;
            // A per-material block takes precedence over the renderer-wide block.
            // Preserve the latter's values when creating a slot block for the first time.
            if (wasEmpty) renderer.GetPropertyBlock(working);
            else renderer.GetPropertyBlock(working, index);
        }

        public void Set(Material material)
        {
            // Use an actual material binding change, not only an MPB texture change:
            // GPU-resident/instanced draws can otherwise keep the old texture binding.
            Material[] materials = renderer.sharedMaterials;
            materials[index] = material;
            renderer.sharedMaterials = materials;
            working.SetTexture(BaseColor, material.GetTexture(BaseColor));
            renderer.SetPropertyBlock(working, index);
        }

        public void Restore()
        {
            if (!renderer) return;
            Material[] materials = renderer.sharedMaterials;
            materials[index] = originalMaterial;
            renderer.sharedMaterials = materials;
            renderer.SetPropertyBlock(wasEmpty ? null : original, index);
        }
    }

    sealed class Shot
    {
        public string name;
        public Vector3 center;
        public Quaternion rotation;
        public float size;
    }

    sealed class CameraWitness
    {
        readonly Vector3 position;
        readonly Quaternion rotation;
        readonly Matrix4x4 projection;
        readonly RenderTexture target;
        readonly Rect rect;
        readonly bool enabled, orthographic;
        readonly float size, fov;
        readonly int cullingMask;
        public CameraWitness(Camera camera)
        {
            position = camera.transform.position; rotation = camera.transform.rotation;
            projection = camera.projectionMatrix; target = camera.targetTexture; rect = camera.rect;
            enabled = camera.enabled; orthographic = camera.orthographic;
            size = camera.orthographicSize; fov = camera.fieldOfView; cullingMask = camera.cullingMask;
        }
        public bool Unchanged(Camera camera)
            => camera && camera.transform.position == position && camera.transform.rotation == rotation
                && camera.projectionMatrix == projection && camera.targetTexture == target && camera.rect == rect
                && camera.enabled == enabled && camera.orthographic == orthographic
                && camera.orthographicSize == size && camera.fieldOfView == fov && camera.cullingMask == cullingMask;
    }

    // No await/frame advance: all variants see the same running state, animation pose,
    // light state and shader time. No pause flag, player transform or main camera is changed.
    public static Task<string> Run() => Task.FromResult(Capture());

    static string Capture()
    {
        if (running) throw new InvalidOperationException("A dock texture capture is already running.");
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play with the imported harbor visible in the loaded scene.");
        Camera sourceCamera = Camera.main;
        if (!sourceCamera) throw new InvalidOperationException("No MainCamera is available.");
        if (!File.Exists(OriginalPng) || !File.Exists(UpscaledPng))
            throw new FileNotFoundException("Set OriginalPng/UpscaledPng at the top of AgentScripts/DockTextureClarityChecks.cs to the existing PNGs.");

        running = true;
        var report = new StringBuilder();
        var slots = new List<Slot>();
        var gpuFlags = new Dictionary<MeshRenderer, GpuFlag>();
        var temporaryMaterials = new List<Material>();
        var witness = new CameraWitness(sourceCamera);
        var game = HabitatGame.Instance;
        string stateBefore = game && game.State != null ? JsonUtility.ToJson(game.State) : null;
        bool pausedBefore = game && game.paused;
        int actionsBefore = game ? game.ActionCount : 0, undoBefore = game ? game.UndoCount : 0;
        int frameBefore = Time.frameCount;
        RenderTexture previousActive = RenderTexture.active;
        GameObject cameraObject = null;
        Camera captureCamera = null;
        RenderTexture target = null, resolved = null;
        Texture2D original = null, upscaled = null, diagnostic = null;
        Material harborMaterial = null;
        string materialBefore = null;
        string folder = Path.GetFullPath(OutputFolder);
        bool completed = false;
        try
        {
            Directory.CreateDirectory(folder);
            harborMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!harborMaterial || !harborMaterial.HasProperty(BaseColor)) throw new InvalidOperationException("Harbor0.mat/baseColorTexture is missing.");
            materialBefore = EditorJsonUtility.ToJson(harborMaterial);
            Material sourceMaterial = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<Material>()
                .FirstOrDefault(material => material.HasProperty(BaseColor) && material.GetTexture(BaseColor));
            Texture2D embedded = sourceMaterial ? sourceMaterial.GetTexture(BaseColor) as Texture2D : null;
            if (!embedded) throw new InvalidOperationException("The unmodified GLB embedded base-color texture was not found.");

            foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] == harborMaterial)
                    {
                        var slot = new Slot { renderer = renderer, index = i };
                        slot.Capture(); slots.Add(slot);
                    }
            }
            if (slots.Count == 0) throw new InvalidOperationException("No active renderer directly references Harbor0.mat. Load the island or harbor scene first.");
            Transform model = FindModelRoot(slots[0].renderer.transform);
            if (slots.Any(slot => !slot.renderer.transform.IsChildOf(model)))
                throw new InvalidOperationException("Multiple harbor instances are loaded; capture one scene/instance at a time.");
            Bounds bounds = slots[0].renderer.bounds;
            foreach (Slot slot in slots) bounds.Encapsulate(slot.renderer.bounds);

            original = LoadTexture(OriginalPng, embedded, "A/B external original");
            upscaled = LoadTexture(UpscaledPng, embedded, "A/B Real-ESRGAN output");
            if (original.width != embedded.width || original.height != embedded.height)
                throw new InvalidOperationException("The extracted original PNG dimensions do not match the embedded texture.");
            if (upscaled.width <= original.width || upscaled.height <= original.height
                || (long)upscaled.width * original.height != (long)upscaled.height * original.width)
                throw new InvalidOperationException("The enhanced image must be larger and preserve the original aspect ratio.");
            diagnostic = DiagnosticTexture(embedded);
            Material embeddedMaterial = VariantMaterial(harborMaterial, embedded, temporaryMaterials);
            Material originalMaterial = VariantMaterial(harborMaterial, original, temporaryMaterials);
            Material enhancedMaterial = VariantMaterial(harborMaterial, upscaled, temporaryMaterials);
            Material diagnosticMaterial = VariantMaterial(harborMaterial, diagnostic, temporaryMaterials);
            // Borrow the persistent asset; only its temporary material wrapper is owned.
            Texture2D imported = string.IsNullOrWhiteSpace(ImportedTexturePath) ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(ImportedTexturePath);
            Material importedMaterial = imported ? VariantMaterial(harborMaterial, imported, temporaryMaterials) : null;
            foreach (Slot slot in slots)
                if (slot.renderer is MeshRenderer mesh && !gpuFlags.ContainsKey(mesh))
                {
                    // Core accesses this through an internal/friend API in some Unity
                    // versions. Resolve it optionally; material clones work independently.
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    PropertyInfo property = typeof(MeshRenderer).GetProperty("allowGPUDrivenRendering", flags)
                        ?? typeof(Renderer).GetProperty("allowGPUDrivenRendering", flags);
                    var saved = new GpuFlag();
                    gpuFlags.Add(mesh, saved);
                    if (property != null && property.PropertyType == typeof(bool) && property.CanRead && property.CanWrite)
                    {
                        try
                        {
                            saved.original = (bool)property.GetValue(mesh);
                            saved.property = property;
                            property.SetValue(mesh, false);
                        }
                        catch (Exception exception)
                        {
                            report.AppendLine("GPU-driven opt-out unavailable for " + mesh.name + ": " + exception.Message);
                        }
                    }
                    else report.AppendLine("GPU-driven opt-out property absent; material-clone binding and diagnostic remain active.");
                }

            cameraObject = new GameObject("Temporary dock texture A/B camera") { hideFlags = HideFlags.HideAndDontSave };
            captureCamera = cameraObject.AddComponent<Camera>();
            captureCamera.CopyFrom(sourceCamera);
            captureCamera.enabled = false;
            captureCamera.tag = "Untagged";
            captureCamera.rect = new Rect(0f, 0f, 1f, 1f);
            captureCamera.aspect = (float)Width / Height;
            captureCamera.orthographic = true;
            captureCamera.allowDynamicResolution = false;
            captureCamera.useOcclusionCulling = false;
            captureCamera.nearClipPlane = .1f;
            captureCamera.farClipPlane = 200f;
            UniversalAdditionalCameraData sourceData = sourceCamera.GetComponent<UniversalAdditionalCameraData>();
            UniversalAdditionalCameraData captureData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            if (sourceData) EditorUtility.CopySerialized(sourceData, captureData);
            captureData.renderType = CameraRenderType.Base;
            if (captureData.cameraStack != null) captureData.cameraStack.Clear();
            captureData.allowXRRendering = false;
            // Keep volume selection identical to the real camera while framing different
            // detail views; the current scene uses global effects but this also handles locals.
            captureData.volumeTrigger = sourceData && sourceData.volumeTrigger ? sourceData.volumeTrigger : sourceCamera.transform;

            int requestedMsaa = 1;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (captureCamera.allowMSAA) requestedMsaa = urp ? urp.msaaSampleCount : Mathf.Max(1, QualitySettings.antiAliasing);
            var descriptor = new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24)
            {
                msaaSamples = requestedMsaa,
                useMipMap = false,
                autoGenerateMips = false,
                useDynamicScale = false,
                sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear
            };
            descriptor.msaaSamples = Mathf.Max(1, SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor));
            target = new RenderTexture(descriptor) { name = "Temporary dock A/B render", hideFlags = HideFlags.HideAndDontSave };
            target.Create();
            if (descriptor.msaaSamples > 1)
            {
                descriptor.msaaSamples = 1; descriptor.depthBufferBits = 0;
                resolved = new RenderTexture(descriptor) { name = "Temporary dock A/B resolve", hideFlags = HideFlags.HideAndDontSave };
                resolved.Create();
            }
            captureCamera.targetTexture = target;

            report.AppendLine("Dock texture clarity capture — " + DateTime.Now.ToString("s"));
            report.AppendLine("Original: " + Path.GetFullPath(OriginalPng));
            report.AppendLine("Enhanced: " + Path.GetFullPath(UpscaledPng));
            report.AppendLine("Embedded " + embedded.width + "x" + embedded.height + "; external " + original.width + "x" + original.height
                + "; enhanced " + upscaled.width + "x" + upscaled.height + "; GPU format " + upscaled.graphicsFormat);
            report.AppendLine("Baseline GPU formats: embedded=" + embedded.graphicsFormat + "; external=" + original.graphicsFormat
                + "; mip counts=" + embedded.mipmapCount + "/" + original.mipmapCount + "/" + upscaled.mipmapCount);
            if (imported)
                report.AppendLine("Imported asset: " + ImportedTexturePath + "; " + imported.width + "x" + imported.height
                    + "; GPU format=" + imported.graphicsFormat + "; mips=" + imported.mipmapCount
                    + "; filter=" + imported.filterMode + "; aniso=" + imported.anisoLevel + "; mip bias=" + imported.mipMapBias
                    + ". Raw/imported difference includes the imported filtering settings as well as compression.");
            else report.AppendLine("Imported variant skipped: " + (string.IsNullOrWhiteSpace(ImportedTexturePath) ? "disabled" : "asset not found at " + ImportedTexturePath));
            report.AppendLine("PNG load: sRGB, no Y flip, mipmaps on; original filter/wrap/aniso copied: "
                + original.filterMode + "/" + original.wrapModeU + "/" + original.wrapModeV + "/" + original.anisoLevel);
            report.AppendLine("Capture " + Width + "x" + Height + "; output MSAA=" + target.antiAliasing
                + "; inherited post=" + captureData.renderPostProcessing + "; AA=" + captureData.antialiasing + "/" + captureData.antialiasingQuality
                + "; active URP scale=" + (urp ? urp.renderScale.ToString("F2") : "built-in"));
            report.AppendLine("No HUD/overlay camera stack; current scene lighting and camera volume selection retained.");
            report.AppendLine("Binding: temporary material clones + matching MPBs; optional GPU-driven opt-out resolved by reflection where available.");
            report.AppendLine("Color: active space=" + QualitySettings.activeColorSpace + "; output RT sRGB=" + target.sRGB
                + "; PNG readback texture uses sRGB (linear=false), no manual gamma transform.");

            bool equivalent = true;
            foreach (Shot shot in Shots(model, bounds))
            {
                ConfigureShot(captureCamera, shot);
                string prefix = Path.Combine(folder, "clarity_" + shot.name + "_");
                Color32[] embeddedPixels = RenderVariant(captureCamera, target, resolved, slots, embeddedMaterial, prefix + "0_embedded.png");
                Color32[] diagnosticPixels = RenderVariant(captureCamera, target, resolved, slots, diagnosticMaterial, prefix + "diagnostic_red_magenta.png");
                double diagnosticError = MeanAbsoluteRgb(embeddedPixels, diagnosticPixels);
                double diagnosticChange = DifferenceFraction(embeddedPixels, diagnosticPixels, 8);
                bool bindingPass = diagnosticError > 1d && diagnosticChange > .002d;
                report.AppendLine(shot.name + ": red/magenta diagnostic MAE=" + diagnosticError.ToString("F4")
                    + "/255; pixels differing >8=" + (diagnosticChange * 100).ToString("F3") + "%; binding=" + (bindingPass ? "PASS" : "FAIL"));
                if (!bindingPass)
                    throw new InvalidOperationException("Texture replacement is not visibly reaching the " + shot.name
                        + " render. Diagnostic must alter the harbor before any A/B equivalence claim is valid.");
                Color32[] originalPixels = RenderVariant(captureCamera, target, resolved, slots, originalMaterial, prefix + "1_original_4k.png");
                Color32[] enhancedPixels = RenderVariant(captureCamera, target, resolved, slots, enhancedMaterial, prefix + "2_realesrgan.png");
                double identityError = MeanAbsoluteRgb(embeddedPixels, originalPixels);
                double largeDifference = DifferenceFraction(embeddedPixels, originalPixels, 8);
                bool identityPass = identityError <= 2d && largeDifference <= .01d;
                equivalent &= identityPass;
                report.AppendLine(shot.name + ": external-original vs embedded MAE=" + identityError.ToString("F4")
                    + "/255; pixels differing >8=" + (largeDifference * 100).ToString("F3") + "%; identity=" + (identityPass ? "PASS" : "REVIEW"));
                report.AppendLine(shot.name + ": enhanced vs original MAE=" + MeanAbsoluteRgb(originalPixels, enhancedPixels).ToString("F4")
                    + "/255; inspect wood grain, shop doorway, roof edges and UV seams in the three matching PNGs.");
                if (importedMaterial)
                {
                    Color32[] importedPixels = RenderVariant(captureCamera, target, resolved, slots, importedMaterial, prefix + "3_imported_8k.png");
                    report.AppendLine(shot.name + ": imported 8K vs raw 8K MAE=" + MeanAbsoluteRgb(enhancedPixels, importedPixels).ToString("F4")
                        + "/255; max absolute RGB difference=" + MaxAbsoluteRgb(enhancedPixels, importedPixels) + "/255"
                        + "; pixels differing >8=" + (DifferenceFraction(enhancedPixels, importedPixels, 8) * 100).ToString("F3") + "%.");
                }
            }
            report.AppendLine(equivalent
                ? "PASS: external original renders like the GLB original; no large orientation/gamma mismatch detected."
                : "REVIEW: external original differs from the GLB baseline. Resolve UV orientation/color/import differences before judging enhancement.");
            report.AppendLine("Upscale quality is a visual decision; larger dimensions or more high-frequency detail alone do not prove improvement.");
            if (!witness.Unchanged(sourceCamera)
                || (game && (stateBefore != JsonUtility.ToJson(game.State) || pausedBefore != game.paused
                    || actionsBefore != game.ActionCount || undoBefore != game.UndoCount))
                || materialBefore != EditorJsonUtility.ToJson(harborMaterial))
                throw new InvalidOperationException("A source camera, gameplay or material witness changed during synchronous capture; inspect the cleanup report.");
            completed = true;
        }
        catch (Exception exception)
        {
            report.AppendLine("ERROR: " + exception);
            throw;
        }
        finally
        {
            foreach (Slot slot in slots) slot.Restore();
            foreach (var pair in gpuFlags)
                if (pair.Key && pair.Value.property != null)
                {
                    try { pair.Value.property.SetValue(pair.Key, pair.Value.original); }
                    catch (Exception exception) { report.AppendLine("ERROR restoring GPU-driven flag: " + exception); }
                }
            RenderTexture.active = previousActive;
            if (captureCamera) captureCamera.targetTexture = null;
            if (cameraObject) UnityEngine.Object.DestroyImmediate(cameraObject);
            if (target) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (resolved) { resolved.Release(); UnityEngine.Object.DestroyImmediate(resolved); }
            foreach (Material material in temporaryMaterials) if (material) UnityEngine.Object.DestroyImmediate(material);
            if (original) UnityEngine.Object.DestroyImmediate(original);
            if (upscaled) UnityEngine.Object.DestroyImmediate(upscaled);
            if (diagnostic) UnityEngine.Object.DestroyImmediate(diagnostic);
            bool stateSame = !game || (stateBefore == JsonUtility.ToJson(game.State) && pausedBefore == game.paused
                && actionsBefore == game.ActionCount && undoBefore == game.UndoCount);
            bool materialSame = !harborMaterial || materialBefore == EditorJsonUtility.ToJson(harborMaterial);
            report.AppendLine("Cleanup: material slots/MPBs/GPU-driven flags restored; temporary materials/cameras/textures destroyed; source camera unchanged=" + witness.Unchanged(sourceCamera)
                + "; material asset unchanged=" + materialSame + "; gameplay unchanged=" + stateSame + "; frame delta=" + (Time.frameCount - frameBefore));
            report.AppendLine("Completed=" + completed + "; this tool does not save materials, prefabs, scenes or gameplay state.");
            running = false;
            if (Directory.Exists(folder)) File.WriteAllText(Path.Combine(folder, "clarity-checks.txt"), report.ToString());
        }
        return report.ToString();
    }

    static Material VariantMaterial(Material source, Texture texture, List<Material> owned)
    {
        var material = new Material(source) { name = "Temporary dock A/B: " + texture.name, hideFlags = HideFlags.HideAndDontSave };
        owned.Add(material);
        material.SetTexture(BaseColor, texture);
        return material;
    }

    static Texture2D DiagnosticTexture(Texture2D reference)
    {
        var texture = new Texture2D(8, 8, TextureFormat.RGBA32, true, false)
            { name = "Texture-binding diagnostic red-magenta", hideFlags = HideFlags.HideAndDontSave };
        Color32[] pixels = new Color32[64];
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                pixels[y * 8 + x] = ((x + y) & 1) == 0 ? new Color32(255, 0, 0, 255) : new Color32(255, 0, 255, 255);
        texture.SetPixels32(pixels);
        texture.Apply(true, true);
        texture.filterMode = reference.filterMode;
        texture.wrapModeU = reference.wrapModeU; texture.wrapModeV = reference.wrapModeV;
        return texture;
    }

    static Texture2D LoadTexture(string path, Texture2D reference, string name)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, false) { name = name, hideFlags = HideFlags.HideAndDontSave };
        try
        {
            // LoadImage keeps PNG row orientation. Both the imported mesh and the
            // source PNG already follow the glTFast conversion; never flip here.
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), true)) throw new InvalidOperationException("PNG decode failed: " + path);
            texture.filterMode = reference.filterMode;
            texture.wrapModeU = reference.wrapModeU; texture.wrapModeV = reference.wrapModeV;
            texture.anisoLevel = reference.anisoLevel; texture.mipMapBias = reference.mipMapBias;
            return texture;
        }
        catch { UnityEngine.Object.DestroyImmediate(texture); throw; }
    }

    static Transform FindModelRoot(Transform current)
    {
        for (Transform candidate = current; candidate; candidate = candidate.parent)
            if (candidate.name == "Wooden harbor on island map" || candidate.name == "Hona wooden harbor • original textured model") return candidate;
        if (current.name == "Original harbor mesh" && current.parent) return current.parent;
        throw new InvalidOperationException("Cannot identify the 24 m normalized Harbor prefab root for detail framing.");
    }

    static IEnumerable<Shot> Shots(Transform model, Bounds bounds)
    {
        Quaternion overviewRotation = model.rotation * Quaternion.Euler(48f, -25f, 0f);
        Vector3 right = overviewRotation * Vector3.right, up = overviewRotation * Vector3.up;
        float extentX = ProjectedExtent(right, bounds.extents), extentY = ProjectedExtent(up, bounds.extents);
        yield return new Shot { name = "overview", center = bounds.center, rotation = overviewRotation,
            size = Mathf.Max(extentY, extentX / ((float)Width / Height)) * 1.08f };
        float scale = Mathf.Abs(model.lossyScale.x);
        yield return new Shot { name = "deck", center = model.TransformPoint(HabitatImportedDockLayout.ShopWaypoint),
            rotation = model.rotation * Quaternion.Euler(55f, -15f, 0f), size = 1.25f * scale };
        yield return new Shot { name = "shop", center = model.TransformPoint(HabitatImportedDockLayout.FromSource(-.29f, .015f, .065f)),
            rotation = model.rotation * Quaternion.Euler(24f, -8f, 0f), size = 1.85f * scale };
    }

    static float ProjectedExtent(Vector3 axis, Vector3 extent)
        => Mathf.Abs(axis.x) * extent.x + Mathf.Abs(axis.y) * extent.y + Mathf.Abs(axis.z) * extent.z;

    static void ConfigureShot(Camera camera, Shot shot)
    {
        camera.transform.SetPositionAndRotation(shot.center - shot.rotation * Vector3.forward * 60f, shot.rotation);
        camera.orthographicSize = shot.size;
        camera.ResetProjectionMatrix();
    }

    static Color32[] RenderVariant(Camera camera, RenderTexture target, RenderTexture resolved, List<Slot> slots, Material material, string path)
    {
        foreach (Slot slot in slots) slot.Set(material);
        if (GraphicsSettings.currentRenderPipeline)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("The active SRP does not support a URP SingleCameraRequest.");
            RenderPipeline.SubmitRenderRequest(camera, request);
        }
        else camera.Render();
        if (resolved) target.ResolveAntiAliasedSurface(resolved);
        RenderTexture previous = RenderTexture.active;
        Texture2D readback = null;
        try
        {
            RenderTexture.active = resolved ? resolved : target;
            readback = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false) { hideFlags = HideFlags.HideAndDontSave };
            readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            readback.Apply(false, false);
            Color32[] pixels = readback.GetPixels32();
            File.WriteAllBytes(path, readback.EncodeToPNG());
            return pixels;
        }
        finally
        {
            RenderTexture.active = previous;
            if (readback) UnityEngine.Object.DestroyImmediate(readback);
            // Restore before the next variant as well as at the outer cleanup boundary.
            foreach (Slot slot in slots) slot.Restore();
        }
    }

    static double MeanAbsoluteRgb(Color32[] a, Color32[] b)
    {
        long sum = 0;
        for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
        return sum / (a.Length * 3d);
    }

    static double DifferenceFraction(Color32[] a, Color32[] b, int threshold)
    {
        int changed = 0;
        for (int i = 0; i < a.Length; i++)
            if (Math.Abs(a[i].r - b[i].r) > threshold || Math.Abs(a[i].g - b[i].g) > threshold || Math.Abs(a[i].b - b[i].b) > threshold) changed++;
        return (double)changed / a.Length;
    }

    static int MaxAbsoluteRgb(Color32[] a, Color32[] b)
    {
        int maximum = 0;
        for (int i = 0; i < a.Length; i++)
            maximum = Math.Max(maximum, Math.Max(Math.Abs(a[i].r - b[i].r),
                Math.Max(Math.Abs(a[i].g - b[i].g), Math.Abs(a[i].b - b[i].b))));
        return maximum;
    }
}
