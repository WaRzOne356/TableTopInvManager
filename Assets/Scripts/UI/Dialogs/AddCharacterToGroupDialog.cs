using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Dialog for adding a character to a group
/// </summary>
public class AddCharacterToGroupDialog : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Dropdown characterDropdown;
    [SerializeField] private Button addButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TextMeshProUGUI errorText;
    [SerializeField] private TextMeshProUGUI infoText;

    public Action<string, string, string> OnCharacterAdded;  // groupId, userId, characterId

    private string currentGroupId;
    private string currentUserId;
    private List<PlayerCharacter> availableCharacters = new List<PlayerCharacter>();

    private void Awake()
    {
        addButton?.onClick.AddListener(OnAddClicked);
        cancelButton?.onClick.AddListener(CloseDialog);

        if (errorText != null)
            errorText.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    public void ShowDialog(string groupId, string userId)
    {
        currentGroupId = groupId;
        currentUserId = userId;

        LoadAvailableCharacters();
        PopulateDropdown();

        if (errorText != null)
            errorText.gameObject.SetActive(false);

        gameObject.SetActive(true);
    }

    private void LoadAvailableCharacters()
    {
        availableCharacters.Clear();

        var characterManager = CharacterManager.Instance;
        var groupManager = GroupManager.Instance;

        if (characterManager == null || groupManager == null) return;

        // Get user's characters
        var userCharacters = characterManager.GetCharactersByUser(currentUserId);

        // Get characters already in group
        var group = groupManager.GetGroupById(currentGroupId);
        var groupCharacterIds = new HashSet<string>();
        if (group != null)
        {
            foreach (var member in group.members)
            {
                foreach (var charId in member.characterIds)
                {
                    groupCharacterIds.Add(charId);
                }
            }
        }

        // Filter to only characters not already in group
        availableCharacters = userCharacters
            .Where(c => !groupCharacterIds.Contains(c.characterId))
            .ToList();
    }

    private void PopulateDropdown()
    {
        if (characterDropdown == null) return;

        characterDropdown.ClearOptions();

        if (availableCharacters.Count == 0)
        {
            characterDropdown.AddOptions(new List<string> { "No available characters" });
            characterDropdown.interactable = false;

            if (addButton != null)
                addButton.interactable = false;

            if (infoText != null)
            {
                infoText.text = "All your characters are already in this group, or you have no characters.";
                infoText.gameObject.SetActive(true);
            }

            return;
        }

        var options = availableCharacters.Select(c =>
            $"{c.characterName} (Lvl {c.level} {c.characterClass})"
        ).ToList();

        characterDropdown.AddOptions(options);
        characterDropdown.interactable = true;

        if (addButton != null)
            addButton.interactable = true;

        if (infoText != null)
            infoText.gameObject.SetActive(false);
    }

    private void OnAddClicked()
    {
        if (availableCharacters.Count == 0)
        {
            ShowError("No characters available to add");
            return;
        }

        int selectedIndex = characterDropdown?.value ?? 0;
        if (selectedIndex < 0 || selectedIndex >= availableCharacters.Count)
        {
            ShowError("Invalid character selected");
            return;
        }

        var selectedCharacter = availableCharacters[selectedIndex];

        OnCharacterAdded?.Invoke(currentGroupId, currentUserId, selectedCharacter.characterId);
        CloseDialog();
    }

    private void ShowError(string message)
    {
        if (errorText != null)
        {
            errorText.text = message;
            errorText.gameObject.SetActive(true);
        }
    }

    private void CloseDialog()
    {
        gameObject.SetActive(false);
    }
}