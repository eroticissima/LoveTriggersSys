// =============================================================================
// CharacterData.cs - ENHANCED WITH PROPER INTEGRATION
// =============================================================================

using UnityEngine;
using System.Collections.Generic;

// FIXED: Conditional imports to avoid compilation errors
#if GAMEPLAY_INGREDIENTS_AVAILABLE
using GameplayIngredients;
using GameplayIngredients.Actions;
using GameplayIngredients.Logic;
using GameplayIngredients.Events;
#endif

[CreateAssetMenu(menuName = "Love Trigger System/Character Data", fileName = "New Character")]
public class CharacterData : ScriptableObject
{
    [Header("Character Identity")]
    public string characterID = "";
    public string characterName = "";
    [TextArea(3, 5)]
    public string characterDescription = "";
    public Sprite characterIcon;
    public GameObject characterPrefab;

    [Header("Animation States")]
    public string desktopIdleState = "Idle";
    public string vrIdleState = "VRIK_Idle";
    public string locomotionState = "Locomotion";
    public string loveTriggerState = "LoveTrigger";

    [Header("Love Triggers")]
    public LoveTriggerSO[] availableLoveTriggers;
    public string[] triggerCategories = { "Affection", "Intimacy", "Playful" };
    public int maxSimultaneousTriggers = 1;

    [Header("Platform Specific Settings")]
    public bool enableVRIKInVR = true;
    public bool adaptAnimationsForPlatform = true;
    public float animationSpeedMultiplier = 1f;

    [Header("Audio")]
    public AudioClip characterSelectSound;
    public AudioClip[] characterVoiceClips;

    [Header("Character Attributes")]
    public int baseAffection = 50;
    public int baseTrust = 50;
    public List<string> characterTags = new List<string>();
    public List<string> emotionalFlags = new List<string>();

    [Header("Compatibility")]
    public string[] compatibleCharacterIDs;
    public bool canInteractWithAnyCharacter = true;

    private void OnValidate()
    {
        // Auto-generate character ID if empty
        if (string.IsNullOrEmpty(characterID))
        {
            characterID = name.Replace(" ", "_").ToLower();
        }

        // Validate trigger categories
        if (triggerCategories == null || triggerCategories.Length == 0)
        {
            triggerCategories = new string[] { "Default" };
        }

        // Ensure character has basic tags
        if (characterTags == null)
        {
            characterTags = new List<string>();
        }

        if (!characterTags.Contains("Character"))
        {
            characterTags.Add("Character");
        }
    }

    // Validation methods
    public bool IsCompatibleWith(CharacterData otherCharacter)
    {
        if (otherCharacter == null) return false;

        if (canInteractWithAnyCharacter) return true;

        if (compatibleCharacterIDs != null)
        {
            foreach (string compatibleID in compatibleCharacterIDs)
            {
                if (compatibleID == otherCharacter.characterID)
                    return true;
            }
        }

        return false;
    }

    public bool HasTag(string tag)
    {
        return characterTags != null && characterTags.Contains(tag);
    }

    public bool HasEmotionalFlag(string flag)
    {
        return emotionalFlags != null && emotionalFlags.Contains(flag);
    }

    public LoveTriggerSO[] GetTriggersForCategory(string category)
    {
        if (availableLoveTriggers == null) return new LoveTriggerSO[0];

        var categoryTriggers = new List<LoveTriggerSO>();
        foreach (var trigger in availableLoveTriggers)
        {
            if (trigger != null && trigger.category == category)
            {
                categoryTriggers.Add(trigger);
            }
        }

        return categoryTriggers.ToArray();
    }

    public LoveTriggerSO GetTriggerByID(string triggerID)
    {
        if (availableLoveTriggers == null || string.IsNullOrEmpty(triggerID))
            return null;

        foreach (var trigger in availableLoveTriggers)
        {
            if (trigger != null && trigger.triggerID == triggerID)
                return trigger;
        }

        return null;
    }

    // Platform adaptation helpers
    public string GetIdleStateForPlatform(PlatformType platform)
    {
        switch (platform)
        {
            case PlatformType.VR_Oculus:
            case PlatformType.VR_SteamVR:
            case PlatformType.VR_OpenXR:
            case PlatformType.VR_Pico:
            case PlatformType.VR_Generic:
                return vrIdleState;

            default:
                return desktopIdleState;
        }
    }

    public float GetAnimationSpeedForPlatform(PlatformType platform)
    {
        if (!adaptAnimationsForPlatform) return animationSpeedMultiplier;

        switch (platform)
        {
            case PlatformType.Mobile:
                return animationSpeedMultiplier * 1.2f; // Slightly faster for mobile

            case PlatformType.VR_Oculus:
            case PlatformType.VR_SteamVR:
            case PlatformType.VR_OpenXR:
            case PlatformType.VR_Pico:
            case PlatformType.VR_Generic:
                return animationSpeedMultiplier * 0.9f; // Slightly slower for VR comfort

            default:
                return animationSpeedMultiplier;
        }
    }

    // Audio helpers
    public AudioClip GetRandomVoiceClip()
    {
        if (characterVoiceClips == null || characterVoiceClips.Length == 0)
            return null;

        int randomIndex = Random.Range(0, characterVoiceClips.Length);
        return characterVoiceClips[randomIndex];
    }

    // Debug and validation
    public bool ValidateCharacterSetup()
    {
        List<string> issues = new List<string>();

        if (string.IsNullOrEmpty(characterID))
            issues.Add("Character ID is empty");

        if (string.IsNullOrEmpty(characterName))
            issues.Add("Character name is empty");

        if (characterPrefab == null)
            issues.Add("Character prefab is not assigned");

        if (characterPrefab != null && characterPrefab.GetComponent<Animator>() == null)
            issues.Add("Character prefab has no Animator component");

        if (availableLoveTriggers == null || availableLoveTriggers.Length == 0)
            issues.Add("No love triggers assigned");

        if (issues.Count > 0)
        {
            Debug.LogWarning($"[CharacterData] Validation issues for {characterName}:\n" + string.Join("\n", issues));
            return false;
        }

        return true;
    }

    [ContextMenu("Validate Character Setup")]
    public void ValidateSetup()
    {
        bool isValid = ValidateCharacterSetup();
        Debug.Log($"[CharacterData] {characterName} validation: {(isValid ? "PASSED" : "FAILED")}");
    }

    [ContextMenu("Log Character Info")]
    public void LogCharacterInfo()
    {
        Debug.Log($"=== CHARACTER INFO: {characterName} ===");
        Debug.Log($"ID: {characterID}");
        Debug.Log($"Description: {characterDescription}");
        Debug.Log($"Available Triggers: {availableLoveTriggers?.Length ?? 0}");
        Debug.Log($"Categories: {string.Join(", ", triggerCategories)}");
        Debug.Log($"Tags: {string.Join(", ", characterTags)}");
        Debug.Log($"Desktop Idle: {desktopIdleState}");
        Debug.Log($"VR Idle: {vrIdleState}");
        Debug.Log($"Base Affection: {baseAffection}");
        Debug.Log($"Base Trust: {baseTrust}");
        Debug.Log($"Compatible Characters: {(compatibleCharacterIDs != null ? string.Join(", ", compatibleCharacterIDs) : "ANY")}");
        Debug.Log("=====================================");
    }
}