using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InventorySystem.UI.Navigation;
using InventorySystem.UI.Pages;
using InventorySystem.Data;

// ============================================================================
// GROUP INVENTORY PAGE
// ============================================================================
namespace InventorySystem.UI.Pages
{
    public class GroupInventoryPage : UIPage
    {
        [Header("Group Inventory Display")]
        [SerializeField] private Transform itemContainer;
        [SerializeField] private ScrollRect inventoryScrollView;
        [SerializeField] private GameObject itemCardPrefab; // Should have ItemCardUI component


        [Header("Dialogs")]
        [SerializeField] private AddItemDialog addItemDialog;

        [Header("Group Controls")]
        [SerializeField] private TMP_InputField searchField;
        [SerializeField] private TMP_Dropdown categoryFilter;
        [SerializeField] private TMP_Dropdown ownerFilter;
        [SerializeField] private Button addItemButton;
        [SerializeField] private Button sortButton;

        [Header("Group Stats")]
        [SerializeField] private TextMeshProUGUI totalItemsText;
        [SerializeField] private TextMeshProUGUI totalWeightText;
        [SerializeField] private TextMeshProUGUI totalValueText;
        [SerializeField] private TextMeshProUGUI groupMembersText;

        [Header("Group Management")]
        [SerializeField] private Button manageUsersButton;
        [SerializeField] private Button exportGroupInventoryButton;
        [SerializeField] private Button shareInventoryLinkButton;

        [Header("Connection Status")]
        [SerializeField] private TextMeshProUGUI connectionStatusText;
        [SerializeField] private TextMeshProUGUI lastSyncText;
        [SerializeField] private Button reconnectButton;

        // State
        private List<InventoryItem> groupItems;
        private List<GameObject> itemCardObjects;
        private InventoryManager inventoryManager;
        private UserManager userManager;
        private ItemCardUI currentlyExpandedCard;

        void Awake()
        {
            pageType = UIPageType.GroupInventory;
            pageTitle = "Group Inventory";

            groupItems = new List<InventoryItem>();
            itemCardObjects = new List<GameObject>();

            SetupEventHandlers();
            SetupAddItemDialog();
        }

        private void SetupAddItemDialog()
        {
            if (addItemDialog != null)
            {
                addItemDialog.OnItemCreated += OnCustomItemCreatedForGroup;
                addItemDialog.OnItemSelected += OnItemSelectedFromSearch;
                addItemDialog.OnDialogClosed += OnAddItemDialogClosed;

                Debug.Log("[GroupInventory] Event handlers subscribed to AddItemDialog");
            }
            else
            {
                Debug.LogWarning("[GroupInventory] AddItemDialog reference not set!");
            }
        }

        void Start()
        {
            inventoryManager = InventoryManager.Instance;
            
            if (inventoryManager != null)
            {
                Debug.Log($"[GroupInventory] InventoryManager found: {inventoryManager.gameObject.name}");
                inventoryManager.OnInventoryChanged += OnNetworkInventoryChanged;
                inventoryManager.OnInventoryMessage += OnNetworkMessage;
                
            }
            else
            {
                Debug.LogError("[GroupInventory] InventoryManager.Instance is NULL!");
                Debug.LogError("Make sure InventoryManager GameObject exists in scene and has NetworkBehaviour component!");
            }
            userManager = UserManager.Instance;
            if (userManager != null)
            {
                userManager.OnUsersChanged += OnUsersChanged;
            }
            else
            {
                Debug.LogError("[GroupInventory] UserManager.Instance is NULL!");
            }
        }

        private void SetupEventHandlers()
        {
            searchField?.onValueChanged.AddListener(OnSearchChanged);
            categoryFilter?.onValueChanged.AddListener(OnFilterChanged);
            ownerFilter?.onValueChanged.AddListener(OnFilterChanged);
            addItemButton?.onClick.AddListener(OpenItemBrowser);
            sortButton?.onClick.AddListener(CycleSortMode);
            manageUsersButton?.onClick.AddListener(OpenUserManagement);
            exportGroupInventoryButton?.onClick.AddListener(ExportGroupInventory);
            shareInventoryLinkButton?.onClick.AddListener(ShareInventoryLink);
            reconnectButton?.onClick.AddListener(Reconnect);
        }

        protected override void RefreshContent()
        {
            LoadGroupInventory();
            UpdateGroupStats();
            UpdateConnectionStatus();
            RefreshItemDisplay();
        }

        private void LoadGroupInventory()
        {
            groupItems.Clear();

            if (inventoryManager != null)
            {
                var inventory = inventoryManager.GetCurrentInventory();
                if (inventory != null)
                {
                    groupItems.AddRange(inventory);
                }
            }

            Debug.Log($"[GroupInventory] Loaded {groupItems.Count} group items");
        }

        private void RefreshItemDisplay()
        {
            ClearItemCards();

            var filteredItems = GetFilteredAndSortedItems();

            foreach (var item in filteredItems)
            {
                CreateGroupItemCard(item);
            }

            UpdateGroupStats();
        }

        private List<InventoryItem> GetFilteredAndSortedItems()
        {
            var filtered = groupItems.ToList();

            // Apply search filter
            string searchTerm = searchField?.text?.ToLower() ?? "";
            if (!string.IsNullOrEmpty(searchTerm))
            {
                filtered = filtered.Where(item =>
                    item.itemName.ToLower().Contains(searchTerm) ||
                    item.description.ToLower().Contains(searchTerm)
                ).ToList();
            }

            // Apply category filter
            if (categoryFilter != null && categoryFilter.value > 0)
            {
                var categories = System.Enum.GetValues(typeof(ItemCategory)).Cast<ItemCategory>().ToArray();
                var selectedCategory = categories[categoryFilter.value - 1];
                filtered = filtered.Where(item => item.category == selectedCategory).ToList();
            }

            // Apply owner filter
            if (ownerFilter != null && ownerFilter.value > 0)
            {
                string selectedOwner = ownerFilter.options[ownerFilter.value].text;
                if (selectedOwner == "Unassigned")
                {
                    filtered = filtered.Where(item => string.IsNullOrEmpty(item.currentOwner)).ToList();
                }
                else
                {
                    filtered = filtered.Where(item => item.currentOwner == selectedOwner).ToList();
                }
            }

            // Sort items
            return filtered.OrderBy(item => item.category).ThenBy(item => item.itemName).ToList();
        }

        private void CreateGroupItemCard(InventoryItem item)
        {
            if (itemCardPrefab == null || itemContainer == null) return;

            var cardObj = Instantiate(itemCardPrefab, itemContainer);
            var cardUI = cardObj.GetComponent<ItemCardUI>();

            if (cardUI != null)
            {
                cardUI.SetupCard(item, ItemCardUI.CardMode.Group);

                // Subscribe to events
                cardUI.OnCardSelected += OnCardExpanded;
                cardUI.OnItemModified += OnGroupItemModified;
                cardUI.OnItemDeleted += OnItemDeleted;

                itemCardObjects.Add(cardObj);
            }
        }



        private void ClearItemCards()
        {
            foreach (var cardObj in itemCardObjects)
            {
                if (cardObj != null)
                    Destroy(cardObj);
            }
            itemCardObjects.Clear();
        }

        private void UpdateGroupStats()
        {
            var filteredItems = GetFilteredAndSortedItems();

            if (totalItemsText != null)
                totalItemsText.text = $"Total Items: {filteredItems.Count}";

            float totalWeight = filteredItems.Sum(item => item.TotalWeight);
            if (totalWeightText != null)
                totalWeightText.text = $"Total Weight: {totalWeight:F1} lbs";

            int totalValue = filteredItems.Sum(item => item.TotalValue);
            if (totalValueText != null)
                totalValueText.text = $"Total Value: {totalValue:N0} gp";

            // Update group members count
            if (groupMembersText != null && inventoryManager != null)
            {
                // This would come from the network manager's user list
                groupMembersText.text = "Group Members: 3"; // Placeholder
            }
        }

        private void UpdateConnectionStatus()
        {
            bool isConnected = inventoryManager != null;

            if (connectionStatusText != null)
            {
                connectionStatusText.text = isConnected ? "🟢 Connected" : "🔴 Offline";
                connectionStatusText.color = isConnected ? Color.green : Color.red;
            }

            if (lastSyncText != null)
            {
                lastSyncText.text = $"Last sync: {System.DateTime.Now:HH:mm:ss}";
            }

            if (reconnectButton != null)
            {
                reconnectButton.gameObject.SetActive(!isConnected);
            }
        }
        //<todo: review, I don't think this is right>
        private void UpdateItemCardWithOwnership(ItemCardUI cardUI, InventoryItem item)
        {
            // Get ownership summary
            var groupInventory = new GroupInventory(); // Or get from InventoryManager
                                                       // You'll need to expose this through InventoryManager

            string ownershipText = groupInventory.GetOwnershipSummary(item.itemId);

            // Update the card's owner text
            // This assumes ItemCardUI has a method to set ownership display
            // cardUI.SetOwnershipText(ownershipText);
        }

        // Event Handlers
        private void OnNetworkInventoryChanged(List<InventoryItem> updatedInventory)
        {
            groupItems = updatedInventory.ToList();
            RefreshItemDisplay();
        }

        private void OnNetworkMessage(string message)
        {
            ShowMessage(message, MessageType.Info);
        }

        private void OnUsersChanged(List<SerializableUserInfo> users)
        {
            UpdateOwnerFilter(users);
            UpdateGroupStats();
        }

        private void UpdateOwnerFilter(List<SerializableUserInfo> users)
        {
            if (ownerFilter == null) return;

            ownerFilter.ClearOptions();
            var options = new List<string> { "All Players", "Unassigned" };
            options.AddRange(users.Where(u => u.isOnline).Select(u => u.userName.ToString()));
            ownerFilter.AddOptions(options);
        }

        private void OnSearchChanged(string searchTerm)
        {
            Debug.Log($"[GroupInventory] Search changed: '{searchTerm}'");
            RefreshItemDisplay();
        }

        private void OnFilterChanged(int filterIndex)
        {
            // Determine which filter changed
            string filterType = "Unknown";
            if (categoryFilter != null && categoryFilter.value == filterIndex)
                filterType = "Category";
            else if (ownerFilter != null && ownerFilter.value == filterIndex)
                filterType = "Owner";

            Debug.Log($"[GroupInventory] {filterType} filter changed to index: {filterIndex}");
            RefreshItemDisplay();
        }

        private void OnCardExpanded(ItemCardUI expandedCard)
        {
            Debug.Log($"[GroupInventory] Card expanded: {expandedCard.CurrentItem.itemName}");

            // Collapse previously expanded card
            if (currentlyExpandedCard != null && currentlyExpandedCard != expandedCard)
            {
                currentlyExpandedCard.CollapseCard();
            }

            currentlyExpandedCard = expandedCard;
        }

        private void OnItemDeleted(InventoryItem item)
        {
            Debug.Log($"[GroupInventory] Deleting item: {item.itemName}");

            // Remove from InventoryManager
            InventoryManager.Instance?.RemoveItem(item.itemId);

            // Refresh display
            RefreshContent();
        }

        private void OnGroupItemModified(InventoryItem item)
        {
            Debug.Log($"[GroupInventory] Item modified: {item.itemName}, Owner: {item.currentOwner ?? "Unassigned"}");

            if (inventoryManager != null)
            {
                inventoryManager.UpdateItemQuantityAsync(item.itemId, item.quantity);
            }
            else
            {
                Debug.LogWarning("[GroupInventory] InventoryManager not available, changes not synced");
            }

            // Just update stats, don't refresh entire display
            UpdateGroupStats();
        }

        private void OpenItemBrowser()
        {
            if (addItemDialog != null)
            {
                string playerName = PlayerPrefs.GetString("UserName", "Player");
                addItemDialog.ShowDialog(playerName);
                Debug.Log($"[GroupInventory] Opening add item dialog for {playerName}");
            }
            else
            {
                // Fallback: Navigate to item browser
                Debug.LogWarning("[GroupInventory] AddItemDialog not assigned, navigating to ItemBrowser");
                NavigateTo(UIPageType.ItemBrowser);
            }
        }


        private async void OnCustomItemCreatedForGroup(CustomItemData customItem)
        {
            Debug.Log($"[GroupInventory] Custom item created for group: {customItem.itemName}");

            try
            {
                // Use built-in conversion method
                var inventoryItem = customItem.ToInventoryItem();

                // Keep owner empty for party items
                inventoryItem.currentOwner = ""; // Empty = party item
                //inventoryItem.isCustomItem = true;

                // Add to network inventory
                if (inventoryManager != null)
                {
                    inventoryManager.AddItemAsync(inventoryItem);

                    await System.Threading.Tasks.Task.Delay(300);
                    RefreshContent();
                }
                else
                {
                    ShowMessage("Network manager not available", MessageType.Error);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GroupInventory] Error adding custom item: {e.Message}");
                ShowMessage($"Error: {e.Message}", MessageType.Error);
            }
        }

        private async void OnItemSelectedFromSearch(InventoryItem item)
        {
            Debug.Log($"[Group Inventory] Item selected from search: {item.itemName}");

            try
            {
                // todo update owner to be the group id
                item.currentOwner = "";

                Debug.Log($"[GroupInventory] InventoryManager exists: {InventoryManager.Instance != null}");

                // Add to network inventory
                if (InventoryManager.Instance != null)
                {
                    Debug.Log($"[GroupInventory] Calling InventoryManager.AddItem()");
                    InventoryManager.Instance.AddItem(item);

                    await System.Threading.Tasks.Task.Delay(300);
                    RefreshContent();
                }
                else
                {
                    Debug.LogError("[GroupInventory] InventoryManager.Instance is NULL!");
                    ShowMessage("Network manager not available", MessageType.Error);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Group Inventory] Error adding searched item: {e.Message}");
                ShowMessage($"Error: {e.Message}", MessageType.Error);
            }
        }

        private void OnAddItemDialogClosed()
        {
            Debug.Log("[GroupInventory] Add item dialog closed");
        }

        private void CycleSortMode()
        {
            // Cycle through different sort modes
            ShowMessage("Sort mode cycling - feature coming soon!", MessageType.Info);
        }

        private void OpenUserManagement()
        {
            ShowMessage("User management dialog - feature coming soon!", MessageType.Info);
        }

        private void ExportGroupInventory()
        {
            ShowMessage("Exporting group inventory...", MessageType.Info);
            // TODO: Implement group inventory export
        }

        private void ShareInventoryLink()
        {
            // Generate shareable link or invite code
            string inviteCode = System.Guid.NewGuid().ToString("N")[..8].ToUpper();
            ShowMessage($"Invite code: {inviteCode} (copied to clipboard)", MessageType.Success);

            // TODO: Implement actual link sharing system
        }

        private void Reconnect()
        {
            ShowMessage("Attempting to reconnect...", MessageType.Info);

            // TODO: Implement network reconnection logic
            if (inventoryManager != null)
            {
                // inventoryManager.Reconnect();
            }
        }

        void OnDestroy()
        {
            if (inventoryManager != null)
            {
                inventoryManager.OnInventoryChanged -= OnNetworkInventoryChanged;
                inventoryManager.OnInventoryMessage -= OnNetworkMessage;
                
            }
            if (userManager != null)
            {
                userManager.OnUsersChanged -= OnUsersChanged;
            }

            // Clean up dialog events
            if (addItemDialog != null)
            {
                addItemDialog.OnItemCreated -= OnCustomItemCreatedForGroup;
                addItemDialog.OnItemSelected -= OnItemSelectedFromSearch;
                addItemDialog.OnDialogClosed -= OnAddItemDialogClosed;
            }
        }
        /*
        [ContextMenu("Run Inventory Diagnostics")]
        public void RunInventoryDiagnostics()
        {
            Debug.Log("========================================");
            Debug.Log("INVENTORY CONNECTION DIAGNOSTICS");
            Debug.Log("========================================");

            // Check AddItemDialog
            Debug.Log($"1. AddItemDialog Reference: {(addItemDialog != null ? "✅ Assigned" : "❌ NULL")}");
            if (addItemDialog != null)
            {
                Debug.Log($"   - Dialog GameObject: {addItemDialog.gameObject.name}");
                Debug.Log($"   - Dialog Active: {addItemDialog.gameObject.activeInHierarchy}");

                // Check event subscriptions via reflection
                var eventField = typeof(AddItemDialog).GetField("OnItemSelected",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (eventField != null)
                {
                    var eventDelegate = eventField.GetValue(addItemDialog) as System.Delegate;
                    if (eventDelegate != null)
                    {
                        Debug.Log($"   - OnItemSelected subscribers: {eventDelegate.GetInvocationList().Length}");
                        foreach (var handler in eventDelegate.GetInvocationList())
                        {
                            Debug.Log($"     • {handler.Method.DeclaringType.Name}.{handler.Method.Name}");
                        }
                    }
                    else
                    {
                        Debug.LogError("   - ❌ OnItemSelected has NO subscribers!");
                    }
                }
            }

            Debug.Log("");

            // Check NetworkInventoryManager
            Debug.Log($"2. NetworkInventoryManager.Instance: {(NetworkInventoryManager.Instance != null ? " Exists" : " NULL")}");
            if (NetworkInventoryManager.Instance != null)
            {
                var nim = NetworkInventoryManager.Instance;
                Debug.Log($"   - GameObject: {nim.gameObject.name}");
                Debug.Log($"   - GameObject Active: {nim.gameObject.activeInHierarchy}");

                // Use reflection to check Netcode status without requiring the package
                var networkBehaviourType = nim.GetType().BaseType;
                if (networkBehaviourType != null && networkBehaviourType.Name == "NetworkBehaviour")
                {
                    Debug.Log($"   - Is NetworkBehaviour:  Yes");

                    try
                    {
                        var isSpawnedProp = networkBehaviourType.GetProperty("IsSpawned");
                        var isServerProp = networkBehaviourType.GetProperty("IsServer");
                        var isClientProp = networkBehaviourType.GetProperty("IsClient");

                        if (isSpawnedProp != null)
                            Debug.Log($"   - IsSpawned: {isSpawnedProp.GetValue(nim)}");
                        if (isServerProp != null)
                            Debug.Log($"   - IsServer: {isServerProp.GetValue(nim)}");
                        if (isClientProp != null)
                            Debug.Log($"   - IsClient: {isClientProp.GetValue(nim)}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"   - Could not read Netcode properties: {e.Message}");
                    }
                }
                else
                {
                    Debug.Log($"   - Is NetworkBehaviour:  No (not using Netcode)");
                }

                var inventory = nim.GetCurrentInventory();
                Debug.Log($"   - Current Inventory Count: {inventory?.Count ?? 0}");

                if (inventory != null && inventory.Count > 0)
                {
                    Debug.Log($"   - Sample items:");
                    foreach (var item in inventory.Take(3))
                    {
                        Debug.Log($"     • {item.itemName} (x{item.quantity})");
                    }
                }
            }

            Debug.Log("");

            // Check if Netcode package exists
            var netcodeAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Unity.Netcode.Runtime");

            if (netcodeAssembly != null)
            {
                Debug.Log($"3. Unity Netcode Package:  Installed");

                // Try to get NetworkManager via reflection
                var networkManagerType = netcodeAssembly.GetType("Unity.Netcode.NetworkManager");
                if (networkManagerType != null)
                {
                    var singletonProp = networkManagerType.GetProperty("Singleton",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                    if (singletonProp != null)
                    {
                        var singleton = singletonProp.GetValue(null);
                        Debug.Log($"   - NetworkManager.Singleton: {(singleton != null ? " Exists" : " NULL")}");

                        if (singleton != null)
                        {
                            var isServerProp = networkManagerType.GetProperty("IsServer");
                            var isClientProp = networkManagerType.GetProperty("IsClient");
                            var isListeningProp = networkManagerType.GetProperty("IsListening");

                            if (isServerProp != null)
                                Debug.Log($"   - IsServer: {isServerProp.GetValue(singleton)}");
                            if (isClientProp != null)
                                Debug.Log($"   - IsClient: {isClientProp.GetValue(singleton)}");
                            if (isListeningProp != null)
                            {
                                bool isListening = (bool)isListeningProp.GetValue(singleton);
                                Debug.Log($"   - IsListening: {isListening}");

                                if (!isListening)
                                {
                                    Debug.LogWarning("   -  Netcode not started! Call StartHost() or StartClient() to enable networking");
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                Debug.Log($"3. Unity Netcode Package:  Not Installed");
            }

            Debug.Log("");

            // Check local state
            Debug.Log($"4. GroupInventoryPage State:");
            Debug.Log($"   - groupItems count: {groupItems?.Count ?? 0}");
            Debug.Log($"   - itemCardObjects count: {itemCardObjects?.Count ?? 0}");
            Debug.Log($"   - networkManager reference: {(networkManager != null ? " Assigned" : " NULL")}");

            Debug.Log("");
            Debug.Log($"5. Event Subscriptions:");
            if (networkManager != null)
            {
                // Check if events are subscribed
                var onInventoryChangedField = typeof(NetworkInventoryManager).GetField("OnInventoryChanged",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (onInventoryChangedField != null)
                {
                    var eventDelegate = onInventoryChangedField.GetValue(networkManager) as System.Delegate;
                    if (eventDelegate != null)
                    {
                        Debug.Log($"   - OnInventoryChanged subscribers: {eventDelegate.GetInvocationList().Length}");
                    }
                    else
                    {
                        Debug.LogWarning("   - ⚠️ OnInventoryChanged has NO subscribers!");
                    }
                }
            }

            Debug.Log("========================================");
            Debug.Log("DIAGNOSTIC COMPLETE");
            Debug.Log("========================================");
        }

        */

    }
}