// =============================================================================
// XRPlayerController.cs - VR Controller with VRIK Integration
// =============================================================================

using UnityEngine;
using RootMotion.FinalIK;

// Add XR input support
#if UNITY_XR_MANAGEMENT || UNITY_XR_LEGACY
using UnityEngine.XR;
#endif

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
#endif

namespace LTSystem.Player
{
    /// <summary>
    /// XR Player Controller with VRIK and VR-specific input handling
    /// </summary>
    public class XRPlayerController : BasePlayerController
    {
        [Header("XR Specific - VRIK")]
        public VRIK vrikComponent;
        public Transform vrRig;
        public Transform headTransform;
        public Transform leftHandTransform;
        public Transform rightHandTransform;

        [Header("XR Specific - Movement")]
        public bool useRoomScale = true;
        public bool enableTeleportation = true;
        public bool enableSmoothLocomotion = true;
        public float smoothMoveSpeed = 3f;
        public float turnSpeed = 60f;

        [Header("XR Specific - Input")]
        public bool useControllerInput = true;
        public bool useHandTracking = false;
        public float gripThreshold = 0.5f;
        public float triggerThreshold = 0.5f;

        [Header("XR Specific - UI")]
        public Canvas vrCanvas;
        public float uiDistance = 2f;
        public bool followHeadMovement = true;

        [Header("XR Specific - Animation")]
        public RuntimeAnimatorController vrikAnimatorController;

        // XR-specific state
        private Vector2 primaryThumbstick;
        private Vector2 secondaryThumbstick;
        private bool leftTriggerPressed;
        private bool rightTriggerPressed;
        private bool leftGripPressed;
        private bool rightGripPressed;

        #region Initialization

        protected override void DetectPlayerType()
        {
            playerType = PlayerType.XR_VRIK;

            // Auto-detect VRIK component
            if (vrikComponent == null)
                vrikComponent = GetComponent<VRIK>();

            // Ensure we have VR rig setup
            if (vrRig == null)
                vrRig = transform.Find("VR Rig") ?? transform;

            if (debugMode)
                Debug.Log($"[XRPlayerController] XR VRIK player detected and configured");
        }

        protected override void SetupCharacterForPlatform(GameObject characterInstance, CharacterData characterData)
        {
            // XR-specific character setup
            var animator = characterInstance.GetComponent<Animator>();
            if (animator != null && vrikAnimatorController != null)
            {
                animator.runtimeAnimatorController = vrikAnimatorController;
            }

            // Setup VRIK if not already present
            if (vrikComponent == null)
            {
                vrikComponent = characterInstance.GetComponent<VRIK>();
                if (vrikComponent == null)
                {
                    vrikComponent = characterInstance.AddComponent<VRIK>();
                }
            }

            // Configure VRIK solver
            if (vrikComponent != null)
            {
                SetupVRIKReferences();
                ConfigureVRIKSettings();
            }

            if (debugMode)
                Debug.Log($"[XRPlayerController] Character setup complete for XR platform with VRIK");
        }

        private void SetupVRIKReferences()
        {
            if (vrikComponent == null) return;

            // Set VRIK references - Updated to use proper VRIK structure
            vrikComponent.references.root = transform;

            if (headTransform != null)
                vrikComponent.solver.spine.headTarget = headTransform;

            if (leftHandTransform != null)
                vrikComponent.solver.leftArm.target = leftHandTransform;

            if (rightHandTransform != null)
                vrikComponent.solver.rightArm.target = rightHandTransform;

            if (debugMode)
                Debug.Log($"[XRPlayerController] VRIK references configured");
        }

        private void ConfigureVRIKSettings()
        {
            if (vrikComponent == null) return;

            // Configure VRIK solver settings for optimal performance in VR
            var solver = vrikComponent.solver;
            solver.IKPositionWeight = 1f;
            solver.spine.headClampWeight = 0.5f;
            solver.spine.maintainPelvisPosition = 0.2f;

            if (debugMode)
                Debug.Log($"[XRPlayerController] VRIK settings configured");
        }

        #endregion

        #region Update Loop

        void Update()
        {
            if (!Object.HasInputAuthority) return;

            HandleInput();
            HandleMovement();
            UpdateCamera();
            UpdateUI();
            UpdateVRIK();
        }

        protected override void HandleInput()
        {
            // XR Controller input - using fallback methods when XR not available
            primaryThumbstick = GetThumbstickInput(true); // left hand
            secondaryThumbstick = GetThumbstickInput(false); // right hand

            leftTriggerPressed = GetTriggerInput(true) > triggerThreshold; // left hand
            rightTriggerPressed = GetTriggerInput(false) > triggerThreshold; // right hand

            leftGripPressed = GetGripInput(true) > gripThreshold; // left hand
            rightGripPressed = GetGripInput(false) > gripThreshold; // right hand

            // Handle love trigger interactions
            if (rightTriggerPressed)
            {
                CheckForVRInteractions();
            }
        }

        protected override void HandleMovement()
        {
            if (!enableSmoothLocomotion) return;

            // Smooth locomotion based on thumbstick
            Vector3 moveDirection = new Vector3(primaryThumbstick.x, 0, primaryThumbstick.y);

            // Use head transform for direction if available, otherwise use transform
            Transform directionReference = headTransform != null ? headTransform : transform;
            moveDirection = directionReference.TransformDirection(moveDirection);
            moveDirection.y = 0; // Keep movement horizontal

            transform.Translate(moveDirection * smoothMoveSpeed * Time.deltaTime, Space.World);

            // Smooth turning
            float turnInput = secondaryThumbstick.x;
            transform.Rotate(0, turnInput * turnSpeed * Time.deltaTime, 0);
        }

        protected override void UpdateCamera()
        {
            // In VR, camera is handled by the XR system
            // We just need to ensure proper head tracking integration
        }

        protected override void UpdateUI()
        {
            if (vrCanvas != null)
            {
                vrCanvas.gameObject.SetActive(true);

                if (followHeadMovement && headTransform != null)
                {
                    // Position UI in front of player
                    Vector3 targetPosition = headTransform.position + headTransform.forward * uiDistance;
                    vrCanvas.transform.position = targetPosition;
                    vrCanvas.transform.LookAt(headTransform);
                }
            }
        }

        private void UpdateVRIK()
        {
            if (vrikComponent == null) return;

            // VRIK is automatically updated, but we can add custom logic here
            // such as dynamic weight adjustments during love triggers
        }

        #endregion

        #region XR Specific Methods

        protected override RuntimeAnimatorController GetAnimatorControllerForCharacter(CharacterData character)
        {
            return vrikAnimatorController;
        }

        // Updated input methods to handle missing XR gracefully
        private Vector2 GetThumbstickInput(bool isLeftHand)
        {
#if UNITY_XR_MANAGEMENT || UNITY_XR_LEGACY
            try
            {
                var inputDevice = GetXRInputDevice(isLeftHand);
                if (inputDevice.isValid)
                {
                    Vector2 thumbstickValue;
                    if (inputDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out thumbstickValue))
                    {
                        return thumbstickValue;
                    }
                }
            }
            catch (System.Exception e)
            {
                if (debugMode)
                    Debug.LogWarning($"[XRPlayerController] XR input error: {e.Message}");
            }
#endif

            // Fallback to traditional input for testing
            if (isLeftHand)
                return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            else
                return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        }

        private float GetTriggerInput(bool isLeftHand)
        {
#if UNITY_XR_MANAGEMENT || UNITY_XR_LEGACY
            try
            {
                var inputDevice = GetXRInputDevice(isLeftHand);
                if (inputDevice.isValid)
                {
                    float triggerValue;
                    if (inputDevice.TryGetFeatureValue(CommonUsages.trigger, out triggerValue))
                    {
                        return triggerValue;
                    }
                }
            }
            catch (System.Exception e)
            {
                if (debugMode)
                    Debug.LogWarning($"[XRPlayerController] XR trigger input error: {e.Message}");
            }
#endif

            // Fallback for testing
            return isLeftHand ? (Input.GetKey(KeyCode.Q) ? 1f : 0f) : (Input.GetKey(KeyCode.E) ? 1f : 0f);
        }

        private float GetGripInput(bool isLeftHand)
        {
#if UNITY_XR_MANAGEMENT || UNITY_XR_LEGACY
            try
            {
                var inputDevice = GetXRInputDevice(isLeftHand);
                if (inputDevice.isValid)
                {
                    float gripValue;
                    if (inputDevice.TryGetFeatureValue(CommonUsages.grip, out gripValue))
                    {
                        return gripValue;
                    }
                }
            }
            catch (System.Exception e)
            {
                if (debugMode)
                    Debug.LogWarning($"[XRPlayerController] XR grip input error: {e.Message}");
            }
#endif

            // Fallback for testing
            return isLeftHand ? (Input.GetKey(KeyCode.LeftShift) ? 1f : 0f) : (Input.GetKey(KeyCode.RightShift) ? 1f : 0f);
        }

#if UNITY_XR_MANAGEMENT || UNITY_XR_LEGACY
        private InputDevice GetXRInputDevice(bool isLeftHand)
        {
            var characteristics = InputDeviceCharacteristics.Controller;
            characteristics |= isLeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right;
            
            var inputDevices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(characteristics, inputDevices);
            
            return inputDevices.Count > 0 ? inputDevices[0] : new InputDevice();
        }
#endif

        private void CheckForVRInteractions()
        {
            // Check for interactable objects within hand reach
            var handPosition = rightHandTransform != null ? rightHandTransform.position : transform.position;
            var nearbyObjects = Physics.OverlapSphere(handPosition, 1f); // Smaller range for VR

            foreach (var obj in nearbyObjects)
            {
                var interactable = obj.GetComponent<LTSystem.Objects.InteractableObject>();
                if (interactable != null)
                {
                    var triggers = interactable.GetAvailableTriggers();
                    if (triggers != null && triggers.Length > 0)
                    {
                        TriggerLoveAction(triggers[0].triggerID);

                        // Add haptic feedback
                        TriggerHapticFeedback();
                        break;
                    }
                }
            }
        }

        private void TriggerHapticFeedback()
        {
#if UNITY_XR_MANAGEMENT || UNITY_XR_LEGACY
            try
            {
                var rightController = GetXRInputDevice(false);
                if (rightController.isValid)
                {
                    HapticCapabilities capabilities;
                    if (rightController.TryGetHapticCapabilities(out capabilities))
                    {
                        if (capabilities.supportsImpulse)
                        {
                            rightController.SendHapticImpulse(0, 0.5f, 0.1f);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                if (debugMode)
                    Debug.LogWarning($"[XRPlayerController] Haptic feedback error: {e.Message}");
            }
#endif

            if (debugMode)
                Debug.Log($"[XRPlayerController] Triggering haptic feedback");
        }

        #endregion

        #region Public API for Testing

        [ContextMenu("Test VRIK Setup")]
        public void TestVRIKSetup()
        {
            if (vrikComponent == null)
            {
                Debug.LogError("[XRPlayerController] No VRIK component found!");
                return;
            }

            Debug.Log("=== VRIK SETUP TEST ===");
            Debug.Log($"VRIK Enabled: {vrikComponent.enabled}");
            Debug.Log($"Head Target: {(vrikComponent.solver.spine.headTarget != null ? vrikComponent.solver.spine.headTarget.name : "NULL")}");
            Debug.Log($"Left Hand Target: {(vrikComponent.solver.leftArm.target != null ? vrikComponent.solver.leftArm.target.name : "NULL")}");
            Debug.Log($"Right Hand Target: {(vrikComponent.solver.rightArm.target != null ? vrikComponent.solver.rightArm.target.name : "NULL")}");
            Debug.Log($"IK Position Weight: {vrikComponent.solver.IKPositionWeight}");
            Debug.Log("======================");
        }

        [ContextMenu("Test XR Input")]
        public void TestXRInput()
        {
            Debug.Log("=== XR INPUT TEST ===");
            Debug.Log($"Left Thumbstick: {primaryThumbstick}");
            Debug.Log($"Right Thumbstick: {secondaryThumbstick}");
            Debug.Log($"Left Trigger: {GetTriggerInput(true):F2}");
            Debug.Log($"Right Trigger: {GetTriggerInput(false):F2}");
            Debug.Log($"Left Grip: {GetGripInput(true):F2}");
            Debug.Log($"Right Grip: {GetGripInput(false):F2}");
            Debug.Log("====================");
        }

        #endregion
    }
}