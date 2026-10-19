using UnityEngine;

namespace HabitatIsland
{
    /// <summary>
    /// Free movement on the island's imported harbor. The state stores dock-local XZ,
    /// so replacing the presentation after a tide change preserves the walking position.
    /// Input, grid movement and rendering remain the caller's responsibility.
    /// </summary>
    public sealed class HabitatMapHarbor
    {
        const float WalkSpeed = 2.5f;
        const float PlayerRadius = .28f;
        const float FootOffset = .055f;
        const float MaxSweepStep = .06f;
        const float GridStep = .7f;
        const float ExitLocalZ = -.08f;

        Transform model, dock;
        MeshCollider[] surfaces = new MeshCollider[0];
        Vector3 shore, bridgeEnd;
        float bridgeWidth;
        bool landingOnly, homeHarbor;
        HabitatHomeIsland homeIsland;
        public bool LandingOnly => landingOnly;

        public bool Ready => model && dock && (landingOnly || surfaces.Length > 0);
        public Transform ModelTransform => model;

        // Timeline and explicit harbor return use the same measured deck as walking.
        public bool TryPlaceAt(HabitatState s, Vector3 normalizedPosition)
        {
            if (!Ready || s == null || !TryGround(model.TransformPoint(normalizedPosition), out Vector3 world)) return false;
            Vector3 local = dock.InverseTransformPoint(world);
            s.onHarbor = true; s.harborX = local.x; s.harborZ = local.z;
            s.playerX = s.dockX; s.playerZ = s.dockZ;
            s.selectedX = s.selectedZ = -1;
            SetFacing(s, model.TransformDirection(Vector3.back));
            foreach (var animal in s.animals)
                if (animal.carried) { animal.x = s.playerX; animal.z = s.playerZ; }
            return true;
        }

        /// <param name="model">The 24 m normalized prefab root, including its map scale.</param>
        /// <param name="shoreLocal">The shore-side plank top, in dock-local coordinates.</param>
        /// <param name="bridgeEndLocal">The deck-side plank top, in dock-local coordinates.</param>
        /// <param name="bridgeWidth">The plank width in dock-local units.</param>
        public void Bind(Transform model, Transform dock, Vector3 shoreLocal, Vector3 bridgeEndLocal, float bridgeWidth, bool landingOnly=false, bool homeHarbor=false)
        {
            this.landingOnly=landingOnly;this.homeHarbor=homeHarbor;
            this.model = model;
            homeIsland = homeHarbor && model ? model.GetComponentInChildren<HabitatHomeIsland>() : null;
            this.dock = dock;
            shore = shoreLocal;
            bridgeEnd = bridgeEndLocal;
            this.bridgeWidth = Finite(bridgeWidth) ? Mathf.Max(0f, bridgeWidth) : 0f;
            surfaces = model ? model.GetComponentsInChildren<MeshCollider>(true) : new MeshCollider[0];
            if(homeIsland)homeIsland.BindObstacles(surfaces);
            foreach (MeshCollider surface in surfaces)
            {
                if (!surface || !surface.sharedMesh) continue;
                // Direct Collider.Raycast still samples this layer; ordinary grid picking
                // uses Physics.DefaultRaycastLayers and will ignore the harbor mesh.
                // Physics.IgnoreRaycastLayer is a mask (4), not the layer index (2).
                surface.gameObject.layer = 2;
                surface.enabled = true;
            }
            // The caller has just positioned/scaled a new instance. Make its collider
            // pose available now, including while gameplay and fixed updates are paused.
            if (Ready) Physics.SyncTransforms();
        }

        public Vector3 LandingGuidePosition(HabitatState s)
        {
            if(!Ready)return GridPosition(s);
            Vector3 start=dock.TransformPoint(bridgeEnd),end=dock.TransformPoint(shore);
            Vector3 span=end-start;span.y=0f;
            Vector3 offset=WorldPosition(s)-start;offset.y=0f;
            float along=span.sqrMagnitude>.001f?Vector3.Dot(offset,span)/span.sqrMagnitude:1f;
            return (along<.12f&&offset.magnitude>.40f?start:end)+Vector3.up*.09f;
        }

        public Vector3 WorldPosition(HabitatState s)
        {
            if (s == null) return Vector3.zero;
            if (!s.onHarbor) return GridPosition(s);
            if (!Ready)
            {
                // An unavailable optional prefab must not strand a restored player.
                s.onHarbor = false;
                return GridPosition(s);
            }
            return ResolvePosition(s);
        }

        public Vector3 WorldFacing(HabitatState s)
        {
            if (s == null) return Vector3.forward;
            Vector3 facing = s.onHarbor
                ? new Vector3(s.harborFacingX, 0f, s.harborFacingZ)
                : new Vector3(s.facingX, 0f, s.facingZ);
            if (Finite(facing) && facing.sqrMagnitude > .000001f) return facing.normalized;
            return Ready && s.onHarbor ? Horizontal(dock.TransformDirection(Vector3.back)).normalized : Vector3.forward;
        }

        public bool CanEnter(HabitatState s, int dx, int dz)
        {
            if (!Ready || s == null || s.onHarbor || s.playerX != s.dockX || s.playerZ != s.dockZ) return false;
            if (!LandingAvailable(s)) return false;
            Vector3 request = new Vector3(dx, 0f, dz);
            if (request.sqrMagnitude < .000001f) return false;
            Vector3 outward = Horizontal(dock.TransformDirection(Vector3.back)).normalized;
            if (Vector3.Dot(request.normalized, outward) < .95f) return false;
            return TryGround(dock.TransformPoint(Vector3.zero), out _);
        }

        public bool TryEnter(HabitatState s, int dx, int dz)
        {
            if (!CanEnter(s, dx, dz)) return false;
            Vector3 outward = Horizontal(dock.TransformDirection(Vector3.back)).normalized;
            s.onHarbor = true;
            s.harborX = s.harborZ = 0f;
            s.selectedX = s.selectedZ = -1;
            s.message = "已走上码头";
            SetFacing(s, outward);
            return true;
        }

        public bool MoveByScreen(HabitatState s, Vector2 axes, Quaternion cameraRotation, float seconds)
        {
            if (!Finite(axes.x) || !Finite(axes.y)) return false;
            Vector3 right = Horizontal(cameraRotation * Vector3.right).normalized;
            Vector3 forward = Horizontal(cameraRotation * Vector3.forward).normalized;
            return MoveWorld(s, right * axes.x + forward * axes.y, seconds);
        }

        public bool MoveWorld(HabitatState s, Vector3 direction, float seconds)
        {
            if (!Ready || s == null || !s.onHarbor || !Finite(direction) || !Finite(seconds) || seconds <= 0f) return false;
            direction = Horizontal(direction);
            if (direction.sqrMagnitude < .000001f) return false;
            direction.Normalize();
            Vector3 position = ResolvePosition(s);
            if (!s.onHarbor) return true;
            Vector3 before = position;
            SetFacing(s, direction);
            bool towardShore = dock.InverseTransformDirection(direction).z > .5f;
            if (!homeHarbor && towardShore && s.harborZ >= ExitLocalZ) return TryExit(s, direction);

            Vector3 movement = direction * (WalkSpeed * Mathf.Min(seconds, .1f));
            int count = Mathf.Max(1, Mathf.CeilToInt(movement.magnitude / MaxSweepStep));
            Vector3 step = movement / count;
            for (int i = 0; i < count; i++)
            {
                if (TryGround(position + step, out Vector3 next)) position = next;
                else
                {
                    // Swept independent-axis sliding also keeps diagonal input from
                    // crossing a building corner or a narrow deck boundary in one frame.
                    if (Mathf.Abs(step.x) > .000001f && TryGround(position + new Vector3(step.x, 0f, 0f), out next)) position = next;
                    if (Mathf.Abs(step.z) > .000001f && TryGround(position + new Vector3(0f, 0f, step.z), out next)) position = next;
                }
                Vector3 local = dock.InverseTransformPoint(position);
                s.harborX = local.x;
                s.harborZ = local.z;
                if (!homeHarbor && towardShore && s.harborZ >= ExitLocalZ)
                    return TryExit(s, direction) || Horizontal(position - before).sqrMagnitude > .000001f;
            }
            Vector3 moved = Horizontal(position - before);
            if (moved.sqrMagnitude <= .000001f) return false;
            SetFacing(s, moved.normalized);
            return true;
        }

        public bool IsWalkable(Vector3 world) => Ready && Finite(world) && TryGround(world, out _);

        public bool NearShop(HabitatState s)
        {
            if (landingOnly || !Ready || s == null || !s.onHarbor) return false;
            Vector3 target = model.TransformPoint(HabitatImportedDockLayout.ShopApproach);
            return Horizontal(WorldPosition(s) - target).sqrMagnitude <= 1.2f * 1.2f;
        }

        public bool NearBoat(HabitatState s)
        {
            if (!Ready || s == null || !s.onHarbor) return false;
            Vector3 target=model.TransformPoint(HabitatImportedDockLayout.BoatApproach);
            return Horizontal(WorldPosition(s)-target).sqrMagnitude<=1.1f*1.1f;
        }

        public bool NearHome(HabitatState s)
        {
            return !landingOnly && Ready && s != null && s.onHarbor && Horizontal(WorldPosition(s)
                - model.TransformPoint(HabitatImportedDockLayout.HomeApproach)).sqrMagnitude <= 1.2f * 1.2f;
        }

        public HabitatDockInteraction NearestInteraction(HabitatState s)
        {
            if (!Ready || s == null || !s.onHarbor) return HabitatDockInteraction.None;
            Vector3 position = WorldPosition(s);
            HabitatDockInteraction result = HabitatDockInteraction.None;
            float best = float.PositiveInfinity;
            foreach (var kind in new[] { HabitatDockInteraction.Home, HabitatDockInteraction.Shop, HabitatDockInteraction.Boat })
            {
                if(landingOnly && kind!=HabitatDockInteraction.Boat)continue;
                Vector3 point = kind == HabitatDockInteraction.Home ? HabitatImportedDockLayout.HomeApproach
                    : kind == HabitatDockInteraction.Shop ? HabitatImportedDockLayout.ShopApproach : HabitatImportedDockLayout.BoatApproach;
                float distance = Horizontal(position - model.TransformPoint(point)).sqrMagnitude;
                float radius = kind == HabitatDockInteraction.Boat ? 1.1f : 1.2f;
                if (distance <= radius * radius && distance < best) { best = distance; result = kind; }
            }
            return result;
        }

        Vector3 ResolvePosition(HabitatState s)
        {
            if (Finite(s.harborX) && Finite(s.harborZ)
                && TryGround(dock.TransformPoint(new Vector3(s.harborX, 0f, s.harborZ)), out Vector3 position)) return position;
            if(homeHarbor && TryPlaceAt(s,HabitatImportedDockLayout.BoatApproach))
                { TryGround(dock.TransformPoint(new Vector3(s.harborX,0f,s.harborZ)),out Vector3 recovered);return recovered; }
            // Old or invalid coordinates recover on the actual shore-side plank,
            // which remains a bridge surface even when the landing tile is flooded.
            s.harborX = shore.x;
            s.harborZ = shore.z;
            SetFacing(s, Horizontal(dock.TransformDirection(Vector3.back)).normalized);
            Vector3 origin = dock.TransformPoint(shore);
            if (TryGround(origin, out position)) return position;
            s.onHarbor = false;
            return GridPosition(s);
        }

        bool TryGround(Vector3 world, out Vector3 position)
        {
            position = world;
            if (!Ready || !Finite(world)) return false;
            if(homeIsland)
            {
                // Prefer the measured wooden deck where it covers the ground,
                // so opening the island never lowers the player's feet into it.
                if(TryDeckGround(world,out position))return true;
                if(homeIsland.TryGround(world,PlayerRadius,out position))
                {position+=Vector3.up*FootOffset;return true;}
                return false;
            }
            Vector3 local = dock.InverseTransformPoint(world);
            float dockScale = Mathf.Min(Mathf.Abs(dock.lossyScale.x), Mathf.Abs(dock.lossyScale.z));
            if (dockScale < .0001f) return false;
            float halfWidth = bridgeWidth * .5f - PlayerRadius / dockScale;
            // Continue across the landing centre to +.25, keeping its top flat.
            // Exit separately checks that the underlying grid tile is still dry.
            Vector3 landing = new Vector3(0f, shore.y, .25f);
            if (OnBridge(local, landing, shore, halfWidth, out float top)
                || OnBridge(local, shore, bridgeEnd, halfWidth, out top))
            {
                position = dock.TransformPoint(new Vector3(local.x, top, local.z)) + Vector3.up * FootOffset;
                return true;
            }

            if(landingOnly)return false;
            return TryDeckGround(world,out position);
        }

        bool TryDeckGround(Vector3 world,out Vector3 position)
        {
            position=world;
            float scale = Mathf.Min(Mathf.Abs(model.lossyScale.x), Mathf.Abs(model.lossyScale.z));
            float verticalScale = Mathf.Abs(model.lossyScale.y);
            if (scale < .0001f || verticalScale < .0001f) return false;
            Vector3 normalized = model.InverseTransformPoint(world);
            if (!HabitatImportedDockLayout.IsWalkable(normalized, PlayerRadius / scale)) return false;
            // The ray starts only .45 normalized metres above the measured deck.
            // Roofs/furniture cannot become ground through a high downward ray.
            Vector3 origin = model.TransformPoint(new Vector3(normalized.x, .45f, normalized.z));
            Ray ray = new Ray(origin, Vector3.down);
            float nearest = float.PositiveInfinity;
            bool found = false;
            foreach (MeshCollider surface in surfaces)
            {
                if (!surface || !surface.enabled || !surface.gameObject.activeInHierarchy || !surface.sharedMesh) continue;
                if (surface.Raycast(ray, out RaycastHit hit, .8f * verticalScale)
                    && hit.normal.y > .5f && hit.distance < nearest)
                {
                    nearest = hit.distance;
                    position = hit.point + Vector3.up * FootOffset;
                    found = true;
                }
            }
            return found;
        }

        static bool OnBridge(Vector3 point, Vector3 start, Vector3 end, float halfWidth, out float top)
        {
            top = start.y;
            if (halfWidth <= 0f || !Finite(start) || !Finite(end)) return false;
            Vector2 a = new Vector2(start.x, start.z), b = new Vector2(end.x, end.z);
            Vector2 segment = b - a;
            float length = segment.magnitude;
            if (length < .0001f) return false;
            Vector2 along = segment / length, relative = new Vector2(point.x, point.z) - a;
            float distance = Vector2.Dot(relative, along);
            if (distance < -.0001f || distance > length + .0001f
                || Mathf.Abs(relative.x * along.y - relative.y * along.x) > halfWidth) return false;
            top = Mathf.Lerp(start.y, end.y, Mathf.Clamp01(distance / length));
            return true;
        }

        bool LandingAvailable(HabitatState s)
        {
            if(homeHarbor)return false;
            Tile tile = HabitatRules.TileAt(s, s.dockX, s.dockZ);
            if (tile == null || HabitatRules.IsWater(s, s.dockX, s.dockZ)
                || tile.prop == PropKind.Obstacle || tile.prop == PropKind.Chest || tile.prop == PropKind.Fan) return false;
            float surface = Mathf.Max(HabitatRules.Height(s, s.dockX, s.dockZ) * GridStep,HabitatRules.WaterSurfaceY(s.waterLevel)+.025f);
            // If a rule change preceded this frame's presentation rebuild, wait for
            // Bind rather than stepping between a stale bridge and the new terrain.
            return Mathf.Abs(surface - dock.TransformPoint(shore).y) <= .06f;
        }

        bool TryExit(HabitatState s, Vector3 facing)
        {
            if (!LandingAvailable(s))
            {
                s.message = HabitatRules.IsWater(s, s.dockX, s.dockZ)
                    ? "岸边已淹没，请等待退潮" : "岸边暂时无法通行";
                return false;
            }
            s.onHarbor = false;
            s.playerX = s.dockX;
            s.playerZ = s.dockZ;
            s.harborX = s.harborZ = 0f;
            s.selectedX = s.selectedZ = -1;
            s.message = "已登岛。按 B 联系商人购买补给；木箱在渡口岸边装卸，返航请走到小船旁按 E。";
            SetFacing(s, facing);
            bool xAxis = Mathf.Abs(facing.x) > Mathf.Abs(facing.z);
            s.facingX = xAxis ? (facing.x >= 0f ? 1 : -1) : 0;
            s.facingZ = xAxis ? 0 : (facing.z >= 0f ? 1 : -1);
            return true;
        }

        static void SetFacing(HabitatState s, Vector3 direction)
        {
            s.harborFacingX = direction.x;
            s.harborFacingZ = direction.z;
        }

        static Vector3 GridPosition(HabitatState s)
            => new Vector3(s.playerX, Mathf.Max(HabitatRules.Height(s, s.playerX, s.playerZ) * GridStep + FootOffset,
                HabitatRules.WaterSurfaceY(s.waterLevel) + .025f), s.playerZ);

        static Vector3 Horizontal(Vector3 value) => new Vector3(value.x, 0f, value.z);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
