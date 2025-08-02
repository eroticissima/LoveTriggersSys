// =============================================================================
// PlayerConfiguration.cs - ScriptableObject for Player Setup
// =============================================================================

using UnityEngine;
using LTSystem.Player;

namespace LTSystem.Factory
{
    [CreateAssetMenu(menuName = "Love Trigger System/Player Configuration", fileName = "PlayerConfig")]
    public class PlayerConfiguration : ScriptableObject
    {
        [Header("Love Trigger Settings")]
        public bool enableLoveTriggers = true;
        public float maxTriggerDistance = 5f;
        public bool requireMutualConsent = true;

        [Header("Character Settings")]
        public CharacterData[] availableCharacters;
        public CharacterData defaultCharacter;

        [Header("Platform Specific")]
        public PCPlayerSettings pcSettings;
        public XRPlayerSettings xrSettings;
        public ConsolePlayerSettings consoleSettings;

        [Header("Debug")]
        public bool debugMode = true;
    }

    [System.Serializable]
    public class PCPlayerSettings
    {
        [Header("Movement")]
        public float moveSpeed = 5f;
        public float runSpeed = 8f;
        public float jumpHeight = 2f;

        [Header("Camera")]
        public float mouseSensitivity = 2f;
        public float cameraDistance = 5f;
        public float cameraHeight = 2f;

        [Header("Input")]
        public KeyCode runKey = KeyCode.LeftShift;
        public KeyCode jumpKey = KeyCode.Space;
        public KeyCode interactKey = KeyCode.E;

        [Header("Animation")]
        public RuntimeAnimatorController animatorController;
    }

    [System.Serializable]
    public class XRPlayerSettings
    {
        [Header("Movement")]
        public bool useRoomScale = true;
        public bool enableTeleportation = true;
        public bool enableSmoothLocomotion = true;
        public float smoothMoveSpeed = 3f;
        public float turnSpeed = 60f;

        [Header("Input")]
        public bool useControllerInput = true;
        public bool useHandTracking = false;
        public float gripThreshold = 0.5f;
        public float triggerThreshold = 0.5f;

        [Header("UI")]
        public float uiDistance = 2f;
        public bool followHeadMovement = true;

        [Header("VRIK")]
        public bool enableVRIK = true;
        public float vrikWeight = 1f;

        [Header("Animation")]
        public RuntimeAnimatorController animatorController;
    }

    [System.Serializable]
    public class ConsolePlayerSettings
    {
        [Header("Movement")]
        public float moveSpeed = 5f;
        public float runSpeed = 8f;
        public float jumpHeight = 2f;

        [Header("Camera")]
        public float lookSensitivity = 3f;
        public float cameraDistance = 5f;
        public float cameraHeight = 2f;

        [Header("Input")]
        public string runButton = "Fire3";
        public string jumpButton = "Jump";
        public string interactButton = "Fire1";

        [Header("Animation")]
        public RuntimeAnimatorController animatorController;
    }
}