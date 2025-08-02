// =============================================================================
// PlayerControllerBootstrap.cs - Initializes Correct Player Based on Platform
// =============================================================================

using UnityEngine;
using Fusion;
using LTSystem.Player;
using LTSystem.Platform;
using LTSystem.Factory;

namespace LTSystem.Bootstrap
{
    /// <summary>
    /// Bootstrap component that ensures the correct player controller is created based on platform
    /// </summary>
    public class PlayerControllerBootstrap : NetworkBehaviour
    {
        [Header("Bootstrap Settings")]
        public bool autoInitializeOnSpawn = true;
        public bool destroyAfterInitialization = true;

        [Header("Factory References")]
        public PlayerFactory playerFactory;
        public EnhancedPlatformDetectionSystem platformDetection;

        [Header("Debug")]
        public bool debugMode = true;

        public override void Spawned()
        {
            if (autoInitializeOnSpawn && Object.HasInputAuthority)
            {
                InitializePlayer();
            }
        }

        public void InitializePlayer()
        {
            // Ensure we have required components
            if (playerFactory == null)
                playerFactory = PlayerFactory.Instance;

            if (platformDetection == null)
                platformDetection = EnhancedPlatformDetectionSystem.Instance;

            if (playerFactory == null || platformDetection == null)
            {
                Debug.LogError("[PlayerControllerBootstrap] Missing required components for player initialization");
                return;
            }

            // Force platform detection
            platformDetection.ForcePlatformDetection();

            // Get the appropriate player type
            var playerType = platformDetection.GetPlayerControllerType();

            if (debugMode)
                Debug.Log($"[PlayerControllerBootstrap] Initializing {playerType} player for {platformDetection.CurrentPlatform}");

            // Create the player
            var player = playerFactory.CreatePlayer(playerType, transform.position, transform.rotation);

            if (player != null)
            {
                // Transfer any existing network authority
                if (Object.HasInputAuthority)
                {
                    // The player object should automatically get input authority
                }

                if (debugMode)
                    Debug.Log($"[PlayerControllerBootstrap] Successfully initialized {playerType} player");

                // Destroy bootstrap if configured to do so
                if (destroyAfterInitialization)
                {
                    if (Object != null)
                        Runner.Despawn(Object);
                    else
                        Destroy(gameObject);
                }
            }
            else
            {
                Debug.LogError("[PlayerControllerBootstrap] Failed to create player!");
            }
        }

        [ContextMenu("Test Player Initialization")]
        public void TestPlayerInitialization()
        {
            if (Application.isPlaying)
            {
                InitializePlayer();
            }
            else
            {
                Debug.LogWarning("[PlayerControllerBootstrap] Test can only be run during play mode");
            }
        }
    }
}