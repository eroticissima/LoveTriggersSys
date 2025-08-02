// =============================================================================
// PlayerFactory.cs - Handles Platform-Specific Player Instantiation
// =============================================================================

using UnityEngine;
using Fusion;
using LTSystem.Player;

namespace LTSystem.Factory
{
    /// <summary>
    /// Factory for creating platform-specific player controllers
    /// </summary>
    public class PlayerFactory : NetworkBehaviour
    {
        [Header("Player Prefabs")]
        public GameObject pcPlayerPrefab;    // PC_player.prefab
        public GameObject xrPlayerPrefab;    // XR_player.prefab
        public GameObject consolePlayerPrefab;

        [Header("Default Configuration")]
        public PlayerConfiguration defaultConfiguration;

        [Header("Debug")]
        public bool debugMode = true;

        private static PlayerFactory instance;
        public static PlayerFactory Instance
        {
            get
            {
                if (instance == null)
                    instance = FindObjectOfType<PlayerFactory>();
                return instance;
            }
        }

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Creates a player based on detected platform
        /// </summary>
        public BasePlayerController CreatePlayer(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            var detectedPlatform = DetectCurrentPlatform();
            return CreatePlayer(detectedPlatform, spawnPosition, spawnRotation);
        }

        /// <summary>
        /// Creates a player of specific type
        /// </summary>
        public BasePlayerController CreatePlayer(BasePlayerController.PlayerType playerType, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            GameObject prefabToSpawn = GetPrefabForPlayerType(playerType);

            if (prefabToSpawn == null)
            {
                Debug.LogError($"[PlayerFactory] No prefab found for player type: {playerType}");
                return null;
            }

            // Spawn the player through Fusion's network system
            var playerObject = Runner.Spawn(prefabToSpawn, spawnPosition, spawnRotation, Object.InputAuthority);

            if (playerObject == null)
            {
                Debug.LogError($"[PlayerFactory] Failed to spawn player of type: {playerType}");
                return null;
            }

            var playerController = playerObject.GetComponent<BasePlayerController>();
            if (playerController == null)
            {
                Debug.LogError($"[PlayerFactory] Spawned object doesn't have BasePlayerController component: {playerType}");
                return null;
            }

            // Apply default configuration
            if (defaultConfiguration != null)
            {
                ApplyConfiguration(playerController, defaultConfiguration);
            }

            if (debugMode)
                Debug.Log($"[PlayerFactory] Successfully created player: {playerType} at {spawnPosition}");

            return playerController;
        }

        private GameObject GetPrefabForPlayerType(BasePlayerController.PlayerType playerType)
        {
            switch (playerType)
            {
                case BasePlayerController.PlayerType.PC_ThirdPerson:
                    return pcPlayerPrefab;

                case BasePlayerController.PlayerType.XR_VRIK:
                    return xrPlayerPrefab;

                case BasePlayerController.PlayerType.Console:
                    return consolePlayerPrefab;

                default:
                    Debug.LogWarning($"[PlayerFactory] Unknown player type: {playerType}, defaulting to PC");
                    return pcPlayerPrefab;
            }
        }

        private BasePlayerController.PlayerType DetectCurrentPlatform()
        {
            // Check for VR first
            if (IsVREnabled())
            {
                if (debugMode)
                    Debug.Log("[PlayerFactory] VR detected, creating XR player");
                return BasePlayerController.PlayerType.XR_VRIK;
            }

            // Check for console
            if (IsConsoleEnabled())
            {
                if (debugMode)
                    Debug.Log("[PlayerFactory] Console detected, creating console player");
                return BasePlayerController.PlayerType.Console;
            }

            // Default to PC
            if (debugMode)
                Debug.Log("[PlayerFactory] PC detected, creating PC third person player");
            return BasePlayerController.PlayerType.PC_ThirdPerson;
        }

        private bool IsVREnabled()
        {
#if UNITY_XR_MANAGEMENT
            var xrSettings = UnityEngine.XR.XRGeneralSettings.Instance;
            if (xrSettings != null && xrSettings.Manager != null && xrSettings.Manager.activeLoader != null)
            {
                return true;
            }
#endif
            return false;
        }

        private bool IsConsoleEnabled()
        {
            return Application.platform == RuntimePlatform.PS4 ||
                   Application.platform == RuntimePlatform.PS5 ||
                   Application.platform == RuntimePlatform.XboxOne ||
                   Application.platform == RuntimePlatform.GameCoreXboxOne ||
                   Application.platform == RuntimePlatform.GameCoreXboxSeries;
        }

        private void ApplyConfiguration(BasePlayerController playerController, PlayerConfiguration config)
        {
            if (playerController == null || config == null) return;

            playerController.enableLoveTriggers = config.enableLoveTriggers;
            playerController.maxTriggerDistance = config.maxTriggerDistance;
            playerController.requireMutualConsent = config.requireMutualConsent;
            playerController.debugMode = config.debugMode;

            if (config.availableCharacters != null && config.availableCharacters.Length > 0)
            {
                playerController.availableCharacters = config.availableCharacters;

                if (config.defaultCharacter != null)
                {
                    playerController.startingCharacter = config.defaultCharacter;
                }
            }

            if (debugMode)
                Debug.Log($"[PlayerFactory] Applied configuration to player: {playerController.GetPlayerType()}");
        }

        [ContextMenu("Test Platform Detection")]
        public void TestPlatformDetection()
        {
            var detected = DetectCurrentPlatform();
            Debug.Log($"[PlayerFactory] Platform Detection Test Result: {detected}");
            Debug.Log($"- VR Available: {IsVREnabled()}");
            Debug.Log($"- Console: {IsConsoleEnabled()}");
            Debug.Log($"- Current Platform: {Application.platform}");
        }
    }
}