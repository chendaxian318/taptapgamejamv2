using System;
using System.Collections.Generic;
using UnityEngine;

namespace HabitatIsland
{
    public sealed partial class HabitatMainStory
    {
        [Header("Optional story art (empty uses replaceable geometry)")]
        [Tooltip("Boat facing local +Z, centered at waterline. Resources fallback: Models/Story/DepartureBoat")]
        public GameObject departureBoatPrefab;
        [Tooltip("Net handle origin, hoop above local +Y. Resources fallback: Models/Story/LandingNet")]
        public GameObject landingNetPrefab;
        [Tooltip("Animated seabird around local origin. Resources fallback: Models/Story/Seabird")]
        public GameObject seabirdPrefab;

        enum ChapterSequence { None, Departure, ChestReveal, WalkHome }
        ChapterSequence chapterSequence;
        Action chapterCompleted;
        float chapterTime, chapterDuration, chapterInitialSize;
        Vector3 chapterStart, chapterTarget, departureDirection;
        Vector3[] chapterRoute;
        Transform departureBoat;
        HabitatHarborBoat harborBoat;
        LineRenderer chestRing;
        bool proceduralGulls;
        readonly Dictionary<Animator, float> storyAnimatorSpeeds = new Dictionary<Animator, float>();

        public bool BeginDeparture(Action completed)
        {
            if (!game || game.State == null || !game.State.onHarbor) return false;
            if (!BeginChapterSequence(ChapterSequence.Departure, 4.2f, completed)) return false;
            try
            {
                CurrentShot = "Departure";
                CurrentCaption = HabitatLevels.IsHomeHarbor(game.State)?"解开缆绳，乘小船前往岛屿。":"带上收获，乘小船返回家园港口。";
                harborBoat = dockModel.GetComponent<HabitatHarborBoat>();
                Vector3 boatPoint = dockModel.TransformPoint(HabitatImportedDockLayout.BoatArrival);
                departureDirection = boatPoint - dockModel.TransformPoint(HabitatImportedDockLayout.DockArrival);
                departureDirection.y = 0; departureDirection.Normalize();
                chapterStart = boatPoint + departureDirection * 1.5f;
                chapterStart.y = HabitatRules.WaterSurfaceY(game.State.waterLevel) + .035f;
                if (harborBoat && harborBoat.Vessel) departureBoat = harborBoat.Vessel;
                else
                {
                    departureBoat = Child("Departure boat", effectsRoot);
                    departureBoat.localScale = Vector3.one / Mathf.Max(.001f, Mathf.Abs(dockModel.lossyScale.y));
                    GameObject asset = departureBoatPrefab ? departureBoatPrefab : Resources.Load<GameObject>("Models/Story/DepartureBoat");
                    if (asset) InstantiateStoryVisual(asset, departureBoat, "Departure boat art");
                    else BuildDepartureBoat(departureBoat);
                }
                SampleChapterSequence(0);
                return true;
            }
            catch (Exception error) { Debug.LogError("[Main story] Departure failed: " + error.Message); Cancel(); return false; }
        }

        public bool BeginChestReveal(int x, int z, Action completed)
        {
            if (!game || game.State == null || !HabitatRules.Inside(game.State, x, z)
                || HabitatRules.TileAt(game.State, x, z).prop != PropKind.Chest) return false;
            string caption=HabitatRules.TileAt(game.State,x,z).chestReward==ChestReward.BackBasket
                ?"岛上留下了一只宝箱。探索道路，找到能携带大虾的背篓。"
                :"岛上留下了一只宝箱。探索机关与道路，找到里面的特制鱼竿。";
            return BeginIslandReveal(new Vector3(x, HabitatRules.Height(game.State, x, z) * .7f + .18f, z),caption,completed);
        }

        public bool BeginResidentReveal(Animal animal,string caption,Action completed)
        {
            if(!game||animal==null||game.State==null||!game.State.animals.Contains(animal)||animal.carried)return false;
            Vector3 target=new Vector3(animal.x,Mathf.Max(game.State.waterLevel+1,HabitatRules.Height(game.State,animal.x,animal.z))*.7f+.18f,animal.z);
            return BeginIslandReveal(target,caption,completed);
        }

        bool BeginIslandReveal(Vector3 target,string caption,Action completed)
        {
            if (!BeginChapterSequence(ChapterSequence.ChestReveal, 3.8f, completed)) return false;
            try
            {
                CurrentShot = "ChestReveal";
                CurrentCaption = caption;
                chapterStart = view.PlayerWorldPosition;
                chapterTarget = target;
                chestRing = new GameObject("Rod chest gold hint", typeof(LineRenderer)).GetComponent<LineRenderer>();
                chestRing.transform.SetParent(effectsRoot, false);
                chestRing.useWorldSpace = true; chestRing.loop = true; chestRing.positionCount = 64;
                chestRing.widthMultiplier = .06f;
                chestRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                chestRing.receiveShadows = false;
                var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor", new Color(1f, .78f, .12f, 1f));
                temporaryMaterials.Add(material); chestRing.sharedMaterial = material;
                SampleChapterSequence(0);
                return true;
            }
            catch (Exception error) { Debug.LogError("[Main story] Chest hint failed: " + error.Message); Cancel(); return false; }
        }

        public bool BeginWalkHome(Action completed)
        {
            if (!game || game.State == null || !game.State.onHarbor || !view || !view.Harbor.Ready) return false;
            Transform model = view.Harbor.ModelTransform;
            Vector3 start = model.InverseTransformPoint(view.PlayerWorldPosition);
            Vector3[] route = HomeRouteFrom(start);
            if (route == null) return false;
            float length = 0;
            for (int i = 1; i < route.Length; i++) length += Vector3.Distance(model.TransformPoint(route[i - 1]), model.TransformPoint(route[i]));
            if (!BeginChapterSequence(ChapterSequence.WalkHome, Mathf.Max(1f, length / HabitatPlayerVisual.WalkSpeed), completed)) return false;
            chapterRoute = route;
            CurrentShot = "WalkHome";
            CurrentCaption = "今天的收获已经带回。沿着码头回家，迎接明天。";
            SampleChapterSequence(0);
            return true;
        }

        bool BeginChapterSequence(ChapterSequence sequence, float duration, Action completed)
        {
            if (IsPlaying || !game || !view || !view.GameCamera || !view.Harbor.Ready || !view.Harbor.ModelTransform) return false;
            chapterInitialSize = view.GameCamera.orthographicSize;
            if (!view.BeginCinematic()) return false;
            dockModel = view.Harbor.ModelTransform;
            chapterSequence = sequence; chapterCompleted = completed;
            chapterTime = 0; chapterDuration = duration;
            IsPlaying = true; IsPaused = false;
            effectsRoot = Child("Chapter story temporary effects", dockModel);
            effectsRoot.gameObject.hideFlags = HideFlags.DontSave;
            return true;
        }

        void Update()
        {
            if(IsCgPlaying){TickCg();return;}
            if (!IsPlaying || IsPaused || chapterSequence == ChapterSequence.None) return;
            chapterTime = Mathf.Min(chapterDuration, chapterTime + Mathf.Min(Time.unscaledDeltaTime, .1f));
            SampleChapterSequence(chapterTime / Mathf.Max(.001f, chapterDuration));
            if (chapterTime >= chapterDuration) Complete();
        }

        void SampleChapterSequence(float progress)
        {
            if (!view || !dockModel) { Cancel(); return; }
            float t = Mathf.Clamp01(progress);
            Quaternion angle = dockModel.rotation * Quaternion.Euler(HabitatView.DefaultCameraPitch, -HabitatView.DefaultCameraYaw, 0f);
            if (chapterSequence == ChapterSequence.Departure)
            {
                Vector3 point = chapterStart + departureDirection * (8f * Mathf.SmoothStep(0, 1, t));
                point.y += Mathf.Sin(chapterTime * 2.7f) * .035f;
                if (harborBoat && harborBoat.Vessel)
                { harborBoat.SampleSailing(t, chapterTime); point = departureBoat.position; }
                else if (departureBoat) departureBoat.SetPositionAndRotation(point,
                    Quaternion.LookRotation(departureDirection) * Quaternion.Euler(0, 0, Mathf.Sin(chapterTime * 2.1f) * 2f));
                // Ride the same actor with the boat; world displacement must not
                // make the character walk in place while on the moving vessel.
                view.SetPlayerMotion(HabitatPlayerMotion.Ride);
                view.SetCinematicPose(harborBoat && harborBoat.Vessel ? harborBoat.PassengerPosition : point + Vector3.up * .71f,
                    harborBoat ? harborBoat.Heading : departureDirection, true);
                view.SetCinematicCamera(point + Vector3.up * .5f, angle, 4.6f);
            }
            else if (chapterSequence == ChapterSequence.ChestReveal)
            {
                float focus = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .32f));
                view.SetCinematicCamera(Vector3.Lerp(chapterStart, chapterTarget, focus), angle,
                    Mathf.Lerp(chapterInitialSize, 2.8f, focus));
                if (chestRing)
                    for (int i = 0; i < chestRing.positionCount; i++)
                    {
                        float a = i * Mathf.PI * 2 / chestRing.positionCount;
                        float radius = .68f + Mathf.Sin(chapterTime * 3) * .035f;
                        chestRing.SetPosition(i, chapterTarget + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
                    }
            }
            else if (chapterSequence == ChapterSequence.WalkHome)
            {
                Vector3 point = SampleRoute(chapterRoute, t);
                Vector3 before = SampleRoute(chapterRoute, Mathf.Max(0, t - .005f));
                Vector3 after = SampleRoute(chapterRoute, Mathf.Min(1, t + .005f));
                Vector3 heading = dockModel.TransformDirection(after - before); heading.y = 0;
                if (heading.sqrMagnitude > .00001f) facing = heading.normalized;
                Vector3 world = DeckPosition(point);
                view.SetCinematicPose(world, facing, true);
                view.SetCinematicCamera(world + Vector3.up * .5f, angle, 4.0f);
            }
        }

        void CompleteChapterSequence()
        {
            Action completed = chapterCompleted;
            IsPlaying = IsPaused = false;
            ClearChapterSequence();
            Cleanup(true);
            // Clearing first makes Skip, cancellation and a callback that starts the
            // next sequence safe. Only the caller decides when gameplay is committed.
            completed?.Invoke();
        }

        void ClearChapterSequence()
        {
            chapterSequence = ChapterSequence.None; chapterCompleted = null;
            chapterTime = chapterDuration = 0; chapterRoute = null;
            if (harborBoat) harborBoat.RestoreBerth();
            harborBoat = null; departureBoat = null; chestRing = null;
        }

        static Vector3 SampleRoute(Vector3[] route, float progress)
        {
            float total = 0;
            for (int i = 1; i < route.Length; i++) total += Vector3.Distance(route[i - 1], route[i]);
            float remaining = total * Mathf.Clamp01(progress);
            for (int i = 1; i < route.Length; i++)
            {
                float segment = Vector3.Distance(route[i - 1], route[i]);
                if (remaining <= segment && segment > .00001f) return Vector3.Lerp(route[i - 1], route[i], remaining / segment);
                remaining -= segment;
            }
            return route[route.Length - 1];
        }

        static Vector3[] HomeRouteFrom(Vector3 start)
        {
            Vector3[] authored = HabitatImportedDockLayout.HomePath;
            int best = -1; float distance = float.PositiveInfinity;
            Vector3 projection = start;
            for (int i = 1; i < authored.Length; i++)
            {
                Vector3 a = authored[i - 1], delta = authored[i] - a;
                Vector3 flatStart = start; flatStart.y = a.y;
                float u = Mathf.Clamp01(Vector3.Dot(flatStart - a, delta) / Mathf.Max(.00001f, delta.sqrMagnitude));
                Vector3 p = a + delta * u;
                float d = (flatStart - p).sqrMagnitude;
                if (d < distance) { distance = d; best = i; projection = p; }
            }
            if (best < 0 || distance > 2.25f) return null;
            var result = new List<Vector3> { start, projection };
            for (int i = best; i < authored.Length; i++) result.Add(authored[i]);
            // Reject off-route callers instead of cutting through the shop/home.
            for (int i = 1; i < result.Count; i++)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(result[i - 1], result[i]) / .15f));
                for (int n = 0; n <= steps; n++)
                    if (!HabitatImportedDockLayout.IsWalkable(Vector3.Lerp(result[i - 1], result[i], n / (float)steps), .25f)) return null;
            }
            return result.ToArray();
        }

        void BuildDepartureBoat(Transform root)
        {
            Material wood = MakeMaterial(new Color(.38f, .19f, .09f));
            Material trim = MakeMaterial(new Color(.83f, .62f, .30f));
            Material inside = MakeMaterial(new Color(.60f, .37f, .18f));
            Part("Hull", root, PrimitiveType.Sphere, new Vector3(0, .18f, 0), new Vector3(1.65f, .65f, 3.0f), wood);
            Part("Open hull", root, PrimitiveType.Cube, new Vector3(0, .47f, -.05f), new Vector3(1.16f, .12f, 2.25f), inside);
            Part("Port gunwale", root, PrimitiveType.Cube, new Vector3(-.68f, .60f, 0), new Vector3(.12f, .20f, 2.45f), trim);
            Part("Starboard gunwale", root, PrimitiveType.Cube, new Vector3(.68f, .60f, 0), new Vector3(.12f, .20f, 2.45f), trim);
            for (int i = -1; i <= 1; i++) Part("Seat", root, PrimitiveType.Cube, new Vector3(0, .66f, i * .68f), new Vector3(1.35f, .10f, .21f), trim);
            Transform paddle = Part("Oar", root, PrimitiveType.Cube, new Vector3(-.96f, .72f, -.2f), new Vector3(.07f, .08f, 2.4f), trim);
            paddle.localRotation = Quaternion.Euler(0, -22, 0);
        }

        GameObject InstantiateStoryVisual(GameObject prefab, Transform parent, string name)
        {
            GameObject result = Instantiate(prefab, parent, false); result.name = name;
            foreach (Collider collider in result.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in result.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
            foreach (Animator animator in result.GetComponentsInChildren<Animator>(true)) storyAnimatorSpeeds[animator] = animator.speed;
            return result;
        }
    }
}
