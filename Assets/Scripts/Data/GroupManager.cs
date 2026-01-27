using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using InventorySystem.Data;

/// <summary>
/// Manages all groups, memberships, and group-related operations
/// </summary>
public class GroupManager : MonoBehaviour
{
    [Header("Storage")]
    [SerializeField] private bool enablePersistence = true;
    [SerializeField] private bool logging = true;

    private List<Group> groups = new List<Group>();
    private string currentGroupId = "group_default";  // Currently active group
    private IInventoryStorage storage;

    // Events
    public Action<List<Group>> OnGroupsChanged;
    public Action<Group> OnCurrentGroupChanged;

    public static GroupManager Instance { get; private set; }

    private void Awake()
    {
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

        storage = new JsonInventoryStorage(logging: logging);
    }

    private async void Start()
    {
        await LoadGroupsAsync();

        // If no groups exist, create default group
        if (groups.Count == 0)
        {
            await CreateDefaultGroup();
        }
    }

    #region Public API

    public IReadOnlyList<Group> GetAllGroups()
    {
        return groups.AsReadOnly();
    }

    public Group GetGroupById(string groupId)
    {
        return groups.FirstOrDefault(g => g.groupId == groupId);
    }

    public Group GetCurrentGroup()
    {
        return GetGroupById(currentGroupId);
    }

    public async Task SetCurrentGroupAsync(string groupId)
    {
        if (groups.Any(g => g.groupId == groupId))
        {
            currentGroupId = groupId;
            PlayerPrefs.SetString("CurrentGroupId", groupId);
            PlayerPrefs.Save();

            OnCurrentGroupChanged?.Invoke(GetCurrentGroup());

            if (logging)
                Debug.Log($"[GroupManager] Set current group: {GetCurrentGroup()?.groupName}");
        }
    }

    public async Task<Group> CreateGroupAsync(string groupName, string creatorUserId, string description = "")
    {
        var newGroup = new Group(groupName, creatorUserId)
        {
            description = description
        };

        groups.Add(newGroup);
        Debug.Log($"[GroupManager] saving async");
        await SaveGroupsAsync();
        Debug.Log($"[GroupManager] saved async");
        OnGroupsChanged?.Invoke(groups);
        Debug.Log($"[GroupManager] invoked groups");

        if (logging)
            Debug.Log($"[GroupManager] Created group: {groupName}");

        return newGroup;
    }

    public async Task DeleteGroupAsync(string groupId)
    {
        var group = GetGroupById(groupId);
        if (group == null) return;

        groups.Remove(group);

        // If deleting current group, switch to another or create default
        if (currentGroupId == groupId)
        {
            if (groups.Count > 0)
            {
                await SetCurrentGroupAsync(groups[0].groupId);
            }
            else
            {
                await CreateDefaultGroup();
            }
        }

        await SaveGroupsAsync();
        OnGroupsChanged?.Invoke(groups);

        if (logging)
            Debug.Log($"[GroupManager] Deleted group: {group.groupName}");
    }

    public async Task UpdateGroupAsync(Group group)
    {
        var existingGroup = GetGroupById(group.groupId);
        if (existingGroup == null) return;

        // Update the group in the list
        int index = groups.IndexOf(existingGroup);
        groups[index] = group;

        group.lastActivity = DateTime.Now;

        await SaveGroupsAsync();
        OnGroupsChanged?.Invoke(groups);

        if (currentGroupId == group.groupId)
        {
            OnCurrentGroupChanged?.Invoke(group);
        }
    }

    public async Task AddMemberToGroupAsync(string groupId, string userId, GroupPermission permission = GroupPermission.Member)
    {
        var group = GetGroupById(groupId);
        if (group == null) return;

        group.AddMember(userId, permission);

        await SaveGroupsAsync();
        OnGroupsChanged?.Invoke(groups);

        if (logging)
            Debug.Log($"[GroupManager] Added user {userId} to group {group.groupName}");
    }

    public async Task RemoveMemberFromGroupAsync(string groupId, string userId)
    {
        var group = GetGroupById(groupId);
        if (group == null) return;

        // Don't allow removing the creator
        if (group.creatorUserId == userId)
        {
            Debug.LogWarning("[GroupManager] Cannot remove group creator");
            return;
        }

        group.RemoveMember(userId);

        await SaveGroupsAsync();
        OnGroupsChanged?.Invoke(groups);

        if (logging)
            Debug.Log($"[GroupManager] Removed user {userId} from group {group.groupName}");
    }

    public async Task UpdateMemberPermissionAsync(string groupId, string userId, GroupPermission newPermission)
    {
        var group = GetGroupById(groupId);
        if (group == null) return;

        var member = group.GetMember(userId);
        if (member == null) return;

        member.permission = newPermission;
        group.lastActivity = DateTime.Now;

        await SaveGroupsAsync();
        OnGroupsChanged?.Invoke(groups);

        if (logging)
            Debug.Log($"[GroupManager] Updated permission for {userId} in {group.groupName}");
    }

    public async Task AddCharacterToGroupAsync(string groupId, string userId, string characterId)
    {
        var group = GetGroupById(groupId);
        if (group == null) return;

        var member = group.GetMember(userId);
        if (member == null)
        {
            Debug.LogWarning($"[GroupManager] User {userId} is not a member of group {groupId}");
            return;
        }

        if (!member.characterIds.Contains(characterId))
        {
            member.characterIds.Add(characterId);
            group.lastActivity = DateTime.Now;

            await SaveGroupsAsync();
            OnGroupsChanged?.Invoke(groups);

            if (logging)
                Debug.Log($"[GroupManager] Added character {characterId} to group {group.groupName}");
        }
    }

    public async Task RemoveCharacterFromGroupAsync(string groupId, string characterId)
    {
        var group = GetGroupById(groupId);
        if (group == null) return;

        foreach (var member in group.members)
        {
            if (member.characterIds.Remove(characterId))
            {
                group.lastActivity = DateTime.Now;
                await SaveGroupsAsync();
                OnGroupsChanged?.Invoke(groups);

                if (logging)
                    Debug.Log($"[GroupManager] Removed character {characterId} from group {group.groupName}");

                return;
            }
        }
    }

    public List<PlayerCharacter> GetGroupCharacters(string groupId)
    {
        var group = GetGroupById(groupId);
        if (group == null) return new List<PlayerCharacter>();

        var characterManager = CharacterManager.Instance;
        if (characterManager == null) return new List<PlayerCharacter>();

        var groupCharacters = new List<PlayerCharacter>();

        foreach (var member in group.members)
        {
            foreach (var characterId in member.characterIds)
            {
                var character = characterManager.GetCharacterById(characterId);
                if (character != null)
                {
                    groupCharacters.Add(character);
                }
            }
        }

        return groupCharacters;
    }

    public List<Group> GetUserGroups(string userId)
    {
        return groups.Where(g => g.HasMember(userId)).ToList();
    }

    #endregion

    #region Persistence

    private async Task LoadGroupsAsync()
    {
        if (!enablePersistence || storage == null) return;

        try
        {
            // Try to load from a dedicated groups file
            var groupsData = await LoadGroupsDataAsync();

            if (groupsData != null && groupsData.Count > 0)
            {
                groups = groupsData.ConvertAll(sg => sg.ToGroup());

                if (logging)
                    Debug.Log($"[GroupManager] Loaded {groups.Count} groups");
            }
            else
            {
                groups = new List<Group>();
                if (logging)
                    Debug.Log("[GroupManager] No groups found in storage");
            }

            // Load current group ID from PlayerPrefs
            currentGroupId = PlayerPrefs.GetString("CurrentGroupId", "group_default");

            // Validate current group exists
            if (!groups.Any(g => g.groupId == currentGroupId))
            {
                if (groups.Count > 0)
                {
                    currentGroupId = groups[0].groupId;
                }
            }

            OnGroupsChanged?.Invoke(groups);
            OnCurrentGroupChanged?.Invoke(GetCurrentGroup());
        }
        catch (Exception e)
        {
            Debug.LogError($"[GroupManager] LoadGroupsAsync failed: {e.Message}");
        }
    }

    private async Task SaveGroupsAsync()
    {
        if (!enablePersistence || storage == null) return;

        try
        {
            var serializableGroups = groups.ConvertAll(SerializableGroup.FromGroup);
            await SaveGroupsDataAsync(serializableGroups);

            if (logging)
                Debug.Log($"[GroupManager] Saved {groups.Count} groups");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GroupManager] SaveGroupsAsync failed: {e.Message}");
        }
    }

    // These methods handle the actual file I/O
    private async Task<List<SerializableGroup>> LoadGroupsDataAsync()
    {
        // For now, we'll store groups in a separate structure
        // This is a simple implementation - you might want to enhance this

        string groupsFilePath = System.IO.Path.Combine(
            Application.persistentDataPath,
            "InventoryData",
            "groups.json"
        );

        if (!System.IO.File.Exists(groupsFilePath))
            return new List<SerializableGroup>();

        string jsonData = await System.IO.File.ReadAllTextAsync(groupsFilePath);

        var wrapper = JsonUtility.FromJson<GroupsDataWrapper>(jsonData);
        return wrapper?.groups ?? new List<SerializableGroup>();
    }

    private async Task SaveGroupsDataAsync(List<SerializableGroup> groupsData)
    {
        string folderPath = System.IO.Path.Combine(Application.persistentDataPath, "InventoryData");

        if (!System.IO.Directory.Exists(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
        }

        string groupsFilePath = System.IO.Path.Combine(folderPath, "groups.json");

        var wrapper = new GroupsDataWrapper { groups = groupsData };
        string jsonData = JsonUtility.ToJson(wrapper, true);

        await System.IO.File.WriteAllTextAsync(groupsFilePath, jsonData);
    }

    private async Task CreateDefaultGroup()
    {
        string userId = PlayerPrefs.GetString("UserId", Guid.NewGuid().ToString());
        string userName = PlayerPrefs.GetString("UserName", "Player");

        var defaultGroup = await CreateGroupAsync("My Campaign", userId, "Default campaign group");
        await SetCurrentGroupAsync(defaultGroup.groupId);

        if (logging)
            Debug.Log("[GroupManager] Created default group");
    }

    #endregion

    // Wrapper class for JSON serialization
    [System.Serializable]
    private class GroupsDataWrapper
    {
        public List<SerializableGroup> groups;
    }
}