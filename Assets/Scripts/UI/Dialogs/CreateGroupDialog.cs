using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Linq;
/// <summary>
/// Dialog for creating a new group
/// </summary>
public class CreateGroupDialog : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField groupNameInput;
    [SerializeField] private TMP_InputField descriptionInput;
    [SerializeField] private Button createButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TextMeshProUGUI errorText;

    public Action<Group> OnGroupCreated;

    private string creatorUserId;

    private void Awake()
    {
        createButton?.onClick.AddListener(OnCreateClicked);
        cancelButton?.onClick.AddListener(CloseDialog);

        if (errorText != null)
            errorText.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    public void ShowDialog(string userId)
    {
        creatorUserId = userId;

        // Clear inputs
        if (groupNameInput != null)
            groupNameInput.text = "";
        if (descriptionInput != null)
            descriptionInput.text = "";

        if (errorText != null)
            errorText.gameObject.SetActive(false);

        gameObject.SetActive(true);
    }

    private async void OnCreateClicked()
    {
        string groupName = groupNameInput?.text ?? "";
        string description = descriptionInput?.text ?? "";

        if (!ValidateInput(groupName, out string error))
        {
            ShowError(error);
            return;
        }

        // VERIFY the creator userId exists in UserManager
        var userManager = UserManager.Instance;
        if (userManager != null)
        {
            var user = userManager.GetUsers().FirstOrDefault(u => u.userId == creatorUserId);
            if (string.IsNullOrEmpty(user.userId))
            {
                ShowError($"Invalid user ID. Please restart the application.");
                Debug.LogError($"[CreateGroup] Creator userId not found: {creatorUserId}");
                return;
            }
        }

        var groupManager = GroupManager.Instance;
        if (groupManager == null)
        {
            ShowError("GroupManager not available");
            return;
        }

        try
        {
            var newGroup = await groupManager.CreateGroupAsync(groupName, creatorUserId, description);
            Debug.Log("New group created");

            OnGroupCreated?.Invoke(newGroup);
            Debug.Log("New group invoked");
            CloseDialog();
        }
        catch (Exception e)
        {
            ShowError($"Failed to create group: {e.Message}");
        }
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