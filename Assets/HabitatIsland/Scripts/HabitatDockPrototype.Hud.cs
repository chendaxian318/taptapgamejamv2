using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HabitatIsland
{
    public sealed partial class HabitatDockPrototype
    {
        GameObject modalRoot, interactionCard, sequencePanel, freePanel;
        HabitatMapSelectionPanel mapSelection;
        TMP_Text dayLabel, inventoryLabel, objectiveLabel, promptLabel, messageLabel;
        TMP_Text sequenceCaption, sequenceLabel, pauseLabel, modalTitle, modalBody, actionLabel;
        Button actionButton;
        readonly Color ink = new Color32(23, 61, 58, 255);
        readonly Color paper = new Color32(248, 250, 247, 248);
        readonly Color muted = new Color32(97, 117, 111, 255);
        readonly Color mint = new Color32(36, 93, 80, 255);
        readonly Color surface = new Color32(232, 239, 235, 255);

        void BuildHud()
        {
            var canvasObject = new GameObject("Harbor Chinese HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("Harbor EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var summary = Panel("Day and catch", canvas.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -24), new Vector2(650, 112), paper);
            dayLabel = Label(summary, "码头 · 第 1 天", 28, ink, new Vector2(24, -15), new Vector2(602, 43), true);
            inventoryLabel = Label(summary, "", 19, muted, new Vector2(24, -66), new Vector2(602, 31));
            var objective = Panel("Current objective", canvas.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-26, -24), new Vector2(635, 112), paper);
            Label(objective, "下一步", 17, muted, new Vector2(23, -15), new Vector2(589, 25));
            objectiveLabel = Label(objective, "", 23, ink, new Vector2(23, -49), new Vector2(589, 48), true);

            var free = Panel("Nearby interaction", canvas.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -150), new Vector2(520, 50), paper);
            freePanel = free.gameObject;
            promptLabel = Label(free, "", 21, ink, new Vector2(16, -7), new Vector2(488, 36), true);
            promptLabel.alignment = TextAlignmentOptions.Center;
            messageLabel = Label(canvas.transform, "", 20, ink, Vector2.zero, new Vector2(1140, 60), true);
            var mr = messageLabel.rectTransform; mr.anchorMin = mr.anchorMax = new Vector2(.5f, 1); mr.pivot = new Vector2(.5f, 1);
            mr.anchoredPosition = new Vector2(0, -212); messageLabel.alignment = TextAlignmentOptions.Center;

            var sequence = Panel("Timeline controls", canvas.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 26), new Vector2(1160, 185), paper);
            sequencePanel = sequence.gameObject;
            sequenceLabel = Label(sequence, "首日开场", 17, muted, new Vector2(24, -13), new Vector2(1112, 27));
            sequenceCaption = Label(sequence, "", 25, ink, new Vector2(24, -45), new Vector2(1112, 63), true);
            Label(sequence, "空格 / Esc 暂停或继续 · 播放结束后恢复自由行动", 17, muted, new Vector2(24, -128), new Vector2(690, 34));
            Button pause = HudButton(sequence, "暂停", new Vector2(747, -121), new Vector2(166, 46), ToggleSequencePause, false);
            pauseLabel = pause.GetComponentInChildren<TMP_Text>();
            HudButton(sequence, "跳过片段", new Vector2(929, -121), new Vector2(207, 46), () => { SkipSequence(); }, true);

            var overlay = Panel("Interaction modal", canvas.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(.03f, .10f, .10f, .43f), true);
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one; overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            modalRoot = overlay.gameObject;
            var card = Panel("Interaction card", overlay, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(650, 362), paper, true);
            interactionCard = card.gameObject;
            modalTitle = Label(card, "", 30, ink, new Vector2(30, -26), new Vector2(590, 49), true);
            modalBody = Label(card, "", 22, muted, new Vector2(30, -92), new Vector2(590, 145));
            actionButton = HudButton(card, "", new Vector2(30, -267), new Vector2(374, 62), RunModalAction, true);
            actionLabel = actionButton.GetComponentInChildren<TMP_Text>();
            HudButton(card, "关闭  Esc", new Vector2(420, -267), new Vector2(200, 62), CloseInteractionFromInput, false);
            mapSelection = HabitatMapSelectionPanel.Create(overlay, chineseFont, index => TrySailToIsland(index), CloseInteraction);
            modalRoot.SetActive(false); sequencePanel.SetActive(false);
            RefreshHud();
        }

        void RunModalAction()
        {
            if (Modal == HabitatDockInteraction.Shop) TryDeliverCatch();
            else if (Modal == HabitatDockInteraction.Home) TryRest();
            else if (Modal == HabitatDockInteraction.Boat) TrySailToIsland();
        }

        void RefreshModal()
        {
            if (!modalRoot) return;
            modalRoot.SetActive(Modal != HabitatDockInteraction.None);
            interactionCard.SetActive(Modal != HabitatDockInteraction.Boat);
            mapSelection.gameObject.SetActive(Modal == HabitatDockInteraction.Boat);
            actionButton.interactable = true;
            if (Modal == HabitatDockInteraction.Home)
            {
                modalTitle.text = "家 · 休息";
                modalBody.text = "回家休息，等明天的阳光照进窗台。\n\n" + (HasCatch ? "随身渔获会保留，明天仍可交付。" : "今天的渔获已交付，可以安心休息。");
                actionLabel.text = "休息到第 " + (Day + 1) + " 天";
            }
            else if (Modal == HabitatDockInteraction.Shop)
            {
                modalTitle.text = "商店 · 交付渔获";
                modalBody.text = HasCatch ? "带来了一筐满满的渔获。\n\n交付后会更新记录。" : "当前没有待交付的渔获。\n\n累计交付：" + DeliveredBatches + " 筐。可以回家休息，也可以乘船去岛屿。";
                actionLabel.text = HasCatch ? "交付这一筐渔获" : "暂无渔获";
                actionButton.interactable = HasCatch;
            }
            else if (Modal == HabitatDockInteraction.Boat)
            {
                mapSelection.Show(HabitatFigJamLevel.Index);
            }
        }

        void RefreshHud()
        {
            if (!dayLabel) return;
            dayLabel.text = "码头 · 第 " + Day + " 天";
            inventoryLabel.text = (HasCatch ? "随身：一筐渔获" : "随身：无待交付渔获") + "     累计交付：" + DeliveredBatches + " 筐";
            objectiveLabel.text = IsSequencePlaying ? (morningSequence ? "等待阳光照入窗台" : "从小船回到码头")
                : HasCatch ? "到商店前按 E，交付渔获" : Day == 1 ? "到家门前按 E，休息到第二天" : "自由探索码头，或乘船去岛屿";
            sequencePanel.SetActive(IsSequencePlaying);
            freePanel.SetActive(!IsSequencePlaying && Modal == HabitatDockInteraction.None && NearbyInteraction != HabitatDockInteraction.None);
            if (IsSequencePlaying)
            {
                sequenceLabel.text = (morningSequence ? "休息 · 清晨" : "首日开场") + (IsSequencePaused ? "  /  已暂停" : "  /  片段播放中");
                sequenceCaption.text = CurrentCaption;
                pauseLabel.text = IsSequencePaused ? "继续" : "暂停";
                messageLabel.text = "";
            }
            else
            {
                var nearby = NearbyInteraction;
                promptLabel.text = nearby == HabitatDockInteraction.Home ? "E  进家休息"
                    : nearby == HabitatDockInteraction.Shop ? "E  商店 · 交付渔获"
                    : nearby == HabitatDockInteraction.Boat ? "E  小船 · 选择目的地" : "";
                messageLabel.text = Modal == HabitatDockInteraction.None && Time.unscaledTime < messageUntil ? message : "";
            }
        }

        static RectTransform HudRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }

        static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color, bool raycast = false)
        {
            var rect = HudRect(name, parent, anchor, pivot, position, size);
            var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = raycast;
            return rect;
        }

        TMP_Text Label(Transform parent, string text, float size, Color color, Vector2 position, Vector2 dimensions, bool bold = false)
        {
            var rect = HudRect("Text", parent, new Vector2(0, 1), new Vector2(0, 1), position, dimensions);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = chineseFont; label.text = text; label.fontSize = size; label.color = color;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.alignment = TextAlignmentOptions.TopLeft; label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false; label.overflowMode = TextOverflowModes.Truncate;
            return label;
        }

        Button HudButton(Transform parent, string text, Vector2 position, Vector2 dimensions, Action action, bool primary)
        {
            var rect = Panel(text + " button", parent, new Vector2(0, 1), new Vector2(0, 1), position, dimensions, primary ? mint : surface, true);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var label = Label(rect, text, 21, primary ? paper : ink, new Vector2(8, 0), new Vector2(dimensions.x - 16, dimensions.y), true);
            label.alignment = TextAlignmentOptions.Center;
            button.onClick.AddListener(() => {HabitatAudio.Play(HabitatAudioCue.Click);action();});
            return button;
        }
    }
}
