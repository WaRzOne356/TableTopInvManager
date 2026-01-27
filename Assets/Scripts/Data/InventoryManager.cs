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



    private float lastAutoSaveTime;
    private string currentGroupId = "group_default";
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

        cachedOwnerships = await LoadOwnershipAsync();

        // notify UI
        OnInventoryChanged?.Invoke(GetCurrentInventory());
    }

    #region Public API (async)

    public List<InventoryItem> GetCurrentInventory()
    {
        return new List<InventoryItem>(localInventoryCache);
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

    public List<ItemOwnership> GetAllOwnerships()
    {
        return new List<ItemOwnership>(cachedOwnerships);
    }

    public async Task UpdateOwnershipAsync(string itemId, string characterId, int newQuantity)
    {
        var ownership = cachedOwnerships.Find(o =>
            o.itemId == itemId && o.characterId == characterId);

        if (newQuantity <= 0)
        {
            // Remove ownership if quantity is 0 or less
            if (ownership != null)
            {
                cachedOwnerships.Remove(ownership);
            }
        }
        else
        {
            if (ownership != null)
            {
                // Update existing
                ownership.quantityOwned = newQuantity;
            }
            else
            {
                // Create new
                cachedOwnerships.Add(new ItemOwnership(itemId, characterId, newQuantity));
            }
        }

        // Save to storage
        await SaveOwnershipAsync(cachedOwnerships);

        if (logging)
            Debug.Log($"[InventoryManager] Updated ownership: Item={itemId}, Character={characterId}, Qty={newQuantity}");
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
