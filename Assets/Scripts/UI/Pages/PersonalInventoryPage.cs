using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InventorySystem.UI.Navigation;
using InventorySystem.Data;

namespace InventorySystem.UI.Pages
{
    /// <summary>
    /// Personal inventory management (player-specific items)
    /// </summary>
    public class PersonalInventoryPage : UIPage
    {
        [Header("Inventory Display")]
        [SerializeField] private Transform itemContainer;
        [SerializeField] private ScrollRect inventoryScrollView;
        [SerializeField] private GameObject itemCardPrefab; // Should have ItemCardUI component
        
        [Header("Controls")]
        [SerializeField] private TMP_InputField searchField;
        [SerializeField] private TMP_Dropdown categoryFilter;
        [SerializeField] private Button addItemButton;
        [SerializeField] private Button importItemsButton;
        [SerializeField] private Button exportInventoryButton;
        
        [Header("Personal Stats")]
        [SerializeField] private TextMeshProUGUI totalItemsText;
        [SerializeField] private TextMeshProUGUI totalWeightText;
        [SerializeField] private TextMeshProUGUI totalValueText;
        [SerializeField] private TextMeshProUGUI encumbranceText;

        [Header("View Options")]
        [SerializeField] private Button gridViewButton;
        [SerializeField] private Button listViewButton;
        [SerializeField] private Toggle showOwnedOnlyToggle;
        
        // State
        private List<InventoryItem> personalItems;
        private List<GameObject> itemCardObjects;
        private bool isGridView = true;
        private string currentPlayerName;
        
        void Awake()
        {
            pageType = UIPageType.PersonalInventory;
            pageTitle = "Personal Inventory";
            
            personalItems = new List<InventoryItem>();
            itemCardObjects = new List<GameObject>();
            
            SetupEventHandlers();
        }
        
        private void SetupEventHandlers()
        {
            searchField?.onValueChanged.AddListener(OnSearchChanged);
            categoryFilter?.onValueChanged.AddListener(OnCategoryFilterChanged);
            addItemButton?.onClick.AddListener(OpenAddItemDialog);
            importItemsButton?.onClick.AddListener(ImportItems);
            exportInventoryButton?.onClick.AddListener(ExportInventory);
            gridViewButton?.onClick.AddListener(() => SetViewMode(true));
            listViewButton?.onClick.AddListener(() => SetViewMode(false));
            showOwnedOnlyToggle?.onValueChanged.AddListener(OnShowOwnedOnlyChanged);
        }
        
        protected override void RefreshContent()
        {
            LoadPersonalInventory();
            UpdatePersonalStats();
            RefreshItemDisplay();
        }
        
        private void LoadPersonalInventory()
        {
            currentPlayerName = GetCurrentPlayerName();
            personalItems.Clear();
            
            // Get items from group inventory that belong to this player
            if (NetworkInventoryManager.Instance != null)
            {
                var groupInventory = NetworkInventoryManager.Instance.GetCurrentInventory();
                if (groupInventory != null)
                {
                    personalItems.AddRange(groupInventory.Where(item => 
                        item.currentOwner == currentPlayerName || 
                        string.IsNullOrEmpty(item.currentOwner)));
                }
            }
            
            Debug.Log($"[PersonalInventory] Loaded {personalItems.Count} personal items");
        }
        
        private void RefreshItemDisplay()
        {
            ClearItemCards();
            
            var filteredItems = GetFilteredItems();
            
            foreach (var item in filteredItems)
            {
                CreateItemCard(item);
            }
            
            UpdatePersonalStats();
        }
        
        private List<InventoryItem> GetFilteredItems()
        {
            var filtered = personalItems.ToList();
            
            // Apply search filter
            string searchTerm = searchField?.text?.ToLower() ?? "";
            if (!string.IsNullOrEmpty(searchTerm))
            {
                filtered = filtered.Where(item =>
                    item.itemName.ToLower().Contains(searchTerm) ||
                    item.description.ToLower().Contains(searchTerm) ||
                    item.category.ToString().ToLower().Contains(searchTerm)
                ).ToList();
            }
            
            // Apply category filter
            if (categoryFilter != null && categoryFilter.value > 0)
            {
                var categories = System.Enum.GetValues(typeof(ItemCategory)).Cast<ItemCategory>().ToArray();
                var selectedCategory = categories[categoryFilter.value - 1];
                filtered = filtered.Where(item => item.category == selectedCategory).ToList();
            }
            
            // Apply owned-only filter
            if (showOwnedOnlyToggle != null && showOwnedOnlyToggle.isOn)
            {
                filtered = filtered.Where(item => item.currentOwner == currentPlayerName).ToList();
            }
            
            return filtered.OrderBy(item => item.category).ThenBy(item => item.itemName).ToList();
        }
        
        private void CreateItemCard(InventoryItem item)
        {
            if (itemCardPrefab == null || itemContainer == null) return;
            
            var cardObj = Instantiate(itemCardPrefab, itemContainer);
            var cardUI = cardObj.GetComponent<ItemCardUI>();
            
            if (cardUI != null)
            {
                // Use the unified ItemCardUI with Group mode
                cardUI.SetupGroupCard(item);
                cardUI.OnItemModified += OnGroupItemModified;
            }
            
            itemCardObjects.Add(cardObj);
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
            RefreshItemDisplay();
        }
        
        private void OnFilterChanged(int filterIndex)
        {
            RefreshItemDisplay();
        }
        
        private void OnGroupItemModified(InventoryItem item)
        {
            if (networkManager != null)
            {
                networkManager.UpdateItemQuantity(item.itemId, item.quantity);
            }
        }
        
        private void OpenItemBrowser()
        {
            NavigateTo(UIPageType.ItemBrowser);
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
        }
    }
} Use the unified ItemCardUI with Personal mode
                cardUI.SetupPersonalCard(item, isGridView);
                cardUI.OnItemModified += OnItemModified;
                cardUI.OnOwnershipChanged += OnOwnershipChanged;
            }
            
            itemCardObjects.Add(cardObj);
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
        
        private void UpdatePersonalStats()
        {
            var filteredItems = GetFilteredItems();
            
            if (totalItemsText != null)
                totalItemsText.text = $"Items: {filteredItems.Count}";
            
            float totalWeight = filteredItems.Sum(item => item.TotalWeight);
            if (totalWeightText != null)
                totalWeightText.text = $"Weight: {totalWeight:F1} lbs";
            
            int totalValue = filteredItems.Sum(item => item.TotalValue);
            if (totalValueText != null)
                totalValueText.text = $"Value: {totalValue:N0} gp";
            
            // Calculate encumbrance (assuming 15 STR = 225 lbs capacity)
            float carryCapacity = 225f; // This could come from character stats
            float encumbrancePercent = (totalWeight / carryCapacity) * 100f;
            
            if (encumbranceText != null)
            {
                string encumbranceStatus = encumbrancePercent switch
                {
                    >= 100f => "Overloaded",
                    >= 75f => "Heavy Load", 
                    >= 50f => "Medium Load",
                    _ => "Light Load"
                };
                
                encumbranceText.text = $"Encumbrance: {encumbranceStatus} ({encumbrancePercent:F0}%)";
                encumbranceText.color = encumbrancePercent >= 75f ? Color.red : 
                                       encumbrancePercent >= 50f ? Color.yellow : Color.green;
            }
        }
        
        private void SetViewMode(bool gridView)
        {
            isGridView = gridView;
            
            // Update button states
            if (gridViewButton != null)
                gridViewButton.GetComponent<Image>().color = gridView ? Color.blue : Color.white;
            if (listViewButton != null)
                listViewButton.GetComponent<Image>().color = !gridView ? Color.blue : Color.white;
            
            RefreshItemDisplay();
        }
        
        private void OnSearchChanged(string searchTerm)
        {
            RefreshItemDisplay();
        }
        
        private void OnCategoryFilterChanged(int filterIndex)
        {
            RefreshItemDisplay();
        }
        
        private void OnShowOwnedOnlyChanged(bool ownedOnly)
        {
            RefreshItemDisplay();
        }
        
        private void OnItemModified(InventoryItem item)
        {
            // Update the item in the network inventory
            if (NetworkInventoryManager.Instance != null)
            {
                NetworkInventoryManager.Instance.UpdateItemQuantity(item.itemId, item.quantity);
            }
            
            RefreshItemDisplay();
        }
        
        private void OnOwnershipChanged(InventoryItem item, string newOwner)
        {
            item.currentOwner = newOwner;
            
            // This would need a new network method to update ownership
            ShowMessage($"Changed {item.itemName} owner to {newOwner}", MessageType.Info);
            
            RefreshContent(); // Refresh to update personal vs group items
        }
        
        private void OpenAddItemDialog()
        {
            // Navigate to item browser page or open add dialog
            NavigateTo(UIPageType.ItemBrowser);
        }
        
        private void ImportItems()
        {
            ShowMessage("Import items feature coming soon!", MessageType.Info);
            // TODO: Implement item import from file
        }
        
        private async void ExportInventory()
        {
            try
            {
                SetLoadingState(true);
                
                var exportData = new
                {
                    playerName = currentPlayerName,
                    exportDate = System.DateTime.Now.ToString("O"),
                    itemCount = personalItems.Count,
                    totalWeight = personalItems.Sum(i => i.TotalWeight),
                    totalValue = personalItems.Sum(i => i.TotalValue),
                    items = personalItems.Select(item => new
                    {
                        name = item.itemName,
                        category = item.category.ToString(),
                        quantity = item.quantity,
                        weight = item.weight,
                        value = item.valueInGold,
                        description = item.description
                    })
                };
                
                string jsonData = JsonUtility.ToJson(exportData, true);
                string fileName = $"PersonalInventory_{currentPlayerName}_{System.DateTime.Now:yyyyMMdd}.json";
                string filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
                
                await System.IO.File.WriteAllTextAsync(filePath, jsonData);
                
                ShowMessage($"Inventory exported: {fileName}", MessageType.Success);
            }
            catch (System.Exception e)
            {
                ShowMessage($"Export failed: {e.Message}", MessageType.Error);
            }
            finally
            {
                SetLoadingState(false);
            }
        }
        
        private string GetCurrentPlayerName()
        {
            return PlayerPrefs.GetString("UserName", System.Environment.UserName ?? "Player");
        }
    }

    