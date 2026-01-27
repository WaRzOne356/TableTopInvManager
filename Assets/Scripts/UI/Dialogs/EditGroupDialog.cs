using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Dialog for editing group details
/// </summary>
public class EditGroupDialog : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField groupNameInput;
    [SerializeField] private TMP_InputField descriptionInput;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button changeAvatarButton;
    [SerializeField] private TextMeshProUGUI errorText;

    public Action<Group> OnGroupUpdated;

    private Group currentGroup;

    private void Awake()
    {
        saveButton?.onClick.AddListener(OnSaveClicked);
        cancelButton?.onClick.AddListener(CloseDialog);
        changeAvatarButton?.onClick.AddListener(OnChangeAvatarClicked);

        if (errorText != null)
            errorText.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    public void ShowDialog(Group group)
    {
        currentGroup = group;

        // Populate inputs
        if (groupNameInput != null)
            groupNameInput.text = group.groupName;
        if (descriptionInput != null)
            descriptionInput.text = group.description;

        if (errorText != null)
            errorText.gameObject.SetActive(false);

        gameObject.SetActive(true);
    }

    private void OnSaveClicked()
    {
        if (currentGroup == null) return;

        string newName = groupNameInput?.text ?? "";
        string newDescription = descriptionInput?.text ?? "";

        if (!ValidateInput(newName, out string error))
        {
            ShowError(error);
            return;
        }

        // Update group
        currentGroup.groupName = newName;
        currentGroup.description = newDescription;
        currentGroup.lastActivity = DateTime.Now;

        OnGroupUpdated?.Invoke(currentGroup);
        CloseDialog();
    }

    private void OnChangeAvatarClicked()
    {
        // TODO: Open avatar selection dialog for groups
        ShowError("Avatar selection coming soon!");
    }

    private bool ValidateInput(string groupName, out string error)
    {
        error = "";

        if (string.IsNullOrWhiteSpace(groupName))
        {
            error = "Group name cannot be empty";
            return false;
        }

        if (groupName.Length < 3)
        {
            error = "Group name must be at least 3 characters";
            return false;
        }

        if (groupName.Length > 50)
        {
            error = "Group name must be less than 50 characters";
            return false;
        }

        return true;
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