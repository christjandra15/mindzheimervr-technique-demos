using UnityEngine;
using Unity.XR.CoreUtils;

/// <summary>
/// Controls XR Origin height for VR testing and development
/// Allows manual height override and provides runtime height adjustment
/// </summary>
public class XROriginHeightController : MonoBehaviour
{
    [Header("Height Settings")]
    [Tooltip("Manual height offset from ground (in meters)")]
    [SerializeField] private float manualHeightOffset = 1.7f;
    
    [Tooltip("Enable manual height override (ignores device tracking)")]
    [SerializeField] private bool useManualHeight = true;
    
    [Tooltip("Apply height on start")]
    [SerializeField] private bool applyOnStart = true;
    
    [Header("Runtime Adjustment")]
    [Tooltip("Allow height adjustment with keyboard during play")]
    [SerializeField] private bool allowRuntimeAdjustment = true;
    
    [Tooltip("Height adjustment speed (meters per second)")]
    [SerializeField] private float adjustmentSpeed = 0.5f;
    
    [Header("References")]
    [SerializeField] private XROrigin xrOrigin;
    [SerializeField] private Transform cameraOffset;
    
    private Vector3 initialCameraOffsetPosition;
    private float currentHeight;
    
    private void Awake()
    {
        if (xrOrigin == null)
        {
            xrOrigin = GetComponent<XROrigin>();
            if (xrOrigin == null)
            {
                xrOrigin = FindFirstObjectByType<XROrigin>();
            }
        }
        
        if (cameraOffset == null && xrOrigin != null)
        {
            if (xrOrigin.Camera != null)
            {
                cameraOffset = xrOrigin.Camera.transform.parent;
            }
        }
        
        if (cameraOffset != null)
        {
            initialCameraOffsetPosition = cameraOffset.localPosition;
        }
        
        currentHeight = manualHeightOffset;
    }
    
    private void Start()
    {
        if (applyOnStart && useManualHeight)
        {
            SetHeight(manualHeightOffset);
        }
    }
    
    private void Update()
    {
        if (allowRuntimeAdjustment && useManualHeight)
        {
            HandleRuntimeAdjustment();
        }
    }
    
    private void HandleRuntimeAdjustment()
    {
        bool heightChanged = false;
        
        if (Input.GetKey(KeyCode.PageUp))
        {
            currentHeight += adjustmentSpeed * Time.deltaTime;
            heightChanged = true;
        }
        else if (Input.GetKey(KeyCode.PageDown))
        {
            currentHeight -= adjustmentSpeed * Time.deltaTime;
            currentHeight = Mathf.Max(0.1f, currentHeight);
            heightChanged = true;
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            currentHeight = manualHeightOffset;
            heightChanged = true;
            Debug.Log($"XR Origin height reset to: {currentHeight:F2}m");
        }
        
        if (heightChanged)
        {
            SetHeight(currentHeight);
            Debug.Log($"XR Origin height: {currentHeight:F2}m (Use PageUp/PageDown to adjust, R to reset)");
        }
    }
    
    /// <summary>
    /// Sets the XR Origin height manually
    /// </summary>
    /// <param name="height">Height in meters from ground</param>
    public void SetHeight(float height)
    {
        if (xrOrigin == null)
        {
            Debug.LogWarning("XROrigin not found!");
            return;
        }
        
        if (cameraOffset == null)
        {
            Debug.LogWarning("CameraOffset not found! Using XR Origin transform directly.");
            Vector3 pos = xrOrigin.transform.position;
            pos.y = height;
            xrOrigin.transform.position = pos;
        }
        else
        {
            Vector3 newPosition = initialCameraOffsetPosition;
            newPosition.y = height;
            cameraOffset.localPosition = newPosition;
        }
        
        currentHeight = height;
        Debug.Log($"XR Origin height set to: {height:F2}m");
    }
    
    /// <summary>
    /// Resets to default height
    /// </summary>
    public void ResetHeight()
    {
        SetHeight(manualHeightOffset);
    }
    
    /// <summary>
    /// Adjusts height by a relative amount
    /// </summary>
    public void AdjustHeight(float delta)
    {
        currentHeight += delta;
        currentHeight = Mathf.Max(0.1f, currentHeight);
        SetHeight(currentHeight);
    }
    
    private void OnValidate()
    {
        manualHeightOffset = Mathf.Max(0.1f, manualHeightOffset);
        adjustmentSpeed = Mathf.Max(0.01f, adjustmentSpeed);
    }
    
    private void OnDrawGizmos()
    {
        if (xrOrigin != null)
        {
            Vector3 basePosition = xrOrigin.transform.position;
            Vector3 heightPosition = basePosition + Vector3.up * currentHeight;
            
            Gizmos.color = Color.green;
            Gizmos.DrawLine(basePosition, heightPosition);
            Gizmos.DrawWireSphere(heightPosition, 0.1f);
        }
    }
}
