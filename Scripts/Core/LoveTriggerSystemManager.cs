// =============================================================================
// LoveTriggerSystemManager.cs - MASTER SYSTEM CONTROLLER
// =============================================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using LTSystem;
using LTSystem.Network;
using LTSystem.Events;
using Fusion;

/// <summary>
/// Master controller for the entire Love Trigger System
/// Handles initialization, coordination, and high-level management
/// Add this to a persistent GameObject in your scene
/// </summary>
public class LoveTriggerSystemManager : MonoBehaviour
{
    [Header("System Configuration")]
    public bool autoInitializeOnStart = true;
    public bool enableDebugMode = true;
    public bool enableNetworking = true;
    public bool enablePlatformAdaptation = true;

    [Header("Global Settings")]
    public float globalMaxTriggerDistance = 5f;
    public bool globalRequireMutualConsent = true;
    public float globalAnimationSpeedMultiplier = 1f;
    public int globalMaxQueueSize = 10;

    [Header("Database")]
    public LoveTriggerDatabase masterDatabase;
    public bool autoCreateDatabaseIfMissing = true;
    public bool autoAssignDatabaseToManagers = true;

    [Header("Platform Systems")]
    public bool autoCreatePlatformSystems = true;
    public bool autoCreateInputSystems = true;
    public bool autoCreateEventSystem = true;

    [Header("Player Management")]
    public PlayerController[] managedPlayers;
    public bool autoFindPlayers = true;
    public bool autoSetupPlayerComponents = true;

    [Header("Networking")]
    public bool requireNetworkRunner = true;
    public bool autoCreateNetworkRunner = false;
    public Fusion.GameMode preferredGameMode = Fusion.GameMode.Shared;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent OnSystemInitialized;
    public UnityEngine.Events.UnityEvent OnSystemShutdown;
    public UnityEngine.Events.UnityEvent OnPlayerConnected;
    public UnityEngine.Events.UnityEvent OnPlayerDisconnected;

    [Header("UI Systems")]
    public GameObject interactableObjectUIPrefab;   // world-space menu
    public GameObject characterSelectionUIPrefab;  // screen-space picker


    // System State
    public bool IsInitialized { get; private set; } = false;
    public bool IsNetworkActive { get; private set; } = false;
    public int ConnectedPlayerCount => managedPlayers?.Length ?? 0;

    // Singleton
    public static LoveTriggerSystemManager Instance { get; private set; }

    // Internal References
    private PlatformDetectionSystem platformSystem;
    private UniversalInputSystem inputSystem;
    private NetworkRunner networkRunner;
    private List<NetworkedLoveTriggerManager> allTriggerManagers = new List<NetworkedLoveTriggerManager>();

    // Statistics
    private int totalTriggersExecuted = 0;
    private float systemUptime = 0f;
    private Dictionary<string, int> triggerUsageStats = new Dictionary<string, int>();

    void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (autoInitializeOnStart)
        {
            StartCoroutine(InitializeSystemCoroutine());
        }
    }

    void Update()
    {
        if (IsInitialized)
        {
            systemUptime += Time.deltaTime;
            UpdateSystemStatus();
        }
    }

    [ContextMenu("Initialize System")]
    public void InitializeSystem()
    {
        if (IsInitialized)
        {
            Debug.LogWarning("[LTSystemManager] System already initialized!");
            return;
        }

        StartCoroutine(InitializeSystemCoroutine());
    }

    IEnumerator InitializeSystemCoroutine()
    {
        Debug.Log("[LTSystemManager] Starting Love Trigger System initialization...");

        // Step 1: Setup platform systems
        yield return StartCoroutine(SetupPlatformSystems());

        // Step 2: Setup core database
        yield return StartCoroutine(SetupDatabase());

        // Step 3: Setup players
        yield return StartCoroutine(SetupPlayers());

        // Step 4: Setup networking (if enabled)
        if (enableNetworking)
        {
            yield return StartCoroutine(SetupNetworking());
        }
        //////////////////
        if (interactableObjectUIPrefab != null)
        {
            Instantiate(interactableObjectUIPrefab, transform);
        }

        // Spawn your character-selection UI if desired
        if (characterSelectionUIPrefab != null)
        {
            Instantiate(characterSelectionUIPrefab, transform);
        }

        // fire off any start callbacks
        OnSystemInitialized?.Invoke();

        yield break;

        // Step 5: Final validation
        yield return StartCoroutine(ValidateSystem());

        // Complete initialization
        IsInitialized = true;
        OnSystemInitialized?.Invoke();

        Debug.Log("[LTSystemManager] ✅ Love Trigger System initialization complete!");
        LogSystemStatus();
    }

    IEnumerator SetupPlatformSystems()
    {
        Debug.Log("[LTSystemManager] Setting up platform systems...");

        // Platform Detection
        if (autoCreatePlatformSystems)
        {
            platformSystem = PlatformDetectionSystem.Instance;
            if (platformSystem == null)
            {
                var platformGO = new GameObject("PlatformDetectionSystem");
                platformGO.transform.SetParent(transform);
                platformSystem = platformGO.AddComponent<PlatformDetectionSystem>();
                Debug.Log("[LTSystemManager] Created PlatformDetectionSystem");
            }
        }

        // Input System
        if (autoCreateInputSystems)
        {
            inputSystem = UniversalInputSystem.Instance;
            if (inputSystem == null)
            {
                var inputGO = new GameObject("UniversalInputSystem");
                inputGO.transform.SetParent(transform);
                inputSystem = inputGO.AddComponent<UniversalInputSystem>();
                Debug.Log("[LTSystemManager] Created UniversalInputSystem");
            }
        }

        // Event System
        if (autoCreateEventSystem)
        {
            var eventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                var eventGO = new GameObject("EventSystem");
                eventGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("[LTSystemManager] Created EventSystem");
            }
        }

        yield return new WaitForSeconds(0.1f); // Allow systems to initialize
    }

    IEnumerator SetupDatabase()
    {
        Debug.Log("[LTSystemManager] Setting up database...");

        // Find or create master database
        if (masterDatabase == null && autoCreateDatabaseIfMissing)
        {
            masterDatabase = CreateMasterDatabase();
        }

        if (masterDatabase != null)
        {
            masterDatabase.Initialize();
            Debug.Log($"[LTSystemManager] Database initialized with {masterDatabase.triggers?.Length ?? 0} triggers");
        }
        else
        {
            Debug.LogError("[LTSystemManager] No master database available!");
        }

        yield return null;
    }

    IEnumerator SetupPlayers()
    {
        Debug.Log("[LTSystemManager] Setting up players...");

        // Find players
        if (autoFindPlayers)
        {
            var foundPlayers = FindObjectsOfType<PlayerController>();
            if (foundPlayers.Length > 0)
            {
                managedPlayers = foundPlayers;
                Debug.Log($"[LTSystemManager] Found {managedPlayers.Length} players");
            }
        }

        // Setup each player
        if (managedPlayers != null)
        {
            foreach (var player in managedPlayers)
            {
                if (player != null)
                {
                    yield return StartCoroutine(SetupPlayer(player));
                }
            }
        }

        yield return null;
    }

    IEnumerator SetupPlayer(PlayerController player)
    {
        Debug.Log($"[LTSystemManager] Setting up player: {player.name}");

        if (autoSetupPlayerComponents)
        {
            // Ensure required components
            var networkObject = player.GetComponent<NetworkObject>();
            if (networkObject == null && enableNetworking)
            {
                networkObject = player.gameObject.AddComponent<NetworkObject>();
            }

            var triggerManager = player.GetComponent<NetworkedLoveTriggerManager>();
            if (triggerManager == null)
            {
                triggerManager = player.gameObject.AddComponent<NetworkedLoveTriggerManager>();
            }

            var menuController = player.GetComponent<UniversalLTMenuController>();
            if (menuController == null)
            {
                menuController = player.gameObject.AddComponent<UniversalLTMenuController>();
            }

            // Configure components
            ConfigurePlayerComponents(player, triggerManager, menuController);
        }

        yield return null;
    }

    void ConfigurePlayerComponents(PlayerController player, NetworkedLoveTriggerManager triggerManager, UniversalLTMenuController menuController)
    {
        // Configure trigger manager
        if (triggerManager != null)
        {
            triggerManager.MaxTriggerDistance = globalMaxTriggerDistance;
            triggerManager.RequireMutualConsent = globalRequireMutualConsent;
            triggerManager.DebugMode = enableDebugMode;

            if (masterDatabase != null && autoAssignDatabaseToManagers)
            {
                triggerManager.Database = masterDatabase;
            }

            // Subscribe to events
            triggerManager.OnTriggerComplete += OnTriggerExecuted;
            allTriggerManagers.Add(triggerManager);
        }

        // Configure menu controller
        if (menuController != null)
        {
            menuController.debugMode = enableDebugMode;
            menuController.npcDetectionRadius = globalMaxTriggerDistance;
            menuController.autoConfigureForPlatform = enablePlatformAdaptation;
        }

        // Configure player controller
        player.debugMode = enableDebugMode;
        player.maxTriggerDistance = globalMaxTriggerDistance;
        player.requireMutualConsent = globalRequireMutualConsent;
        player.autoDetectPlatform = enablePlatformAdaptation;
    }

    IEnumerator SetupNetworking()
    {
        Debug.Log("[LTSystemManager] Setting up networking...");

        // Find or create network runner
        networkRunner = FindObjectOfType<NetworkRunner>();

        if (networkRunner == null && autoCreateNetworkRunner)
        {
            var runnerGO = new GameObject("NetworkRunner");
            runnerGO.transform.SetParent(transform);
            networkRunner = runnerGO.AddComponent<NetworkRunner>();
            Debug.Log("[LTSystemManager] Created NetworkRunner");
        }

        if (networkRunner != null)
        {
            // Configure network runner
            // Note: Actual networking setup would depend on your specific Fusion configuration
            IsNetworkActive = true;
            Debug.Log("[LTSystemManager] Network runner configured");
        }
        else if (requireNetworkRunner)
        {
            Debug.LogError("[LTSystemManager] NetworkRunner required but not found!");
        }

        yield return null;
    }

    IEnumerator ValidateSystem()
    {
        Debug.Log("[LTSystemManager] Validating system...");

        // Check critical components
        bool isValid = true;

        if (platformSystem == null && enablePlatformAdaptation)
        {
            Debug.LogWarning("[LTSystemManager] Platform system not found");
            isValid = false;
        }

        if (inputSystem == null)
        {
            Debug.LogWarning("[LTSystemManager] Input system not found");
            isValid = false;
        }

        if (masterDatabase == null)
        {
            Debug.LogError("[LTSystemManager] No master database assigned");
            isValid = false;
        }

        if (managedPlayers == null || managedPlayers.Length == 0)
        {
            Debug.LogWarning("[LTSystemManager] No players configured");
        }

        if (isValid)
        {
            Debug.Log("[LTSystemManager] ✅ System validation passed");
        }
        else
        {
            Debug.LogWarning("[LTSystemManager] ⚠️ System validation found issues");
        }

        yield return null;
    }

    LoveTriggerDatabase CreateMasterDatabase()
    {
        Debug.Log("[LTSystemManager] Creating master database...");

        // Find all available triggers
        var allTriggers = new List<LoveTriggerSO>();

#if UNITY_EDITOR
        string[] triggerGUIDs = UnityEditor.AssetDatabase.FindAssets("t:LoveTriggerSO");
        foreach (string guid in triggerGUIDs)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var trigger = UnityEditor.AssetDatabase.LoadAssetAtPath<LoveTriggerSO>(path);
            if (trigger != null)
            {
                allTriggers.Add(trigger);
            }
        }
#endif

        // Create database
        var database = ScriptableObject.CreateInstance<LoveTriggerDatabase>();
        database.name = "AutoGeneratedMasterDatabase";
        database.triggers = allTriggers.ToArray();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            string dbPath = "Assets/AutoGeneratedMasterDatabase.asset";
            UnityEditor.AssetDatabase.CreateAsset(database, dbPath);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"[LTSystemManager] Saved database to {dbPath}");
        }
#endif

        Debug.Log($"[LTSystemManager] Created database with {allTriggers.Count} triggers");
        return database;
    }

    void UpdateSystemStatus()
    {
        // Update managed players list
        if (autoFindPlayers && Time.frameCount % 60 == 0) // Check every 60 frames
        {
            var foundPlayers = FindObjectsOfType<PlayerController>();
            if (foundPlayers.Length != managedPlayers?.Length)
            {
                var oldCount = managedPlayers?.Length ?? 0;
                managedPlayers = foundPlayers;
                Debug.Log($"[LTSystemManager] Player count changed: {oldCount} -> {managedPlayers.Length}");
            }
        }
    }

    void OnTriggerExecuted(LoveTriggerRequest request)
    {
        totalTriggersExecuted++;

        if (request.Trigger != null)
        {
            string triggerID = request.Trigger.triggerID;
            if (!triggerUsageStats.ContainsKey(triggerID))
            {
                triggerUsageStats[triggerID] = 0;
            }
            triggerUsageStats[triggerID]++;
        }

        if (enableDebugMode)
        {
            Debug.Log($"[LTSystemManager] Trigger executed: {request.GetDisplayName()}");
        }
    }

    // Public API
    public void ShutdownSystem()
    {
        Debug.Log("[LTSystemManager] Shutting down Love Trigger System...");

        // Cleanup event subscriptions
        foreach (var manager in allTriggerManagers)
        {
            if (manager != null)
            {
                manager.OnTriggerComplete -= OnTriggerExecuted;
            }
        }

        IsInitialized = false;
        IsNetworkActive = false;
        OnSystemShutdown?.Invoke();

        Debug.Log("[LTSystemManager] System shutdown complete");
    }

    public void RestartSystem()
    {
        if (IsInitialized)
        {
            ShutdownSystem();
        }

        StartCoroutine(InitializeSystemCoroutine());
    }

    public void UpdateGlobalSettings(float maxDistance, bool requireConsent, bool debugMode)
    {
        globalMaxTriggerDistance = maxDistance;
        globalRequireMutualConsent = requireConsent;
        enableDebugMode = debugMode;

        // Apply to all managed components
        foreach (var manager in allTriggerManagers)
        {
            if (manager != null)
            {
                manager.MaxTriggerDistance = maxDistance;
                manager.RequireMutualConsent = requireConsent;
                manager.DebugMode = debugMode;
            }
        }

        Debug.Log($"[LTSystemManager] Updated global settings: Distance={maxDistance}, Consent={requireConsent}, Debug={debugMode}");
    }

    public void AddPlayer(PlayerController player)
    {
        if (player == null) return;

        var playerList = new List<PlayerController>(managedPlayers ?? new PlayerController[0]);
        if (!playerList.Contains(player))
        {
            playerList.Add(player);
            managedPlayers = playerList.ToArray();

            if (IsInitialized)
            {
                StartCoroutine(SetupPlayer(player));
            }

            OnPlayerConnected?.Invoke();
            Debug.Log($"[LTSystemManager] Added player: {player.name}");
        }
    }

    public void RemovePlayer(PlayerController player)
    {
        if (player == null || managedPlayers == null) return;

        var playerList = new List<PlayerController>(managedPlayers);
        if (playerList.Remove(player))
        {
            managedPlayers = playerList.ToArray();
            OnPlayerDisconnected?.Invoke();
            Debug.Log($"[LTSystemManager] Removed player: {player.name}");
        }
    }

    // Debug and info methods
    [ContextMenu("Log System Status")]
    public void LogSystemStatus()
    {
        Debug.Log("=== LOVE TRIGGER SYSTEM STATUS ===");
        Debug.Log($"Initialized: {IsInitialized}");
        Debug.Log($"Network Active: {IsNetworkActive}");
        Debug.Log($"Uptime: {systemUptime:F1}s");
        Debug.Log($"Managed Players: {ConnectedPlayerCount}");
        Debug.Log($"Total Triggers Executed: {totalTriggersExecuted}");
        Debug.Log($"Database: {(masterDatabase != null ? $"{masterDatabase.triggers?.Length ?? 0} triggers" : "None")}");
        Debug.Log($"Platform: {(platformSystem?.CurrentPlatform.ToString() ?? "Unknown")}");
        Debug.Log("===================================");
    }

    [ContextMenu("Log Trigger Usage Stats")]
    public void LogTriggerUsageStats()
    {
        Debug.Log("=== TRIGGER USAGE STATISTICS ===");
        if (triggerUsageStats.Count == 0)
        {
            Debug.Log("No triggers executed yet");
        }
        else
        {
            foreach (var kvp in triggerUsageStats)
            {
                Debug.Log($"{kvp.Key}: {kvp.Value} times");
            }
        }
        Debug.Log("================================");
    }

    [ContextMenu("Force System Validation")]
    public void ForceSystemValidation()
    {
        StartCoroutine(ValidateSystem());
    }

    [ContextMenu("Emergency Reset")]
    public void EmergencyReset()
    {
        Debug.LogWarning("[LTSystemManager] Performing emergency reset...");

        ShutdownSystem();

        // Clear all references
        allTriggerManagers.Clear();
        managedPlayers = null;
        totalTriggersExecuted = 0;
        systemUptime = 0f;
        triggerUsageStats.Clear();

        // Restart after a short delay
        StartCoroutine(DelayedRestart());
    }

    IEnumerator DelayedRestart()
    {
        yield return new WaitForSeconds(1f);
        InitializeSystem();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        ShutdownSystem();
    }

    // Gizmos for debugging
    void OnDrawGizmosSelected()
    {
        if (managedPlayers != null)
        {
            Gizmos.color = Color.cyan;
            foreach (var player in managedPlayers)
            {
                if (player != null)
                {
                    Gizmos.DrawWireSphere(player.transform.position, globalMaxTriggerDistance);
                }
            }
        }
    }
}