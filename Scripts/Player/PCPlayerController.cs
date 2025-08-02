// =============================================================================
// PCPlayerController.cs - Third Person Controller for Desktop
// =============================================================================

using UnityEngine;

namespace LTSystem.Player
{
    /// <summary>
    /// PC Player Controller with Third Person Camera and traditional input
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PCPlayerController : BasePlayerController
    {
        [Header("PC Specific - Third Person")]
        public Camera thirdPersonCamera;
        public Transform cameraTarget;
        public float cameraDistance = 5f;
        public float cameraHeight = 2f;
        public float mouseSensitivity = 2f;

        [Header("PC Specific - Movement")]
        public float moveSpeed = 5f;
        public float runSpeed = 8f;
        public float jumpHeight = 2f;
        public float gravity = -9.81f;

        [Header("PC Specific - Input")]
        public KeyCode runKey = KeyCode.LeftShift;
        public KeyCode jumpKey = KeyCode.Space;
        public KeyCode interactKey = KeyCode.E;

        [Header("PC Specific - UI")]
        public Canvas desktopCanvas;
        public GameObject desktopCursor;

        [Header("PC Specific - Animation")]
        public RuntimeAnimatorController pcAnimatorController;

        // PC-specific components
        private CharacterController characterController;
        private Vector3 velocity;
        private bool isGrounded;
        private float cameraRotationX = 0f;
        private float cameraRotationY = 0f;

        // Input state
        private Vector2 moveInput;
        private Vector2 lookInput;
        private bool isRunning;
        private bool jumpPressed;

        #region Initialization

        protected override void DetectPlayerType()
        {
            playerType = PlayerType.PC_ThirdPerson;

            // Ensure we have required PC components
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            if (thirdPersonCamera == null)
                thirdPersonCamera = FindObjectOfType<Camera>();

            if (debugMode)
                Debug.Log($"[PCPlayerController] PC Third Person player detected and configured");
        }

        protected override void SetupCharacterForPlatform(GameObject characterInstance, CharacterData characterData)
        {
            // PC-specific character setup
            var animator = characterInstance.GetComponent<Animator>();
            if (animator != null && pcAnimatorController != null)
            {
                animator.runtimeAnimatorController = pcAnimatorController;
            }

            // Ensure character has appropriate colliders for third person
            var capsuleCollider = characterInstance.GetComponent<CapsuleCollider>();
            if (capsuleCollider == null)
            {
                capsuleCollider = characterInstance.AddComponent<CapsuleCollider>();
                capsuleCollider.height = 2f;
                capsuleCollider.radius = 0.5f;
                capsuleCollider.center = new Vector3(0, 1f, 0);
            }

            if (debugMode)
                Debug.Log($"[PCPlayerController] Character setup complete for PC platform");
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
        }

        protected override void HandleInput()
        {
            // Movement input
            moveInput.x = Input.GetAxis("Horizontal");
            moveInput.y = Input.GetAxis("Vertical");

            // Look input
            lookInput.x = Input.GetAxis("Mouse X") * mouseSensitivity;
            lookInput.y = Input.GetAxis("Mouse Y") * mouseSensitivity;

            // Action inputs
            isRunning = Input.GetKey(runKey);
            jumpPressed = Input.GetKeyDown(jumpKey);

            // Love trigger interaction
            if (Input.GetKeyDown(interactKey))
            {
                CheckForNearbyInteractions();
            }
        }

        protected override void HandleMovement()
        {
            if (characterController == null) return;

            // Ground check
            isGrounded = characterController.isGrounded;
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            // Movement
            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
            float currentSpeed = isRunning ? runSpeed : moveSpeed;

            characterController.Move(move * currentSpeed * Time.deltaTime);

            // Jump
            if (jumpPressed && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            // Gravity
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);

            // Update animator
            if (currentAnimController != null)
            {
                float speed = move.magnitude * currentSpeed;
                // Set animator parameters for third person movement
                // currentAnimController.SetFloat("Speed", speed);
                // currentAnimController.SetBool("IsRunning", isRunning);
            }
        }

        protected override void UpdateCamera()
        {
            if (thirdPersonCamera == null) return;

            // Mouse look
            cameraRotationY += lookInput.x;
            cameraRotationX -= lookInput.y;
            cameraRotationX = Mathf.Clamp(cameraRotationX, -80f, 80f);

            // Apply rotation to player body (Y-axis only)
            transform.rotation = Quaternion.Euler(0f, cameraRotationY, 0f);

            // Position camera behind player
            Vector3 targetPosition = transform.position;
            targetPosition += Vector3.up * cameraHeight;
            targetPosition -= transform.forward * cameraDistance;

            thirdPersonCamera.transform.position = targetPosition;
            thirdPersonCamera.transform.LookAt(transform.position + Vector3.up * cameraHeight);
        }

        protected override void UpdateUI()
        {
            if (desktopCanvas != null)
            {
                desktopCanvas.gameObject.SetActive(true);
            }

            // Handle cursor lock
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ?
                    CursorLockMode.None : CursorLockMode.Locked;
            }
        }

        #endregion

        #region PC Specific Methods

        protected override RuntimeAnimatorController GetAnimatorControllerForCharacter(CharacterData character)
        {
            return pcAnimatorController;
        }

        private void CheckForNearbyInteractions()
        {
            // Check for interactable objects
            var nearbyObjects = Physics.OverlapSphere(transform.position, maxTriggerDistance);
            foreach (var obj in nearbyObjects)
            {
                var interactable = obj.GetComponent<LTSystem.Objects.InteractableObject>();
                if (interactable != null)
                {
                    // Show interaction UI or trigger love action
                    var triggers = interactable.GetAvailableTriggers();
                    if (triggers != null && triggers.Length > 0)
                    {
                        TriggerLoveAction(triggers[0].triggerID);
                        break;
                    }
                }
            }
        }

        #endregion
    }
}