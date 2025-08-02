// =============================================================================
// CameraManager.cs - Platform-Aware Camera Management for Love Trigger System
// =============================================================================

using UnityEngine;
using Cinemachine;
using System.Collections;
using System.Collections.Generic;

namespace LTSystem
{
    /// <summary>
    /// Manages camera switching between 3rd person (Cinemachine) and VR cameras
    /// Integrates with Love Trigger System for cinematic sequences
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        [Header("Camera References")]
        [Tooltip("Your Cinemachine 3rd person camera rig")]
        public CinemachineFreeLook thirdPersonCamera;

        [Tooltip("VR camera (usually XR Rig camera)")]
        public Camera vrCamera;

        [Tooltip("Main camera for non-VR fallback")]
        public Camera mainCamera;

        [Header("Platform Configuration")]
        public bool autoDetectPlatform = true;
        public PlatformType forcePlatform = PlatformType.Desktop;

        [Header("Love Trigger Cinematic")]
        [Tooltip("Camera for love trigger cinematics")]
        public CinemachineVirtualCamera cinematicCamera;

        [Tooltip("Blend duration for cinematic transitions")]
        public float cinematicBlendDuration = 1f;

        [Tooltip("Should love triggers override current camera?")]
        public bool allowCinematicOverride = true;

        [Header("Smooth Transitions")]
        public float platformSwitchBlendDuration = 2f;
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Follow Targets")]
        [Tooltip("Transform to follow (usually player)")]
        public Transform followTarget;

        [Tooltip("Transform to look at (usually player head)")]
        public Transform lookAtTarget;

        [Header("Debug")]
        public bool debugMode = true;

        // Current state
        private PlatformType currentPlatform = PlatformType.Desktop;
        private CameraMode currentCameraMode = CameraMode.ThirdPerson;
        private Camera activeCamera;
        private bool isInCinematicMode = false;
        private Coroutine transitionCoroutine;

        // Cinemachine brain for blending
        private CinemachineBrain cinemachineBrain;

        // Events
        public System.Action<CameraMode> OnCameraModeChanged;
        public System.Action<PlatformType> OnPlatformChanged;

        public enum CameraMode
        {
            ThirdPerson,
            FirstPerson_VR,
            Cinematic,
            Free
        }

        #region Initialization

        void Awake()
        {
            InitializeCameraSystem();
        }

        void Start()
        {
            if (autoDetectPlatform)
            {
                SetupPlatformDetection();
            }
            else
            {
                ConfigureCamerasForPlatform(forcePlatform);
            }

            // Find follow target if not assigned
            if (followTarget == null)
            {
                var player = FindObjectOfType<PlayerController>();
                if (player != null)
                {
                    followTarget = player.transform;
                    lookAtTarget = followTarget; // Can be refined to head bone later
                }
            }

            UpdateCameraTargets();
        }

        void InitializeCameraSystem()
        {
            // Get or create main camera
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera == null)
                mainCamera = FindObjectOfType<Camera>();

            // Get Cinemachine brain
            if (mainCamera != null)
                cinemachineBrain = mainCamera.GetComponent<CinemachineBrain>();

            // Auto-find Cinemachine camera if not assigned
            if (thirdPersonCamera == null)
                thirdPersonCamera = FindObjectOfType<CinemachineFreeLook>();

            // Auto-find VR camera
            if (vrCamera == null)
            {
                // Look for common VR camera names
                var vrCameraNames = new string[]
                {
                    "XR Camera", "Main Camera (VR)", "CenterEyeAnchor",
                    "Camera", "Head Camera", "VR Camera"
                };

                foreach (var name in vrCameraNames)
                {
                    var found = GameObject.Find(name);
                    if (found != null)
                    {
                        var cam = found.GetComponent<Camera>();
                        if (cam != null && cam != mainCamera)
                        {
                            vrCamera = cam;
                            break;
                        }
                    }
                }
            }

            if (debugMode)
            {
                Debug.Log($"[CameraManager] Initialized:");
                Debug.Log($"- 3rd Person: {(thirdPersonCamera != null ? "Found" : "Missing")}");
                Debug.Log($"- VR Camera: {(vrCamera != null ? "Found" : "Missing")}");
                Debug.Log($"- Main Camera: {(mainCamera != null ? "Found" : "Missing")}");
                Debug.Log($"- Cinematic: {(cinematicCamera != null ? "Found" : "Missing")}");
            }
        }

        void SetupPlatformDetection()
        {
            if (PlatformDetectionSystem.Instance != null)
            {
                PlatformDetectionSystem.Instance.OnPlatformDetected += OnPlatformDetected;
                OnPlatformDetected(PlatformDetectionSystem.Instance.CurrentPlatform);
            }
            else
            {
                // Fallback detection
                StartCoroutine(DelayedPlatformDetection());
            }
        }

        IEnumerator DelayedPlatformDetection()
        {
            yield return new WaitForSeconds(0.5f);

            if (PlatformDetectionSystem.Instance != null)
            {
                PlatformDetectionSystem.Instance.OnPlatformDetected += OnPlatformDetected;
                OnPlatformDetected(PlatformDetectionSystem.Instance.CurrentPlatform);
            }
            else
            {
                // Manual detection
                bool isVR = UnityEngine.XR.XRSettings.enabled && !string.IsNullOrEmpty(UnityEngine.XR.XRSettings.loadedDeviceName);
                OnPlatformDetected(isVR ? PlatformType.VR_Generic : PlatformType.Desktop);
            }
        }

        #endregion

        #region Platform Switching

        void OnPlatformDetected(PlatformType platform)
        {
            if (currentPlatform == platform) return;

            currentPlatform = platform;
            ConfigureCamerasForPlatform(platform);
            OnPlatformChanged?.Invoke(platform);

            if (debugMode)
                Debug.Log($"[CameraManager] Platform changed to: {platform}");
        }

        void ConfigureCamerasForPlatform(PlatformType platform)
        {
            switch (platform)
            {
                case PlatformType.Desktop:
                case PlatformType.Mobile:
                case PlatformType.Console:
                    SetCameraMode(CameraMode.ThirdPerson);
                    break;

                case PlatformType.VR_Oculus:
                case PlatformType.VR_SteamVR:
                case PlatformType.VR_OpenXR:
                case PlatformType.VR_Pico:
                case PlatformType.VR_Generic:
                    SetCameraMode(CameraMode.FirstPerson_VR);
                    break;
            }
        }

        public void SetCameraMode(CameraMode mode)
        {
            if (currentCameraMode == mode && !isInCinematicMode) return;

            var previousMode = currentCameraMode;
            currentCameraMode = mode;

            if (transitionCoroutine != null)
                StopCoroutine(transitionCoroutine);

            transitionCoroutine = StartCoroutine(TransitionToCamera(mode));

            OnCameraModeChanged?.Invoke(mode);

            if (debugMode)
                Debug.Log($"[CameraManager] Camera mode: {previousMode} → {mode}");
        }

        IEnumerator TransitionToCamera(CameraMode targetMode)
        {
            switch (targetMode)
            {
                case CameraMode.ThirdPerson:
                    yield return StartCoroutine(ActivateThirdPersonCamera());
                    break;

                case CameraMode.FirstPerson_VR:
                    yield return StartCoroutine(ActivateVRCamera());
                    break;

                case CameraMode.Cinematic:
                    yield return StartCoroutine(ActivateCinematicCamera());
                    break;
            }
        }

        IEnumerator ActivateThirdPersonCamera()
        {
            // Disable VR camera
            if (vrCamera != null)
                vrCamera.enabled = false;

            // Enable main camera for Cinemachine
            if (mainCamera != null)
                mainCamera.enabled = true;

            // Activate 3rd person virtual camera
            if (thirdPersonCamera != null)
            {
                thirdPersonCamera.gameObject.SetActive(true);
                thirdPersonCamera.Priority = 10;
            }

            // Deactivate cinematic camera
            if (cinematicCamera != null)
                cinematicCamera.Priority = 0;

            activeCamera = mainCamera;

            // Wait for blend if using Cinemachine
            if (cinemachineBrain != null && platformSwitchBlendDuration > 0)
            {
                yield return new WaitForSeconds(platformSwitchBlendDuration);
            }
        }

        IEnumerator ActivateVRCamera()
        {
            // Disable Cinemachine cameras
            if (thirdPersonCamera != null)
            {
                thirdPersonCamera.Priority = 0;
                // Don't deactivate completely - just lower priority
            }

            if (cinematicCamera != null)
                cinematicCamera.Priority = 0;

            // Disable main camera
            if (mainCamera != null)
                mainCamera.enabled = false;

            // Enable VR camera
            if (vrCamera != null)
            {
                vrCamera.enabled = true;
                activeCamera = vrCamera;
            }
            else
            {
                Debug.LogWarning("[CameraManager] VR Camera not found! Falling back to main camera.");
                if (mainCamera != null)
                {
                    mainCamera.enabled = true;
                    activeCamera = mainCamera;
                }
            }

            yield return new WaitForSeconds(0.1f); // Small delay for VR initialization
        }

        IEnumerator ActivateCinematicCamera()
        {
            if (cinematicCamera == null)
            {
                Debug.LogWarning("[CameraManager] No cinematic camera assigned!");
                yield break;
            }

            isInCinematicMode = true;

            // Set cinematic camera as highest priority
            cinematicCamera.Priority = 100;

            // For VR, we might want to keep VR camera active but blend with cinematic
            if (currentPlatform == PlatformType.VR_Generic || IsVRPlatform(currentPlatform))
            {
                // In VR, cinematic cameras need special handling
                // You might want to use a different approach here
                if (vrCamera != null)
                    vrCamera.enabled = true;
            }
            else
            {
                // Desktop: use normal Cinemachine blending
                if (mainCamera != null)
                    mainCamera.enabled = true;
            }

            yield return new WaitForSeconds(cinematicBlendDuration);
        }

        #endregion

        #region Love Trigger Integration

        /// <summary>
        /// Called by Love Trigger System when starting a cinematic sequence
        /// </summary>
        public void StartLoveTriggerCinematic(LoveTriggerSO trigger, Transform player, Transform partner = null)
        {
            if (!allowCinematicOverride) return;

            if (debugMode)
                Debug.Log($"[CameraManager] Starting love trigger cinematic: {trigger.triggerName}");

            // Configure cinematic camera for the specific trigger
            if (cinematicCamera != null)
            {
                ConfigureCinematicCameraForTrigger(trigger, player, partner);
                SetCameraMode(CameraMode.Cinematic);
            }
        }

        /// <summary>
        /// Called by Love Trigger System when ending a cinematic sequence
        /// </summary>
        public void EndLoveTriggerCinematic()
        {
            if (!isInCinematicMode) return;

            if (debugMode)
                Debug.Log("[CameraManager] Ending love trigger cinematic");

            isInCinematicMode = false;

            // Return to platform-appropriate camera
            ConfigureCamerasForPlatform(currentPlatform);
        }

        void ConfigureCinematicCameraForTrigger(LoveTriggerSO trigger, Transform player, Transform partner)
        {
            if (cinematicCamera == null) return;

            // Enhanced Love Trigger with specific camera positions
            if (trigger is EnhancedLoveTriggerSO enhancedTrigger)
            {
                if (enhancedTrigger.cameraConfigurations != null && enhancedTrigger.cameraConfigurations.Length > 0)
                {
                    var cameraConfig = enhancedTrigger.cameraConfigurations[0]; // Use first camera config

                    // Position cinematic camera
                    Vector3 cameraPosition = player.position + cameraConfig.position;
                    Vector3 cameraRotation = cameraConfig.rotation;

                    cinematicCamera.transform.position = cameraPosition;
                    cinematicCamera.transform.rotation = Quaternion.Euler(cameraRotation);

                    // Set field of view
                    cinematicCamera.m_Lens.FieldOfView = cameraConfig.fieldOfView;
                }
            }
            else
            {
                // Default cinematic positioning
                Vector3 midPoint = partner != null ?
                    Vector3.Lerp(player.position, partner.position, 0.5f) :
                    player.position;

                // Position camera for good view of both characters
                Vector3 offset = new Vector3(2f, 1.5f, -3f);
                cinematicCamera.transform.position = midPoint + offset;
                cinematicCamera.transform.LookAt(midPoint + Vector3.up * 1f);
            }

            // Set follow and look at targets
            cinematicCamera.Follow = player;
            cinematicCamera.LookAt = partner != null ? partner : player;
        }

        #endregion

        #region Target Management

        public void UpdateCameraTargets()
        {
            if (followTarget == null) return;

            // Update 3rd person camera targets
            if (thirdPersonCamera != null)
            {
                thirdPersonCamera.Follow = followTarget;
                thirdPersonCamera.LookAt = lookAtTarget != null ? lookAtTarget : followTarget;
            }

            // Update cinematic camera if not in cinematic mode
            if (cinematicCamera != null && !isInCinematicMode)
            {
                cinematicCamera.Follow = followTarget;
                cinematicCamera.LookAt = lookAtTarget != null ? lookAtTarget : followTarget;
            }
        }

        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            UpdateCameraTargets();
        }

        public void SetLookAtTarget(Transform target)
        {
            lookAtTarget = target;
            UpdateCameraTargets();
        }

        #endregion

        #region Public API

        public Camera GetActiveCamera()
        {
            return activeCamera;
        }

        public bool IsVRMode()
        {
            return currentCameraMode == CameraMode.FirstPerson_VR;
        }

        public bool IsInCinematicMode()
        {
            return isInCinematicMode;
        }

        public CameraMode GetCurrentCameraMode()
        {
            return currentCameraMode;
        }

        public void ForceSetPlatform(PlatformType platform)
        {
            autoDetectPlatform = false;
            OnPlatformDetected(platform);
        }

        /// <summary>
        /// Manually trigger camera mode (useful for debugging)
        /// </summary>
        public void SetCameraModeManual(CameraMode mode)
        {
            SetCameraMode(mode);
        }

        bool IsVRPlatform(PlatformType platform)
        {
            return platform == PlatformType.VR_Oculus ||
                   platform == PlatformType.VR_SteamVR ||
                   platform == PlatformType.VR_OpenXR ||
                   platform == PlatformType.VR_Pico ||
                   platform == PlatformType.VR_Generic;
        }

        #endregion

        #region Debug and Utilities

        [ContextMenu("Switch to 3rd Person")]
        public void Debug_SwitchTo3rdPerson()
        {
            SetCameraMode(CameraMode.ThirdPerson);
        }

        [ContextMenu("Switch to VR")]
        public void Debug_SwitchToVR()
        {
            SetCameraMode(CameraMode.FirstPerson_VR);
        }

        [ContextMenu("Test Cinematic")]
        public void Debug_TestCinematic()
        {
            SetCameraMode(CameraMode.Cinematic);
            StartCoroutine(TestCinematicSequence());
        }

        IEnumerator TestCinematicSequence()
        {
            yield return new WaitForSeconds(3f);
            EndLoveTriggerCinematic();
        }

        [ContextMenu("Log Camera Status")]
        public void Debug_LogCameraStatus()
        {
            Debug.Log("=== CAMERA MANAGER STATUS ===");
            Debug.Log($"Current Platform: {currentPlatform}");
            Debug.Log($"Current Camera Mode: {currentCameraMode}");
            Debug.Log($"Active Camera: {(activeCamera != null ? activeCamera.name : "None")}");
            Debug.Log($"Is Cinematic Mode: {isInCinematicMode}");
            Debug.Log($"Follow Target: {(followTarget != null ? followTarget.name : "None")}");
            Debug.Log($"Look At Target: {(lookAtTarget != null ? lookAtTarget.name : "None")}");

            Debug.Log("--- Camera References ---");
            Debug.Log($"3rd Person Camera: {(thirdPersonCamera != null ? "OK" : "Missing")}");
            Debug.Log($"VR Camera: {(vrCamera != null ? "OK" : "Missing")}");
            Debug.Log($"Main Camera: {(mainCamera != null ? "OK" : "Missing")}");
            Debug.Log($"Cinematic Camera: {(cinematicCamera != null ? "OK" : "Missing")}");
            Debug.Log("============================");
        }

        #endregion

        #region Cleanup

        void OnDestroy()
        {
            if (PlatformDetectionSystem.Instance != null)
            {
                PlatformDetectionSystem.Instance.OnPlatformDetected -= OnPlatformDetected;
            }

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
        }

        #endregion
    }
}