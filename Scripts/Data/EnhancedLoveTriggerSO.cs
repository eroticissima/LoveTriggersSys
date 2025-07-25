using UnityEngine;
using UnityEngine.Timeline;
using System.Collections.Generic;
using UnityEngine.Rendering;                  // core VolumeProfile
using UnityEngine.Rendering.HighDefinition;   // HDRP-specific types
using LTSystem.Objects;


namespace LTSystem
{
    /// <summary>
    /// Enhanced Love Trigger ScriptableObject with full Timeline support
    /// and object-specific configuration
    /// </summary>
    [CreateAssetMenu(menuName = "Love Trigger System/Enhanced Love Trigger", fileName = "LT_")]
    public class EnhancedLoveTriggerSO : LoveTriggerSO
    {
        [Header("Timeline Configuration")]
        [Space(10)]
        [Tooltip("Timeline asset for cinematic sequences")]
        public TimelineAsset primaryTimeline;

        [Tooltip("Alternative timelines for different scenarios")]
        public TimelineVariant[] timelineVariants;

        [Header("Camera Setup")]
        public CameraConfiguration[] cameraConfigurations;
        public bool useCustomCameraRig = true;
        public float cameraTransitionTime = 1f;
        public AnimationCurve cameraTransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Object Context")]
        [Tooltip("What type of objects can use this trigger")]
        public ObjectCategory[] compatibleObjects = { ObjectCategory.Furniture };

        [Tooltip("Specific object tags that can use this trigger")]
        public string[] compatibleTags = { "Chair", "Bed", "Couch" };

        [Tooltip("Required object components")]
        public string[] requiredComponents;

        [Header("Positioning")]
        public PositioningMode positioningMode = PositioningMode.UseObjectAnchors;
        public Vector3 playerOffset = Vector3.zero;
        public Vector3 partnerOffset = new Vector3(1, 0, 0);
        public bool alignToObject = true;
        public float positioningDuration = 0.5f;

        [Header("Visual Effects")]
        public VisualEffectConfiguration[] visualEffects;
        public bool dimEnvironmentLighting = true;
        public float lightingTransitionDuration = 1f;
        public Color ambientColorOverride = new Color(0.2f, 0.2f, 0.3f);

        [Header("Audio")]
        public AudioConfiguration audioConfig;
        public bool muteEnvironmentAudio = true;
        public float audioFadeTime = 0.5f;

        [Header("Post Processing")]
        public bool usePostProcessingOverride = true;
        public UnityEngine.Rendering.VolumeProfile postProcessProfile;
        public float postProcessBlendTime = 1f;

        [Header("Interaction Restrictions")]
        public bool requiresPrivacy = false;
        public float privacyCheckRadius = 10f;
        public bool blockOtherInteractions = true;
        public float interactionBlockRadius = 5f;

        [Header("Partner Requirements")]
        public PartnerRequirement partnerRequirement = PartnerRequirement.Optional;
        public PartnerPreference[] partnerPreferences;
        public bool allowNPCSubstitution = true;
        public GameObject preferredNPCPrefab;

        [Header("Platform Adaptations")]
        public PlatformAdaptation[] platformAdaptations;

        #region Nested Types

        [System.Serializable]
        public class TimelineVariant
        {
            public string variantName;
            public TimelineAsset timeline;
            public VariantCondition condition;
            public string conditionValue;

            public enum VariantCondition
            {
                PlayerGender,
                PartnerGender,
                TimeOfDay,
                LocationTag,
                PlayerStat,
                Random
            }
        }

        [System.Serializable]
        public class CameraConfiguration
        {
            public string shotName;
            public Vector3 position;
            public Vector3 rotation;
            public float fieldOfView = 60f;
            public float duration = 2f;
            public AnimationCurve transitionCurve;
            public CameraMovementType movementType = CameraMovementType.Static;
            public Transform trackTarget;
            public Vector3 trackOffset;

            public enum CameraMovementType
            {
                Static,
                Tracking,
                Orbit,
                Dolly,
                Handheld
            }
        }

        [System.Serializable]
        public class VisualEffectConfiguration
        {
            public GameObject effectPrefab;
            public EffectTiming timing;
            public float delay;
            public Vector3 positionOffset;
            public bool attachToPlayer;
            public bool attachToPartner;
            public bool attachToObject;
            public float duration = -1; // -1 for effect's natural duration

            public enum EffectTiming
            {
                OnStart,
                OnPlayerPositioned,
                OnTimelineStart,
                AtTimelineTime,
                OnComplete
            }
        }

        [System.Serializable]
        public class AudioConfiguration
        {
            public AudioClip ambientMusic;
            public AudioClip[] soundEffects;
            public AudioTriggerPoint[] triggerPoints;
            public float musicVolume = 0.7f;
            public float effectsVolume = 1f;
            public bool use3DAudio = true;

            [System.Serializable]
            public class AudioTriggerPoint
            {
                public AudioClip clip;
                public float timelineTime;
                public AudioSource targetSource;
                public bool isOneShot = true;
            }
        }

        [System.Serializable]
        public class PartnerPreference
        {
            public PreferenceType type;
            public string value;
            public float weight = 1f;

            public enum PreferenceType
            {
                Tag,
                Component,
                Stat,
                Relationship
            }
        }

        [System.Serializable]
        public class PlatformAdaptation
        {
            public PlatformType platform;
            public AdaptationType adaptationType;
            public string adaptationValue;
            public TimelineAsset alternativeTimeline;
            public float modifierValue;

            public enum AdaptationType
            {
                UseAlternativeTimeline,
                ScaleAnimationSpeed,
                SimplifyEffects,
                ReduceCameraMovement,
                UseStaticCameras
            }
        }

        public enum ObjectCategory
        {
            Furniture,
            Bed,
            Chair,
            Table,
            Couch,
            Floor,
            Wall,
            Prop,
            Vehicle,
            Nature,
            Special
        }

        public enum PositioningMode
        {
            UseObjectAnchors,
            RelativeToObject,
            CustomPositions,
            TimelineControlled,
            PhysicsBased
        }

        public enum PartnerRequirement
        {
            Required,
            Optional,
            Forbidden
        }

        #endregion

        #region Validation

        public void OnValidate()
        {
            base.OnValidate();

            // Ensure timeline is set if useTimeline is true
            if (useTimeline && primaryTimeline == null && cinematicTimeline != null)
            {
                primaryTimeline = cinematicTimeline;
            }

            // Validate camera configurations
            if (cameraConfigurations != null)
            {
                for (int i = 0; i < cameraConfigurations.Length; i++)
                {
                    if (cameraConfigurations[i].transitionCurve == null ||
                        cameraConfigurations[i].transitionCurve.keys.Length == 0)
                    {
                        cameraConfigurations[i].transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
                    }
                }
            }

            // Auto-setup compatible objects based on name
            if (compatibleObjects == null || compatibleObjects.Length == 0)
            {
                compatibleObjects = GuessCompatibleObjects();
            }
        }

        ObjectCategory[] GuessCompatibleObjects()
        {
            var categories = new List<ObjectCategory>();

            string lowerName = triggerName.ToLower();

            if (lowerName.Contains("sit"))
                categories.Add(ObjectCategory.Chair);

            if (lowerName.Contains("lie") || lowerName.Contains("sleep") || lowerName.Contains("cuddle"))
                categories.Add(ObjectCategory.Bed);

            if (lowerName.Contains("lean"))
            {
                categories.Add(ObjectCategory.Wall);
                categories.Add(ObjectCategory.Table);
            }

            if (categories.Count == 0)
                categories.Add(ObjectCategory.Furniture); // Default

            return categories.ToArray();
        }

        #endregion

        #region Runtime Helpers

        public TimelineAsset GetTimelineForContext(GameObject player, GameObject partner, GameObject interactableObject)
        {
            // Check variants first
            if (timelineVariants != null && timelineVariants.Length > 0)
            {
                foreach (var variant in timelineVariants)
                {
                    if (CheckVariantCondition(variant, player, partner, interactableObject))
                    {
                        return variant.timeline;
                    }
                }
            }

            // Check platform adaptations
            if (platformAdaptations != null && PlatformDetectionSystem.Instance != null)
            {
                var currentPlatform = PlatformDetectionSystem.Instance.CurrentPlatform;

                foreach (var adaptation in platformAdaptations)
                {
                    if (adaptation.platform == currentPlatform &&
                        adaptation.adaptationType == PlatformAdaptation.AdaptationType.UseAlternativeTimeline &&
                        adaptation.alternativeTimeline != null)
                    {
                        return adaptation.alternativeTimeline;
                    }
                }
            }

            // Return primary timeline
            return primaryTimeline != null ? primaryTimeline : cinematicTimeline;
        }

        bool CheckVariantCondition(TimelineVariant variant, GameObject player, GameObject partner, GameObject interactableObject)
        {
            switch (variant.condition)
            {
                case TimelineVariant.VariantCondition.Random:
                    return Random.value < 0.5f;

                case TimelineVariant.VariantCondition.TimeOfDay:
                    // Check game time system
                    var timeManager = FindObjectOfType<TimeManager>();
                    if (timeManager != null)
                    {
                        return timeManager.GetTimeOfDay().ToString() == variant.conditionValue;
                    }
                    return false;

                case TimelineVariant.VariantCondition.PlayerGender:
                    var playerData = player?.GetComponent<CharacterData>();
                    return playerData != null && playerData.name.Contains(variant.conditionValue);

                case TimelineVariant.VariantCondition.LocationTag:
                    return interactableObject != null && interactableObject.CompareTag(variant.conditionValue);

                default:
                    return false;
            }
        }

        public bool IsCompatibleWithObject(GameObject obj)
        {
            if (obj == null) return false;

            // Check category
            var interactable = obj.GetComponent<InteractableObject>();
            if (interactable != null)
            {
                // Check by tags
                foreach (var tag in compatibleTags)
                {
                    if (obj.CompareTag(tag))
                        return true;
                }
            }

            // Check required components
            if (requiredComponents != null)
            {
                foreach (var componentName in requiredComponents)
                {
                    if (obj.GetComponent(componentName) == null)
                        return false;
                }
            }

            return true;
        }

        public Vector3 GetPlayerPosition(Transform objectTransform)
        {
            switch (positioningMode)
            {
                case PositioningMode.UseObjectAnchors:
                    var anchors = objectTransform.GetComponentsInChildren<Transform>();
                    foreach (var anchor in anchors)
                    {
                        if (anchor.name.Contains("PlayerAnchor"))
                            return anchor.position;
                    }
                    goto case PositioningMode.RelativeToObject;

                case PositioningMode.RelativeToObject:
                    return objectTransform.position + objectTransform.rotation * playerOffset;

                default:
                    return objectTransform.position + playerOffset;
            }
        }

        public Vector3 GetPartnerPosition(Transform objectTransform)
        {
            switch (positioningMode)
            {
                case PositioningMode.UseObjectAnchors:
                    var anchors = objectTransform.GetComponentsInChildren<Transform>();
                    foreach (var anchor in anchors)
                    {
                        if (anchor.name.Contains("PartnerAnchor"))
                            return anchor.position;
                    }
                    goto case PositioningMode.RelativeToObject;

                case PositioningMode.RelativeToObject:
                    return objectTransform.position + objectTransform.rotation * partnerOffset;

                default:
                    return objectTransform.position + partnerOffset;
            }
        }

        public CameraConfiguration GetCameraForTime(float normalizedTime)
        {
            if (cameraConfigurations == null || cameraConfigurations.Length == 0)
                return null;

            // Find best camera for current time
            float segmentDuration = 1f / cameraConfigurations.Length;
            int index = Mathf.FloorToInt(normalizedTime / segmentDuration);
            index = Mathf.Clamp(index, 0, cameraConfigurations.Length - 1);

            return cameraConfigurations[index];
        }

        public bool CheckPrivacyRequirement(Vector3 position)
        {
            if (!requiresPrivacy) return true;

            // Check for other players in radius
            var colliders = Physics.OverlapSphere(position, privacyCheckRadius);
            foreach (var col in colliders)
            {
                var player = col.GetComponent<PlayerController>();
                if (player != null && player.gameObject.activeSelf)
                {
                    return false; // Someone else is nearby
                }
            }

            return true;
        }

        #endregion

        #region Editor Helpers

        [ContextMenu("Create Camera Configuration Template")]
        void CreateCameraTemplate()
        {
            cameraConfigurations = new CameraConfiguration[]
            {
                new CameraConfiguration
                {
                    shotName = "Establishing",
                    position = new Vector3(2, 2, -3),
                    rotation = new Vector3(15, -30, 0),
                    fieldOfView = 60,
                    duration = 2,
                    movementType = CameraConfiguration.CameraMovementType.Static
                },
                new CameraConfiguration
                {
                    shotName = "CloseUp",
                    position = new Vector3(0.5f, 1.5f, -1),
                    rotation = new Vector3(10, -15, 0),
                    fieldOfView = 40,
                    duration = 3,
                    movementType = CameraConfiguration.CameraMovementType.Tracking
                },
                new CameraConfiguration
                {
                    shotName = "Wide",
                    position = new Vector3(3, 3, -5),
                    rotation = new Vector3(20, -45, 0),
                    fieldOfView = 70,
                    duration = 2,
                    movementType = CameraConfiguration.CameraMovementType.Orbit
                }
            };
        }

        [ContextMenu("Validate Timeline Bindings")]
        void ValidateTimelineBindings()
        {
            if (primaryTimeline == null)
            {
                Debug.LogWarning($"[{name}] No primary timeline assigned");
                return;
            }

            var outputs = primaryTimeline.outputs;
            Debug.Log($"[{name}] Timeline has {System.Linq.Enumerable.Count(outputs)} tracks:");

            foreach (var output in outputs)
            {
                Debug.Log($"  - {output.streamName} ({output.outputTargetType})");
            }
        }

        #endregion
    }

    /// <summary>
    /// Optional time management system interface
    /// </summary>
    public interface ITimeManager
    {
        System.DateTime GetCurrentGameTime();
        TimeOfDay GetTimeOfDay();
    }

    public enum TimeOfDay
    {
        Dawn,
        Morning,
        Afternoon,
        Evening,
        Night
    }

    public class TimeManager : MonoBehaviour, ITimeManager
    {
        public float timeScale = 1f;
        private float currentHour = 12f;

        public System.DateTime GetCurrentGameTime()
        {
            return System.DateTime.Now.Date.AddHours(currentHour);
        }

        public TimeOfDay GetTimeOfDay()
        {
            if (currentHour < 6) return TimeOfDay.Night;
            if (currentHour < 10) return TimeOfDay.Morning;
            if (currentHour < 17) return TimeOfDay.Afternoon;
            if (currentHour < 20) return TimeOfDay.Evening;
            return TimeOfDay.Night;
        }

        void Update()
        {
            currentHour += Time.deltaTime * timeScale / 3600f;
            if (currentHour >= 24) currentHour -= 24;
        }
    }
}