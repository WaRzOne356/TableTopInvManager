using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InventorySystem.UI.Navigation;
using InventorySystem.UI.Dialogs;
using InventorySystem.Data;

// ============================================================================
// GROUP INVENTORY PAGE - UPDATED FOR CHARACTER-BASED OWNERSHIP
// ============================================================================
namespace InventorySystem.UI.Pages
{
    public class GroupInventoryPage : UIPage
    {
        [Header("Group Inventory Display")]
        [SerializeField] private Transform itemContainer;
        [SerializeField] private ScrollRect inventoryScrollView;
        [SerializeField] private GameObject itemCardPrefab;

        [Header("Dialogs")]
        [SerializeField] private AddItemDialog addItemDialog;
        [SerializeField] private ClaimItemDialog claimItemDialog;

        [Header("Group Controls")]
        [SerializeField] private TMP_InputField searchField;
        [SerializeField] private TMP_Dropdown categoryFilter;
        [SerializeField] private TMP_Dropdown characterFilter;
        [SerializeField] private TMP_Dropdown availabilityFilter; // All, Available, Claimed
        [SerializeField] private Button addItemButton;
        [SerializeField] private Button sortButton;

        [Header("Group Stats")]
        [SerializeField] private TextMeshProUGUI totalItemsText;
        [SerializeField] private TextMeshProUGUI totalWeightText;
        [SerializeField] private TextMeshProUGUI totalValueText;
        [SerializeField] private TextMeshProUGUI availableItemsText;
        [SerializeField] private TextMeshProUGUI claimedItemsText;
        [SerializeField] private TextMeshProUGUI groupMemberText;

        [Header("Group Management")]
        [SerializeField] private Button manageGroupButton;
        [SerializeField] private Button exportGroupInventoryButton;

        [Header("Connection Status")]
        [SerializeField] private TextMeshProUGUI connectionStatusText;
        [SerializeField] private TextMeshProUGUI lastSyncText;

        // State
        private string currentUserId;
        private string currentGroupId;
        private List<InventoryItem> groupItems = new List<InventoryItem>();
        private List<ItemOwnership> allOwnerships = new List<ItemOwnership>();
        private List<PlayerCharacter> groupCharacters = new List<PlayerCharacter>();
        private List<GameObject> itemCardObjects = new List<GameObject>();
        private InventoryManager inventoryManager;
        private GroupManager groupManager;
        private CharacterManager characterManager;
        private ItemCardUI currentlyExpandedCard;

        void Awake()
        {
            pageType = UIPageType.GroupInventory;
            pageTitle = "Group Inventory";

            SetupEventHandlers();
            SetupAddItemDialog();
        }

        void Start()
        {
            inventoryManager = InventoryManager.Instance;
            groupManager = GroupManager.Instance;
            characterManager = CharacterManager.Instance;

            if (inventoryManager != null)
            {
                inventoryManager.OnInventoryChanged += OnInventoryChanged;
                inventoryManager.OnInventoryMessage += OnInventoryMessage;
            }

            if (groupManager != null)
            {
                groupManager.OnCurrentGroupChanged += OnCurrentGroupChanged;
            }

            if (characterManager != null)
            {
                characterManager.OnCharactersChanged += OnCharactersChanged;
            }
        }

        private void SetupAddItemDialog()
        {
            if (addItemDialog != null)
            {
                addItemDialog.OnItemCreated += OnCustomItemCreatedForGroup;
                addItemDialog.OnItemSelected += OnItemSelectedFromSearch;
                addItemDialog.OnDialogClosed += OnAddItemDialogClosed;
            }
        }

        private void SetupEventHandlers()
        {
            searchField?.onValueChanged.AddListener(OnSearchChanged);
            categoryFilter?.onValueChanged.AddListener(OnFilterChanged);
            characterFilter?.onValueChanged.AddListener(OnFilterChanged);
            availabilityFilter?.onValueChanged.AddListener(OnFilterChanged);
            addItemButton?.onClick.AddListener(OpenItemBrowser);
            sortButton?.onClick.AddListener(CycleSortMode);
            manageGroupButton?.onClick.AddListener(OpenGroupManagement);
            exportGroupInventoryButton?.onClick.AddListener(ExportGroupInventory);
        }

        protected override void RefreshContent()
        {
            LoadCurrentUser();
            LoadCurrentGroup();
            LoadGroupInventory();
            LoadGroupCharacters();
            LoadOwnership();
            UpdateCharacterFilter();
            UpdateGroupStats();
            UpdateConnectionStatus();
            RefreshItemDisplay();
        }

        // =====================================================================
        // DATA LOADING
        // =====================================================================

        private void LoadCurrentUser()
        {
            currentUserId = PlayerPrefs.GetString("UserId", "");

            if (string.IsNullOrEmpty(currentUserId))
            {
                Debug.LogError("[GroupInventory] No userId found!");
                currentUserId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString("UserId", currentUserId);
                PlayerPrefs.Save();
            }
        }

        private void LoadCurrentGroup()
        {
            if (groupManager == null) return;

            var currentGroup = groupManager.GetCurrentGroup();
            if (currentGroup != null)
            {
                currentGroupId = currentGroup.groupId;
                Debug.Log($"[GroupInventory] Current group: {currentGroup.groupName}");
            }
            else
            {
                Debug.LogWarning("[GroupInventory] No current group!");
            }
        }

        private void LoadGroupInventory()
        {
            groupItems.Clear();

            if (inventoryManager != null)
            {
                groupItems.AddRange(inventoryManager.GetCurrentInventory());
                Debug.Log($"[GroupInventory] Loaded {groupItems.Count} group items");
            }
        }

        private void LoadGroupCharacters()
        {
            groupCharacters.Clear();

            if (groupManager == null || string.IsNullOrEmpty(currentGroupId)) return;

            groupCharacters = groupManager.GetGroupCharacters(currentGroupId);
            Debug.Log($"[GroupInventory] Loaded {groupCharacters.Count} characters in group");
        }

        private void LoadOwnership()
        {
            allOwnerships.Clear();

            if (inventoryManager != null)
            {
                allOwnerships = inventoryManager.GetAllOwnerships();
                Debug.Log($"[GroupInventory] Loaded {allOwnerships.Count} ownership records");
            }
        }

        private void UpdateCharacterFilter()
        {
            if (characterFilter == null) return;

            characterFilter.ClearOptions();

            var options = new List<string> { "All Characters", "Unassigned" };
            options.AddRange(groupCharacters.Select(c => c.characterName));

            characterFilter.AddOptions(options);
        }

        // =====================================================================
        // DISPLAY MANAGEMENT
        // =====================================================================

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
                var categories = Enum.GetValues(typeof(ItemCategory)).Cast<ItemCategory>().ToArray();
                var selectedCategory = categories[categoryFilter.value - 1];
                filtered = filtered.Where(item => item.category == selectedCategory).ToList();
            }

            // Apply character filter
            if (characterFilter != null && characterFilter.value > 0)
            {
                if (characterFilter.value == 1) // "Unassigned"
                {
                    // Show only items with no ownership or available quantity
                    filtered = filtered.Where(item =>
                    {
                        int totalOwned = allOwnerships
                            .Where(o => o.itemId == item.itemId)
                            .Sum(o => o.quantityOwned);
                        return totalOwned < item.quantity; // Has available quantity
                    }).ToList();
                }
                else // Specific character
                {
                    int charIndex = characterFilter.value - 2;
                    if (charIndex >= 0 && charIndex < groupCharacters.Count)
                    {
                        string characterId = groupCharacters[charIndex].characterId;

                        // Show items owned by this character
                        var ownedItemIds = allOwnerships
                            .Where(o => o.characterId == characterId && o.quantityOwned > 0)
                            .Select(o => o.itemId)
                            .ToHashSet();

                        filtered = filtered.Where(item => ownedItemIds.Contains(item.itemId)).ToList();
                    }
                }
            }

            // Apply availability filter
            if (availabilityFilter != null && availabilityFilter.value > 0)
            {
                if (availabilityFilter.value == 1) // "Available Only"
                {
                    filtered = filtered.Where(item =>
                    {
                        int totalOwned = allOwnerships
                            .Where(o => o.itemId == item.itemId)
                            .Sum(o => o.quantityOwned);
                        return totalOwned < item.quantity;
                    }).ToList();
                }
                else if (availabilityFilter.value == 2) // "Claimed Only"
                {
                    filtered = filtered.Where(item =>
                    {
                        int totalOwned = allOwnerships
                            .Where(o => o.itemId == item.itemId)
                            .Sum(o => o.quantityOwned);
                        return totalOwned > 0;
                    }).ToList();
                }
            }

            return filtered.OrderBy(item => item.category).ThenBy(item => item.itemName).ToList();
        }

        private void CreateGroupItemCard(InventoryItem item)
        {
            if (itemCardPrefab == null || itemContainer == null) return;

            GameObject cardObj = Instantiate(itemCardPrefab, itemContainer);
            ItemCardUI cardUI = cardObj.GetComponent<ItemCardUI>();

            if (cardUI != null)
            {
                // Calculate ownership info
                var ownerships = allOwnerships.Where(o => o.itemId == item.itemId).ToList();
                int totalOwned = ownerships.Sum(o => o.quantityOwned);
                int available = item.quantity - totalOwned;

                // Create ownership summary
                string ownershipSummary = GetOwnershipSummary(item.itemId);

                cardUI.SetupCard(item, ItemCardUI.CardMode.Group);

                // Set ownership display
                cardUI.SetOwnershipInfo(ownershipSummary, available, totalOwned);

                // Subscribe to events
                cardUI.OnCardSelected += OnCardExpanded;
                cardUI.OnItemModified += OnGroupItemModified;
                cardUI.OnItemDeleted += OnItemDeleted;
                cardUI.OnClaimRequested += OnClaimRequested;
                cardUI.OnShareRequested += OnShareRequested;
                cardUI.OnReturnRequested += OnReturnRequested;
                cardUI.OnTransferRequested += OnTransferToPersonalRequested;


                itemCardObjects.Add(cardObj);
            }
        }

        private string GetOwnershipSummary(string itemId)
        {
            var ownerships = allOwnerships.Where(o => o.itemId == itemId).ToList();

            if (ownerships.Count == 0)
                return "Party Storage";

            var ownershipList = new List<string>();

            foreach (var ownership in ownerships)
            {
                var character = groupCharacters.FirstOrDefault(c => c.characterId == ownership.characterId);
                string characterName = character?.characterName ?? "Unknown";
                ownershipList.Add($"{characterName} ({ownership.quantityOwned})");
            }

            var item = groupItems.FirstOrDefault(i => i.itemId == itemId);
            int available = item != null ? item.quantity - ownerships.Sum(o => o.quantityOwned) : 0;

            if (available > 0)
                ownershipList.Add($"Party ({available})");

            return string.Join(", ", ownershipList);
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


            // Calculate available vs claimed
            int availableCount = 0;
            int claimedCount = 0;

            foreach (var item in groupItems)
            {
                int totalOwned = allOwnerships
                    .Where(o => o.itemId == item.itemId)
                    .Sum(o => o.quantityOwned);

                int available = item.quantity - totalOwned;

                if (available > 0)
                    availableCount++;
                if (totalOwned > 0)
                    claimedCount++;
            }

            if (availableItemsText != null)
                availableItemsText.text = $"Available: {availableCount}";

            if (claimedItemsText != null)
                claimedItemsText.text = $"Claimed: {claimedCount}";

            if (groupMemberText != null)
                groupMemberText.text = $"Members: {groupCharacters.Count}";
            
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
                lastSyncText.text = $"Last sync: {DateTime.Now:HH:mm:ss}";
            }
        }

        // =====================================================================
        // EVENT HANDLERS
        // =====================================================================

        private void OnInventoryChanged(List<InventoryItem> updatedInventory)
        {
            LoadGroupInventory();
            LoadOwnership();
            RefreshItemDisplay();
        }

        private void OnInventoryMessage(string message)
        {
            ShowMessage(message, MessageType.Info);
        }

        private void OnCurrentGroupChanged(Group newGroup)
        {
            if (newGroup != null)
            {
                currentGroupId = newGroup.groupId;
                RefreshContent();
            }
        }

        private void OnCharactersChanged(List<PlayerCharacter> characters)
        {
            LoadGroupCharacters();
            UpdateCharacterFilter();
            RefreshItemDisplay();
        }

        private void OnSearchChanged(string searchTerm)
        {
            RefreshItemDisplay();
        }

        private void OnFilterChanged(int filterIndex)
        {
            RefreshItemDisplay();
        }

        private void OnCardExpanded(ItemCardUI expandedCard)
        {
            if (currentlyExpandedCard != null && currentlyExpandedCard != expandedCard)
            {
                currentlyExpandedCard.CollapseCard();
            }
            currentlyExpandedCard = expandedCard;
        }

        private async void OnGroupItemModified(InventoryItem item)
        {
            Debug.Log($"[GroupInventory] Item modified: {item.itemName}");

            if (inventoryManager != null)
            {
                await inventoryManager.UpdateItemQuantityAsync(item.itemId, item.quantity);
            }

            UpdateGroupStats();
        }

        private async void OnItemDeleted(InventoryItem item)
        {
            Debug.Log($"[GroupInventory] Deleting item: {item.itemName}");

            if (inventoryManager != null)
            {
                await inventoryManager.RemoveItemAsync(item.itemId);
            }

            RefreshContent();
        }

        private void OnClaimRequested(InventoryItem item)
        {
            if (claimItemDialog != null)
            {
                // Get user's characters
                var userCharacters = characterManager?.GetCharactersByUser(currentUserId) ?? new List<PlayerCharacter>();

                if (userCharacters.Count == 0)
                {
                    ShowMessage("Create a character first to claim items", MessageType.Warning);
                    return;
                }

                // Calculate available quantity
                int totalOwned = allOwnerships
                    .Where(o => o.itemId == item.itemId)
                    .Sum(o => o.quantityOwned);
                int available = item.quantity - totalOwned;

                if (available <= 0)
                {
                    ShowMessage("No available quantity to claim", MessageType.Warning);
                    return;
                }

                claimItemDialog.ShowDialog(item, userCharacters, available);
                claimItemDialog.OnItemClaimed += OnItemClaimed;
            }
            else
            {
                ShowMessage("Claim dialog not available", MessageType.Error);
            }
        }

        private async void OnItemClaimed(string itemId, string characterId, int quantity)
        {
            Debug.Log($"[GroupInventory] Claiming {quantity}x item {itemId} for character {characterId}");

            if (inventoryManager != null)
            {
                await inventoryManager.UpdateOwnershipAsync(itemId, characterId, quantity);

                ShowMessage($"Claimed {quantity} item(s)", MessageType.Success);

                LoadOwnership();
                RefreshItemDisplay();
            }
        }

        private void OpenItemBrowser()
        {
            if (addItemDialog != null)
            {
                string userName = PlayerPrefs.GetString("UserName", "Player");
                addItemDialog.ShowDialog(userName);
            }
        }

        private async void OnCustomItemCreatedForGroup(CustomItemData customItem)
        {
            Debug.Log($"[GroupInventory] Custom item created: {customItem.itemName}");

            var inventoryItem = customItem.ToInventoryItem();

            if (inventoryManager != null)
            {
                await inventoryManager.AddItemAsync(inventoryItem);
                await System.Threading.Tasks.Task.Delay(300);
                RefreshContent();
            }
        }

        private async void OnItemSelectedFromSearch(InventoryItem item)
        {
            Debug.Log($"[GroupInventory] Item selected from search: {item.itemName}");

            if (inventoryManager != null)
            {
                await inventoryManager.AddItemAsync(item);
                await System.Threading.Tasks.Task.Delay(300);
                RefreshContent();
            }
        }

        private void OnAddItemDialogClosed()
        {
            Debug.Log("[GroupInventory] Add item dialog closed");
        }


        /// <summary>
        /// Character claims (shares) ownership of item from group pool
        /// Item stays in group inventory, character is marked as owner
        /// </summary>
        private async void OnShareRequested(InventoryItem item, int quantity)
        {
            string currentCharacterId = PlayerPrefs.GetString("SelectedCharacterId", "");

            if (string.IsNullOrEmpty(currentCharacterId))
            {
                ShowMessage("No character selected", MessageType.Warning);
                return;
            }

            if (inventoryManager == null) return;

            // Check unclaimed quantity
            int unclaimed = inventoryManager.GetUnallocatedQuantity(item.itemId);

            if (quantity > unclaimed)
            {
                ShowMessage($"Only {unclaimed} unclaimed available", MessageType.Warning);
                return;
            }

            try
            {
                // Claim ownership (adds ItemOwnership record)
                await inventoryManager.UpdateOwnershipAsync(
                    item.itemId,
                    currentCharacterId,
                    quantity
                );

                var character = characterManager?.GetCharacterById(currentCharacterId);
                ShowMessage($"{character?.characterName ?? "Character"} claimed {quantity}x {item.itemName}", MessageType.Success);

                Debug.Log($"[GroupInventory] Character {currentCharacterId} claimed {quantity}x {item.itemName}");

                // Refresh display
                LoadOwnership();
                RefreshItemDisplay();
            }
            catch (Exception e)
            {
                ShowMessage($"Claim failed: {e.Message}", MessageType.Error);
                Debug.LogError($"[GroupInventory] Claim error: {e}");
            }
        }

        /// <summary>
        /// Character returns (unshares) ownership back to group pool
        /// Item stays in group inventory, just releases ownership
        /// </summary>
        private async void OnReturnRequested(InventoryItem item, int quantity)
        {
            string currentCharacterId = PlayerPrefs.GetString("SelectedCharacterId", "");

            if (string.IsNullOrEmpty(currentCharacterId))
            {
                ShowMessage("No character selected", MessageType.Warning);
                return;
            }

            if (inventoryManager == null) return;

            // Check how much character owns
            int owned = inventoryManager.GetCharacterOwnership(item.itemId, currentCharacterId);

            if (quantity > owned)
            {
                ShowMessage($"You only own {owned}", MessageType.Warning);
                return;
            }

            try
            {
                // Return ownership (removes/reduces ItemOwnership record)
                await inventoryManager.ReturnOwnershipAsync(
                    item.itemId,
                    currentCharacterId,
                    quantity
                );

                var character = characterManager?.GetCharacterById(currentCharacterId);
                ShowMessage($"{character?.characterName ?? "Character"} returned {quantity}x {item.itemName} to pool", MessageType.Success);

                Debug.Log($"[GroupInventory] Character {currentCharacterId} returned {quantity}x {item.itemName}");

                // Refresh display
                LoadOwnership();
                RefreshItemDisplay();
            }
            catch (Exception e)
            {
                ShowMessage($"Return failed: {e.Message}", MessageType.Error);
                Debug.LogError($"[GroupInventory] Return error: {e}");
            }
        }

        /// <summary>
        /// Character transfers item FROM group TO personal storage (PERMANENT)
        /// Item is removed from group entirely and added to personal inventory
        /// </summary>
        private async void OnTransferToPersonalRequested(InventoryItem item)
        {
            string currentCharacterId = PlayerPrefs.GetString("SelectedCharacterId", "");

            if (string.IsNullOrEmpty(currentCharacterId))
            {
                ShowMessage("No character selected", MessageType.Warning);
                return;
            }

            var personalStorage = PersonalStorageManager.Instance;
            if (inventoryManager == null || personalStorage == null) return;

            // Check ownership
            int owned = inventoryManager.GetCharacterOwnership(item.itemId, currentCharacterId);

            if (owned <= 0)
            {
                ShowMessage("You don't own any of this item", MessageType.Warning);
                return;
            }

            // TODO: Show quantity dialog if owned > 1
            int quantityToTransfer = owned; // For now, transfer all

            try
            {
                Debug.Log($"[GroupInventory] PERMANENT TRANSFER: {quantityToTransfer}x {item.itemName} to personal");

                // 1. Return ownership (unclaim)
                await inventoryManager.ReturnOwnershipAsync(item.itemId, currentCharacterId, quantityToTransfer);

                // 2. Reduce group's total quantity
                int newGroupQuantity = item.quantity - quantityToTransfer;
                await inventoryManager.UpdateItemQuantityAsync(item.itemId, newGroupQuantity);

                // 3. Add to personal storage
                var personalItem = new InventoryItem(item.itemName, item.category)
                {
                    itemId = Guid.NewGuid().ToString(), // New ID
                    description = item.description,
                    quantity = quantityToTransfer,
                    weight = item.weight,
                    valueInGold = item.valueInGold,
                    thumbnailUrl = item.thumbnailUrl,
                    sourceUrl = item.sourceUrl,
                    properties = new Dictionary<string, string>(item.properties ?? new Dictionary<string, string>())
                };

                await personalStorage.AddItemAsync(personalItem, currentCharacterId);

                var character = characterManager?.GetCharacterById(currentCharacterId);
                ShowMessage($"Transferred {quantityToTransfer}x {item.itemName} to {character?.characterName}'s personal storage (PERMANENT)", MessageType.Success);

                // Refresh
                RefreshContent();
            }
            catch (Exception e)
            {
                ShowMessage($"Transfer failed: {e.Message}", MessageType.Error);
                Debug.LogError($"[GroupInventory] Transfer error: {e}");
            }
        }

        private void CycleSortMode()
        {
            ShowMessage("Sort mode cycling - feature coming soon!", MessageType.Info);
        }

        private void OpenGroupManagement()
        {
            NavigateTo(UIPageType.GroupManagement);
        }

        private void ExportGroupInventory()
        {
            ShowMessage("Exporting group inventory...", MessageType.Info);
            // TODO: Implement export
        }

        void OnDestroy()
        {
            if (inventoryManager != null)
            {
                inventoryManager.OnInventoryChanged -= OnInventoryChanged;
                inventoryManager.OnInventoryMessage -= OnInventoryMessage;
            }

            if (groupManager != null)
            {
                groupManager.OnCurrentGroupChanged -= OnCurrentGroupChanged;
            }

            if (characterManager != null)
            {
                characterManager.OnCharactersChanged -= OnCharactersChanged;
            }

            if (addItemDialog != null)
            {
                addItemDialog.OnItemCreated -= OnCustomItemCreatedForGroup;
                addItemDialog.OnItemSelected -= OnItemSelectedFromSearch;
                addItemDialog.OnDialogClosed -= OnAddItemDialogClosed;
            }

            if (claimItemDialog != null)
            {
                claimItemDialog.OnItemClaimed -= OnItemClaimed;
            }
            // Clean up item card event subscriptions
            foreach (var cardObj in itemCardObjects)
            {
                if (cardObj != null)
                {
                    var card = cardObj.GetComponent<ItemCardUI>();
                    if (card != null)
                    {
                        card.OnShareRequested -= OnShareRequested;
                        card.OnReturnRequested -= OnReturnRequested;
                        card.OnTransferRequested -= OnTransferToPersonalRequested;
                    }
                }
            }
        }
    }
}