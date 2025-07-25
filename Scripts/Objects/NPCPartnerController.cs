using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

namespace LTSystem.Objects
{
    /// <summary>
    /// Controls NPC partners that are spawned when a player is alone
    /// and tries to execute a partner-based love trigger.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class NPCPartnerController : MonoBehaviour
    {
        [Header("NPC Configuration")]
        [SerializeField] private string npcName = "Partner";
        [SerializeField] private NPCGender gender = NPCGender.Neutral;
        [SerializeField] private NPCPersonality personality = NPCPersonality.Friendly;

        [Header("Movement")]
        [SerializeField] private bool useNavMeshAgent = true;
        [SerializeField] private float walkSpeed = 2f;
        [SerializeField] private float runSpeed = 5f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float stoppingDistance = 0.5f;

        [Header("Animation")]
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string walkStateName = "Walk";
        [SerializeField] private string runStateName = "Run";
        [SerializeField] private RuntimeAnimatorController defaultAnimatorController;

        [Header("Interaction")]
        [SerializeField] private float interactionRadius = 2f;
        [SerializeField] private Transform interactionPoint;
        [SerializeField] private bool autoFacePlayer = true;
        [SerializeField] private float facingSpeed = 5f;

        [Header("Behavior")]
        [SerializeField] private NPCBehaviorMode behaviorMode = NPCBehaviorMode.Responsive;
        [SerializeField] private float reactionDelay = 0.5f;
        [SerializeField] private bool canInitiateTriggers = false;
        [SerializeField] private float initiativeChance = 0.3f;

        [Header("Visual")]
        [SerializeField] private GameObject[] appearanceVariations;
        [SerializeField] private Material[] skinMaterials;
        [SerializeField] private Material[] clothingMaterials;
        [SerializeField] private bool randomizeAppearance = true;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] greetingSounds;
        [SerializeField] private AudioClip[] agreementSounds;
        [SerializeField] private AudioClip[] goodbyeSounds;

        // Components
        private Animator animator;
        private NavMeshAgent navAgent;
        private Rigidbody rb;
        private CapsuleCollider capsuleCollider;
        private AnimatorOverrideController overrideController;

        // State
        private InteractableObject currentInteractable;
        private PlayerController targetPlayer;
        private bool isMoving = false;
        private bool isInteracting = false;
        private bool isExecutingTrigger = false;
        private Vector3 targetPosition;
        private Quaternion targetRotation;

        // Initialization info
        private Vector3 spawnPosition;
        private bool isInitialized = false;

        public enum NPCGender
        {
            Male,
            Female,
            Neutral
        }

        public enum NPCPersonality
        {
            Friendly,
            Shy,
            Playful,
            Serious,
            Romantic
        }

        public enum NPCBehaviorMode
        {
            Passive,      // Only responds to player actions
            Responsive,   // Responds with appropriate reactions
            Active        // Can initiate some interactions
        }

        #region Initialization

        void Awake()
        {
            GetComponents();
            SetupAnimator();
        }

        void GetComponents()
        {
            animator = GetComponent<Animator>();
            navAgent = GetComponent<NavMeshAgent>();
            rb = GetComponent<Rigidbody>();
            capsuleCollider = GetComponent<CapsuleCollider>();

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (interactionPoint == null)
                interactionPoint = transform;
        }

        void SetupAnimator()
        {
            if (animator == null) return;

            // Set default controller if needed
            if (animator.runtimeAnimatorController == null && defaultAnimatorController != null)
            {
                animator.runtimeAnimatorController = defaultAnimatorController;
            }

            // Create override controller for dynamic animations
            if (animator.runtimeAnimatorController != null)
            {
                overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
                animator.runtimeAnimatorController = overrideController;
            }
        }

        public void Initialize(InteractableObject interactable, PlayerController player)
        {
            currentInteractable = interactable;
            targetPlayer = player;
            spawnPosition = transform.position;

            // Randomize appearance if enabled
            if (randomizeAppearance)
            {
                RandomizeAppearance();
            }

            // Configure movement
            if (useNavMeshAgent && navAgent != null)
            {
                navAgent.speed = walkSpeed;
                navAgent.stoppingDistance = stoppingDistance;
                navAgent.enabled = true;
            }

            // Play greeting
            PlayGreeting();

            // Start behavior
            StartCoroutine(NPCBehaviorLoop());

            isInitialized = true;

            Debug.Log($"[NPCPartner] {npcName} initialized for interaction with {player.name}");
        }

        void RandomizeAppearance()
        {
            // Activate random variation
            if (appearanceVariations != null && appearanceVariations.Length > 0)
            {
                foreach (var variation in appearanceVariations)
                {
                    if (variation != null)
                        variation.SetActive(false);
                }

                int randomIndex = Random.Range(0, appearanceVariations.Length);
                appearanceVariations[randomIndex].SetActive(true);
            }

            // Randomize materials
            var renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.gameObject.name.Contains("Body") && skinMaterials.Length > 0)
                {
                    renderer.material = skinMaterials[Random.Range(0, skinMaterials.Length)];
                }
                else if (renderer.gameObject.name.Contains("Cloth") && clothingMaterials.Length > 0)
                {
                    renderer.material = clothingMaterials[Random.Range(0, clothingMaterials.Length)];
                }
            }
        }

        #endregion

        #region Movement

        public void MoveToPosition(Vector3 position, bool run = false)
        {
            targetPosition = position;
            isMoving = true;

            if (useNavMeshAgent && navAgent != null && navAgent.enabled)
            {
                navAgent.SetDestination(position);
                navAgent.speed = run ? runSpeed : walkSpeed;
            }

            // Update animation
            if (animator != null)
            {
                animator.SetBool("IsMoving", true);
                animator.SetFloat("MoveSpeed", run ? 1f : 0.5f);
            }
        }

        public void MoveToPlayer()
        {
            if (targetPlayer == null) return;

            // Calculate position near player
            Vector3 directionToPlayer = (transform.position - targetPlayer.transform.position).normalized;
            Vector3 targetPos = targetPlayer.transform.position + directionToPlayer * interactionRadius;

            MoveToPosition(targetPos);
        }

        public void StopMoving()
        {
            isMoving = false;

            if (useNavMeshAgent && navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = true;
            }

            if (animator != null)
            {
                animator.SetBool("IsMoving", false);
            }
        }

        void UpdateMovement()
        {
            if (!isMoving) return;

            if (useNavMeshAgent && navAgent != null && navAgent.enabled)
            {
                // Check if reached destination
                if (!navAgent.pathPending && navAgent.remainingDistance < stoppingDistance)
                {
                    StopMoving();
                }
            }
            else
            {
                // Manual movement
                Vector3 direction = (targetPosition - transform.position).normalized;
                float distance = Vector3.Distance(transform.position, targetPosition);

                if (distance > stoppingDistance)
                {
                    transform.position += direction * walkSpeed * Time.deltaTime;
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(direction), rotationSpeed * Time.deltaTime);
                }
                else
                {
                    StopMoving();
                }
            }
        }

        #endregion

        #region Interaction

        public void PrepareForTrigger(LoveTriggerSO trigger, Transform interactionPoint)
        {
            StartCoroutine(PrepareForTriggerCoroutine(trigger, interactionPoint));
        }

        IEnumerator PrepareForTriggerCoroutine(LoveTriggerSO trigger, Transform interactionPoint)
        {
            isInteracting = true;

            // React with appropriate delay
            yield return new WaitForSeconds(reactionDelay);

            // Play agreement sound
            PlayAgreement();

            // Move to position if needed
            if (Vector3.Distance(transform.position, interactionPoint.position) > stoppingDistance)
            {
                MoveToPosition(interactionPoint.position);

                // Wait until in position
                while (isMoving)
                {
                    yield return null;
                }
            }

            // Face the correct direction
            if (interactionPoint != null)
            {
                yield return RotateTowards(interactionPoint.rotation, 0.5f);
            }

            isInteracting = false;
        }

        public void ExecuteTriggerAnimation(AnimationClip animationClip, float duration)
        {
            if (animator == null || overrideController == null) return;

            StartCoroutine(ExecuteTriggerAnimationCoroutine(animationClip, duration));
        }

        IEnumerator ExecuteTriggerAnimationCoroutine(AnimationClip animationClip, float duration)
        {
            isExecutingTrigger = true;

            // Override animation
            if (animationClip != null)
            {
                overrideController["Action"] = animationClip;
                animator.Play("Action");
            }

            // Wait for animation
            yield return new WaitForSeconds(duration);

            // Return to idle
            animator.Play(idleStateName);

            isExecutingTrigger = false;
        }

        IEnumerator RotateTowards(Quaternion targetRotation, float duration)
        {
            Quaternion startRotation = transform.rotation;
            float elapsed = 0;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }

            transform.rotation = targetRotation;
        }

        #endregion

        #region Behavior

        IEnumerator NPCBehaviorLoop()
        {
            while (isInitialized)
            {
                yield return new WaitForSeconds(1f);

                if (!isInteracting && !isExecutingTrigger)
                {
                    switch (behaviorMode)
                    {
                        case NPCBehaviorMode.Passive:
                            // Just stand and wait
                            break;

                        case NPCBehaviorMode.Responsive:
                            // Face player if nearby
                            if (autoFacePlayer && targetPlayer != null)
                            {
                                FaceTarget(targetPlayer.transform);
                            }
                            break;

                        case NPCBehaviorMode.Active:
                            // Occasionally move or gesture
                            if (Random.value < 0.1f)
                            {
                                PerformIdleAction();
                            }
                            break;
                    }
                }
            }
        }

        void FaceTarget(Transform target)
        {
            if (target == null) return;

            Vector3 direction = (target.position - transform.position).normalized;
            direction.y = 0; // Keep upright

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, facingSpeed * Time.deltaTime);
        }

        void PerformIdleAction()
        {
            // Could play idle animations, look around, etc.
            if (animator != null)
            {
                // Trigger random idle variation
                int randomIdle = Random.Range(0, 3);
                animator.SetTrigger($"IdleVariation{randomIdle}");
            }
        }

        #endregion

        #region Audio

        void PlayGreeting()
        {
            if (audioSource != null && greetingSounds.Length > 0)
            {
                AudioClip clip = greetingSounds[Random.Range(0, greetingSounds.Length)];
                audioSource.PlayOneShot(clip);
            }
        }

        void PlayAgreement()
        {
            if (audioSource != null && agreementSounds.Length > 0)
            {
                AudioClip clip = agreementSounds[Random.Range(0, agreementSounds.Length)];
                audioSource.PlayOneShot(clip);
            }
        }

        void PlayGoodbye()
        {
            if (audioSource != null && goodbyeSounds.Length > 0)
            {
                AudioClip clip = goodbyeSounds[Random.Range(0, goodbyeSounds.Length)];
                audioSource.PlayOneShot(clip);
            }
        }

        #endregion

        #region Cleanup

        public void Dismiss()
        {
            StartCoroutine(DismissCoroutine());
        }

        IEnumerator DismissCoroutine()
        {
            isInitialized = false;

            // Play goodbye
            PlayGoodbye();

            // Wait a moment
            yield return new WaitForSeconds(1f);

            // Could add fade out or other effects

            // Destroy
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            StopAllCoroutines();
        }

        #endregion

        #region Public API

        public bool IsReady()
        {
            return isInitialized && !isMoving && !isInteracting && !isExecutingTrigger;
        }

        public Transform GetInteractionPoint()
        {
            return interactionPoint;
        }

        public void SetPersonality(NPCPersonality newPersonality)
        {
            personality = newPersonality;
            // Could adjust behavior parameters based on personality
        }

        public void EnableAI(bool enable)
        {
            if (navAgent != null)
                navAgent.enabled = enable;

            enabled = enable;
        }

        #endregion

        #region Gizmos

        void OnDrawGizmosSelected()
        {
            // Draw interaction radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);

            // Draw interaction point
            if (interactionPoint != null && interactionPoint != transform)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(interactionPoint.position, Vector3.one * 0.2f);
                Gizmos.DrawLine(transform.position, interactionPoint.position);
            }
        }

        #endregion
    }
}