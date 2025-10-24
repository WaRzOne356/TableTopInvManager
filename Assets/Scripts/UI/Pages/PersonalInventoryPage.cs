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

        [Header("Dialogs")]
        [SerializeField] private AddItemDialog addItemDialog;

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
            SetupAddItemDialog();
        }

        private void SetupAddItemDialog()
        {
            if (addItemDialog != null)
            {
                addItemDialog.OnItemCreated += OnCustomItemCreated;
                addItemDialog.OnItemSelected += OnItemSelectedFromSearch;
                addItemDialog.OnDialogClosed += OnAddItemDialogClosed;
            }
            else
            {
                Debug.LogWarning("[PersonalInventory] AddItemDialog reference not set!");
            }
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
            // Debounce search to avoid refreshing on every keystroke
            // In a production app, you'd want to add a small delay here
            Debug.Log($"[PersonalInventory] Search changed: '{searchTerm}'");
            RefreshItemDisplay();
        }

        private void OnCategoryFilterChanged(int filterIndex)
        {
            string categoryName = filterIndex > 0 ? categoryFilter.options[filterIndex].text : "All";
            Debug.Log($"[PersonalInventory] Category filter changed to: {categoryName}");
            RefreshItemDisplay();
        }

        private void OnShowOwnedOnlyChanged(bool ownedOnly)
        {
            Debug.Log($"[PersonalInventory] Show owned only: {ownedOnly}");
            RefreshItemDisplay();
        }

        private void OnItemModified(InventoryItem item)
        {
            Debug.Log($"[PersonalInventory] Item modified: {item.itemName}, Quantity: {item.quantity}");

            // Update the item in the network inventory
            if (NetworkInventoryManager.Instance != null)
            {
                NetworkInventoryManager.Instance.UpdateItemQuantity(item.itemId, item.quantity);
            }
            else
            {
                Debug.LogWarning("[PersonalInventory] NetworkInventoryManager not available, changes not synced");
            }

            // Refresh to show updated stats
            UpdatePersonalStats();
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
            if (addItemDialog != null)
            {
                string playerName = GetCurrentPlayerName();
                addItemDialog.ShowDialog(playerName);
                Debug.Log($"[PersonalInventory] Opening add item dialog for {playerName}");
            }
            else
            {
                // Fallback: Navigate to item browser if dialog not available
                Debug.LogWarning("[PersonalInventory] AddItemDialog not assigned, navigating to ItemBrowser");
                NavigateTo(UIPageType.ItemBrowser);
            }
        }

        private async void OnCustomItemCreated(CustomItemData customItem)
        {
            Debug.Log($"[PersonalInventory] Custom item created: {customItem.itemName}");

            try
            {
                // Use built-in conversion method
                var inventoryItem = customItem.ToInventoryItem();

                // Override owner to assign to current player
                inventoryItem.currentOwner = GetCurrentPlayerName();
                // inventoryItem.isCustomItem = true;

                // Add to network inventory
                if (NetworkInventoryManager.Instance != null)
                {
                    NetworkInventoryManager.Instance.AddItem(inventoryItem);

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
                Debug.LogError($"[PersonalInventory] Error adding custom item: {e.Message}");
                ShowMessage($"Error: {e.Message}", MessageType.Error);
            }
        }

        private void OnAddItemDialogClosed()
        {
            Debug.Log("[PersonalInventory] Add item dialog closed");
            // Optional: Refresh focus or perform cleanup
        }

        private async void OnItemSelectedFromSearch(InventoryItem item)
        {
            Debug.Log($"[PersonalInventory] Item selected from search: {item.itemName}");

            try
            {
                // Override owner to assign to current player
                item.currentOwner = GetCurrentPlayerName();

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
                Debug.LogError($"[PersonalInventory] Error adding searched item: {e.Message}");
                ShowMessage($"Error: {e.Message}", MessageType.Error);
            }
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

        void OnDestroy()
        {
            // Clean up dialog events
            if (addItemDialog != null)
            {
                addItemDialog.OnItemCreated -= OnCustomItemCreated;
                addItemDialog.OnItemSelected -= OnItemSelectedFromSearch;
                addItemDialog.OnDialogClosed -= OnAddItemDialogClosed;
            }
        }
    }
}