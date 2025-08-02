// =============================================================================
// VRPlayerController.cs - VR Player Implementation with Final IK Integration
// =============================================================================

using UnityEngine;
using UnityEngine.XR;
using RootMotion.FinalIK;
using LTSystem.Player;

namespace LTSystem.Player
{
    /// <summary>
    /// VR Player Controller for XR_player.prefab
    /// Handles VR movement, hand tracking, and VRIK integration
    /// </summary>
    public class VRPlayerController : BasePlayerController
    {
        [Header("VR Movement")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float sprintSpeed = 5f;
        [SerializeField] private bool enableTeleportation = true;
        [SerializeField] private bool enableSmoothLocomotion = true;
        [SerializeField] private float snapTurnAngle = 30f;

        [Header("VR Rig Components")]
        [SerializeField] private Transform xrOrigin;
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform leftHandTransform;
        [SerializeField] private Transform rightHandTransform;
        [SerializeField] private Camera vrCamera;

        [Header("VRIK Integration")]
        [SerializeField] private VRIK vrikComponent;
        [SerializeField] private bool autoConfigureVRIK = true;
        [SerializeField] private float vrikSolverWeight = 1f;
        [SerializeField] private bool pauseVRIKDuringTriggers = true;

        [Header("Hand Tracking")]
        [SerializeField] private bool enableHandTracking = true;
        [SerializeField] private float handInteractionDistance = 0.5f;
        [SerializeField] private LayerMask interactionLayerMask = -1;

        [Header("VR Input")]
        [SerializeField] private XRNode leftControllerNode = XRNode.LeftHand;
        [SerializeField] private XRNode rightControllerNode = XRNode.RightHand;
        [SerializeField] private string triggerButton = "TriggerButton";
        [SerializeField] private string gripButton = "GripButton";
        [SerializeField] private string primaryButton = "PrimaryButton";

        [Header("Teleportation")]
        [SerializeField] private GameObject teleportationRayPrefab;
        [SerializeField] private GameObject teleportationMarkerPrefab;
        [SerializeField] private float teleportationRange = 10f;
        [SerializeField] private LayerMask teleportationLayerMask = 1;

        [Header("Comfort Settings")]
        [SerializeField] private bool enableVignetting = true;
        [SerializeField] private bool enableSnapTurn = true;
        [SerializeField] private float vignetteIntensity = 0.5f;

        // VR Input State
        private bool leftTriggerPressed;
        private bool rightTriggerPressed;
        private bool leftGripPressed;
        private bool rightGripPressed;
        private bool leftPrimaryPressed;
        private bool rightPrimaryPressed;

        // VR Movement State
        private Vector2 leftThumbstick;
        private Vector2 rightThumbstick;
        private bool isTeleporting;
        private Vector3 teleportationTarget;

        // Hand Interaction
        private GameObject leftHandInteractable;
        private GameObject rightHandInteractable;
        private LineRenderer leftHandRay;
        private LineRenderer rightHandRay;

        // VRIK State
        private bool vrikWasEnabled;
        private float originalVRIKWeight;

        #region Initialization

        protected override void InitializeBaseController()
        {
            playerType = PlayerType.VR;
            base.InitializeBaseController();
        }

        protected override void SetupInputSystem()
        {
            // VR input is handled through XR Input Subsystem
            // No traditional input system needed
            
            if (debugMode)
                Debug.Log("[VRPlayer] VR input system configured");
        }

        protected override void SetupCameraSystem()
        {
            // Find XR Origin
            if (xrOrigin == null)
            {
                xrOrigin = transform.Find("XR Origin") ?? transform.Find("XR Rig") ?? transform.Find("[CameraRig]");
                
                if (xrOrigin == null)
                {
                    Debug.LogError("[VRPlayer] XR Origin not found! Please ensure XR Rig is properly set up.");
                    return;
                }
            }

            // Find VR camera
            if (vrCamera == null)
            {
                vrCamera = GetComponentInChildren<Camera>();
                if (vrCamera == null)
                {
                    Debug.LogError("[VRPlayer] VR Camera not found in XR Rig!");
                    return;
                }
            }

            // Find head transform
            if (headTransform == null)
            {
                headTransform = vrCamera.transform;
            }

            // Find hand transforms
            if (leftHandTransform == null)
            {
                leftHandTransform = FindChildByName(xrOrigin, "LeftHand") ?? 
                                   FindChildByName(xrOrigin, "Left Controller") ??
                                   FindChildByName(xrOrigin, "LeftHandAnchor");
            }

            if (rightHandTransform == null)
            {
                rightHandTransform = FindChildByName(xrOrigin, "RightHand") ?? 
                                    FindChildByName(xrOrigin, "Right Controller") ??
                                    FindChildByName(xrOrigin, "RightHandAnchor");
            }

            playerCamera = vrCamera;

            // Setup hand rays for interaction
            SetupHandRays();

            if (debugMode)
                Debug.Log("[VRPlayer] VR camera system configured");
        }

        protected override void SetupAnimatorController()
        {
            // Setup VRIK component
            if (vrikComponent == null && autoConfigureVRIK)
            {
                vrikComponent = GetComponentInChildren<VRIK>();
                
                if (vrikComponent == null)
                {
                    Debug.LogWarning("[VRPlayer] VRIK component not found. VR IK will not be available.");
                    return;
                }
            }

            if (vrikComponent != null)
            {
                ConfigureVRIK();
            }

            if (debugMode)
                Debug.Log("[VRPlayer] VR animator controller configured");
        }

        private void ConfigureVRIK()
        {
            if (vrikComponent == null) return;

            // Store original settings
            vrikWasEnabled = vrikComponent.enabled;
            originalVRIKWeight = vrikComponent.solver.IKPositionWeight;

            // Configure VRIK references
            var references = vrikComponent.solver.spine;
            
            // Set head target
            if (headTransform != null)
            {
                references.headTarget = headTransform;
            }

            // Set hand targets
            if (leftHandTransform != null)
            {
                references.leftArmTarget = leftHandTransform;
            }

            if (rightHandTransform != null)
            {
                references.rightArmTarget = rightHandTransform;
            }

            // Configure solver settings
            vrikComponent.solver.IKPositionWeight = vrikSolverWeight;
            vrikComponent.solver.plantFeet = true;
            vrikComponent.solver.spine.maintainPelvisPosition = 0.5f;

            if (debugMode)
                Debug.Log("[VRPlayer] VRIK configured successfully");
        }

        private void SetupHandRays()
        {
            if (leftHandTransform != null)
            {
                leftHandRay = CreateHandRay(leftHandTransform, "LeftHandRay");
            }

            if (rightHandTransform != null)
            {
                rightHandRay = CreateHandRay(rightHandTransform, "RightHandRay");
            }
        }

        private LineRenderer CreateHandRay(Transform hand, string name)
        {
            GameObject rayObject = new GameObject(name);
            rayObject.transform.SetParent(hand);
            rayObject.transform.localPosition = Vector3.zero;
            rayObject.transform.localRotation = Quaternion.identity;

            LineRenderer ray = rayObject.AddComponent<LineRenderer>();
            ray.material = new Material(Shader.Find("Sprites/Default"));
            ray.startColor = Color.blue;
            ray.endColor = Color.blue;
            ray.startWidth = 0.01f;
            ray.endWidth = 0.01f;
            ray.positionCount = 2;
            ray.enabled = false;

            return ray;
        }

        private Transform FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>())
            {
                if (child.name.Contains(name))
                    return child;
            }
            return null;
        }

        #endregion

        #region Input Handling

        protected override void HandleMovementInput()
        {
            if (!Object.HasInputAuthority) return;

            UpdateVRInput();
            HandleVRMovement();
            HandleTeleportation();
        }

        protected override void HandleInteractionInput()
        {
            if (!Object.HasInputAuthority) return;

            HandleHandInteractions();
            HandleLoveTriggerInput();
        }

        private void UpdateVRInput()
        {
            // Get controller input
            var leftController = GetXRController(leftControllerNode);
            var rightController = GetXRController(rightControllerNode);

            if (leftController.isValid)
            {
                leftController.TryGetFeatureValue(CommonUsages.triggerButton, out leftTriggerPressed);
                leftController.TryGetFeatureValue(CommonUsages.gripButton, out leftGripPressed);
                leftController.TryGetFeatureValue(CommonUsages.primaryButton, out leftPrimaryPressed);
                leftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out leftThumbstick);
            }

            if (rightController.isValid)
            {
                rightController.TryGetFeatureValue(CommonUsages.triggerButton, out rightTriggerPressed);
                rightController.TryGetFeatureValue(CommonUsages.gripButton, out rightGripPressed);
                rightController.TryGetFeatureValue(CommonUsages.primaryButton, out rightPrimaryPressed);
                rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out rightThumbstick);
            }
        }

        private InputDevice GetXRController(XRNode node)
        {
            var inputDevices = new System.Collections.Generic.List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(node, inputDevices);
            return inputDevices.Count > 0 ? inputDevices[0] : default;
        }

        private void HandleVRMovement()
        {
            if (!enableSmoothLocomotion) return;

            // Smooth locomotion using left thumbstick
            if (leftThumbstick.magnitude > 0.1f)
            {
                Vector3 forward = vrCamera.transform.forward;
                Vector3 right = vrCamera.transform.right;
                
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();

                Vector3 moveDirection = forward * leftThumbstick.y + right * leftThumbstick.x;
                float currentSpeed = rightGripPressed ? sprintSpeed : moveSpeed;
                
                transform.Translate(moveDirection * currentSpeed * Time.deltaTime, Space.World);
            }

            // Snap turn using right thumbstick
            if (enableSnapTurn && Mathf.Abs(rightThumbstick.x) > 0.8f)
            {
                float turnAmount = rightThumbstick.x > 0 ? snapTurnAngle : -snapTurnAngle;
                transform.Rotate(0, turnAmount, 0);
                
                // Prevent continuous turning
                rightThumbstick = Vector2.zero;
            }
        }

        private void HandleTeleportation()
        {
            if (!enableTeleportation) return;

            // Start teleportation with right trigger
            if (rightTriggerPressed && !isTeleporting)
            {
                StartTeleportation();
            }
            else if (!rightTriggerPressed && isTeleporting)
            {
                ExecuteTeleportation();
            }

            if (isTeleporting)
            {
                UpdateTeleportationRay();
            }
        }

        private void StartTeleportation()
        {
            isTeleporting = true;
            if (rightHandRay != null)
            {
                rightHandRay.enabled = true;
            }
        }

        private void UpdateTeleportationRay()
        {
            if (rightHandTransform == null || rightHandRay == null) return;

            Ray teleportRay = new Ray(rightHandTransform.position, rightHandTransform.forward);
            
            if (Physics.Raycast(teleportRay, out RaycastHit hit, teleportationRange, teleportationLayerMask))
            {
                teleportationTarget = hit.point;
                rightHandRay.SetPosition(0, rightHandTransform.position);
                rightHandRay.SetPosition(1, hit.point);
                rightHandRay.startColor = Color.green;
                rightHandRay.endColor = Color.green;
            }
            else
            {
                rightHandRay.SetPosition(0, rightHandTransform.position);
                rightHandRay.SetPosition(1, rightHandTransform.position + rightHandTransform.forward * teleportationRange);
                rightHandRay.startColor = Color.red;
                rightHandRay.endColor = Color.red;
                teleportationTarget = Vector3.zero;
            }
        }

        private void ExecuteTeleportation()
        {
            isTeleporting = false;
            
            if (rightHandRay != null)
            {
                rightHandRay.enabled = false;
            }

            if (teleportationTarget != Vector3.zero)
            {
                // Calculate offset to maintain head position
                Vector3 headOffset = vrCamera.transform.position - transform.position;
                headOffset.y = 0; // Don't adjust for height
                
                Vector3 newPosition = teleportationTarget - headOffset;
                transform.position = newPosition;
                
                if (debugMode)
                    Debug.Log($"[VRPlayer] Teleported to: {newPosition}");
            }
        }

        private void HandleHandInteractions()
        {
            // Left hand interaction
            if (leftHandTransform != null)
            {
                UpdateHandInteraction(leftHandTransform, leftHandRay, leftTriggerPressed, ref leftHandInteractable);
            }

            // Right hand interaction (when not teleporting)
            if (rightHandTransform != null && !isTeleporting)
            {
                UpdateHandInteraction(rightHandTransform, rightHandRay, rightTriggerPressed, ref rightHandInteractable);
            }
        }

        private void UpdateHandInteraction(Transform hand, LineRenderer ray, bool triggerPressed, ref GameObject currentInteractable)
        {
            Ray handRay = new Ray(hand.position, hand.forward);
            
            if (Physics.Raycast(handRay, out RaycastHit hit, handInteractionDistance, interactionLayerMask))
            {
                GameObject hitObject = hit.collider.gameObject;
                
                // Show interaction ray
                if (ray != null && !ray.enabled)
                {
                    ray.enabled = true;
                    ray.SetPosition(0, hand.position);
                    ray.SetPosition(1, hit.point);
                }

                // Handle interaction
                if (triggerPressed && hitObject != currentInteractable)
                {
                    var interactable = hitObject.GetComponent<IInteractable>();
                    if (interactable != null)
                    {
                        interactable.OnInteract(this);
                        currentInteractable = hitObject;
                    }
                }
            }
            else
            {
                // Hide interaction ray
                if (ray != null && ray.enabled)
                {
                    ray.enabled = false;
                }
                
                currentInteractable = null;
            }
        }

        private void HandleLoveTriggerInput()
        {
            // Use grip buttons to trigger love actions
            if (leftGripPressed || rightGripPressed)
            {
                TriggerNearbyLoveAction();
            }
        }

        #endregion

        #region Character Setup Override

        protected override void SetupCharacterComponents(CharacterData characterData)
        {
            base.SetupCharacterComponents(characterData);

            if (currentCharacterInstance != null && vrikComponent != null)
            {
                // Update VRIK references to new character
                UpdateVRIKReferences();
                
                if (debugMode)
                    Debug.Log("[VRPlayer] VRIK references updated for new character");
            }
        }

        private void UpdateVRIKReferences()
        {
            if (vrikComponent == null || currentCharacterInstance == null) return;

            // Get the new character's animator
            Animator newAnimator = currentCharacterInstance.GetComponent<Animator>();
            if (newAnimator == null) return;

            // Update VRIK solver references
            var solver = vrikComponent.solver;
            
            // Update bone references from new character
            solver.SetToReferences(newAnimator.transform);
            
            // Maintain VR tracking targets
            solver.spine.headTarget = headTransform;
            solver.leftArm.target = leftHandTransform;
            solver.rightArm.target = rightHandTransform;

            if (debugMode)
                Debug.Log("[VRPlayer] VRIK references updated for character: " + currentCharacterData.characterName);
        }

        protected override void ConfigureAnimationController(UniversalAnimationController animController, CharacterData characterData)
        {
            base.ConfigureAnimationController(animController, characterData);

            // Configure for VR with VRIK
            animController.vrikComponent = vrikComponent;
            animController.autoDetectVRIK = true;
            animController.pauseVRIKDuringAnimations = pauseVRIKDuringTriggers;
        }

        #endregion

        #region Love Trigger Integration

        protected override void OnAnimationStart()
        {
            base.OnAnimationStart();

            // Pause VRIK during love triggers if enabled
            if (pauseVRIKDuringTriggers && vrikComponent != null)
            {
                StartCoroutine(BlendVRIKWeight(0f, 0.5f));
            }
        }

        protected override void OnAnimationComplete()
        {
            base.OnAnimationComplete();

            // Restore VRIK after love triggers
            if (pauseVRIKDuringTriggers && vrikComponent != null)
            {
                StartCoroutine(BlendVRIKWeight(originalVRIKWeight, 0.5f));
            }
        }

        private System.Collections.IEnumerator BlendVRIKWeight(float targetWeight, float duration)
        {
            if (vrikComponent == null) yield break;

            float startWeight = vrikComponent.solver.IKPositionWeight;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                vrikComponent.solver.IKPositionWeight = Mathf.Lerp(startWeight, targetWeight, t);
                yield return null;
            }

            vrikComponent.solver.IKPositionWeight = targetWeight;
        }

        #endregion

        #region Helper Methods

        private void TriggerNearbyLoveAction()
        {
            if (triggerManager == null) return;

            // Find nearby targets
            var nearbyTargets = Physics.OverlapSphere(transform.position, maxTriggerDistance);
            foreach (var target in nearbyTargets)
            {
                var targetNetworkObject = target.GetComponent<NetworkObject>();
                var targetManager = target.GetComponent<NetworkedLoveTriggerManager>();

                if (targetNetworkObject != null && targetManager != null && target.gameObject != gameObject)
                {
                    // Get first available trigger
                    if (currentCharacterData?.availableLoveTriggers != null && currentCharacterData.availableLoveTriggers.Length > 0)
                    {
                        string triggerID = currentCharacterData.availableLoveTriggers[0].triggerID;
                        triggerManager.RequestLoveTrigger(triggerID, targetNetworkObject);
                        break;
                    }
                }
            }
        }

        protected override string GetIdleStateName()
        {
            return currentCharacterData?.vrIdleState ?? "VRIK_Idle";
        }

        protected override bool ValidatePlatformComponents()
        {
            bool isValid = true;

            if (xrOrigin == null)
            {
                Debug.LogError("[VRPlayer] XR Origin not found");
                isValid = false;
            }

            if (vrCamera == null)
            {
                Debug.LogError("[VRPlayer] VR Camera not found");
                isValid = false;
            }

            if (headTransform == null)
            {
                Debug.LogWarning("[VRPlayer] Head transform not assigned");
            }

            if (leftHandTransform == null || rightHandTransform == null)
            {
                Debug.LogWarning("[VRPlayer] Hand transforms not found");
            }

            if (vrikComponent == null)
            {
                Debug.LogWarning("[VRPlayer] VRIK component not found - VR IK will not be available");
            }

            return isValid;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            // Draw interaction radius
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, maxTriggerDistance);

            // Draw hand interaction ranges
            if (leftHandTransform != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(leftHandTransform.position, handInteractionDistance);
            }

            if (rightHandTransform != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(rightHandTransform.position, handInteractionDistance);
            }

            // Draw teleportation range
            if (enableTeleportation && rightHandTransform != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawRay(rightHandTransform.position, rightHandTransform.forward * teleportationRange);
            }
        }

        #endregion

        #region Public API

        public VRIK GetVRIK() => vrikComponent;
        public Transform GetLeftHand() => leftHandTransform;
        public Transform GetRightHand() => rightHandTransform;
        public Transform GetHead() => headTransform;
        public Camera GetVRCamera() => vrCamera;

        public void SetVRIKWeight(float weight)
        {
            if (vrikComponent != null)
            {
                vrikComponent.solver.IKPositionWeight = weight;
            }
        }

        public void EnableTeleportation(bool enable)
        {
            enableTeleportation = enable;
        }

        public void EnableSmoothLocomotion(bool enable)
        {
            enableSmoothLocomotion = enable;
        }

        #endregion
    }

    /// <summary>
    /// Interface for VR interactable objects
    /// </summary>
    public interface IInteractable
    {
        void OnInteract(VRPlayerController player);
    }
}