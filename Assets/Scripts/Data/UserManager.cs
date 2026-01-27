// Assets/Scripts/UserManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using InventorySystem.Data;

/// <summary>
/// Non-networked user manager that keeps track of users and permissions.
/// - Persists users into the same persistence file (InventoryPersistenceData.users)
/// - Raises OnUsersChanged(List<SerializableUserInfo>) for UI compatibility
/// - Provides helpers to add/remove users, set permission, toggle online state
/// </summary>
public class UserManager : MonoBehaviour
{
    [Header("Storage")]
    [SerializeField] private bool enablePersistence = true;
    [SerializeField] private InventoryManager.StorageType storageType = InventoryManager.StorageType.JSON;
    [SerializeField] private bool prettyPrintJson = true;
    [SerializeField] private bool logging = true;

    [Header("Group/Storage Id")]
    [SerializeField] private string currentGroupId = "group_default";

    // Users list (JSON-serializable DTOs)
    private List<SerializableUserInfo> users = new List<SerializableUserInfo>();

    // Events (UI expects this signature)
    public Action<List<SerializableUserInfo>> OnUsersChanged;

    // Storage backend
    private IInventoryStorage storage;

    public static UserManager Instance { get; private set; }

    private void Awake()
    {
        // singleton convenience
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (storageType == InventoryManager.StorageType.JSON)
            storage = new JsonInventoryStorage(customSaveFolder: null, prettyPrint: prettyPrintJson, logging: logging);
        
    }

    private async void Start()
    {
        await LoadUsersAsync();
        // If no users exist, create a default admin user
        if (users.Count == 0)
        {
            Debug.Log("[UserManager] No users found - creating default admin user");
            await CreateDefaultUserAsync();
        }
    }

    #region Public API

    public IReadOnlyList<SerializableUserInfo> GetUsers()
    {
        return users.AsReadOnly();
    }

    public SerializableUserInfo GetUserByClientId(ulong clientId)
    {
        return users.FirstOrDefault(u => u.clientId == clientId);
    }

    public SerializableUserInfo GetUserByName(string userName)
    {
        return users.FirstOrDefault(u => string.Equals(u.userName, userName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task AddUserAsync(SerializableUserInfo user)
    {
        if (user == null) return;

        // If clientId is 0, generate a client id using timestamp (since there's no network)
        if (user.clientId == 0)
            user.clientId = (ulong)DateTime.Now.Ticks;

        if (string.IsNullOrEmpty(user.userName))
            user.userName = $"User_{user.clientId}";

        users.RemoveAll(u => u.clientId == user.clientId); // replace if exists
        users.Add(user);

        await SaveUsersAsync();
        OnUsersChanged?.Invoke(users);
    }

    public async Task RemoveUserAsync(ulong clientId)
    {
        int removed = users.RemoveAll(u => u.clientId == clientId);
        if (removed > 0)
        {
            await SaveUsersAsync();
            OnUsersChanged?.Invoke(users);
        }
    }

    public async Task SetUserPermissionAsync(ulong clientId, GroupPermission newPermission)
    {
        var idx = users.FindIndex(u => u.clientId == clientId);
        if (idx >= 0)
        {
            users[idx].permission = newPermission;
            await SaveUsersAsync();
            OnUsersChanged?.Invoke(users);
        }
    }

    public async Task SetUserOnlineStateAsync(ulong clientId, bool online)
    {
        var idx = users.FindIndex(u => u.clientId == clientId);
        if (idx >= 0)
        {
            users[idx].isOnline = online;
            // update connection time if becoming online
            if (online)
                users[idx].connectionTime = DateTime.Now.ToString("O");

            await SaveUsersAsync();
            OnUsersChanged?.Invoke(users);
        }
    }

    #endregion

    #region Persistence (users only)

    public async Task LoadUsersAsync()
    {
        if (!enablePersistence || storage == null) return;

        try
        {
            // Load from dedicated users file instead of group inventory
            var usersData = await LoadUsersDataAsync();

            if (usersData != null && usersData.Count > 0)
            {
                users = usersData;

                if (logging)
                    Debug.Log($"[UserManager] Loaded {users.Count} users from storage");
            }
            else
            {
                users = new List<SerializableUserInfo>();
                if (logging)
                    Debug.Log("[UserManager] No users found in storage");
            }

            OnUsersChanged?.Invoke(users);
        }
        catch (Exception e)
        {
            Debug.LogError($"[UserManager] LoadUsersAsync failed: {e.Message}");
        }
    }

    public async Task SaveUsersAsync()
    {
        if (!enablePersistence || storage == null) return;

        try
        {
            await SaveUsersDataAsync(users);
            if (logging) Debug.Log($"[UserManager] Saved {users.Count} users to storage.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[UserManager] SaveUsersAsync failed: {e.Message}");
        }
    }
    private async Task<List<SerializableUserInfo>> LoadUsersDataAsync()
    {
        string usersFilePath = System.IO.Path.Combine(
            Application.persistentDataPath,
            "InventoryData",
            "users.json"
        );

        if (!System.IO.File.Exists(usersFilePath))
            return new List<SerializableUserInfo>();

        string jsonData = await System.IO.File.ReadAllTextAsync(usersFilePath);

        var wrapper = JsonUtility.FromJson<UsersDataWrapper>(jsonData);
        return wrapper?.users ?? new List<SerializableUserInfo>();
    }

    private async Task SaveUsersDataAsync(List<SerializableUserInfo> usersData)
    {
        string folderPath = System.IO.Path.Combine(Application.persistentDataPath, "InventoryData");

        if (!System.IO.Directory.Exists(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
        }

        string usersFilePath = System.IO.Path.Combine(folderPath, "users.json");

        var wrapper = new UsersDataWrapper { users = usersData };
        string jsonData = JsonUtility.ToJson(wrapper, true);

        await System.IO.File.WriteAllTextAsync(usersFilePath, jsonData);
    }

    private async Task CreateDefaultUserAsync()
    {
        string userName = PlayerPrefs.GetString("UserName", Environment.UserName ?? "Admin");
        string userId = PlayerPrefs.GetString("UserId", "");

        if (string.IsNullOrEmpty(userId))
        {
            userId = Guid.NewGuid().ToString();
            PlayerPrefs.SetString("UserId", userId);
            PlayerPrefs.Save();
        }

        var defaultUser = new SerializableUserInfo
        {
            clientId = (ulong)DateTime.Now.Ticks,
            userId = userId,
            userName = userName,
            permission = GroupPermission.Admin,  // Make first user admin
            connectionTime = DateTime.Now.ToString("O"),
            isOnline = true
        };

        // Update PlayerPrefs to match
        PlayerPrefs.SetString("UserId", defaultUser.userId);
        PlayerPrefs.SetString("UserName", defaultUser.userName);
        PlayerPrefs.Save();

        await AddUserAsync(defaultUser);

        Debug.Log($"[UserManager] Created default admin user: {defaultUser.userName}");
    }

    #endregion
}

[System.Serializable]
public class UsersDataWrapper
{
    public List<SerializableUserInfo> users;
}