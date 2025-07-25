// =============================================================================
// LoveTriggerGridUI.cs - CORRECTED WITH PROPER NAMESPACES
// =============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using LTSystem.Network; // ⭐ REQUIRED FOR NetworkedLoveTriggerManager
using Fusion; // ⭐ REQUIRED FOR NetworkObject

public class LoveTriggerGridUI : MonoBehaviour
{
    [Header("Grid UI")]
    public Transform gridContainer;
    public GameObject triggerButtonPrefab;
    public ScrollRect gridScrollRect;

    [Header("Category Filtering")]
    public TMP_Dropdown categoryDropdown;
    public Button allCategoriesButton;

    [Header("Visual Style")]
    public Color availableColor = Color.white;
    public Color unavailableColor = Color.gray;
    public Color cooldownColor = new Color(1f, 0.5f, 0.5f, 0.7f);
    public Color consentColor = new Color(1f, 1f, 0f, 0.8f);
    public Color hoverColor = new Color(0.6f, 0.1f, 0.8f, 1f);

    private PlayerController playerController;
    private NetworkedLoveTriggerManager triggerManager;
    private List<LoveTriggerButtonUI> triggerButtons = new List<LoveTriggerButtonUI>();
    private string currentCategoryFilter = "";
    private Dictionary<string, float> cooldownTimers = new Dictionary<string, float>();

    void Start()
    {
        playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            triggerManager = playerController.GetComponent<NetworkedLoveTriggerManager>();
        }

        SetupCategoryFilter();
        RefreshTriggerGrid();

        // Refresh grid periodically for cooldowns
        StartCoroutine(RefreshGridPeriodically());
    }

    void SetupCategoryFilter()
    {
        if (categoryDropdown != null)
        {
            categoryDropdown.options.Clear();
            categoryDropdown.options.Add(new TMP_Dropdown.OptionData("All Categories"));

            var currentCharacter = playerController?.GetCurrentCharacter();
            if (currentCharacter?.triggerCategories != null)
            {
                foreach (string category in currentCharacter.triggerCategories)
                {
                    categoryDropdown.options.Add(new TMP_Dropdown.OptionData(category));
                }
            }

            categoryDropdown.onValueChanged.AddListener(OnCategoryChanged);
        }

        if (allCategoriesButton != null)
        {
            allCategoriesButton.onClick.AddListener(() => SetCategoryFilter(""));
        }
    }

    public void RefreshTriggerGrid()
    {
        ClearGrid();

        var currentCharacter = playerController?.GetCurrentCharacter();
        if (currentCharacter?.availableLoveTriggers == null) return;

        foreach (var trigger in currentCharacter.availableLoveTriggers)
        {
            if (trigger != null && ShouldShowTrigger(trigger))
            {
                CreateTriggerButton(trigger);
            }
        }
    }

    bool ShouldShowTrigger(LoveTriggerSO trigger)
    {
        if (string.IsNullOrEmpty(currentCategoryFilter))
            return true;

        return trigger.category == currentCategoryFilter;
    }

    void CreateTriggerButton(LoveTriggerSO trigger)
    {
        if (triggerButtonPrefab == null || gridContainer == null) return;

        GameObject buttonObj = Instantiate(triggerButtonPrefab, gridContainer);
        LoveTriggerButtonUI triggerButton = buttonObj.GetComponent<LoveTriggerButtonUI>();

        if (triggerButton != null)
        {
            triggerButton.Setup(trigger, this, triggerManager);
            triggerButtons.Add(triggerButton);
        }
    }

    void ClearGrid()
    {
        foreach (var button in triggerButtons)
        {
            if (button != null && button.gameObject != null)
                Destroy(button.gameObject);
        }
        triggerButtons.Clear();
    }

    void OnCategoryChanged(int index)
    {
        if (index == 0)
        {
            SetCategoryFilter(""); // All categories
        }
        else
        {
            var currentCharacter = playerController?.GetCurrentCharacter();
            if (currentCharacter?.triggerCategories != null && index - 1 < currentCharacter.triggerCategories.Length)
            {
                SetCategoryFilter(currentCharacter.triggerCategories[index - 1]);
            }
        }
    }

    public void SetCategoryFilter(string category)
    {
        currentCategoryFilter = category;
        RefreshTriggerGrid();
    }

    public void OnTriggerExecuted(string triggerID, float cooldownDuration)
    {
        if (cooldownDuration > 0)
        {
            cooldownTimers[triggerID] = Time.time + cooldownDuration;
        }
    }

    public bool IsTriggerOnCooldown(string triggerID)
    {
        if (!cooldownTimers.ContainsKey(triggerID))
            return false;

        return Time.time < cooldownTimers[triggerID];
    }

    public float GetCooldownProgress(string triggerID)
    {
        if (!cooldownTimers.ContainsKey(triggerID))
            return 1f;

        float endTime = cooldownTimers[triggerID];
        float duration = endTime - Time.time;
        var trigger = GetTriggerByID(triggerID);

        if (trigger != null && duration > 0)
        {
            return 1f - (duration / trigger.cooldownDuration);
        }

        return 1f;
    }

    LoveTriggerSO GetTriggerByID(string triggerID)
    {
        var character = playerController?.GetCurrentCharacter();
        if (character?.availableLoveTriggers != null)
        {
            foreach (var trigger in character.availableLoveTriggers)
            {
                if (trigger != null && trigger.triggerID == triggerID)
                    return trigger;
            }
        }
        return null;
    }

    IEnumerator RefreshGridPeriodically()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.1f);

            // Update cooldown states
            foreach (var button in triggerButtons)
            {
                if (button != null)
                    button.UpdateCooldownState();
            }
        }
    }
}