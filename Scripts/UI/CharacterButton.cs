// =============================================================================
// CharacterButton.cs - CORRECTED WITH PROPER NAMESPACES
// =============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterButton : MonoBehaviour
{
    [Header("UI Components")]
    public Button button;
    public TMP_Text nameText;
    public Image iconImage;
    public Image backgroundImage;

    public string CharacterID { get; private set; }
    private CharacterSelectionUI parentUI;
    private bool isSelected = false;

    public void Setup(CharacterData character, CharacterSelectionUI parent)
    {
        CharacterID = character.characterID;
        parentUI = parent;

        if (nameText != null)
            nameText.text = character.characterName;

        if (iconImage != null && character.characterIcon != null)
            iconImage.sprite = character.characterIcon;

        if (button != null)
            button.onClick.AddListener(() => parentUI.SelectCharacter(CharacterID));

        // Add hover effects
        SetupHoverEffects();
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

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateVisualState();
    }

    void OnHoverEnter()
    {
        if (!isSelected && backgroundImage != null && parentUI != null)
            backgroundImage.color = parentUI.hoverButtonColor;
    }

    void OnHoverExit()
    {
        UpdateVisualState();
    }

    void UpdateVisualState()
    {
        if (backgroundImage == null || parentUI == null) return;

        if (isSelected)
            backgroundImage.color = parentUI.selectedButtonColor;
        else
            backgroundImage.color = parentUI.normalButtonColor;
    }
}