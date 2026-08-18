using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Fix jagged edges and smoothing issues in VR
/// Configures anti-aliasing and quality settings for URP
/// </summary>
public class VRSmoothingFix : MonoBehaviour
{
    [Header("Anti-Aliasing Settings")]
    [Tooltip("MSAA level - higher = smoother but more expensive")]
    public MsaaQuality msaaLevel = MsaaQuality._4x;

    [Header("Post Processing (Optional)")]
    [Tooltip("Enable post-processing anti-aliasing (FXAA/SMAA)")]
    public bool enablePostProcessAA = true;

    [Header("Texture Filtering")]
    [Tooltip("Anisotropic filtering for texture smoothness")]
    public AnisotropicFiltering anisotropicFiltering = AnisotropicFiltering.ForceEnable;

    [Header("Shadow Quality")]
    [Tooltip("Higher resolution = smoother shadow edges")]
    public UnityEngine.ShadowResolution shadowResolution = UnityEngine.ShadowResolution.VeryHigh;

    void Start()
    {
        ApplyQualitySettings();
        SetupCameraAA();
        EnableHighQualityMaterials();
    }

    [ContextMenu("Apply VR Smoothing Settings")]
    public void ApplyQualitySettings()
    {
        UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        if (urpAsset != null)
        {
            Debug.Log($"URP Asset found. Target MSAA: {msaaLevel}");
        }
        else
        {
            Debug.LogWarning("URP Asset not found! Make sure you're using Universal Render Pipeline.");
        }

        QualitySettings.antiAliasing = GetMSAAValue(msaaLevel);
        QualitySettings.anisotropicFiltering = anisotropicFiltering;
        QualitySettings.shadowResolution = shadowResolution;
        QualitySettings.softParticles = true;
        QualitySettings.realtimeReflectionProbes = true;

        QualitySettings.shadows = UnityEngine.ShadowQuality.All;
        QualitySettings.shadowDistance = 50f;
        QualitySettings.shadowNearPlaneOffset = 3f;
        QualitySettings.shadowCascades = 2;

        Debug.Log("VR Smoothing settings applied!");
    }

    private int GetMSAAValue(MsaaQuality quality)
    {
        switch (quality)
        {
            case MsaaQuality.Disabled: return 0;
            case MsaaQuality._2x: return 2;
            case MsaaQuality._4x: return 4;
            case MsaaQuality._8x: return 8;
            default: return 4;
        }
    }

    [ContextMenu("Setup VR Camera Anti-Aliasing")]
    public void SetupCameraAA()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        int count = 0;

        foreach (Camera cam in cameras)
        {
            var cameraData = cam.gameObject.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;

            count++;
        }

        Debug.Log($"Configured anti-aliasing on {count} cameras");
    }

    [ContextMenu("Enable High Quality Materials")]
    public void EnableHighQualityMaterials()
    {
        MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);

        foreach (MeshRenderer renderer in renderers)
        {
            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat != null)
                {
                    mat.enableInstancing = true;

                    if (mat.HasProperty("_Smoothness"))
                    {
                        float currentSmoothness = mat.GetFloat("_Smoothness");
                        if (currentSmoothness < 0.3f)
                        {
                            mat.SetFloat("_Smoothness", 0.5f);
                        }
                    }
                }
            }
        }

        Debug.Log("Enhanced material quality");
    }

#if UNITY_EDITOR

    [ContextMenu("Find URP Asset")]
    public void FindURPAsset()
    {
        UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        if (urpAsset != null)
        {
            Debug.Log($"Found URP Asset: {urpAsset.name}");
            string path = AssetDatabase.GetAssetPath(urpAsset);
            Debug.Log($"Location: {path}");
            EditorGUIUtility.PingObject(urpAsset);
        }
        else
        {
            Debug.LogError("URP Asset not found!");
        }
    }

    [ContextMenu("FULL SMOOTHING FIX")]
    public void FullSmoothingFix()
    {
        ApplyQualitySettings();
        SetupCameraAA();
        EnableHighQualityMaterials();

        Debug.Log("=== FULL SMOOTHING FIX APPLIED ===");
        Debug.Log("If edges are still jagged, manually increase MSAA in URP Asset:");
        Debug.Log("1. Find your URP Asset (use 'Find URP Asset' context menu)");
        Debug.Log("2. Set MSAA to 4x or 8x");
        Debug.Log("3. Enable 'Depth Texture' and 'Opaque Texture' if using post-processing");
    }
#endif
}
