// Assets/Scripts/InventoryManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using InventorySystem.Data;

/// <summary>
/// Non-networked inventory manager (items only).
/// - Uses IInventoryStorage (JsonInventoryStorage by default)
/// - Optional API mode (REST endpoints)
/// - Provides compatibility wrappers for older UI calling style:
///     AddItem(item), UpdateItemQuantity(itemId, qty), RemoveItem(itemId), IsConnected()
/// - Raises OnInventoryChanged events.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    [Header("Storage")]
    [SerializeField] private bool enablePersistence = true;
    [SerializeField] private StorageType storageType = StorageType.JSON;
    [SerializeField] private bool enableAutoSave = true;
    [SerializeField] private float autoSaveIntervalMinutes = 5f;
    [SerializeField] private bool prettyPrintJson = true;
    [SerializeField] private bool logging = true;

    [Header("API (optional)")]
    [SerializeField] private bool apiMode = false;
    [SerializeField] private string baseApiUrl = "https://localhost:5001/api";

    [Header("Misc")]
    [SerializeField] private int maxInventoryItems = 500;

    public enum StorageType { JSON } // future: SQLite, Cloud

    // Events for UI
    public Action<List<InventoryItem>> OnInventoryChanged;
    public Action<string> OnInventoryMessage;

    // Internal state
    private IInventoryStorage storage;
    private List<InventoryItem> localInventoryCache = new List<InventoryItem>();
    private Dictionary<string, InventoryItem> itemLookup = new Dictionary<string, InventoryItem>();
    private List<ItemOwnership> cachedOwnerships = new List<ItemOwnership>();
    private GroupInventory currentGroupInventory;



    private float lastAutoSaveTime;
    private string currentGroupId;
    private string currentGroupName = "Party Inventory";
    private int version = 0;

    // Singleton
    public static InventoryManager Instance { get; private set; }

    private void Awake()
    {
        // Singleton convenience
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

        // Create storage
        if (storageType == StorageType.JSON)
        {
            storage = new JsonInventoryStorage(customSaveFolder: null, prettyPrint: prettyPrintJson, logging: logging);
        }

        lastAutoSaveTime = Time.time;
    }

    private async void Start()
    {
        // Get current group ID from GroupManager
        UpdateCurrentGroupId();

        await InitializeAsync();
    }

    private void Update()
    {
        if (enableAutoSave && Time.time - lastAutoSaveTime >= autoSaveIntervalMinutes * 60f)
        {
            _ = SaveCurrentInventoryAsync();
            lastAutoSaveTime = Time.time;
        }
    }

    private async Task InitializeAsync()
    {
        if (enablePersistence && storage != null)
        {
            try
            {
                var data = await storage.LoadAsync(currentGroupId);
                if (data != null)
                {
                    RestoreFromPersistenceData(data);
                    Log($"Loaded {localInventoryCache.Count} items from local storage.");

                    if( data.itemOwnerships != null && data.itemOwnerships.Count >0)
                    {
                        cachedOwnerships = data.itemOwnerships.Select(so => so.ToOwnership()).ToList();
                        Log($"Loaded {cachedOwnerships.Count} owndership records");
                    }
                }
                else
                {
                    CreateSampleInventory();
                    await SaveCurrentInventoryAsync();
                    Log("No saved data: created sample inventory and saved locally.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[InventoryManager] Initialize error: {e.Message}");
            }
        }

        if (apiMode)
        {
            try
            {
                await FetchFullInventoryFromApiAsync();
                Log("Fetched inventory from API (apiMode).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InventoryManager] API fetch failed: {e.Message}");
            }
        }

        if (cachedOwnerships.Count == 0)
        {
            cachedOwnerships = await LoadOwnershipAsync();
        }

        // notify UI
        OnInventoryChanged?.Invoke(GetCurrentInventory());
    }

    private void UpdateCurrentGroupId()
    {
        var groupManager = GroupManager.Instance;
        if (groupManager != null)
        {
            var currentGroup = groupManager.GetCurrentGroup();
            if (currentGroup != null)
            {
                currentGroupId = currentGroup.groupId;
                currentGroupName = currentGroup.groupName;
                Debug.Log($"[InventoryManager] Using group: {currentGroupName} ({currentGroupId})");
            }
            else
            {
                currentGroupId = "group_default";
                Debug.LogWarning("[InventoryManager] No current group, using default");
            }
        }
    }

    #region Public API (async)

    /// <summary>
    /// Get the full list of items in the current group inventory
    /// </summary>
    public List<InventoryItem> GetCurrentInventory()
    {
        if (currentGroupInventory == null)
            return new List<InventoryItem>();

        return currentGroupInventory.items ?? new List<InventoryItem>();
    }

    public async Task AddItemAsync(InventoryItem item)
    {
        if (item == null) return;

        if (localInventoryCache.Count >= maxInventoryItems)
        {
            Notify("Inventory is full.");
            return;
        }

        // Merge with existing stack when same name+category
        var existing = localInventoryCache.Find(i => i.itemName == item.itemName && i.category == item.category);
        if (existing != null)
        {
            existing.quantity += item.quantity;
            existing.lastModified = DateTime.Now;
            itemLookup[existing.itemId] = existing;
            Notify($"Combined {item.quantity}x {item.itemName} with existing stack.");
        }
        else
        {
            if (string.IsNullOrEmpty(item.itemId))
                item.itemId = Guid.NewGuid().ToString();

            item.dateAdded = DateTime.Now;
            item.lastModified = DateTime.Now;

            localInventoryCache.Add(item);
            itemLookup[item.itemId] = item;
            Notify($"Added {item.itemName}.");
        }

        version++;

        if (apiMode)
        {
            try
            {
                await PostAddItemToApiAsync(SerializableInventoryItem.FromInventoryItem(item));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InventoryManager] API add failed: {e.Message}");
            }
        }
        else if (enablePersistence)
        {
            await SaveCurrentInventoryAsync();
        }

        OnInventoryChanged?.Invoke(GetCurrentInventory());
    }
    /*
    public async Task UpdateItemQuantityAsync(string itemId, int newQuantity)
    {
        if (!itemLookup.ContainsKey(itemId)) return;

        var item = itemLookup[itemId];

        if (newQuantity <= 0)
        {
            await RemoveItemAsync(itemId);
            return;
        }

        item.quantity = newQuantity;
        item.lastModified = DateTime.Now;
        itemLookup[itemId] = item;

        int idx = localInventoryCache.FindIndex(i => i.itemId == itemId);
        if (idx >= 0) localInventoryCache[idx] = item;

        version++;

        if (apiMode)
        {
            try
            {
                await PostUpdateQuantityToApiAsync(itemId, newQuantity);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InventoryManager] API update failed: {e.Message}");
            }
        }
        else if (enablePersistence)
        {
            await SaveCurrentInventoryAsync();
        }

        OnInventoryChanged?.Invoke(GetCurrentInventory());
    }
    */

    /// <summary>
    /// Update item quantity in group inventory
    /// Used when items are added or removed from total pool
    /// </summary>
    public async Task UpdateItemQuantityAsync(string itemId, int newQuantity)
    {
        if (currentGroupInventory == null)
        {
            Debug.LogWarning("[InventoryManager] No current group inventory");
            return;
        }

        var item = currentGroupInventory.items.FirstOrDefault(i => i.itemId == itemId);
        if (item == null)
        {
            Debug.LogWarning($"[InventoryManager] Item {itemId} not found in group");
            return;
        }

        if (newQuantity <= 0)
        {
            // Remove item entirely
            currentGroupInventory.items.Remove(item);

            // Also remove all ownership records for this item
            currentGroupInventory.itemOwnerships.RemoveAll(o => o.itemId == itemId);

            Debug.Log($"[InventoryManager] Removed item {itemId} from group (quantity 0)");
        }
        else
        {
            item.quantity = newQuantity;
            Debug.Log($"[InventoryManager] Updated item {itemId} quantity to {newQuantity}");
        }

        currentGroupInventory.lastModified = DateTime.Now;
        currentGroupInventory.version++;

        await SaveInventoryAsync();
        OnInventoryChanged?.Invoke(currentGroupInventory.items);
    }

    public async Task RemoveItemAsync(string itemId)
    {
        if (!itemLookup.ContainsKey(itemId)) return;

        var item = itemLookup[itemId];

        bool removed = localInventoryCache.RemoveAll(i => i.itemId == itemId) > 0;
        if (removed)
        {
            itemLookup.Remove(itemId);
            Notify($"Removed {item.itemName}.");

            version++;

            if (apiMode)
            {
                try
                {
                    await PostRemoveItemToApiAsync(itemId);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[InventoryManager] API remove failed: {e.Message}");
                }
            }
            else if (enablePersistence)
            {
                await SaveCurrentInventoryAsync();
            }

            OnInventoryChanged?.Invoke(GetCurrentInventory());
        }
    }

    private async Task SaveInventoryAsync()
    {
        if (currentGroupInventory == null || storage == null) return;
        await storage.SaveAsync(new InventoryPersistenceData
        {
            groupId = currentGroupId,
            groupName = currentGroupName,
            version = currentGroupInventory.version,
            lastSaved = DateTime.Now.ToString("O"),
            items = currentGroupInventory.items.Select(i => SerializableInventoryItem.FromInventoryItem(i)).ToList(),
            itemOwnerships = currentGroupInventory.itemOwnerships.Select(o => SerializableItemOwnership.FromOwnership(o)).ToList()
        });
    }

    public async Task ForceSaveAsync()
    {
        if (apiMode)
        {
            await PushFullInventoryToApiAsync();
        }
        else
        {
            await SaveCurrentInventoryAsync();
        }
    }
    

    public async Task SaveOwnershipAsync(List<ItemOwnership> ownerships)
    {
        if (!enablePersistence || storage == null) return;

        try
        {
            var data = await storage.LoadAsync(currentGroupId) ?? new InventoryPersistenceData();
            data.itemOwnerships = ownerships
                .Select(SerializableItemOwnership.FromOwnership)
                .ToList();

            //Update the groupid and timestamp
            data.groupId = currentGroupId;
            data.lastSaved = DateTime.Now.ToString("0");               

            await storage.SaveAsync(data);

            if (logging)
                Debug.Log($"[InventoryManager] Saved {ownerships.Count} ownership records");
        }
        catch (Exception e)
        {
            Debug.LogError($"[InventoryManager] SaveOwnershipAsync failed: {e.Message}");
        }
    }

    public async Task<List<ItemOwnership>> LoadOwnershipAsync()
    {
        if (!enablePersistence || storage == null) return new List<ItemOwnership>();

        try
        {
            var data = await storage.LoadAsync(currentGroupId);
            if (data != null && data.itemOwnerships != null)
            {
                var ownerships = data.itemOwnerships
                    .Select(so => so.ToOwnership())
                    .ToList();

                if (logging)
                    Debug.Log($"[InventoryManager] Loaded {ownerships.Count} ownership records");

                return ownerships;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[InventoryManager] LoadOwnershipAsync failed: {e.Message}");
        }

        return new List<ItemOwnership>();
    }

    public List<ItemOwnership> GetOwnershipForItem(string itemId)
    {
        return cachedOwnerships.FindAll(o => o.itemId == itemId);
    }

    /// <summary>
    /// Get all ownership records for current group
    /// </summary>
    public List<ItemOwnership> GetAllOwnerships()
    {
        if (currentGroupInventory == null)
            return new List<ItemOwnership>();

        return currentGroupInventory.itemOwnerships ?? new List<ItemOwnership>();
    }


    /// <summary>
    /// Get ownership records for a specific character
    /// </summary>
    public List<ItemOwnership> GetCharacterOwnerships(string characterId)
    {
        if (currentGroupInventory == null)
            return new List<ItemOwnership>();

        return currentGroupInventory.itemOwnerships
            .Where(o => o.characterId == characterId)
            .ToList();
    }

    /// <summary>
    /// Get how much of an item a character owns
    /// </summary>
    public int GetCharacterOwnership(string itemId, string characterId)
    {
        if (currentGroupInventory == null) return 0;

        var ownership = currentGroupInventory.itemOwnerships
            .FirstOrDefault(o => o.itemId == itemId && o.characterId == characterId);

        return ownership?.quantityOwned ?? 0;
    }

    /// <summary>
    /// Check if an item exists in group inventory
    /// </summary>
    public bool ItemExistsInGroup(string itemId)
    {
        if (currentGroupInventory == null) return false;
        return currentGroupInventory.items.Any(i => i.itemId == itemId);
    }
    public int GetUnallocatedQuantity(string itemId)
{
    if (currentGroupInventory == null) return 0;
    return currentGroupInventory.GetUnallocatedQuantity(itemId);
}

    /// <summary>
    /// Return items from a character back to group storage (unclaimed)
    /// Used when a character returns shared items or transfers to personal
    /// </summary>
    public async Task ReturnOwnershipAsync(string itemId, string characterId, int quantity)
    {
        if (currentGroupInventory == null)
        {
            Debug.LogWarning("[InventoryManager] No current group inventory");
            return;
        }

        // Use the existing ReturnItem method from GroupInventory
        bool success = currentGroupInventory.ReturnItem(itemId, characterId, quantity);

        if (success)
        {
            await SaveInventoryAsync();
            OnInventoryChanged?.Invoke(currentGroupInventory.items);

            Debug.Log($"[InventoryManager] Character {characterId} returned {quantity}x item {itemId} to pool");
        }
        else
        {
            Debug.LogWarning($"[InventoryManager] Failed to return {quantity}x item {itemId}");
            throw new InvalidOperationException($"Cannot return {quantity} of item - insufficient ownership");
        }
    }


    /// <summary>
    /// Update ownership quantity for a character
    /// If newQuantity is 0, removes the ownership record
    /// If character doesn't own it yet, creates new ownership
    /// </summary>
    public async Task UpdateOwnershipAsync(string itemId, string characterId, int quantityChange)
    {
        if (currentGroupInventory == null)
        {
            Debug.LogWarning("[InventoryManager] No current group inventory");
            return;
        }

        // Find existing ownership
        var ownership = currentGroupInventory.itemOwnerships
            .FirstOrDefault(o => o.itemId == itemId && o.characterId == characterId);

        if (ownership != null)
        {
            // Update existing
            int newQuantity = ownership.quantityOwned + quantityChange;

            if (newQuantity <= 0)
            {
                // Remove ownership
                currentGroupInventory.itemOwnerships.Remove(ownership);
                Debug.Log($"[InventoryManager] Removed ownership of {itemId} from character {characterId}");
            }
            else
            {
                ownership.quantityOwned = newQuantity;
                Debug.Log($"[InventoryManager] Updated ownership of {itemId} to {newQuantity} for character {characterId}");
            }
        }
        else if (quantityChange > 0)
        {
            // Create new ownership
            currentGroupInventory.itemOwnerships.Add(
                new ItemOwnership(itemId, characterId, quantityChange)
            );
            Debug.Log($"[InventoryManager] Created new ownership of {itemId} ({quantityChange}) for character {characterId}");
        }
        else
        {
            Debug.LogWarning($"[InventoryManager] Cannot create negative ownership for {itemId}");
            return;
        }

        currentGroupInventory.lastModified = DateTime.Now;
        currentGroupInventory.version++;

        await SaveInventoryAsync();
        OnInventoryChanged?.Invoke(currentGroupInventory.items);
    }


    /// <summary>
    /// Check if a character owns enough of an item
    /// </summary>
    public bool CharacterOwnsQuantity(string itemId, string characterId, int quantity)
    {
        if (currentGroupInventory == null) return false;

        var ownership = currentGroupInventory.itemOwnerships
            .FirstOrDefault(o => o.itemId == itemId && o.characterId == characterId);

        return ownership != null && ownership.quantityOwned >= quantity;
    }

    #endregion

    #region Compatibility wrappers (sync-style) - UI expects these

    // Legacy: AddItem(item) -> fire-and-forget
    public void AddItem(InventoryItem item)
    {
        _ = AddItemAsync(item);
    }

    // Legacy: UpdateItemQuantity(itemId, qty)
    public void UpdateItemQuantity(string itemId, int newQuantity)
    {
        _ = UpdateItemQuantityAsync(itemId, newQuantity);
    }

    // Legacy: RemoveItem(itemId)
    public void RemoveItem(string itemId)
    {
        _ = RemoveItemAsync(itemId);
    }

    // Legacy: IsConnected - in non-networked mode this returns true, in API mode we return true (you may extend with ping checks)
    public bool IsConnected()
    {
        if (apiMode)
        {
            // Optionally implement a health check endpoint and cache the result
            return true;
        }
        return true;
    }

    #endregion

    #region Persistence (uses InventoryPersistenceData)

    private async Task SaveCurrentInventoryAsync()
    {
        if (!enablePersistence || storage == null) return;

        try
        {
            // Load existing file so we don't blow away users when we save items
            var existing = await storage.LoadAsync(currentGroupId) ?? new InventoryPersistenceData();

            existing.groupId = currentGroupId;
            existing.groupName = currentGroupName;
            existing.lastSaved = DateTime.Now.ToString("O");
            existing.version = version;
            existing.items = localInventoryCache.Select(SerializableInventoryItem.FromInventoryItem).ToList();
            // preserve existing.users unless something else changed them;
            // we write back whatever existing.users currently contain.
            await storage.SaveAsync(existing);

            Log("Saved inventory to local storage.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[InventoryManager] Save failed: {e.Message}");
        }
    }

    private void RestoreFromPersistenceData(InventoryPersistenceData data)
    {
        localInventoryCache.Clear();
        itemLookup.Clear();

        if (data.items != null)
        {
            foreach (var s in data.items)
            {
                var item = new InventoryItem(s.itemName, s.category)
                {
                    itemId = s.itemId ?? Guid.NewGuid().ToString(),
                    description = s.description ?? "",
                    quantity = s.quantity,
                    weight = s.weight,
                    valueInGold = s.valueInGold,
                    currentOwner = s.currentOwner ?? "",
                    thumbnailUrl = s.thumbnailUrl ?? "",
                    lastModified = DateTime.TryParse(s.lastModified, out var parsed) ? parsed : DateTime.Now
                };
                item.dateAdded = item.lastModified;
                localInventoryCache.Add(item);
                itemLookup[item.itemId] = item;
            }
        }

        if (!string.IsNullOrEmpty(data.groupName))
            currentGroupName = data.groupName;

        version = data.version;
    }

    private void CreateSampleInventory()
    {
        var sampleItems = new List<InventoryItem>
        {
            new InventoryItem("Longsword", ItemCategory.Weapon)
            {
                description = "A versatile martial weapon.",
                weight = 3f,
                valueInGold = 15,
                currentOwner = "Host"
            },
            new InventoryItem("Health Potion", ItemCategory.Consumable)
            {
                description = "Restores 2d4+2 hit points.",
                weight = 0.5f,
                valueInGold = 50,
                quantity = 3
            },
            new InventoryItem("Gold Coins", ItemCategory.Currency)
            {
                description = "Standard currency.",
                weight = 0.02f,
                valueInGold = 1,
                quantity = 150
            }
        };

        foreach (var it in sampleItems)
        {
            if (string.IsNullOrEmpty(it.itemId)) it.itemId = Guid.NewGuid().ToString();
            it.dateAdded = DateTime.Now;
            it.lastModified = DateTime.Now;
            localInventoryCache.Add(it);
            itemLookup[it.itemId] = it;
        }
    }

    #endregion

    #region API helpers (simple REST wrappers)

    private async Task FetchFullInventoryFromApiAsync()
    {
        string url = $"{baseApiUrl}/inventory/full?groupId={UnityWebRequest.EscapeURL(currentGroupId)}";
        var json = await SendGetRequestAsync(url);
        if (string.IsNullOrEmpty(json)) return;

        var data = JsonUtility.FromJson<InventoryPersistenceData>(json);
        if (data != null) RestoreFromPersistenceData(data);
    }

    private async Task PushFullInventoryToApiAsync()
    {
        string url = $"{baseApiUrl}/inventory/pushfull";
        var data = new InventoryPersistenceData
        {
            groupId = currentGroupId,
            groupName = currentGroupName,
            lastSaved = DateTime.Now.ToString("O"),
            version = version,
            items = localInventoryCache.Select(SerializableInventoryItem.FromInventoryItem).ToList(),
            users = new List<SerializableUserInfo>() // leave users blank; UserManager will manage users separately
        };

        string payload = JsonUtility.ToJson(data, prettyPrintJson);
        await SendPostRequestAsync(url, payload);
    }

    private async Task PostAddItemToApiAsync(SerializableInventoryItem item)
    {
        string url = $"{baseApiUrl}/inventory/add";
        string payload = JsonUtility.ToJson(item, prettyPrintJson);
        await SendPostRequestAsync(url, payload);
    }

    private async Task PostRemoveItemToApiAsync(string itemId)
    {
        string url = $"{baseApiUrl}/inventory/remove";
        var payloadObj = new { groupId = currentGroupId, itemId = itemId };
        string payload = JsonUtility.ToJson(payloadObj);
        await SendPostRequestAsync(url, payload);
    }

    private async Task PostUpdateQuantityToApiAsync(string itemId, int qty)
    {
        string url = $"{baseApiUrl}/inventory/updateQuantity";
        var payloadObj = new { groupId = currentGroupId, itemId = itemId, quantity = qty };
        string payload = JsonUtility.ToJson(payloadObj);
        await SendPostRequestAsync(url, payload);
    }

    private async Task<string> SendGetRequestAsync(string url)
    {
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            var op = www.SendWebRequest();
            while (!op.isDone) await Task.Yield();

#if UNITY_2020_1_OR_NEWER
            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
#else
            if (www.isNetworkError || www.isHttpError)
#endif
            {
                Debug.LogWarning($"[InventoryManager] GET {url} failed: {www.error}");
                return null;
            }
            return www.downloadHandler.text;
        }
    }

    private async Task<bool> SendPostRequestAsync(string url, string jsonPayload)
    {
        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            var op = www.SendWebRequest();
            while (!op.isDone) await Task.Yield();

#if UNITY_2020_1_OR_NEWER
            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
#else
            if (www.isNetworkError || www.isHttpError)
#endif
            {
                Debug.LogWarning($"[InventoryManager] POST {url} failed: {www.error} payload: {jsonPayload}");
                return false;
            }
            return true;
        }
    }

    #endregion

    #region Utilities

    private void Notify(string message)
    {
        OnInventoryMessage?.Invoke(message);
        if (logging) Debug.Log($"[InventoryManager] {message}");
    }

    private void Log(string message)
    {
        if (logging) Debug.Log($"[InventoryManager] {message}");
    }

    #endregion
}
