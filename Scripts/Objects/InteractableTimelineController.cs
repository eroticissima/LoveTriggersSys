using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using System.Linq;

namespace LTSystem.Objects
{
    /// <summary>
    /// Handles Timeline playback with dynamic binding for love triggers.
    /// Manages cameras, effects, and player/NPC synchronization.
    /// </summary>
    public class InteractableTimelineController : MonoBehaviour
    {
        [Header("Runtime Configuration")]
        [SerializeField] private PlayableDirector playableDirector;
        [SerializeField] private bool createDirectorIfMissing = true;

        [Header("Camera Management")]
        [SerializeField] private bool manageCameras = true;
        [SerializeField] private float cameraBlendTime = 1f;
        [SerializeField] private CinemachineVirtualCamera timelineVirtualCamera;
        [SerializeField] private int timelineCameraPriority = 100;

        [Header("Binding Configuration")]
        [SerializeField] private bool autoDetectBindings = true;
        [SerializeField] private string playerTrackName = "Player";
        [SerializeField] private string partnerTrackName = "Partner";
        [SerializeField] private string objectTrackName = "InteractableObject";

        [Header("Effects")]
        [SerializeField] private bool pauseGameDuringTimeline = false;
        [SerializeField] private float timeScale = 1f;
        [SerializeField] private bool fadeInOut = true;
        [SerializeField] private float fadeDuration = 0.5f;

        // Runtime state
        private Camera originalCamera;
        private CinemachineBrain cinemachineBrain;
        private Dictionary<string, Object> runtimeBindings = new Dictionary<string, Object>();
        private bool isPlaying = false;
        private float originalTimeScale;
        private List<Component> disabledComponents = new List<Component>();

        // Events
        public System.Action OnTimelineStarted;
        public System.Action OnTimelineCompleted;
        public System.Action<float> OnTimelineProgress;

        #region Initialization

        void Awake()
        {
            SetupPlayableDirector();
            SetupCameraSystem();
        }

        void SetupPlayableDirector()
        {
            playableDirector = GetComponent<PlayableDirector>();

            if (playableDirector == null && createDirectorIfMissing)
            {
                playableDirector = gameObject.AddComponent<PlayableDirector>();
                playableDirector.playOnAwake = false;
            }

            if (playableDirector != null)
            {
                playableDirector.stopped += OnPlayableDirectorStopped;
            }
        }

        void SetupCameraSystem()
        {
            if (!manageCameras) return;

            // Find main camera
            originalCamera = Camera.main;

            // Setup Cinemachine
            if (originalCamera != null)
            {
                cinemachineBrain = originalCamera.GetComponent<CinemachineBrain>();
                if (cinemachineBrain == null)
                {
                    cinemachineBrain = originalCamera.gameObject.AddComponent<CinemachineBrain>();
                }
            }

            // Create timeline virtual camera if needed
            if (timelineVirtualCamera == null)
            {
                CreateTimelineVirtualCamera();
            }
        }

        void CreateTimelineVirtualCamera()
        {
            GameObject vcamObject = new GameObject("Timeline_VirtualCamera");
            vcamObject.transform.SetParent(transform);

            timelineVirtualCamera = vcamObject.AddComponent<CinemachineVirtualCamera>();
            timelineVirtualCamera.Priority = 0; // Start with low priority

            // Basic setup
            var composer = timelineVirtualCamera.AddCinemachineComponent<CinemachineComposer>();
            var transposer = timelineVirtualCamera.AddCinemachineComponent<CinemachineTransposer>();
        }

        #endregion

        #region Timeline Playback

        public IEnumerator PlayTimeline(TimelineAsset timeline, TimelineBindings bindings, float transitionDuration = 1f)
        {
            if (timeline == null || playableDirector == null)
            {
                Debug.LogError("[TimelineController] Cannot play timeline - missing timeline or director");
                yield break;
            }

            if (isPlaying)
            {
                Debug.LogWarning("[TimelineController] Timeline already playing");
                yield break;
            }

            isPlaying = true;
            OnTimelineStarted?.Invoke();

            // Setup
            yield return PrepareForTimeline(transitionDuration);

            // Configure timeline
            playableDirector.playableAsset = timeline;
            ApplyBindings(bindings);

            // Start playback
            playableDirector.Play();

            // Monitor progress
            yield return MonitorTimelineProgress();

            // Cleanup
            yield return CleanupAfterTimeline(transitionDuration);

            isPlaying = false;
            OnTimelineCompleted?.Invoke();
        }

        IEnumerator PrepareForTimeline(float transitionDuration)
        {
            // Store original time scale
            originalTimeScale = Time.timeScale;

            // Fade out if enabled
            if (fadeInOut)
            {
                yield return FadeScreen(true, fadeDuration);
            }

            // Pause game if required
            if (pauseGameDuringTimeline)
            {
                Time.timeScale = 0f;
                playableDirector.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            }
            else
            {
                Time.timeScale = timeScale;
            }

            // Activate timeline camera
            if (manageCameras && timelineVirtualCamera != null)
            {
                timelineVirtualCamera.Priority = timelineCameraPriority;
                yield return new WaitForSeconds(cameraBlendTime);
            }

            // Disable player controls
            DisablePlayerControls();

            // Fade in
            if (fadeInOut)
            {
                yield return FadeScreen(false, fadeDuration);
            }
        }

        IEnumerator CleanupAfterTimeline(float transitionDuration)
        {
            // Fade out
            if (fadeInOut)
            {
                yield return FadeScreen(true, fadeDuration);
            }

            // Restore camera
            if (manageCameras && timelineVirtualCamera != null)
            {
                timelineVirtualCamera.Priority = 0;
                yield return new WaitForSeconds(cameraBlendTime);
            }

            // Restore time scale
            Time.timeScale = originalTimeScale;

            // Re-enable player controls
            EnablePlayerControls();

            // Fade in
            if (fadeInOut)
            {
                yield return FadeScreen(false, fadeDuration);
            }

            // Clear bindings
            ClearBindings();
        }

        IEnumerator MonitorTimelineProgress()
        {
            while (playableDirector.state == PlayState.Playing)
            {
                float progress = (float)(playableDirector.time / playableDirector.duration);
                OnTimelineProgress?.Invoke(progress);
                yield return null;
            }
        }

        #endregion

        #region Binding Management

        void ApplyBindings(TimelineBindings bindings)
        {
            // Clear existing bindings
            ClearBindings();

            // Get timeline outputs
            var outputs = playableDirector.playableAsset.outputs;

            foreach (var output in outputs)
            {
                if (output.sourceObject != null)
                {
                    // Try to match by name
                    string trackName = output.sourceObject.name;

                    if (trackName.Contains(playerTrackName) && bindings.player != null)
                    {
                        playableDirector.SetGenericBinding(output.sourceObject, bindings.player);
                        runtimeBindings[trackName] = bindings.player;
                    }
                    else if (trackName.Contains(partnerTrackName) && bindings.partner != null)
                    {
                        playableDirector.SetGenericBinding(output.sourceObject, bindings.partner);
                        runtimeBindings[trackName] = bindings.partner;
                    }
                    else if (trackName.Contains(objectTrackName) && bindings.interactableObject != null)
                    {
                        playableDirector.SetGenericBinding(output.sourceObject, bindings.interactableObject);
                        runtimeBindings[trackName] = bindings.interactableObject;
                    }
                    else if (trackName.Contains("Camera") && bindings.cameraAnchors != null)
                    {
                        // Handle camera tracks
                        BindCameraTrack(output, bindings.cameraAnchors);
                    }
                    else
                    {
                        // Check additional bindings
                        TryBindFromAdditional(output, bindings.additionalBindings);
                    }
                }
            }

            Debug.Log($"[TimelineController] Applied {runtimeBindings.Count} bindings to timeline");
        }

        void BindCameraTrack(PlayableBinding output, Transform[] cameraAnchors)
        {
            if (cameraAnchors == null || cameraAnchors.Length == 0) return;

            // Extract camera index from track name (e.g., "Camera_1", "Camera_2")
            string trackName = output.sourceObject.name;
            string[] parts = trackName.Split('_');

            if (parts.Length > 1 && int.TryParse(parts[1], out int index))
            {
                if (index >= 0 && index < cameraAnchors.Length && cameraAnchors[index] != null)
                {
                    // Create or get virtual camera at this anchor
                    var vcam = GetOrCreateVirtualCamera(cameraAnchors[index], $"TimelineCamera_{index}");
                    playableDirector.SetGenericBinding(output.sourceObject, vcam);
                    runtimeBindings[trackName] = vcam;
                }
            }
            else
            {
                // Default to first camera anchor
                if (cameraAnchors[0] != null)
                {
                    var vcam = GetOrCreateVirtualCamera(cameraAnchors[0], "TimelineCamera_Default");
                    playableDirector.SetGenericBinding(output.sourceObject, vcam);
                    runtimeBindings[trackName] = vcam;
                }
            }
        }

        CinemachineVirtualCamera GetOrCreateVirtualCamera(Transform anchor, string name)
        {
            // Check if camera already exists at anchor
            var existingCam = anchor.GetComponentInChildren<CinemachineVirtualCamera>();
            if (existingCam != null) return existingCam;

            // Create new virtual camera
            GameObject vcamObject = new GameObject(name);
            vcamObject.transform.SetParent(anchor);
            vcamObject.transform.localPosition = Vector3.zero;
            vcamObject.transform.localRotation = Quaternion.identity;

            var vcam = vcamObject.AddComponent<CinemachineVirtualCamera>();
            vcam.Priority = 0;

            return vcam;
        }

        void TryBindFromAdditional(PlayableBinding output, Dictionary<string, Object> additionalBindings)
        {
            if (additionalBindings == null) return;

            string trackName = output.sourceObject.name;

            // Direct name match
            if (additionalBindings.ContainsKey(trackName))
            {
                playableDirector.SetGenericBinding(output.sourceObject, additionalBindings[trackName]);
                runtimeBindings[trackName] = additionalBindings[trackName];
                return;
            }

            // Partial match
            foreach (var kvp in additionalBindings)
            {
                if (trackName.Contains(kvp.Key) || kvp.Key.Contains(trackName))
                {
                    playableDirector.SetGenericBinding(output.sourceObject, kvp.Value);
                    runtimeBindings[trackName] = kvp.Value;
                    return;
                }
            }
        }

        void ClearBindings()
        {
            foreach (var binding in runtimeBindings)
            {
                playableDirector.ClearGenericBinding(binding.Value);
            }

            runtimeBindings.Clear();
        }

        #endregion

        #region Player Control Management

        void DisablePlayerControls()
        {
            // Find all player controllers in bound objects
            var players = GetBoundPlayers();

            foreach (var player in players)
            {
                // Disable movement
                var movement = player.GetComponent<CharacterController>();
                if (movement != null && movement.enabled)
                {
                    movement.enabled = false;
                    disabledComponents.Add(movement);
                }

                // Disable input
                var input = player.GetComponent<PlayerController>();
                if (input != null)
                {
                    input.SetLoveTriggerEnabled(false);
                }

                // Disable animation controller
                var animController = player.GetComponent<UniversalAnimationController>();
                if (animController != null && animController.enabled)
                {
                    animController.enabled = false;
                    disabledComponents.Add(animController);
                }
            }
        }

        void EnablePlayerControls()
        {
            // Re-enable all disabled components
            // foreach (var component in disabledComponents)
            // {
            //     if (component != null)
            //     {
            //        component.enabled = true;
            //     }
            //  }

            //  disabledComponents.Clear();

          // Re - enabling previously disabled bits:
            foreach (var comp in disabledComponents)
            {
                if (comp is Behaviour b)
                    b.enabled = true;          // MonoBehaviour, scripts, cameras, etc.
                else if (comp is Collider c)
                    c.enabled = true;          // CharacterController, other colliders
                                               // else if you need other types, add more clauses…
            }

            disabledComponents.Clear();

            // Re-enable love triggers
            var players = GetBoundPlayers();
            foreach (var player in players)
            {
                var input = player.GetComponent<PlayerController>();
                if (input != null)
                {
                    input.SetLoveTriggerEnabled(true);
                }
            }
        }

        List<GameObject> GetBoundPlayers()
        {
            var players = new List<GameObject>();

            foreach (var binding in runtimeBindings.Values)
            {
                GameObject go = binding as GameObject;
                if (go != null && go.GetComponent<PlayerController>() != null)
                {
                    players.Add(go);
                }
            }

            return players;
        }

        #endregion

        #region Effects

        IEnumerator FadeScreen(bool fadeOut, float duration)
        {
            // This would integrate with your fade system
            // For now, just wait
            yield return new WaitForSeconds(duration);
        }

        #endregion

        #region Callbacks

        void OnPlayableDirectorStopped(PlayableDirector director)
        {
            if (director == playableDirector && isPlaying)
            {
                // Timeline ended naturally
                Debug.Log("[TimelineController] Timeline playback completed");
            }
        }

        #endregion

        #region Public API

        public void StopTimeline()
        {
            if (playableDirector != null && isPlaying)
            {
                playableDirector.Stop();
                StopAllCoroutines();
                StartCoroutine(CleanupAfterTimeline(0f));
            }
        }

        public void PauseTimeline()
        {
            if (playableDirector != null)
            {
                playableDirector.Pause();
            }
        }

        public void ResumeTimeline()
        {
            if (playableDirector != null)
            {
                playableDirector.Resume();
            }
        }

        public bool IsPlaying => isPlaying;
        public float Progress => playableDirector != null ? (float)(playableDirector.time / playableDirector.duration) : 0f;

        #endregion

        void OnDestroy()
        {
            if (playableDirector != null)
            {
                playableDirector.stopped -= OnPlayableDirectorStopped;
            }

            // Ensure we re-enable everything
            EnablePlayerControls();
            Time.timeScale = 1f;
        }
    }

    /// <summary>
    /// Container for timeline bindings
    /// </summary>
    [System.Serializable]
    public class TimelineBindings
    {
        public GameObject player;
        public GameObject partner;
        public GameObject interactableObject;
        public Transform[] cameraAnchors;
        public Dictionary<string, Object> additionalBindings;
    }
}