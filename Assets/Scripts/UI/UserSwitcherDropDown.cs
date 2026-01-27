using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using InventorySystem.Data;

/// <summary>
/// Dropdown component for switching between user profiles (admin only)
/// </summary>
public class UserSwitcher : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Dropdown userDropdown;
    [SerializeField] private Button switchButton;
    [SerializeField] private GameObject switcherPanel;

    private List<SerializableUserInfo> allUsers = new List<SerializableUserInfo>();

    private void Awake()
    {
        switchButton?.onClick.AddListener(OnSwitchUserClicked);
    }

    private void Start()
    {
        // Delay initial refresh to ensure managers are initialized
        Invoke(nameof(RefreshAndUpdate), 0.1f);

        // Subscribe to UserManager events - ADD THIS
        var userManager = UserManager.Instance;
        if (userManager != null)
        {
            userManager.OnUsersChanged += OnUsersChanged;
        }
    }

    private void OnDestroy()  // ADD THIS METHOD
    {
        // Unsubscribe from events
        var userManager = UserManager.Instance;
        if (userManager != null)
        {
            userManager.OnUsersChanged -= OnUsersChanged;
        }
    }

    private void OnUsersChanged(List<SerializableUserInfo> users)  // ADD THIS METHOD
    {
        Debug.Log($"[UserSwitcher] Users changed, refreshing list ({users.Count} users)");
        RefreshUserList();
        UpdateVisibility();
    }

    private void RefreshAndUpdate()
    {
        RefreshUserList();
        UpdateVisibility();
    }

    public void RefreshUserList()
    {
        allUsers.Clear();

        if (userDropdown == null) return;

        userDropdown.ClearOptions();

        var userManager = UserManager.Instance;
        if (userManager == null)
        {
            // Manager not ready yet - hide panel
            if (switcherPanel != null)
                switcherPanel.SetActive(false);
            return;
        }

        allUsers = userManager.GetUsers().ToList();

        if (allUsers.Count == 0)
        {
            userDropdown.AddOptions(new List<string> { "No users" });
            userDropdown.interactable = false;
            if (switchButton != null)
                switchButton.interactable = false;
            return;
        }

        // Get current user
        string currentUserId = PlayerPrefs.GetString("UserId", "");
        int currentIndex = 0;

        // Populate dropdown
        var options = new List<string>();
        for (int i = 0; i < allUsers.Count; i++)
        {
            var user = allUsers[i];
            options.Add(user.userName.ToString());

            // Find current user's index
            if (user.userId == currentUserId)
            {
                currentIndex = i;
            }
        }

        userDropdown.AddOptions(options);
        userDropdown.value = currentIndex;
        userDropdown.interactable = true;

        if (switchButton != null)
            switchButton.interactable = true;
    }

    private void UpdateVisibility()
    {
        if (switcherPanel == null) return;

        var userManager = UserManager.Instance;
        if (userManager == null)
        {
            switcherPanel.SetActive(false);
            return;
        }

        // Get current user
        string currentUserId = PlayerPrefs.GetString("UserId", "");
        var currentUser = userManager.GetUsers().FirstOrDefault(u => u.userId == currentUserId);

        // Show if user is Admin OR if in testing (always show for now)
        bool isAdmin = !string.IsNullOrEmpty(currentUser.userId) && currentUser.permission == GroupPermission.Admin;

        // OPTION 1: Only admins
        // switcherPanel.SetActive(isAdmin);

        // OPTION 2: Always show (for testing/development)
        switcherPanel.SetActive(true);  // Use this during development!

        // OPTION 3: Show if multiple users exist
        // switcherPanel.SetActive(allUsers.Count > 1);
    }

    private void OnSwitchUserClicked()
    {
        if (userDropdown == null || allUsers.Count == 0) return;

        int selectedIndex = userDropdown.value;
        if (selectedIndex < 0 || selectedIndex >= allUsers.Count) return;

        var selectedUser = allUsers[selectedIndex];

        // Don't switch if already selected
        string currentUserId = PlayerPrefs.GetString("UserId", "");
        if (selectedUser.userId == currentUserId)
        {
            Debug.Log("[UserSwitcher] Already logged in as this user");
            return;
        }

        // Switch user
        PlayerPrefs.SetString("UserId", selectedUser.userId);
        PlayerPrefs.SetString("UserName", selectedUser.userName.ToString());
        PlayerPrefs.Save();

        Debug.Log($"[UserSwitcher] Switched to user: {selectedUser.userName}");

        // Reload the current scene to refresh everything
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        );
    }
}