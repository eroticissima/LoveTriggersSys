// =============================================================================
// PlayerSpawnManager.cs - Handles Player Spawning in Network
// =============================================================================

using UnityEngine;
using Fusion;
using LTSystem.Player;
using LTSystem.Factory;
using System.Collections.Generic;

namespace LTSystem.Network
{
    /// <summary>
    /// Manages player spawning and despawning in networked environment
    /// </summary>
    public class PlayerSpawnManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
    {
        [Header("Spawn Settings")]
        public Transform[] spawnPoints;
        public float spawnRadius = 2f;
        public LayerMask obstacleLayerMask = 1;

        [Header("Factory Reference")]
        public PlayerFactory playerFactory;

        [Header("Debug")]
        public bool debugMode = true;

        private Dictionary<PlayerRef, BasePlayerController> spawnedPlayers = new Dictionary<PlayerRef, BasePlayerController>();
        private int lastUsedSpawnIndex = -1;

        void Start()
        {
            if (playerFactory == null)
                playerFactory = PlayerFactory.Instance;

            if (playerFactory == null)
            {
                Debug.LogError("[PlayerSpawnManager] No PlayerFactory found! Player spawning will not work.");
            }
        }

        public void PlayerJoined(PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;

            SpawnPlayer(player);
        }

        public void PlayerLeft(PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;

            DespawnPlayer(player);
        }

        private void SpawnPlayer(PlayerRef player)
        {
            if (playerFactory == null)
            {
                Debug.LogError("[PlayerSpawnManager] Cannot spawn player - no factory available");
                return;
            }

            if (spawnedPlayers.ContainsKey(player))
            {
                Debug.LogWarning($"[PlayerSpawnManager] Player {player} already spawned");
                return;
            }

            Vector3 spawnPosition = GetSpawnPosition();
            Quaternion spawnRotation = GetSpawnRotation();

            var playerController = playerFactory.CreatePlayer(spawnPosition, spawnRotation);

            if (playerController != null)
            {
                spawnedPlayers[player] = playerController;

                if (debugMode)
                    Debug.Log($"[PlayerSpawnManager] Spawned player {player} of type {playerController.GetPlayerType()} at {spawnPosition}");
            }
            else
            {
                Debug.LogError($"[PlayerSpawnManager] Failed to spawn player {player}");
            }
        }

        private void DespawnPlayer(PlayerRef player)
        {
            if (spawnedPlayers.TryGetValue(player, out BasePlayerController playerController))
            {
                if (playerController != null && playerController.Object != null)
                {
                    Runner.Despawn(playerController.Object);
                }

                spawnedPlayers.Remove(player);

                if (debugMode)
                    Debug.Log($"[PlayerSpawnManager] Despawned player {player}");
            }
        }

        private Vector3 GetSpawnPosition()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return Vector3.zero;
            }

            // Find next available spawn point
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                int spawnIndex = (lastUsedSpawnIndex + 1 + i) % spawnPoints.Length;
                Vector3 spawnPos = spawnPoints[spawnIndex].position;

                // Check if spawn point is clear
                if (!Physics.CheckSphere(spawnPos, spawnRadius, obstacleLayerMask))
                {
                    lastUsedSpawnIndex = spawnIndex;
                    return spawnPos;
                }
            }

            // If all spawn points are blocked, use the first one anyway
            lastUsedSpawnIndex = 0;
            return spawnPoints[0].position;
        }

        private Quaternion GetSpawnRotation()
        {
            if (spawnPoints != null && spawnPoints.Length > lastUsedSpawnIndex && lastUsedSpawnIndex >= 0)
            {
                return spawnPoints[lastUsedSpawnIndex].rotation;
            }

            return Quaternion.identity;
        }

        [ContextMenu("Test Spawn Local Player")]
        public void TestSpawnLocalPlayer()
        {
            if (Application.isPlaying && playerFactory != null)
            {
                Vector3 testPos = GetSpawnPosition();
                Quaternion testRot = GetSpawnRotation();

                var testPlayer = playerFactory.CreatePlayer(testPos, testRot);
                Debug.Log($"[PlayerSpawnManager] Test spawn result: {(testPlayer != null ? testPlayer.GetPlayerType().ToString() : "FAILED")}");
            }
        }

        void OnDrawGizmosSelected()
        {
            if (spawnPoints != null)
            {
                Gizmos.color = Color.green;
                foreach (var spawnPoint in spawnPoints)
                {
                    if (spawnPoint != null)
                    {
                        Gizmos.DrawWireSphere(spawnPoint.position, spawnRadius);
                        Gizmos.DrawRay(spawnPoint.position, spawnPoint.forward * 2f);
                    }
                }
            }
        }
    }
}