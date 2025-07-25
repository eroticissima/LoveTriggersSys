using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;
using LTSystem;
using LTSystem.Events;

namespace LTSystem.Objects
{
    /// <summary>
    /// Makes any GameObject an interactable love trigger host.
    /// Attach this to chairs, beds, or any interactive object.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class InteractableObject : MonoBehaviour
    {
        [Header("Object Identity")]
        [SerializeField] private string objectID = "";
        [SerializeField] public string objectName = "Interactable Object";
        [SerializeField] private Sprite objectIcon;

        [Header("Available Love Triggers")]
        [Tooltip("Love triggers specific to this object")]
        [SerializeField] private LoveTriggerSO[] objectTriggers;

        [Header("Interaction Settings")]
        [SerializeField] public float interactionRadius = 2f;
        [SerializeField] private bool requireLineOfSight = true;
        [SerializeField] private LayerMask lineOfSightMask = -1;
        [SerializeField] private Transform interactionPoint;

        [Header("Visual Feedback")]
        [SerializeField] private bool showInteractionGizmos = true;
        [SerializeField] private Color gizmoColor = Color.cyan;
        [SerializeField] private GameObject highlightEffect;
        [SerializeField] private Material highlightMaterial;

        [Header("UI Positioning")]
        [SerializeField] private Transform uiAnchorPoint;
        [SerializeField] private Vector3 uiOffset = Vector3.up * 2f;
        [SerializeField] private bool faceCamera = true;

        [Header("NPC Fallback")]
        [SerializeField] public bool allowNPCPartners = true;
        [SerializeField] private GameObject npcPartnerPrefab;
        [SerializeField] private Transform[] npcSpawnPoints;
        [SerializeField] private float npcSpawnDelay = 0.5f;

        [Header("Timeline Settings")]
        [SerializeField] private Transform[] cameraAnchors;
        [SerializeField] private bool lockPlayerDuringTimeline = true;
        [SerializeField] private float timelineTransitionDuration = 1f;

        [Header("Events")]
        public UnityEvent<PlayerController> OnPlayerEnterRange;
        public UnityEvent<PlayerController> OnPlayerExitRange;
        public UnityEvent<LoveTriggerRequest> OnTriggerExecuted;
        public UnityEvent OnInteractionHighlight;
        public UnityEvent OnInteractionUnhighlight;

        // Runtime state
        private List<PlayerController> playersInRange = new List<PlayerController>();
        private PlayerController currentInteractingPlayer;
        private GameObject spawnedNPC;
        private bool isHighlighted = false;
        private bool isExecutingTrigger = false;
        private Renderer objectRenderer;
        private Material originalMaterial;
        private Collider interactionCollider;

        // Cached references
        private static InteractableObjectUI uiPrefab;
        private InteractableObjectUI activeUI;

        #region Initialization

        void Awake()
        {
            InitializeComponents();
            ValidateConfiguration();
        }

        void InitializeComponents()
        {
            // Auto-generate ID if empty
            if (string.IsNullOrEmpty(objectID))
            {
                objectID = $"{gameObject.name}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
            }

            // Setup interaction point
            if (interactionPoint == null)
            {
                interactionPoint = transform;
            }

            // Setup UI anchor
            if (uiAnchorPoint == null)
            {
                uiAnchorPoint = transform;
            }

            // Get renderer for highlighting
            objectRenderer = GetComponent<Renderer>();
            if (objectRenderer != null && highlightMaterial != null)
            {
                originalMaterial = objectRenderer.material;
            }

            // Ensure trigger collider
            interactionCollider = GetComponent<Collider>();
            if (interactionCollider != null)
            {
                interactionCollider.isTrigger = true;
            }

            // Create interaction sphere if needed
            SetupInteractionZone();
        }

        void SetupInteractionZone()
        {
            // Create a child object with sphere trigger for interaction detection
            GameObject interactionZone = new GameObject($"{gameObject.name}_InteractionZone");
            interactionZone.transform.SetParent(transform);
            interactionZone.transform.localPosition = Vector3.zero;
            interactionZone.layer = gameObject.layer;

            SphereCollider sphereCollider = interactionZone.AddComponent<SphereCollider>();
            sphereCollider.radius = interactionRadius;
            sphereCollider.isTrigger = true;

            // Add detection component
            var detector = interactionZone.AddComponent<InteractionDetector>();
            detector.parentObject = this;
        }

        void ValidateConfiguration()
        {
            if (objectTriggers == null || objectTriggers.Length == 0)
            {
                Debug.LogWarning($"[InteractableObject] {gameObject.name} has no love triggers assigned!");
            }

            // Validate triggers
            foreach (var trigger in objectTriggers)
            {
                if (trigger == null) continue;

                if (trigger.useTimeline && trigger.cinematicTimeline == null)
                {
                    Debug.LogWarning($"[InteractableObject] Trigger '{trigger.triggerName}' has useTimeline enabled but no timeline assigned!");
                }
            }
        }

        #endregion

        #region Player Detection

        public void OnPlayerDetected(PlayerController player)
        {
            if (player == null || playersInRange.Contains(player)) return;

            // Check line of sight if required
            if (requireLineOfSight && !HasLineOfSight(player.transform))
            {
                return;
            }

            playersInRange.Add(player);
            OnPlayerEnterRange?.Invoke(player);

            // Show UI for this player
            if (player.HasInputAuthority())
            {
                ShowInteractionUI(player);
            }

            // Highlight object
            if (!isHighlighted && playersInRange.Count > 0)
            {
                HighlightObject(true);
            }

            Debug.Log($"[InteractableObject] Player {player.name} entered range of {objectName}");
        }

        public void OnPlayerLost(PlayerController player)
        {
            if (player == null || !playersInRange.Contains(player)) return;

            playersInRange.Remove(player);
            OnPlayerExitRange?.Invoke(player);

            // Hide UI for this player
            if (player.HasInputAuthority())
            {
                HideInteractionUI();
            }

            // Remove highlight if no players nearby
            if (playersInRange.Count == 0)
            {
                HighlightObject(false);
            }

            Debug.Log($"[InteractableObject] Player {player.name} left range of {objectName}");
        }

        bool HasLineOfSight(Transform target)
        {
            Vector3 direction = target.position - interactionPoint.position;
            float distance = direction.magnitude;

            if (Physics.Raycast(interactionPoint.position, direction.normalized, out RaycastHit hit, distance, lineOfSightMask))
            {
                return hit.transform == target || hit.transform.IsChildOf(target);
            }

            return true;
        }

        #endregion

        #region UI Management

        void ShowInteractionUI(PlayerController player)
        {
            if (activeUI != null)
            {
                HideInteractionUI();
            }

            // Load UI prefab if needed
            if (uiPrefab == null)
            {
                uiPrefab = Resources.Load<InteractableObjectUI>("UI/InteractableObjectUI");
            }

            if (uiPrefab != null)
            {
                activeUI = Instantiate(uiPrefab);
                activeUI.Setup(this, player, objectTriggers);
                activeUI.transform.position = uiAnchorPoint.position + uiOffset;

                if (faceCamera && Camera.main != null)
                {
                    activeUI.StartFacingCamera(Camera.main);
                }
            }
        }

        void HideInteractionUI()
        {
            if (activeUI != null)
            {
                Destroy(activeUI.gameObject);
                activeUI = null;
            }
        }

        #endregion

        #region Trigger Execution

        public void ExecuteTrigger(string triggerID, PlayerController player)
        {
            if (isExecutingTrigger)
            {
                Debug.LogWarning($"[InteractableObject] Already executing a trigger on {objectName}");
                return;
            }

            var trigger = GetTriggerByID(triggerID);
            if (trigger == null)
            {
                Debug.LogError($"[InteractableObject] Trigger '{triggerID}' not found on {objectName}");
                return;
            }

            currentInteractingPlayer = player;
            isExecutingTrigger = true;

            StartCoroutine(ExecuteTriggerCoroutine(trigger, player));
        }

        System.Collections.IEnumerator ExecuteTriggerCoroutine(LoveTriggerSO trigger, PlayerController player)
        {
            Debug.Log($"[InteractableObject] Executing trigger '{trigger.triggerName}' on {objectName}");

            // Hide UI during execution
            HideInteractionUI();

            // Determine if we need a partner
            GameObject partner = null;
            if (trigger.animationType == AnimationType.Partner || trigger.animationType == AnimationType.Synchronized)
            {
                partner = FindOrCreatePartner(player);
                yield return new WaitForSeconds(npcSpawnDelay);
            }

            // Position player at interaction point
            yield return PositionPlayerForInteraction(player, trigger);

            // Execute based on type
            if (trigger.useTimeline && trigger.cinematicTimeline != null)
            {
                yield return ExecuteTimelineTrigger(trigger, player.gameObject, partner);
            }
            else
            {
                yield return ExecuteAnimationTrigger(trigger, player, partner);
            }

            // Cleanup
            if (spawnedNPC != null)
            {
                Destroy(spawnedNPC, 1f);
                spawnedNPC = null;
            }

            // Fire completion event
            var request = new LoveTriggerRequest(trigger, player.gameObject, partner);
            OnTriggerExecuted?.Invoke(request);

            isExecutingTrigger = false;
            currentInteractingPlayer = null;

            // Re-show UI if player still in range
            if (playersInRange.Contains(player) && player.HasInputAuthority())
            {
                ShowInteractionUI(player);
            }
        }

        GameObject FindOrCreatePartner(PlayerController player)
        {
            // First, try to find another player in range
            foreach (var otherPlayer in playersInRange)
            {
                if (otherPlayer != player && otherPlayer != null)
                {
                    return otherPlayer.gameObject;
                }
            }

            // If no other player and NPCs allowed, spawn one
            if (allowNPCPartners && npcPartnerPrefab != null)
            {
                Transform spawnPoint = GetBestNPCSpawnPoint(player.transform);
                spawnedNPC = Instantiate(npcPartnerPrefab, spawnPoint.position, spawnPoint.rotation);

                // Configure NPC
                var npcController = spawnedNPC.GetComponent<NPCPartnerController>();
                if (npcController != null)
                {
                    npcController.Initialize(this, player);
                }

                return spawnedNPC;
            }

            return null;
        }

        Transform GetBestNPCSpawnPoint(Transform playerTransform)
        {
            if (npcSpawnPoints != null && npcSpawnPoints.Length > 0)
            {
                // Find spawn point furthest from player
                Transform bestPoint = npcSpawnPoints[0];
                float maxDistance = 0f;

                foreach (var point in npcSpawnPoints)
                {
                    if (point == null) continue;

                    float distance = Vector3.Distance(point.position, playerTransform.position);
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        bestPoint = point;
                    }
                }

                return bestPoint;
            }

            // Fallback: spawn opposite the player
            Vector3 direction = (transform.position - playerTransform.position).normalized;
            Vector3 spawnPos = transform.position + direction * 2f;

            GameObject tempPoint = new GameObject("TempSpawnPoint");
            tempPoint.transform.position = spawnPos;
            tempPoint.transform.LookAt(transform);

            return tempPoint.transform;
        }

        #endregion

        #region Timeline Execution

        System.Collections.IEnumerator ExecuteTimelineTrigger(LoveTriggerSO trigger, GameObject player, GameObject partner)
        {
            Debug.Log($"[InteractableObject] Executing timeline trigger: {trigger.triggerName}");

            // Create timeline controller
            var timelineController = gameObject.AddComponent<InteractableTimelineController>();

            // Configure timeline bindings
            var bindings = new TimelineBindings
            {
                player = player,
                partner = partner,
                interactableObject = gameObject,
                cameraAnchors = cameraAnchors,
                additionalBindings = GetAdditionalTimelineBindings(trigger)
            };

            // Lock player movement if required
            if (lockPlayerDuringTimeline)
            {
                var playerController = player.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    playerController.SetLoveTriggerEnabled(false);
                }
            }

            // Play timeline
            yield return timelineController.PlayTimeline(trigger.cinematicTimeline, bindings, timelineTransitionDuration);

            // Restore player control
            if (lockPlayerDuringTimeline)
            {
                var playerController = player.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    playerController.SetLoveTriggerEnabled(true);
                }
            }

            // Cleanup
            Destroy(timelineController);
        }

        Dictionary<string, Object> GetAdditionalTimelineBindings(LoveTriggerSO trigger)
        {
            var bindings = new Dictionary<string, Object>();

            // Add any trigger-specific bindings
            // This could be extended to support custom binding configurations

            return bindings;
        }

        #endregion

        #region Animation Execution

        System.Collections.IEnumerator ExecuteAnimationTrigger(LoveTriggerSO trigger, PlayerController player, GameObject partner)
        {
            Debug.Log($"[InteractableObject] Executing animation trigger: {trigger.triggerName}");

            // Use the player's animation controller
            var animController = player.GetCurrentAnimationController();
            if (animController != null)
            {
                animController.PlayAnimation(trigger.singleAnimation);

                // Wait for animation to complete
                yield return new WaitUntil(() => !animController.IsPlaying());
            }
            else
            {
                // Fallback duration
                yield return new WaitForSeconds(trigger.overrideDuration);
            }
        }

        #endregion

        #region Positioning

        System.Collections.IEnumerator PositionPlayerForInteraction(PlayerController player, LoveTriggerSO trigger)
        {
            // Move player to interaction point smoothly
            Transform playerTransform = player.transform;
            Vector3 startPos = playerTransform.position;
            Vector3 targetPos = interactionPoint.position;
            Quaternion startRot = playerTransform.rotation;
            Quaternion targetRot = interactionPoint.rotation;

            float elapsed = 0f;
            float duration = 0.5f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                playerTransform.position = Vector3.Lerp(startPos, targetPos, t);
                playerTransform.rotation = Quaternion.Slerp(startRot, targetRot, t);

                yield return null;
            }

            playerTransform.position = targetPos;
            playerTransform.rotation = targetRot;
        }

        #endregion

        #region Visual Feedback

        void HighlightObject(bool highlight)
        {
            isHighlighted = highlight;

            if (highlight)
            {
                OnInteractionHighlight?.Invoke();

                if (highlightEffect != null)
                {
                    highlightEffect.SetActive(true);
                }

                if (objectRenderer != null && highlightMaterial != null)
                {
                    objectRenderer.material = highlightMaterial;
                }
            }
            else
            {
                OnInteractionUnhighlight?.Invoke();

                if (highlightEffect != null)
                {
                    highlightEffect.SetActive(false);
                }

                if (objectRenderer != null && originalMaterial != null)
                {
                    objectRenderer.material = originalMaterial;
                }
            }
        }

        #endregion

        #region Public API

        public LoveTriggerSO GetTriggerByID(string triggerID)
        {
            if (objectTriggers == null) return null;

            foreach (var trigger in objectTriggers)
            {
                if (trigger != null && trigger.triggerID == triggerID)
                {
                    return trigger;
                }
            }

            return null;
        }

        public LoveTriggerSO[] GetAvailableTriggers()
        {
            return objectTriggers;
        }

        public bool IsPlayerInRange(PlayerController player)
        {
            return playersInRange.Contains(player);
        }

        public bool IsExecutingTrigger()
        {
            return isExecutingTrigger;
        }

        public void ForceStopExecution()
        {
            if (isExecutingTrigger)
            {
                StopAllCoroutines();
                isExecutingTrigger = false;

                if (spawnedNPC != null)
                {
                    Destroy(spawnedNPC);
                    spawnedNPC = null;
                }

                HideInteractionUI();
            }
        }

        #endregion

        #region Editor Support

        void OnDrawGizmosSelected()
        {
            if (!showInteractionGizmos) return;

            // Draw interaction radius
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);

            // Draw interaction point
            if (interactionPoint != null && interactionPoint != transform)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(interactionPoint.position, Vector3.one * 0.2f);
                Gizmos.DrawLine(transform.position, interactionPoint.position);
            }

            // Draw UI anchor point
            if (uiAnchorPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(uiAnchorPoint.position + uiOffset, 0.3f);
            }

            // Draw NPC spawn points
            if (npcSpawnPoints != null)
            {
                Gizmos.color = Color.blue;
                foreach (var point in npcSpawnPoints)
                {
                    if (point != null)
                    {
                        Gizmos.DrawWireCube(point.position, Vector3.one * 0.5f);
                        DrawArrow(point.position, point.forward, 0.5f);
                    }
                }
            }

            // Draw camera anchors
            if (cameraAnchors != null)
            {
                Gizmos.color = Color.magenta;
                foreach (var anchor in cameraAnchors)
                {
                    if (anchor != null)
                    {
                        DrawCamera(anchor.position, anchor.forward, 0.5f);
                    }
                }
            }
        }

        void DrawArrow(Vector3 position, Vector3 direction, float size)
        {
            Gizmos.DrawRay(position, direction * size);
            Gizmos.DrawRay(position + direction * size, Quaternion.Euler(0, 150, 0) * -direction * size * 0.3f);
            Gizmos.DrawRay(position + direction * size, Quaternion.Euler(0, -150, 0) * -direction * size * 0.3f);
        }

        void DrawCamera(Vector3 position, Vector3 forward, float size)
        {
            Vector3[] corners = new Vector3[4];
            float halfSize = size * 0.5f;

            corners[0] = position + forward * size + Vector3.up * halfSize - Vector3.right * halfSize;
            corners[1] = position + forward * size + Vector3.up * halfSize + Vector3.right * halfSize;
            corners[2] = position + forward * size - Vector3.up * halfSize + Vector3.right * halfSize;
            corners[3] = position + forward * size - Vector3.up * halfSize - Vector3.right * halfSize;

            // Draw frustum
            for (int i = 0; i < 4; i++)
            {
                Gizmos.DrawLine(position, corners[i]);
                Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
            }

            // Draw camera body
            Gizmos.DrawWireCube(position, Vector3.one * size * 0.3f);
        }

        [ContextMenu("Validate Object Setup")]
        public void ValidateSetup()
        {
            List<string> issues = new List<string>();

            if (objectTriggers == null || objectTriggers.Length == 0)
            {
                issues.Add("No love triggers assigned");
            }

            if (interactionRadius <= 0)
            {
                issues.Add("Interaction radius must be greater than 0");
            }

            if (GetComponent<Collider>() == null)
            {
                issues.Add("No collider found on object");
            }

            if (allowNPCPartners && npcPartnerPrefab == null)
            {
                issues.Add("NPC partners allowed but no prefab assigned");
            }

            foreach (var trigger in objectTriggers)
            {
                if (trigger == null)
                {
                    issues.Add("Null trigger in array");
                    continue;
                }

                if (trigger.useTimeline && trigger.cinematicTimeline == null)
                {
                    issues.Add($"Trigger '{trigger.triggerName}' uses timeline but none assigned");
                }
            }

            if (issues.Count > 0)
            {
                Debug.LogWarning($"[InteractableObject] Validation issues for {objectName}:\n" + string.Join("\n- ", issues));
            }
            else
            {
                Debug.Log($"[InteractableObject] {objectName} validation passed!");
            }
        }

        #endregion
    }

    /// <summary>
    /// Helper component for sphere trigger detection
    /// </summary>
    public class InteractionDetector : MonoBehaviour
    {
        public InteractableObject parentObject;

        void OnTriggerEnter(Collider other)
        {
            if (parentObject == null) return;

            var player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                parentObject.OnPlayerDetected(player);
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (parentObject == null) return;

            var player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                parentObject.OnPlayerLost(player);
            }
        }
    }
}