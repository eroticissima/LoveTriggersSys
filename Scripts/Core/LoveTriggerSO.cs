// =============================================================================
// LoveTriggerSO.cs - CLEAN VERSION (No Conflicts)
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

public enum ConsentType { Manual, TrustedOnly, AlwaysAccept }

public enum AnimationType
{
    SingleCharacter,    // Solo el source ejecuta
    Partner,           // Requiere partner para ejecutar
    Synchronized      // Ambos ejecutan animaciones sincronizadas
}

public enum TriggerPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

[System.Serializable]
public struct AnimationData
{
    public AnimationClip clip;
    public string stateName;
    public float transitionDuration;
    public bool forceInterrupt;
    public string[] requiredBones;
}

[System.Serializable]
public struct PartnerAnimationData
{
    public AnimationData sourceAnimation;
    public AnimationData targetAnimation;
    public float syncOffset;
    public Transform[] syncPoints;
}

[CreateAssetMenu(menuName = "Eroticissima/LoveTrigger")]
public class LoveTriggerSO : ScriptableObject
{
    [Header("Identification")]
    public string triggerID = ""; // Auto-generate if empty
    public string triggerName;
    public string category = "Default";
    public TriggerPriority priority = TriggerPriority.Normal;

    [Header("Animation Type")]
    public AnimationType animationType = AnimationType.SingleCharacter;

    [Header("Legacy Animations (Compatibility)")]
    public AnimationClip animatorClip; // Maintained for compatibility

    [Header("New Animations")]
    public AnimationData singleAnimation;
    public PartnerAnimationData partnerAnimation;

    [Header("Timeline")]
    public TimelineAsset cinematicTimeline;
    public bool useTimeline;

    [Header("Requirements")]
    public bool requiresConsent;
    public ConsentType consentMode;
    public int affectionRequired;
    public int trustRequired;
    public List<string> requiredEmotionFlags;
    public string[] requiredTags;

    [Header("Configuration")]
    public float cooldownDuration;
    public float overrideDuration = 3f; // Fallback duration when no animation
    public bool canBeInterrupted = true;
    public bool returnToIdleAfter = true;
    public string idleStateName = "Idle";

    [Header("Effects")]
    public string[] soundEffects;
    public string[] visualEffects;
    public Sprite icon;

    protected virtual void OnValidate()
    {
        // Auto-generate ID if empty
        if (string.IsNullOrEmpty(triggerID))
        {
            triggerID = name.Replace(" ", "_").ToLower();
        }

        // Migrate legacy animation if exists
        if (animatorClip != null && singleAnimation.clip == null)
        {
            singleAnimation.clip = animatorClip;
            singleAnimation.stateName = animatorClip.name;
        }
    }

    public bool IsCompatibleWith(GameObject source, GameObject target)
    {
        if (animationType == AnimationType.SingleCharacter)
            return ValidateCharacter(source);

        return ValidateCharacter(source) && ValidateCharacter(target);
    }

    private bool ValidateCharacter(GameObject character)
    {
        if (character == null) return false;

        if (requiredTags != null)
        {
            foreach (string tag in requiredTags)
            {
                if (!character.CompareTag(tag))
                    return false;
            }
        }

        return character.GetComponent<Animator>() != null;
    }
}