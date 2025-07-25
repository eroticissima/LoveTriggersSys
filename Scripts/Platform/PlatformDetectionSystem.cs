// =============================================================================
// PlatformDetectionSystem.cs - Universal Platform Detection & Management
// =============================================================================

using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

#if UNITY_XR_MANAGEMENT
using UnityEngine.XR.Management;
#endif

public enum PlatformType
{
    Desktop,
    VR_Oculus,
    VR_SteamVR,
    VR_OpenXR,
    VR_Pico,
    VR_Generic,
    Mobile,
    Console
}

public enum InputType
{
    MouseKeyboard,
    Gamepad,
    VR_Controllers,
    Touch,
    Mixed
}

public enum UIMode
{
    ScreenSpace,    // Traditional 2D UI
    WorldSpace,     // 3D VR UI
    Hybrid          // Both available
}

[System.Serializable]
public class PlatformSettings
{
    [Header("Performance")]
    public int targetFramerate = 60;
    public bool enableVSync = true;
    public float animationSpeedMultiplier = 1f;
    public float transitionSpeedMultiplier = 1f;

    [Header("UI")]
    public UIMode uiMode = UIMode.ScreenSpace;
    public Vector2 uiScale = Vector2.one;
    public float interactionDistance = 1f;

    [Header("Input")]
    public InputType primaryInputType = InputType.MouseKeyboard;
    public bool enableHandTracking = false;
    public bool enableEyeTracking = false;

    [Header("Features")]
    public bool enableVRIK = false;
    public bool enableHaptics = false;
    public bool enable3DAudio = false;
}

public class PlatformDetectionSystem : MonoBehaviour
{
    [Header("Platform Configuration")]
    public PlatformSettings desktopSettings;
    public PlatformSettings vrSettings;
    public PlatformSettings mobileSettings;

    [Header("Debug")]
    public bool debugMode = true;
    public bool forceVRMode = false;
    public bool forceDesktopMode = false;

    // Singleton
    public static PlatformDetectionSystem Instance { get; private set; }

    // Current Platform Info
    public PlatformType CurrentPlatform { get; private set; }
    public InputType CurrentInputType { get; private set; }
    public UIMode CurrentUIMode { get; private set; }
    public PlatformSettings CurrentSettings { get; private set; }

    // Platform Capabilities
    public bool IsVR => CurrentPlatform != PlatformType.Desktop && CurrentPlatform != PlatformType.Mobile && CurrentPlatform != PlatformType.Console;
    public bool IsDesktop => CurrentPlatform == PlatformType.Desktop;
    public bool IsMobile => CurrentPlatform == PlatformType.Mobile;
    public bool HasVRIK => CurrentSettings.enableVRIK;
    public bool HasHandTracking => CurrentSettings.enableHandTracking;
    public bool HasHaptics => CurrentSettings.enableHaptics;

    // Events
    public System.Action<PlatformType> OnPlatformDetected;
    public System.Action<InputType> OnInputTypeChanged;
    public System.Action<UIMode> OnUIModeChanged;

    // Internal
    private List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();
    private List<XRDisplaySubsystem> displaySubsystems = new List<XRDisplaySubsystem>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            DetectPlatform();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        ApplyPlatformSettings();
        StartCoroutine(MonitorPlatformChanges());
    }

    void DetectPlatform()
    {
        if (debugMode)
            Debug.Log("[PlatformDetection] Starting platform detection...");

        // Force modes for testing
        if (forceVRMode)
        {
            SetPlatform(PlatformType.VR_Generic);
            return;
        }

        if (forceDesktopMode)
        {
            SetPlatform(PlatformType.Desktop);
            return;
        }

        // Detect actual platform
        PlatformType detectedPlatform = DetectActualPlatform();
        SetPlatform(detectedPlatform);
    }

    PlatformType DetectActualPlatform()
    {
        // Check for VR first
        if (IsVREnabled())
        {
            string xrDevice = GetXRDeviceName();

            if (debugMode)
                Debug.Log($"[PlatformDetection] VR detected: {xrDevice}");

            // Identify specific VR platform
            if (xrDevice.ToLower().Contains("oculus") || xrDevice.ToLower().Contains("meta"))
                return PlatformType.VR_Oculus;
            else if (xrDevice.ToLower().Contains("openvr") || xrDevice.ToLower().Contains("steamvr"))
                return PlatformType.VR_SteamVR;
            else if (xrDevice.ToLower().Contains("openxr"))
                return PlatformType.VR_OpenXR;
            else if (xrDevice.ToLower().Contains("pico"))
                return PlatformType.VR_Pico;
            else
                return PlatformType.VR_Generic;
        }

        // Check for mobile
        if (Application.isMobilePlatform)
        {
            return PlatformType.Mobile;
        }

        // Check for console
#if UNITY_CONSOLE
        return PlatformType.Console;
#endif

        // Default to desktop
        return PlatformType.Desktop;
    }

    bool IsVREnabled()
    {
#if UNITY_XR_MANAGEMENT
        if (XRGeneralSettings.Instance != null && 
            XRGeneralSettings.Instance.Manager != null && 
            XRGeneralSettings.Instance.Manager.activeLoader != null)
        {
            return true;
        }
#endif

        // Fallback check
        if (XRSettings.enabled && !string.IsNullOrEmpty(XRSettings.loadedDeviceName))
        {
            return true;
        }

        // Check for VR display
        SubsystemManager.GetSubsystems(displaySubsystems);
        foreach (var display in displaySubsystems)
        {
            if (display.running)
                return true;
        }

        return false;
    }

    string GetXRDeviceName()
    {
#if UNITY_XR_MANAGEMENT
        if (XRGeneralSettings.Instance?.Manager?.activeLoader != null)
        {
            return XRGeneralSettings.Instance.Manager.activeLoader.name;
        }
#endif

        if (!string.IsNullOrEmpty(XRSettings.loadedDeviceName))
        {
            return XRSettings.loadedDeviceName;
        }

        return "Unknown VR Device";
    }

    void SetPlatform(PlatformType platform)
    {
        CurrentPlatform = platform;

        // Set appropriate settings
        switch (platform)
        {
            case PlatformType.Desktop:
                CurrentSettings = desktopSettings;
                CurrentInputType = InputType.MouseKeyboard;
                CurrentUIMode = UIMode.ScreenSpace;
                break;

            case PlatformType.VR_Oculus:
            case PlatformType.VR_SteamVR:
            case PlatformType.VR_OpenXR:
            case PlatformType.VR_Pico:
            case PlatformType.VR_Generic:
                CurrentSettings = vrSettings;
                CurrentInputType = InputType.VR_Controllers;
                CurrentUIMode = UIMode.WorldSpace;
                break;

            case PlatformType.Mobile:
                CurrentSettings = mobileSettings;
                CurrentInputType = InputType.Touch;
                CurrentUIMode = UIMode.ScreenSpace;
                break;

            default:
                CurrentSettings = desktopSettings;
                CurrentInputType = InputType.MouseKeyboard;
                CurrentUIMode = UIMode.ScreenSpace;
                break;
        }

        if (debugMode)
        {
            Debug.Log($"[PlatformDetection] Platform detected: {CurrentPlatform}");
            Debug.Log($"[PlatformDetection] Input Type: {CurrentInputType}");
            Debug.Log($"[PlatformDetection] UI Mode: {CurrentUIMode}");
        }

        OnPlatformDetected?.Invoke(CurrentPlatform);
        OnInputTypeChanged?.Invoke(CurrentInputType);
        OnUIModeChanged?.Invoke(CurrentUIMode);
    }

    void ApplyPlatformSettings()
    {
        if (CurrentSettings == null) return;

        // Apply performance settings
        Application.targetFrameRate = CurrentSettings.targetFramerate;
        QualitySettings.vSyncCount = CurrentSettings.enableVSync ? 1 : 0;

        // VR specific settings
        if (IsVR)
        {
#if UNITY_XR_MANAGEMENT
            // Set VR render scale
            XRSettings.renderViewportScale = 1.0f;
#endif

            // Disable screen space overlay canvases in VR
            DisableScreenSpaceCanvases();
        }

        if (debugMode)
        {
            Debug.Log($"[PlatformDetection] Applied settings for {CurrentPlatform}");
            Debug.Log($"- Target FPS: {CurrentSettings.targetFramerate}");
            Debug.Log($"- Animation Speed: {CurrentSettings.animationSpeedMultiplier}");
            Debug.Log($"- UI Mode: {CurrentUIMode}");
        }
    }

    void DisableScreenSpaceCanvases()
    {
        var canvases = FindObjectsOfType<Canvas>();
        foreach (var canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.gameObject.SetActive(false);
                if (debugMode)
                    Debug.Log($"[PlatformDetection] Disabled screen space canvas: {canvas.name}");
            }
        }
    }

    System.Collections.IEnumerator MonitorPlatformChanges()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            // Check for input device changes
            InputType newInputType = DetectCurrentInputType();
            if (newInputType != CurrentInputType)
            {
                CurrentInputType = newInputType;
                OnInputTypeChanged?.Invoke(CurrentInputType);

                if (debugMode)
                    Debug.Log($"[PlatformDetection] Input type changed to: {CurrentInputType}");
            }

            // Check for VR state changes
            bool wasVR = IsVR;
            bool isVRNow = IsVREnabled();

            if (wasVR != isVRNow)
            {
                if (debugMode)
                    Debug.Log($"[PlatformDetection] VR state changed: {wasVR} -> {isVRNow}");

                DetectPlatform();
                ApplyPlatformSettings();
            }
        }
    }

    InputType DetectCurrentInputType()
    {
        if (IsVR)
        {
            return InputType.VR_Controllers;
        }

        // Check for gamepad input
        string[] joystickNames = Input.GetJoystickNames();
        foreach (string joystick in joystickNames)
        {
            if (!string.IsNullOrEmpty(joystick))
            {
                return InputType.Gamepad;
            }
        }

        // Check for mobile
        if (Application.isMobilePlatform)
        {
            return InputType.Touch;
        }

        return InputType.MouseKeyboard;
    }

    // Public API
    public bool SupportsFeature(string featureName)
    {
        switch (featureName.ToLower())
        {
            case "vrik":
                return HasVRIK;
            case "handtracking":
                return HasHandTracking;
            case "haptics":
                return HasHaptics;
            case "3daudio":
                return CurrentSettings.enable3DAudio;
            default:
                return false;
        }
    }

    public void SwitchToVRMode()
    {
        if (!IsVR)
        {
            forceVRMode = true;
            DetectPlatform();
            ApplyPlatformSettings();
        }
    }

    public void SwitchToDesktopMode()
    {
        if (IsVR)
        {
            forceDesktopMode = true;
            DetectPlatform();
            ApplyPlatformSettings();
        }
    }

    public void LogPlatformInfo()
    {
        Debug.Log("=== PLATFORM DETECTION INFO ===");
        Debug.Log($"Platform: {CurrentPlatform}");
        Debug.Log($"Input Type: {CurrentInputType}");
        Debug.Log($"UI Mode: {CurrentUIMode}");
        Debug.Log($"Target FPS: {CurrentSettings.targetFramerate}");
        Debug.Log($"Animation Speed: {CurrentSettings.animationSpeedMultiplier}");
        Debug.Log($"VR Enabled: {IsVR}");
        Debug.Log($"Mobile Platform: {Application.isMobilePlatform}");
        Debug.Log($"Unity Version: {Application.unityVersion}");

        if (IsVR)
        {
            Debug.Log($"XR Device: {GetXRDeviceName()}");
            Debug.Log($"XR Settings Enabled: {XRSettings.enabled}");
        }

        Debug.Log("=================================");
    }

    [ContextMenu("Force Detect Platform")]
    public void ForceDetectPlatform()
    {
        DetectPlatform();
        LogPlatformInfo();
    }
}