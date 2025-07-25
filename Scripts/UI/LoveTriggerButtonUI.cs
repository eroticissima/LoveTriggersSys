// =============================================================================
// LoveTriggerButtonUI.cs - CORRECTED WITH PROPER NAMESPACES
// =============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LTSystem.Network; // ⭐ REQUIRED FOR NetworkedLoveTriggerManager
using Fusion; // ⭐ REQUIRED FOR NetworkObject

public class LoveTriggerButtonUI : MonoBehaviour
{
    [Header("UI Components")]
    public Button button;
    public Image backgroundImage;
    public Image iconImage;
    public TMP_Text nameText;
    public Image categoryBadge;
    public Image cooldownOverlay;
    public Image consentIndicator;

    private LoveTriggerSO trigger;
    private LoveTriggerGridUI parentGrid;
    private NetworkedLoveTriggerManager triggerManager;

    public void Setup(LoveTriggerSO triggerData, LoveTriggerGridUI grid, NetworkedLoveTriggerManager manager)
    {
        trigger = triggerData;
        parentGrid = grid;
        triggerManager = manager;

        // Setup visual elements
        if (nameText != null && trigger != null)
            nameText.text = trigger.triggerName;

        if (iconImage != null && trigger?.icon != null)
            iconImage.sprite = trigger.icon;

        if (consentIndicator != null && trigger != null)
            consentIndicator.gameObject.SetActive(trigger.requiresConsent);

        // Setup click handler
        if (button != null)
            button.onClick.AddListener(OnButtonClicked);

        // Setup hover effects
        SetupHoverEffects();

        UpdateVisualState();
    }

    void SetupHoverEffects()
    {
        var eventTrigger = gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (eventTrigger == null)
            eventTrigger = gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        // Clear existing triggers
        eventTrigger.triggers.Clear();

        var pointerEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        pointerEnter.callback.AddListener((data) => OnHoverEnter());
        eventTrigger.triggers.Add(pointerEnter);

        var pointerExit = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        pointerExit.callback.AddListener((data) => OnHoverExit());
        eventTrigger.triggers.Add(pointerExit);
    }

    void OnButtonClicked()
    {
        if (!CanExecuteTrigger()) return;
        if (triggerManager == null || trigger == null) return;

        // Find nearby target for love trigger
        var nearbyTargets = Physics.OverlapSphere(triggerManager.transform.position, triggerManager.MaxTriggerDistance);
        NetworkObject targetNetworkObject = null;

        foreach (var target in nearbyTargets)
        {
            var networkObj = target.GetComponent<NetworkObject>();
            var targetManager = target.GetComponent<NetworkedLoveTriggerManager>();

            if (networkObj != null && targetManager != null && target.gameObject != triggerManager.gameObject)
            {
                targetNetworkObject = networkObj;
                break;
            }
        }

        if (targetNetworkObject != null)
        {
            triggerManager.RequestLoveTrigger(trigger.triggerID, targetNetworkObject);
            parentGrid.OnTriggerExecuted(trigger.triggerID, trigger.cooldownDuration);
        }
        else
        {
            Debug.Log($"[LTGridUI] No valid target found for trigger: {trigger.triggerName}");
        }
    }

    bool CanExecuteTrigger()
    {
        if (parentGrid == null || trigger == null) return false;

        if (parentGrid.IsTriggerOnCooldown(trigger.triggerID))
            return false;

        if (triggerManager != null && triggerManager.IsProcessing)
            return false;

        return true;
    }

    public void UpdateCooldownState()
    {
        if (parentGrid == null || trigger == null) return;

        bool onCooldown = parentGrid.IsTriggerOnCooldown(trigger.triggerID);

        if (cooldownOverlay != null)
        {
            cooldownOverlay.gameObject.SetActive(onCooldown);

            if (onCooldown)
            {
                float progress = parentGrid.GetCooldownProgress(trigger.triggerID);
                cooldownOverlay.fillAmount = 1f - progress;
            }
        }

        UpdateVisualState();
    }

    void UpdateVisualState()
    {
        bool canExecute = CanExecuteTrigger();

        if (button != null)
            button.interactable = canExecute;

        if (backgroundImage != null && parentGrid != null)
        {
            backgroundImage.color = canExecute ? parentGrid.availableColor : parentGrid.unavailableColor;
        }

        Color iconColor = canExecute ? Color.white : Color.gray;

        if (iconImage != null)
            iconImage.color = iconColor;

        if (nameText != null)
            nameText.color = iconColor;
    }

    void OnHoverEnter()
    {
        if (CanExecuteTrigger() && backgroundImage != null && parentGrid != null)
        {
            backgroundImage.color = parentGrid.hoverColor;
        }
    }

    void OnHoverExit()
    {
        UpdateVisualState();
    }
}
