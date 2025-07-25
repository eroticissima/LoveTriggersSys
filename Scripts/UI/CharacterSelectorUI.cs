// =============================================================================
// CharacterSelectionUI.cs - CORRECTED WITH PROPER NAMESPACES
// =============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class CharacterSelectionUI : MonoBehaviour
{
    [Header("Character List UI")]
    public Transform characterListContainer;
    public GameObject characterButtonPrefab;
    public ScrollRect characterScrollRect;

    [Header("Character Display")]
    public Transform characterDisplayArea;
    public TMP_Text selectedCharacterName;
    public TMP_Text selectedCharacterDescription;

    [Header("Visual Style")]
    public Color normalButtonColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
    public Color selectedButtonColor = new Color(0.4f, 0.1f, 0.6f, 0.9f); // Purple
    public Color hoverButtonColor = new Color(0.6f, 0.1f, 0.8f, 0.9f);

    private PlayerController playerController;
    private List<CharacterButton> characterButtons = new List<CharacterButton>();
    private string currentSelectedCharacterID;

    void Start()
    {
        playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            SetupCharacterList();
        }
    }

    void SetupCharacterList()
    {
        var availableCharacters = playerController.GetAvailableCharacters();

        if (availableCharacters != null)
        {
            foreach (var character in availableCharacters)
            {
                if (character != null)
                    CreateCharacterButton(character);
            }

            // Select starting character
            if (availableCharacters.Length > 0)
            {
                SelectCharacter(availableCharacters[0].characterID);
            }
        }
    }

    void CreateCharacterButton(CharacterData character)
    {
        if (characterButtonPrefab == null || characterListContainer == null) return;

        GameObject buttonObj = Instantiate(characterButtonPrefab, characterListContainer);
        CharacterButton charButton = buttonObj.GetComponent<CharacterButton>();

        if (charButton != null)
        {
            charButton.Setup(character, this);
            characterButtons.Add(charButton);
        }
    }

    public void SelectCharacter(string characterID)
    {
        currentSelectedCharacterID = characterID;

        // Update visual selection
        foreach (var button in characterButtons)
        {
            if (button != null)
                button.SetSelected(button.CharacterID == characterID);
        }

        // Update character display
        var characterData = GetCharacterData(characterID);
        if (characterData != null)
        {
            if (selectedCharacterName != null)
                selectedCharacterName.text = characterData.characterName;

            if (selectedCharacterDescription != null)
                selectedCharacterDescription.text = characterData.characterDescription;

            // Switch character in game
            if (playerController != null)
                playerController.SwitchCharacter(characterID);
        }
    }

    CharacterData GetCharacterData(string characterID)
    {
        if (playerController == null) return null;

        var characters = playerController.GetAvailableCharacters();
        if (characters != null)
        {
            foreach (var character in characters)
            {
                if (character != null && character.characterID == characterID)
                    return character;
            }
        }
        return null;
    }
}