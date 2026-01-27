using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using InventorySystem.Data;

/// <summary>
/// Dialog for adding an existing user to a group
/// </summary>
public class AddMemberDialog : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Dropdown userDropdown;
    [SerializeField] private TMP_Dropdown permissionDropdown;
    [SerializeField] private Button addButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TextMeshProUGUI errorText;
    [SerializeField] private TextMeshProUGUI infoText;
    
    public Action<string, string, GroupPermission> OnMemberAdded;  // groupId, userId, permission
    
    private string currentGroupId;
    private List<SerializableUserInfo> availableUsers = new List<SerializableUserInfo>();
    
    private void Awake()
    {
        addButton?.onClick.AddListener(OnAddClicked);
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
        permissionDropdown.AddOptions(new List<string>
        {
            "Viewer",
            "Member",
            "Editor",
            "Moderator",
            "Admin"
        });
        
        // Default to Member
        permissionDropdown.value = 1;
    }
    
    public void ShowDialog(string groupId)
    {
        currentGroupId = groupId;
        
        LoadAvailableUsers();
        
        if (permissionDropdown != null)
            permissionDropdown.value = 1;  // Member
        
        if (errorText != null)
            errorText.gameObject.SetActive(false);
        
        gameObject.SetActive(true);
    }
    
    private void LoadAvailableUsers()
    {
        availableUsers.Clear();
        
        if (userDropdown == null) return;
        
        userDropdown.ClearOptions();
        
        // Load all users
        var userManager = UserManager.Instance;
        if (userManager == null)
        {
            userDropdown.AddOptions(new List<string> { "No users available" });
            userDropdown.interactable = false;
            
            if (addButton != null)
                addButton.interactable = false;
            
            if (infoText != null)
            {
                infoText.text = "UserManager not available. Cannot load users.";
                infoText.gameObject.SetActive(true);
            }
            return;
        }
        
        availableUsers = userManager.GetUsers().ToList();
        
        // Filter out users already in the group
        var groupManager = GroupManager.Instance;
        if (groupManager != null)
        {
            var group = groupManager.GetGroupById(currentGroupId);
            if (group != null)
            {
                var existingUserIds = group.members.Select(m => m.userId).ToHashSet();
                availableUsers = availableUsers
                    .Where(u => !existingUserIds.Contains(u.userId))
                    .ToList();
            }
        }
        
        if (availableUsers.Count == 0)
        {
            userDropdown.AddOptions(new List<string> { "All users already in group" });
            userDropdown.interactable = false;
            
            if (addButton != null)
                addButton.interactable = false;
            
            if (infoText != null)
            {
                infoText.text = "All existing users are already members of this group. Create a new user first.";
                infoText.gameObject.SetActive(true);
            }
            return;
        }
        
        // Populate dropdown with available users
        var options = availableUsers.Select(u => u.userName.ToString()).ToList();
        userDropdown.AddOptions(options);
        userDropdown.interactable = true;
        
        if (addButton != null)
            addButton.interactable = true;
        
        if (infoText != null)
        {
            infoText.text = $"Select a user to add to this group. {availableUsers.Count} user(s) available.";
            infoText.gameObject.SetActive(true);
        }
    }
    
    private void OnAddClicked()
    {
        if (availableUsers.Count == 0)
        {
            ShowError("No users available to add");
            return;
        }
        
        int selectedIndex = userDropdown?.value ?? 0;
        if (selectedIndex < 0 || selectedIndex >= availableUsers.Count)
        {
            ShowError("Invalid user selected");
            return;
        }
        
        var selectedUser = availableUsers[selectedIndex];
        
        // Get selected permission
        int permissionIndex = permissionDropdown?.value ?? 1;
        GroupPermission permission = (GroupPermission)permissionIndex;
        
        OnMemberAdded?.Invoke(currentGroupId, selectedUser.userId, permission);
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
    
    private void OnDestroy()
    {
        addButton?.onClick.RemoveAllListeners();
        cancelButton?.onClick.RemoveAllListeners();
    }
}