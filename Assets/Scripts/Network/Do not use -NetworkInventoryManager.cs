using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using System.Threading.Tasks;
using InventorySystem.Data;

public class NetworkInventoryManager : NetworkBehaviour
{
    [Header("Network Settings")]
    [SerializeField] private int maxInventoryItems = 500;
    [SerializeField] private float syncIntervalSeconds = 1f;
    [SerializeField] private bool logNetworkActivity = true;

    [Header("Permissions")]
    [SerializeField] private GroupPermission defaultUserPermission = GroupPermission.Editor;
    [SerializeField] private bool autoPromoteFirstClient = true;

    
    [Header("Storage Settings")]
    [SerializeField] private bool enablePersistence = true;
    [SerializeField] private StorageType storageType = StorageType.JSON;
    [SerializeField] private bool enableAutoSave = true;
    [SerializeField] private float autoSaveIntervalMinutes = 5f;


    public enum StorageType { JSON }  // Future: SQLite, Cloud


    //Netowrk Variables - Automatically sync them between all clients
    private NetworkVariable<int> inventoryVersion = new NetworkVariable<int>(0);
    private NetworkVariable<FixedString64Bytes> groupName = new NetworkVariable<FixedString64Bytes>("Party Inventory");
    private IInventoryStorage storage;
    private float lastAutoSaveTime = 1f;

    //Server Side only Variables
    private List<NetworkInventoryItem> serverInventory;
    private Dictionary<ulong, NetworkUserInfo> connectedUsers;
    private Dictionary<string, NetworkInventoryItem> itemLookup;

    //Events for UI Updates
    public System.Action<List<InventoryItem>> OnInventoryChanged;
    public System.Action<List<NetworkUserInfo>> OnUsersChanged;
    public System.Action<string> OnInventoryMessage;

    //Client Side Variables/cache
    private List<InventoryItem> localInventoryCache;

    // Singleton Pattern
    public static NetworkInventoryManager Instance { get; set;  }

    public void Awake()
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

        //Initialize Collections
        serverInventory = new List<NetworkInventoryItem>();
        connectedUsers = new Dictionary<ulong, NetworkUserInfo>();
        itemLookup = new Dictionary<string, NetworkInventoryItem>();
        localInventoryCache = new List<InventoryItem>();
    }
    void Start()
    {
#if UNITY_EDITOR
        // Auto-start for testing
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("[NetworkInventory] Auto-starting as Host for editor testing");
            NetworkManager.Singleton.StartHost();
        }
#endif
    }

    public override void OnNetworkSpawn()
    {
        if (logNetworkActivity)
        {
            Debug.Log($"[NetworkInventory] Network spawned. IsServer: {IsServer}, IsClient: {IsClient}");
        }

        //Subscribe to the network variable changes
        inventoryVersion.OnValueChanged += OnInventoryVersionChanged;
        groupName.OnValueChanged += OnGroupNameChanged;

        if (IsServer)
        {
            Debug.Log("[NetworkInventory] Server initialization starting...");
            InitializeServerInventory();

            //NEEDED for Netcode: Suybscribe to Connection Callbacks
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        }
        if (IsClient && !IsServer)
        {
            //Request initial inventory data
            RequestFullInventorySyncServerRpc();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        if(IsServer && storage != null)
        {
            if (logNetworkActivity)
            {
                Debug.Log("NetworkInventory Server shutting down, Saving final inventory state");
                
            }

            _ = SaveCurrentInventory();
            _ = storage.CreateBackupAsync($"group_{groupName}");
        }

        //Unsubscribe from events to prevent memory leaks
        if(inventoryVersion != null)
            inventoryVersion.OnValueChanged -= OnInventoryVersionChanged;
        if (groupName != null)
            groupName.OnValueChanged -= OnGroupNameChanged;

    }


    public void Update()
    {
        if (IsServer && enableAutoSave && storage != null &&
            Time.time - lastAutoSaveTime >= autoSaveIntervalMinutes*60f)
        {
            _ = SaveCurrentInventory();
            lastAutoSaveTime = Time.time;
        }

    }

    //=======================================================================================================
    // SERVER AUTH METHODS - only to be run on the server/host
    //=======================================================================================================
    private async void InitializeServerInventory()
    {
        if (!IsServer) return;

        Debug.Log("[Network] Initializing server inventory with storage...");

        // Initialize storage
        storage = new JsonInventoryStorage(logging: logNetworkActivity);

        // Try to load existing data
        var existingData = await storage.LoadAsync("default_group");

        if (existingData != null)
        {
            RestoreInventoryFromData(existingData);
            Debug.Log($"[Network] Loaded {existingData.items.Count} items from storage");
        }
        else
        {
            // No existing data, create samples
            CreateSampleInventory();
            await SaveCurrentInventory();
            Debug.Log("[Network] Created sample inventory and saved");
        }

        UpdateInventoryVersion();
    }

    private void CreateSampleInventory()
    {
        if (!IsServer) return;
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
        
        //now loop through the items and add them to the serverinventory
        foreach (var item in sampleItems)
        {
            var networkItem = NetworkInventoryItem.FromInventoryItem(item);
            serverInventory.Add(networkItem);
            itemLookup[item.itemId] = networkItem;
        }

        if (logNetworkActivity)
            Debug.Log($"[NetowrkInventory] Created {sampleItems.Count} sample items");
    }


    //=======================================================================================================
    // SERVER RPCs - Clients Call these to request the server to take actions
    //=======================================================================================================

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void AddItemServerRpc(NetworkInventoryItem item, ulong requestingClientId = 0, RpcParams serverRpcParams = default)
    {
        if (!IsServer) return;
        var clientId = serverRpcParams.Receive.SenderClientId;

        if (logNetworkActivity) Debug.Log($"[NetworkInventory] Client {clientId} requesting to add item {item.itemName} to inventory");

        Debug.Log($"[NetworkInventory] AddItemServerRpc called by client {clientId}");
        Debug.Log($"[NetworkInventory] Item: {item.itemName}, Quantity: {item.quantity}");
        Debug.Log($"[NetworkInventory] Current serverInventory count: {serverInventory.Count}");


        //check permissions
        if (!HasPermission(clientId, GroupPermission.Editor))
        {
            NotifyClientRpc("You don't have permission to add items",
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
            return;
        }
        //check inventory limits
        if (serverInventory.Count >= maxInventoryItems)
        {
            NotifyClientRpc("Inventory is Full",
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
            return;
        }

        //Check if item already exists (add quantities together)
        var existingItem = serverInventory.FirstOrDefault(i => i.itemName.ToString() == item.itemName.ToString() && i.category == item.category);

        if (existingItem.itemId.Length > 0) //if it exists it will be greater than zero (since its a fixedstring, it cant be null
        {
            //update existing item quantity
            var updatedItem = existingItem;
            updatedItem.quantity += item.quantity;
            updatedItem.lastModifiedTicks = DateTime.Now.Ticks;

            // Replace in collections
            int index = serverInventory.FindIndex(i => i.itemId.ToString() == existingItem.itemId.ToString());
            serverInventory[index] = updatedItem;
            itemLookup[existingItem.itemId.ToString()] = updatedItem;

            if (logNetworkActivity) Debug.Log($"[Network Inventory] Combined with Existing item, new quantity for {updatedItem.itemName} is {updatedItem.quantity}");
        }
        else
        {
            //Add as new item
            serverInventory.Add(item);
            itemLookup[item.itemId.ToString()] = item;

            if (logNetworkActivity) Debug.Log($"[Network Inventory] Added new item: {item.itemName}");
        }

        // Notify All Clients of the change
        UpdateInventoryVersion();
        Debug.Log($"[NetworkInventory] Item added! New serverInventory count: {serverInventory.Count}");
        BroadcastInventoryChangeClientRpc(NetworkInventoryAction.ItemAdded, item.itemId, GetUserName(clientId), item.quantity, "");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void UpdateItemQuantityServerRpc(FixedString64Bytes itemID, int newQuantity, RpcParams serverRpcParams = default)
    {
        if (!IsServer) return;

        var clientId = serverRpcParams.Receive.SenderClientId;

        if(!HasPermission(clientId, GroupPermission.Editor))
        {
            NotifyClientRpc("You don't have permission to edit items",
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
            return;
        }

        string itemIdStr = itemID.ToString();
        if (itemLookup.ContainsKey(itemIdStr))
        {
            var item = itemLookup[itemIdStr];

            if (newQuantity <= 0)
            {
                //Remove item
                RemoveItemFromInventory(itemIdStr);
                BroadcastInventoryChangeClientRpc(NetworkInventoryAction.ItemRemoved, itemID, GetUserName(clientId), 0, "");
            }
            else
            {
                //update quantity
                item.quantity = newQuantity;
                item.lastModifiedTicks = DateTime.Now.Ticks;

                //update in collections
                itemLookup[itemIdStr] = item;
                int index = serverInventory.FindIndex(i => i.itemId.ToString() == itemIdStr);
                if (index >= 0)
                {
                    serverInventory[index] = item;
                }
                BroadcastInventoryChangeClientRpc(NetworkInventoryAction.ItemQuantityChanged, itemID, GetUserName(clientId), newQuantity, "");
            }
            UpdateInventoryVersion();
        }

    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RemoveItemServerRpc(FixedString64Bytes itemId, RpcParams serverRpcParams = default)
    {
        if (!IsServer) return;

        var clientId = serverRpcParams.Receive.SenderClientId;

        if (!HasPermission(clientId, GroupPermission.Editor))
        {
            LogMessageClientRpc("You don't have permission to delete items");
            return;
        }

        string itemIdStr = itemId.ToString();

        if(RemoveItemFromInventory(itemIdStr))
        {
            UpdateInventoryVersion();
            BroadcastInventoryChangeClientRpc(NetworkInventoryAction.ItemRemoved, itemId, GetUserName(clientId), 0, "");
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestFullInventorySyncServerRpc(RpcParams serverRpcParams = default)
    {
        if (!IsServer) return;

        var clientId = serverRpcParams.Receive.SenderClientId;

        if (logNetworkActivity) Debug.Log($"[Network Manager] Client {clientId} is requesting a full sync");

        //send current inventory to requesting client
        SendFullInventoryToClientClientRpc(serverInventory.ToArray(), new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetUserPermissionServerRpc(ulong targetClientId, GroupPermission newPermission, RpcParams serverRpcParams=default)
    {
        if (!IsServer) return;

        var clientId = serverRpcParams.Receive.SenderClientId;

        if (!HasPermission(clientId, GroupPermission.Admin))
        {
            NotifyClientRpc("You Don't have permission to change user permissions", new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
            return;
        }

        if(connectedUsers.ContainsKey(targetClientId))
        {
            var user = connectedUsers[targetClientId];
            user.permission = newPermission;
            connectedUsers[targetClientId] = user;

            //Notify all clients of permission change
            BroadcastUserListClientRpc(connectedUsers.Values.ToArray());

            if (logNetworkActivity) Debug.Log($"[NetowrkInvntory] Changed {user.userName}'s permission to {newPermission}");
        }    
    }

    //=======================================================================================================
    // CLIENT RPCs (Server calls these methods to update clients)
    //=======================================================================================================
    [ClientRpc]
    private void SendFullInventoryToClientClientRpc(NetworkInventoryItem[] items, ClientRpcParams clientRpcParams =default)
    {
        if (logNetworkActivity) Debug.Log($"[NetowrkInventory] Received full inventory: {items.Length} items");

        // Convert Network items to local format
        localInventoryCache.Clear();
        foreach(var networkItem in items)
        {
            localInventoryCache.Add(networkItem.ToInventoryItem());
        }

        // Notify UI to refresh
        OnInventoryChanged?.Invoke(localInventoryCache);
    }

    [ClientRpc]
    private void BroadcastInventoryChangeClientRpc(NetworkInventoryAction action, FixedString64Bytes itemId, FixedString64Bytes userName, int value, FixedString128Bytes stringData)
    {
        string message = action switch
        {
            NetworkInventoryAction.ItemAdded => $"{userName} added an item",
            NetworkInventoryAction.ItemRemoved => $"{userName} removed an item",
            NetworkInventoryAction.ItemQuantityChanged => $"{userName} changed the item quantity to {value}",
            NetworkInventoryAction.ItemOwnerChanged => $"{userName} changed item owner to {stringData}",
            _ => $"{ userName} made a change"
        };

        if (logNetworkActivity) Debug.Log($"[NetworkInventory] {message}");

        OnInventoryMessage?.Invoke(message);

        // Request updated inventory
        if(IsClient && !IsServer)
        {
            RequestFullInventorySyncServerRpc();
        }

    }

    [ClientRpc]
    public void BroadcastUserListClientRpc(NetworkUserInfo[] users)
    {
        OnUsersChanged?.Invoke(users.ToList());

        if (logNetworkActivity) Debug.Log($"[NeworkInventory] user list updated: {users.Length} users online");
    }

    [ClientRpc]
    public void NotifyClientRpc(FixedString128Bytes message, ClientRpcParams clientRpcParams = default)
    {
        OnInventoryMessage?.Invoke(message.ToString());
        Debug.LogWarning($"[NetworkInventory] Sever message: {message}");
    }

    [ClientRpc]
    public void LogMessageClientRpc(FixedString128Bytes message, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log(message);
    }

    //=======================================================================================================
    // HELPER METHODS
    //=======================================================================================================
    private bool HasPermission(ulong clientId, GroupPermission networkUserPermission)
    {
        if (!connectedUsers.ContainsKey(clientId)) return false;

        var userPermission = connectedUsers[clientId].permission;
        return userPermission >= networkUserPermission;
    }
    
    private FixedString64Bytes GetUserName(ulong clientId)
    {
        if (connectedUsers.ContainsKey(clientId))
        {
            return connectedUsers[clientId].userName;
        }
        Debug.Log($"User_{clientId} not found in list of connectedUsers");
        return $"User_{clientId}";
    }

    private void RestoreInventoryFromData(InventoryPersistenceData data)
    {
        serverInventory.Clear();
        itemLookup.Clear();

        foreach(var item in data.items)
        {
            var networkItem = item.ToNetworkItem();
            serverInventory.Add(networkItem);
            itemLookup[item.itemId] = networkItem;

        }

        if (!string.IsNullOrEmpty(data.groupName))
            groupName.Value = data.groupName;

        foreach(var user in data.users)
        {
            var networkUser = user.ToNetworkUser();
            networkUser.isOnline = false;
            connectedUsers[user.clientId] = networkUser; 

        }
    }

    private async Task SaveCurrentInventory()
    {
        if (!IsServer || !enablePersistence || storage == null) return;

        try
        {
            var data = new InventoryPersistenceData
            {
                groupId = $"group_{groupName}",
                groupName = groupName.Value.ToString(),
                version = inventoryVersion.Value,
                items = serverInventory.Select(SerializableInventoryItem.FromNetworkItem).ToList(),
                users = connectedUsers.Values.Select(SerializableUserInfo.FromNetworkUser).ToList()
            };

            await storage.SaveAsync(data);
        }
        catch(Exception e)
        {
            Debug.LogError($"[Network] Save Failed: {e.Message}");
        }
    }

    private bool RemoveItemFromInventory(string itemId)
    {
        if(itemLookup.ContainsKey(itemId))
        {
            itemLookup.Remove(itemId);
            int removed = serverInventory.RemoveAll(i => i.itemId.ToString() == itemId);

            if (logNetworkActivity && removed > 0)
                Debug.Log($"Item {itemId} has been removed from inventory");
            return removed > 0;
        }
        return false;
    }


    private void UpdateInventoryVersion()
    {
        if(IsServer) inventoryVersion.Value++;
    }

    //Network Varriable Change Handlers
    private void OnInventoryVersionChanged(int oldVersion, int newVersion)
    {
        if (logNetworkActivity) Debug.Log($"[NetworkInventory] Version changed from: {oldVersion} -> {newVersion}");
    }

    private void OnGroupNameChanged(FixedString64Bytes oldGroupName, FixedString64Bytes newGroupName)
    {
        if (logNetworkActivity) Debug.Log($"[NetworkInvetory] Group name changed from: {oldGroupName} -> {newGroupName}");
    }

    //Debug helper to log item conversion details
    private void LogItemConversion(string itemName, string source, InventoryItem result)
    {
        if (!logNetworkActivity) return;
        Debug.Log($"[ItemFetcher] Converted {source} item: {itemName}");
        Debug.Log($"  → Name: {result.itemName}");
        Debug.Log($"  → Category: {result.category}");
        Debug.Log($"  → Weight: {result.weight} lbs");
        Debug.Log($"  → Value: {result.valueInGold}");
        Debug.Log($"  → Description: {result.description.Substring(0, Math.Min(100, result.description.Length))}...");

        if (result.properties != null && result.properties.Count > 0)
        {
            Debug.Log($"  → Properties: {string.Join(", ", result.properties.Select(p => $"{p.Key}={p.Value}"))}");
        }
    }

    //=======================================================================================================
    // Public API calls for UI
    //=======================================================================================================
    public void AddItem(InventoryItem item)
    {
        Debug.Log($"[NetworkInventory] AddItem called for: {item.itemName}");

        if (IsServer)
        {
            // If we are the server (or host), modify the inventory directly here.
            Debug.Log("[NetworkInventory] IsServer: Performing inventory modification directly.");
            // Add the item to the internal server inventory list/logic here
            // Example: _serverInventory.Add(item);
            return; // Exit the method after handling on the server
        }

        if (IsClient)
        {
            Debug.Log($"[NetworkInventory] IsClient: {IsClient}");
            var networkItem = NetworkInventoryItem.FromInventoryItem(item);
            Debug.Log($"[NetworkInventory] Converted to NetworkInventoryItem, calling ServerRpc");
            AddItemServerRpc(networkItem);
        }
        else
        {
            Debug.LogError("[NetworkInventory] Not a client! Cannot add item.");
        }

    }

    public void UpdateItemQuantity(string itemId, int newQuantity)
    {
        if(IsClient)
        {
            UpdateItemQuantityServerRpc(itemId, newQuantity);
        }
    }

    public void RemoveItem(string itemId)
    {
        if (IsClient)
        {
            RemoveItemServerRpc(itemId);
        }
    }

    public List<InventoryItem> GetCurrentInventory()
    {
        return new List<InventoryItem>(localInventoryCache);
    }

    public bool IsConnected()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;
    }



    //=======================================================================================================
    // Connection Management
    //=======================================================================================================
    public void OnClientConnected(ulong clientId)
    {
        if (IsServer)
        {
            var newUser = new NetworkUserInfo
            {
                clientId = clientId,
                userName = $"Player_{clientId}",
                permission = autoPromoteFirstClient && connectedUsers.Count == 0 ? GroupPermission.Admin : defaultUserPermission,
                connectionTimeTicks = DateTime.Now.Ticks,
                isOnline = true
            };

            connectedUsers[clientId] = newUser;

            if (logNetworkActivity) Debug.Log($"[NetworkInventory] Client {clientId} connected as {newUser.userName} with {newUser.permission} permission type");

            //broadcast the updated user list
            BroadcastUserListClientRpc(connectedUsers.Values.ToArray());
        }
    }

    public void OnClientDisconnected(ulong clientId)
    {
        if (IsServer && connectedUsers.ContainsKey(clientId))
        {
            var user = connectedUsers[clientId];
            user.isOnline = false;
            connectedUsers[clientId] = user;

            if (logNetworkActivity) Debug.Log($"[NetworkInventory] Client {clientId} ({user.userName}) disconnected");

            //broadcast updated user list
            BroadcastUserListClientRpc(connectedUsers.Values.ToArray());
        }
    }

    //=======================================================================================================
    // TESTING METHODS
    //=======================================================================================================
    [ContextMenu("Force Save")]
    public void ForceSave()
    {
        if(IsServer&& storage != null)
        {
            _ = SaveCurrentInventory();
            Debug.Log($"[NetworkInventory] Manual Save Triggered");
        }
    }

    [ContextMenu("Storage Info")]
    public void ShowStorageInfo()
    {
        if (storage != null)
        {
            string info = storage.GetStorageInfo($"group_{groupName}");
            Debug.Log($"[NetworkInventory] {info}");
        }
    }

    [ContextMenu("Open Save Folder")]
    public void OpenSaveFolder()
    {
        if (storage is JsonInventoryStorage jsonStorage)
            jsonStorage.OpenSaveFolder();
    }
}


