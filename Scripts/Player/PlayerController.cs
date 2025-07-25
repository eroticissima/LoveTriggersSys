// =============================================================================
// PlayerController.cs - FIXED FOR FUSION 2 (RpcTarget Issue)
// =============================================================================

using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Collections;
using LTSystem.Events;
using LTSystem.Network;

// FIXED: Conditional imports to avoid compilation errors
#if GAMEPLAY_INGREDIENTS_AVAILABLE
using GameplayIngredients;
#endif

/// <summary>
/// Persistent player controller that manages love triggers across character switches
/// </summary>
public class PlayerController : NetworkBehaviour
{
    [Header("Network Reference")]
    [SerializeField] private NetworkedLoveTriggerManager networkedLTManager;

    [Header("Character Management")]
    public Transform characterRoot;
    public CharacterData[] availableCharacters;
    public CharacterData startingCharacter;

    [Header("Love Trigger System")]
    public bool enableLoveTriggers = true;
    public float maxTriggerDistance = 5f;
    public bool requireMutualConsent = true;

    [Header("Platform Detection")]
    public bool autoDetectPlatform = true;
    public PlatformType forcePlatform = PlatformType.Desktop;

    [Header("UI Management")]
    public Canvas desktopCanvas;
    public Canvas vrCanvas;
    public Transform uiFollowTarget;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip characterSwitchSound;
    public AudioClip loveTriggerStartSound;
    public AudioClip loveTriggerCompleteSound;

    [Header("Debug")]
    public bool debugMode = true;

    // Network Properties - FIXED: Using proper network types
    [Networked] public NetworkString<_32> CurrentCharacterID { get; set; }
    [Networked] public LoveTriggerNetworkState NetworkState { get; set; }
    [Networked] public NetworkId PendingConsentRequester { get; set; }
    [Networked] public float ConsentRequestTime { get; set; }

    // Current State
    private CharacterData currentCharacterData;
    private GameObject currentCharacterInstance;
    private UniversalAnimationController currentAnimController;

    // Persistent Components
    private UniversalLTMenuController menuController;
    private NetworkedLoveTriggerManager triggerManager;
    private Camera playerCamera;

    // Character switching
    private Dictionary<string, CharacterData> characterDatabase = new Dictionary<string, CharacterData>();
    private bool isCharacterSwitching = false;

    // Events
    public System.Action<CharacterData> OnCharacterSwitched;
    public System.Action<string> OnLoveTriggerStarted;
    public System.Action<string> OnLoveTriggerCompleted;

    #region Initialization

    public override void Spawned()
    {
        InitializePlayerController();

        if (Object.HasInputAuthority)
        {
            SetupInputAuthority();
        }

        if (Object.HasStateAuthority)
        {
            SetupStateAuthority();
        }
    }

    void InitializePlayerController()
    {
        BuildCharacterDatabase();
        SetupCamera();
        SetupPersistentComponents();

        if (autoDetectPlatform)
        {
            SetupPlatformDetection();
        }

        if (debugMode)
            Debug.Log($"[PlayerController] Initialized for {Object.InputAuthority}");
    }

    void BuildCharacterDatabase()
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
            Debug.Log($"[PlayerController] Built character database with {characterDatabase.Count} characters");
    }

    void SetupCamera()
    {
        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (uiFollowTarget == null)
        {
            uiFollowTarget = playerCamera?.transform;
        }
    }

    void SetupPersistentComponents()
    {
        // Setup NetworkedLoveTriggerManager
        if (networkedLTManager == null)
        {
            triggerManager = GetComponent<NetworkedLoveTriggerManager>();
            if (triggerManager == null)
            {
                triggerManager = gameObject.AddComponent<NetworkedLoveTriggerManager>();
            }
            networkedLTManager = triggerManager;
        }
        else
        {
            triggerManager = networkedLTManager;
        }

        // Configure trigger manager
        if (triggerManager != null)
        {
            triggerManager.MaxTriggerDistance = maxTriggerDistance;
            triggerManager.RequireMutualConsent = requireMutualConsent;
            triggerManager.DebugMode = debugMode;
        }

        // Setup UniversalLTMenuController
        menuController = GetComponent<UniversalLTMenuController>();
        if (menuController == null)
        {
            menuController = gameObject.AddComponent<UniversalLTMenuController>();
        }

        // Configure menu controller
        if (menuController != null)
        {
            menuController.autoConfigureForPlatform = autoDetectPlatform;
            menuController.desktopCanvas = desktopCanvas;
            menuController.vrCanvas = vrCanvas;
            menuController.debugMode = debugMode;
        }

        // Setup audio
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void SetupPlatformDetection()
    {
        if (PlatformDetectionSystem.Instance != null)
        {
            PlatformDetectionSystem.Instance.OnPlatformDetected += OnPlatformChanged;
        }
        else if (autoDetectPlatform)
        {
            CreatePlatformDetectionSystem();
        }
    }

    void CreatePlatformDetectionSystem()
    {
        if (PlatformDetectionSystem.Instance == null)
        {
            GameObject platformManager = new GameObject("PlatformManager");
            DontDestroyOnLoad(platformManager);
            platformManager.AddComponent<PlatformDetectionSystem>();
            platformManager.AddComponent<UniversalInputSystem>();
        }
    }

    void SetupInputAuthority()
    {
        if (debugMode)
            Debug.Log($"[PlayerController] Setting up input authority");

        if (startingCharacter != null)
        {
            SwitchCharacter(startingCharacter.characterID);
        }
        else if (availableCharacters.Length > 0)
        {
            SwitchCharacter(availableCharacters[0].characterID);
        }
    }

    void SetupStateAuthority()
    {
        if (debugMode)
            Debug.Log($"[PlayerController] Setting up state authority");
    }

    #endregion

    #region Character Switching

    public void SwitchCharacter(string characterID)
    {
        if (isCharacterSwitching) return;
        if (!characterDatabase.ContainsKey(characterID))
        {
            Debug.LogError($"[PlayerController] Character not found: {characterID}");
            return;
        }

        if (Object.HasInputAuthority)
        {
            RPC_RequestCharacterSwitch(characterID);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_RequestCharacterSwitch(NetworkString<_32> characterID)
    {
        if (debugMode)
            Debug.Log($"[PlayerController] Character switch requested: {characterID}");

        CurrentCharacterID = characterID;
        // FIXED: Use RpcTargets.All instead of RpcTarget.All
        RPC_SwitchCharacterOnAllClients(characterID);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_SwitchCharacterOnAllClients(NetworkString<_32> characterID)
    {
        StartCoroutine(SwitchCharacterCoroutine(characterID.ToString()));
    }

    IEnumerator SwitchCharacterCoroutine(string characterID)
    {
        isCharacterSwitching = true;

        if (debugMode)
            Debug.Log($"[PlayerController] Switching to character: {characterID}");

        // Stop current animation if playing
        if (currentAnimController != null && currentAnimController.IsPlaying())
        {
            currentAnimController.ForceStop();
        }

        // Validate character exists in database
        if (!characterDatabase.ContainsKey(characterID))
        {
            Debug.LogError($"[PlayerController] Character not found in database: {characterID}");
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
            Debug.Log($"[PlayerController] Character switch completed: {characterID}");
    }

    void SetupCharacterComponents(CharacterData characterData)
    {
        if (currentCharacterInstance == null) return;

        currentAnimController = currentCharacterInstance.GetComponent<UniversalAnimationController>();
        if (currentAnimController == null)
        {
            currentAnimController = currentCharacterInstance.AddComponent<UniversalAnimationController>();
        }

        // Configure animation controller
        currentAnimController.autoConfigureForPlatform = autoDetectPlatform;
        currentAnimController.desktopIdleState = characterData.desktopIdleState;
        currentAnimController.vrIdleState = characterData.vrIdleState;
        currentAnimController.loveTriggerStateName = "LoveTrigger";
        currentAnimController.debugMode = debugMode;

        // Subscribe to animation events
        currentAnimController.OnAnimationStart += OnAnimationStart;
        currentAnimController.OnAnimationComplete += OnAnimationComplete;
        currentAnimController.OnReturnToLocomotion += OnReturnToLocomotion;

        ApplyCharacterSettings(characterData);
    }

    void ApplyCharacterSettings(CharacterData characterData)
    {
        if (currentAnimController == null) return;

        // Apply custom animation settings if available
        if (debugMode)
            Debug.Log($"[PlayerController] Applied settings for character: {characterData.characterName}");
    }

    void UpdateLoveTriggerDatabase(CharacterData characterData)
    {
        if (triggerManager == null) return;

        // FIXED: Safe database update
        if (characterData.availableLoveTriggers != null)
        {
            if (triggerManager.Database == null)
            {
                // Create a temporary database
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
            Debug.Log($"[PlayerController] Updated love trigger database: {characterData.availableLoveTriggers?.Length ?? 0} triggers for {characterData.characterName}");
    }

    #endregion

    #region Love Trigger Integration

    void OnAnimationStart()
    {
        string triggerName = currentAnimController?.GetCurrentTriggerName() ?? "Unknown";

        if (debugMode)
            Debug.Log($"[PlayerController] Love trigger started: {triggerName}");

        if (audioSource != null && loveTriggerStartSound != null)
            audioSource.PlayOneShot(loveTriggerStartSound);

        OnLoveTriggerStarted?.Invoke(triggerName);
    }

    void OnAnimationComplete()
    {
        string triggerName = currentAnimController?.GetCurrentTriggerName() ?? "Unknown";

        if (debugMode)
            Debug.Log($"[PlayerController] Love trigger completed: {triggerName}");

        if (audioSource != null && loveTriggerCompleteSound != null)
            audioSource.PlayOneShot(loveTriggerCompleteSound);

        OnLoveTriggerCompleted?.Invoke(triggerName);
    }

    void OnReturnToLocomotion()
    {
        if (debugMode)
            Debug.Log($"[PlayerController] Returned to locomotion");
    }

    void OnPlatformChanged(PlatformType platform)
    {
        if (debugMode)
            Debug.Log($"[PlayerController] Platform changed to: {platform}");
    }

    #endregion

    #region Character Selection Integration

    public void OnCharacterSelected(string characterID)
    {
        if (debugMode)
            Debug.Log($"[PlayerController] Character selected: {characterID}");

        SwitchCharacter(characterID);
    }

    public string GetCurrentCharacterID()
    {
        return currentCharacterData?.characterID ?? "";
    }

    public bool CanSwitchCharacter()
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

    public void TriggerLoveAction(string triggerID)
    {
        if (triggerManager != null && !string.IsNullOrEmpty(triggerID))
        {
            // Find nearby targets
            var nearbyTargets = Physics.OverlapSphere(transform.position, maxTriggerDistance);
            foreach (var target in nearbyTargets)
            {
                var targetNetworkObject = target.GetComponent<NetworkObject>();
                var targetManager = target.GetComponent<NetworkedLoveTriggerManager>();

                if (targetNetworkObject != null && targetManager != null && target.gameObject != gameObject)
                {
                    triggerManager.RequestLoveTrigger(triggerID, targetNetworkObject);
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
    public bool IsCharacterSwitching() => isCharacterSwitching;
    public CharacterData[] GetAvailableCharacters() => availableCharacters;

    public void SetLoveTriggerEnabled(bool enabled)
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

    // Network state queries
    // public bool HasInputAuthority() => Object.HasInputAuthority;
    public bool HasInputAuthority()
    {
        if (Object == null) return true; // Non-networked scenario
        return Object.HasInputAuthority;
    }
    public bool HasStateAuthority() => Object.HasStateAuthority;
    public PlayerRef GetPlayerRef() => Object.InputAuthority;

    // Distance and interaction helpers
    public float GetDistanceToPlayer(PlayerController otherPlayer)
    {
        if (otherPlayer == null) return float.MaxValue;
        return Vector3.Distance(transform.position, otherPlayer.transform.position);
    }

    public bool IsInRangeOf(PlayerController otherPlayer)
    {
        return GetDistanceToPlayer(otherPlayer) <= maxTriggerDistance;
    }

    #endregion

    #region Cleanup

    void OnDestroy()
    {
        // Unsubscribe from platform detection
        if (PlatformDetectionSystem.Instance != null)
        {
            PlatformDetectionSystem.Instance.OnPlatformDetected -= OnPlatformChanged;
        }

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

    #region Debug and Validation

    [ContextMenu("Validate Player Controller")]
    public void ValidatePlayerController()
    {
        Debug.Log("=== PlayerController Validation ===");
        Debug.Log($"Network Object: {(Object != null ? "EXISTS" : "NULL")}");
        Debug.Log($"Has Input Authority: {(Object?.HasInputAuthority ?? false)}");
        Debug.Log($"Has State Authority: {(Object?.HasStateAuthority ?? false)}");
        Debug.Log($"Current Character: {(currentCharacterData != null ? currentCharacterData.characterName : "NONE")}");
        Debug.Log($"Character Instance: {(currentCharacterInstance != null ? "EXISTS" : "NULL")}");
        Debug.Log($"Animation Controller: {(currentAnimController != null ? "EXISTS" : "NULL")}");
        Debug.Log($"Trigger Manager: {(triggerManager != null ? "EXISTS" : "NULL")}");
        Debug.Log($"Menu Controller: {(menuController != null ? "EXISTS" : "NULL")}");
        Debug.Log($"Available Characters: {availableCharacters?.Length ?? 0}");
        Debug.Log($"Character Database: {characterDatabase.Count}");
        Debug.Log($"Is Switching: {isCharacterSwitching}");
        Debug.Log($"Love Triggers Enabled: {enableLoveTriggers}");
        Debug.Log("===================================");
    }

    [ContextMenu("Log Character Database")]
    public void LogCharacterDatabase()
    {
        Debug.Log("=== Character Database ===");
        foreach (var kvp in characterDatabase)
        {
            Debug.Log($"ID: {kvp.Key} -> {kvp.Value.characterName}");
        }
        Debug.Log("=========================");
    }

    #endregion
}