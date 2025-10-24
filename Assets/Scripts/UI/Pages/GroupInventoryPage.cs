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
        private NetworkInventoryManager networkManager;

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
            }
            else
            {
                Debug.LogWarning("[GroupInventory] AddItemDialog reference not set!");
            }
        }

        void Start()
        {
            networkManager = NetworkInventoryManager.Instance;
            if (networkManager != null)
            {
                networkManager.OnInventoryChanged += OnNetworkInventoryChanged;
                networkManager.OnInventoryMessage += OnNetworkMessage;
                networkManager.OnUsersChanged += OnUsersChanged;
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

            if (networkManager != null)
            {
                var inventory = networkManager.GetCurrentInventory();
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
                {
                    // Use the unified ItemCardUI with Group mode
                    cardUI.SetupGroupCard(item);
                    cardUI.OnItemModified += OnGroupItemModified;
                }

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
            if (groupMembersText != null && networkManager != null)
            {
                // This would come from the network manager's user list
                groupMembersText.text = "Group Members: 3"; // Placeholder
            }
        }

        private void UpdateConnectionStatus()
        {
            bool isConnected = networkManager != null && networkManager.IsConnected();

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

        private void OnUsersChanged(List<NetworkUserInfo> users)
        {
            UpdateOwnerFilter(users);
            UpdateGroupStats();
        }

        private void UpdateOwnerFilter(List<NetworkUserInfo> users)
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

        private void OnGroupItemModified(InventoryItem item)
        {
            Debug.Log($"[GroupInventory] Item modified: {item.itemName}, Owner: {item.currentOwner ?? "Unassigned"}");

            if (networkManager != null)
            {
                networkManager.UpdateItemQuantity(item.itemId, item.quantity);
            }
            else
            {
                Debug.LogWarning("[GroupInventory] NetworkInventoryManager not available, changes not synced");
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
                if (networkManager != null)
                {
                    networkManager.AddItem(inventoryItem);

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

                // Add to network inventory
                if (NetworkInventoryManager.Instance != null)
                {
                    NetworkInventoryManager.Instance.AddItem(item);

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
            if (networkManager != null)
            {
                // networkManager.Reconnect();
            }
        }

        void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager.OnInventoryChanged -= OnNetworkInventoryChanged;
                networkManager.OnInventoryMessage -= OnNetworkMessage;
                networkManager.OnUsersChanged -= OnUsersChanged;
            }

            // Clean up dialog events
            if (addItemDialog != null)
            {
                addItemDialog.OnItemCreated -= OnCustomItemCreatedForGroup;
                addItemDialog.OnItemSelected -= OnItemSelectedFromSearch;
                addItemDialog.OnDialogClosed -= OnAddItemDialogClosed;
            }
        }
    }
}