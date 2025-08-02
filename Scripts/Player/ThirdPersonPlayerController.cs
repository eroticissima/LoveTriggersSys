// =============================================================================
// ThirdPersonPlayerController.cs - Third Person Player Implementation
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using LTSystem.Player;

namespace LTSystem.Player
{
    /// <summary>
    /// Third Person Player Controller for PC_player.prefab
    /// Handles traditional third-person movement and camera controls
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonPlayerController : BasePlayerController
    {
        [Header("Third Person Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -15f;
        [SerializeField] private float groundedGravity = -0.05f;

        [Header("Third Person Camera")]
        [SerializeField] private Transform cameraFollowTarget;
        [SerializeField] private float mouseSensitivity = 1f;
        [SerializeField] private float cameraDistance = 5f;
        [SerializeField] private float minVerticalAngle = -30f;
        [SerializeField] private float maxVerticalAngle = 70f;

        [Header("Input System")]
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string lookActionName = "Look";
        [SerializeField] private string jumpActionName = "Jump";
        [SerializeField] private string sprintActionName = "Sprint";
        [SerializeField] private string interactActionName = "Interact";

        [Header("Ground Detection")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundDistance = 0.4f;
        [SerializeField] private LayerMask groundMask = 1;

        [Header("Animation")]
        [SerializeField] private string moveSpeedParameter = "MoveSpeed";
        [SerializeField] private string isGroundedParameter = "IsGrounded";
        [SerializeField] private string jumpTrigger = "Jump";

        // Components
        private CharacterController characterController;
        private Animator characterAnimator;
        private Camera thirdPersonCamera;
        private CinemachineVirtualCamera virtualCamera;

        // Input
        private Vector2 moveInput;
        private Vector2 lookInput;
        private bool jumpInput;
        private bool sprintInput;
        private bool interactInput;

        // Movement State
        private Vector3 velocity;
        private bool isGrounded;
        private bool isSprinting;
        private float currentMoveSpeed;

        // Camera State
        private float cameraVerticalRotation;
        private float cameraHorizontalRotation;

        #region Initialization

        protected override void InitializeBaseController()
        {
            playerType = PlayerType.ThirdPerson;
            base.InitializeBaseController();
        }

        protected override void SetupInputSystem()
        {
            // Get or add PlayerInput component
            if (playerInput == null)
                playerInput = GetComponent<PlayerInput>();

            if (playerInput == null)
            {
                playerInput = gameObject.AddComponent<PlayerInput>();
                
                // Load default input actions for third person
                var inputActions = Resources.Load<InputActionAsset>("Input/ThirdPersonInputActions");
                if (inputActions != null)
                {
                    playerInput.actions = inputActions;
                }
            }

            // Subscribe to input events
            if (playerInput != null && playerInput.actions != null)
            {
                var moveAction = playerInput.actions.FindAction(moveActionName);
                if (moveAction != null)
                {
                    moveAction.performed += OnMoveInput;
                    moveAction.canceled += OnMoveInput;
                }

                var lookAction = playerInput.actions.FindAction(lookActionName);
                if (lookAction != null)
                {
                    lookAction.performed += OnLookInput;
                    lookAction.canceled += OnLookInput;
                }

                var jumpAction = playerInput.actions.FindAction(jumpActionName);
                if (jumpAction != null)
                {
                    jumpAction.performed += OnJumpInput;
                    jumpAction.canceled += OnJumpInput;
                }

                var sprintAction = playerInput.actions.FindAction(sprintActionName);
                if (sprintAction != null)
                {
                    sprintAction.performed += OnSprintInput;
                    sprintAction.canceled += OnSprintInput;
                }

                var interactAction = playerInput.actions.FindAction(interactActionName);
                if (interactAction != null)
                {
                    interactAction.performed += OnInteractInput;
                }
            }

            if (debugMode)
                Debug.Log("[ThirdPersonPlayer] Input system configured");
        }

        protected override void SetupCameraSystem()
        {
            // Find or create camera follow target
            if (cameraFollowTarget == null)
            {
                GameObject followTarget = new GameObject("CameraFollowTarget");
                followTarget.transform.SetParent(transform);
                followTarget.transform.localPosition = new Vector3(0, 1.8f, 0);
                cameraFollowTarget = followTarget.transform;
            }

            // Find third person camera
            thirdPersonCamera = GetComponentInChildren<Camera>();
            if (thirdPersonCamera == null)
            {
                // Create camera if not found
                GameObject cameraGO = new GameObject("ThirdPersonCamera");
                cameraGO.transform.SetParent(transform);
                thirdPersonCamera = cameraGO.AddComponent<Camera>();
                thirdPersonCamera.tag = "MainCamera";
            }

            // Setup Cinemachine if available
            #if CINEMACHINE_AVAILABLE
            virtualCamera = GetComponentInChildren<CinemachineVirtualCamera>();
            if (virtualCamera == null)
            {
                GameObject vcamGO = new GameObject("ThirdPersonVirtualCamera");
                vcamGO.transform.SetParent(transform);
                virtualCamera = vcamGO.AddComponent<CinemachineVirtualCamera>();
                
                // Configure virtual camera
                virtualCamera.Follow = cameraFollowTarget;
                virtualCamera.LookAt = cameraFollowTarget;
                
                var composer = virtualCamera.AddCinemachineComponent<Cinemachine3rdPersonFollow>();
                composer.CameraDistance = cameraDistance;
                composer.ShoulderOffset = new Vector3(1f, 0f, 0f);
            }
            #endif

            playerCamera = thirdPersonCamera;

            if (debugMode)
                Debug.Log("[ThirdPersonPlayer] Camera system configured");
        }

        protected override void SetupAnimatorController()
        {
            // The character animator will be on the character instance
            // This is handled in SetupCharacterComponents
            if (debugMode)
                Debug.Log("[ThirdPersonPlayer] Animator controller setup deferred to character instantiation");
        }

        #endregion

        #region Input Handling

        private void OnMoveInput(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
        }

        private void OnLookInput(InputAction.CallbackContext context)
        {
            lookInput = context.ReadValue<Vector2>();
        }

        private void OnJumpInput(InputAction.CallbackContext context)
        {
            jumpInput = context.performed;
        }

        private void OnSprintInput(InputAction.CallbackContext context)
        {
            sprintInput = context.performed;
        }

        private void OnInteractInput(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                interactInput = true;
            }
        }

        protected override void HandleMovementInput()
        {
            if (!Object.HasInputAuthority) return;

            // Ground check
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

            // Handle gravity
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = groundedGravity;
            }

            // Handle jump
            if (jumpInput && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                
                if (characterAnimator != null)
                {
                    characterAnimator.SetTrigger(jumpTrigger);
                }
                
                jumpInput = false;
            }

            // Handle movement
            Vector3 move = Vector3.zero;
            if (moveInput.magnitude >= 0.1f)
            {
                // Calculate movement direction relative to camera
                Vector3 forward = thirdPersonCamera.transform.forward;
                Vector3 right = thirdPersonCamera.transform.right;
                
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();

                move = forward * moveInput.y + right * moveInput.x;
                move.Normalize();

                // Determine speed
                isSprinting = sprintInput;
                currentMoveSpeed = isSprinting ? sprintSpeed : moveSpeed;

                // Rotate character to face movement direction
                if (move != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(move);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
                }
            }
            else
            {
                currentMoveSpeed = 0f;
                isSprinting = false;
            }

            // Apply movement
            characterController.Move(move * currentMoveSpeed * Time.deltaTime);

            // Apply gravity
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);

            // Update animator
            if (characterAnimator != null)
            {
                float animationSpeed = move.magnitude * (isSprinting ? 2f : 1f);
                characterAnimator.SetFloat(moveSpeedParameter, animationSpeed);
                characterAnimator.SetBool(isGroundedParameter, isGrounded);
            }
        }

        protected override void HandleInteractionInput()
        {
            if (!Object.HasInputAuthority) return;

            if (interactInput)
            {
                // Handle love trigger interactions
                TriggerNearbyLoveAction();
                interactInput = false;
            }

            // Handle camera look
            if (lookInput.magnitude > 0.1f)
            {
                cameraHorizontalRotation += lookInput.x * mouseSensitivity;
                cameraVerticalRotation -= lookInput.y * mouseSensitivity;
                cameraVerticalRotation = Mathf.Clamp(cameraVerticalRotation, minVerticalAngle, maxVerticalAngle);

                // Apply camera rotation
                if (cameraFollowTarget != null)
                {
                    cameraFollowTarget.rotation = Quaternion.Euler(cameraVerticalRotation, cameraHorizontalRotation, 0f);
                }
            }
        }

        #endregion

        #region Character Setup Override

        protected override void SetupCharacterComponents(CharacterData characterData)
        {
            base.SetupCharacterComponents(characterData);

            if (currentCharacterInstance != null)
            {
                // Get character controller from character instance
                characterController = GetComponent<CharacterController>();
                if (characterController == null)
                {
                    characterController = gameObject.AddComponent<CharacterController>();
                    
                    // Configure character controller
                    characterController.height = 2f;
                    characterController.radius = 0.5f;
                    characterController.center = new Vector3(0, 1f, 0);
                }

                // Get animator from character instance
                characterAnimator = currentCharacterInstance.GetComponent<Animator>();

                // Setup ground check if not assigned
                if (groundCheck == null)
                {
                    GameObject groundCheckGO = new GameObject("GroundCheck");
                    groundCheckGO.transform.SetParent(transform);
                    groundCheckGO.transform.localPosition = new Vector3(0, 0.1f, 0);
                    groundCheck = groundCheckGO.transform;
                }

                if (debugMode)
                    Debug.Log("[ThirdPersonPlayer] Character components configured");
            }
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
            return currentCharacterData?.desktopIdleState ?? "Idle";
        }

        protected override bool ValidatePlatformComponents()
        {
            bool isValid = true;

            if (playerInput == null)
            {
                Debug.LogError("[ThirdPersonPlayer] PlayerInput component missing");
                isValid = false;
            }

            if (thirdPersonCamera == null)
            {
                Debug.LogWarning("[ThirdPersonPlayer] Third person camera not found");
            }

            if (cameraFollowTarget == null)
            {
                Debug.LogWarning("[ThirdPersonPlayer] Camera follow target not assigned");
            }

            return isValid;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            // Draw ground check
            if (groundCheck != null)
            {
                Gizmos.color = isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
            }

            // Draw interaction radius
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, maxTriggerDistance);
        }

        #endregion

        #region Cleanup

        protected override void OnDestroy()
        {
            // Unsubscribe from input events
            if (playerInput != null && playerInput.actions != null)
            {
                var moveAction = playerInput.actions.FindAction(moveActionName);
                if (moveAction != null)
                {
                    moveAction.performed -= OnMoveInput;
                    moveAction.canceled -= OnMoveInput;
                }

                var lookAction = playerInput.actions.FindAction(lookActionName);
                if (lookAction != null)
                {
                    lookAction.performed -= OnLookInput;
                    lookAction.canceled -= OnLookInput;
                }

                var jumpAction = playerInput.actions.FindAction(jumpActionName);
                if (jumpAction != null)
                {
                    jumpAction.performed -= OnJumpInput;
                    jumpAction.canceled -= OnJumpInput;
                }

                var sprintAction = playerInput.actions.FindAction(sprintActionName);
                if (sprintAction != null)
                {
                    sprintAction.performed -= OnSprintInput;
                    sprintAction.canceled -= OnSprintInput;
                }

                var interactAction = playerInput.actions.FindAction(interactActionName);
                if (interactAction != null)
                {
                    interactAction.performed -= OnInteractInput;
                }
            }

            base.OnDestroy();
        }

        #endregion
    }
}