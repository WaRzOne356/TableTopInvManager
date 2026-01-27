using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using InventorySystem.Data;

/// <summary>
/// Dialog for creating a new user/member
/// </summary>
public class CreateUserDialog : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField userNameInput;
    [SerializeField] private TMP_Dropdown permissionDropdown;
    [SerializeField] private Button createButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TextMeshProUGUI errorText;
    [SerializeField] private TextMeshProUGUI infoText;

    public Action<SerializableUserInfo> OnUserCreated;

    private void Awake()
    {
        createButton?.onClick.AddListener(OnCreateClicked);
        cancelButton?.onClick.AddListener(CloseDialog);

        SetupPermissionDropdown();

        if (errorText != null)
            errorText.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    private void SetupPermissionDropdown()
    {
        if (permissionDropdown == null) return;

        permissionDropdown.ClearOptions();

        // Use GroupPermission enum
        var permissionOptions = new List<string>
        {
            "Viewer",      // GroupPermission.Viewer = 0
            "Member",      // GroupPermission.Member = 1
            "Editor",      // GroupPermission.Editor = 2
            "Moderator",   // GroupPermission.Moderator = 3
            "Admin"        // GroupPermission.Admin = 4
        };

        permissionDropdown.AddOptions(permissionOptions);

        // Default to "Member" 
        permissionDropdown.value = 1;
    }

    public void ShowDialog()
    {
        if (userNameInput != null)
            userNameInput.text = "";

        if (permissionDropdown != null)
            permissionDropdown.value = 1;  // Reset to "Member"

        if (errorText != null)
            errorText.gameObject.SetActive(false);

        if (infoText != null)
        {
            infoText.text = "Create a new user. Default permission can be adjusted per-group.";
            infoText.gameObject.SetActive(true);
        }

        gameObject.SetActive(true);
    }

    private async void OnCreateClicked()
    {
        string userName = userNameInput?.text ?? "";

        if (!ValidateInput(userName, out string error))
        {
            ShowError(error);
            return;
        }

        var userManager = UserManager.Instance;
        if (userManager == null)
        {
            ShowError("UserManager not available");
            return;
        }

        // Get selected permission
        int permissionIndex = permissionDropdown?.value ?? 1;
        GroupPermission permission = (GroupPermission)permissionIndex;

        // Create new user
        var newUser = new SerializableUserInfo
        {
            clientId = (ulong)DateTime.Now.Ticks,
            userId = Guid.NewGuid().ToString(),
            userName = userName,
            permission = permission,  // Now uses GroupPermission
            connectionTime = DateTime.Now.ToString("O"),
            isOnline = true
        };

        try
        {
            await userManager.AddUserAsync(newUser);
            Debug.Log($"[CreateUser] Created user: {userName} with permission: {permission}");

            OnUserCreated?.Invoke(newUser);
            CloseDialog();
        }
        catch (Exception e)
        {
            ShowError($"Failed to create user: {e.Message}");
        }
    }

    private bool ValidateInput(string userName, out string error)
    {
        error = "";

        if (string.IsNullOrWhiteSpace(userName))
        {
            error = "User name cannot be empty";
            return false;
        }

        if (userName.Length < 3)
        {
            error = "User name must be at least 3 characters";
            return false;
        }

        if (userName.Length > 30)
        {
            error = "User name must be less than 30 characters";
            return false;
        }

        // Check if user already exists
        var userManager = UserManager.Instance;
        if (userManager != null)
        {
            var existingUser = userManager.GetUserByName(userName);
            if (existingUser != null && !string.IsNullOrEmpty(existingUser.userName.ToString()))
            {
                error = "A user with this name already exists";
                return false;
            }
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

    private void OnDestroy()
    {
        createButton?.onClick.RemoveAllListeners();
        cancelButton?.onClick.RemoveAllListeners();
    }
}