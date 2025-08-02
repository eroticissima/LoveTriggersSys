// =============================================================================
// BasePlayerController.cs - Base class for all player types
// =============================================================================

using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Collections;
using LTSystem.Events;
using LTSystem.Network;

namespace LTSystem.Player
{
    /// <summary>
    /// Base class for all player controllers - handles common functionality
    /// </summary>
    public abstract class BasePlayerController : NetworkBehaviour
    {
        [Header("Player Identity")]
        [SerializeField] protected PlayerType playerType;
        [SerializeField] protected string playerID = "";
        
        [Header("Character Management")]
        public Transform characterRoot;
        public CharacterData[] availableCharacters;
        public CharacterData startingCharacter;

        [Header("Love Trigger System")]
        public bool enableLoveTriggers = true;
        public float maxTriggerDistance = 5f;
        public bool requireMutualConsent = true;

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

        // Current State
        protected CharacterData currentCharacterData;
        protected GameObject currentCharacterInstance;
        protected UniversalAnimationController currentAnimController;

        // Persistent Components
        protected UniversalLTMenuController menuController;
        protected NetworkedLoveTriggerManager triggerManager;
        protected Camera playerCamera;

        // Character switching
        protected Dictionary<string, CharacterData> characterDatabase = new Dictionary<string, CharacterData>();
        protected bool isCharacterSwitching = false;

        // Events
        public System.Action<CharacterData> OnCharacterSwitched;
        public System.Action<string> OnLoveTriggerStarted;
        public System.Action<string> OnLoveTriggerCompleted;

        public enum PlayerType
        {
            ThirdPerson,
            VR,
            Mobile
        }

        #region Abstract Methods - Must be implemented by derived classes

        /// <summary>
        /// Setup platform-specific input system
        /// </summary>
        protected abstract void SetupInputSystem();

        /// <summary>
        /// Setup platform-specific camera system
        /// </summary>
        protected abstract void SetupCameraSystem();

        /// <summary>
        /// Setup platform-specific animator controller
        /// </summary>
        protected abstract void SetupAnimatorController();

        /// <summary>
        /// Handle platform-specific movement input
        /// </summary>
        protected abstract void HandleMovementInput();

        /// <summary>
        /// Handle platform-specific interaction input
        /// </summary>
        protected abstract void HandleInteractionInput();

        /// <summary>
        /// Get platform-specific idle state name
        /// </summary>
        protected abstract string GetIdleStateName();

        /// <summary>
        /// Validate platform-specific components
        /// </summary>
        protected abstract bool ValidatePlatformComponents();

        #endregion

        #region Base Initialization

        public override void Spawned()
        {
            InitializeBaseController();

            if (Object.HasInputAuthority)
            {
                SetupInputAuthority();
            }

            if (Object.HasStateAuthority)
            {
                SetupStateAuthority();
            }
        }

        protected virtual void InitializeBaseController()
        {
            // Generate player ID if empty
            if (string.IsNullOrEmpty(playerID))
            {
                playerID = $"{playerType}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
            }

            BuildCharacterDatabase();
            SetupPersistentComponents();
            
            // Platform-specific setup
            SetupInputSystem();
            SetupCameraSystem();
            SetupAnimatorController();

            if (debugMode)
                Debug.Log($"[BasePlayerController] Initialized {playerType} player: {playerID}");
        }

        protected virtual void BuildCharacterDatabase()
        {
            characterDatabase.Clear();

            foreach (var character in availableCharacters)
            {
                if (character != null && !string.IsNullOrEmpty(character.characterID))
                {
                    characterDatabase[character.characterID] = character;
                }
            }

            if (debugMode)
                Debug.Log($"[BasePlayerController] Built character database with {characterDatabase.Count} characters");
        }

        protected virtual void SetupPersistentComponents()
        {
            // Setup NetworkedLoveTriggerManager
            triggerManager = GetComponent<NetworkedLoveTriggerManager>();
            if (triggerManager == null)
            {
                triggerManager = gameObject.AddComponent<NetworkedLoveTriggerManager>();
            }

            // Configure trigger manager
            triggerManager.MaxTriggerDistance = maxTriggerDistance;
            triggerManager.RequireMutualConsent = requireMutualConsent;
            triggerManager.DebugMode = debugMode;

            // Setup UniversalLTMenuController
            menuController = GetComponent<UniversalLTMenuController>();
            if (menuController == null)
            {
                menuController = gameObject.AddComponent<UniversalLTMenuController>();
            }

            // Configure menu controller
            menuController.autoConfigureForPlatform = true;
            menuController.debugMode = debugMode;

            // Setup audio
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                    audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        protected virtual void SetupInputAuthority()
        {
            if (debugMode)
                Debug.Log($"[BasePlayerController] Setting up input authority for {playerType}");

            if (startingCharacter != null)
            {
                SwitchCharacter(startingCharacter.characterID);
            }
            else if (availableCharacters.Length > 0)
            {
                SwitchCharacter(availableCharacters[0].characterID);
            }
        }

        protected virtual void SetupStateAuthority()
        {
            if (debugMode)
                Debug.Log($"[BasePlayerController] Setting up state authority for {playerType}");
        }

        #endregion

        #region Update Loop

        protected virtual void Update()
        {
            if (!Object.HasInputAuthority) return;

            HandleMovementInput();
            HandleInteractionInput();
        }

        #endregion

        #region Character Switching

        public virtual void SwitchCharacter(string characterID)
        {
            if (isCharacterSwitching) return;
            if (!characterDatabase.ContainsKey(characterID))
            {
                Debug.LogError($"[BasePlayerController] Character not found: {characterID}");
                return;
            }

            if (Object.HasInputAuthority)
            {
                RPC_RequestCharacterSwitch(characterID);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        protected virtual void RPC_RequestCharacterSwitch(NetworkString<_32> characterID)
        {
            if (debugMode)
                Debug.Log($"[BasePlayerController] Character switch requested: {characterID}");

            CurrentCharacterID = characterID;
            RPC_SwitchCharacterOnAllClients(characterID);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_SwitchCharacterOnAllClients(NetworkString<_32> characterID)
        {
            StartCoroutine(SwitchCharacterCoroutine(characterID.ToString()));
        }

        protected virtual IEnumerator SwitchCharacterCoroutine(string characterID)
        {
            isCharacterSwitching = true;

            if (debugMode)
                Debug.Log($"[BasePlayerController] Switching to character: {characterID}");

            // Stop current animation if playing
            if (currentAnimController != null && currentAnimController.IsPlaying())
            {
                currentAnimController.ForceStop();
            }

            // Validate character exists in database
            if (!characterDatabase.ContainsKey(characterID))
            {
                Debug.LogError($"[BasePlayerController] Character not found in database: {characterID}");
                isCharacterSwitching = false;
                yield break;
            }

            CharacterData newCharacterData = characterDatabase[characterID];

            // Destroy old character instance
            if (currentCharacterInstance != null)
            {
                DestroyImmediate(currentCharacterInstance);
                currentCharacterInstance = null;
                currentAnimController = null;
            }

            yield return new WaitForSeconds(0.1f);

            // Create new character instance
            if (newCharacterData.characterPrefab != null)
            {
                currentCharacterInstance = Instantiate(newCharacterData.characterPrefab, characterRoot);
                currentCharacterInstance.transform.localPosition = Vector3.zero;
                currentCharacterInstance.transform.localRotation = Quaternion.identity;

                SetupCharacterComponents(newCharacterData);
                UpdateLoveTriggerDatabase(newCharacterData);

                if (audioSource != null && characterSwitchSound != null)
                    audioSource.PlayOneShot(characterSwitchSound);
            }

            currentCharacterData = newCharacterData;
            isCharacterSwitching = false;

            OnCharacterSwitched?.Invoke(newCharacterData);

            if (debugMode)
                Debug.Log($"[BasePlayerController] Character switch completed: {characterID}");
        }

        protected virtual void SetupCharacterComponents(CharacterData characterData)
        {
            if (currentCharacterInstance == null) return;

            currentAnimController = currentCharacterInstance.GetComponent<UniversalAnimationController>();
            if (currentAnimController == null)
            {
                currentAnimController = currentCharacterInstance.AddComponent<UniversalAnimationController>();
            }

            // Configure animation controller for this player type
            ConfigureAnimationController(currentAnimController, characterData);

            // Subscribe to animation events
            currentAnimController.OnAnimationStart += OnAnimationStart;
            currentAnimController.OnAnimationComplete += OnAnimationComplete;
            currentAnimController.OnReturnToLocomotion += OnReturnToLocomotion;
        }

        protected virtual void ConfigureAnimationController(UniversalAnimationController animController, CharacterData characterData)
        {
            animController.autoConfigureForPlatform = true;
            animController.desktopIdleState = characterData.desktopIdleState;
            animController.vrIdleState = characterData.vrIdleState;
            animController.loveTriggerStateName = "LoveTrigger";
            animController.debugMode = debugMode;
        }

        protected virtual void UpdateLoveTriggerDatabase(CharacterData characterData)
        {
            if (triggerManager == null) return;

            if (characterData.availableLoveTriggers != null)
            {
                if (triggerManager.Database == null)
                {
                    var tempDatabase = ScriptableObject.CreateInstance<LTSystem.LoveTriggerDatabase>();
                    tempDatabase.triggers = characterData.availableLoveTriggers;
                    triggerManager.Database = tempDatabase;
                }
                else
                {
                    triggerManager.Database.triggers = characterData.availableLoveTriggers;
                }

                triggerManager.Database.Initialize();
            }

            if (debugMode)
                Debug.Log($"[BasePlayerController] Updated love trigger database: {characterData.availableLoveTriggers?.Length ?? 0} triggers");
        }

        #endregion

        #region Animation Events

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

        #endregion

        #region Public API

        public PlayerType GetPlayerType() => playerType;
        public string GetPlayerID() => playerID;
        public CharacterData GetCurrentCharacter() => currentCharacterData;
        public GameObject GetCurrentCharacterInstance() => currentCharacterInstance;
        public UniversalAnimationController GetCurrentAnimationController() => currentAnimController;
        public bool IsPlayingLoveTrigger() => currentAnimController?.IsPlaying() ?? false;
        public bool IsCharacterSwitching() => isCharacterSwitching;
        public CharacterData[] GetAvailableCharacters() => availableCharacters;

        public virtual void SetLoveTriggerEnabled(bool enabled)
        {
            enableLoveTriggers = enabled;

            if (triggerManager != null)
            {
                triggerManager.enabled = enabled;
            }

            if (menuController != null)
            {
                menuController.enabled = enabled;
            }
        }

        public bool HasInputAuthority()
        {
            if (Object == null) return true;
            return Object.HasInputAuthority;
        }

        public bool HasStateAuthority() => Object.HasStateAuthority;
        public PlayerRef GetPlayerRef() => Object.InputAuthority;

        public virtual bool CanSwitchCharacter()
        {
            if (currentAnimController != null && currentAnimController.IsPlaying())
            {
                return false;
            }

            if (isCharacterSwitching)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Validation

        protected virtual bool ValidateBaseComponents()
        {
            bool isValid = true;

            if (characterRoot == null)
            {
                Debug.LogError($"[BasePlayerController] Character root not assigned on {playerType} player");
                isValid = false;
            }

            if (availableCharacters == null || availableCharacters.Length == 0)
            {
                Debug.LogWarning($"[BasePlayerController] No available characters assigned on {playerType} player");
            }

            if (triggerManager == null)
            {
                Debug.LogError($"[BasePlayerController] NetworkedLoveTriggerManager not found on {playerType} player");
                isValid = false;
            }

            // Platform-specific validation
            isValid &= ValidatePlatformComponents();

            return isValid;
        }

        [ContextMenu("Validate Player Controller")]
        public virtual void ValidatePlayerController()
        {
            Debug.Log($"=== {playerType} Player Controller Validation ===");
            Debug.Log($"Player ID: {playerID}");
            Debug.Log($"Network Object: {(Object != null ? "EXISTS" : "NULL")}");
            Debug.Log($"Has Input Authority: {(Object?.HasInputAuthority ?? false)}");
            Debug.Log($"Current Character: {(currentCharacterData != null ? currentCharacterData.characterName : "NONE")}");
            Debug.Log($"Base Components Valid: {ValidateBaseComponents()}");
            Debug.Log("=======================================");
        }

        #endregion

        #region Cleanup

        protected virtual void OnDestroy()
        {
            // Unsubscribe from animation events
            if (currentAnimController != null)
            {
                currentAnimController.OnAnimationStart -= OnAnimationStart;
                currentAnimController.OnAnimationComplete -= OnAnimationComplete;
                currentAnimController.OnReturnToLocomotion -= OnReturnToLocomotion;
            }

            // Clean up events
            OnCharacterSwitched = null;
            OnLoveTriggerStarted = null;
            OnLoveTriggerCompleted = null;
        }

        #endregion
    }
}