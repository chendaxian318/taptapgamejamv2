using UnityEngine;

namespace HabitatIsland
{
    /// <summary>
    /// Layout measured from Meshy_AI_Wooden_Dock_Scene_Uni_1014170130_texture (1).glb.
    /// Source SHA256: de85c18557053aa9c9cd819879208fa6ab80e2284a34e0c5ccb30a7a67ce2a7b.
    /// glTF Y is up. Unity import flips X. SourceScale/SourceOffset normalize the full model to 24 m.
    /// The small hut is the shop, the large hut is home. The NoStairs variant fills the
    /// former home stair footprint; only that local boundary is updated below.
    /// </summary>
    public static class HabitatImportedDockLayout
    {
        public const float SourceScale = 24f / 1.9015330076f;
        public const float SourceDeckY = -.073f;
        // The boat is much lower than the main deck; the old prototype sea level would submerge it.
        public const float SeaSurfaceY = (-.18f - SourceDeckY) * SourceScale;
        public const float SourceCenterX = -.0008374453f;
        public const float SourceCenterZ = -.0056934953f;
        public static readonly Vector3 SourceOffset = new Vector3(
            SourceCenterX * SourceScale, -SourceDeckY * SourceScale, -SourceCenterZ * SourceScale);
        public const float BoardingWidth = 1.2f;

        public static Vector3 FromSource(float x, float y, float z)
            => new Vector3(-x, y, z) * SourceScale + SourceOffset;

        public static readonly Vector3 BoatArrival = FromSource(-.78f, -.125f, .58f);
        public static readonly Vector3 BoardingStart = BoatArrival;
        public static readonly Vector3 BoardingEnd = FromSource(-.72f, -.070f, .375f);
        public static readonly Vector3 BoatApproach = FromSource(-.69f, SourceDeckY, .31f);
        public static readonly Vector3 DockArrival = FromSource(-.60f, SourceDeckY, .15f);
        public static readonly Vector3 ShopWaypoint = FromSource(-.59f, SourceDeckY, -.015f);
        public static readonly Vector3 ShopApproach = FromSource(-.29f, SourceDeckY, -.015f);
        public static readonly Vector3 HomeApproach = FromSource(.22f, SourceDeckY, -.295f);
        public static readonly Vector3 WindowTarget = FromSource(.714f, .044f, -.562f);
        public static readonly Vector3 WindowForward = Vector3.back;
        public static readonly Quaternion WindowRotation = Quaternion.Euler(12, 0, 0);
        public static readonly Vector3 HomeLabel = FromSource(.65f, .22f, -.43f);
        public static readonly Vector3 ShopLabel = FromSource(-.29f, .15f, .205f);

        // BoatArrival is on the added boarding plank inside the existing boat.
        // The bridge crosses a real water gap. Free walking remains on the deck; E boards the boat.
        public static readonly Vector3[] ReturnPath = {
            BoatArrival, BoardingEnd, BoatApproach, DockArrival
        };
        public static readonly Vector3[] ShopPath = {
            DockArrival, ShopWaypoint, ShopApproach
        };
        public static readonly Vector3[] HomePath = {
            ShopApproach, FromSource(-.085f, SourceDeckY, -.015f),
            FromSource(.04f, SourceDeckY, -.1275f), FromSource(.165f, SourceDeckY, -.2525f), HomeApproach
        };

        /// <summary>
        /// Planar clearance check. Call the imported mesh's ground-height sampler separately.
        /// The boundary and obstacle loops come from a 0.0125-source-unit top-surface scan,
        /// keeping the main connected deck in source Y [-.09,-.05]. Roofs, tables, plants,
        /// rail posts and low awnings are excluded. Contours are simplified by <= .006 source units.
        /// Keep these measurements paired with the source hash above if replacing the model.
        /// </summary>
        public static bool IsWalkable(Vector3 position, float radius)
        {
            if (float.IsNaN(position.x) || float.IsInfinity(position.x) || float.IsNaN(position.z)
                || float.IsInfinity(position.z) || float.IsNaN(radius) || float.IsInfinity(radius)) return false;
            Vector2 p = new Vector2(-(position.x - SourceOffset.x) / SourceScale,
                (position.z - SourceOffset.z) / SourceScale);
            float clearance = Mathf.Max(0, radius) / SourceScale;
            if (!Contains(p, SourceDeckOutline) || DistanceToBoundary(p, SourceDeckOutline) < clearance) return false;
            foreach (Vector2[] obstacle in SourceObstacles)
                if (Contains(p, obstacle) || DistanceToBoundary(p, obstacle) < clearance) return false;
            return true;
        }

        public static bool OverDeck(Vector3 position,float radius)
        {
            Vector2 p=new Vector2(-(position.x-SourceOffset.x)/SourceScale,(position.z-SourceOffset.z)/SourceScale);
            return Contains(p,SourceDeckOutline)||DistanceToBoundary(p,SourceDeckOutline)<Mathf.Max(0f,radius)/SourceScale;
        }

        static bool Contains(Vector2 p, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y)
                    && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        static float DistanceToBoundary(Vector2 p, Vector2[] polygon)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i], d = polygon[(i + 1) % polygon.Length] - a;
                float t = d.sqrMagnitude > .000000001f ? Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude) : 0;
                best = Mathf.Min(best, (p - a - t * d).sqrMagnitude);
            }
            return Mathf.Sqrt(best);
        }

        static readonly Vector2[] SourceDeckOutline = {
            new Vector2(0.34625f, -0.60875f), new Vector2(0.43375f, -0.60875f), new Vector2(0.43375f, -0.59625f), new Vector2(0.47125f, -0.59625f),
            new Vector2(0.47125f, -0.58375f), new Vector2(0.45875f, -0.58375f), new Vector2(0.45875f, -0.55875f), new Vector2(0.42125f, -0.55875f),
            new Vector2(0.42125f, -0.54625f), new Vector2(0.40875f, -0.54625f), new Vector2(0.40875f, -0.35875f), new Vector2(0.39625f, -0.35875f),
            new Vector2(0.39625f, -0.28375f), new Vector2(0.48375f, -0.28375f), new Vector2(0.48375f, -0.24625f), new Vector2(0.77125f, -0.24625f),
            new Vector2(0.77125f, -0.23375f), new Vector2(0.79625f, -0.23375f), new Vector2(0.80875f, -0.20875f), new Vector2(0.83375f, -0.20875f),
            new Vector2(0.83375f, -0.19625f), new Vector2(0.80875f, -0.18375f), new Vector2(0.80875f, -0.17125f), new Vector2(0.77125f, -0.17125f),
            new Vector2(0.77125f, -0.14625f), new Vector2(0.74625f, -0.14625f), new Vector2(0.74625f, -0.15875f), new Vector2(0.75875f, -0.15875f),
            new Vector2(0.74625f, -0.18375f), new Vector2(0.72125f, -0.17125f), new Vector2(0.72125f, -0.19625f), new Vector2(0.70875f, -0.19625f),
            new Vector2(0.69625f, -0.17125f), new Vector2(0.68375f, -0.17125f), new Vector2(0.67125f, -0.19625f), new Vector2(0.65875f, -0.19625f),
            new Vector2(0.65875f, -0.18375f), new Vector2(0.63375f, -0.18375f), new Vector2(0.63375f, -0.15875f), new Vector2(0.60875f, -0.15875f),
            new Vector2(0.59625f, -0.13375f), new Vector2(0.58375f, -0.13375f), new Vector2(0.58375f, -0.12125f), new Vector2(0.59625f, -0.12125f),
            new Vector2(0.58375f, -0.09625f), new Vector2(0.60875f, -0.09625f), new Vector2(0.60875f, -0.08375f), new Vector2(0.59625f, -0.08375f),
            new Vector2(0.59625f, -0.05875f), new Vector2(0.58375f, -0.05875f), new Vector2(0.58375f, -0.03375f), new Vector2(0.55875f, -0.02125f),
            new Vector2(0.55875f, -0.00875f), new Vector2(0.53375f, 0.00375f), new Vector2(0.53375f, 0.01625f), new Vector2(0.42125f, 0.01625f),
            new Vector2(0.42125f, 0.02875f), new Vector2(0.37125f, 0.02875f), new Vector2(0.37125f, 0.04125f), new Vector2(0.30875f, 0.04125f),
            new Vector2(0.30875f, 0.05375f), new Vector2(0.28375f, 0.05375f), new Vector2(0.28375f, 0.06625f), new Vector2(0.24625f, 0.06625f),
            new Vector2(0.24625f, 0.07875f), new Vector2(0.20875f, 0.07875f), new Vector2(0.20875f, 0.09125f), new Vector2(0.17125f, 0.09125f),
            new Vector2(0.17125f, 0.10375f), new Vector2(0.13375f, 0.10375f), new Vector2(0.12125f, 0.12875f), new Vector2(0.09625f, 0.12875f),
            new Vector2(0.09625f, 0.14125f), new Vector2(0.08375f, 0.14125f), new Vector2(0.07125f, 0.16625f), new Vector2(0.05875f, 0.16625f),
            new Vector2(0.05875f, 0.19125f), new Vector2(0.03375f, 0.19125f), new Vector2(0.03375f, 0.20375f), new Vector2(0.04625f, 0.20375f),
            new Vector2(0.04625f, 0.22875f), new Vector2(0.02125f, 0.22875f), new Vector2(0.02125f, 0.24125f), new Vector2(0.03375f, 0.24125f),
            new Vector2(0.02125f, 0.26625f), new Vector2(0.07125f, 0.26625f), new Vector2(0.07125f, 0.29125f), new Vector2(-0.02875f, 0.29125f),
            new Vector2(-0.02875f, 0.27875f), new Vector2(-0.00375f, 0.27875f), new Vector2(-0.00375f, 0.26625f), new Vector2(-0.02875f, 0.26625f),
            new Vector2(-0.02875f, 0.25375f), new Vector2(-0.04125f, 0.25375f), new Vector2(-0.04125f, 0.19125f), new Vector2(-0.05375f, 0.19125f),
            new Vector2(-0.05375f, 0.30375f), new Vector2(-0.06625f, 0.30375f), new Vector2(-0.06625f, 0.32875f), new Vector2(-0.07875f, 0.32875f),
            new Vector2(-0.07875f, 0.35375f), new Vector2(-0.09125f, 0.35375f), new Vector2(-0.09125f, 0.37875f), new Vector2(-0.10375f, 0.37875f),
            new Vector2(-0.10375f, 0.40375f), new Vector2(-0.11625f, 0.40375f), new Vector2(-0.11625f, 0.42875f), new Vector2(-0.16625f, 0.42875f),
            new Vector2(-0.16625f, 0.41625f), new Vector2(-0.20375f, 0.41625f), new Vector2(-0.20375f, 0.37875f), new Vector2(-0.22875f, 0.37875f),
            new Vector2(-0.22875f, 0.41625f), new Vector2(-0.27875f, 0.41625f), new Vector2(-0.27875f, 0.40375f), new Vector2(-0.34125f, 0.40375f),
            new Vector2(-0.34125f, 0.39125f), new Vector2(-0.25375f, 0.39125f), new Vector2(-0.25375f, 0.37875f), new Vector2(-0.35375f, 0.37875f),
            new Vector2(-0.35375f, 0.39125f), new Vector2(-0.37875f, 0.39125f), new Vector2(-0.39125f, 0.36625f), new Vector2(-0.46625f, 0.36625f),
            new Vector2(-0.46625f, 0.37875f), new Vector2(-0.44125f, 0.37875f), new Vector2(-0.44125f, 0.39125f), new Vector2(-0.49125f, 0.39125f),
            new Vector2(-0.49125f, 0.37875f), new Vector2(-0.47875f, 0.37875f), new Vector2(-0.47875f, 0.35375f), new Vector2(-0.51625f, 0.35375f),
            new Vector2(-0.51625f, 0.37875f), new Vector2(-0.52875f, 0.37875f), new Vector2(-0.52875f, 0.36625f), new Vector2(-0.57875f, 0.36625f),
            new Vector2(-0.57875f, 0.37875f), new Vector2(-0.66625f, 0.37875f), new Vector2(-0.66625f, 0.39125f), new Vector2(-0.71625f, 0.39125f),
            new Vector2(-0.71625f, 0.37875f), new Vector2(-0.75375f, 0.37875f), new Vector2(-0.75375f, 0.36625f), new Vector2(-0.77875f, 0.36625f),
            new Vector2(-0.77875f, 0.35375f), new Vector2(-0.81625f, 0.35375f), new Vector2(-0.81625f, 0.34125f), new Vector2(-0.84125f, 0.34125f),
            new Vector2(-0.84125f, 0.32875f), new Vector2(-0.87875f, 0.32875f), new Vector2(-0.87875f, 0.31625f), new Vector2(-0.90375f, 0.31625f),
            new Vector2(-0.90375f, 0.30375f), new Vector2(-0.94125f, 0.30375f), new Vector2(-0.94125f, 0.29125f), new Vector2(-0.95375f, 0.29125f),
            new Vector2(-0.95375f, 0.26625f), new Vector2(-0.94125f, 0.26625f), new Vector2(-0.94125f, 0.24125f), new Vector2(-0.70375f, 0.24125f),
            new Vector2(-0.70375f, 0.22875f), new Vector2(-0.67875f, 0.21625f), new Vector2(-0.67875f, 0.16625f), new Vector2(-0.69125f, 0.16625f),
            new Vector2(-0.69125f, 0.15375f), new Vector2(-0.67875f, 0.15375f), new Vector2(-0.67875f, 0.09125f), new Vector2(-0.66625f, 0.09125f),
            new Vector2(-0.66625f, 0.06625f), new Vector2(-0.67875f, 0.06625f), new Vector2(-0.67875f, 0.05375f), new Vector2(-0.65375f, 0.05375f),
            new Vector2(-0.65375f, 0.00375f), new Vector2(-0.64125f, 0.00375f), new Vector2(-0.64125f, -0.07125f), new Vector2(-0.62875f, -0.07125f),
            new Vector2(-0.62875f, -0.14625f), new Vector2(-0.61625f, -0.14625f), new Vector2(-0.61625f, -0.13375f), new Vector2(-0.56625f, -0.13375f),
            new Vector2(-0.54125f, -0.08375f), new Vector2(-0.51625f, -0.08375f), new Vector2(-0.51625f, -0.09625f), new Vector2(-0.46625f, -0.09625f),
            new Vector2(-0.46625f, -0.10875f), new Vector2(-0.40375f, -0.10875f), new Vector2(-0.40375f, -0.09625f), new Vector2(-0.35375f, -0.09625f),
            new Vector2(-0.35375f, -0.07125f), new Vector2(-0.31625f, -0.07125f), new Vector2(-0.31625f, -0.09625f), new Vector2(-0.32875f, -0.09625f),
            new Vector2(-0.31625f, -0.12125f), new Vector2(-0.30375f, -0.12125f), new Vector2(-0.30375f, -0.10875f), new Vector2(-0.22875f, -0.10875f),
            new Vector2(-0.22875f, -0.13375f), new Vector2(-0.17875f, -0.15875f), new Vector2(-0.19125f, -0.18375f), new Vector2(-0.16625f, -0.18375f),
            new Vector2(-0.16625f, -0.17125f), new Vector2(-0.15375f, -0.17125f), new Vector2(-0.15375f, -0.18375f), new Vector2(-0.12875f, -0.18375f),
            new Vector2(-0.12875f, -0.19625f), new Vector2(-0.10375f, -0.19625f), new Vector2(-0.10375f, -0.20875f), new Vector2(-0.07875f, -0.20875f),
            new Vector2(-0.07875f, -0.22125f), new Vector2(-0.05375f, -0.22125f), new Vector2(-0.05375f, -0.20875f), new Vector2(-0.04125f, -0.20875f),
            new Vector2(-0.04125f, -0.22125f), new Vector2(-0.01625f, -0.22125f), new Vector2(-0.01625f, -0.24625f), new Vector2(-0.00375f, -0.24625f),
            new Vector2(-0.00375f, -0.25875f), new Vector2(0.02125f, -0.25875f), new Vector2(0.02125f, -0.27125f), new Vector2(0.04625f, -0.28375f),
            new Vector2(0.04625f, -0.29625f), new Vector2(0.07125f, -0.30875f), new Vector2(0.07125f, -0.32125f), new Vector2(0.13375f, -0.32125f),
            new Vector2(0.13375f, -0.34625f), new Vector2(0.12125f, -0.34625f), new Vector2(0.12125f, -0.37125f), new Vector2(0.10875f, -0.37125f),
            new Vector2(0.10875f, -0.47125f), new Vector2(0.09625f, -0.47125f), new Vector2(0.09625f, -0.53375f), new Vector2(0.12125f, -0.52125f),
            new Vector2(0.12125f, -0.50875f), new Vector2(0.10875f, -0.50875f), new Vector2(0.10875f, -0.48375f), new Vector2(0.14625f, -0.48375f),
            new Vector2(0.14625f, -0.49625f), new Vector2(0.25875f, -0.49625f), new Vector2(0.25875f, -0.48375f), new Vector2(0.28375f, -0.48375f),
            new Vector2(0.28375f, -0.49625f), new Vector2(0.33375f, -0.49625f), new Vector2(0.33375f, -0.47125f), new Vector2(0.32125f, -0.47125f),
            new Vector2(0.32125f, -0.48375f), new Vector2(0.30875f, -0.48375f), new Vector2(0.30875f, -0.47125f), new Vector2(0.28375f, -0.47125f),
            new Vector2(0.28375f, -0.45875f), new Vector2(0.29625f, -0.45875f), new Vector2(0.28375f, -0.43375f), new Vector2(0.30875f, -0.43375f),
            new Vector2(0.30875f, -0.42125f), new Vector2(0.29625f, -0.42125f), new Vector2(0.29625f, -0.40875f), new Vector2(0.30875f, -0.40875f),
            new Vector2(0.30875f, -0.35875f), new Vector2(0.27125f, -0.35875f), new Vector2(0.27125f, -0.33375f),
            // Repaired flat planks replace the old stair exclusion; surrounding obstacles stay in place.
            new Vector2(0.38375f, -0.33375f), new Vector2(0.38375f, -0.35875f), new Vector2(0.37125f, -0.35875f),
            new Vector2(0.37125f, -0.37125f), new Vector2(0.39625f, -0.37125f), new Vector2(0.39625f, -0.45875f), new Vector2(0.37125f, -0.45875f),
            new Vector2(0.37125f, -0.48375f), new Vector2(0.34625f, -0.48375f), new Vector2(0.34625f, -0.49625f), new Vector2(0.37125f, -0.49625f),
            new Vector2(0.37125f, -0.52125f), new Vector2(0.35875f, -0.52125f), new Vector2(0.35875f, -0.59625f), new Vector2(0.34625f, -0.59625f),
        };

        static readonly Vector2[][] SourceObstacles = {
            new Vector2[] {
                new Vector2(-0.42875f, 0.05375f), new Vector2(-0.44125f, 0.05375f), new Vector2(-0.44125f, 0.19125f), new Vector2(-0.45375f, 0.19125f),
                new Vector2(-0.45375f, 0.27875f), new Vector2(-0.46625f, 0.27875f), new Vector2(-0.46625f, 0.29125f), new Vector2(-0.45375f, 0.29125f),
                new Vector2(-0.45375f, 0.32875f), new Vector2(-0.42875f, 0.32875f), new Vector2(-0.42875f, 0.34125f), new Vector2(-0.41625f, 0.34125f),
                new Vector2(-0.41625f, 0.32875f), new Vector2(-0.34125f, 0.32875f), new Vector2(-0.34125f, 0.34125f), new Vector2(-0.22875f, 0.34125f),
                new Vector2(-0.22875f, 0.35375f), new Vector2(-0.09125f, 0.35375f), new Vector2(-0.09125f, 0.34125f), new Vector2(-0.10375f, 0.34125f),
                new Vector2(-0.10375f, 0.31625f), new Vector2(-0.11625f, 0.31625f), new Vector2(-0.10375f, 0.29125f), new Vector2(-0.14125f, 0.29125f),
                new Vector2(-0.14125f, 0.27875f), new Vector2(-0.12875f, 0.27875f), new Vector2(-0.12875f, 0.15375f), new Vector2(-0.10375f, 0.14125f),
                new Vector2(-0.10375f, 0.11625f), new Vector2(-0.07875f, 0.11625f), new Vector2(-0.07875f, 0.12875f), new Vector2(-0.06625f, 0.12875f),
                new Vector2(-0.06625f, 0.15375f), new Vector2(-0.05375f, 0.15375f), new Vector2(-0.05375f, 0.12875f), new Vector2(-0.06625f, 0.12875f),
                new Vector2(-0.06625f, 0.07875f), new Vector2(-0.10375f, 0.07875f), new Vector2(-0.10375f, 0.09125f), new Vector2(-0.12875f, 0.09125f),
                new Vector2(-0.12875f, 0.07875f), new Vector2(-0.14125f, 0.07875f), new Vector2(-0.14125f, 0.05375f), new Vector2(-0.19125f, 0.05375f),
                new Vector2(-0.19125f, 0.07875f), new Vector2(-0.31625f, 0.07875f), new Vector2(-0.31625f, 0.06625f), new Vector2(-0.42875f, 0.06625f),
            },
            new Vector2[] {
                new Vector2(0.23375f, -0.12125f), new Vector2(0.22125f, -0.12125f), new Vector2(0.22125f, -0.09625f), new Vector2(0.23375f, -0.09625f),
                new Vector2(0.23375f, -0.08375f), new Vector2(0.25875f, -0.07125f), new Vector2(0.25875f, -0.04625f), new Vector2(0.23375f, -0.05875f),
                new Vector2(0.23375f, -0.07125f), new Vector2(0.20875f, -0.07125f), new Vector2(0.20875f, -0.08375f), new Vector2(0.19625f, -0.08375f),
                new Vector2(0.19625f, -0.10875f), new Vector2(0.15875f, -0.10875f), new Vector2(0.15875f, -0.09625f), new Vector2(0.13375f, -0.08375f),
                new Vector2(0.13375f, -0.07125f), new Vector2(0.10875f, -0.07125f), new Vector2(0.09625f, -0.04625f), new Vector2(0.08375f, -0.04625f),
                new Vector2(0.08375f, -0.02125f), new Vector2(0.09625f, -0.02125f), new Vector2(0.09625f, -0.00875f), new Vector2(0.12125f, -0.00875f),
                new Vector2(0.12125f, 0.00375f), new Vector2(0.10875f, 0.00375f), new Vector2(0.10875f, 0.02875f), new Vector2(0.13375f, 0.04125f),
                new Vector2(0.13375f, 0.06625f), new Vector2(0.14625f, 0.06625f), new Vector2(0.14625f, 0.07875f), new Vector2(0.17125f, 0.07875f),
                new Vector2(0.18375f, 0.05375f), new Vector2(0.19625f, 0.05375f), new Vector2(0.19625f, 0.04125f), new Vector2(0.22125f, 0.04125f),
                new Vector2(0.22125f, 0.02875f), new Vector2(0.24625f, 0.01625f), new Vector2(0.24625f, 0.00375f), new Vector2(0.27125f, 0.00375f),
                new Vector2(0.27125f, -0.03375f), new Vector2(0.29625f, -0.03375f), new Vector2(0.29625f, -0.04625f), new Vector2(0.30875f, -0.04625f),
                new Vector2(0.29625f, -0.07125f), new Vector2(0.28375f, -0.07125f), new Vector2(0.28375f, -0.09625f), new Vector2(0.27125f, -0.09625f),
                new Vector2(0.27125f, -0.10875f), new Vector2(0.25875f, -0.10875f), new Vector2(0.25875f, -0.12125f),
            },
            new Vector2[] {
                new Vector2(0.54625f, -0.22125f), new Vector2(0.53375f, -0.22125f), new Vector2(0.53375f, -0.15875f), new Vector2(0.59625f, -0.15875f),
                new Vector2(0.59625f, -0.22125f),
            },
            new Vector2[] {
                new Vector2(0.05875f, -0.00875f), new Vector2(0.03375f, 0.00375f), new Vector2(0.03375f, 0.01625f), new Vector2(0.04625f, 0.01625f),
                new Vector2(0.05875f, 0.04125f), new Vector2(0.07125f, 0.04125f), new Vector2(0.07125f, 0.06625f), new Vector2(0.08375f, 0.06625f),
                new Vector2(0.08375f, 0.07875f), new Vector2(0.10875f, 0.07875f), new Vector2(0.12125f, 0.05375f), new Vector2(0.09625f, 0.04125f),
                new Vector2(0.09625f, 0.01625f), new Vector2(0.08375f, 0.01625f), new Vector2(0.07125f, -0.00875f),
            },
        };
    }
}
