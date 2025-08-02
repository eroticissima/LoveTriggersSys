// =============================================================================
// ConsolePlayerController.cs - Controller for Console Platforms
// =============================================================================

using UnityEngine;

namespace LTSystem.Player
{
    /// <summary>
    /// Console Player Controller optimized for gamepad input and console performance
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ConsolePlayerController : BasePlayerController
    {
        [Header("Console Specific - Movement")]
        public float moveSpeed = 5f;
        public float runSpeed = 8f;
        public float jumpHeight = 2f;
        public float gravity = -9.81f;

        [Header("Console Specific - Camera")]
        public Camera thirdPersonCamera;
        public Transform cameraTarget;
        public float cameraDistance = 5f;
        public float cameraHeight = 2f;
        public float lookSensitivity = 3f;
        public float cameraLerpSpeed = 10f;

        [Header("Console Specific - Input")]
        public string horizontalAxis = "Horizontal";
        public string verticalAxis = "Vertical";
        public string lookHorizontalAxis = "Mouse X";
        public string lookVerticalAxis = "Mouse Y";
        public string runButton = "Fire3";
        public string jumpButton = "Jump";
        public string interactButton = "Fire1";

        [Header("Console Specific - UI")]
        public Canvas consoleCanvas;
        public GameObject consoleUIElements;

        [Header("Console Specific - Animation")]
        public RuntimeAnimatorController consoleAnimatorController;

        [Header("Console Specific - Performance")]
        public bool enablePerformanceOptimizations = true;
        public int targetFramerate = 60;
        public float updateDistance = 50f; // Distance for LOD updates

        // Console-specific components
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
        private bool interactPressed;

        // Performance tracking
        private float lastUpdateTime;
        private float updateInterval = 0.016f; // ~60fps

        protected override void DetectPlayerType()
        {
            playerType = PlayerType.Console;

            // Setup console-specific performance settings
            if (enablePerformanceOptimizations)
            {
                Application.targetFrameRate = targetFramerate;
                QualitySettings.vSyncCount = 1;
            }

            // Ensure we have required console components
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            if (thirdPersonCamera == null)
                thirdPersonCamera = FindObjectOfType<Camera>();

            if (debugMode)
                Debug.Log($"[ConsolePlayerController] Console player detected and configured for {Application.platform}");
        }

        protected override void SetupCharacterForPlatform(GameObject characterInstance, CharacterData characterData)
        {
            // Console-specific character setup
            var animator = characterInstance.GetComponent<Animator>();
            if (animator != null && consoleAnimatorController != null)
            {
                animator.runtimeAnimatorController = consoleAnimatorController;

                // Optimize animator for console performance
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }

            // Ensure character has appropriate colliders for console
            var capsuleCollider = characterInstance.GetComponent<CapsuleCollider>();
            if (capsuleCollider == null)
            {
                capsuleCollider = characterInstance.AddComponent<CapsuleCollider>();
                capsuleCollider.height = 2f;
                capsuleCollider.radius = 0.5f;
                capsuleCollider.center = new Vector3(0, 1f, 0);
            }

            // Setup LOD system for console performance
            SetupLODSystem(characterInstance);

            if (debugMode)
                Debug.Log($"[ConsolePlayerController] Character setup complete for console platform");
        }

        private void SetupLODSystem(GameObject characterInstance)
        {
            var lodGroup = characterInstance.GetComponent<LODGroup>();
            if (lodGroup == null && enablePerformanceOptimizations)
            {
                lodGroup = characterInstance.AddComponent<LODGroup>();

                // Setup basic LOD levels for console performance
                var renderers = characterInstance.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    LOD[] lods = new LOD[2];
                    lods[0] = new LOD(0.3f, renderers); // High detail
                    lods[1] = new LOD(0.05f, new Renderer[0]); // Culled

                    lodGroup.SetLODs(lods);
                }
            }
        }

        void Update()
        {
            if (!Object.HasInputAuthority) return;

            // Performance-conscious update system
            if (enablePerformanceOptimizations)
            {
                float currentTime = Time.time;
                if (currentTime - lastUpdateTime < updateInterval)
                    return;
                lastUpdateTime = currentTime;
            }

            HandleInput();
            HandleMovement();
            UpdateCamera();
            UpdateUI();
        }

        protected override void HandleInput()
        {
            // Console gamepad input
            moveInput.x = Input.GetAxis(horizontalAxis);
            moveInput.y = Input.GetAxis(verticalAxis);

            // Look input with console-appropriate sensitivity
            lookInput.x = Input.GetAxis(lookHorizontalAxis) * lookSensitivity;
            lookInput.y = Input.GetAxis(lookVerticalAxis) * lookSensitivity;

            // Button inputs
            isRunning = Input.GetButton(runButton);
            jumpPressed = Input.GetButtonDown(jumpButton);
            interactPressed = Input.GetButtonDown(interactButton);

            // Handle love trigger interaction
            if (interactPressed)
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

            // Movement with console-optimized calculations
            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
            float currentSpeed = isRunning ? runSpeed : moveSpeed;

            // Apply deadzone for console controllers
            if (move.magnitude < 0.1f)
                move = Vector3.zero;

            characterController.Move(move * currentSpeed * Time.deltaTime);

            // Jump
            if (jumpPressed && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            // Gravity
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);

            // Update animator with console-optimized parameters
            if (currentAnimController != null)
            {
                float speed = move.magnitude * currentSpeed;
                // Reduce animator update frequency for performance
                if (Time.frameCount % 3 == 0) // Update every 3 frames
                {
                    // Set animator parameters for console movement
                    // currentAnimController.SetFloat("Speed", speed);
                    // currentAnimController.SetBool("IsRunning", isRunning);
                }
            }
        }

        protected override void UpdateCamera()
        {
            if (thirdPersonCamera == null) return;

            // Console gamepad look with smoothing
            cameraRotationY += lookInput.x;
            cameraRotationX -= lookInput.y;
            cameraRotationX = Mathf.Clamp(cameraRotationX, -80f, 80f);

            // Apply rotation to player body (Y-axis only)
            transform.rotation = Quaternion.Lerp(transform.rotation,
                Quaternion.Euler(0f, cameraRotationY, 0f),
                cameraLerpSpeed * Time.deltaTime);

            // Position camera behind player with console-optimized smoothing
            Vector3 targetPosition = transform.position;
            targetPosition += Vector3.up * cameraHeight;
            targetPosition -= transform.forward * cameraDistance;

            thirdPersonCamera.transform.position = Vector3.Lerp(
                thirdPersonCamera.transform.position,
                targetPosition,
                cameraLerpSpeed * Time.deltaTime);

            thirdPersonCamera.transform.LookAt(transform.position + Vector3.up * cameraHeight);
        }

        protected override void UpdateUI()
        {
            if (consoleCanvas != null)
            {
                consoleCanvas.gameObject.SetActive(true);
            }

            if (consoleUIElements != null)
            {
                // Update UI elements for console-specific display
                consoleUIElements.SetActive(true);
            }
        }

        protected override RuntimeAnimatorController GetAnimatorControllerForCharacter(CharacterData character)
        {
            return consoleAnimatorController;
        }

        private void CheckForNearbyInteractions()
        {
            // Console-optimized interaction checking
            var nearbyObjects = Physics.OverlapSphere(transform.position, maxTriggerDistance,
                ~0, QueryTriggerInteraction.Collide);

            foreach (var obj in nearbyObjects)
            {
                var interactable = obj.GetComponent<LTSystem.Objects.InteractableObject>();
                if (interactable != null)
                {
                    var triggers = interactable.GetAvailableTriggers();
                    if (triggers != null && triggers.Length > 0)
                    {
                        TriggerLoveAction(triggers[0].triggerID);

                        // Console haptic feedback if available
                        TriggerControllerVibration();
                        break;
                    }
                }
            }
        }

        private void TriggerControllerVibration()
        {
            // Implement controller vibration for console feedback
            if (debugMode)
                Debug.Log($"[ConsolePlayerController] Triggering controller vibration feedback");

            // Platform-specific vibration implementation would go here
            // For example, for PlayStation or Xbox controllers
        }

        public void SetPerformanceMode(bool enabled)
        {
            enablePerformanceOptimizations = enabled;

            if (enabled)
            {
                Application.targetFrameRate = targetFramerate;
                QualitySettings.vSyncCount = 1;
                updateInterval = 1f / targetFramerate;
            }
            else
            {
                Application.targetFrameRate = -1;
                updateInterval = 0f;
            }

            if (debugMode)
                Debug.Log($"[ConsolePlayerController] Performance mode: {(enabled ? "Enabled" : "Disabled")}");
        }

        public void AdjustQualityForConsole(int qualityLevel)
        {
            QualitySettings.SetQualityLevel(qualityLevel, true);

            if (debugMode)
                Debug.Log($"[ConsolePlayerController] Quality level set to: {qualityLevel}");
        }

        [ContextMenu("Optimize for Current Console")]
        public void OptimizeForCurrentConsole()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.PS4:
                    targetFramerate = 30;
                    AdjustQualityForConsole(2); // Medium quality
                    break;

                case RuntimePlatform.PS5:
                    targetFramerate = 60;
                    AdjustQualityForConsole(4); // High quality
                    break;

                case RuntimePlatform.XboxOne:
                case RuntimePlatform.GameCoreXboxOne:
                    targetFramerate = 30;
                    AdjustQualityForConsole(2); // Medium quality
                    break;

                case RuntimePlatform.GameCoreXboxSeries:
                    targetFramerate = 60;
                    AdjustQualityForConsole(4); // High quality
                    break;

                default:
                    targetFramerate = 60;
                    AdjustQualityForConsole(3); // Default quality
                    break;
            }

            SetPerformanceMode(true);

            if (debugMode)
                Debug.Log($"[ConsolePlayerController] Optimized for {Application.platform} - Target FPS: {targetFramerate}");
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (enablePerformanceOptimizations)
            {
                // Pause/resume optimizations based on focus
                Time.timeScale = hasFocus ? 1f : 0f;

                if (debugMode)
                    Debug.Log($"[ConsolePlayerController] Application focus changed: {hasFocus}");
            }
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (enablePerformanceOptimizations)
            {
                // Handle console suspend/resume
                if (pauseStatus)
                {
                    // Save state if needed
                    if (debugMode)
                        Debug.Log("[ConsolePlayerController] Application paused - saving state");
                }
                else
                {
                    // Restore state if needed
                    if (debugMode)
                        Debug.Log("[ConsolePlayerController] Application resumed - restoring state");
                }
            }
        }
    }
}