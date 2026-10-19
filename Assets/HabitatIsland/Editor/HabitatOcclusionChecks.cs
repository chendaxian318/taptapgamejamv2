using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace HabitatIsland.Editor
{
    /// <summary>Run in Play mode; the live state, undo stack and view are restored afterwards.</summary>
    public static class HabitatOcclusionChecks
    {
        static int count;
        static StringBuilder report;

        sealed class RendererSnapshot
        {
            public Renderer renderer;
            public Material[] materials;
            public ShadowCastingMode shadows;
        }

        sealed class MaterialSnapshot
        {
            public Material material;
            public Color color;
            public int queue;
            public float surface;
        }

        static void Check(bool condition, string label)
        {
            count++;
            if (!condition) throw new InvalidOperationException("Occlusion check failed: " + label);
            report.AppendLine("PASS " + count + ": " + label);
        }

        static HabitatState Fixture(HabitatState original, Vector2Int rear)
        {
            HabitatState state = HabitatRules.Clone(original);
            state.width = state.depth = 7;
            state.tiles = new Tile[49];
            for (int i = 0; i < state.tiles.Length; i++) state.tiles[i] = new Tile { height = -1 };
            HabitatRules.TileAt(state, 3, 3).height = 3;
            HabitatRules.TileAt(state, rear.x, rear.y).height = 1;
            state.animals.Clear();
            state.playerX = rear.x; state.playerZ = rear.y;
            state.selectedX = rear.x; state.selectedZ = rear.y;
            state.carryingAnimal = -1; state.carryingProp = PropKind.None;
            state.waterLevel = state.tideBaseLevel = 0; state.tideActive = false;
            state.dockX = state.dockZ = 0;
            return state;
        }

        static Transform CellRoot(HabitatView view, int x, int z)
        {
            foreach (GridHit hit in view.GetComponentsInChildren<GridHit>())
                if (hit.x == x && hit.z == z) return hit.transform.parent;
            throw new InvalidOperationException("No active visual for cell " + x + ", " + z);
        }

        static List<RendererSnapshot> Capture(Transform terrain, out List<MaterialSnapshot> sources)
        {
            var renderers = new List<RendererSnapshot>();
            var seen = new HashSet<Material>();
            sources = new List<MaterialSnapshot>();
            foreach (Renderer renderer in terrain.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                Material[] materials = renderer.sharedMaterials;
                renderers.Add(new RendererSnapshot { renderer = renderer, materials = materials, shadows = renderer.shadowCastingMode });
                foreach (Material material in materials)
                {
                    if (!material || !seen.Add(material) || !material.HasProperty("_BaseColor")) continue;
                    sources.Add(new MaterialSnapshot { material = material, color = material.GetColor("_BaseColor"),
                        queue = material.renderQueue, surface = material.HasProperty("_Surface") ? material.GetFloat("_Surface") : -1 });
                }
            }
            return renderers;
        }

        static bool SameMaterials(RendererSnapshot snapshot)
        {
            Material[] current = snapshot.renderer.sharedMaterials;
            if (current.Length != snapshot.materials.Length) return false;
            for (int i = 0; i < current.Length; i++) if (current[i] != snapshot.materials[i]) return false;
            return true;
        }

        static void CheckSources(List<MaterialSnapshot> sources)
        {
            bool intact = sources.Count > 0;
            foreach (MaterialSnapshot snapshot in sources)
                intact &= snapshot.material && snapshot.material.GetColor("_BaseColor") == snapshot.color
                    && snapshot.material.renderQueue == snapshot.queue
                    && (snapshot.surface < 0 || snapshot.material.GetFloat("_Surface") == snapshot.surface);
            Check(intact, "source material colors, render queues and opaque surfaces are unchanged");
        }

        static void CheckRestored(List<RendererSnapshot> renderers)
        {
            bool intact = renderers.Count > 0;
            foreach (RendererSnapshot snapshot in renderers)
                intact &= snapshot.renderer && SameMaterials(snapshot) && snapshot.renderer.shadowCastingMode == snapshot.shadows;
            Check(intact, "all original renderer material slots and shadow modes restored");
        }

        static void CheckSingleDock(HabitatView view, string label)
        {
            int activeDocks = 0;
            foreach (Transform child in view.GetComponentsInChildren<Transform>())
                if (child.name == "Trading dock" && child.gameObject.activeInHierarchy) activeDocks++;
            Check(activeDocks == 1, label + " (active docks: " + activeDocks + ")");
        }

        // Physics supplies an independent witness that both pick colliders really overlap
        // at this pixel. This avoids asserting Alt behavior on a side-wall-only overlap.
        static bool FindOverlap(Camera camera, Transform rearRoot, Vector2Int rear, out Vector2 screen)
        {
            Collider rearCollider = rearRoot.GetComponentInChildren<GridHit>().GetComponent<Collider>();
            Physics.SyncTransforms();
            for (int z = -2; z <= 2; z++) for (int x = -2; x <= 2; x++)
            {
                Vector3 point = rearCollider.bounds.center;
                point.y = rearCollider.bounds.max.y + .001f;
                point.x += x * .12f; point.z += z * .12f;
                screen = camera.WorldToScreenPoint(point);
                float frontDistance = float.PositiveInfinity, rearDistance = float.PositiveInfinity;
                foreach (RaycastHit hit in Physics.RaycastAll(camera.ScreenPointToRay(screen), 200f))
                {
                    GridHit cell = hit.collider.GetComponent<GridHit>();
                    if (!cell) continue;
                    // A lower camera can hit the .24-high pick collider's side
                    // without crossing its top. Require upward normals so this
                    // is an independent witness of two visible top surfaces.
                    if (Vector3.Dot(hit.normal, Vector3.up) < .95f) continue;
                    if (cell.x == 3 && cell.z == 3) frontDistance = Mathf.Min(frontDistance, hit.distance);
                    if (cell.x == rear.x && cell.z == rear.y) rearDistance = Mathf.Min(rearDistance, hit.distance);
                }
                if (!float.IsInfinity(rearDistance) && frontDistance < rearDistance) return true;
            }
            screen = default;
            return false;
        }

        static void Present(HabitatGame game, HabitatOcclusionFade fade)
        {
            game.View.Render(game.State, game.tuning, false);
            fade.Refresh(.4f);
        }

        static float CameraHeading(HabitatView view)
        {
            Vector3 forward = view.GameCamera.transform.forward;
            return Mathf.Atan2(-forward.x, forward.z) * Mathf.Rad2Deg;
        }

        static void SetHeading(HabitatGame game, float desired)
        {
            // Initialize now resets camera yaw for each level. Read the current
            // camera instead of carrying a heading across fixture replacements.
            game.View.RotateCamera(Mathf.DeltaAngle(CameraHeading(game.View), desired));
            game.View.Render(game.State, game.tuning, false);
        }

        public static string Run()
        {
            if (!Application.isPlaying || !HabitatGame.Instance || !HabitatGame.Instance.View)
                throw new InvalidOperationException("Enter Play mode in the Habitat Island scene before running occlusion checks.");

            HabitatGame game = HabitatGame.Instance;
            HabitatView view = game.View;
            HabitatOcclusionFade fade = view.GetComponent<HabitatOcclusionFade>();
            if (!fade || !view.GameCamera) throw new InvalidOperationException("Terrain occlusion and camera must be initialized.");
            HabitatState original = game.State;
            bool paused = game.paused, fadeEnabled = fade.FadeEnabled, componentEnabled = fade.enabled;
            int undoCount = game.UndoCount;
            float originalHeading = CameraHeading(view);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/occlusion-checks.txt"));
            Exception failure = null;
            count = 0;
            report = new StringBuilder("Habitat Island terrain occlusion integration checks\nUTC: " + DateTimeOffset.UtcNow.ToString("O") + "\n");
            Vector2Int[] rears = { new Vector2Int(3, 4), new Vector2Int(2, 3), new Vector2Int(3, 2), new Vector2Int(4, 3) };
            try
            {
                game.paused = true;
                fade.enabled = true;
                for (int direction = 0; direction < 4; direction++)
                {
                    Vector2Int rear = rears[direction];
                    report.AppendLine("Camera heading: " + direction * 90 + " degrees");
                    fade.SetEnabled(false);
                    game.State = Fixture(original, rear);
                    view.Initialize(game.State);
                    SetHeading(game, direction * 90);
                    Check(Mathf.Abs(Mathf.DeltaAngle(CameraHeading(view), direction * 90)) < .05f,
                        "fixture camera matches the requested cardinal heading");
                    Present(game, fade);
                    Transform frontRoot = CellRoot(view, 3, 3);
                    var renderers = Capture(frontRoot.parent, out List<MaterialSnapshot> sources);
                    fade.SetEnabled(true);
                    fade.Refresh(.4f);
                    Check(fade.RegisteredCellCount == 49, "all current cells registered after state replacement");
                    Check(fade.IsCellFaded(3, 3) && !fade.IsCellFaded(rear.x, rear.y) && fade.FadedRendererCount > 0,
                        "blocking high terrain fades while the lower rear cell stays opaque");
                    CheckSources(sources);
                    bool supportPresent = false, supportSolid = true, transparentPresent = false;
                    foreach (RendererSnapshot snapshot in renderers)
                    {
                        if (!snapshot.renderer.transform.IsChildOf(frontRoot)) continue;
                        if (snapshot.renderer.bounds.max.y < .8f)
                        { supportPresent = true; supportSolid &= SameMaterials(snapshot); }
                        if (!SameMaterials(snapshot))
                        {
                            var block = new MaterialPropertyBlock();
                            snapshot.renderer.GetPropertyBlock(block, 0);
                            transparentPresent |= block.GetColor("_BaseColor").a >= .75f && block.GetColor("_BaseColor").a < .85f
                                && snapshot.renderer.shadowCastingMode == ShadowCastingMode.Off;
                        }
                    }
                    Check(supportPresent && supportSolid && transparentPresent,
                        "upper courses have transparent color and no shadow; supporting courses stay solid");
                    Check(FindOverlap(view.GameCamera, CellRoot(view, rear.x, rear.y), rear, out Vector2 pixel),
                        "physics confirms a screen pixel overlaps both front and rear top colliders");
                    Check(fade.TryPick(view.GameCamera, pixel, false, out int x, out int z) && x == rear.x && z == rear.y,
                        "normal picking reaches lower terrain through the faded foreground");
                    Check(fade.TryPick(view.GameCamera, pixel, true, out x, out z) && x == 3 && z == 3,
                        "Alt picking retains access to the nearest high terrain");
                    fade.SetEnabled(false);
                    Check(fade.FadedRendererCount == 0 && !fade.IsCellFaded(3, 3), "disabling transparency restores immediately");
                    CheckRestored(renderers);
                    Check(fade.TryPick(view.GameCamera, pixel, false, out x, out z) && x == 3 && z == 3,
                        "with transparency disabled, normal picking returns to the foreground");
                    fade.SetEnabled(true);
                    fade.Refresh(.4f);
                    if (direction == 0)
                    {
                        fade.enabled = false;
                        Check(fade.FadedRendererCount == 0 && !fade.IsCellFaded(3, 3),
                            "disabling the component restores visible terrain immediately");
                        CheckRestored(renderers);
                        fade.enabled = true;
                        fade.Refresh(.4f);
                        Check(fade.IsCellFaded(3, 3) && fade.FadedRendererCount > 0,
                            "reenabling the component resumes occlusion detection and fading");
                    }
                    SetHeading(game, direction * 90 + 180);
                    fade.Refresh(.4f);
                    Check(!fade.IsCellFaded(3, 3) && fade.FadedRendererCount == 0,
                        "rotating to the unobstructed side restores terrain while the simulation is paused");
                    CheckRestored(renderers);
                }

                game.State = Fixture(original, rears[0]);
                view.Initialize(game.State);
                SetHeading(game, 0);
                Present(game, fade);
                game.Step();
                HabitatRules.TileAt(game.State, 3, 3).height = 1;
                Present(game, fade);
                Check(!fade.IsCellFaded(3, 3) && fade.RegisteredCellCount == 49,
                    "lowering terrain rebuilds and removes obsolete faded renderers");
                game.Undo();
                Present(game, fade);
                Check(game.UndoCount == undoCount && HabitatRules.TileAt(game.State, 3, 3).height == 3 && fade.IsCellFaded(3, 3),
                    "controller Undo replaces the state and restores high-terrain occlusion");
                game.State.waterLevel = 1;
                Present(game, fade);
                Check(fade.RegisteredCellCount == 49 && fade.IsCellFaded(3, 3),
                    "rising tide rebuilds the submerged rear cell and retains occlusion");
                game.State.waterLevel = 0;
                Present(game, fade);
                Check(fade.RegisteredCellCount == 49 && fade.IsCellFaded(3, 3),
                    "falling tide restores land without stale registrations");
                CheckSingleDock(view, "same-frame tide rebuilds leave exactly one active dock");
            }
            catch (Exception error) { failure = error; report.AppendLine("FAIL: " + error); }
            finally
            {
                try
                {
                    while (game.UndoCount > undoCount) game.Undo();
                    game.State = original;
                    fade.SetEnabled(false);
                    view.Initialize(original);
                    SetHeading(game, originalHeading);
                    view.Render(original, game.tuning, game.overlays);
                    fade.SetEnabled(fadeEnabled);
                    fade.enabled = componentEnabled;
                    if (componentEnabled) fade.Refresh(.4f);
                    game.paused = paused;
                    if (game.Hud) game.Hud.Refresh();
                    CheckSingleDock(view, "cleanup leaves exactly one active dock for the restored state");
                    report.AppendLine("Cleanup: original state, selection, pause, camera heading, fade setting, component enabled state and undo depth restored.");
                }
                catch (Exception error)
                {
                    game.paused = paused;
                    report.AppendLine("Cleanup failed: " + error);
                    if (failure == null) failure = error;
                }
                report.AppendLine((failure == null ? "PASS: " : "FAILED after ") + count + " occlusion integration assertions.");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, report.ToString());
            }
            if (failure != null) throw new InvalidOperationException("Habitat occlusion checks failed; see " + path, failure);
            string result = "PASS: " + count + " occlusion integration assertions. Report: " + path;
            Debug.Log("[HabitatIsland] " + result);
            return result;
        }
    }
}
