// =============================================================================
// UniversalAnimationController.cs - PROPER VRIK INTEGRATION
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using RootMotion.FinalIK; // ✅ This is correct - VRIK is in this namespace

public class UniversalAnimationController : MonoBehaviour
{
    [Header("Platform Adaptation")]
    public bool autoConfigureForPlatform = true;
    public string desktopIdleState = "Idle";
    public string vrIdleState = "VRIK_Idle";
    public string loveTriggerStateName = "LoveTrigger";
    public bool requiresLoveTriggerState = true;

    [Header("Animation Method")]
    public AnimationMethod playbackMethod = AnimationMethod.AnimatorOverride;

    [Header("Platform Settings")]
    [SerializeField] private PlatformAnimationSettings desktopSettings;
    [SerializeField] private PlatformAnimationSettings vrSettings;
    [SerializeField] private PlatformAnimationSettings mobileSettings;

    [Header("VRIK Integration")]
    public VRIK vrikComponent;  // ✅ This references the VRIK class you showed me
    public bool autoDetectVRIK = true;
    public bool pauseVRIKDuringAnimations = true;
    public float vrikBlendDuration = 0.5f;
    public float vrikRestoreDelay = 0.2f;

    [Header("Configuration")]
    public bool debugMode = false;
    public bool restoreLocomotionAfter = true;

    // Components
    private Animator animator;
    private RuntimeAnimatorController originalController;
    private AnimatorOverrideController overrideController;
    private PlayableGraph playableGraph;
    private AnimationPlayableOutput playableOutput;
    private AnimationMixerPlayable mixerPlayable;

    // Platform State
    private PlatformType currentPlatform;
    private PlatformAnimationSettings currentSettings;
    private string currentIdleState;
    private bool isVRMode;

    // Animation State
    private bool isPlayingLoveTrigger = false;
    private string currentTriggerName = "";
    private Coroutine currentAnimationCoroutine;
    private Coroutine vrikBlendCoroutine;
    private float originalVRIKWeight = 1f;
    private bool wasVRIKEnabled = true;

    // Events
    public System.Action OnAnimationStart;
    public System.Action OnAnimationComplete;
    public System.Action OnReturnToLocomotion;
    public System.Action<PlatformType> OnPlatformConfigured;

    public enum AnimationMethod
    {
        AnimatorOverride,
        PlayableGraph,
        SeparateAnimator
    }

    [System.Serializable]
    public class PlatformAnimationSettings
    {
        [Header("Performance")]
        public float animationSpeedMultiplier = 1f;
        public float transitionSpeedMultiplier = 1f;
        public float defaultTransitionDuration = 0.25f;
        public bool optimizeForPerformance = false;

        [Header("Features")]
        public bool enableVRIKIntegration = false;
        public bool enableHapticFeedback = false;
        public bool enable3DAudio = false;

        [Header("Timing")]
        public float minAnimationDuration = 0.5f;
        public float maxAnimationDuration = 10f;
        public float completionThreshold = 0.95f;

        [Header("Quality")]
        public AnimatorUpdateMode updateMode = AnimatorUpdateMode.Normal;
        public AnimatorCullingMode cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError($"[UniversalAnimationController] No Animator found on {gameObject.name}");
            return;
        }

        // Auto-detect VRIK if enabled
        if (autoDetectVRIK && vrikComponent == null)
            vrikComponent = GetComponent<VRIK>();

        // ✅ PROPER VRIK INTEGRATION - Based on actual FinalIK code
        if (vrikComponent != null)
        {
            // Access the solver property (IKSolverVR) from VRIK
            originalVRIKWeight = vrikComponent.solver.IKPositionWeight;
            wasVRIKEnabled = vrikComponent.enabled;

            if (debugMode)
                Debug.Log($"[UniversalAnimationController] VRIK detected. Original weight: {originalVRIKWeight}");
        }

        originalController = animator.runtimeAnimatorController;

        // Initialize default settings if null
        if (desktopSettings == null) desktopSettings = new PlatformAnimationSettings();
        if (vrSettings == null) vrSettings = new PlatformAnimationSettings();
        if (mobileSettings == null) mobileSettings = new PlatformAnimationSettings();

        // Subscribe to platform detection
        if (autoConfigureForPlatform)
        {
            if (PlatformDetectionSystem.Instance != null)
            {
                PlatformDetectionSystem.Instance.OnPlatformDetected += ConfigureForPlatform;
                ConfigureForPlatform(PlatformDetectionSystem.Instance.CurrentPlatform);
            }
            else
            {
                ConfigureForPlatform(PlatformType.Desktop);
            }
        }
        else
        {
            SetupAnimationSystem();
        }
    }

    public void ConfigureForPlatform(PlatformType platform)
    {
        currentPlatform = platform;
        isVRMode = (platform != PlatformType.Desktop && platform != PlatformType.Mobile && platform != PlatformType.Console);

        // Select appropriate settings
        switch (platform)
        {
            case PlatformType.Desktop:
                currentSettings = desktopSettings;
                currentIdleState = desktopIdleState;
                break;

            case PlatformType.VR_Oculus:
            case PlatformType.VR_SteamVR:
            case PlatformType.VR_OpenXR:
            case PlatformType.VR_Pico:
            case PlatformType.VR_Generic:
                currentSettings = vrSettings;
                currentIdleState = vrIdleState;
                break;

            case PlatformType.Mobile:
                currentSettings = mobileSettings;
                currentIdleState = desktopIdleState;
                break;

            default:
                currentSettings = desktopSettings;
                currentIdleState = desktopIdleState;
                break;
        }

        ApplyPlatformSettings();
        SetupAnimationSystem();
        OnPlatformConfigured?.Invoke(platform);

        if (debugMode)
        {
            Debug.Log($"[UniversalAnimationController] Configured for {platform}");
            Debug.Log($"- Idle State: {currentIdleState}");
            Debug.Log($"- Animation Speed: {currentSettings.animationSpeedMultiplier}");
            Debug.Log($"- VR Mode: {isVRMode}");
            Debug.Log($"- VRIK Integration: {currentSettings.enableVRIKIntegration}");
        }
    }

    void ApplyPlatformSettings()
    {
        if (animator == null || currentSettings == null) return;

        animator.updateMode = currentSettings.updateMode;
        animator.cullingMode = currentSettings.cullingMode;

        if (isVRMode && currentSettings.optimizeForPerformance)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            QualitySettings.vSyncCount = 0;
        }

        pauseVRIKDuringAnimations = currentSettings.enableVRIKIntegration && vrikComponent != null;
    }

    void SetupAnimationSystem()
    {
        switch (playbackMethod)
        {
            case AnimationMethod.AnimatorOverride:
                SetupOverrideController();
                break;
            case AnimationMethod.PlayableGraph:
                SetupPlayableGraph();
                break;
            case AnimationMethod.SeparateAnimator:
                SetupSeparateAnimation();
                break;
        }
    }

    void SetupOverrideController()
    {
        if (originalController != null)
        {
            overrideController = new AnimatorOverrideController(originalController);
            animator.runtimeAnimatorController = overrideController;
        }
    }

    void SetupPlayableGraph()
    {
        playableGraph = PlayableGraph.Create($"UniversalLoveTrigger_{gameObject.name}");
        playableOutput = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);
        mixerPlayable = AnimationMixerPlayable.Create(playableGraph, 2);
        playableOutput.SetSourcePlayable(mixerPlayable);
    }

    void SetupSeparateAnimation()
    {
        if (GetComponent<Animation>() == null)
        {
            gameObject.AddComponent<Animation>();
        }
    }

    public void PlayAnimation(AnimationData animData, bool forceInterrupt = false)
    {
        if (isPlayingLoveTrigger && !forceInterrupt)
        {
            if (debugMode)
                Debug.LogWarning($"[UniversalAnimationController] Love trigger already playing on {gameObject.name}");
            return;
        }

        if (currentAnimationCoroutine != null)
        {
            StopCoroutine(currentAnimationCoroutine);
        }

        currentAnimationCoroutine = StartCoroutine(PlayAnimationCoroutine(animData));
    }

    public void PlayAnimation(AnimationClip clip, string stateName = "", float transitionDuration = 0f)
    {
        AnimationData animData = new AnimationData
        {
            clip = clip,
            stateName = string.IsNullOrEmpty(stateName) ? loveTriggerStateName : stateName,
            transitionDuration = transitionDuration > 0 ? transitionDuration : currentSettings.defaultTransitionDuration
        };

        animData.transitionDuration *= currentSettings.transitionSpeedMultiplier;
        PlayAnimation(animData);
    }

    private IEnumerator PlayAnimationCoroutine(AnimationData animData)
    {
        isPlayingLoveTrigger = true;
        currentTriggerName = animData.stateName;

        if (debugMode)
            Debug.Log($"[UniversalAnimationController] Starting love trigger: {currentTriggerName} on {gameObject.name} (Platform: {currentPlatform})");

        OnAnimationStart?.Invoke();

        // Platform-specific pre-animation setup
        yield return StartCoroutine(PreAnimationSetup());

        // Execute animation based on method
        switch (playbackMethod)
        {
            case AnimationMethod.AnimatorOverride:
                yield return PlayWithAnimatorOverride(animData);
                break;
            case AnimationMethod.PlayableGraph:
                yield return PlayWithPlayableGraph(animData);
                break;
            case AnimationMethod.SeparateAnimator:
                yield return PlayWithSeparateAnimation(animData);
                break;
        }

        if (debugMode)
            Debug.Log($"[UniversalAnimationController] Love trigger completed: {currentTriggerName} on {gameObject.name}");

        OnAnimationComplete?.Invoke();

        // Platform-specific post-animation cleanup
        yield return StartCoroutine(PostAnimationCleanup());

        if (restoreLocomotionAfter)
        {
            ReturnToLocomotion();
        }
    }

    private IEnumerator PreAnimationSetup()
    {
        // ✅ PROPER VRIK PAUSE - Based on actual FinalIK structure
        if (pauseVRIKDuringAnimations && vrikComponent != null && currentSettings.enableVRIKIntegration)
        {
            yield return StartCoroutine(BlendVRIKWeight(0f));
        }

        if (currentSettings.enableHapticFeedback && isVRMode)
        {
            TriggerHapticFeedback();
        }
    }

    private IEnumerator PostAnimationCleanup()
    {
        // ✅ PROPER VRIK RESTORE - Based on actual FinalIK structure
        if (pauseVRIKDuringAnimations && vrikComponent != null && currentSettings.enableVRIKIntegration)
        {
            yield return new WaitForSeconds(vrikRestoreDelay);
            yield return StartCoroutine(BlendVRIKWeight(originalVRIKWeight));
        }
    }

    private IEnumerator PlayWithAnimatorOverride(AnimationData animData)
    {
        if (overrideController == null || animData.clip == null)
        {
            Debug.LogError($"[UniversalAnimationController] Missing override controller or clip on {gameObject.name}");
            yield break;
        }

        string targetState = loveTriggerStateName;
        string clipToOverride = "LoveTriggerPlaceholder";

        AnimationClip optimizedClip = CreatePlatformOptimizedClip(animData.clip);
        overrideController[clipToOverride] = optimizedClip;

        float transitionDuration = animData.transitionDuration > 0 ?
            animData.transitionDuration : currentSettings.defaultTransitionDuration;

        animator.CrossFade(targetState, transitionDuration);
        yield return new WaitForSeconds(transitionDuration);

        float animationLength = optimizedClip.length / currentSettings.animationSpeedMultiplier;
        float maxWaitTime = Mathf.Clamp(animationLength + 2f, currentSettings.minAnimationDuration, currentSettings.maxAnimationDuration);
        float startTime = Time.time;

        yield return new WaitUntil(() => {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            bool isInCorrectState = stateInfo.IsName(targetState);
            bool hasFinished = stateInfo.normalizedTime >= currentSettings.completionThreshold;
            bool timedOut = (Time.time - startTime) > maxWaitTime;

            if (timedOut && debugMode)
                Debug.LogWarning($"[UniversalAnimationController] Animation timeout: {currentTriggerName} (Platform: {currentPlatform})");

            return (isInCorrectState && hasFinished) || timedOut;
        });

        overrideController[clipToOverride] = null;

        if (optimizedClip != animData.clip)
            DestroyImmediate(optimizedClip);
    }

    private AnimationClip CreatePlatformOptimizedClip(AnimationClip originalClip)
    {
        if (Mathf.Approximately(currentSettings.animationSpeedMultiplier, 1f))
            return originalClip;

        AnimationClip optimizedClip = Object.Instantiate(originalClip);
        optimizedClip.name = $"{originalClip.name}_{currentPlatform}";
        return optimizedClip;
    }

    private IEnumerator PlayWithPlayableGraph(AnimationData animData)
    {
        if (animData.clip == null)
        {
            Debug.LogError($"[UniversalAnimationController] No clip provided for playable graph on {gameObject.name}");
            yield break;
        }

        var clipPlayable = AnimationClipPlayable.Create(playableGraph, animData.clip);
        clipPlayable.SetSpeed(currentSettings.animationSpeedMultiplier);

        playableGraph.Connect(clipPlayable, 0, mixerPlayable, 0);
        mixerPlayable.SetInputWeight(0, 1f);
        playableGraph.Play();

        float adjustedLength = animData.clip.length / currentSettings.animationSpeedMultiplier;
        yield return new WaitForSeconds(adjustedLength);

        playableGraph.Stop();
    }

    private IEnumerator PlayWithSeparateAnimation(AnimationData animData)
    {
        Animation animComponent = GetComponent<Animation>();
        if (animComponent == null || animData.clip == null)
        {
            Debug.LogError($"[UniversalAnimationController] Missing Animation component or clip on {gameObject.name}");
            yield break;
        }

        animator.enabled = false;

        animComponent.clip = animData.clip;
        animComponent[animData.clip.name].speed = currentSettings.animationSpeedMultiplier;
        animComponent.Play();

        yield return new WaitUntil(() => !animComponent.isPlaying);

        animator.enabled = true;
    }

    // ✅ PROPER VRIK WEIGHT BLENDING - Based on actual FinalIK structure
    private IEnumerator BlendVRIKWeight(float targetWeight)
    {
        if (vrikComponent == null) yield break;

        // Access the solver property from VRIK (as shown in your VRIK.cs)
        float startWeight = vrikComponent.solver.IKPositionWeight;
        float elapsed = 0f;

        while (elapsed < vrikBlendDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / vrikBlendDuration;

            // Properly access the IKPositionWeight through the solver
            vrikComponent.solver.IKPositionWeight = Mathf.Lerp(startWeight, targetWeight, t);
            yield return null;
        }

        vrikComponent.solver.IKPositionWeight = targetWeight;

        if (debugMode)
            Debug.Log($"[UniversalAnimationController] VRIK weight blended to: {targetWeight} (Platform: {currentPlatform})");
    }

    private void TriggerHapticFeedback()
    {
        if (isVRMode && debugMode)
        {
            Debug.Log($"[UniversalAnimationController] Triggering VR haptic feedback");
        }
    }

    public void ReturnToLocomotion()
    {
        if (!isPlayingLoveTrigger) return;

        if (debugMode)
            Debug.Log($"[UniversalAnimationController] Returning to locomotion: {currentIdleState} (Platform: {currentPlatform})");

        if (animator.enabled)
        {
            animator.CrossFade(currentIdleState, currentSettings.defaultTransitionDuration);
        }

        if (playbackMethod == AnimationMethod.PlayableGraph && playableGraph.IsValid())
        {
            if (playableGraph.IsPlaying())
                playableGraph.Stop();
        }

        isPlayingLoveTrigger = false;
        currentTriggerName = "";

        OnReturnToLocomotion?.Invoke();
    }

    public void ForceStop()
    {
        if (currentAnimationCoroutine != null)
        {
            StopCoroutine(currentAnimationCoroutine);
            currentAnimationCoroutine = null;
        }

        if (vrikBlendCoroutine != null)
        {
            StopCoroutine(vrikBlendCoroutine);
            vrikBlendCoroutine = null;
        }

        // ✅ PROPER VRIK RESTORE - Immediate restore through solver
        if (vrikComponent != null && currentSettings.enableVRIKIntegration)
        {
            vrikComponent.solver.IKPositionWeight = originalVRIKWeight;
        }

        ReturnToLocomotion();
    }

    // Public query methods
    public bool IsPlaying() => isPlayingLoveTrigger;
    public string GetCurrentTriggerName() => currentTriggerName;
    public bool CanMove() => !isPlayingLoveTrigger;
    public PlatformType GetCurrentPlatform() => currentPlatform;
    public bool IsVRMode() => isVRMode;

    // ✅ PROPER VRIK WEIGHT ACCESS - Through solver property
    public float GetVRIKWeight() => vrikComponent?.solver.IKPositionWeight ?? 1f;

    public PlatformAnimationSettings GetCurrentSettings() => currentSettings;

    private void OnDestroy()
    {
        if (playableGraph.IsValid())
        {
            playableGraph.Destroy();
        }

        if (PlatformDetectionSystem.Instance != null)
        {
            PlatformDetectionSystem.Instance.OnPlatformDetected -= ConfigureForPlatform;
        }
    }

    // Debug methods
    [ContextMenu("Reset VRIK")]
    public void ResetVRIK()
    {
        if (vrikComponent != null)
        {
            vrikComponent.solver.IKPositionWeight = originalVRIKWeight;
            vrikComponent.enabled = wasVRIKEnabled;

            if (debugMode)
                Debug.Log($"[UniversalAnimationController] VRIK reset to original weight: {originalVRIKWeight}");
        }
    }
}