using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Mindzheimer.Portfolio
{
    /// <summary>
    /// Procedurally builds a tracking-setup screen (sidebar, instruction
    /// card, humanoid silhouette with clickable tracker points, pagination)
    /// entirely from code — no imported art required.
    ///
    /// PORTFOLIO PIECE ONLY. From-scratch reskin inspired by the general
    /// shape of body-tracker calibration UIs (own colour palette, own copy,
    /// own generated geometry) — not a copy of any third-party product's
    /// assets/logo/text, and not wired into MINDZHEIMER's actual pipeline.
    ///
    /// ACCURACY NOTE: MINDZHEIMER tracks exactly three things — the Quest 3
    /// headset (HMD) and its two controllers (hands). Gaze is derived from
    /// head orientation, not a separate tracked point, and there are no
    /// external body trackers. Only Head / L Hand / R Hand are shown as
    /// active tracker dots; the rest of the silhouette is context only.
    ///
    /// Add PortfolioLiveXRPointer alongside this component to drive the
    /// tracker dots from real headset/controller movement instead of the
    /// scripted auto-play demo.
    ///
    /// SETUP (do this once in the Editor):
    ///   1. New empty Scene.
    ///   2. Empty GameObject, e.g. "PortfolioCalibrationUI".
    ///   3. Add this component to it.
    ///   4. (Optional) Assets > Create > Portfolio > UI Theme, tweak colours,
    ///      drag the asset into the Theme field below.
    ///   5. Press Play. The UI builds itself and the demo sequence auto-runs.
    ///   6. Record Play Mode with Unity Recorder or OBS for your portfolio gif/video.
    /// </summary>
    public class PortfolioCalibrationUIBuilder : MonoBehaviour
    {
        [Header("Theme")]
        public PortfolioUITheme theme;

        [Header("Content (edit freely — no code changes needed)")]
        public string appName        = "MOTION CALIB";
        public string screenTitle    = "Tracking Setup";
        public string stepHeading    = "Headset & Controllers";
        [TextArea(2, 4)]
        public string stepDescription =
            "MINDZHEIMER only needs the Quest 3 headset and its two " +
            "controllers — head, left hand, right hand. No external " +
            "trackers, no extra hardware, no per-limb calibration.";
        [TextArea(2, 3)]
        public string tipText =
            "Head and hand tracking come straight from the headset and " +
            "controllers — nothing extra to put on or calibrate.";
        public string ctaLabel = "Headset & Controllers Ready";
        public string bodyCaption = "Only 3 points are tracked — no external hardware";

        [Header("Demo Playback")]
        public bool autoPlayDemoOnStart = true;
        public bool loopDemo            = true;
        public float stepDelay          = 0.45f;

        /// <summary>Root Canvas RectTransform, exposed so companion scripts
        /// (e.g. PortfolioLiveXRPointer) can add their own overlay elements
        /// and convert positions correctly.</summary>
        public RectTransform CanvasRect { get; private set; }

        private readonly List<PortfolioTrackerPoint> trackerPoints = new List<PortfolioTrackerPoint>();
        private TMP_FontAsset font;
        private Coroutine demoRoutine;

        private static readonly (string label, Vector2 pos)[] TrackerJoints =
        {
            ("Head",   new Vector2(   0,  230)),
            ("L Hand", new Vector2(-100,  -35)),
            ("R Hand", new Vector2( 100,  -35)),
        };

        private static readonly Vector2 ShoulderL = new Vector2(-70, 165);
        private static readonly Vector2 ShoulderR = new Vector2(70, 165);
        private static readonly Vector2 ElbowL    = new Vector2(-95, 65);
        private static readonly Vector2 ElbowR    = new Vector2(95, 65);
        private static readonly Vector2 HandL     = new Vector2(-100, -35);
        private static readonly Vector2 HandR     = new Vector2(100, -35);
        private static readonly Vector2 HipL      = new Vector2(-40, -15);
        private static readonly Vector2 HipR      = new Vector2(40, -15);
        private static readonly Vector2 KneeL     = new Vector2(-42, -150);
        private static readonly Vector2 KneeR     = new Vector2(42, -150);
        private static readonly Vector2 FootL     = new Vector2(-42, -270);
        private static readonly Vector2 FootR     = new Vector2(42, -270);

        private void Start()
        {
            if (theme == null) theme = PortfolioUITheme.Default;
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font == null) font = TMP_Settings.defaultFontAsset;

            EnsureEventSystem();
            BuildUI();

            if (autoPlayDemoOnStart)
                PlayDemo();
        }

        public void PlayDemo()
        {
            if (demoRoutine != null) StopCoroutine(demoRoutine);
            demoRoutine = StartCoroutine(PlayDemoSequence());
        }

        public void StopDemo()
        {
            if (demoRoutine != null)
            {
                StopCoroutine(demoRoutine);
                demoRoutine = null;
            }
            foreach (var t in trackerPoints) t.SetCalibrated(false);
        }

        public PortfolioTrackerPoint GetTrackerPoint(string label)
        {
            foreach (var t in trackerPoints)
                if (t.Label == label) return t;
            return null;
        }

        private IEnumerator PlayDemoSequence()
        {
            do
            {
                foreach (var t in trackerPoints) t.SetCalibrated(false);
                yield return new WaitForSeconds(stepDelay);

                foreach (var t in trackerPoints)
                {
                    t.SetCalibrated(true);
                    yield return new WaitForSeconds(stepDelay);
                }

                yield return new WaitForSeconds(stepDelay * 4f);
            }
            while (loopDemo);
        }

        private void BuildUI()
        {
            CanvasRect = CreateCanvas();

            RectTransform bg = CreateUIObject("Background", CanvasRect);
            SetFullStretch(bg);
            AddImage(bg, UIShapeFactory.RoundedRect(8, 8, 0, theme.background), theme.background);

            CreateTopBar(CanvasRect);
            CreateSidebar(CanvasRect);
            CreateMainContent(CanvasRect);
        }

        private RectTransform CreateCanvas()
        {
            GameObject canvasGO = new GameObject("Canvas_PortfolioCalibration",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            return canvasGO.GetComponent<RectTransform>();
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            GameObject esGO = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            esGO.AddComponent<InputSystemUIInputModule>();
#else
            esGO.AddComponent<StandaloneInputModule>();
#endif
        }

        private void CreateTopBar(RectTransform parent)
        {
            RectTransform bar = CreateUIObject("TopBar", parent);
            SetTopLeft(bar, 0, 0, 1920, 64);
            AddImage(bar, UIShapeFactory.RoundedRect(8, 8, 0, theme.sidebarPanel), theme.sidebarPanel);

            RectTransform logo = CreateUIObject("Logo", bar);
            SetTopLeft(logo, 24, 16, 32, 32);
            AddImage(logo, UIShapeFactory.RoundedRect(32, 32, 9, theme.accentPrimary), theme.accentPrimary);

            RectTransform title = CreateUIObject("AppName", bar);
            SetTopLeft(title, 68, 20, 400, 24);
            AddText(title, appName, 20, theme.textPrimary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            for (int i = 0; i < 3; i++)
            {
                RectTransform dot = CreateUIObject($"WinDot{i}", bar);
                SetTopLeft(dot, 1920 - 24 - (i * 26) - 12, 24, 12, 12);
                AddImage(dot, UIShapeFactory.Circle(12, theme.textSecondary), theme.textSecondary);
            }
        }

        private void CreateSidebar(RectTransform parent)
        {
            RectTransform sidebar = CreateUIObject("Sidebar", parent);
            SetTopLeft(sidebar, 0, 64, 260, 1016);
            AddImage(sidebar, UIShapeFactory.RoundedRect(8, 8, 0, theme.sidebarPanel), theme.sidebarPanel);

            string[] items = { "Main Page", "Body Proportions", "Tracker Assignment", "Position Calibration", "Initial Configuration" };
            int activeIndex = 3;

            float y = 32;
            for (int i = 0; i < items.Length; i++)
            {
                bool active = i == activeIndex;
                RectTransform row = CreateUIObject($"Nav_{items[i]}", sidebar);
                SetTopLeft(row, 0, y, 260, 52);

                if (active)
                    AddImage(row, UIShapeFactory.RoundedRect(260, 52, 0, theme.accentSoft), theme.accentSoft);

                if (active)
                {
                    RectTransform accentBar = CreateUIObject("AccentBar", row);
                    SetTopLeft(accentBar, 0, 8, 4, 36);
                    AddImage(accentBar, UIShapeFactory.RoundedRect(4, 36, 2, theme.accentPrimary), theme.accentPrimary);
                }

                RectTransform label = CreateUIObject("Label", row);
                SetTopLeft(label, 28, 0, 210, 52);
                AddText(label, items[i], 16, active ? theme.textPrimary : theme.textSecondary,
                    TextAlignmentOptions.MidlineLeft, active ? FontStyles.Bold : FontStyles.Normal);

                y += 56;
            }

            RectTransform settingsRow = CreateUIObject("Nav_Settings", sidebar);
            SetTopLeft(settingsRow, 0, 1016 - 84, 260, 52);
            RectTransform settingsLabel = CreateUIObject("Label", settingsRow);
            SetTopLeft(settingsLabel, 28, 0, 210, 52);
            AddText(settingsLabel, "Settings", 16, theme.textSecondary, TextAlignmentOptions.MidlineLeft);
        }

        private void CreateMainContent(RectTransform parent)
        {
            RectTransform main = CreateUIObject("MainContent", parent);
            SetTopLeft(main, 260, 64, 1660, 1016);

            RectTransform title = CreateUIObject("ScreenTitle", main);
            SetTopLeft(title, 48, 40, 800, 44);
            AddText(title, screenTitle, 32, theme.textPrimary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            RectTransform instructionCard = CreateInstructionCard(main);
            SetTopLeft(instructionCard, 48, 110, 900, 620);

            RectTransform bodyCard = CreateUIObject("BodyCard", main);
            SetTopLeft(bodyCard, 988, 110, 560, 620);
            AddImage(bodyCard, UIShapeFactory.RoundedRect(560, 620, 20, theme.cardPanel), theme.cardPanel);

            RectTransform bodyAnchor = CreateUIObject("BodyAnchor", bodyCard);
            SetTopLeft(bodyAnchor, 280, 340, 0, 0);
            BuildBodySilhouette(bodyAnchor);

            RectTransform caption = CreateUIObject("BodyCaption", bodyCard);
            SetTopLeft(caption, 20, 560, 520, 32);
            AddText(caption, bodyCaption, 15, theme.textSecondary, TextAlignmentOptions.Center);

            RectTransform sliver = CreateUIObject("NextPanelSliver", main);
            SetTopLeft(sliver, 1660 - 24, 110, 24, 620);
            AddImage(sliver, UIShapeFactory.RoundedRect(24, 620, 12, theme.cardPanelAlt), theme.cardPanelAlt);

            CreatePagination(main);
        }

        private RectTransform CreateInstructionCard(RectTransform parent)
        {
            RectTransform card = CreateUIObject("InstructionCard", parent);
            AddImage(card, UIShapeFactory.RoundedRect(900, 620, 20, theme.cardPanel), theme.cardPanel);

            RectTransform badge = CreateUIObject("StepBadge", card);
            SetTopLeft(badge, 40, 40, 40, 40);
            AddImage(badge, UIShapeFactory.Circle(40, theme.accentPrimary), theme.accentPrimary);
            RectTransform badgeLabel = CreateUIObject("BadgeLabel", badge);
            SetFullStretch(badgeLabel);
            AddText(badgeLabel, "1", 20, theme.textOnAccent, TextAlignmentOptions.Center, FontStyles.Bold);

            RectTransform heading = CreateUIObject("Heading", card);
            SetTopLeft(heading, 96, 46, 760, 32);
            AddText(heading, stepHeading, 24, theme.textPrimary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            RectTransform desc = CreateUIObject("Description", card);
            SetTopLeft(desc, 40, 110, 820, 100);
            var descText = AddText(desc, stepDescription, 17, theme.textSecondary, TextAlignmentOptions.TopLeft);
            descText.textWrappingMode = TextWrappingModes.Normal;

            RectTransform tip = CreateUIObject("TipCallout", card);
            SetTopLeft(tip, 40, 230, 820, 110);
            AddImage(tip, UIShapeFactory.RoundedRect(820, 110, 16, theme.cardPanelAlt), theme.cardPanelAlt);

            RectTransform tipIcon = CreateUIObject("TipIcon", tip);
            SetTopLeft(tipIcon, 20, 20, 32, 32);
            AddImage(tipIcon, UIShapeFactory.Circle(32, theme.accentPrimary), theme.accentPrimary);
            RectTransform tipIconLabel = CreateUIObject("TipIconLabel", tipIcon);
            SetFullStretch(tipIconLabel);
            AddText(tipIconLabel, "i", 18, theme.textOnAccent, TextAlignmentOptions.Center, FontStyles.Bold);

            RectTransform tipTextRect = CreateUIObject("TipText", tip);
            SetTopLeft(tipTextRect, 68, 16, 730, 80);
            var tipTMP = AddText(tipTextRect, tipText, 15, theme.textSecondary, TextAlignmentOptions.TopLeft);
            tipTMP.textWrappingMode = TextWrappingModes.Normal;

            RectTransform cta = CreateButton(card, ctaLabel, () => PlayDemo());
            SetTopLeft(cta, 40, 620 - 90, 380, 56);

            return card;
        }

        private void CreatePagination(RectTransform parent)
        {
            int count = 5;
            float spacing = 20;
            float totalWidth = (count - 1) * spacing;
            float startX = 48 + (900 + 560) / 2f - totalWidth / 2f;

            for (int i = 0; i < count; i++)
            {
                RectTransform dot = CreateUIObject($"PageDot{i}", parent);
                SetTopLeft(dot, startX + i * spacing, 760, 10, 10);
                Color c = i == 0 ? theme.accentPrimary : theme.trackerInactive;
                AddImage(dot, UIShapeFactory.Circle(10, c), c);
            }
        }

        private void BuildBodySilhouette(RectTransform anchor)
        {
            Vector2 neck      = new Vector2(0, 195);
            Vector2 hipCentre = new Vector2(0, 5);
            Vector2 chest     = new Vector2(0, 150);
            Vector2 headPos   = new Vector2(0, 230);

            CreateLimbSegment(anchor, chest, hipCentre, 140, theme.bodyFill);
            CreateLimbSegment(anchor, neck, chest, 40, theme.bodyFill);

            RectTransform pelvis = CreateUIObject("Pelvis", anchor);
            SetCentered(pelvis, hipCentre, new Vector2(130, 70));
            AddImage(pelvis, UIShapeFactory.RoundedRect(130, 70, 30, theme.bodyFill), theme.bodyFill);

            RectTransform head = CreateUIObject("Head", anchor);
            SetCentered(head, headPos, new Vector2(76, 76));
            AddImage(head, UIShapeFactory.Circle(76, theme.bodyFill), theme.bodyFill);

            CreateLimbSegment(anchor, ShoulderL, ElbowL, 34, theme.bodyFill);
            CreateLimbSegment(anchor, ElbowL, HandL, 28, theme.bodyFill);
            CreateLimbSegment(anchor, ShoulderR, ElbowR, 34, theme.bodyFill);
            CreateLimbSegment(anchor, ElbowR, HandR, 28, theme.bodyFill);

            CreateLimbSegment(anchor, HipL, KneeL, 46, theme.bodyFill);
            CreateLimbSegment(anchor, KneeL, FootL, 38, theme.bodyFill);
            CreateLimbSegment(anchor, HipR, KneeR, 46, theme.bodyFill);
            CreateLimbSegment(anchor, KneeR, FootR, 38, theme.bodyFill);

            foreach (var joint in TrackerJoints)
                CreateTrackerPoint(anchor, joint.label, joint.pos);
        }

        private void CreateLimbSegment(RectTransform parent, Vector2 a, Vector2 b, float thickness, Color color)
        {
            Vector2 mid = (a + b) / 2f;
            float length = Vector2.Distance(a, b);
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;

            RectTransform rt = CreateUIObject("Limb", parent);
            SetCentered(rt, mid, new Vector2(Mathf.Max(length, thickness), thickness));
            AddImage(rt, UIShapeFactory.RoundedRect(
                Mathf.RoundToInt(Mathf.Max(length, thickness)), Mathf.RoundToInt(thickness),
                Mathf.RoundToInt(thickness / 2f), color), color);
            rt.localEulerAngles = new Vector3(0, 0, angle);
        }

        private void CreateTrackerPoint(RectTransform parent, string label, Vector2 pos)
        {
            RectTransform glow = CreateUIObject($"Glow_{label}", parent);
            SetCentered(glow, pos, new Vector2(46, 46));
            Image glowImage = AddImage(glow, UIShapeFactory.SoftGlowCircle(46, theme.trackerActive), theme.trackerActive);
            glowImage.color = new Color(theme.trackerActive.r, theme.trackerActive.g, theme.trackerActive.b, 0f);

            RectTransform dot = CreateUIObject($"Tracker_{label}", parent);
            SetCentered(dot, pos, new Vector2(22, 22));
            Image dotImage = AddImage(dot, UIShapeFactory.Circle(22, theme.trackerInactive), theme.trackerInactive);

            RectTransform tooltip = CreateUIObject($"Tooltip_{label}", parent);
            SetCentered(tooltip, pos + new Vector2(0, 26), new Vector2(140, 26));
            TMP_Text tooltipText = AddText(tooltip, label, 13, theme.textPrimary, TextAlignmentOptions.Center, FontStyles.Bold);

            var trackerPoint = dot.gameObject.AddComponent<PortfolioTrackerPoint>();
            trackerPoint.Init(label, theme, dotImage, glowImage, tooltipText);
            trackerPoints.Add(trackerPoint);
        }

        private RectTransform CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.localScale = Vector3.one;
            return rt;
        }

        private Image AddImage(RectTransform rt, Sprite sprite, Color color)
        {
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        private TMP_Text AddText(RectTransform rt, string text, float size, Color color,
            TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
        {
            TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = style;
            if (font != null) tmp.font = font;
            return tmp;
        }

        private RectTransform CreateButton(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform btn = CreateUIObject("Button_" + label, parent);
            Image bg = AddImage(btn, UIShapeFactory.RoundedRect(380, 56, 28, theme.accentPrimary), theme.accentPrimary);

            Button button = btn.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.highlightedColor = theme.accentHover;
            colors.pressedColor = theme.accentHover;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);

            RectTransform label_ = CreateUIObject("Label", btn);
            SetFullStretch(label_);
            AddText(label_, label, 16, theme.textOnAccent, TextAlignmentOptions.Center, FontStyles.Bold);

            return btn;
        }

        private void SetTopLeft(RectTransform rt, float x, float yFromTop, float w, float h)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -yFromTop);
            rt.sizeDelta = new Vector2(w, h);
        }

        private void SetCentered(RectTransform rt, Vector2 centreLocalPos, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = centreLocalPos;
            rt.sizeDelta = size;
        }

        private void SetFullStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
