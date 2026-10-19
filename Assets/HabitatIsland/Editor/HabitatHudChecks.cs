using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HabitatIsland.Editor
{
    /// <summary>Play-mode UI integration and layout audit. Restores the live game and HUD after checking.</summary>
    public static class HabitatHudChecks
    {
        static StringBuilder report;
        static int checks, failures, warnings;
        static int pendingRenderedChecks;
        static RenderedSession renderedSession;
        static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Vector2[] Viewports = {
            new Vector2(1920, 1080), new Vector2(1366, 768), new Vector2(1280, 720),
            new Vector2(1024, 768), new Vector2(1280, 1024), new Vector2(1600, 600),
            new Vector2(1920, 600), new Vector2(800, 600), new Vector2(960, 540)
        };
        public static bool RenderedChecksRunning => renderedSession != null;
        public static int PendingRenderedChecks => pendingRenderedChecks;
        public static string LastResult { get; private set; }

        sealed class RenderedSession
        {
            public HabitatGame game;
            public HabitatHud hud;
            public Canvas canvas;
            public GraphicRaycaster raycaster;
            public GameObject modal;
            public GameObject[] panels;
            public readonly string[] names = { "Settings", "Shop", "Help" };
            public readonly string[][] tokens = {
                new[] { "Settings", "实验室", "设置" }, new[] { "Shop", "补给", "商店" }, new[] { "Help", "帮助" }
            };
            public bool priorPause, waiting;
            public int index, openedFrame, openedRenderedFrame;
            public double deadline;
            public Dictionary<GameObject, bool> active;
            public Dictionary<FieldInfo, object> fields;
            public EventSystem eventSystem;
            public GameObject selection;
        }

        static string ReportPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/ui-checks.txt"));

        static void SaveReport()
        {
            LastResult = "UI checks: " + checks + "; failures: " + failures + "; review notes: " + warnings +
                "; pending rendered checks: " + pendingRenderedChecks + ". Report: " + ReportPath;
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report + "\nRESULT: " + checks + " checks; " + failures + " failures; " + warnings +
                " long-content review notes; " + pendingRenderedChecks + " rendered checks pending.\n");
        }

        struct VirtualRect
        {
            public Vector2 size;
            public Matrix4x4 matrix;
        }

        static void Check(bool condition, string label)
        {
            checks++;
            if (!condition) failures++;
            report.AppendLine((condition ? "PASS " : "FAIL ") + checks + ": " + label);
        }

        static T Field<T>(object instance, string name) where T : class
        {
            FieldInfo field = instance.GetType().GetField(name, Fields);
            return field == null ? null : field.GetValue(instance) as T;
        }

        static string PathOf(Transform transform, Transform root)
        {
            var names = new List<string>();
            while (transform != null && transform != root) { names.Add(transform.name); transform = transform.parent; }
            names.Reverse();
            return string.Join("/", names);
        }

        static void Refresh(HabitatGame game)
        {
            FieldInfo next = typeof(HabitatHud).GetField("nextRefresh", Fields);
            if (next != null) next.SetValue(game.Hud, 0f);
            game.Hud.Refresh();
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in game.Hud.GetComponentsInChildren<TMP_Text>())
                if (text.enabled && !string.IsNullOrWhiteSpace(text.text)) text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
        }

        static string ButtonText(Button button)
        {
            return button.name + " " + string.Join(" ", button.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text));
        }

        static Button Entry(Canvas canvas, GameObject modal, params string[] tokens)
        {
            return canvas.GetComponentsInChildren<Button>(true).FirstOrDefault(button =>
                !button.transform.IsChildOf(modal.transform) && tokens.Any(token =>
                    ButtonText(button).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        static Button CloseButton(GameObject panel, GameObject modal)
        {
            string[] names = { "Close", "回到小岛", "返回小岛", "开始观察", "继续观察", "关闭", "返回", "×" };
            Button found = panel.GetComponentsInChildren<Button>(true).FirstOrDefault(button =>
                names.Any(name => ButtonText(button).IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0));
            if (found != null) return found;
            return modal.GetComponentsInChildren<Button>(true).FirstOrDefault(button => button.gameObject.activeInHierarchy &&
                names.Any(name => ButtonText(button).IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        static List<RaycastResult> Raycast(GraphicRaycaster raycaster, Vector2 position)
        {
            var result = new List<RaycastResult>();
            var data = new PointerEventData(EventSystem.current) { position = position };
            raycaster.Raycast(data, result);
            return result;
        }

        static Vector2 ScreenCenter(RectTransform rect, Canvas canvas)
        {
            return RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center));
        }

        static bool IsVisible(Graphic graphic)
        {
            if (!graphic.enabled || !graphic.gameObject.activeInHierarchy || graphic.color.a <= .01f) return false;
            foreach (CanvasGroup group in graphic.GetComponentsInParent<CanvasGroup>())
                if (group.alpha <= .01f) return false;
            TMP_Text text = graphic as TMP_Text;
            return text == null || !string.IsNullOrWhiteSpace(text.text);
        }

        // Re-evaluate the RectTransform anchor equations against each virtual canvas size.
        // This does not change the Game view or live CanvasScaler while the audit runs.
        static VirtualRect Project(RectTransform rect, RectTransform canvas, Vector2 canvasSize, Dictionary<RectTransform, VirtualRect> cache)
        {
            VirtualRect result;
            if (cache.TryGetValue(rect, out result)) return result;
            if (rect == canvas)
                result = new VirtualRect { size = canvasSize, matrix = Matrix4x4.identity };
            else
            {
                RectTransform parent = rect.parent as RectTransform;
                if (parent == null) throw new InvalidOperationException("Non-RectTransform parent: " + rect.name);
                VirtualRect p = Project(parent, canvas, canvasSize, cache);
                Vector2 span = rect.anchorMax - rect.anchorMin;
                Vector2 anchor = rect.anchorMin + Vector2.Scale(span, rect.pivot);
                Vector2 position = Vector2.Scale(p.size, anchor - parent.pivot) + rect.anchoredPosition;
                result = new VirtualRect
                {
                    size = Vector2.Scale(p.size, span) + rect.sizeDelta,
                    matrix = p.matrix * Matrix4x4.TRS(new Vector3(position.x, position.y, rect.localPosition.z), rect.localRotation, rect.localScale)
                };
            }
            cache[rect] = result;
            return result;
        }

        static float ViewportScale(CanvasScaler scaler, Vector2 viewport)
        {
            float widthRatio = viewport.x / scaler.referenceResolution.x;
            float heightRatio = viewport.y / scaler.referenceResolution.y;
            switch (scaler.screenMatchMode)
            {
                case CanvasScaler.ScreenMatchMode.Expand: return Mathf.Min(widthRatio, heightRatio);
                case CanvasScaler.ScreenMatchMode.Shrink: return Mathf.Max(widthRatio, heightRatio);
                default: return Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(widthRatio, 2f), Mathf.Log(heightRatio, 2f), scaler.matchWidthOrHeight));
            }
        }

        static UnityEngine.Rect ProjectedBounds(RectTransform rect, RectTransform canvas, Vector2 canvasSize,
            Dictionary<RectTransform, VirtualRect> cache)
        {
            VirtualRect projected = Project(rect, canvas, canvasSize, cache);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 local = Vector2.Scale(projected.size, new Vector2((corner & 1) == 0 ? 0 : 1, (corner & 2) == 0 ? 0 : 1) - rect.pivot);
                Vector3 point = projected.matrix.MultiplyPoint3x4(local);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static void CheckPanelSeparation(Canvas canvas, RectTransform canvasRect, Vector2 canvasSize,
            Dictionary<RectTransform, VirtualRect> cache, string label)
        {
            // These groups must coexist; their backgrounds intentionally block board clicks.
            // Screen containment alone would miss one group obscuring another.
            string[,] pairs = {
                { "Keeper tools", "Camera controls" }, { "Keeper tools", "Quick actions" },
                { "Camera controls", "Log toggle" }, { "Selected habitat", "Island log" },
                { "Resident handbook", "Quick actions" }
            };
            var overlaps = new List<string>();
            for (int pair = 0; pair < pairs.GetLength(0); pair++)
            {
                RectTransform first = canvas.transform.Find(pairs[pair, 0]) as RectTransform;
                RectTransform second = canvas.transform.Find(pairs[pair, 1]) as RectTransform;
                if (first == null || second == null || !first.gameObject.activeInHierarchy || !second.gameObject.activeInHierarchy) continue;
                UnityEngine.Rect a = ProjectedBounds(first, canvasRect, canvasSize, cache);
                UnityEngine.Rect b = ProjectedBounds(second, canvasRect, canvasSize, cache);
                float width = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                float height = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                if (width > 1f && height > 1f) overlaps.Add(first.name + " / " + second.name + " (" + width.ToString("0.0") + " x " + height.ToString("0.0") + ")");
            }
            Check(overlaps.Count == 0, label + " independent controls and expanded panels do not overlap" +
                (overlaps.Count > 0 ? "\n  Overlap: " + string.Join("\n  Overlap: ", overlaps) : ""));
        }

        static void Layout(Canvas canvas, GameObject modal, string stateName)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            RectTransform canvasRect = canvas.transform as RectTransform;
            var graphics = canvas.GetComponentsInChildren<Graphic>().Where(IsVisible).ToArray();
            foreach (Vector2 viewport in Viewports)
            {
                float scale = ViewportScale(scaler, viewport);
                Vector2 canvasSize = viewport / scale;
                var cache = new Dictionary<RectTransform, VirtualRect>();
                var outside = new List<string>();
                foreach (Graphic graphic in graphics)
                {
                    if (graphic.gameObject == modal) continue; // The dimming backdrop deliberately extends past the viewport.
                    RectTransform rect = graphic.rectTransform;
                    VirtualRect projected = Project(rect, canvasRect, canvasSize, cache);
                    bool clipped = false;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        Vector2 point = Vector2.Scale(projected.size, new Vector2((corner & 1) == 0 ? 0 : 1, (corner & 2) == 0 ? 0 : 1) - rect.pivot);
                        Vector3 transformed = projected.matrix.MultiplyPoint3x4(point);
                        Vector2 screen = (new Vector2(transformed.x, transformed.y) + Vector2.Scale(canvasSize, canvasRect.pivot)) * scale;
                        if (screen.x < -1.5f || screen.y < -1.5f || screen.x > viewport.x + 1.5f || screen.y > viewport.y + 1.5f) clipped = true;
                    }
                    if (clipped) outside.Add(PathOf(rect, canvas.transform));
                }
                Check(outside.Count == 0, stateName + " fits " + (int)viewport.x + " x " + (int)viewport.y +
                    " (anchor projection, " + graphics.Length + " visible graphics)" + (outside.Count > 0 ? "\n  Outside: " + string.Join("\n  Outside: ", outside) : ""));
                CheckPanelSeparation(canvas, canvasRect, canvasSize, cache, stateName + " at " + (int)viewport.x + " x " + (int)viewport.y);
            }

            var overflows = new List<string>();
            foreach (TMP_Text text in graphics.OfType<TMP_Text>())
            {
                string plain = Regex.Replace(text.text, "<[^>]+>", "");
                bool overflow = text.isTextTruncated || text.isTextOverflowing;
                if (!overflow) continue;
                string detail = PathOf(text.transform, canvas.transform) + " [" + plain.Replace("\n", " / ") + "]";
                // Long contextual prose/logs can intentionally use a preview. Keep these actionable
                // in the report, while requiring every button and short control label to fit in full.
                bool contextualPreview = text.GetComponentInParent<Button>() == null && (plain.Length > 90 || plain.Count(c => c == '\n') >= 5);
                if (contextualPreview)
                {
                    warnings++;
                    report.AppendLine("REVIEW long-content overflow: " + detail);
                }
                else overflows.Add(detail);
            }
            Check(overflows.Count == 0, stateName + " control and short-text labels are not truncated" +
                (overflows.Count > 0 ? "\n  Overflow: " + string.Join("\n  Overflow: ", overflows) : ""));
            Check(graphics.OfType<TMP_Text>().All(text => !text.raycastTarget), stateName + " decorative text does not intercept pointer input");
        }

        static void CheckModal(HabitatGame game, Canvas canvas, GameObject modal,
            GameObject panel, string name, params string[] tokens)
        {
            Button entry = Entry(canvas, modal, tokens);
            Check(entry != null, name + " has a real HUD entry button");
            if (entry == null || panel == null) return;
            game.paused = false;
            entry.onClick.Invoke();
            Refresh(game);
            Check(game.Hud.ModalOpen && panel.activeInHierarchy && game.paused, name + " button opens the panel and pauses a running game");
            Layout(canvas, modal, name);
            // Graphic.depth is assigned by the native canvas render. ForceUpdateCanvases in
            // this synchronous call only rebuilds layout/graphics; the strict hit assertions
            // run in BeginRenderedChecks after a real rendered frame has advanced.
            report.AppendLine("PENDING rendered phase: " + name + " panel and backdrop raycast checks.");
            Button close = CloseButton(panel, modal);
            Check(close != null, name + " has a visible close button");
            if (close != null) close.onClick.Invoke(); else game.Hud.ClosePanels();
            Check(!game.Hud.ModalOpen && !game.paused, name + " close button restores the prior running state");
            game.paused = true;
            entry.onClick.Invoke();
            if (close != null) close.onClick.Invoke(); else game.Hud.ClosePanels();
            Check(!game.Hud.ModalOpen && game.paused, name + " close preserves a preexisting pause");
        }

        static void CheckDisclosure(HabitatGame game, Canvas canvas, GameObject modal, string fieldName, string name, params string[] tokens)
        {
            GameObject panel = Field<GameObject>(game.Hud, fieldName);
            Button entry = Entry(canvas, modal, tokens);
            if (panel == null || entry == null) return; // Optional disclosures may be removed by a HUD layout.
            bool before = panel.activeSelf;
            bool paused = game.paused;
            entry.onClick.Invoke();
            Refresh(game);
            Check(panel.activeSelf != before && game.paused == paused, name + " button toggles its disclosure without changing simulation pause");
            if (panel.activeInHierarchy) Layout(canvas, modal, name + " expanded");
            entry.onClick.Invoke();
            Refresh(game);
            Check(panel.activeSelf == before && game.paused == paused, name + " second click restores the disclosure");
        }

        static void CheckExpandedPanels(HabitatGame game, Canvas canvas, GameObject modal)
        {
            HabitatState state = game.State;
            GameObject handbook = Field<GameObject>(game.Hud, "ecologyPanel");
            GameObject log = Field<GameObject>(game.Hud, "eventPanel");
            bool handbookWasOpen = handbook != null && handbook.activeSelf;
            bool logWasOpen = log != null && log.activeSelf;
            try
            {
                // Isolate the largest inspector variant from the live game and undo history.
                game.State = HabitatRules.Clone(state);
                game.State.authoredLevel = true;
                game.State.selectedX = game.State.selectedZ = 0;
                game.State.tiles[0].prop = PropKind.Fan;
                if (handbook != null) handbook.SetActive(true);
                if (log != null) log.SetActive(true);
                Refresh(game);
                Check(Field<GameObject>(game.Hud, "inspectorPanel").activeInHierarchy &&
                      handbook != null && handbook.activeInHierarchy && log != null && log.activeInHierarchy,
                    "largest inspector, resident handbook and island log can be displayed together");
                Layout(canvas, modal, "All contextual panels expanded");
            }
            finally
            {
                game.State = state;
                if (handbook != null) handbook.SetActive(handbookWasOpen);
                if (log != null) log.SetActive(logWasOpen);
                Refresh(game);
            }
        }

        public static string Run()
        {
            if (RenderedChecksRunning) throw new InvalidOperationException("Wait for the rendered UI checks to finish before starting another audit.");
            if (!Application.isPlaying || HabitatGame.Instance == null)
                throw new InvalidOperationException("Enter Play mode in the Habitat Island scene before running UI checks.");
            HabitatGame game = HabitatGame.Instance;
            HabitatHud hud = game.Hud;
            Canvas canvas = hud.GetComponentsInChildren<Canvas>(true).FirstOrDefault(c => c.name == "Habitat HUD");
            if (canvas == null) throw new InvalidOperationException("Habitat HUD canvas was not found.");
            GameObject modal = Field<GameObject>(hud, "modal");
            GameObject settings = Field<GameObject>(hud, "settingsPanel");
            GameObject shop = Field<GameObject>(hud, "shopPanel");
            GameObject help = Field<GameObject>(hud, "helpPanel");
            HabitatState originalState = game.State;
            bool originalPause = game.paused, originalOverlays = game.overlays;
            int originalTool = game.tool, originalActions = game.ActionCount, originalUndo = game.UndoCount;
            float originalSpeed = game.speed;
            var active = canvas.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.gameObject, t => t.gameObject.activeSelf);
            var savedFields = new Dictionary<FieldInfo, object>();
            foreach (string name in new[] { "priorPause", "nextRefresh", "messageAt", "lastMessage" })
            {
                FieldInfo field = typeof(HabitatHud).GetField(name, Fields);
                if (field != null) savedFields[field] = field.GetValue(hud);
            }
            EventSystem eventSystem = EventSystem.current;
            GameObject originalSelection = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
            checks = failures = warnings = 0;
            pendingRenderedChecks = 6;
            report = new StringBuilder("Habitat Island HUD integration and layout checks\nUTC: " + DateTimeOffset.UtcNow.ToString("O") +
                "\nActual Game view: " + Screen.width + " x " + Screen.height + "\nVirtual viewport checks use CanvasScaler and RectTransform anchor equations; screenshots still require visual inspection.\n\n");
            try
            {
                Check(modal != null && settings != null && shop != null && help != null, "all modal panels initialized");
                if (modal == null || settings == null || shop == null || help == null) throw new InvalidOperationException("Required HUD panel fields are missing.");
                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                Check(raycaster != null && raycaster.enabled, "HUD has an enabled GraphicRaycaster");
                Check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Count(e => e.enabled) == 1,
                    "exactly one active EventSystem handles UI input");
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                Check(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                      scaler.referenceResolution.x > 0 && scaler.referenceResolution.y > 0,
                      "canvas uses a valid reference resolution; viewport checks use its configured screen matching mode");
                hud.ClosePanels();
                game.paused = true;

                for (int level = 0; level < HabitatLevels.Names.Length; level++)
                {
                    // Do not call LoadLevel: it would clear the player's live undo history.
                    game.State = HabitatLevels.Create(level);
                    Refresh(game);
                    Layout(canvas, modal, "Chapter " + (level + 1) + " default");
                    foreach (Animal animal in game.State.animals)
                    {
                        game.SelectAnimal(animal.id);
                        Refresh(game);
                        Layout(canvas, modal, "Chapter " + (level + 1) + " selected " + HabitatRules.Name(animal.species));
                    }
                }

                game.State.selectedX = game.State.selectedZ = -1;
                Refresh(game);
                CheckDisclosure(game, canvas, modal, "ecologyPanel", "Resident handbook", "居民", "手册");
                CheckDisclosure(game, canvas, modal, "eventPanel", "Island log", "日志");
                CheckExpandedPanels(game, canvas, modal);

                game.State = HabitatRules.Clone(originalState);
                game.State.selectedX = game.State.selectedZ = -1;
                Refresh(game);
                Image[] toolImages = Field<Image[]>(hud, "toolImages");
                Check(toolImages != null && toolImages.Length == HabitatGame.ToolNames.Length, "five tool controls are available");
                if (toolImages != null)
                    for (int tool = 0; tool < toolImages.Length; tool++)
                    {
                        Button button = toolImages[tool] == null ? null : toolImages[tool].GetComponent<Button>();
                        Check(button != null && button.interactable && button.gameObject.activeInHierarchy, "tool " + (tool + 1) + " is an active button");
                        if (button == null) continue;
                        button.onClick.Invoke();
                        Refresh(game);
                        Check(game.tool == tool, "tool " + (tool + 1) + " click selects " + HabitatGame.ToolNames[tool]);
                        List<RaycastResult> hits = Raycast(raycaster, ScreenCenter(button.transform as RectTransform, canvas));
                        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
                            "tool " + (tool + 1) + " receives the foremost UI raycast");
                        Check(button.GetComponentsInChildren<Graphic>().Where(g => g.gameObject != button.gameObject).All(g => !g.raycastTarget),
                            "tool " + (tool + 1) + " icon and label do not intercept its button raycast");
                    }
                Check(Raycast(raycaster, new Vector2(Screen.width * .5f, Screen.height * .5f)).Count == 0,
                    "the central board area receives no HUD raycast when panels are closed");
                CheckModal(game, canvas, modal, settings, "Settings", "Settings", "实验室", "设置");
                CheckModal(game, canvas, modal, shop, "Shop", "Shop", "补给", "商店");
                CheckModal(game, canvas, modal, help, "Help", "Help", "帮助");
                Check(game.ActionCount == originalActions && game.UndoCount == originalUndo,
                    "UI selection and panel checks preserve gameplay transactions and undo history");
            }
            catch (Exception exception)
            {
                failures++;
                report.AppendLine("EXCEPTION: " + exception);
            }
            finally
            {
                hud.ClosePanels();
                game.State = originalState;
                game.paused = originalPause;
                game.tool = originalTool;
                game.speed = originalSpeed;
                game.overlays = originalOverlays;
                foreach (var pair in savedFields) pair.Key.SetValue(hud, pair.Value);
                foreach (var pair in active) if (pair.Key != null) pair.Key.SetActive(pair.Value);
                Refresh(game);
                foreach (var pair in savedFields) pair.Key.SetValue(hud, pair.Value);
                foreach (var pair in active) if (pair.Key != null) pair.Key.SetActive(pair.Value);
                if (eventSystem != null) eventSystem.SetSelectedGameObject(originalSelection);
                report.AppendLine("\nLive State reference, tool, pause, speed, overlays, panel visibility and EventSystem selection restored.");
                report.AppendLine("Call BeginRenderedChecks() to finish the six actual modal raycasts across rendered frames.");
                SaveReport();
            }
            return LastResult;
        }

        /// <summary>
        /// Start after Run(). Poll RenderedChecksRunning/LastResult from a later editor call.
        /// Each panel must be active through a real render and have assigned Graphic depths;
        /// a ten-second render timeout fails the assertions rather than skipping them.
        /// </summary>
        public static string BeginRenderedChecks()
        {
            if (RenderedChecksRunning) return "Rendered UI checks are already running. " + LastResult;
            if (report == null) throw new InvalidOperationException("Run the synchronous UI audit with Run() first.");
            if (pendingRenderedChecks == 0) return LastResult;
            if (!Application.isPlaying || HabitatGame.Instance == null)
                throw new InvalidOperationException("Keep the Habitat Island scene in Play mode with the Game view rendering.");
            HabitatGame game = HabitatGame.Instance;
            Canvas canvas = game.Hud.GetComponentsInChildren<Canvas>(true).First(c => c.name == "Habitat HUD");
            var session = new RenderedSession
            {
                game = game,
                hud = game.Hud,
                canvas = canvas,
                raycaster = canvas.GetComponent<GraphicRaycaster>(),
                modal = Field<GameObject>(game.Hud, "modal"),
                panels = new[] { Field<GameObject>(game.Hud, "settingsPanel"), Field<GameObject>(game.Hud, "shopPanel"), Field<GameObject>(game.Hud, "helpPanel") },
                priorPause = game.paused,
                active = canvas.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.gameObject, t => t.gameObject.activeSelf),
                fields = new Dictionary<FieldInfo, object>(),
                eventSystem = EventSystem.current,
                selection = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject
            };
            foreach (string name in new[] { "priorPause", "nextRefresh", "messageAt", "lastMessage" })
            {
                FieldInfo field = typeof(HabitatHud).GetField(name, Fields);
                if (field != null) session.fields[field] = field.GetValue(game.Hud);
            }
            renderedSession = session;
            game.Hud.ClosePanels();
            game.paused = true;
            report.AppendLine("\nRendered raycast phase started at frame " + Time.frameCount + ", rendered frame " + Time.renderedFrameCount + ".");
            UnityEditor.EditorApplication.update += TickRenderedChecks;
            TickRenderedChecks();
            return "Rendered UI checks started; poll RenderedChecksRunning, then read LastResult. " + LastResult;
        }

        static void TickRenderedChecks()
        {
            RenderedSession session = renderedSession;
            if (session == null) return;
            try
            {
                if (!Application.isPlaying || session.game == null || session.hud == null)
                    throw new InvalidOperationException("Play mode ended before the rendered UI checks completed.");
                if (session.index >= session.panels.Length) { FinishRenderedChecks(); return; }
                string name = session.names[session.index];
                GameObject panel = session.panels[session.index];
                if (!session.waiting)
                {
                    session.hud.ClosePanels();
                    session.game.paused = true;
                    Button entry = Entry(session.canvas, session.modal, session.tokens[session.index]);
                    if (entry == null || panel == null) throw new InvalidOperationException(name + " rendered test is missing its entry button or panel.");
                    entry.onClick.Invoke();
                    Refresh(session.game);
                    session.openedFrame = Time.frameCount;
                    session.openedRenderedFrame = Time.renderedFrameCount;
                    session.deadline = UnityEditor.EditorApplication.timeSinceStartup + 10;
                    session.waiting = true;
                    return;
                }

                Canvas.ForceUpdateCanvases();
                Graphic surface = panel.GetComponent<Graphic>();
                Graphic backdrop = session.modal.GetComponent<Graphic>();
                int surfaceDepth = surface == null ? -1 : surface.depth;
                int backdropDepth = backdrop == null ? -1 : backdrop.depth;
                bool rendered = Time.frameCount > session.openedFrame && Time.renderedFrameCount > session.openedRenderedFrame;
                bool ready = rendered && panel.activeInHierarchy && session.modal.activeInHierarchy && surfaceDepth >= 0 && backdropDepth >= 0;
                bool timedOut = UnityEditor.EditorApplication.timeSinceStartup >= session.deadline;
                if (!ready && !timedOut) return;

                report.AppendLine(name + " render evidence: opened frame " + session.openedFrame + ", checked frame " + Time.frameCount +
                    "; opened rendered frame " + session.openedRenderedFrame + ", checked rendered frame " + Time.renderedFrameCount +
                    "; surface depth " + surfaceDepth + ", backdrop depth " + backdropDepth + (ready ? "." : "; RENDER READINESS TIMEOUT."));
                // Exact original hit requirements remain in force. Readiness timeout is failure,
                // even if an old Graphic depth or a stale raycast would appear to pass.
                bool panelHit = Raycast(session.raycaster, ScreenCenter(panel.transform as RectTransform, session.canvas)).Any(hit => hit.gameObject == panel);
                bool backdropHit = Raycast(session.raycaster, new Vector2(Screen.width * .03f, Screen.height * .5f)).Any(hit => hit.gameObject == session.modal);
                Check(ready && panelHit, name + " rendered panel surface blocks clicks on the game world");
                Check(ready && backdropHit, name + " rendered backdrop blocks world clicks outside the panel");
                pendingRenderedChecks -= 2;
                Button close = CloseButton(panel, session.modal);
                if (close != null) close.onClick.Invoke(); else session.hud.ClosePanels();
                session.game.paused = true;
                session.index++;
                session.waiting = false;
                SaveReport();
                if (session.index == session.panels.Length) FinishRenderedChecks();
            }
            catch (Exception exception)
            {
                failures++;
                report.AppendLine("RENDERED PHASE EXCEPTION: " + exception);
                FinishRenderedChecks();
            }
        }

        static void FinishRenderedChecks()
        {
            UnityEditor.EditorApplication.update -= TickRenderedChecks;
            RenderedSession session = renderedSession;
            renderedSession = null;
            if (session != null && session.game != null && session.hud != null)
            {
                session.hud.ClosePanels();
                session.game.paused = session.priorPause;
                foreach (var pair in session.fields) pair.Key.SetValue(session.hud, pair.Value);
                foreach (var pair in session.active) if (pair.Key != null) pair.Key.SetActive(pair.Value);
                Refresh(session.game);
                foreach (var pair in session.fields) pair.Key.SetValue(session.hud, pair.Value);
                foreach (var pair in session.active) if (pair.Key != null) pair.Key.SetActive(pair.Value);
                if (session.eventSystem != null) session.eventSystem.SetSelectedGameObject(session.selection);
                report.AppendLine("Rendered phase restored the prior pause, panel visibility and EventSystem selection.");
            }
            SaveReport();
        }
    }
}
