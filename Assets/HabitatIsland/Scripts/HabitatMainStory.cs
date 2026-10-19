using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HabitatIsland
{
    /// <summary>Plays the authored dock Timeline using the main map's existing actor and dock.</summary>
    [DisallowMultipleComponent]
    public sealed partial class HabitatMainStory : MonoBehaviour, IHabitatDockTimelineTarget
    {
        public TimelineAsset openingTimeline;
        public TimelineAsset morningTimeline;
        public PlayableDirector director;
        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public string CurrentCaption { get; private set; } = "";
        public string CurrentShot { get; private set; } = "";
        public event Action<bool> Completed;

        HabitatGame game;
        HabitatView view;
        PlayableDirector subscribedDirector;
        Transform dockModel, effectsRoot, gearRoot, netRoot, catchRoot;
        Transform[] gulls;
        double gullFlightStart, gullFlightDuration = 3.3;
        float gullFlightExtent;
        Light sun, windowLight;
        float savedSunIntensity;
        Quaternion savedSunRotation;
        Color savedSunColor;
        bool morningSequence;
        Vector3 facing = Vector3.forward;
        readonly List<Material> temporaryMaterials = new List<Material>();

        public void Initialize(HabitatGame owner)
        {
            game = owner;
            view = owner ? owner.View : null;
            if (!director) director = GetComponent<PlayableDirector>();
            if (!director) director = gameObject.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.None;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            if (subscribedDirector != director)
            {
                if (subscribedDirector) subscribedDirector.stopped -= OnStopped;
                subscribedDirector = director;
                director.stopped += OnStopped;
            }
        }

        public bool BeginOpening() => BeginCg("cg00",()=>Completed?.Invoke(false));
        public bool BeginMorning() => BeginCg("cg03",()=>Completed?.Invoke(true));

        bool BeginSequence(bool morning)
        {
            TimelineAsset sequence = morning ? morningTimeline : openingTimeline;
            if (IsPlaying || !game || !view || !director || !sequence || sequence.duration <= 0
                || !view.Harbor.Ready || !view.Harbor.ModelTransform) return false;
            bool hasTrack = false;
            foreach (TrackAsset track in sequence.GetOutputTracks()) if (track is HabitatDockBeatTrack) hasTrack = true;
            if (!hasTrack || !view.BeginCinematic()) return false;
            dockModel = view.Harbor.ModelTransform;
            morningSequence = morning;
            IsPlaying = true; IsPaused = false;
            try
            {
                SaveLighting();
                BuildEffects();
                ConfigureGullFlight(sequence);
                director.playableAsset = sequence;
                foreach (TrackAsset track in sequence.GetOutputTracks())
                    if (track is HabitatDockBeatTrack) director.SetGenericBinding(track, this);
                director.time = 0;
                director.Play();
                director.Evaluate();
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError("[Main story] Could not begin Timeline: " + error.Message);
                Cancel();
                return false;
            }
        }

        public void TogglePause()
        {
            if (!IsPlaying) return;
            IsPaused = !IsPaused;
            if (IsCgPlaying) { if(IsPaused)cgPlayer.Pause(); else if(cgPlayer.isPrepared)cgPlayer.Play(); }
            else if (chapterSequence == ChapterSequence.None)
            { if (IsPaused) director.Pause(); else director.Resume(); }
            foreach (var item in storyAnimatorSpeeds) if (item.Key) item.Key.speed = IsPaused ? 0 : item.Value;
            view.SetCinematicPaused(IsPaused);
        }

        public bool Skip()
        {
            if (!IsPlaying) return false;
            Complete();
            return true;
        }

        public void Cancel()
        {
            bool wasPlaying = IsPlaying;
            IsPlaying = IsPaused = false;
            ClearCg();
            ClearChapterSequence();
            if (wasPlaying && director) director.Stop();
            Cleanup(true);
        }

        void OnStopped(PlayableDirector _) { if (IsPlaying && !IsCgPlaying && chapterSequence == ChapterSequence.None) Complete(); }

        void Complete()
        {
            if (!IsPlaying) return;
            if(IsCgPlaying){CompleteCg();return;}
            if (chapterSequence != ChapterSequence.None) { CompleteChapterSequence(); return; }
            bool completedMorning = morningSequence;
            // Stop can synchronously invoke OnStopped; clear the guard first.
            IsPlaying = IsPaused = false;
            if (director) director.Stop();
            Cleanup(false);
            Completed?.Invoke(completedMorning);
        }

        void Cleanup(bool restorePlayer)
        {
            if (sun)
            {
                sun.intensity = savedSunIntensity;
                sun.color = savedSunColor;
                sun.transform.rotation = savedSunRotation;
            }
            sun = null;
            // Catch can be parented to the permanent character basket, outside effectsRoot.
            if (catchRoot) { catchRoot.gameObject.SetActive(false); Destroy(catchRoot.gameObject); }
            if (effectsRoot) { effectsRoot.gameObject.SetActive(false); Destroy(effectsRoot.gameObject); }
            foreach (Material material in temporaryMaterials) if (material) Destroy(material);
            temporaryMaterials.Clear();
            storyAnimatorSpeeds.Clear();
            effectsRoot = gearRoot = netRoot = catchRoot = null;
            windowLight = null; gulls = null;
            if (view) view.EndCinematic(restorePlayer);
            CurrentCaption = CurrentShot = "";
        }

        void OnDisable() { Cancel(); }
        void OnDestroy()
        {
            Cancel();
            if (subscribedDirector) subscribedDirector.stopped -= OnStopped;
        }

        void SaveLighting()
        {
            foreach (Light candidate in FindObjectsByType<Light>())
                if (candidate.type == LightType.Directional) { sun = candidate; break; }
            if (!sun) return;
            savedSunIntensity = sun.intensity;
            savedSunColor = sun.color;
            savedSunRotation = sun.transform.rotation;
        }

        public void EvaluateTimelineShot(HabitatDockBeatClip shot, float progress)
        {
            if (!IsPlaying || !shot || !view || !dockModel) return;
            float t = Mathf.Clamp01(progress);
            bool morning = shot.beat == HabitatDockBeat.MorningWindow;
            CurrentShot = shot.beat.ToString();
            CurrentCaption = shot.caption;
            Vector3 local = shot.SampleActor(t);
            Vector3 position = dockModel.TransformPoint(local);
            if (!morning)
            {
                Vector3 tangent = shot.SampleActor(Mathf.Min(1f, t + .015f)) - shot.SampleActor(Mathf.Max(0f, t - .015f));
                if (tangent.sqrMagnitude > .00001f)
                {
                    facing = dockModel.TransformDirection(tangent);
                    facing.y = 0f;
                    facing.Normalize();
                }
                // Deck sampling starts below furniture. The boat/boarding section
                // retains its authored path so the actor never jumps onto a roof.
                if (HabitatImportedDockLayout.IsWalkable(local, .25f)) position = DeckPosition(local);
                var boat = dockModel.GetComponent<HabitatHarborBoat>();
                if (boat && boat.Vessel && shot.beat == HabitatDockBeat.BlueSky)
                {
                    position = boat.PassengerPosition; facing = boat.Heading;
                    view.SetPlayerMotion(HabitatPlayerMotion.Ride);
                }
                else if (boat && boat.Vessel && shot.beat == HabitatDockBeat.ReturnToDock)
                {
                    // The delivered boat has a deeper open hull. Cross its actual rim
                    // before following the unchanged authored route on the dock.
                    Vector3[] route = { boat.PassengerPosition, boat.BoardingPosition,
                        dockModel.TransformPoint(HabitatImportedDockLayout.BoardingEnd),
                        DeckPosition(HabitatImportedDockLayout.BoatApproach), DeckPosition(HabitatImportedDockLayout.DockArrival) };
                    position = SampleRoute(route, t);
                    Vector3 boardingTangent = SampleRoute(route, Mathf.Min(1f, t + .01f)) - SampleRoute(route, Mathf.Max(0f, t - .01f));
                    boardingTangent.y = 0;
                    if (boardingTangent.sqrMagnitude > .00001f) facing = boardingTangent.normalized;
                    view.SetPlayerMotion(HabitatPlayerMotion.Walk);
                }
            }
            view.SetCinematicPose(position, facing, !morning);
            if (gearRoot)
            {
                gearRoot.position = position;
                gearRoot.rotation = Quaternion.LookRotation(facing.sqrMagnitude > .001f ? facing : dockModel.forward, Vector3.up);
                gearRoot.gameObject.SetActive(!morning);
            }
            if (netRoot)
            {
                netRoot.gameObject.SetActive(!morning && shot.beat != HabitatDockBeat.WalkToShop
                    && (shot.beat != HabitatDockBeat.StowNet || t < .93f));
                netRoot.localRotation = Quaternion.Euler(0f, 0f, shot.beat == HabitatDockBeat.StowNet ? Mathf.Lerp(-18f, -105f, t) : -18f);
            }
            if (catchRoot) catchRoot.gameObject.SetActive(!morning);
            if (windowLight) windowLight.intensity = morning ? Mathf.Lerp(0f, 2.8f, t) : 0f;
            if (sun && morning)
            {
                Vector3 forward = dockModel.TransformDirection(HabitatImportedDockLayout.WindowForward);
                sun.intensity = Mathf.Lerp(savedSunIntensity * .12f, savedSunIntensity, t);
                sun.color = Color.Lerp(new Color(1f, .72f, .40f), savedSunColor, t);
                sun.transform.rotation = Quaternion.LookRotation(-forward + Vector3.down * Mathf.Lerp(1.7f, .8f, t));
            }
            float scale = Mathf.Abs(dockModel.lossyScale.y);
            if (shot.beat == HabitatDockBeat.BlueSky)
            {
                view.GameCamera.backgroundColor = new Color(.27f, .66f, .90f);
                view.GameCamera.clearFlags = RenderSettings.skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                view.SetCinematicCamera(dockModel.TransformPoint(new Vector3(0f, 12f, -3f)),
                    dockModel.rotation * Quaternion.Euler(8f, 0f, 0f), 4.6f * scale);
                SampleGulls();
            }
            else if (morning)
            {
                Vector3 window = dockModel.TransformPoint(HabitatImportedDockLayout.WindowTarget);
                Vector3 forward = dockModel.TransformDirection(HabitatImportedDockLayout.WindowForward);
                view.SetCinematicCamera(window, Quaternion.LookRotation(-forward + Vector3.down * .16f), 2.15f * scale);
                SampleGulls();
            }
            else
            {
                view.SetCinematicCamera(position + dockModel.forward * .8f * scale,
                    dockModel.rotation * Quaternion.Euler(HabitatView.DefaultCameraPitch, -HabitatView.DefaultCameraYaw, 0f), 5.7f * scale);
                SampleGulls();
            }
        }

        Vector3 DeckPosition(Vector3 normalized)
        {
            Vector3 point = dockModel.TransformPoint(normalized);
            Vector3 rayOrigin = dockModel.TransformPoint(new Vector3(normalized.x, .45f, normalized.z));
            foreach (MeshCollider surface in dockModel.GetComponentsInChildren<MeshCollider>())
                if (surface.Raycast(new Ray(rayOrigin, Vector3.down), out RaycastHit hit, .8f * Mathf.Abs(dockModel.lossyScale.y)) && hit.normal.y > .5f)
                { point.y = hit.point.y + .015f; break; }
            return point;
        }

        void ConfigureGullFlight(TimelineAsset sequence)
        {
            gullFlightStart = 0;
            gullFlightDuration = 3.3;
            foreach (TrackAsset track in sequence.GetOutputTracks())
                if (track is HabitatDockBeatTrack)
                    foreach (TimelineClip clip in track.GetClips())
                        if (clip.asset is HabitatDockBeatClip shot && shot.beat == HabitatDockBeat.BlueSky)
                        { gullFlightStart = clip.start; gullFlightDuration = Math.Max(.1, clip.duration); }
            // The whole flock must clear either edge, including its wings, before the cut.
            gullFlightExtent = 4.6f * view.GameCamera.aspect + 2.5f;
        }

        void SampleGulls()
        {
            if (gulls == null) return;
            // One continuous pass on the sequence clock: individual shots must not
            // rewind the birds or teleport them down into the following dock shot.
            float elapsed = (float)(director.time - gullFlightStart);
            float p = elapsed / (float)gullFlightDuration;
            for (int i = 0; i < gulls.Length; i++)
            {
                Transform gull = gulls[i];
                float x = Mathf.LerpUnclamped(-gullFlightExtent, gullFlightExtent + (gulls.Length - 1) * 1.8f, p) - i * 1.8f;
                gull.localPosition = new Vector3(x, 12f + i * .4f, -5f + i * .55f);
                gull.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 5f + i) * 7f);
                if (proceduralGulls)
                    for (int side = 0; side < 2; side++)
                        gull.GetChild(side).localRotation = Quaternion.Euler(0f, 0f, (side == 0 ? 1f : -1f) * (12f + Mathf.Sin(elapsed * 12f) * 22f));
            }
        }

        void BuildEffects()
        {
            effectsRoot = new GameObject("Main story temporary effects").transform;
            effectsRoot.SetParent(dockModel, false);
            effectsRoot.gameObject.hideFlags = HideFlags.DontSave;
            Material wood = MakeMaterial(new Color(.53f, .32f, .16f));
            Material rim = MakeMaterial(new Color(.79f, .61f, .32f));
            Material white = MakeMaterial(new Color(.96f, .97f, .92f));
            Material fish = MakeMaterial(new Color(.44f, .73f, .77f));
            gearRoot = Child("Catch and landing net", effectsRoot);
            gearRoot.localScale = Vector3.one * (HabitatPlayerVisual.CharacterScale / Mathf.Max(.001f, Mathf.Abs(dockModel.lossyScale.y)));
            Transform basketRim = view.PlayerVisual ? view.PlayerVisual.BackBasketRim : null;
            catchRoot = Child(basketRim ? "Opening catch in back basket" : "Full catch basket", basketRim ? basketRim : gearRoot);
            if (!basketRim)
            {
                catchRoot.localPosition = new Vector3(0f, .88f, -.37f);
                Part("Basket", catchRoot, PrimitiveType.Cube, Vector3.zero, new Vector3(.72f, .53f, .50f), wood);
                Part("Basket rim", catchRoot, PrimitiveType.Cube, new Vector3(0f, .28f, 0f), new Vector3(.79f, .07f, .56f), rim);
            }
            for (int i = 0; i < 4; i++)
                Part("Fish " + i, catchRoot, PrimitiveType.Sphere,
                    basketRim ? new Vector3(-.18f + i * .12f, .005f + i % 2 * .025f, 0f)
                        : new Vector3(-.23f + i * .15f, .34f + i % 2 * .04f, 0f),
                    basketRim ? new Vector3(.095f, .075f, .19f) : new Vector3(.12f, .10f, .31f), fish);
            netRoot = Child("Landing net", gearRoot);
            netRoot.localPosition = new Vector3(.55f, .75f, 0f);
            GameObject netAsset = landingNetPrefab ? landingNetPrefab : Resources.Load<GameObject>("Models/Story/LandingNet");
            if (netAsset)
            {
                GameObject art = InstantiateStoryVisual(netAsset, netRoot, "Landing net art");
                art.AddComponent<HabitatLandingNetGrip>().Bind(view.PlayerVisual);
            }
            else
            {
                Part("Net handle", netRoot, PrimitiveType.Cylinder, new Vector3(0f, .27f, 0f), new Vector3(.045f, .5f, .045f), wood);
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.PI / 8f;
                    Transform ring = Part("Net hoop", netRoot, PrimitiveType.Cube, new Vector3(Mathf.Cos(angle) * .32f, 1.02f + Mathf.Sin(angle) * .32f, 0f), new Vector3(.14f, .025f, .025f), rim);
                    ring.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f);
                }
                for (int i = -2; i <= 2; i++)
                {
                    float span = Mathf.Sqrt(.32f * .32f - i * i * .01f) * 2f;
                    Part("Net cord", netRoot, PrimitiveType.Cube, new Vector3(i * .1f, 1.02f, .012f), new Vector3(.012f, span, .012f), white);
                    Part("Net cord", netRoot, PrimitiveType.Cube, new Vector3(0f, 1.02f + i * .1f, .012f), new Vector3(span, .012f, .012f), white);
                }
            }
            GameObject gullAsset = seabirdPrefab ? seabirdPrefab : Resources.Load<GameObject>("Models/Story/Seabird");
            proceduralGulls = !gullAsset;
            gulls = new Transform[3];
            for (int i = 0; i < gulls.Length; i++)
            {
                Transform gull = gulls[i] = Child("Seabird " + (i + 1), effectsRoot);
                if (gullAsset) { InstantiateStoryVisual(gullAsset, gull, "Seabird art"); continue; }
                Part("Left wing", gull, PrimitiveType.Cube, new Vector3(-.3f, 0f, 0f), new Vector3(.62f, .055f, .20f), white);
                Part("Right wing", gull, PrimitiveType.Cube, new Vector3(.3f, 0f, 0f), new Vector3(.62f, .055f, .20f), white);
                Part("Body", gull, PrimitiveType.Sphere, Vector3.zero, new Vector3(.24f, .17f, .42f), white);
                Part("Beak", gull, PrimitiveType.Cube, new Vector3(.18f, 0f, 0f), new Vector3(.16f, .07f, .09f), rim);
            }
            windowLight = new GameObject("Morning light at actual home window", typeof(Light)).GetComponent<Light>();
            windowLight.transform.SetParent(effectsRoot, false);
            windowLight.transform.localPosition = HabitatImportedDockLayout.WindowTarget + HabitatImportedDockLayout.WindowForward * .8f;
            windowLight.type = LightType.Point;
            windowLight.range = 5f * Mathf.Abs(dockModel.lossyScale.y);
            windowLight.color = new Color(1f, .80f, .46f);
            windowLight.intensity = 0f;
        }

        Material MakeMaterial(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .15f);
            temporaryMaterials.Add(material); return material;
        }

        static Transform Child(string name, Transform parent)
        {
            Transform child = new GameObject(name).transform; child.SetParent(parent, false); return child;
        }

        static Transform Part(string name, Transform parent, PrimitiveType kind, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(kind);
            part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = part.GetComponent<Collider>();
            if (collider) { collider.enabled = false; Destroy(collider); }
            return part.transform;
        }

        public void GatherTimelineProperties(IPropertyCollector collector)
        {
            if (view && view.GameCamera)
            {
                collector.AddFromComponent(view.GameCamera.gameObject, view.GameCamera);
                collector.AddFromComponent(view.GameCamera.gameObject, view.GameCamera.transform);
            }
            if (view && view.PlayerVisual) collector.AddFromComponent(view.PlayerVisual.gameObject, view.PlayerVisual.transform);
            if (sun) { collector.AddFromComponent(sun.gameObject, sun); collector.AddFromComponent(sun.gameObject, sun.transform); }
        }
    }
}
