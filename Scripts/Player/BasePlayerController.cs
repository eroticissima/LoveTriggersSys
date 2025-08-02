// =============================================================================
// BasePlayerController.cs - Abstract Base for All Player Types
// =============================================================================

using UnityEngine;
using Fusion;
using LTSystem.Events;
using LTSystem.Network;
using System.Collections;

namespace LTSystem.Player
{
    /// <summary>
    /// Abstract base class for all player controllers.
    /// Handles Love Trigger System integration and common networking.
    /// </summary>
    public abstract class BasePlayerController : NetworkBehaviour
    {
        [Header("Player Configuration")]
        public PlayerType playerType;
        public string playerID = "";

        [Header("Love Trigger System")]
        public bool enableLoveTriggers = true;
        public float maxTriggerDistance = 5f;
        public bool requireMutualConsent = true;

        [Header("Character Management")]
        public Transform characterRoot;
        public CharacterData[] availableCharacters;
        public CharacterData startingCharacter;

        [Header("Network Reference")]
        [SerializeField] protected NetworkedLoveTriggerManager networkedLTManager;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip characterSwitchSound;
        public AudioClip loveTriggerStartSound;
        public AudioClip loveTriggerCompleteSound;

        [Header("Debug")]
        public bool debugMode = true;

        // Network Properties
        [Networked] public NetworkString<_32> CurrentCharacterID { get; set; }
        [Networked] public LoveTriggerNetworkState NetworkState { get; set; }
        [Networked] public NetworkId PendingConsentRequester { get; set; }
        [Networked] public float ConsentRequestTime { get; set; }

        // Protected Members
        protected CharacterData currentCharacterData;
        protected GameObject currentCharacterInstance;
        protected UniversalAnimationController currentAnimController;
        protected bool isCharacterSwitching = false;

        // Events
        public System.Action<string> OnLoveTriggerStarted;
        public System.Action<string> OnLoveTriggerCompleted;
        public System.Action<string> OnCharacterChanged;
        public System.Action<PlayerType> OnPlayerTypeDetected;

        public enum PlayerType
        {
            PC_ThirdPerson,
            XR_VRIK,
            Console
        }

        #region Unity Lifecycle & Networking

        public override void Spawned()
        {
            InitializePlayerType();
            InitializeLoveTriggerSystem();
            InitializeCharacterSystem();

            if (debugMode)
                Debug.Log($"[BasePlayerController] Player spawned: {playerType}");
        }

        protected virtual void InitializePlayerType()
        {
            DetectPlayerType();
            OnPlayerTypeDetected?.Invoke(playerType);
        }

        protected virtual void DetectPlayerType()
        {
            if (debugMode)
                Debug.Log($"[BasePlayerController] Player type detected: {playerType}");
        }

        protected virtual void InitializeLoveTriggerSystem()
        {
            if (networkedLTManager == null)
                networkedLTManager = GetComponent<NetworkedLoveTriggerManager>();

            if (networkedLTManager != null)
            {
                networkedLTManager.MaxTriggerDistance = maxTriggerDistance;
                networkedLTManager.RequireMutualConsent = requireMutualConsent;
                networkedLTManager.DebugMode = debugMode;
            }
        }

        protected virtual void InitializeCharacterSystem()
        {
            if (startingCharacter != null)
            {
                SwitchCharacter(startingCharacter.characterID);
            }
        }

        #endregion

        #region Abstract Methods - Must be implemented by derived classes

        /// <summary>
        /// Platform-specific input handling
        /// </summary>
        protected abstract void HandleInput();

        /// <summary>
        /// Platform-specific movement implementation
        /// </summary>
        protected abstract void HandleMovement();

        /// <summary>
        /// Platform-specific UI management
        /// </summary>
        protected abstract void UpdateUI();

        /// <summary>
        /// Platform-specific camera management
        /// </summary>
        protected abstract void UpdateCamera();

        /// <summary>
        /// Get platform-specific animator controller for character
        /// </summary>
        protected abstract RuntimeAnimatorController GetAnimatorControllerForCharacter(CharacterData character);

        /// <summary>
        /// Platform-specific character setup
        /// </summary>
        protected abstract void SetupCharacterForPlatform(GameObject characterInstance, CharacterData characterData);

        #endregion

        #region Character Management

        public virtual void SwitchCharacter(string characterID)
        {
            if (isCharacterSwitching || string.IsNullOrEmpty(characterID))
                return;

            var characterData = System.Array.Find(availableCharacters, c => c.characterID == characterID);
            if (characterData == null)
            {
                Debug.LogError($"[BasePlayerController] Character not found: {characterID}");
                return;
            }

            StartCoroutine(SwitchCharacterCoroutine(characterData));
        }

        protected virtual IEnumerator SwitchCharacterCoroutine(CharacterData characterData)
        {
            isCharacterSwitching = true;

            // Cleanup current character
            if (currentCharacterInstance != null)
            {
                DestroyImmediate(currentCharacterInstance);
            }

            // Create new character
            if (characterData.characterPrefab != null)
            {
                currentCharacterInstance = Instantiate(characterData.characterPrefab, characterRoot);

                // Platform-specific setup
                SetupCharacterForPlatform(currentCharacterInstance, characterData);

                // Setup animation controller
                SetupAnimationController(characterData);

                currentCharacterData = characterData;
                CurrentCharacterID = characterData.characterID;

                OnCharacterChanged?.Invoke(characterData.characterID);

                if (debugMode)
                    Debug.Log($"[BasePlayerController] Character switched to: {characterData.characterName}");
            }

            yield return new WaitForEndOfFrame();
            isCharacterSwitching = false;
        }

        protected virtual void SetupAnimationController(CharacterData characterData)
        {
            if (currentCharacterInstance == null) return;

            currentAnimController = currentCharacterInstance.GetComponent<UniversalAnimationController>();
            if (currentAnimController == null)
            {
                currentAnimController = currentCharacterInstance.AddComponent<UniversalAnimationController>();
            }

            // Get platform-specific animator controller
            var animatorController = GetAnimatorControllerForCharacter(characterData);
            if (animatorController != null)
            {
                var animator = currentCharacterInstance.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.runtimeAnimatorController = animatorController;
                }
            }

            // Configure animation controller for platform
            currentAnimController.autoConfigureForPlatform = true;
            currentAnimController.desktopIdleState = characterData.desktopIdleState;
            currentAnimController.vrIdleState = characterData.vrIdleState;
            currentAnimController.loveTriggerStateName = "LoveTrigger";
            currentAnimController.debugMode = debugMode;

            // Subscribe to animation events
            currentAnimController.OnAnimationStart += OnAnimationStart;
            currentAnimController.OnAnimationComplete += OnAnimationComplete;
            currentAnimController.OnReturnToLocomotion += OnReturnToLocomotion;
        }

        #endregion

        #region Love Trigger Integration

        protected virtual void OnAnimationStart()
        {
            string triggerName = currentAnimController?.GetCurrentTriggerName() ?? "Unknown";

            if (debugMode)
                Debug.Log($"[BasePlayerController] Love trigger started: {triggerName}");

            if (audioSource != null && loveTriggerStartSound != null)
                audioSource.PlayOneShot(loveTriggerStartSound);

            OnLoveTriggerStarted?.Invoke(triggerName);
        }

        protected virtual void OnAnimationComplete()
        {
            string triggerName = currentAnimController?.GetCurrentTriggerName() ?? "Unknown";

            if (debugMode)
                Debug.Log($"[BasePlayerController] Love trigger completed: {triggerName}");

            if (audioSource != null && loveTriggerCompleteSound != null)
                audioSource.PlayOneShot(loveTriggerCompleteSound);

            OnLoveTriggerCompleted?.Invoke(triggerName);
        }

        protected virtual void OnReturnToLocomotion()
        {
            if (debugMode)
                Debug.Log($"[BasePlayerController] Returned to locomotion");
        }

        public virtual void TriggerLoveAction(string triggerID)
        {
            if (networkedLTManager != null && !string.IsNullOrEmpty(triggerID))
            {
                // Find nearby targets
                var nearbyTargets = Physics.OverlapSphere(transform.position, maxTriggerDistance);
                foreach (var target in nearbyTargets)
                {
                    var targetNetworkObject = target.GetComponent<NetworkObject>();
                    var targetManager = target.GetComponent<NetworkedLoveTriggerManager>();

                    if (targetNetworkObject != null && targetManager != null && target.gameObject != gameObject)
                    {
                        networkedLTManager.RequestLoveTrigger(triggerID, targetNetworkObject);
                        break;
                    }
                }
            }
        }

        #endregion

        #region Public API

        public CharacterData GetCurrentCharacter() => currentCharacterData;
        public GameObject GetCurrentCharacterInstance() => currentCharacterInstance;
        public UniversalAnimationController GetCurrentAnimationController() => currentAnimController;
        public bool IsPlayingLoveTrigger() => currentAnimController?.IsPlaying() ?? false;
        public bool CanSwitchCharacter() => !isCharacterSwitching && !IsPlayingLoveTrigger();
        public PlayerType GetPlayerType() => playerType;

        #endregion
    }
}