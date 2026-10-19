using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace HabitatIsland
{
    // Visual-only terrain cutaway. Detection uses mesh bounds because the terrain
    // intentionally has only thin, invisible picking colliders at its top surface.
    [DisallowMultipleComponent]
    public sealed class HabitatOcclusionFade : MonoBehaviour
    {
        const float Step = .7f;
        const float FadedAlpha = .78f;
        const float PickThroughAlpha = (1f + FadedAlpha) * .5f;
        const float FadeSeconds = .22f;
        const float HoldSeconds = .08f;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        readonly Dictionary<Vector2Int, Cell> cells = new Dictionary<Vector2Int, Cell>();
        readonly Dictionary<Material, Material> transparentMaterials = new Dictionary<Material, Material>();
        readonly List<Vector2Int> staleCells = new List<Vector2Int>();
        readonly List<Cell> activeCells = new List<Cell>();
        HabitatView view;
        HabitatGame game;
        Transform terrainRoot;
        HabitatState registeredState;
        float fadeClock;

        public bool FadeEnabled { get; private set; } = true;
        public int FadedRendererCount { get; private set; }
        public int RegisteredCellCount => cells.Count;

        sealed class Cell
        {
            public Transform root;
            public int x, z;
            public float surface;
            public Bounds bounds;
            public bool hasBounds;
            public float fadeFromY = float.PositiveInfinity;
            public readonly List<Part> parts = new List<Part>();
            public readonly List<Renderer> opaqueClimbParts = new List<Renderer>();
        }

        sealed class Part
        {
            public Renderer renderer;
            public Material[] originals, transparent;
            public MaterialPropertyBlock[] originalBlocks, fadeBlocks;
            public Color[] colors;
            public bool[] canFade;
            public ShadowCastingMode shadows;
            public Bounds bounds;
            public float alpha = 1f, lastBlocked = float.NegativeInfinity;
            public float appliedAlpha = -1f;
            public bool blocked, swapped;
        }

        public void Initialize(HabitatView habitatView, Transform root)
        {
            if (terrainRoot && terrainRoot != root)
            {
                RestoreAll();
                cells.Clear();
            }
            view = habitatView;
            game = habitatView ? habitatView.GetComponent<HabitatGame>() : null;
            terrainRoot = root;
        }

        public void RegisterCell(Transform root, HabitatState state, int x, int z)
        {
            registeredState = state;
            Vector2Int key = new Vector2Int(x, z);
            if (cells.TryGetValue(key, out Cell old)) Restore(old);
            Cell cell = new Cell { root = root, x = x, z = z };
            cells[key] = cell;
            if (!root) return;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer is SpriteRenderer || IsOverlay(renderer.transform, root)) continue;
                // A climbable block is an interactive prop, not a terrain course.
                // Keep its grass cap, body and ladder opaque even when the ground
                // underneath is cut away to reveal a cell behind it.
                if (IsClimbBlockPart(renderer.transform, root))
                {
                    cell.opaqueClimbParts.Add(renderer);
                    continue;
                }
                Material[] originals = renderer.sharedMaterials;
                bool any = false;
                Part part = new Part
                {
                    renderer = renderer, originals = originals,
                    transparent = (Material[])originals.Clone(),
                    originalBlocks = new MaterialPropertyBlock[originals.Length],
                    fadeBlocks = new MaterialPropertyBlock[originals.Length],
                    colors = new Color[originals.Length], canFade = new bool[originals.Length],
                    shadows = renderer.shadowCastingMode
                };
                for (int i = 0; i < originals.Length; i++)
                {
                    Material source = originals[i];
                    // Only opaque URP materials participate. Existing transparent
                    // art, water and status overlays keep their own rendering rules.
                    bool supported = source && source.HasProperty("_Surface") && source.GetFloat("_Surface") < .5f
                        && source.HasProperty(BaseColorId) && source.HasProperty("_ZWrite");
                    part.canFade[i] = supported;
                    if (!supported) continue;
                    any = true;
                    part.colors[i] = source.GetColor(BaseColorId);
                    part.transparent[i] = GetTransparentMaterial(source);
                    part.originalBlocks[i] = new MaterialPropertyBlock();
                    part.fadeBlocks[i] = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(part.originalBlocks[i], i);
                    renderer.GetPropertyBlock(part.fadeBlocks[i], i);
                }
                if (any) cell.parts.Add(part);
            }
            UpdateSurface(cell, state);
        }

        static bool IsOverlay(Transform current, Transform root)
        {
            for (; current && current != root; current = current.parent)
                if (current.name == "Habitat overlay") return true;
            return false;
        }

        static bool IsClimbBlockPart(Transform current, Transform root)
        {
            // BuildProp names this direct child after PropKind; checking only the
            // prop root avoids exempting an unrelated nested imported mesh name.
            for (; current && current != root; current = current.parent)
                if (current.parent == root) return current.name == nameof(PropKind.ClimbBlock);
            return false;
        }

        Material GetTransparentMaterial(Material source)
        {
            if (transparentMaterials.TryGetValue(source, out Material material)) return material;
            material = new Material(source) { name = source.name + " (occlusion fade)", hideFlags = HideFlags.DontSave };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_SrcBlendAlpha")) material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (material.HasProperty("_DstBlendAlpha")) material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
            if (material.HasProperty("_AlphaToMask")) material.SetFloat("_AlphaToMask", 0f);
            if (material.HasProperty("_BlendModePreserveSpecular")) material.SetFloat("_BlendModePreserveSpecular", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("DepthNormals", false);
            material.SetShaderPassEnabled("DepthNormalsOnly", false);
            transparentMaterials.Add(source, material);
            return material;
        }

        public void SetEnabled(bool value)
        {
            FadeEnabled = value;
            if (!value) RestoreAll();
        }

        HabitatState CurrentState => game && game.State != null ? game.State : registeredState;

        void LateUpdate()
        {
            if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame
                && (!game || ((!game.Hud || !game.Hud.ModalOpen) && (!game.Fishing || !game.Fishing.IsOpen))))
                SetEnabled(!FadeEnabled);

            Refresh(Time.unscaledDeltaTime);
        }

        // Also used by editor checks to advance the visual transition without
        // depending on editor frame timing or the simulation's paused state.
        public void Refresh(float unscaledDeltaTime)
        {
            float dt = float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime) ? 0f : Mathf.Max(0f, unscaledDeltaTime);
            fadeClock += dt;
            HabitatState state = CurrentState;
            Camera camera = view ? view.GameCamera : null;
            if (state == null || !camera || !terrainRoot) return;
            PrepareCells(state);
            if (FadeEnabled)
            {
                foreach (Cell target in activeCells)
                {
                    if (!ShouldRevealCell(state, target.x, target.z)) continue;
                    Vector3 point = target.root.TransformPoint(new Vector3(0, target.surface + .018f, 0));
                    RevealPoint(camera, point, target.root.TransformPoint(new Vector3(0, target.surface, 0)).y, target.x, target.z);
                }
                if (!state.onHarbor) RevealActor(camera, state, state.playerX, state.playerZ, .55f);
                foreach (Animal animal in state.animals)
                    if (!animal.carried && (!state.authoredLevel || (state.selectedX == animal.x && state.selectedZ == animal.z)))
                        RevealActor(camera, state, animal.x, animal.z, .28f);
            }

            FadedRendererCount = 0;
            foreach (Cell cell in activeCells)
            foreach (Part part in cell.parts)
            {
                if (!part.renderer) continue;
                // Once a course hides a cell, fade the courses above it too.
                // Keeping a solid grass cap over a ghost middle course makes the
                // height hard to read and disagrees with click-through picking.
                if (FadeEnabled && cell.fadeFromY < float.PositiveInfinity
                    && part.bounds.min.y >= cell.fadeFromY - .025f) part.blocked = true;
                if (part.blocked) part.lastBlocked = fadeClock;
                bool fade = FadeEnabled && (part.blocked || fadeClock - part.lastBlocked < HoldSeconds);
                part.alpha = Mathf.MoveTowards(part.alpha, fade ? FadedAlpha : 1f, dt * (1f - FadedAlpha) / FadeSeconds);
                Apply(part);
                if (part.alpha < .999f) FadedRendererCount++;
            }
        }

        void PrepareCells(HabitatState state)
        {
            activeCells.Clear();
            staleCells.Clear();
            foreach (KeyValuePair<Vector2Int, Cell> pair in cells)
            {
                Cell cell = pair.Value;
                if (!cell.root || !cell.root.gameObject.activeInHierarchy || !HabitatRules.Inside(state, cell.x, cell.z))
                {
                    Restore(cell);
                    staleCells.Add(pair.Key);
                    continue;
                }
                UpdateSurface(cell, state);
                cell.hasBounds = false;
                cell.fadeFromY = float.PositiveInfinity;
                foreach (Part part in cell.parts)
                {
                    part.blocked = false;
                    if (!part.renderer || !part.renderer.enabled || !part.renderer.gameObject.activeInHierarchy) continue;
                    part.bounds = part.renderer.bounds;
                    if (cell.hasBounds) cell.bounds.Encapsulate(part.bounds);
                    else { cell.bounds = part.bounds; cell.hasBounds = true; }
                }
                activeCells.Add(cell);
            }
            foreach (Vector2Int key in staleCells) cells.Remove(key);
        }

        bool ShouldRevealCell(HabitatState state, int x, int z)
        {
            Tile tile = HabitatRules.TileAt(state, x, z);
            if (tile == null) return false;
            if (!state.authoredLevel) return tile.height >= 0;
            // Ordinary land, props and idle residents must not turn whole cliffs
            // into glass. Reveal only the player's current focus on the authored map.
            return (state.selectedX == x && state.selectedZ == z)
                || (!state.onHarbor && state.playerX == x && state.playerZ == z);
        }

        static float PickSurface(Cell cell, HabitatState state)
        {
            // Movable/standable blocks are clicked where their visible top is,
            // rather than on the terrain plane hidden underneath the block.
            return state == null ? cell.surface : Mathf.Max(cell.surface, HabitatRules.Height(state, cell.x, cell.z) * Step + .045f);
        }

        static void UpdateSurface(Cell cell, HabitatState state)
        {
            Tile tile = state == null ? null : HabitatRules.TileAt(state, cell.x, cell.z);
            if (tile != null) cell.surface = tile.height <= state.waterLevel ? HabitatRules.WaterSurfaceY(state.waterLevel) + .015f : tile.height * Step + .045f;
        }

        void RevealActor(Camera camera, HabitatState state, int x, int z, float bodyHeight)
        {
            if (!cells.TryGetValue(new Vector2Int(x, z), out Cell cell) || !cell.root) return;
            float feet = Mathf.Max(HabitatRules.Height(state, x, z) * Step + .055f, HabitatRules.WaterSurfaceY(state.waterLevel) + .027f);
            Vector3 point = cell.root.TransformPoint(new Vector3(0, feet + bodyHeight, 0));
            RevealPoint(camera, point, cell.root.TransformPoint(new Vector3(0, cell.surface, 0)).y, x, z);
        }

        void RevealPoint(Camera camera, Vector3 point, float floor, int targetX, int targetZ)
        {
            Vector3 screen = camera.WorldToScreenPoint(point);
            if (screen.z <= camera.nearClipPlane || !camera.pixelRect.Contains(new Vector2(screen.x, screen.y))) return;
            // ScreenPointToRay is important: orthographic rays are parallel and
            // do not originate at the camera's position.
            Ray ray = camera.ScreenPointToRay(screen);
            float targetDistance = Vector3.Dot(point - ray.origin, ray.direction);
            foreach (Cell cell in activeCells)
            {
                if ((cell.x == targetX && cell.z == targetZ) || !cell.hasBounds
                    || !cell.bounds.IntersectRay(ray, out float cellDistance) || cellDistance >= targetDistance - .04f) continue;
                foreach (Part part in cell.parts)
                {
                    if (!part.renderer || !part.renderer.enabled || !part.renderer.gameObject.activeInHierarchy || part.blocked) continue;
                    // Keep the courses supporting the target floor solid. The
                    // imported blocks are separate .7-high meshes, so their upper
                    // courses can fade without making the island foundation vanish.
                    if (part.bounds.max.y <= floor + .06f || part.bounds.min.y < floor - .075f) continue;
                    if (part.bounds.IntersectRay(ray, out float distance) && distance < targetDistance - .04f)
                    {
                        part.blocked = true;
                        cell.fadeFromY = Mathf.Min(cell.fadeFromY, part.bounds.min.y);
                    }
                }
            }
        }

        static void Apply(Part part)
        {
            if (part.alpha >= .999f) { Restore(part); return; }
            if (part.swapped && Mathf.Abs(part.appliedAlpha-part.alpha)<.00001f) return;
            Renderer renderer = part.renderer;
            if (!renderer) return;
            if (!part.swapped)
            {
                renderer.sharedMaterials = part.transparent;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                part.swapped = true;
            }
            for (int i = 0; i < part.canFade.Length; i++)
            {
                if (!part.canFade[i]) continue;
                Color color = part.colors[i];
                color.a *= part.alpha;
                part.fadeBlocks[i].SetColor(BaseColorId, color);
                if (part.originals[i] && part.originals[i].HasProperty(ColorId)) part.fadeBlocks[i].SetColor(ColorId, color);
                renderer.SetPropertyBlock(part.fadeBlocks[i], i);
            }
            part.appliedAlpha=part.alpha;
        }

        public bool IsCellFaded(int x, int z)
        {
            if (!isActiveAndEnabled || !FadeEnabled || !cells.TryGetValue(new Vector2Int(x, z), out Cell cell)) return false;
            foreach (Part part in cell.parts)
                if (part.renderer && part.alpha < PickThroughAlpha) return true;
            return false;
        }

        public bool IsCellTopFaded(int x, int z)
        {
            if (!isActiveAndEnabled || !FadeEnabled || !cells.TryGetValue(new Vector2Int(x, z), out Cell cell) || !cell.root) return false;
            foreach (Renderer renderer in cell.opaqueClimbParts)
                if (renderer && renderer.enabled && renderer.gameObject.activeInHierarchy) return false;
            float pickSurface = PickSurface(cell, CurrentState);
            float top = cell.root.TransformPoint(new Vector3(0, pickSurface, 0)).y;
            // Rounded crate/raft meshes finish slightly below the logical .7 step.
            float tolerance = pickSurface > cell.surface + .01f ? .12f : .025f;
            foreach (Part part in cell.parts)
                if (part.renderer && part.alpha < PickThroughAlpha && part.bounds.max.y >= top - tolerance && part.bounds.min.y <= top + .025f) return true;
            return false;
        }

        // Select the visible cell behind a faded top. Alt can explicitly choose
        // the nearest top instead, so high terrain remains fully interactive.
        public bool TryPick(Camera camera, Vector2 screen, bool preferFront, out int x, out int z)
        {
            x = z = -1;
            if (!camera) return false;
            Ray ray = camera.ScreenPointToRay(screen);
            Cell nearest = null, solid = null;
            float nearestDistance = float.PositiveInfinity, solidDistance = float.PositiveInfinity;
            HabitatState state = CurrentState;
            foreach (Cell cell in cells.Values)
            {
                if (!cell.root || !cell.root.gameObject.activeInHierarchy || state == null || !HabitatRules.Inside(state, cell.x, cell.z)) continue;
                UpdateSurface(cell, state);
                Vector3 top = cell.root.TransformPoint(new Vector3(0, PickSurface(cell, state), 0));
                Plane plane = new Plane(cell.root.up, top);
                float distance = float.PositiveInfinity;
                if (plane.Raycast(ray, out float planeDistance) && planeDistance > 0f)
                {
                    Vector3 local = cell.root.InverseTransformPoint(ray.GetPoint(planeDistance));
                    if (Mathf.Abs(local.x) <= .495f && Mathf.Abs(local.z) <= .495f) distance = planeDistance;
                }
                // The opaque body/ladder may be clicked below the top plane.
                // Use only this prop's renderer bounds, never its faded support.
                foreach (Renderer renderer in cell.opaqueClimbParts)
                    if (renderer && renderer.enabled && renderer.gameObject.activeInHierarchy
                        && renderer.bounds.IntersectRay(ray, out float propDistance) && propDistance > 0f)
                        distance = Mathf.Min(distance, propDistance);
                if (float.IsPositiveInfinity(distance)) continue;
                if (distance < nearestDistance) { nearest = cell; nearestDistance = distance; }
                if (!IsCellTopFaded(cell.x, cell.z) && distance < solidDistance) { solid = cell; solidDistance = distance; }
            }
            Cell result = preferFront ? nearest : solid ?? nearest;
            if (result == null) return false;
            x = result.x; z = result.z;
            return true;
        }

        static void Restore(Part part)
        {
            if (part.renderer && part.swapped)
            {
                part.renderer.sharedMaterials = part.originals;
                part.renderer.shadowCastingMode = part.shadows;
                for (int i = 0; i < part.canFade.Length; i++)
                    if (part.canFade[i]) part.renderer.SetPropertyBlock(part.originalBlocks[i], i);
            }
            part.swapped = false;
            part.alpha = 1f;
            part.appliedAlpha=-1f;
        }

        static void Restore(Cell cell)
        {
            foreach (Part part in cell.parts)
            {
                Restore(part);
                part.lastBlocked = float.NegativeInfinity;
                part.blocked = false;
            }
        }

        void RestoreAll()
        {
            foreach (Cell cell in cells.Values) Restore(cell);
            FadedRendererCount = 0;
        }

        void OnDisable() { RestoreAll(); }

        void OnDestroy()
        {
            RestoreAll();
            foreach (Material material in transparentMaterials.Values) if (material) Destroy(material);
            transparentMaterials.Clear();
            cells.Clear();
        }
    }
}
