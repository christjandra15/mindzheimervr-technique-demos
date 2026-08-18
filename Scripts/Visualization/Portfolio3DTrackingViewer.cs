using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.Rendering;
using TMPro;

namespace Mindzheimer.Portfolio
{
    /// <summary>
    /// Live 3D skeleton viewer for MINDZHEIMER's real tracked points — Head,
    /// L Hand, R Hand — shown as world-space markers moving through actual
    /// 3D space, connected by bone lines, plus a forward-facing gaze ray +
    /// reticle mirroring the real HMDGazeTracker.cs. This is the SlimeVR-
    /// style "3D avatar view" companion to PortfolioCalibrationUIBuilder's
    /// flat calibration screen — a separate, standalone piece.
    ///
    /// TWO CAMERAS, BOTH LIVE — swap between them with the Game view's
    /// Display dropdown during Play:
    ///   • Display 1 — World Camera (Camera.main): fixed third-person view
    ///     of the whole interaction, anchored to roomOffset. Renders
    ///     everything including the Head sphere marker.
    ///   • Display 2 — Head Camera: follows live head pose every frame for
    ///     a first-person POV view. The Head sphere is excluded from THIS
    ///     camera only (via headOnlyLayer) since the camera sits inside it;
    ///     Display 1 still shows it normally.
    /// If frame rate suffers, keep only ONE Game view tab open and swap its
    /// Display dropdown — the cost is two windows streaming simultaneously,
    /// not the cameras themselves.
    ///
    /// GRAB / TRIGGER INDICATORS: each hand sphere tints and swells in
    /// proportion to that controller's ANALOG trigger and grip values (0-1,
    /// not just pressed/not-pressed) — a hard squeeze reads differently to
    /// a light one, which shows up far better on video than a binary pop.
    /// This mirrors the real system's data: MINDZHEIMER's trajectory_inputs
    /// CSVs log per-sample trigger/grip magnitudes for exactly this reason.
    /// Trigger and grip get distinct colours and blend when both are held.
    ///
    /// GAZE FEEDBACK: world-space ray + reticle (mirrors HMDGazeTracker.cs)
    /// plus a fixed centre-of-view HUD dot parented to each camera.
    /// gazeDirectionSmoothing damps sensor-noise jitter; minHitDwellSeconds
    /// debounces hit/miss/target changes so edge-noise doesn't flicker.
    ///
    /// GAZE TARGET CLEARANCE: each of the six placeholder objects has a
    /// designed "base" position on the ring around roomOffset, but every
    /// frame is pushed outward from BOTH the live head position AND the
    /// World Camera's fixed position, so it never renders closer than
    /// minTargetClearance to either.
    ///
    /// COORDINATE SPACE: the head/hand avatar is drawn at RAW, unshifted XR
    /// device positions, identical to what Unity's real headset stereo
    /// rendering uses. roomOffset (re-taken on first tracked frame and on
    /// recentre — Y left controller, B right controller) anchors the target
    /// ring's base positions and the World Camera, never the avatar itself.
    ///
    /// SKELETON: Head, a virtual chest point offset straight down from Head
    /// by chestDropMeters, then bones out to each hand. Intentionally
    /// minimal — MINDZHEIMER only tracks 3 points.
    ///
    /// PURELY OBSERVATIONAL — no hover/click/calibration logic here.
    ///
    /// REQUIREMENTS: Quest 3 connected via Link cable or Air Link, AND an XR
    /// loader active under Project Settings > XR Plug-in Management > "PC,
    /// Mac & Linux Standalone".
    ///
    /// SETUP: new empty Scene, empty GameObject, add this component, press
    /// Play. Use the Game view's Display dropdown to swap between Display 1
    /// (environment) and Display 2 (POV).
    /// </summary>
    public class Portfolio3DTrackingViewer : MonoBehaviour
    {
        [Header("Optional — reuse the calibration screen's colour palette")]
        public PortfolioUITheme theme;

        [Header("Sources")]
        public bool trackHead      = true;
        public bool trackLeftHand  = true;
        public bool trackRightHand = true;

        [Header("Sizes (metres)")]
        public float headRadius      = 0.12f;
        public float handRadius      = 0.05f;
        public float boneWidth       = 0.02f;
        public float chestDropMeters = 0.45f;

        [Header("Grab / Trigger Indicators")]
        [Tooltip("Hand spheres tint and swell in proportion to analog trigger/grip pressure.")]
        public bool showInteractionIndicators = true;
        [Tooltip("Colour the hand tints toward at full trigger pull.")]
        public Color triggerColor = new Color(1f, 0.85f, 0.2f, 1f);   // warm yellow
        [Tooltip("Colour the hand tints toward at full grip squeeze.")]
        public Color gripColor    = new Color(0.35f, 0.75f, 1f, 1f);  // cyan-blue
        [Tooltip("How much bigger the hand sphere gets at full press. 1 = no size change.")]
        public float pressedScaleMultiplier = 1.7f;
        [Tooltip("Below this analog value, input is treated as zero — filters resting-finger noise on the trigger.")]
        public float inputDeadzone = 0.05f;
        [Tooltip("How fast the tint/scale follows the analog value. Higher = snappier.")]
        public float indicatorSmoothing = 14f;

        [Header("Gaze")]
        public bool showGaze          = true;
        public float maxGazeDistance  = 5f;
        public float gazeRayWidth     = 0.012f;
        public float reticleDiameter  = 0.25f;
        public Color gazeHitColor     = new Color(0.35f, 0.95f, 0.4f, 0.9f);  // matches HMDGazeTracker's rayColorHit (green)
        public Color gazeMissColor    = new Color(0.9f, 0.35f, 0.3f, 0.5f);   // matches HMDGazeTracker's rayColorMiss (red), dimmer

        [Header("Gaze Stability (fixes ray/colour flicker)")]
        [Tooltip("Higher = snappier response to head movement, lower = smoother but slightly laggier.")]
        public float gazeDirectionSmoothing = 10f;
        [Tooltip("A hit/miss or target change must hold steady this long before the ray colour/target actually updates.")]
        public float minHitDwellSeconds = 0.1f;

        [Header("Gaze HUD — fixed centre-of-view dot")]
        public bool showCenterReticle       = true;
        public float centerReticleDistance  = 0.6f;  // metres in front of the camera — comfortable VR HUD depth
        public float centerReticleSize      = 0.015f;

        [Header("Gaze Targets (ring radius/heights are per-object below)")]
        public bool showGazeTargets = true;
        public float targetSize     = 0.35f;
        [Tooltip("No target can ever render closer than this to EITHER your live head position OR the World Camera's fixed position — pushed outward dynamically every frame, not just recomputed at recentre time.")]
        public float minTargetClearance = 1.0f;

        [Header("World Camera — Display 1 (3rd-person environment view)")]
        public Vector3 cameraOffsetFromRoom = new Vector3(0f, 1.4f, -2.2f); // relative to roomOffset
        public Vector3 cameraLookOffsetFromRoom = new Vector3(0f, 1.3f, 0f);
        public float cameraFOV       = 55f;
        public Color backgroundColor = new Color(0.043f, 0.059f, 0.078f, 1f); // matches theme.background default

        [Header("Head Camera — Display 2 (POV, follows live head pose)")]
        public bool showHeadCamera     = true;
        public float headCamFOV        = 90f;
        public int headCamTargetDisplay = 1; // Unity Displays are 0-indexed; "Display 2" in the UI
        public int headOnlyLayer       = 31; // hides the Head sphere from the Head Camera only — Display 1 still shows it

        [Header("Floor")]
        public bool showFloor  = true;
        public float floorSize = 6f;
        public int floorTextureSize = 128; // one-time cost at Start()
        public Color floorColor    = new Color(0.09f, 0.11f, 0.14f, 1f);
        public Color gridLineColor = new Color(0.2f, 0.24f, 0.28f, 1f);

        [Header("Diagnostics")]
        public bool showDiagnostics = true;
        [Tooltip("How often the diagnostics text refreshes, in seconds. TMP rebuilds its mesh on every text change, so updating every frame at 90-120Hz is wasted cost for text nobody reads that fast.")]
        public float diagnosticsUpdateInterval = 0.15f;

        private class HandVisual
        {
            public readonly XRNode node;
            public GameObject sphere;
            public Material sphereMat; // cached — avoids GetComponent<Renderer>() every frame
            public Color baseColor;
            public float baseDiameter;
            public LineRenderer bone;
            public bool lastValid;
            public Vector3 lastPos;
            public float trigger;      // smoothed analog 0-1
            public float grip;         // smoothed analog 0-1
            public HandVisual(XRNode node) { this.node = node; }
        }

        private struct GazeTargetDef
        {
            public string name;
            public float angleDeg, height, distance;
            public bool isCube;
            public Color color;
        }

        private static readonly GazeTargetDef[] TargetDefs =
        {
            new GazeTargetDef { name = "Coffee Mug",    angleDeg = 0f,   height = 1.3f, distance = 1.8f, isCube = true,  color = new Color(0.95f, 0.55f, 0.35f) },
            new GazeTargetDef { name = "Plant Pot",     angleDeg = 60f,  height = 0.3f, distance = 1.5f, isCube = false, color = new Color(0.45f, 0.75f, 0.4f) },
            new GazeTargetDef { name = "Picture Frame", angleDeg = 120f, height = 1.8f, distance = 2.2f, isCube = true,  color = new Color(0.85f, 0.4f, 0.75f) },
            new GazeTargetDef { name = "Table Lamp",    angleDeg = 180f, height = 1.3f, distance = 2.0f, isCube = false, color = new Color(0.9f, 0.85f, 0.35f) },
            new GazeTargetDef { name = "Book Stack",    angleDeg = 240f, height = 0.3f, distance = 1.6f, isCube = true,  color = new Color(0.5f, 0.65f, 0.95f) },
            new GazeTargetDef { name = "Wall Clock",    angleDeg = 300f, height = 1.7f, distance = 2.4f, isCube = false, color = new Color(0.75f, 0.6f, 0.95f) },
        };

        private Camera worldCam; // cached — avoids repeated Camera.main lookups every frame

        private GameObject headSphere;
        private LineRenderer headChestBone;
        private HandVisual leftHand;
        private HandVisual rightHand;
        private Camera headCam;

        private LineRenderer gazeRay;
        private GameObject gazeReticle;
        private Material centerDotWorldMat; // cached — avoids GetComponent<Renderer>() every frame
        private Material centerDotHeadMat;

        private Quaternion smoothedHeadRot;
        private bool hasSmoothedRot;
        private string pendingHitKey;   // raw, unfiltered — null means "miss"
        private float pendingHitSince;
        private string committedHitKey; // debounced — what's actually displayed

        private bool lastGazeHit;
        private float lastGazeDistance;
        private string lastGazeTarget;

        private readonly List<GameObject> gazeTargetObjects = new List<GameObject>();
        private readonly List<Vector3> gazeTargetBasePositions = new List<Vector3>();
        private bool gazeTargetsCreated;

        private bool hasRecentredRoom;
        private Vector3 roomOffset; // X/Z only (Y stays 0) — anchor for target base positions + World Camera only, NEVER applied to the avatar
        private bool wasRoomRecenterPressed;

        private TMP_Text diagnosticText;
        private string loaderStatus = "checking...";
        private float nextDiagnosticsUpdateTime;

        private void Start()
        {
            DetectXRLoader();
            EnsureWorldCamera();
            if (showHeadCamera) CreateHeadCamera();
            if (showFloor) CreateFloor();

            Color headColor  = theme != null ? theme.accentPrimary : new Color(0.2f, 0.89f, 0.77f, 1f);
            Color leftColor  = new Color(1f, 0.68f, 0.26f, 1f);  // amber, matches the 2D screen's left-hand colour
            Color rightColor = new Color(1f, 1f, 1f, 1f);        // white, matches the 2D screen's right-hand colour

            if (trackHead)
            {
                headSphere = CreateSphere("Head", headRadius, headColor);
                headSphere.layer = headOnlyLayer; // excluded from the Head Camera's culling mask only
                headChestBone = CreateBone("Bone_HeadChest", headColor, boneWidth);

                if (showGaze)
                {
                    gazeRay = CreateBone("GazeRay", gazeMissColor, gazeRayWidth);
                    gazeReticle = CreateReticle(gazeHitColor);

                    if (showCenterReticle)
                    {
                        if (worldCam != null)
                            centerDotWorldMat = CreateFixedGazeDot(worldCam.transform, gazeMissColor).GetComponent<Renderer>().material;
                        if (headCam != null)
                            centerDotHeadMat = CreateFixedGazeDot(headCam.transform, gazeMissColor).GetComponent<Renderer>().material;
                    }
                }
            }
            if (trackLeftHand)
            {
                leftHand = new HandVisual(XRNode.LeftHand);
                SetupHandVisual(leftHand, "L Hand", leftColor);
            }
            if (trackRightHand)
            {
                rightHand = new HandVisual(XRNode.RightHand);
                SetupHandVisual(rightHand, "R Hand", rightColor);
            }

            if (showDiagnostics) StartCoroutine(CreateDiagnosticsOverlay());
        }

        private void SetupHandVisual(HandVisual hv, string label, Color color)
        {
            hv.sphere = CreateSphere(label, handRadius, color);
            hv.sphereMat = hv.sphere.GetComponent<Renderer>().material;
            hv.baseColor = color;
            hv.baseDiameter = handRadius * 2f;
            hv.bone = CreateBone($"Bone_Chest{label.Replace(" ", "")}", color, boneWidth);
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

            Debug.Log($"[Portfolio3DTrackingViewer] Active XR loader: {loaderStatus}");
        }

        private void EnsureWorldCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camGO = new GameObject("WorldCamera", typeof(Camera));
                camGO.tag = "MainCamera";
                cam = camGO.GetComponent<Camera>();
            }

            cam.fieldOfView = cameraFOV;
            cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
            cam.targetDisplay = 0;

            worldCam = cam;
        }

        private void RepositionWorldCamera()
        {
            if (worldCam == null) return;

            Vector3 pos = roomOffset + cameraOffsetFromRoom;
            Vector3 look = roomOffset + cameraLookOffsetFromRoom;
            worldCam.transform.position = pos;
            worldCam.transform.rotation = Quaternion.LookRotation((look - pos).normalized, Vector3.up);
        }

        private void CreateHeadCamera()
        {
            GameObject go = new GameObject("HeadCamera_POV", typeof(Camera));
            headCam = go.GetComponent<Camera>();
            headCam.fieldOfView = headCamFOV;
            headCam.nearClipPlane = 0.05f;
            headCam.clearFlags = CameraClearFlags.SolidColor;
            headCam.backgroundColor = backgroundColor;
            headCam.targetDisplay = headCamTargetDisplay;
            headCam.cullingMask = ~(1 << headOnlyLayer);
        }

        private static Shader GetUnlitShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Sprites/Default");
            return s;
        }

        private static void DisableShadows(Renderer rend)
        {
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        private GameObject CreateSphere(string name, float radius, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.localScale = Vector3.one * (radius * 2f);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = go.GetComponent<Renderer>();
            var mat = new Material(GetUnlitShader());
            mat.color = color;
            rend.material = mat;
            DisableShadows(rend);

            go.SetActive(false);
            return go;
        }

        private GameObject CreateReticle(Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "GazeReticle";
            go.transform.localScale = new Vector3(reticleDiameter, 0.01f, reticleDiameter);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = go.GetComponent<Renderer>();
            var mat = new Material(GetUnlitShader());
            mat.color = color;
            rend.material = mat;
            DisableShadows(rend);

            go.SetActive(false);
            return go;
        }

        private GameObject CreateFixedGazeDot(Transform parent, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "GazeCenterDot";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, centerReticleDistance);
            go.transform.localScale = Vector3.one * centerReticleSize;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = go.GetComponent<Renderer>();
            var mat = new Material(GetUnlitShader());
            mat.color = color;
            rend.material = mat;
            DisableShadows(rend);

            return go;
        }

        private LineRenderer CreateBone(string name, Color color, float width)
        {
            GameObject go = new GameObject(name);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 4;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            var mat = new Material(GetUnlitShader());
            mat.color = color;
            lr.material = mat;
            lr.enabled = false;
            return lr;
        }

        private void CreateFloor()
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Floor";
            plane.transform.position = Vector3.zero;
            plane.transform.localScale = new Vector3(floorSize / 10f, 1f, floorSize / 10f);
            var col = plane.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = plane.GetComponent<Renderer>();
            var mat = new Material(GetUnlitShader());
            mat.mainTexture = CreateGridTexture(floorTextureSize, Mathf.Max(4, floorTextureSize / 8), floorColor, gridLineColor);
            mat.mainTextureScale = new Vector2(floorSize, floorSize);
            rend.material = mat;
            DisableShadows(rend);
        }

        private void CreateOrRepositionGazeTargets()
        {
            if (!showGazeTargets) return;

            if (!gazeTargetsCreated)
            {
                foreach (var d in TargetDefs)
                {
                    GameObject go = GameObject.CreatePrimitive(d.isCube ? PrimitiveType.Cube : PrimitiveType.Sphere);
                    go.name = d.name;
                    go.transform.localScale = Vector3.one * targetSize;

                    var rend = go.GetComponent<Renderer>();
                    var mat = new Material(GetUnlitShader());
                    mat.color = d.color;
                    rend.material = mat;
                    DisableShadows(rend);

                    gazeTargetObjects.Add(go);
                    gazeTargetBasePositions.Add(Vector3.zero);
                }
                gazeTargetsCreated = true;
            }

            for (int i = 0; i < TargetDefs.Length; i++)
            {
                var d = TargetDefs[i];
                float rad = d.angleDeg * Mathf.Deg2Rad;
                Vector3 basePos = new Vector3(
                    roomOffset.x + Mathf.Sin(rad) * d.distance,
                    d.height,
                    roomOffset.z + Mathf.Cos(rad) * d.distance);
                gazeTargetBasePositions[i] = basePos;
                gazeTargetObjects[i].transform.position = basePos;
            }
        }

        private void UpdateGazeTargetClearance(Vector3 headPos)
        {
            bool haveWorldCam = worldCam != null;
            Vector3 worldCamPos = haveWorldCam ? worldCam.transform.position : Vector3.zero;

            for (int i = 0; i < gazeTargetObjects.Count; i++)
            {
                Vector3 pos = gazeTargetBasePositions[i];
                pos = ClearFrom(pos, headPos, minTargetClearance);
                if (haveWorldCam)
                    pos = ClearFrom(pos, worldCamPos, minTargetClearance);
                gazeTargetObjects[i].transform.position = pos;
            }
        }

        private static Vector3 ClearFrom(Vector3 pos, Vector3 avoidPoint, float minDist)
        {
            Vector3 delta = pos - avoidPoint;
            float dist = delta.magnitude;

            if (dist <= 0.0001f)
                return avoidPoint + Vector3.forward * minDist;

            if (dist < minDist)
                return avoidPoint + (delta / dist) * minDist;

            return pos;
        }

        private static Texture2D CreateGridTexture(int size, int cell, Color bg, Color line)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool onLine = (x % cell == 0) || (y % cell == 0);
                    tex.SetPixel(x, y, onLine ? line : bg);
                }
            }
            tex.Apply();
            return tex;
        }

        private IEnumerator CreateDiagnosticsOverlay()
        {
            GameObject canvasGO = new GameObject("DiagnosticsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = 0;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();

            GameObject bg = new GameObject("DiagnosticPanel", typeof(RectTransform));
            bg.transform.SetParent(canvasRect, false);
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = bgRect.anchorMax = new Vector2(0, 0);
            bgRect.pivot = new Vector2(0, 0);
            bgRect.anchoredPosition = new Vector2(24, 24);
            bgRect.sizeDelta = new Vector2(560, 170);
            Image panelImg = bg.AddComponent<Image>();
            panelImg.sprite = UIShapeFactory.RoundedRect(560, 170, 12, new Color(0f, 0f, 0f, 0.55f));
            panelImg.color = new Color(0f, 0f, 0f, 0.55f);
            panelImg.raycastTarget = false;

            GameObject textGO = new GameObject("DiagnosticText", typeof(RectTransform));
            textGO.transform.SetParent(bgRect, false);
            RectTransform textRt = textGO.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(14, 10);
            textRt.offsetMax = new Vector2(-14, -10);

            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font == null) font = TMP_Settings.defaultFontAsset;

            diagnosticText = textGO.AddComponent<TextMeshProUGUI>();
            diagnosticText.fontSize = 14;
            diagnosticText.color = Color.white;
            diagnosticText.alignment = TextAlignmentOptions.TopLeft;
            if (font != null) diagnosticText.font = font;
            diagnosticText.text = "Waiting for first frame...";

            GameObject labelGO = new GameObject("StatusLabel", typeof(RectTransform));
            labelGO.transform.SetParent(canvasRect, false);
            RectTransform labelRt = labelGO.GetComponent<RectTransform>();
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(1, 1);
            labelRt.pivot = new Vector2(1, 1);
            labelRt.anchoredPosition = new Vector2(-24, -24);
            labelRt.sizeDelta = new Vector2(360, 40);
            TextMeshProUGUI labelTmp = labelGO.AddComponent<TextMeshProUGUI>();
            labelTmp.text = "\u25CF LIVE 3D \u2014 Y/B to recentre \u00b7 Display 2 = head cam";
            labelTmp.fontSize = 14;
            labelTmp.color = new Color(1f, 1f, 1f, 0.9f);
            labelTmp.alignment = TextAlignmentOptions.TopRight;
            if (font != null) labelTmp.font = font;

            yield break;
        }

        private void Update()
        {
            bool headValid = TryGetPose(XRNode.Head, out Vector3 headPos, out Quaternion headRot);
            bool justRecentred = false;

            if (headValid && !hasRecentredRoom)
            {
                roomOffset = new Vector3(headPos.x, 0f, headPos.z);
                hasRecentredRoom = true;
                justRecentred = true;
            }

            bool recenterPressed =
                (trackLeftHand  && IsButtonPressed(XRNode.LeftHand,  CommonUsages.secondaryButton)) ||
                (trackRightHand && IsButtonPressed(XRNode.RightHand, CommonUsages.secondaryButton));
            if (recenterPressed && !wasRoomRecenterPressed && headValid)
            {
                roomOffset = new Vector3(headPos.x, 0f, headPos.z);
                justRecentred = true;
            }
            wasRoomRecenterPressed = recenterPressed;

            if (justRecentred)
            {
                CreateOrRepositionGazeTargets();
                RepositionWorldCamera();
            }

            if (headValid && gazeTargetsCreated)
                UpdateGazeTargetClearance(headPos);

            Vector3 chestPos = headPos - Vector3.up * chestDropMeters;

            if (trackHead && headSphere != null)
            {
                headSphere.SetActive(headValid);
                if (headValid) headSphere.transform.position = headPos;
            }
            if (headChestBone != null)
            {
                headChestBone.enabled = headValid;
                if (headValid)
                {
                    headChestBone.SetPosition(0, headPos);
                    headChestBone.SetPosition(1, chestPos);
                }
            }

            if (headCam != null && headValid)
            {
                headCam.transform.position = headPos;
                headCam.transform.rotation = headRot;
            }

            if (showGaze && trackHead) UpdateGaze(headValid, headPos, headRot);

            if (leftHand != null)  UpdateHand(leftHand, headValid, chestPos);
            if (rightHand != null) UpdateHand(rightHand, headValid, chestPos);

            if (Time.time >= nextDiagnosticsUpdateTime)
            {
                UpdateDiagnostics(headValid, headPos);
                nextDiagnosticsUpdateTime = Time.time + diagnosticsUpdateInterval;
            }
        }

        private void UpdateGaze(bool headValid, Vector3 headPos, Quaternion headRot)
        {
            if (gazeRay == null) return;

            gazeRay.enabled = headValid;
            if (gazeReticle != null) gazeReticle.SetActive(false);

            if (!headValid)
            {
                hasSmoothedRot = false;
                return;
            }

            if (!hasSmoothedRot)
            {
                smoothedHeadRot = headRot;
                hasSmoothedRot = true;
            }
            else
            {
                smoothedHeadRot = Quaternion.Slerp(smoothedHeadRot, headRot, Mathf.Clamp01(gazeDirectionSmoothing * Time.deltaTime));
            }

            Vector3 forward = smoothedHeadRot * Vector3.forward;

            Vector3 endPoint = headPos + forward * maxGazeDistance;
            Vector3 hitNormal = Vector3.up;
            bool rawHit = false;
            string rawHitName = null;

            if (Physics.Raycast(headPos, forward, out RaycastHit rayHit, maxGazeDistance))
            {
                endPoint = rayHit.point;
                hitNormal = rayHit.normal;
                rawHit = true;
                rawHitName = rayHit.collider.gameObject.name;
            }
            else if (forward.y < -0.001f)
            {
                float t = -headPos.y / forward.y;
                if (t > 0f && t <= maxGazeDistance)
                {
                    endPoint = headPos + forward * t;
                    rawHit = true;
                    rawHitName = "Floor";
                }
            }

            if (rawHitName != pendingHitKey)
            {
                pendingHitKey = rawHitName;
                pendingHitSince = Time.time;
            }
            if (Time.time - pendingHitSince >= minHitDwellSeconds)
                committedHitKey = pendingHitKey;

            bool committedHit = committedHitKey != null;

            lastGazeHit = committedHit;
            lastGazeTarget = committedHitKey;
            lastGazeDistance = Vector3.Distance(headPos, endPoint);

            Color rayColor = committedHit ? gazeHitColor : gazeMissColor;

            gazeRay.SetPosition(0, headPos);
            gazeRay.SetPosition(1, endPoint);
            gazeRay.material.color = rayColor;
            gazeRay.startColor = rayColor;
            gazeRay.endColor = rayColor;

            if (gazeReticle != null)
            {
                gazeReticle.SetActive(committedHit && rawHit);
                if (committedHit && rawHit)
                {
                    gazeReticle.transform.position = endPoint + hitNormal * 0.01f;
                    gazeReticle.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitNormal);
                }
            }

            if (centerDotWorldMat != null) centerDotWorldMat.color = rayColor;
            if (centerDotHeadMat  != null) centerDotHeadMat.color = rayColor;
        }

        private void UpdateHand(HandVisual hv, bool headValid, Vector3 chestPos)
        {
            bool handValid = TryGetPose(hv.node, out Vector3 pos, out Quaternion _);
            hv.lastValid = handValid;

            bool show = headValid && handValid;
            hv.sphere.SetActive(show);
            hv.bone.enabled = show;
            if (!show) return;

            hv.lastPos = pos;
            hv.sphere.transform.position = pos;
            hv.bone.SetPosition(0, chestPos);
            hv.bone.SetPosition(1, pos);

            UpdateHandInteraction(hv);
        }

        private void UpdateHandInteraction(HandVisual hv)
        {
            if (!showInteractionIndicators)
            {
                hv.trigger = 0f;
                hv.grip = 0f;
                if (hv.sphereMat != null) hv.sphereMat.color = hv.baseColor;
                hv.sphere.transform.localScale = Vector3.one * hv.baseDiameter;
                return;
            }

            float rawTrigger = GetAnalogInput(hv.node, CommonUsages.trigger, CommonUsages.triggerButton);
            float rawGrip    = GetAnalogInput(hv.node, CommonUsages.grip,    CommonUsages.gripButton);

            if (rawTrigger < inputDeadzone) rawTrigger = 0f;
            if (rawGrip    < inputDeadzone) rawGrip    = 0f;

            float k = Mathf.Clamp01(indicatorSmoothing * Time.deltaTime);
            hv.trigger = Mathf.Lerp(hv.trigger, rawTrigger, k);
            hv.grip    = Mathf.Lerp(hv.grip,    rawGrip,    k);

            Color c = hv.baseColor;
            c = Color.Lerp(c, triggerColor, hv.trigger);
            c = Color.Lerp(c, gripColor, hv.grip);
            if (hv.sphereMat != null) hv.sphereMat.color = c;

            float press = Mathf.Max(hv.trigger, hv.grip);
            float scale = Mathf.Lerp(hv.baseDiameter, hv.baseDiameter * pressedScaleMultiplier, press);
            hv.sphere.transform.localScale = Vector3.one * scale;
        }

        private static float GetAnalogInput(XRNode node, InputFeatureUsage<float> axis, InputFeatureUsage<bool> button)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid) return 0f;

            if (device.TryGetFeatureValue(axis, out float value))
                return Mathf.Clamp01(value);
            if (device.TryGetFeatureValue(button, out bool pressed))
                return pressed ? 1f : 0f;
            return 0f;
        }

        private void UpdateDiagnostics(bool headValid, Vector3 headPos)
        {
            if (diagnosticText == null) return;

            string headLine = headValid
                ? $"Head:   OK   pos {headPos.ToString("F2")}"
                : "Head:   NOT TRACKED";
            string leftLine  = DescribeHand(leftHand,  "L Hand");
            string rightLine = DescribeHand(rightHand, "R Hand");
            string gazeLine  = !showGaze ? "Gaze:   disabled"
                : lastGazeHit ? $"Gaze:   {lastGazeTarget} at {lastGazeDistance:F2}m"
                : $"Gaze:   no target (max {maxGazeDistance:F1}m)";

            diagnosticText.text = $"XR Loader: {loaderStatus}\n{headLine}\n{leftLine}\n{rightLine}\n{gazeLine}\nRoom recentred: {hasRecentredRoom}   roomOffset: {roomOffset.ToString("F2")}";
        }

        private static string DescribeHand(HandVisual hv, string label)
        {
            if (hv == null) return $"{label}: disabled";
            if (!hv.lastValid) return $"{label}: NOT TRACKED";
            return $"{label}: OK  pos {hv.lastPos.ToString("F2")}  trig {hv.trigger:F2}  grip {hv.grip:F2}";
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
