// =============================================================================
// PlayerControllerFactory.cs - Factory for creating appropriate player controllers
// =============================================================================

using UnityEngine;
using LTSystem.Player;

namespace LTSystem.Player
{
    /// <summary>
    /// Factory class for creating and configuring the appropriate player controller
    /// based on the prefab type and detected components
    /// </summary>
    public static class PlayerControllerFactory
    {
        /// <summary>
        /// Automatically configure a GameObject with the appropriate player controller
        /// </summary>
        public static BasePlayerController ConfigurePlayerController(GameObject playerObject)
        {
            if (playerObject == null)
            {
                Debug.LogError("[PlayerControllerFactory] Player object is null");
                return null;
            }

            PlayerType detectedType = DetectPlayerType(playerObject);
            return CreatePlayerController(playerObject, detectedType);
        }

        /// <summary>
        /// Create a specific type of player controller
        /// </summary>
        public static BasePlayerController CreatePlayerController(GameObject playerObject, PlayerType playerType)
        {
            if (playerObject == null)
            {
                Debug.LogError("[PlayerControllerFactory] Player object is null");
                return null;
            }

            // Remove any existing player controllers to avoid conflicts
            RemoveExistingControllers(playerObject);

            BasePlayerController controller = null;

            switch (playerType)
            {
                case BasePlayerController.PlayerType.ThirdPerson:
                    controller = CreateThirdPersonController(playerObject);
                    break;

                case BasePlayerController.PlayerType.VR:
                    controller = CreateVRController(playerObject);
                    break;

                case BasePlayerController.PlayerType.Mobile:
                    controller = CreateMobileController(playerObject);
                    break;

                default:
                    Debug.LogError($"[PlayerControllerFactory] Unsupported player type: {playerType}");
                    break;
            }

            if (controller != null)
            {
                Debug.Log($"[PlayerControllerFactory] Created {playerType} controller for {playerObject.name}");
            }

            return controller;
        }

        /// <summary>
        /// Detect the appropriate player type based on existing components
        /// </summary>
        public static PlayerType DetectPlayerType(GameObject playerObject)
        {
            if (playerObject == null)
                return BasePlayerController.PlayerType.ThirdPerson;

            // Check for VR indicators
            if (HasVRComponents(playerObject))
            {
                return BasePlayerController.PlayerType.VR;
            }

            // Check for mobile indicators
            if (HasMobileComponents(playerObject))
            {
                return BasePlayerController.PlayerType.Mobile;
            }

            // Default to third person
            return BasePlayerController.PlayerType.ThirdPerson;
        }

        private static bool HasVRComponents(GameObject playerObject)
        {
            // Check for VRIK component
            if (playerObject.GetComponentInChildren<RootMotion.FinalIK.VRIK>() != null)
                return true;

            // Check for XR Rig structures
            if (playerObject.transform.Find("XR Origin") != null ||
                playerObject.transform.Find("XR Rig") != null ||
                playerObject.transform.Find("[CameraRig]") != null ||
                playerObject.transform.Find("OVRCameraRig") != null)
                return true;

            // Check for VR-specific components
            if (playerObject.GetComponentInChildren<UnityEngine.XR.XRRig>() != null)
                return true;

            // Check prefab name
            if (playerObject.name.ToLower().Contains("xr") || 
                playerObject.name.ToLower().Contains("vr"))
                return true;

            return false;
        }

        private static bool HasMobileComponents(GameObject playerObject)
        {
            // Check for mobile-specific components or naming
            if (playerObject.name.ToLower().Contains("mobile"))
                return true;

            // Add other mobile detection logic here
            return false;
        }

        private static ThirdPersonPlayerController CreateThirdPersonController(GameObject playerObject)
        {
            var controller = playerObject.AddComponent<ThirdPersonPlayerController>();

            // Ensure required components exist
            EnsureCharacterController(playerObject);
            EnsureThirdPersonCamera(playerObject);
            EnsurePlayerInput(playerObject);

            return controller;
        }

        private static VRPlayerController CreateVRController(GameObject playerObject)
        {
            var controller = playerObject.AddComponent<VRPlayerController>();

            // Ensure VR components are properly configured
            EnsureVRRig(playerObject);
            EnsureVRIK(playerObject);

            return controller;
        }

        private static BasePlayerController CreateMobileController(GameObject playerObject)
        {
            // For now, mobile uses the same base as third person but with different settings
            var controller = playerObject.AddComponent<ThirdPersonPlayerController>();
            
            // Configure for mobile-specific settings
            // This could be expanded into a separate MobilePlayerController class

            return controller;
        }

        private static void RemoveExistingControllers(GameObject playerObject)
        {
            // Remove legacy PlayerController
            var legacyController = playerObject.GetComponent<PlayerController>();
            if (legacyController != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(legacyController);
                else
                    Object.DestroyImmediate(legacyController);
            }

            // Remove any existing BasePlayerController derivatives
            var existingControllers = playerObject.GetComponents<BasePlayerController>();
            foreach (var controller in existingControllers)
            {
                if (Application.isPlaying)
                    Object.Destroy(controller);
                else
                    Object.DestroyImmediate(controller);
            }
        }

        #region Component Ensuring Methods

        private static void EnsureCharacterController(GameObject playerObject)
        {
            if (playerObject.GetComponent<CharacterController>() == null)
            {
                var characterController = playerObject.AddComponent<CharacterController>();
                characterController.height = 2f;
                characterController.radius = 0.5f;
                characterController.center = new Vector3(0, 1f, 0);
            }
        }

        private static void EnsureThirdPersonCamera(GameObject playerObject)
        {
            var camera = playerObject.GetComponentInChildren<Camera>();
            if (camera == null)
            {
                GameObject cameraGO = new GameObject("ThirdPersonCamera");
                cameraGO.transform.SetParent(playerObject.transform);
                cameraGO.transform.localPosition = new Vector3(0, 1.8f, -5f);
                cameraGO.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
                
                camera = cameraGO.AddComponent<Camera>();
                camera.tag = "MainCamera";
            }
        }

        private static void EnsurePlayerInput(GameObject playerObject)
        {
            #if ENABLE_INPUT_SYSTEM
            if (playerObject.GetComponent<UnityEngine.InputSystem.PlayerInput>() == null)
            {
                var playerInput = playerObject.AddComponent<UnityEngine.InputSystem.PlayerInput>();
                
                // Try to load default input actions
                var inputActions = Resources.Load<UnityEngine.InputSystem.InputActionAsset>("Input/ThirdPersonInputActions");
                if (inputActions != null)
                {
                    playerInput.actions = inputActions;
                }
            }
            #endif
        }

        private static void EnsureVRRig(GameObject playerObject)
        {
            // Check if XR Rig already exists
            Transform xrRig = playerObject.transform.Find("XR Origin") ?? 
                             playerObject.transform.Find("XR Rig") ?? 
                             playerObject.transform.Find("[CameraRig]");

            if (xrRig == null)
            {
                Debug.LogWarning("[PlayerControllerFactory] No XR Rig found. VR functionality may be limited.");
                // Could create a basic XR rig here if needed
            }
        }

        private static void EnsureVRIK(GameObject playerObject)
        {
            var vrik = playerObject.GetComponentInChildren<RootMotion.FinalIK.VRIK>();
            if (vrik == null)
            {
                Debug.LogWarning("[PlayerControllerFactory] No VRIK component found. VR IK will not be available.");
                // VRIK should be added to the character model, not the player controller
            }
        }

        #endregion

        #region Validation Methods

        /// <summary>
        /// Validate that a player controller is properly configured
        /// </summary>
        public static bool ValidatePlayerController(BasePlayerController controller)
        {
            if (controller == null)
            {
                Debug.LogError("[PlayerControllerFactory] Controller is null");
                return false;
            }

            bool isValid = true;

            // Validate based on player type
            switch (controller.GetPlayerType())
            {
                case BasePlayerController.PlayerType.ThirdPerson:
                    isValid &= ValidateThirdPersonController(controller as ThirdPersonPlayerController);
                    break;

                case BasePlayerController.PlayerType.VR:
                    isValid &= ValidateVRController(controller as VRPlayerController);
                    break;

                case BasePlayerController.PlayerType.Mobile:
                    isValid &= ValidateThirdPersonController(controller as ThirdPersonPlayerController);
                    break;
            }

            return isValid;
        }

        private static bool ValidateThirdPersonController(ThirdPersonPlayerController controller)
        {
            if (controller == null) return false;

            bool isValid = true;

            if (controller.GetComponent<CharacterController>() == null)
            {
                Debug.LogError("[PlayerControllerFactory] ThirdPersonPlayerController missing CharacterController");
                isValid = false;
            }

            if (controller.GetComponentInChildren<Camera>() == null)
            {
                Debug.LogWarning("[PlayerControllerFactory] ThirdPersonPlayerController missing Camera");
            }

            return isValid;
        }

        private static bool ValidateVRController(VRPlayerController controller)
        {
            if (controller == null) return false;

            bool isValid = true;

            if (controller.GetComponentInChildren<Camera>() == null)
            {
                Debug.LogError("[PlayerControllerFactory] VRPlayerController missing VR Camera");
                isValid = false;
            }

            var xrRig = controller.transform.Find("XR Origin") ?? 
                       controller.transform.Find("XR Rig") ?? 
                       controller.transform.Find("[CameraRig]");

            if (xrRig == null)
            {
                Debug.LogWarning("[PlayerControllerFactory] VRPlayerController missing XR Rig");
            }

            return isValid;
        }

        #endregion

        #region Editor Utilities

        #if UNITY_EDITOR
        [UnityEditor.MenuItem("Love Trigger System/Convert Legacy Player Controllers")]
        public static void ConvertLegacyPlayerControllers()
        {
            var legacyControllers = Object.FindObjectsOfType<PlayerController>();
            int convertedCount = 0;

            foreach (var legacyController in legacyControllers)
            {
                if (legacyController.GetType() == typeof(PlayerController))
                {
                    GameObject playerObject = legacyController.gameObject;
                    
                    // Store settings before removal
                    var availableCharacters = legacyController.GetAvailableCharacters();
                    var startingCharacter = legacyController.startingCharacter;
                    var maxTriggerDistance = legacyController.maxTriggerDistance;
                    var requireMutualConsent = legacyController.requireMutualConsent;
                    var debugMode = legacyController.debugMode;

                    // Remove legacy controller
                    Object.DestroyImmediate(legacyController);

                    // Create new controller
                    var newController = ConfigurePlayerController(playerObject);
                    
                    if (newController != null)
                    {
                        // Restore settings
                        newController.availableCharacters = availableCharacters;
                        newController.startingCharacter = startingCharacter;
                        newController.maxTriggerDistance = maxTriggerDistance;
                        newController.requireMutualConsent = requireMutualConsent;
                        newController.debugMode = debugMode;

                        convertedCount++;
                        UnityEditor.EditorUtility.SetDirty(playerObject);
                    }
                }
            }

            Debug.Log($"[PlayerControllerFactory] Converted {convertedCount} legacy player controllers");
            UnityEditor.AssetDatabase.SaveAssets();
        }

        [UnityEditor.MenuItem("Love Trigger System/Validate All Player Controllers")]
        public static void ValidateAllPlayerControllers()
        {
            var allControllers = Object.FindObjectsOfType<BasePlayerController>();
            int validCount = 0;
            int invalidCount = 0;

            foreach (var controller in allControllers)
            {
                if (ValidatePlayerController(controller))
                {
                    validCount++;
                }
                else
                {
                    invalidCount++;
                }
            }

            Debug.Log($"[PlayerControllerFactory] Validation complete: {validCount} valid, {invalidCount} invalid controllers");
        }
        #endif

        #endregion
    }
}