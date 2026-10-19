using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HabitatIsland
{
    /// <summary>Small hold/release fishing challenge. Only successful catches call the game's existing harvest transaction.</summary>
    public sealed class HabitatFishing : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        public float Progress { get; private set; }
        public float Bar { get; private set; }
        public float Fish { get; private set; }
        public float BarSize = .29f;
        public float CatchGainPerSecond = .19f;
        public float EscapeLossPerSecond = .12f;
        public float LiftAcceleration = 2.6f;
        public float Gravity = 1.9f;
        HabitatGame game;
        int targetX, targetZ, targetId;
        Species targetSpecies;
        bool priorPaused;
        float elapsed, velocity, phase, nextText, castElapsed;
        public const float CastDuration = 1.25f;
        public bool IsCasting => IsOpen && castElapsed < CastDuration;
        Canvas canvas;
        RectTransform root, track, catchBar, fishMarker, progressFill;
        UnityEngine.UI.Image barImage;
        UnityEngine.UI.RawImage finalCatchZone;
        TMP_Text progressLabel, feedback, catchDescription;
        TMP_FontAsset font;
        HabitatUiRing progressRing;
        const float HorizontalWidth=800;
        const float TrackHeight = 272f;
        const float ProgressWidth = 548f;
        readonly Color ink = new Color32(23,61,58,255);
        readonly Color paper = new Color32(248,250,247,255);
        readonly Color muted = new Color32(97,117,111,255);
        readonly Color surface = new Color32(232,239,235,255);
        readonly Color mint = new Color32(145,185,160,255);
        readonly Color ochre = new Color32(216,117,88,255);
        readonly Color successText = new Color32(36,93,80,255);
        readonly Color warningText = new Color32(161,78,54,255);

        public bool Begin(HabitatGame controller, int x, int z)
        {
            if (IsOpen || controller == null || controller.State == null) return false;
            HabitatState s = controller.State;
            if(controller.Busy || !HabitatRules.Inside(s,x,z))return false;
            Animal target=s.animals.Find(a=>!a.carried&&a.x==x&&a.z==z&&(a.species==Species.Goldfish||a.species==Species.Jellyfish));
            if(!HabitatRules.CanHarvest(s,target,controller.tuning,out string reason))
            { HabitatRules.Log(s,reason);return false; }
            targetId=target.id;targetSpecies=target.species;
            game = controller; targetX = x; targetZ = z;
            priorPaused = game.paused; game.paused = true;
            Progress = .35f; Bar = .42f; Fish = .53f; velocity = 0; elapsed = 0;
            castElapsed = 0;
            phase = (target.id % 7) * .29f; nextText = 0;
            if (root == null) BuildUI();
            root.gameObject.SetActive(true); IsOpen = true;
            HabitatAudio.PlayAction(HabitatActionCue.FishingCast);
            game.PlayToolVisual("Cast", x, z);
            controller.View.PlayerVisual.SetFishingPose(1,0,new Vector3(x,HabitatRules.WaterSurfaceY(s.waterLevel)+.06f,z));
            RefreshUI(true);
            root.gameObject.SetActive(false);
            return true;
        }

        void Update()
        {
            if (!IsOpen || game && game.BackgroundSuspended) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Cancel(); return; }
            bool held = keyboard != null && keyboard.spaceKey.isPressed;
            held |= Mouse.current != null && Mouse.current.leftButton.isPressed;
            Advance(Mathf.Min(Time.unscaledDeltaTime, .1f), held);
        }

        /// <summary>Deterministic simulation entry point for smoke tests, independent of input devices and timescale.</summary>
        public void Advance(float dt, bool held)
        {
            if (!IsOpen || game && game.BackgroundSuspended || dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            if (IsCasting)
            {
                castElapsed = Mathf.Min(CastDuration, castElapsed + Mathf.Min(dt,.1f));
                game.View.PlayerVisual.SetFishingPose(1,castElapsed/CastDuration,new Vector3(targetX,HabitatRules.WaterSurfaceY(game.State.waterLevel)+.06f,targetZ));
                if(castElapsed>=CastDuration)root.gameObject.SetActive(true);
                return;
            }
            game.View.PlayerVisual.SetFishingPose(2,elapsed,new Vector3(targetX,HabitatRules.WaterSurfaceY(game.State.waterLevel)+.06f,targetZ),held);
            float remaining = Mathf.Min(dt, 1f);
            while (remaining > .00001f && IsOpen)
            {
                float step = Mathf.Min(remaining, 1f / 120f); remaining -= step; elapsed += step;
                velocity += (held ? LiftAcceleration : -Gravity) * step;
                velocity = Mathf.Clamp(velocity, -.78f, .78f);
                Bar += velocity * step;
                float half = Mathf.Clamp(BarSize, .12f, .8f) * .5f;
                if (Bar < half) { Bar = half; velocity = 0; }
                if (Bar > 1f - half) { Bar = 1f - half; velocity = 0; }
                Fish = Mathf.Clamp(.50f + .27f * Mathf.Sin(elapsed * 1.12f + phase) + .12f * Mathf.Sin(elapsed * 2.34f + phase * .6f), .07f, .93f);
                bool overlap = Mathf.Abs(Fish - Bar) <= half - .025f;
                // A brief bite window gives the player time to react to the modal opening.
                if (elapsed > .45f) Progress = Mathf.Clamp01(Progress + step * (overlap ? CatchGainPerSecond : -EscapeLossPerSecond));
                if (Progress >= 1f) { Finish(true); break; }
                if (Progress <= 0f || elapsed >= 35f) { Finish(false); break; }
            }
            if (IsOpen) RefreshUI(false);
        }

        public void Cancel(bool audible=true)
        {
            if (!IsOpen) return;
            HabitatGame owner = game;
            if(audible)HabitatAudio.PlayAction(HabitatActionCue.FishingReel);
            Close();
            if (owner != null && owner.State != null) HabitatRules.Log(owner.State, "已收竿：未扣除渔获，可以再次尝试");
        }

        void Finish(bool success)
        {
            HabitatGame owner = game;
            Close();
            if (owner == null || owner.State == null) return;
            if (success) owner.CompleteFishing(targetX, targetZ, targetSpecies, targetId);
            else {HabitatRules.Log(owner.State, "脱钩了：未扣除渔获，调整按住与松开的节奏再试一次");HabitatAudio.PlayAction(HabitatActionCue.FishingMiss);}
        }

        void Close()
        {
            IsOpen = false;
            if (game && game.View && game.View.PlayerVisual) game.View.PlayerVisual.CancelEquipmentAction();
            if (root != null) root.gameObject.SetActive(false);
            if (game != null) game.paused = priorPaused;
        }

        void OnDisable() { if (IsOpen) Close(); }

        void BuildUI()
        {
            font = HabitatUiAssets.Body;
            if (font == null) font = TMP_Settings.defaultFontAsset;
            if (font != null) font.getFontFeatures = false;
            GameObject canvasObject = new GameObject("Fishing Modal Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            root = MakeRect("Fishing input shield", canvasObject.transform, Vector2.zero, Vector2.zero);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            UnityEngine.UI.Image shade = root.gameObject.AddComponent<UnityEngine.UI.Image>(); shade.color = new Color(.06f, .15f, .14f, .46f); shade.raycastTarget = true;
            RectTransform card = Panel("Fishing card", root, Vector2.zero, new Vector2(620, 552), paper, true);
            UnityEngine.UI.Shadow shadow = card.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(.04f, .12f, .10f, .16f); shadow.effectDistance = new Vector2(0, -5); shadow.useGraphicAlpha = true;
            Label(card, "跟上小鱼的节奏", 26, ink, new Vector2(36, -26), new Vector2(450, 48), true);
            catchDescription = Label(card, "鱼类后代  ·  亲本留在岛上", 16, muted, new Vector2(36, -76), new Vector2(490, 30));
            Panel("Header divider", card, new Vector2(0, 160), new Vector2(548, 1), surface);
            RectTransform cancel = Panel("Cancel fishing", card, new Vector2(268, 234), new Vector2(36, 36), surface, true);
            UnityEngine.UI.Button cancelButton = cancel.gameObject.AddComponent<UnityEngine.UI.Button>(); cancelButton.targetGraphic = cancel.GetComponent<UnityEngine.UI.Image>(); cancelButton.onClick.AddListener(()=>Cancel());
            UnityEngine.UI.ColorBlock buttonColors = cancelButton.colors;
            buttonColors.highlightedColor = new Color(.91f, .96f, .93f); buttonColors.pressedColor = new Color(.81f, .89f, .84f); buttonColors.fadeDuration = .12f; cancelButton.colors = buttonColors;
            TMP_Text closeText = Label(cancel, "×", 22, ink, Vector2.zero, new Vector2(36, 36)); CenterLabel(closeText, new Vector2(36, 36));
            Label(card, "让小鱼留在绿色区域", 20, ink, new Vector2(36, -132), new Vector2(330, 38), true);
            Label(card, "跟随小鱼，积累收线进度。", 16, muted, new Vector2(36, -172), new Vector2(318, 30));
            RectTransform holdHint = Panel("Hold instruction", card, new Vector2(-115, 34), new Vector2(318, 70), surface);
            Label(holdHint, "按住", 18, ink, new Vector2(16, -7), new Vector2(60, 33), true);
            Label(holdHint, "空格 / 鼠标左键", 16, muted, new Vector2(80, -9), new Vector2(220, 30));
            Label(holdHint, "绿色区域向上", 16, muted, new Vector2(16, -37), new Vector2(282, 30));
            RectTransform releaseHint = Panel("Release instruction", card, new Vector2(-115, -50), new Vector2(318, 70), surface);
            Label(releaseHint, "松开", 18, ink, new Vector2(16, -7), new Vector2(100, 33), true);
            Label(releaseHint, "绿色区域向下", 16, muted, new Vector2(16, -37), new Vector2(282, 30));
            feedback = Label(card, "鱼已咬钩，准备收线", 17, warningText, new Vector2(36, -389), new Vector2(328, 34));
            track = Panel("Fishing water column", card, new Vector2(203, -1), new Vector2(82, TrackHeight + 12), ink);
            Panel("Water lane", track, Vector2.zero, new Vector2(66, TrackHeight), new Color32(48, 83, 77, 255));
            catchBar = Panel("Catch zone", track, Vector2.zero, new Vector2(66, TrackHeight * BarSize), mint);
            barImage = catchBar.GetComponent<UnityEngine.UI.Image>();
            fishMarker = Panel("Fish marker", track, Vector2.zero, new Vector2(42, 26), ochre);
            TMP_Text fishIcon = Label(fishMarker, "鱼", 14, paper, Vector2.zero, new Vector2(42, 26), true); CenterLabel(fishIcon, new Vector2(42, 26));
            Label(card, "收线进度", 16, muted, new Vector2(36, -442), new Vector2(280, 29));
            progressLabel = Label(card, "35%", 18, ink, new Vector2(522, -440), new Vector2(62, 33), true); progressLabel.alignment = TextAlignmentOptions.Right;
            RectTransform progressTrack = Panel("Catch progress track", card, new Vector2(0, -208), new Vector2(ProgressWidth, 7), surface);
            progressFill = Panel("Catch progress", progressTrack, Vector2.zero, new Vector2(ProgressWidth * Progress, 7), successText);
            progressFill.pivot = new Vector2(0, .5f); progressFill.anchorMin = progressFill.anchorMax = new Vector2(0, .5f); progressFill.anchoredPosition = Vector2.zero;
            Label(card, "ESC 收竿  ·  垂钓时岛屿暂停", 14, muted, new Vector2(36, -504), new Vector2(548, 26));
            ApplyDeliveredFishing(card);
        }

        void ApplyDeliveredFishing(RectTransform card)
        {
            foreach(Transform child in card){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            root.GetComponent<UnityEngine.UI.Image>().color=new Color(0,0,0,.08f);
            card.sizeDelta=new Vector2(1440,810);card.GetComponent<UnityEngine.UI.Image>().color=Color.clear;card.GetComponent<UnityEngine.UI.Shadow>().enabled=false;
            catchDescription=Label(card,"",18,Color.white,new Vector2(230,-95),new Vector2(900,34));
            var cancel=Panel("Cancel fishing",card,new Vector2(670,370),new Vector2(40,40),new Color32(41,157,180,255),true);
            var button=cancel.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=cancel.GetComponent<UnityEngine.UI.Image>();button.onClick.AddListener(()=>Cancel());
            var close=Label(cancel,"×",25,ink,Vector2.zero,new Vector2(40,40));CenterLabel(close,new Vector2(40,40));
            track=Panel("Fishing water column",card,new Vector2(0,340),new Vector2(HorizontalWidth+12,34),new Color32(111,215,187,255));
            catchBar=Panel("Catch zone",track,Vector2.zero,new Vector2(HorizontalWidth*BarSize,34),new Color32(41,157,180,255));barImage=catchBar.GetComponent<UnityEngine.UI.Image>();
            finalCatchZone=HabitatFinalUi.Art(catchBar,"R107",Vector2.zero,catchBar.sizeDelta);finalCatchZone.uvRect=new Rect(.421f,.365f,.493f,.268f);finalCatchZone.rectTransform.anchorMin=Vector2.zero;finalCatchZone.rectTransform.anchorMax=Vector2.one;finalCatchZone.rectTransform.offsetMin=finalCatchZone.rectTransform.offsetMax=Vector2.zero;
            fishMarker=Panel("Fish marker",track,Vector2.zero,new Vector2(22,28),Color.clear);HabitatUiMotif.Add(fishMarker,new Vector2(1,-2),new Vector2(20,25),new Color32(239,109,113,255));
            var deliveredBar=HabitatFinalUi.Art(card,"R106",new Vector2(155,-34),new Vector2(1130,57));deliveredBar.transform.SetAsFirstSibling();
            Label(card,"按住空格 / 左键右移 · 松开左移",19,Color.white,new Vector2(135,-725),new Vector2(900,40));
            feedback=Label(card,"",20,Color.white,new Vector2(135,-610),new Vector2(600,40));
            var ring=MakeRect("Catch progress ring",card,new Vector2(-535,-40),new Vector2(104,104));progressRing=ring.gameObject.AddComponent<HabitatUiRing>();progressRing.color=new Color32(111,215,187,255);progressRing.raycastTarget=false;
            progressLabel=Label(ring,"",20,Color.white,Vector2.zero,new Vector2(104,104),true);CenterLabel(progressLabel,new Vector2(104,104));
            HabitatFinalUi.Art(card,"R108",new Vector2(260,-418),new Vector2(220,66));
            progressFill=Panel("Catch progress",card,new Vector2(0,-245),new Vector2(548,4),new Color32(41,157,180,255));progressFill.gameObject.SetActive(false);
            Label(card,"ESC 收竿 · 岛屿暂停 · 取消不消耗渔获",16,Color.white,new Vector2(135,-770),new Vector2(900,35));
        }

        void RefreshUI(bool force)
        {
            if (root == null) return;
            if (force && catchDescription) catchDescription.text = (targetSpecies==Species.Jellyfish?"宝石水母":game && HabitatChapterRules.IsMainIsland(game.State)?"琉璃鱼":"大眼鱼") + "  ·  亲本留在岛上";
            catchBar.sizeDelta = new Vector2(HorizontalWidth * Mathf.Clamp(BarSize, .12f, .8f),34);
            catchBar.anchoredPosition = new Vector2((Bar - .5f) * HorizontalWidth,0);
            fishMarker.anchoredPosition = new Vector2((Fish - .5f) * HorizontalWidth,0);
            if(progressRing)progressRing.Value=Progress;
            progressFill.sizeDelta = new Vector2(ProgressWidth * Progress, 7);
            bool overlap = Mathf.Abs(Fish - Bar) <= BarSize * .5f - .025f;
            barImage.color = finalCatchZone ? Color.clear : overlap ? mint : new Color32(117,149,138,255);
            if(finalCatchZone)finalCatchZone.color=overlap?Color.white:new Color(.78f,.85f,.85f,1);
            if (force || elapsed >= nextText)
            {
                nextText = elapsed + .12f;
                progressLabel.text = Mathf.RoundToInt(Progress * 100) + "%";
                feedback.text = elapsed < .45f ? "鱼已咬钩，准备收线" : overlap ? "正在收线，保持节奏" : "跟上小鱼，别让它溜走";
                feedback.color = Color.white;
            }
        }

        static RectTransform MakeRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            RectTransform r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = position; r.sizeDelta = size; return r;
        }
        static void CenterLabel(TMP_Text label, Vector2 size)
        {
            RectTransform r = label.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = Vector2.zero; r.sizeDelta = size;
            label.alignment = TextAlignmentOptions.Center;
        }
        static RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color, bool raycast = false)
        {
            RectTransform r = MakeRect(name, parent, position, size); UnityEngine.UI.Image im = r.gameObject.AddComponent<UnityEngine.UI.Image>(); im.color = color; im.raycastTarget = raycast; im.sprite=HabitatHud.RoundedSprite;im.type=UnityEngine.UI.Image.Type.Sliced;return r;
        }
        TMP_Text Label(Transform parent, string value, int size, Color color, Vector2 position, Vector2 dimensions, bool emphasis=false)
        {
            dimensions.y=Mathf.Max(dimensions.y,size*1.8f);
            RectTransform r = MakeRect("Fishing text", parent, position, dimensions); r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
            TextMeshProUGUI label = r.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = size; label.text = value; label.color = color; label.raycastTarget = false;
            label.fontStyle=emphasis?FontStyles.Bold:FontStyles.Normal;label.enableAutoSizing=false;
            label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Truncate; return label;
        }
    }
}
