using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using InventorySystem.Data;

/// <summary>
/// Manages personal character and item storage outside of groups
/// This handles characters that aren't in any group and their personal items
/// </summary>
public class PersonalStorageManager : MonoBehaviour
{
    [Header("Storage")]
    [SerializeField] private bool enablePersistence = true;
    [SerializeField] private bool logging = true;

    private string currentUserId;
    private List<PlayerCharacter> personalCharacters = new List<PlayerCharacter>();
    private List<PersonalInventoryItem> personalItems = new List<PersonalInventoryItem>();

    // Events
    public Action<List<PlayerCharacter>> OnPersonalCharactersChanged;
    public Action<List<PersonalInventoryItem>> OnPersonalItemsChanged;

    public static PersonalStorageManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private async void Start()
    {
        currentUserId = PlayerPrefs.GetString("UserId", "");

        if (string.IsNullOrEmpty(currentUserId))
        {
            Debug.LogWarning("[PersonalStorage] No userId found in PlayerPrefs");
            return;
        }

        await LoadPersonalStorageAsync();
    }

    #region Public API

    /// <summary>
    /// Load all personal storage for current user
    /// </summary>
    public async Task LoadPersonalStorageAsync()
    {
        if (string.IsNullOrEmpty(currentUserId))
        {
            Debug.LogWarning("[PersonalStorage] Cannot load - no userId");
            return;
        }

        try
        {
            var data = await LoadPersonalDataAsync();

            if (data != null)
            {
                personalCharacters = data.characters?.Select(sc => sc.ToPlayerCharacter()).ToList()
                    ?? new List<PlayerCharacter>();

                personalItems = data.items ?? new List<PersonalInventoryItem>();

                if (logging)
                    Debug.Log($"[PersonalStorage] Loaded {personalCharacters.Count} characters, {personalItems.Count} items for user {currentUserId}");
            }
            else
            {
                personalCharacters = new List<PlayerCharacter>();
                personalItems = new List<PersonalInventoryItem>();

                if (logging)
                    Debug.Log($"[PersonalStorage] No existing data for user {currentUserId}");
            }

            OnPersonalCharactersChanged?.Invoke(personalCharacters);
            OnPersonalItemsChanged?.Invoke(personalItems);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PersonalStorage] Load failed: {e.Message}");
        }
    }

    /// <summary>
    /// Save all personal storage for current user
    /// </summary>
    public async Task SavePersonalStorageAsync()
    {
        if (string.IsNullOrEmpty(currentUserId))
        {
            Debug.LogWarning("[PersonalStorage] Cannot save - no userId");
            return;
        }

        if (!enablePersistence)
        {
            Debug.Log("[PersonalStorage] Persistence disabled, skipping save");
            return;
        }

        try
        {
            var data = new PersonalStorageData
            {
                userId = currentUserId,
                userName = PlayerPrefs.GetString("UserName", ""),
                lastSaved = DateTime.Now.ToString("O"),
                characters = personalCharacters.Select(SerializablePlayerCharacter.FromPlayerCharacter).ToList(),
                items = personalItems
            };

            await SavePersonalDataAsync(data);

            if (logging)
                Debug.Log($"[PersonalStorage] Saved {personalCharacters.Count} characters, {personalItems.Count} items");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PersonalStorage] Save failed: {e.Message}");
        }
    }

    /// <summary>
    /// Get all personal characters (not in any group)
    /// </summary>
    public List<PlayerCharacter> GetPersonalCharacters()
    {
        return personalCharacters.Where(c => string.IsNullOrEmpty(c.groupId)).ToList();
    }

    /// <summary>
    /// Get all characters owned by current user (including those in groups)
    /// </summary>
    public List<PlayerCharacter> GetAllCharacters()
    {
        return new List<PlayerCharacter>(personalCharacters);
    }

    /// <summary>
    /// Get a specific character by ID
    /// </summary>
    public PlayerCharacter GetCharacterById(string characterId)
    {
        return personalCharacters.FirstOrDefault(c => c.characterId == characterId);
    }

    /// <summary>
    /// Get items for a specific character
    /// </summary>
    public List<InventoryItem> GetCharacterItems(string characterId)
    {
        var items = personalItems
            .Where(pi => pi.ownerCharacterId == characterId)
            .Select(pi => pi.ToInventoryItem())
            .ToList();

        if (logging)
            Debug.Log($"[PersonalStorage] Found {items.Count} items for character {characterId}");

        return items;
    }

    /// <summary>
    /// Add or update a character
    /// </summary>
    public async Task AddCharacterAsync(PlayerCharacter character)
    {
        if (character == null)
        {
            Debug.LogWarning("[PersonalStorage] Cannot add null character");
            return;
        }

        personalCharacters.RemoveAll(c => c.characterId == character.characterId);
        personalCharacters.Add(character);

        await SavePersonalStorageAsync();
        OnPersonalCharactersChanged?.Invoke(personalCharacters);

        if (logging)
            Debug.Log($"[PersonalStorage] Added/updated character: {character.characterName}");
    }

    /// <summary>
    /// Remove a character
    /// </summary>
    public async Task RemoveCharacterAsync(string characterId)
    {
        int removed = personalCharacters.RemoveAll(c => c.characterId == characterId);

        if (removed > 0)
        {
            // Also remove all items owned by this character
            personalItems.RemoveAll(pi => pi.ownerCharacterId == characterId);

            await SavePersonalStorageAsync();
            OnPersonalCharactersChanged?.Invoke(personalCharacters);
            OnPersonalItemsChanged?.Invoke(personalItems);

            if (logging)
                Debug.Log($"[PersonalStorage] Removed character and their items");
        }
    }

    /// <summary>
    /// Add an item to a character's personal inventory
    /// </summary>
    public async Task AddItemAsync(InventoryItem item, string characterId)
    {
        if (item == null || string.IsNullOrEmpty(characterId))
        {
            Debug.LogWarning("[PersonalStorage] Cannot add item - invalid parameters");
            return;
        }

        var personalItem = PersonalInventoryItem.FromInventoryItem(item, characterId);
        personalItems.Add(personalItem);

        await SavePersonalStorageAsync();
        OnPersonalItemsChanged?.Invoke(personalItems);

        if (logging)
            Debug.Log($"[PersonalStorage] Added {item.itemName} to character {characterId}");
    }

    /// <summary>
    /// Update item quantity
    /// </summary>
    public async Task UpdateItemQuantityAsync(string itemId, string characterId, int newQuantity)
    {
        var item = personalItems.FirstOrDefault(pi =>
            pi.itemId == itemId && pi.ownerCharacterId == characterId);

        if (item != null)
        {
            if (newQuantity <= 0)
            {
                personalItems.Remove(item);
                if (logging)
                    Debug.Log($"[PersonalStorage] Removed item {itemId} (quantity 0)");
            }
            else
            {
                item.quantity = newQuantity;
                if (logging)
                    Debug.Log($"[PersonalStorage] Updated item {itemId} quantity to {newQuantity}");
            }

            await SavePersonalStorageAsync();
            OnPersonalItemsChanged?.Invoke(personalItems);
        }
    }

    /// <summary>
    /// Remove an item
    /// </summary>
    public async Task RemoveItemAsync(string itemId, string characterId)
    {
        int removed = personalItems.RemoveAll(pi =>
            pi.itemId == itemId && pi.ownerCharacterId == characterId);

        if (removed > 0)
        {
            await SavePersonalStorageAsync();
            OnPersonalItemsChanged?.Invoke(personalItems);

            if (logging)
                Debug.Log($"[PersonalStorage] Removed item {itemId}");
        }
    }

    /// <summary>
    /// Move character to a group (updates groupId, doesn't move items)
    /// </summary>
    public async Task MoveCharacterToGroupAsync(string characterId, string groupId)
    {
        var character = personalCharacters.FirstOrDefault(c => c.characterId == characterId);

        if (character != null)
        {
            character.groupId = groupId;
            await SavePersonalStorageAsync();
            OnPersonalCharactersChanged?.Invoke(personalCharacters);

            if (logging)
                Debug.Log($"[PersonalStorage] Moved character {character.characterName} to group {groupId}");
        }
    }

    /// <summary>
    /// Remove character from group (clears groupId)
    /// </summary>
    public enum LeaveGroupItemChoice { ReturnToGroup, KeepAsPersonal, Prompt }

    public async Task RemoveCharacterFromGroupAsync(
        string characterId,
        LeaveGroupItemChoice itemChoice = LeaveGroupItemChoice.Prompt)
    {
        var character = personalCharacters.FirstOrDefault(c => c.characterId == characterId);
        if (character == null) return;

        // Get items this character owns in the group
        var inventoryManager = InventoryManager.Instance;
        if (inventoryManager != null)
        {
            var ownedItems = inventoryManager.GetCharacterOwnerships(characterId);

            foreach (var ownership in ownedItems)
            {
                var groupItem = inventoryManager.GetCurrentInventory()
                    .FirstOrDefault(i => i.itemId == ownership.itemId);

                if (groupItem == null) continue;

                if (itemChoice == LeaveGroupItemChoice.ReturnToGroup)
                {
                    // Return to group pool (unallocated)
                    await inventoryManager.ReturnOwnershipAsync(
                        ownership.itemId, characterId, ownership.quantityOwned);
                    Debug.Log($"[PersonalStorage] Returned {groupItem.itemName} to group");
                }
                else if (itemChoice == LeaveGroupItemChoice.KeepAsPersonal)
                {
                    // Transfer to personal storage
                    var personalItem = new InventoryItem(groupItem.itemName, groupItem.category)
                    {
                        description = groupItem.description,
                        quantity = ownership.quantityOwned,
                        weight = groupItem.weight,
                        valueInGold = groupItem.valueInGold,
                        thumbnailUrl = groupItem.thumbnailUrl,
                        sourceUrl = groupItem.sourceUrl
                    };

                    await inventoryManager.ReturnOwnershipAsync(
                        ownership.itemId, characterId, ownership.quantityOwned);
                    await AddItemAsync(personalItem, characterId);

                    Debug.Log($"[PersonalStorage] Transferred {groupItem.itemName} to personal storage");
                }
                // NOTE: LeaveGroupItemChoice.Prompt is handled by the UI (show dialog first)
            }
        }

        character.groupId = null;
        await SavePersonalStorageAsync();
        OnPersonalCharactersChanged?.Invoke(personalCharacters);
        Debug.Log($"[PersonalStorage] Character {character.characterName} left group");
    }

    #endregion

    #region Persistence

    private async Task<PersonalStorageData> LoadPersonalDataAsync()
    {
        string filePath = GetPersonalStorageFilePath();

        if (!System.IO.File.Exists(filePath))
        {
            if (logging)
                Debug.Log($"[PersonalStorage] No file found at: {filePath}");
            return null;
        }

        string json = await System.IO.File.ReadAllTextAsync(filePath);

        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning($"[PersonalStorage] File is empty: {filePath}");
            return null;
        }

        return JsonUtility.FromJson<PersonalStorageData>(json);
    }

    private async Task SavePersonalDataAsync(PersonalStorageData data)
    {
        string folderPath = System.IO.Path.Combine(Application.persistentDataPath, "InventoryData");

        if (!System.IO.Directory.Exists(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
            if (logging)
                Debug.Log($"[PersonalStorage] Created folder: {folderPath}");
        }

        string filePath = GetPersonalStorageFilePath();
        string json = JsonUtility.ToJson(data, true);

        await System.IO.File.WriteAllTextAsync(filePath, json);

        if (logging)
            Debug.Log($"[PersonalStorage] Saved to: {filePath}");
    }

    private string GetPersonalStorageFilePath()
    {
        return System.IO.Path.Combine(
            Application.persistentDataPath,
            "InventoryData",
            $"personal_{currentUserId}.json"
        );
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Get storage info for debugging
    /// </summary>
    public string GetStorageInfo()
    {
        return $"User: {currentUserId}, Characters: {personalCharacters.Count}, Items: {personalItems.Count}";
    }

    /// <summary>
    /// Clear all personal storage (use with caution!)
    /// </summary>
    public async Task ClearAllDataAsync()
    {
        personalCharacters.Clear();
        personalItems.Clear();

        await SavePersonalStorageAsync();

        OnPersonalCharactersChanged?.Invoke(personalCharacters);
        OnPersonalItemsChanged?.Invoke(personalItems);

        if (logging)
            Debug.Log("[PersonalStorage] Cleared all data");
    }

    #endregion
}

// ============================================================================
// DATA STRUCTURES
// ============================================================================

/// <summary>
/// Personal storage data for a single user
/// </summary>
[System.Serializable]
public class PersonalStorageData
{
    public string userId;
    public string userName;
    public string lastSaved;
    public List<SerializablePlayerCharacter> characters;
    public List<PersonalInventoryItem> items;

    public PersonalStorageData()
    {
        characters = new List<SerializablePlayerCharacter>();
        items = new List<PersonalInventoryItem>();
    }
}

/// <summary>
/// Personal inventory item (exists outside of group inventory)
/// </summary>
[System.Serializable]
public class PersonalInventoryItem
{
    public string itemId;
    public string itemName;
    public string description;
    public ItemCategory category;
    public int quantity;
    public float weight;
    public int valueInGold;
    public string ownerCharacterId;  // Which character owns this
    public string thumbnailUrl;
    public string dateAdded;
    public string lastModified;

    public static PersonalInventoryItem FromInventoryItem(InventoryItem item, string characterId)
    {
        return new PersonalInventoryItem
        {
            itemId = item.itemId ?? Guid.NewGuid().ToString(),
            itemName = item.itemName,
            description = item.description,
            category = item.category,
            quantity = item.quantity,
            weight = item.weight,
            valueInGold = item.valueInGold,
            ownerCharacterId = characterId,
            thumbnailUrl = item.thumbnailUrl,
            dateAdded = item.dateAdded.ToString("O"),
            lastModified = DateTime.Now.ToString("O")
        };
    }

    public InventoryItem ToInventoryItem()
    {
        DateTime parsedDateAdded = DateTime.TryParse(dateAdded, out parsedDateAdded) ? parsedDateAdded : DateTime.Now;
        DateTime parsedDateModified = DateTime.TryParse(lastModified, out parsedDateModified) ? parsedDateModified : DateTime.Now;

        var item = new InventoryItem(itemName, category)
        {
            itemId = itemId,
            description = description ?? "",
            quantity = quantity,
            weight = weight,
            valueInGold = valueInGold,
            currentOwner = ownerCharacterId,
            thumbnailUrl = thumbnailUrl ?? "",
            dateAdded = parsedDateAdded,
            lastModified = parsedDateModified
        };

        // Initialize collections
        if (item.properties == null)
            item.properties = new Dictionary<string, string>();

        if (item.playerNotes == null)
            item.playerNotes = new List<PlayerNote>();

        return item;
    }
}