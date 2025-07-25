// ============================================================================
// NetworkedLoveTriggerManager.cs - FIXED FOR UNITY 2023.2.19f1
// ============================================================================

using System;
using System.Collections;
using Fusion;
using UnityEngine;
using LTSystem.Events;

namespace LTSystem.Network
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedLoveTriggerManager : NetworkBehaviour, ILoveTriggerNetworkService
    {
        // ----- FIXED: Properly implemented interface properties -----
        [Header("Configuration")]
        [SerializeField] private float _maxTriggerDistance = 5f;
        [SerializeField] private bool _requireMutualConsent = true;
        [SerializeField] private bool _debugMode = false;
        [SerializeField] private LTSystem.LoveTriggerDatabase _database;

        // Interface implementation with proper get/set
        public float MaxTriggerDistance
        {
            get => _maxTriggerDistance;
            set => _maxTriggerDistance = value;
        }

        public bool RequireMutualConsent
        {
            get => _requireMutualConsent;
            set => _requireMutualConsent = value;
        }

        public bool DebugMode
        {
            get => _debugMode;
            set => _debugMode = value;
        }

        public LTSystem.LoveTriggerDatabase Database
        {
            get => _database;
            set => _database = value;
        }

        // ----- Runtime state -----
        public bool IsProcessing { get; private set; }

        [Header("Components")]
        [SerializeField] private Animator _animator;
        [SerializeField] private UniversalAnimationController _animationController;

        private AnimatorOverrideController _overrideController;

        // ----- Events -----
        public event Action<LoveTriggerRequest> OnTriggerComplete;

        public override void Spawned()
        {
            InitializeComponents();

            if (DebugMode)
                Debug.Log($"[NetworkedLoveTriggerManager] Spawned for {Object.InputAuthority}");
        }

        private void InitializeComponents()
        {
            // Auto-find components if not assigned
            if (_animator == null)
                _animator = GetComponent<Animator>();

            if (_animationController == null)
                _animationController = GetComponent<UniversalAnimationController>();

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _overrideController = new AnimatorOverrideController(_animator.runtimeAnimatorController);
                _animator.runtimeAnimatorController = _overrideController;
            }

            // Initialize database if assigned
            if (_database != null)
            {
                _database.Initialize();
            }
        }

        /// <summary> Called by UI or PlayerController to invoke a trigger. </summary>
        public void RequestLoveTrigger(string triggerID, NetworkObject target)
        {
            if (!Object.HasInputAuthority)
            {
                if (DebugMode) Debug.LogWarning("[NetworkedLoveTriggerManager] No input authority for trigger request");
                return;
            }

            if (IsProcessing)
            {
                if (DebugMode) Debug.Log("[NetworkedLoveTriggerManager] Already processing trigger");
                return;
            }

            if (target == null)
            {
                if (DebugMode) Debug.LogError("[NetworkedLoveTriggerManager] Target is null");
                return;
            }

            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance > MaxTriggerDistance)
            {
                if (DebugMode) Debug.Log($"[NetworkedLoveTriggerManager] Target out of range: {distance:F2} > {MaxTriggerDistance}");
                return;
            }

            // Send request to StateAuthority (server)
            if (Runner != null)
            {
                RPC_ServerRequest(triggerID, Object.InputAuthority, target.InputAuthority);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_ServerRequest(string triggerID, PlayerRef requester, PlayerRef target, RpcInfo info = default)
        {
            if (DebugMode)
                Debug.Log($"[NetworkedLoveTriggerManager] Server received request: {triggerID} from {requester} to {target}");

            // Server-side validation
            if (_database == null)
            {
                Debug.LogError("[NetworkedLoveTriggerManager] Database is null on server");
                return;
            }

            var triggerSO = _database.Get(triggerID);
            if (triggerSO == null)
            {
                Debug.LogError($"[NetworkedLoveTriggerManager] Trigger '{triggerID}' not found in database");
                return;
            }

            // TODO: Add consent logic here for RequireMutualConsent

            // Broadcast execution to target client
            RPC_ClientExecute(triggerID, requester, target);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ClientExecute(string triggerID, PlayerRef requester, PlayerRef target, RpcInfo info = default)
        {
            if (DebugMode)
                Debug.Log($"[NetworkedLoveTriggerManager] Client execute: {triggerID} from {requester} to {target}");

            // Only execute on the client that matches 'target'
            if (Object.HasInputAuthority && Runner.LocalPlayer == target)
            {
                StartCoroutine(PlayTriggerCoroutine(triggerID, requester, target));
            }
        }

        private IEnumerator PlayTriggerCoroutine(string triggerID, PlayerRef requester, PlayerRef target)
        {
            IsProcessing = true;

            if (DebugMode)
                Debug.Log($"[NetworkedLoveTriggerManager] Playing trigger: {triggerID}");

            // Lookup SO
            var triggerSO = _database?.Get(triggerID);
            if (triggerSO == null)
            {
                Debug.LogError($"[NetworkedLoveTriggerManager] Trigger '{triggerID}' not found in database");
                IsProcessing = false;
                yield break;
            }

            // Use UniversalAnimationController if available
            if (_animationController != null)
            {
                yield return StartCoroutine(PlayWithUniversalController(triggerSO));
            }
            else if (_animator != null && _overrideController != null)
            {
                yield return StartCoroutine(PlayWithAnimatorOverride(triggerSO));
            }
            else
            {
                Debug.LogError("[NetworkedLoveTriggerManager] No animation system available");

                // Fallback: wait for trigger duration
                float waitDuration = triggerSO.overrideDuration > 0 ? triggerSO.overrideDuration : 3f;
                yield return new WaitForSeconds(waitDuration);
            }

            IsProcessing = false;

            // Notify listeners
            var request = new LoveTriggerRequest(triggerSO, gameObject, null);
            OnTriggerComplete?.Invoke(request);

            if (DebugMode)
                Debug.Log($"[NetworkedLoveTriggerManager] Completed trigger: {triggerID}");
        }

        private IEnumerator PlayWithUniversalController(LoveTriggerSO triggerSO)
        {
            if (triggerSO.singleAnimation.clip != null)
            {
                _animationController.PlayAnimation(triggerSO.singleAnimation);

                // Wait for animation to complete with timeout
                float timeout = triggerSO.singleAnimation.clip.length + 5f;
                float startTime = Time.time;

                yield return new WaitUntil(() => {
                    bool finished = !_animationController.IsPlaying();
                    bool timedOut = (Time.time - startTime) > timeout;

                    if (timedOut)
                    {
                        Debug.LogWarning($"[NetworkedLoveTriggerManager] Animation timeout, forcing stop");
                        _animationController.ForceStop();
                        return true;
                    }

                    return finished;
                });
            }
            else if (triggerSO.animatorClip != null)
            {
                // Legacy support
                _animationController.PlayAnimation(triggerSO.animatorClip);

                float timeout = triggerSO.animatorClip.length + 5f;
                float startTime = Time.time;

                yield return new WaitUntil(() => {
                    bool finished = !_animationController.IsPlaying();
                    bool timedOut = (Time.time - startTime) > timeout;

                    if (timedOut)
                    {
                        _animationController.ForceStop();
                        return true;
                    }

                    return finished;
                });
            }
            else
            {
                // No animation, just wait
                float waitDuration = triggerSO.overrideDuration > 0 ? triggerSO.overrideDuration : 3f;
                yield return new WaitForSeconds(waitDuration);
            }
        }

        private IEnumerator PlayWithAnimatorOverride(LoveTriggerSO triggerSO)
        {
            AnimationClip clipToPlay = triggerSO.singleAnimation.clip ?? triggerSO.animatorClip;

            if (clipToPlay != null)
            {
                // Override the Action state
                _overrideController["Action"] = clipToPlay;
                _animator.Play("Action");

                float waitDuration = clipToPlay.length;
                yield return new WaitForSeconds(waitDuration);

                // Return to idle
                _animator.Play("Idle");
            }
            else
            {
                float waitDuration = triggerSO.overrideDuration > 0 ? triggerSO.overrideDuration : 3f;
                yield return new WaitForSeconds(waitDuration);
            }
        }

        // Public API for external access
        public void Initialize(LTSystem.LoveTriggerDatabase database, float maxDistance = 5f, bool requireConsent = true, bool debug = false)
        {
            Database = database;
            MaxTriggerDistance = maxDistance;
            RequireMutualConsent = requireConsent;
            DebugMode = debug;

            if (database != null)
                database.Initialize();

            if (DebugMode)
                Debug.Log($"[NetworkedLoveTriggerManager] Initialized with {database?.triggers?.Length ?? 0} triggers");
        }

        private void OnDestroy()
        {
            // Clean up events
            OnTriggerComplete = null;
        }

        // Debug helpers
        [ContextMenu("Test Local Trigger")]
        private void TestLocalTrigger()
        {
            if (_database?.triggers != null && _database.triggers.Length > 0)
            {
                string testTriggerID = _database.triggers[0].triggerID;
                StartCoroutine(PlayTriggerCoroutine(testTriggerID, Runner?.LocalPlayer ?? default, Runner?.LocalPlayer ?? default));
            }
        }

        [ContextMenu("Log Database Info")]
        private void LogDatabaseInfo()
        {
            Debug.Log("=== NetworkedLoveTriggerManager Database Info ===");
            Debug.Log($"Database: {(_database != null ? "EXISTS" : "NULL")}");
            Debug.Log($"Triggers: {_database?.triggers?.Length ?? 0}");
            Debug.Log($"Max Distance: {MaxTriggerDistance}");
            Debug.Log($"Require Consent: {RequireMutualConsent}");
            Debug.Log($"Debug Mode: {DebugMode}");
            Debug.Log($"Is Processing: {IsProcessing}");
            Debug.Log("=============================================");
        }
    }
}