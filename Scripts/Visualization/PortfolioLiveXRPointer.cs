using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using TMPro;

namespace Mindzheimer.Portfolio
{
    /// <summary>
    /// Drives all three of MINDZHEIMER's real tracked points — Head, L Hand,
    /// R Hand — simultaneously from live Quest 3 tracking data, so the
    /// calibration screen reacts to actual headset + both controllers at
    /// once instead of the scripted auto-play demo.
    ///
    /// ANCHORING: the live skeleton is anchored to the ACTUAL rendered
    /// position of PortfolioCalibrationUIBuilder's static Head / L Hand /
    /// R Hand tracker dots on the humanoid silhouette (read once at start-up
    /// via RectTransformUtility, not hard-coded), so the live cursors rest
    /// exactly on top of the mannequin's own hand positions and the bone
    /// lines originate from the real Head dot — not an arbitrary screen
    /// point. Each hand cursor then moves within a bounded radius around
    /// that resting spot as you move your real hand.
    ///
    /// HEAD: has no "point and click" gesture — HMD tracking is simply on
    /// or off the moment the headset is worn. Its tracker dot mirrors live
    /// tracking validity every frame: lit while tracked, grey the instant
    /// tracking drops.
    ///
    /// L HAND / R HAND: each hand's position is measured RELATIVE to the
    /// headset's current pose (so it reacts to head movement too), then
    /// added on top of that hand's resting spot on the mannequin. Because
    /// resting arm position isn't near head height, each hand auto-centres
    /// on its first tracked frame, and can be re-zeroed anytime — press Y to
    /// recentre the left cursor, B to recentre the right one. Pulling either
    /// trigger while hovering a tracker dot fires the same click MINDZHEIMER's
    /// existing PortfolioTrackerPoint hover/click logic already handles.
    ///
    /// REQUIREMENTS: Quest 3 connected via Link cable or Air Link, AND an XR
    /// loader active under Project Settings > XR Plug-in Management > "PC,
    /// Mac & Linux Standalone" (separate from the Android tab used for
    /// MINDZHEIMER builds).
    ///
    /// SETUP: add this component next to PortfolioCalibrationUIBuilder on
    /// the same GameObject. Connect the headset, press Play. Remove/disable
    /// this component to fall back to the scripted demo.
    /// </summary>
    [RequireComponent(typeof(PortfolioCalibrationUIBuilder))]
    public class PortfolioLiveXRPointer : MonoBehaviour
    {
        [Header("Sources")]
        public bool trackHead      = true;
        public bool trackLeftHand  = true;
        public bool trackRightHand = true;
        [Tooltip("Stops PortfolioCalibrationUIBuilder's scripted demo loop so live input has sole control of the tracker dots.")]
        public bool disableAutoDemoOnStart = true;

        [Header("Mapping")]
        [Tooltip("Pixels of cursor movement per metre of hand movement relative to the recentred origin.")]
        public float sensitivity = 1400f;
        [Tooltip("Small hand movements below this (metres) are ignored, to stop the cursor jittering from hand tremor / tracking noise.")]
        public Vector2 deadzoneMeters = new Vector2(0.015f, 0.015f);
        [Tooltip("Maximum distance (pixels, reference-resolution units) a hand cursor can travel from its resting spot on the mannequin.")]
        public float maxTravelPixels = 260f;

        [Header("Cursor Visual")]
        public float cursorDiameter = 28f;
        public Color rightHandColor = new Color(1f, 1f, 1f, 0.9f);        // white
        public Color leftHandColor  = new Color(1f, 0.68f, 0.26f, 0.9f);  // amber

        [Header("Skeleton Bone Lines")]
        public bool showBoneLines = true;
        public float boneThickness = 6f;
        public float anchorDiameter = 20f;

        [Header("Diagnostics")]
        [Tooltip("Shows a live tracking-status panel bottom-left in Play Mode. Turn off once everything's confirmed working.")]
        public bool showDiagnostics = true;

        private class HandPointer
        {
            public readonly XRNode node;
            public readonly Color color;
            public readonly string label;
            public Vector2 basePos;
            public RectTransform cursor;
            public RectTransform boneLine;
            public GameObject currentHover;
            public bool wasTriggerPressed;
            public bool wasRecenterPressed;
            public bool hasCentred;
            public Vector3 centreOffset;
            public bool lastValid;
            public Vector3 lastPos;

            public HandPointer(XRNode node, Color color, string label)
            {
                this.node = node;
                this.color = color;
                this.label = label;
            }
        }

        private PortfolioCalibrationUIBuilder builder;
        private RectTransform canvasRect;
        private RectTransform skeletonAnchor;
        private TMP_Text diagnosticText;
        private string loaderStatus = "checking...";

        private HandPointer leftHand;
        private HandPointer rightHand;

        private void Start()
        {
            builder = GetComponent<PortfolioCalibrationUIBuilder>();
            DetectXRLoader();
            StartCoroutine(InitWhenReady());
        }

        private void DetectXRLoader()
        {
            try
            {
                var settings = XRGeneralSettings.Instance;
                var activeLoader = settings != null && settings.Manager != null ? settings.Manager.activeLoader : null;
                loaderStatus = activeLoader != null
                    ? activeLoader.name
                    : "NONE — no XR loader active for this platform";
            }
            catch (System.Exception e)
            {
                loaderStatus = $"unknown (XR Management not accessible: {e.Message})";
            }

            Debug.Log($"[PortfolioLiveXRPointer] Active XR loader: {loaderStatus}");
        }

        private IEnumerator InitWhenReady()
        {
            float timeout = 3f;
            while (builder.CanvasRect == null && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (builder.CanvasRect == null)
            {
                Debug.LogWarning("[PortfolioLiveXRPointer] Canvas never became available — is PortfolioCalibrationUIBuilder on the same GameObject?");
                yield break;
            }

            canvasRect = builder.CanvasRect;

            if (disableAutoDemoOnStart)
                builder.StopDemo();

            Color accent = builder.theme != null ? builder.theme.accentPrimary : new Color(0.2f, 0.89f, 0.77f, 1f);
            CreateSkeletonAnchor(accent);

            if (trackLeftHand)
            {
                leftHand = new HandPointer(XRNode.LeftHand, leftHandColor, "L Hand");
                if (showBoneLines) leftHand.boneLine = CreateBoneLineVisual(leftHandColor);
                CreateCursorFor(leftHand);
            }
            if (trackRightHand)
            {
                rightHand = new HandPointer(XRNode.RightHand, rightHandColor, "R Hand");
                if (showBoneLines) rightHand.boneLine = CreateBoneLineVisual(rightHandColor);
                CreateCursorFor(rightHand);
            }

            CreateStatusLabel();
            if (showDiagnostics) CreateDiagnosticPanel();
        }

        private Vector2 GetStaticAnchoredPosition(string label)
        {
            var point = builder.GetTrackerPoint(label);
            if (point == null) return Vector2.zero;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, point.transform.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 local);
            return local;
        }

        private void CreateSkeletonAnchor(Color color)
        {
            Vector2 anchorPos = GetStaticAnchoredPosition("Head");

            GameObject glow = new GameObject("SkeletonAnchorGlow", typeof(RectTransform));
            glow.transform.SetParent(canvasRect, false);
            RectTransform glowRt = glow.GetComponent<RectTransform>();
            glowRt.anchorMin = glowRt.anchorMax = glowRt.pivot = new Vector2(0.5f, 0.5f);
            glowRt.sizeDelta = new Vector2(anchorDiameter * 2.6f, anchorDiameter * 2.6f);
            glowRt.anchoredPosition = anchorPos;
            Image glowImg = glow.AddComponent<Image>();
            glowImg.sprite = UIShapeFactory.SoftGlowCircle(Mathf.RoundToInt(anchorDiameter * 2.6f), color);
            glowImg.color = new Color(color.r, color.g, color.b, 0.5f);
            glowImg.raycastTarget = false;

            GameObject dot = new GameObject("SkeletonAnchor", typeof(RectTransform));
            dot.transform.SetParent(canvasRect, false);
            RectTransform dotRt = dot.GetComponent<RectTransform>();
            dotRt.anchorMin = dotRt.anchorMax = dotRt.pivot = new Vector2(0.5f, 0.5f);
            dotRt.sizeDelta = new Vector2(anchorDiameter, anchorDiameter);
            dotRt.anchoredPosition = anchorPos;
            Image dotImg = dot.AddComponent<Image>();
            dotImg.sprite = UIShapeFactory.Circle(Mathf.RoundToInt(anchorDiameter), color);
            dotImg.color = color;
            dotImg.raycastTarget = false;

            skeletonAnchor = dotRt;
        }

        private RectTransform CreateBoneLineVisual(Color color)
        {
            GameObject go = new GameObject("BoneLine", typeof(RectTransform));
            go.transform.SetParent(canvasRect, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200f, boneThickness);
            rt.anchoredPosition = Vector2.zero;

            Image img = go.AddComponent<Image>();
            img.sprite = UIShapeFactory.RoundedRect(200, Mathf.RoundToInt(boneThickness), Mathf.RoundToInt(boneThickness / 2f), color);
            img.type = Image.Type.Sliced;
            img.color = new Color(color.r, color.g, color.b, 0.85f);
            img.raycastTarget = false;

            go.SetActive(false);
            return rt;
        }

        private void CreateCursorFor(HandPointer hp)
        {
            hp.basePos = GetStaticAnchoredPosition(hp.label);

            GameObject go = new GameObject($"LiveCursor_{hp.node}", typeof(RectTransform));
            go.transform.SetParent(canvasRect, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cursorDiameter, cursorDiameter);
            rt.anchoredPosition = hp.basePos;

            Image img = go.AddComponent<Image>();
            img.sprite = UIShapeFactory.Circle(Mathf.RoundToInt(cursorDiameter), hp.color);
            img.color = hp.color;
            img.raycastTarget = false;

            rt.SetAsLastSibling();
            hp.cursor = rt;
        }

        private void CreateStatusLabel()
        {
            GameObject go = new GameObject("LiveStatusLabel", typeof(RectTransform));
            go.transform.SetParent(canvasRect, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-24, -84);
            rt.sizeDelta = new Vector2(340, 40);

            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font == null) font = TMP_Settings.defaultFontAsset;

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = "\u25CF LIVE TRACKING\nY = recentre left \u00b7 B = recentre right";
            tmp.fontSize = 13;
            tmp.color = new Color(1f, 1f, 1f, 0.9f);
            tmp.alignment = TextAlignmentOptions.TopRight;
            if (font != null) tmp.font = font;
        }

        private void CreateDiagnosticPanel()
        {
            GameObject bg = new GameObject("DiagnosticPanel", typeof(RectTransform));
            bg.transform.SetParent(canvasRect, false);
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = bgRect.anchorMax = new Vector2(0, 0);
            bgRect.pivot = new Vector2(0, 0);
            bgRect.anchoredPosition = new Vector2(24, 24);
            bgRect.sizeDelta = new Vector2(540, 130);

            Image panelImg = bg.AddComponent<Image>();
            panelImg.sprite = UIShapeFactory.RoundedRect(540, 130, 12, new Color(0f, 0f, 0f, 0.55f));
            panelImg.color = new Color(0f, 0f, 0f, 0.55f);
            panelImg.raycastTarget = false;

            GameObject go = new GameObject("DiagnosticText", typeof(RectTransform));
            go.transform.SetParent(bgRect, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(14, 10);
            rt.offsetMax = new Vector2(-14, -10);

            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font == null) font = TMP_Settings.defaultFontAsset;

            diagnosticText = go.AddComponent<TextMeshProUGUI>();
            diagnosticText.fontSize = 14;
            diagnosticText.color = Color.white;
            diagnosticText.alignment = TextAlignmentOptions.TopLeft;
            if (font != null) diagnosticText.font = font;
            diagnosticText.text = "Waiting for first frame...";
        }

        private void Update()
        {
            if (canvasRect == null) return;

            bool headValid = TryGetPose(XRNode.Head, out Vector3 headPos, out Quaternion headRot);

            if (trackHead)
            {
                var headPoint = builder.GetTrackerPoint("Head");
                if (headPoint != null) headPoint.SetCalibrated(headValid);
            }

            if (leftHand != null)  UpdateHand(leftHand, headValid, headPos, headRot);
            if (rightHand != null) UpdateHand(rightHand, headValid, headPos, headRot);

            UpdateBoneLines(headValid);
            UpdateDiagnostics(headValid, headPos);
        }

        private void UpdateHand(HandPointer hp, bool headValid, Vector3 headPos, Quaternion headRot)
        {
            bool handValid = TryGetPose(hp.node, out Vector3 handPos, out Quaternion _);
            hp.lastValid = handValid;
            hp.lastPos = handPos;

            if (hp.cursor == null) return;
            if (!headValid || !handValid) return;

            Vector3 relative = Quaternion.Inverse(headRot) * (handPos - headPos);

            if (!hp.hasCentred)
            {
                hp.centreOffset = relative;
                hp.hasCentred = true;
            }

            bool recenterPressed = IsButtonPressed(hp.node, CommonUsages.secondaryButton);
            if (recenterPressed && !hp.wasRecenterPressed)
                hp.centreOffset = relative;
            hp.wasRecenterPressed = recenterPressed;

            Vector3 centred = relative - hp.centreOffset;

            float dx = Mathf.Abs(centred.x) < deadzoneMeters.x ? 0f : centred.x;
            float dy = Mathf.Abs(centred.y) < deadzoneMeters.y ? 0f : centred.y;

            Vector2 delta = new Vector2(dx, dy) * sensitivity;
            delta = Vector2.ClampMagnitude(delta, maxTravelPixels);

            hp.cursor.anchoredPosition = hp.basePos + delta;

            UpdateHoverAndClick(hp);
        }

        private void UpdateHoverAndClick(HandPointer hp)
        {
            if (EventSystem.current == null) return;

            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, hp.cursor.position)
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            GameObject hit = results.Count > 0 ? results[0].gameObject : null;

            if (hit != hp.currentHover)
            {
                if (hp.currentHover != null)
                    ExecuteEvents.ExecuteHierarchy(hp.currentHover, pointerData, ExecuteEvents.pointerExitHandler);
                if (hit != null)
                    ExecuteEvents.ExecuteHierarchy(hit, pointerData, ExecuteEvents.pointerEnterHandler);
                hp.currentHover = hit;
            }

            bool triggerPressed = IsButtonPressed(hp.node, CommonUsages.triggerButton);
            if (triggerPressed && !hp.wasTriggerPressed && hp.currentHover != null)
                ExecuteEvents.ExecuteHierarchy(hp.currentHover, pointerData, ExecuteEvents.pointerClickHandler);
            hp.wasTriggerPressed = triggerPressed;
        }

        private void UpdateBoneLines(bool headValid)
        {
            if (!showBoneLines || skeletonAnchor == null) return;

            UpdateBoneLine(leftHand, headValid);
            UpdateBoneLine(rightHand, headValid);
        }

        private void UpdateBoneLine(HandPointer hp, bool headValid)
        {
            if (hp == null || hp.boneLine == null) return;

            bool show = headValid && hp.lastValid && hp.cursor != null;
            if (hp.boneLine.gameObject.activeSelf != show)
                hp.boneLine.gameObject.SetActive(show);

            if (!show) return;

            Vector2 a = skeletonAnchor.anchoredPosition;
            Vector2 b = hp.cursor.anchoredPosition;
            Vector2 mid = (a + b) / 2f;
            float length = Vector2.Distance(a, b);
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;

            hp.boneLine.anchoredPosition = mid;
            hp.boneLine.sizeDelta = new Vector2(Mathf.Max(length, boneThickness), boneThickness);
            hp.boneLine.localEulerAngles = new Vector3(0, 0, angle);
        }

        private void UpdateDiagnostics(bool headValid, Vector3 headPos)
        {
            if (diagnosticText == null) return;

            string headLine = headValid
                ? $"Head:   OK   pos {headPos.ToString("F2")}"
                : "Head:   NOT TRACKED";
            string leftLine  = DescribeHand(leftHand,  "L Hand");
            string rightLine = DescribeHand(rightHand, "R Hand");

            diagnosticText.text = $"XR Loader: {loaderStatus}\n{headLine}\n{leftLine}\n{rightLine}";
        }

        private static string DescribeHand(HandPointer hp, string label)
        {
            if (hp == null) return $"{label}: disabled";
            if (!hp.lastValid) return $"{label}: NOT TRACKED";
            return $"{label}: OK   pos {hp.lastPos.ToString("F2")}   centred={hp.hasCentred}";
        }

        private static bool TryGetPose(XRNode node, out Vector3 position, out Quaternion rotation)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (!device.isValid) return false;

            bool gotPos = device.TryGetFeatureValue(CommonUsages.devicePosition, out position);
            bool gotRot = device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
            return gotPos && gotRot;
        }

        private static bool IsButtonPressed(XRNode node, InputFeatureUsage<bool> usage)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid) return false;
            return device.TryGetFeatureValue(usage, out bool pressed) && pressed;
        }
    }
}
