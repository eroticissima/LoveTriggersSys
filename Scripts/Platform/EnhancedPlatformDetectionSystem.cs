// =============================================================================
// EnhancedPlatformDetectionSystem.cs - Platform Detection Without Mobile Support
// =============================================================================

using UnityEngine;
using System.Collections;
using LTSystem.Player;

namespace LTSystem.Platform
{
    public enum PlatformType
    {
        PC_Desktop,
        XR_VR,
        Console
    }

    public enum InputType
    {
        MouseKeyboard,
        Gamepad,
        VR_Controllers
    }

    /// <summary>
    /// Enhanced platform detection system without mobile support
    /// </summary>
    public class EnhancedPlatformDetectionSystem : MonoBehaviour
    {
        [Header("Platform Settings")]
        public bool autoDetectOnStart = true;
        public bool continuousMonitoring = true;
        public float detectionInterval = 1f;

        [Header("Platform Configurations")]
        public PlatformSettings pcSettings;
        public PlatformSettings xrSettings;
        public PlatformSettings consoleSettings;

        [Header("Debug")]
        public bool debugMode = true;
        public bool showPlatformUI = true;

        // Current state
        public PlatformType CurrentPlatform { get; private set; } = PlatformType.PC_Desktop;
        public InputType CurrentInputType { get; private set; } = InputType.MouseKeyboard;
        public PlatformSettings CurrentSettings { get; private set; }

        // Properties
        public bool IsPC => CurrentPlatform == PlatformType.PC_Desktop;
        public bool IsXR => CurrentPlatform == PlatformType.XR_VR;
        public bool IsConsole => CurrentPlatform == PlatformType.Console;

        // Capabilities
        public bool HasVRIK => IsXR && CurrentSettings != null && CurrentSettings.hasVRIK;
        public bool HasThirdPersonCamera => (IsPC || IsConsole) && CurrentSettings != null && CurrentSettings.hasThirdPersonCamera;
        public bool HasGamepadInput => (IsConsole || CurrentInputType == InputType.Gamepad);

        // Events
        public System.Action<PlatformType> OnPlatformDetected;
        public System.Action<InputType> OnInputTypeChanged;
        public System.Action<PlatformSettings> OnSettingsApplied;

        // Singleton
        public static EnhancedPlatformDetectionSystem Instance { get; private set; }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        void Start()
        {
            if (autoDetectOnStart)
            {
                DetectPlatform();
                ApplyPlatformSettings();
            }

            if (continuousMonitoring)
            {
                StartCoroutine(MonitorPlatformChanges());
            }
        }

        #region Platform Detection

        public void DetectPlatform()
        {
            PlatformType detectedPlatform = PlatformType.PC_Desktop;

            // Check for VR first (highest priority)
            if (IsVREnabled())
            {
                detectedPlatform = PlatformType.XR_VR;
                CurrentInputType = InputType.VR_Controllers;
            }
            // Check for console platforms
            else if (IsConsolePlatform())
            {
                detectedPlatform = PlatformType.Console;
                CurrentInputType = DetectConsoleInputType();
            }
            // Default to PC
            else
            {
                detectedPlatform = PlatformType.PC_Desktop;
                CurrentInputType = DetectPCInputType();
            }

            if (CurrentPlatform != detectedPlatform)
            {
                CurrentPlatform = detectedPlatform;
                OnPlatformDetected?.Invoke(CurrentPlatform);

                if (debugMode)
                    Debug.Log($"[EnhancedPlatformDetection] Platform detected: {CurrentPlatform} with input: {CurrentInputType}");
            }
        }

        private bool IsVREnabled()
        {
#if UNITY_XR_MANAGEMENT
            var xrSettings = UnityEngine.XR.XRGeneralSettings.Instance;
            if (xrSettings != null && xrSettings.Manager != null && xrSettings.Manager.activeLoader != null)
            {
                return true;
            }
#endif

#if UNITY_XR_LEGACY
            return UnityEngine.XR.XRSettings.enabled && UnityEngine.XR.XRSettings.loadedDeviceName != "None";
#endif

            return false;
        }

        private bool IsConsolePlatform()
        {
            return Application.platform == RuntimePlatform.PS4 ||
                   Application.platform == RuntimePlatform.PS5 ||
                   Application.platform == RuntimePlatform.XboxOne ||
                   Application.platform == RuntimePlatform.GameCoreXboxOne ||
                   Application.platform == RuntimePlatform.GameCoreXboxSeries;
        }

        private InputType DetectPCInputType()
        {
            // Check for gamepad input on PC
            string[] joystickNames = Input.GetJoystickNames();
            foreach (string joystick in joystickNames)
            {
                if (!string.IsNullOrEmpty(joystick))
                {
                    return InputType.Gamepad;
                }
            }

            return InputType.MouseKeyboard;
        }

        private InputType DetectConsoleInputType()
        {
            // Consoles primarily use gamepad input
            return InputType.Gamepad;
        }

        #endregion

        #region Settings Management

        public void ApplyPlatformSettings()
        {
            switch (CurrentPlatform)
            {
                case PlatformType.PC_Desktop:
                    CurrentSettings = pcSettings;
                    break;
                case PlatformType.XR_VR:
                    CurrentSettings = xrSettings;
                    break;
                case PlatformType.Console:
                    CurrentSettings = consoleSettings;
                    break;
            }

            if (CurrentSettings != null)
            {
                ApplySettings(CurrentSettings);
                OnSettingsApplied?.Invoke(CurrentSettings);
            }
        }

        private void ApplySettings(PlatformSettings settings)
        {
            // Apply frame rate settings
            Application.targetFrameRate = settings.targetFramerate;
            QualitySettings.vSyncCount = settings.enableVSync ? 1 : 0;

            // Apply quality settings
            QualitySettings.SetQualityLevel(settings.qualityLevel, true);

            // Apply XR specific settings
            if (IsXR)
            {
#if UNITY_XR_MANAGEMENT
                UnityEngine.XR.XRSettings.renderViewportScale = settings.renderScale;
#endif
            }

            if (debugMode)
            {
                Debug.Log($"[EnhancedPlatformDetection] Applied settings for {CurrentPlatform}");
                Debug.Log($"- Target FPS: {settings.targetFramerate}");
                Debug.Log($"- Quality Level: {settings.qualityLevel}");
                Debug.Log($"- VSync: {settings.enableVSync}");
                Debug.Log($"- Render Scale: {settings.renderScale}");
            }
        }

        #endregion

        #region Monitoring

        private IEnumerator MonitorPlatformChanges()
        {
            while (true)
            {
                yield return new WaitForSeconds(detectionInterval);

                // Check for input type changes
                InputType newInputType = CurrentPlatform switch
                {
                    PlatformType.PC_Desktop => DetectPCInputType(),
                    PlatformType.Console => DetectConsoleInputType(),
                    PlatformType.XR_VR => InputType.VR_Controllers,
                    _ => CurrentInputType
                };

            if (newInputType != CurrentInputType)
            {
                CurrentInputType = newInputType;
                OnInputTypeChanged?.Invoke(CurrentInputType);

                if (debugMode)
                    Debug.Log($"[EnhancedPlatformDetection] Input type changed to: {CurrentInputType}");
            }

            // Check for VR state changes
            bool wasVR = IsXR;
            bool isVRNow = IsVREnabled();

            if (wasVR != isVRNow)
            {
                DetectPlatform();
                ApplyPlatformSettings();
            }
        }
    }

    #endregion

    #region Public API

    public bool SupportsFeature(string featureName)
    {
        if (CurrentSettings == null) return false;

        return featureName.ToLower() switch
            {
                "vrik" => CurrentSettings.hasVRIK,
                "thirdperson" => CurrentSettings.hasThirdPersonCamera,
                "gamepad" => CurrentSettings.hasGamepadSupport,
                "haptics" => CurrentSettings.hasHapticFeedback,
                "handtracking" => CurrentSettings.hasHandTracking,
                _ => false
            };
}

public BasePlayerController.PlayerType GetPlayerControllerType()
{
    return CurrentPlatform switch
            {
                PlatformType.PC_Desktop => BasePlayerController.PlayerType.PC_ThirdPerson,
                PlatformType.XR_VR => BasePlayerController.PlayerType.XR_VRIK,
                PlatformType.Console => BasePlayerController.PlayerType.Console,
                _ => BasePlayerController.PlayerType.PC_ThirdPerson
            };
        }

        [ContextMenu("Force Platform Detection")]
public void ForcePlatformDetection()
{
    DetectPlatform();
    ApplyPlatformSettings();
}

[ContextMenu("Log Platform Info")]
public void LogPlatformInfo()
{
    Debug.Log("=== PLATFORM DETECTION INFO ===");
    Debug.Log($"Current Platform: {CurrentPlatform}");
    Debug.Log($"Input Type: {CurrentInputType}");
    Debug.Log($"Unity Platform: {Application.platform}");
    Debug.Log($"VR Enabled: {IsVREnabled()}");
    Debug.Log($"Has VRIK: {HasVRIK}");
    Debug.Log($"Has Third Person Camera: {HasThirdPersonCamera}");
    Debug.Log($"Has Gamepad: {HasGamepadInput}");

    if (CurrentSettings != null)
    {
        Debug.Log($"Target FPS: {CurrentSettings.targetFramerate}");
        Debug.Log($"Quality Level: {CurrentSettings.qualityLevel}");
    }
    Debug.Log("==============================");
}

        #endregion
    }

    [System.Serializable]
public class PlatformSettings
{
    [Header("Performance")]
    public int targetFramerate = 60;
    public int qualityLevel = 3;
    public bool enableVSync = true;
    public float renderScale = 1.0f;

    [Header("Features")]
    public bool hasVRIK = false;
    public bool hasThirdPersonCamera = true;
    public bool hasGamepadSupport = true;
    public bool hasHapticFeedback = false;
    public bool hasHandTracking = false;

    [Header("Animation")]
    public float animationSpeedMultiplier = 1f;
    public float transitionSpeedMultiplier = 1f;

    [Header("Audio")]
    public bool enable3DAudio = true;
    public float masterVolume = 1f;

    [Header("Rendering")]
    public bool enableHDRP = true;
    public bool enablePostProcessing = true;
    public int shadowQuality = 2;
    public int textureQuality = 0;
}
}