using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HabitatIsland
{
    public enum HabitatDockInteraction { None, Home, Shop, Boat }

    /// <summary>Independent harbor prototype. It deliberately does not mutate island ecology rules.</summary>
    [DisallowMultipleComponent]
    public sealed partial class HabitatDockPrototype : MonoBehaviour, IHabitatDockTimelineTarget
    {
        public const string HarborSceneName = "HarborPrototype";
        public const string IslandSceneName = "HabitatIsland";
        public static readonly Vector3 HomeApproach = HabitatImportedDockLayout.HomeApproach;
        public static readonly Vector3 ShopApproach = HabitatImportedDockLayout.ShopApproach;
        public static readonly Vector3 BoatApproach = HabitatImportedDockLayout.BoatApproach;
        public static readonly Vector3 BoatArrival = HabitatImportedDockLayout.BoatArrival;
        public static readonly Vector3 DockArrival = HabitatImportedDockLayout.DockArrival;

        [Header("Editable Timeline sequences")]
        public TimelineAsset openingTimeline;
        public TimelineAsset morningTimeline;
        public PlayableDirector director;
        [Header("Scene references — populated by Harbor Prototype Setup")]
        public Camera harborCamera;
        public Light sun;
        [SerializeField] Transform environmentRoot, playerRoot, gearRoot, netRoot, catchRoot, boatRoot;
        [SerializeField] GameObject editorTravelerPreview, morningPatch;
        [SerializeField] Light windowLight;
        [SerializeField] Transform[] gulls;
        [SerializeField] Transform[] worldLabels;
        [SerializeField] Material[] palette;
        [SerializeField] MeshCollider harborSurface;
        [SerializeField] TMP_FontAsset chineseFont;
        [Header("Movement")]
        [Range(1f, 8f)] public float walkSpeed = 2.5f;
        [Range(.15f, .6f)] public float playerRadius = .32f;
        [Range(1f, 3f)] public float interactionDistance = 1.85f;

        public int Day { get; private set; } = 1;
        public bool HasCatch { get; private set; } = true;
        public int DeliveredBatches { get; private set; }
        public bool IntroCompleted { get; private set; }
        public bool IsSequencePlaying { get; private set; }
        public bool IsSequencePaused { get; private set; }
        public HabitatDockInteraction Modal { get; private set; }
        public HabitatDockInteraction NearbyInteraction => FindNearbyInteraction();
        public bool HasControl => initialized && !IsSequencePlaying && Modal == HabitatDockInteraction.None;
        public Vector3 PlayerPosition => playerRoot ? playerRoot.position : Vector3.zero;
        public HabitatPlayerVisual PlayerVisual => playerVisual;
        public string CurrentShot { get; private set; } = "自由行动";
        public string CurrentCaption { get; private set; } = "";

        HabitatPlayerVisual playerVisual;
        Vector3 facing = Vector3.forward;
        float cameraYaw = -HabitatView.DefaultCameraYaw, messageUntil, stepDistance;
        string message = "";
        bool initialized, morningSequence, leavingScene;
        static DockSession session;

        sealed class DockSession
        {
            public int day = 1, delivered;
            public bool hasCatch = true, introCompleted;
            public Vector3 position = BoatApproach;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPlaySession() { session = null; }

        void Awake()
        {
            HabitatAudio.Ensure();
            if (!harborCamera) harborCamera = Camera.main;
            if (!director) director = GetComponent<PlayableDirector>();
            if (!director) director = gameObject.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.None;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            director.stopped += OnDirectorStopped;
            if (!environmentRoot) BuildPrototypeWorld();
            if (editorTravelerPreview)
            { editorTravelerPreview.SetActive(false); Destroy(editorTravelerPreview); }
            playerVisual = playerRoot.GetComponent<HabitatPlayerVisual>();
            if (!playerVisual) playerVisual = playerRoot.gameObject.AddComponent<HabitatPlayerVisual>();
            if (!playerVisual.Initialize()) Debug.LogError("[Harbor] MeshyTraveler prefab is missing.");
            ApplyLandingNetArt();
            BindCatchBasket();
            if (!chineseFont) chineseFont = Resources.Load<TMP_FontAsset>("Fonts/HabitatChinese");
            if (chineseFont) chineseFont.getFontFeatures = false;
            session = session ?? new DockSession();
            Day = session.day; HasCatch = session.hasCatch;
            DeliveredBatches = session.delivered; IntroCompleted = session.introCompleted;
            playerRoot.position = GroundPosition(IsWalkable(session.position) ? session.position : BoatApproach);
            SetCatchVisible();
            netRoot.gameObject.SetActive(false);
            BuildHud();
            initialized = true;
            SetFreeCamera(true);
        }

        void Start()
        {
            if (!IntroCompleted) BeginSequence(false);
            else Notify("回到码头。家、商店和小船都可以靠近后按 E 互动。");
        }

        void OnDestroy()
        {
            HabitatAudio.ClearTransient(); HabitatAudio.SetDucked(false);
            if (initialized) SaveSession();
            if (director) director.stopped -= OnDirectorStopped;
            ReleaseCatchContents();
        }

        void Update()
        {
            if (!initialized || leavingScene) return;
            Keyboard keys = Keyboard.current;
            if (IsSequencePlaying)
            {
                if (keys != null && (keys.spaceKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame))
                    ToggleSequencePause();
            }
            else if (Modal != HabitatDockInteraction.None)
            {
                if (keys != null && keys.escapeKey.wasPressedThisFrame) CloseInteractionFromInput();
            }
            else if (keys != null)
            {
                if (keys.qKey.wasPressedThisFrame) cameraYaw -= 45f;
                if (keys.cKey.wasPressedThisFrame) cameraYaw += 45f;
                Vector2 axes = new Vector2((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
                    (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0));
                MoveByScreen(axes, Time.unscaledDeltaTime);
                if (keys.eKey.wasPressedThisFrame) TryInteract();
            }
            if (!IsSequencePlaying)
            {
                SampleGulls(Mathf.Repeat(Time.unscaledTime / 18f, 1f), false);
                if (boatRoot) boatRoot.localPosition = new Vector3(2.3f, -.16f + Mathf.Sin(Time.unscaledTime * 1.3f) * .035f, -7.5f);
            }
            RefreshHud();
            HabitatAudio.SetDucked(Modal!=HabitatDockInteraction.None);
        }

        void LateUpdate()
        {
            if (!initialized) return;
            if (playerVisual)
            {
                if (playerVisual.Animator) playerVisual.Animator.speed = IsSequencePaused ? 0f : 1f;
                if (!IsSequencePaused) playerVisual.Render(Time.unscaledDeltaTime, facing);
            }
            if (!IsSequencePlaying) SetFreeCamera(false);
            if (gearRoot && facing.sqrMagnitude > .001f) gearRoot.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0, facing.z));
            if (worldLabels != null && harborCamera)
                foreach (Transform label in worldLabels) if (label) label.rotation = harborCamera.transform.rotation;
        }

        /// <summary>Screen-relative movement shared by input and smoke checks. No motion during a shot or modal.</summary>
        public bool MoveByScreen(Vector2 axes, float seconds)
        {
            if (!HasControl || !float.IsFinite(seconds) || !float.IsFinite(axes.x) || !float.IsFinite(axes.y)) return false;
            axes = Vector2.ClampMagnitude(axes, 1f);
            Quaternion yaw = Quaternion.Euler(0, cameraYaw, 0);
            Vector3 forward = harborCamera ? harborCamera.transform.forward : yaw * Vector3.forward;
            Vector3 right = harborCamera ? harborCamera.transform.right : yaw * Vector3.right;
            forward.y = 0; right.y = 0;
            Vector3 movement = (right.normalized * axes.x + forward.normalized * axes.y) * (walkSpeed * Mathf.Clamp(seconds, 0, .1f));
            Vector3 before = playerRoot.position;
            // Small swept steps with independent-axis sliding prevent crossing a building or a narrow pier edge.
            int count = Mathf.Max(1, Mathf.CeilToInt(movement.magnitude / .08f));
            Vector3 step = movement / count;
            for (int i = 0; i < count; i++)
            {
                Vector3 next = playerRoot.position + step;
                if (IsWalkable(next)) playerRoot.position = next;
                else
                {
                    next = playerRoot.position + new Vector3(step.x, 0, 0);
                    if (IsWalkable(next)) playerRoot.position = next;
                    next = playerRoot.position + new Vector3(0, 0, step.z);
                    if (IsWalkable(next)) playerRoot.position = next;
                }
            }
            playerRoot.position = GroundPosition(playerRoot.position);
            Vector3 moved = playerRoot.position - before;
            moved.y = 0;
            if (moved.sqrMagnitude > .000001f)
            {
                facing = moved.normalized;
                stepDistance+=moved.magnitude;
                if(stepDistance>=.65f)
                {stepDistance%=.65f;HabitatAudio.PlayAction(HabitatActionCue.StepWood);}
            }
            return moved.sqrMagnitude > .000001f;
        }

        public bool IsWalkable(Vector3 position)
        {
            return HabitatImportedDockLayout.IsWalkable(position, playerRadius);
        }

        Vector3 GroundPosition(Vector3 point)
        {
            // Sample below furniture/roof height, so the original uneven planks
            // support feet without turning tables or roofs into walking surfaces.
            if (harborSurface && harborSurface.Raycast(new Ray(new Vector3(point.x, .45f, point.z), Vector3.down), out RaycastHit hit, .8f)
                && hit.normal.y > .5f) point.y = hit.point.y + .012f;
            else point.y = 0f;
            return point;
        }

        HabitatDockInteraction FindNearbyInteraction()
        {
            if (!initialized || IsSequencePlaying) return HabitatDockInteraction.None;
            float best = interactionDistance * interactionDistance;
            HabitatDockInteraction found = HabitatDockInteraction.None;
            foreach (HabitatDockInteraction kind in new[] { HabitatDockInteraction.Home, HabitatDockInteraction.Shop, HabitatDockInteraction.Boat })
            {
                float distance = (PlayerPosition - Approach(kind)).sqrMagnitude;
                if (distance <= best) { found = kind; best = distance; }
            }
            return found;
        }

        public static Vector3 Approach(HabitatDockInteraction interaction)
            => interaction == HabitatDockInteraction.Home ? HomeApproach
                : interaction == HabitatDockInteraction.Shop ? ShopApproach : BoatApproach;

        public bool TryInteract() => TryOpenInteraction(FindNearbyInteraction());

        public bool TryOpenInteraction(HabitatDockInteraction interaction)
        {
            if (!HasControl || interaction == HabitatDockInteraction.None || FindNearbyInteraction() != interaction) return false;
            Modal = interaction;
            RefreshModal();
            HabitatAudio.Play(HabitatAudioCue.Open);
            return true;
        }

        public void CloseInteractionFromInput()
        {
            if(Modal==HabitatDockInteraction.None)return;
            HabitatAudio.Play(HabitatAudioCue.Close);
            CloseInteraction();
        }

        public void CloseInteraction()
        {
            Modal = HabitatDockInteraction.None;
            if (modalRoot) modalRoot.SetActive(false);
        }

        public bool TryDeliverCatch()
        {
            if (IsSequencePlaying || Modal != HabitatDockInteraction.Shop || FindNearbyInteraction() != Modal || !HasCatch) return false;
            HasCatch = false; DeliveredBatches++;
            SetCatchVisible(); SaveSession(); RefreshModal();
            HabitatAudio.PlayAction(HabitatActionCue.Trade);
            Notify("已交付这一筐渔获。交付记录已更新。");
            return true;
        }

        public bool TryRest()
        {
            if (IsSequencePlaying || Modal != HabitatDockInteraction.Home || FindNearbyInteraction() != Modal) return false;
            CloseInteraction();
            BeginSequence(true);
            return true;
        }

        public bool TrySailToIsland() => TrySailToIsland(mapSelection ? mapSelection.SelectedLevel : HabitatFigJamLevel.Index);

        public bool TrySailToIsland(int levelIndex)
        {
            if (IsSequencePlaying || Modal != HabitatDockInteraction.Boat || FindNearbyInteraction() != Modal || leavingScene) return false;
            if (levelIndex < 0 || levelIndex >= HabitatLevels.Names.Length) return false;
            if (!Application.CanStreamedLevelBeLoaded(IslandSceneName))
            { Notify("正式岛屿场景未加入场景列表，请先运行码头场景创建工具。"); return false; }
            CloseInteraction();
            playerRoot.position = BoatApproach;
            SaveSession(); leavingScene = true;
            HabitatAudio.ClearTransient(); stepDistance=0f;
            HabitatGame.TravelToLevel(levelIndex);
            return true;
        }

        void BeginSequence(bool morning)
        {
            HabitatAudio.ClearTransient(); stepDistance=0f;
            CloseInteraction();
            morningSequence = morning;
            IsSequencePlaying = true; IsSequencePaused = false;
            SetCatchVisible();
            TimelineAsset sequence = morning ? morningTimeline : openingTimeline;
            if (!sequence || sequence.duration <= 0)
            {
                Debug.LogError("[Harbor] Missing Timeline sequence. Run HabitatDockPrototypeSetup.CreateScene().");
                CompleteSequence(); return;
            }
            director.playableAsset = sequence;
            foreach (TrackAsset track in sequence.GetOutputTracks())
                if (track is HabitatDockBeatTrack) director.SetGenericBinding(track, this);
            director.time = 0;
            director.Play();
            director.Evaluate();
            RefreshHud();
        }

        public void ToggleSequencePause()
        {
            if (!IsSequencePlaying) return;
            IsSequencePaused = !IsSequencePaused;
            if (IsSequencePaused) director.Pause(); else director.Resume();
            if (playerVisual && playerVisual.Animator) playerVisual.Animator.speed = IsSequencePaused ? 0f : 1f;
            RefreshHud();
        }

        public bool SkipSequence()
        {
            if (!IsSequencePlaying) return false;
            CompleteSequence();
            return true;
        }

        void OnDirectorStopped(PlayableDirector stopped) { if (IsSequencePlaying) CompleteSequence(); }

        void CompleteSequence()
        {
            // Set the guard before Stop: its callback is synchronous and must not advance the day twice.
            if (!IsSequencePlaying) return;
            IsSequencePlaying = false; IsSequencePaused = false;
            director.Stop();
            if (morningSequence)
            {
                Day++;
                playerRoot.position = GroundPosition(HomeApproach);
                Notify("第 " + Day + " 天。阳光照进窗台，可以自由行动了。");
            }
            else
            {
                IntroCompleted = true;
                playerRoot.position = GroundPosition(ShopApproach);
                Notify("已回到商店前。按 E 交付渔获，之后可以回家休息。");
            }
            playerRoot.gameObject.SetActive(true);
            netRoot.gameObject.SetActive(false);
            SetCatchVisible();
            if (morningPatch) morningPatch.SetActive(false);
            if (windowLight) windowLight.intensity = 0;
            if (sun) { sun.intensity = 1.35f; sun.transform.rotation = Quaternion.Euler(48, -35, 0); }
            CurrentShot = "自由行动"; CurrentCaption = "";
            cameraYaw = -HabitatView.DefaultCameraYaw; facing = Vector3.back;
            if (playerVisual)
            {
                if (playerVisual.Animator) playerVisual.Animator.speed = 1;
                playerVisual.ResetVisual(facing);
            }
            SetFreeCamera(true); SaveSession(); RefreshHud();
        }

        /// <summary>Explicit replay/reset for inspection. Ordinary scene return never invokes this.</summary>
        public bool RestartFirstDayForChecks()
        {
            if (!Application.isPlaying || !initialized) return false;
            IsSequencePlaying = false; director.Stop();
            Day = 1; DeliveredBatches = 0; HasCatch = true; IntroCompleted = false;
            SaveSession(); BeginSequence(false); return true;
        }

        /// <summary>Controlled smoke-test positioning. Cannot bypass movement locking or place the actor in a wall.</summary>
        public bool PlacePlayerForChecks(Vector3 position)
        {
            if (!HasControl || !IsWalkable(position)) return false;
            stepDistance=0f;
            playerRoot.position = GroundPosition(position);
            if (playerVisual) playerVisual.ResetVisual(facing);
            SetFreeCamera(true); return true;
        }

        void SaveSession()
        {
            session = session ?? new DockSession();
            session.day = Day; session.hasCatch = HasCatch; session.delivered = DeliveredBatches;
            session.introCompleted = IntroCompleted;
            session.position = IsWalkable(PlayerPosition) ? PlayerPosition : BoatApproach;
        }

        void SetCatchVisible()
        {
            if (catchRoot) catchRoot.gameObject.SetActive(HasCatch && IsSequencePlaying && !morningSequence);
        }
        void Notify(string text) { message = text; messageUntil = Time.unscaledTime + 6f; }

        public void EvaluateTimelineShot(HabitatDockBeatClip shot, float progress)
        {
            if (!playerRoot || !harborCamera) return;
            float t = Mathf.Clamp01(progress);
            CurrentShot = shot.beat.ToString(); CurrentCaption = shot.caption;
            bool morning = shot.beat == HabitatDockBeat.MorningWindow;
            playerRoot.gameObject.SetActive(!morning);
            if (morningPatch) morningPatch.SetActive(morning);
            if (windowLight) windowLight.intensity = morning ? Mathf.Lerp(0, 2.8f, t) : 0;
            if (sun)
            {
                sun.intensity = morning ? Mathf.Lerp(.15f, 1.35f, t) : 1.35f;
                sun.transform.rotation = morning ? Quaternion.Euler(Mathf.Lerp(75, 48, t), -35, 0) : Quaternion.Euler(48, -35, 0);
            }
            netRoot.gameObject.SetActive(shot.beat == HabitatDockBeat.BlueSky || shot.beat == HabitatDockBeat.ReturnToDock
                || (shot.beat == HabitatDockBeat.StowNet && t < .93f));
            netRoot.localRotation = Quaternion.Euler(0, 0, shot.beat == HabitatDockBeat.StowNet ? Mathf.Lerp(-18, -105, t) : -18);
            if (catchRoot) catchRoot.gameObject.SetActive(!morning && (!Application.isPlaying || (HasCatch && IsSequencePlaying)));
            if (!morning)
            {
                Vector3 sampled = shot.SampleActor(t);
                playerRoot.position = IsWalkable(sampled) ? GroundPosition(sampled) : sampled;
                Vector3 tangent = shot.SampleActor(Mathf.Min(1f, t + .015f)) - shot.SampleActor(Mathf.Max(0, t - .015f));
                if (tangent.sqrMagnitude > .00001f) facing = tangent.normalized;
                if (!Application.isPlaying && editorTravelerPreview && facing.sqrMagnitude > .001f)
                    editorTravelerPreview.transform.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0, facing.z));
                if (gearRoot && facing.sqrMagnitude > .001f) gearRoot.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0, facing.z));
            }
            if (shot.beat == HabitatDockBeat.BlueSky)
            {
                SetCamera(new Vector3(0, 6.4f, 0), Quaternion.Euler(8, 0, 0), 5.3f);
                SampleGulls(t, true);
            }
            else if (morning)
            {
                SetCamera(HabitatImportedDockLayout.WindowTarget,
                    Quaternion.LookRotation(-HabitatImportedDockLayout.WindowForward + Vector3.down * .16f), 2.15f);
                if (morningPatch)
                {
                    morningPatch.transform.localScale = new Vector3(Mathf.Lerp(.05f, 2.5f, t), .018f, Mathf.Lerp(.08f, 1.9f, t));
                    morningPatch.transform.localPosition = new Vector3(-6.25f, .04f, 2.45f);
                }
                SampleGulls(t, false);
            }
            else
            {
                SetCamera(playerRoot.position + Vector3.forward * .8f, Quaternion.Euler(HabitatView.DefaultCameraPitch, -HabitatView.DefaultCameraYaw, 0), 6.7f);
                SampleGulls(t, false);
            }
        }

        void SetFreeCamera(bool snap)
        {
            if (!harborCamera || !playerRoot) return;
            Vector3 target = playerRoot.position + Vector3.forward * .7f;
            Quaternion rotation = Quaternion.Euler(HabitatView.DefaultCameraPitch, cameraYaw, 0);
            Vector3 desired = target - rotation * Vector3.forward * 23f;
            float blend = snap ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime * 8f);
            harborCamera.transform.SetPositionAndRotation(Vector3.Lerp(harborCamera.transform.position, desired, blend),
                Quaternion.Slerp(harborCamera.transform.rotation, rotation, blend));
            harborCamera.orthographicSize = Mathf.Lerp(harborCamera.orthographicSize, 7.5f, blend);
        }

        void SetCamera(Vector3 target, Quaternion rotation, float size)
        {
            harborCamera.orthographic = true;
            harborCamera.orthographicSize = size;
            harborCamera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * 23f, rotation);
        }

        void SampleGulls(float progress, bool close)
        {
            if (gulls == null) return;
            for (int i = 0; i < gulls.Length; i++)
            {
                if (!gulls[i]) continue;
                float p = Mathf.Repeat(progress + i * .11f, 1f);
                gulls[i].localPosition = new Vector3(Mathf.Lerp(-12, 12, p), close ? 6.2f + i * .46f : 5.5f + i * .4f,
                    close ? -5.8f + i * .6f : -1.8f + i);
                gulls[i].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(p * Mathf.PI * 16f + i) * 8f);
                for (int j = 0; j < 2 && j < gulls[i].childCount; j++)
                    gulls[i].GetChild(j).localRotation = Quaternion.Euler(0, 0,
                        (j == 0 ? 1 : -1) * (12f + Mathf.Sin(p * Mathf.PI * 20f) * 20f));
            }
        }

        /// <summary>Timeline preview registers every changed scene property for editor restoration.</summary>
        public void GatherTimelineProperties(IPropertyCollector collector)
        {
            if (environmentRoot)
                foreach (Transform item in environmentRoot.GetComponentsInChildren<Transform>(true))
                {
                    collector.AddFromComponent(item.gameObject, item);
                    collector.AddFromName(item.gameObject, "m_IsActive");
                }
            if (harborCamera)
            {
                collector.AddFromComponent(harborCamera.gameObject, harborCamera.transform);
                collector.AddFromComponent(harborCamera.gameObject, harborCamera);
            }
            if (sun)
            {
                collector.AddFromComponent(sun.gameObject, sun);
                collector.AddFromComponent(sun.gameObject, sun.transform);
            }
            if (windowLight) collector.AddFromComponent(windowLight.gameObject, windowLight);
        }

        [Serializable]
        public sealed class Inspection
        {
            public int day, deliveredBatches, openingTracks, openingClips, morningClips;
            public bool hasCatch, introCompleted, hasControl, sequencePlaying, sequencePaused, hasTraveler, rootMotion, chineseFont;
            public string shot, modal, nearby, scene;
            public Vector3 player;
            public double timelineTime, timelineDuration;
        }

        public string GetStatusJson()
        {
            int tracks = 0, clips = 0, mornings = 0;
            if (openingTimeline) foreach (TrackAsset track in openingTimeline.GetOutputTracks())
            { tracks++; foreach (TimelineClip clip in track.GetClips()) clips++; }
            if (morningTimeline) foreach (TrackAsset track in morningTimeline.GetOutputTracks())
                foreach (TimelineClip clip in track.GetClips()) mornings++;
            return JsonUtility.ToJson(new Inspection {
                day = Day, deliveredBatches = DeliveredBatches, hasCatch = HasCatch, introCompleted = IntroCompleted,
                hasControl = HasControl, sequencePlaying = IsSequencePlaying, sequencePaused = IsSequencePaused,
                hasTraveler = playerVisual ? playerVisual.IsModelActive : editorTravelerPreview != null,
                rootMotion = playerVisual && playerVisual.Animator && playerVisual.Animator.applyRootMotion,
                chineseFont = chineseFont != null, shot = CurrentShot, modal = Modal.ToString(), nearby = NearbyInteraction.ToString(),
                player = PlayerPosition, timelineTime = director ? director.time : 0,
                timelineDuration = director && director.playableAsset ? director.duration : 0,
                openingTracks = tracks, openingClips = clips, morningClips = mornings, scene = gameObject.scene.name
            }, true);
        }
    }
}
