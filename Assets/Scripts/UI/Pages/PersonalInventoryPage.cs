using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InventorySystem.UI.Navigation;
using InventorySystem.UI.Dialogs;
using InventorySystem.Data;

namespace InventorySystem.UI.Pages
{
    /// <summary>
    /// Personal inventory management (character-specific items)
    /// UPDATED: Now supports character-based ownership
    /// </summary>
    public class PersonalInventoryPage : UIPage
    {
        [Header("Character Selection")]
        [SerializeField] private TMP_Dropdown characterDropdown;
        [SerializeField] private Button createCharacterButton;
        [SerializeField] private TextMeshProUGUI activeCharacterText;

        [Header("Inventory Display")]
        [SerializeField] private Transform itemContainer;
        [SerializeField] private ScrollRect inventoryScrollView;
        [SerializeField] private GameObject itemCardPrefab;

        [Header("Dialogs")]
        [SerializeField] private AddItemDialog addItemDialog;
        [SerializeField] private CreateCharacterDialog createCharacterDialog;
        [SerializeField] private TransferItemDialog transferItemDialog;

        [Header("Controls")]
        [SerializeField] private TMP_InputField searchField;
        [SerializeField] private TMP_Dropdown categoryFilter;
        [SerializeField] private Button addItemButton;
        [SerializeField] private Button transferToGroupButton; //Button to return selected items to group inventory
        [SerializeField] private Button transferSelectedButton; // Button to transfer to a different character

        [Header("Personal Stats")]
        [SerializeField] private TextMeshProUGUI totalItemsText;
        [SerializeField] private TextMeshProUGUI totalWeightText;
        [SerializeField] private TextMeshProUGUI totalValueText;
        [SerializeField] private TextMeshProUGUI encumbranceText;

        [Header("View Options")]
        [SerializeField] private Button gridViewButton;
        [SerializeField] private Button listViewButton;

        // State
        private string currentUserId;
        private string currentCharacterId;
        private PlayerCharacter currentCharacter;
        private List<PlayerCharacter> userCharacters = new List<PlayerCharacter>();
        private List<ItemOwnership> characterOwnerships = new List<ItemOwnership>();
        private List<InventoryItem> characterItems = new List<InventoryItem>();
        private List<GameObject> itemCardObjects = new List<GameObject>();
        private bool isGridView = true;

        void Awake()
        {
            pageType = UIPageType.PersonalInventory;
            pageTitle = "Personal Inventory";

            SetupEventHandlers();
            SetupAddItemDialog();
            SetupTransferDialog();
            SetupCategoryFilter();

        }

        private void Start()
        {
            // Subscribe to character changes
            var characterManager = CharacterManager.Instance;
            if (characterManager != null)
            {
                characterManager.OnCharactersChanged += OnCharactersChanged;
            }
        }

        private void SetupEventHandlers()
        {
            characterDropdown?.onValueChanged.AddListener(OnCharacterSelectionChanged);
            createCharacterButton?.onClick.AddListener(OpenCreateCharacterDialog);
            searchField?.onValueChanged.AddListener(OnSearchChanged);
            categoryFilter?.onValueChanged.AddListener(OnCategoryFilterChanged);
            addItemButton?.onClick.AddListener(OpenAddItemDialog);
            transferToGroupButton?.onClick.AddListener(OnTransferToGroupClicked);
            gridViewButton?.onClick.AddListener(() => SetViewMode(true));
            listViewButton?.onClick.AddListener(() => SetViewMode(false));
        }

        private void SetupAddItemDialog()
        {
            if (addItemDialog != null)
            {
                addItemDialog.OnItemCreated += OnCustomItemCreated;
                addItemDialog.OnItemSelected += OnItemSelectedFromSearch;
                addItemDialog.OnDialogClosed += OnAddItemDialogClosed;
            }
        }

        private void SetupTransferDialog()
        {
            if (transferItemDialog != null)
            {
                transferItemDialog.OnTransferConfirmed += OnTransferConfirmed;
                transferItemDialog.OnDialogClosed += OnTransferDialogClosed;
            }
        }

        private void SetupCategoryFilter()
        {
            if (categoryFilter == null) return;

            categoryFilter.ClearOptions();

            var options = new List<string> { "All Categories" };

            // Add all item categories
            foreach (ItemCategory category in System.Enum.GetValues(typeof(ItemCategory)))
            {
                options.Add(GetCategoryDisplayName(category));
            }

            categoryFilter.AddOptions(options);
            categoryFilter.value = 0; // Default to "All Categories"

            Debug.Log($"[PersonalInventory] Category filter populated with {options.Count} options");
        }

        protected override void RefreshContent()
        {
            LoadCurrentUser();
            LoadUserCharacters();
            LoadCurrentCharacter();
            LoadCharacterInventory();
            UpdatePersonalStats();
            RefreshItemDisplay();
        }

        // =====================================================================
        // USER & CHARACTER LOADING
        // =====================================================================

        private void LoadCurrentUser()
        {
            currentUserId = PlayerPrefs.GetString("UserId", "");

            if (string.IsNullOrEmpty(currentUserId))
            {
                Debug.LogError("[PersonalInventory] No userId found in PlayerPrefs!");
                currentUserId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString("UserId", currentUserId);
                PlayerPrefs.Save();
            }

            Debug.Log($"[PersonalInventory] Current userId: {currentUserId}");
        }

        private void LoadUserCharacters()
        {
            userCharacters.Clear();

            var characterManager = CharacterManager.Instance;
            var personalStorage = PersonalStorageManager.Instance;
            if (characterManager != null)
            {
                var groupChars = characterManager.GetCharactersByUser(currentUserId);
                userCharacters.AddRange(groupChars);
                Debug.Log($"[PersonalInventory] Loaded {userCharacters.Count} characters for user");
            }

            if (personalStorage != null)
            {
                var personalChars = personalStorage.GetPersonalCharacters();
                userCharacters.AddRange(personalChars);
            }

            UpdateCharacterDropdown();
        }

        private void UpdateCharacterDropdown()
        {
            if (characterDropdown == null) return;

            characterDropdown.ClearOptions();

            if (userCharacters.Count == 0)
            {
                characterDropdown.AddOptions(new List<string> { "No characters - Create one!" });
                characterDropdown.interactable = false;

                if (addItemButton != null)
                    addItemButton.interactable = false;

                return;
            }

            var options = userCharacters.Select(c =>
                $"{c.characterName} (Lvl {c.level} {c.characterClass})").ToList();

            characterDropdown.AddOptions(options);
            characterDropdown.interactable = true;

            if (addItemButton != null)
                addItemButton.interactable = true;

            // Try to select previously selected character or active character
            int selectedIndex = 0;
            string savedCharacterId = PlayerPrefs.GetString("SelectedCharacterId", "");

            if (!string.IsNullOrEmpty(savedCharacterId))
            {
                selectedIndex = userCharacters.FindIndex(c => c.characterId == savedCharacterId);
                if (selectedIndex < 0) selectedIndex = 0;
            }
            else
            {
                // Select active character
                selectedIndex = userCharacters.FindIndex(c => c.isActive);
                if (selectedIndex < 0) selectedIndex = 0;
            }

            characterDropdown.value = selectedIndex;
            OnCharacterSelectionChanged(selectedIndex);
        }

        private void LoadCurrentCharacter()
        {
            if (userCharacters.Count == 0)
            {
                currentCharacter = null;
                currentCharacterId = "";

                if (activeCharacterText != null)
                {
                    activeCharacterText.text = "No Character Selected";
                    activeCharacterText.color = Color.gray;
                }

                return;
            }

            int selectedIndex = characterDropdown?.value ?? 0;
            if (selectedIndex >= 0 && selectedIndex < userCharacters.Count)
            {
                currentCharacter = userCharacters[selectedIndex];
                currentCharacterId = currentCharacter.characterId;

                if (activeCharacterText != null)
                {
                    activeCharacterText.text = $"Playing as: {currentCharacter.characterName}";
                    activeCharacterText.color = currentCharacter.isActive ? Color.green : Color.white;
                }

                Debug.Log($"[PersonalInventory] Selected character: {currentCharacter.characterName}");
            }
        }

        private void LoadCharacterInventory()
        {
            characterItems.Clear();
            characterOwnerships.Clear();

            if (string.IsNullOrEmpty(currentCharacterId))
            {
                Debug.LogWarning("[PersonalInventory] No character selected");
                return;
            }
            // Determine if character is in a group or personal
            if (string.IsNullOrEmpty(currentCharacter.groupId))
            {
                // Personal character - load from PersonalStorageManager
                LoadPersonalCharacterInventory();
            }
            else
            {
                LoadGroupCharacterInventory();

                Debug.Log($"[PersonalInventory] Loaded {characterItems.Count} items for {currentCharacter?.characterName}");
            }
        }

        private void LoadPersonalCharacterInventory()
        {
            var personalStorage = PersonalStorageManager.Instance;
            if (personalStorage == null)
            {
                Debug.LogError("[PersonalInventory] PersonalStorageManager not found!");
                return;
            }

            characterItems = personalStorage.GetCharacterItems(currentCharacterId);

            Debug.Log($"[PersonalInventory] Loaded {characterItems.Count} personal items");
        }

        private void LoadGroupCharacterInventory()
        {
            var inventoryManager = InventoryManager.Instance;
            if (inventoryManager == null)
            {
                Debug.LogError("[PersonalInventory] InventoryManager not found!");
                return;
            }

            // Get ownership records for this character
            characterOwnerships = inventoryManager.GetAllOwnerships()
                .Where(o => o.characterId == currentCharacterId)
                .ToList();

            // Get all items in group inventory
            var allItems = inventoryManager.GetCurrentInventory();

            // Build character's item list with proper quantities
            foreach (var ownership in characterOwnerships)
            {
                var item = allItems.FirstOrDefault(i => i.itemId == ownership.itemId);
                if (item != null)
                {
                    // Create a copy with character's quantity
                    var characterItem = new InventoryItem(item.itemName, item.category)
                    {
                        itemId = item.itemId,
                        description = item.description,
                        quantity = ownership.quantityOwned,
                        weight = item.weight,
                        valueInGold = item.valueInGold,
                        thumbnailUrl = item.thumbnailUrl,
                        sourceUrl = item.sourceUrl,
                        properties = new Dictionary<string, string>(item.properties ?? new Dictionary<string, string>())
                    };

                    characterItems.Add(characterItem);
                }
            }

            Debug.Log($"[PersonalInventory] Loaded {characterItems.Count} group items");
        }

        // =====================================================================
        // DISPLAY MANAGEMENT
        // =====================================================================

        private void RefreshItemDisplay()
        {
            ClearItemCards();

            if (string.IsNullOrEmpty(currentCharacterId))
            {
                ShowMessage("Select or create a character to view their inventory", MessageType.Info);
                return;
            }

            var filteredItems = GetFilteredItems();

            if (filteredItems.Count == 0)
            {
                ShowMessage("No items in inventory. Claim items from party storage!", MessageType.Info);
                return;
            }

            foreach (var item in filteredItems)
            {
                CreateItemCard(item);
            }
        }

        private List<InventoryItem> GetFilteredItems()
        {
            var filtered = characterItems.ToList();

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
                var categories = Enum.GetValues(typeof(ItemCategory)).Cast<ItemCategory>().ToArray();
                var selectedCategory = categories[categoryFilter.value - 1];
                filtered = filtered.Where(item => item.category == selectedCategory).ToList();
            }

            return filtered.OrderBy(item => item.category).ThenBy(item => item.itemName).ToList();
        }

        private void CreateItemCard(InventoryItem item)
        {
            if (itemCardPrefab == null || itemContainer == null) return;

            GameObject cardObj = Instantiate(itemCardPrefab, itemContainer);
            ItemCardUI cardUI = cardObj.GetComponent<ItemCardUI>();

            if (cardUI != null)
            {
                ItemCardUI.CardMode mode = string.IsNullOrEmpty(currentCharacter?.groupId) ? ItemCardUI.CardMode.Personal : ItemCardUI.CardMode.Group;

                cardUI.SetupCard(item, mode);

                cardUI.OnItemModified += OnItemModified;
                cardUI.OnItemDeleted += OnItemDeleted;
                cardUI.OnTransferRequested += ShowTransferDialog;

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

            // Calculate encumbrance (get from character stats if available)
            float carryCapacity = 225f; // TODO: Get from character STR
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

        private string GetCategoryDisplayName(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.MagicItem => "Magic Items",
                ItemCategory.QuestItem => "Quest Items",
                _ => category.ToString()
            };
        }


        private void OpenAddItemDialog()
        {
            if (string.IsNullOrEmpty(currentCharacterId))
            {
                ShowMessage("Select a character first", MessageType.Warning);
                return;
            }

            if (addItemDialog != null)
            {
                string characterName = currentCharacter?.characterName ?? "Character";
                addItemDialog.ShowDialog(characterName);
            }
        }

        private void OpenCreateCharacterDialog()
        {
            if (createCharacterDialog != null)
            {
                createCharacterDialog.ShowDialog(currentUserId);
                createCharacterDialog.OnCharacterCreated += OnCharacterCreated;
            }
        }

        /// <summary>
        /// Show transfer dialog for an item
        /// </summary>
        public void ShowTransferDialog(InventoryItem item)
        {
            if (currentCharacter == null || string.IsNullOrEmpty(currentCharacterId))
            {
                ShowMessage("No character selected", MessageType.Warning);
                return;
            }

            if (transferItemDialog == null)
            {
                ShowMessage("Transfer dialog not configured", MessageType.Error);
                return;
            }

            // Personal items can only go TO group
            // Group items (owned by character) can go TO personal
            var direction = string.IsNullOrEmpty(currentCharacter.groupId)
                ? TransferItemDialog.TransferDirection.PersonalToGroup
                : TransferItemDialog.TransferDirection.GroupToPersonal;

            int availableQuantity = item.quantity;

            // If character is in group, check ownership
            if (!string.IsNullOrEmpty(currentCharacter.groupId))
            {
                var inventoryManager = InventoryManager.Instance;
                var ownership = inventoryManager?.GetAllOwnerships()
                    .FirstOrDefault(o => o.itemId == item.itemId && o.characterId == currentCharacterId);

                availableQuantity = ownership?.quantityOwned ?? 0;

                if (availableQuantity <= 0)
                {
                    ShowMessage("You don't own any of this item to transfer", MessageType.Warning);
                    return;
                }
            }

            transferItemDialog.ShowDialog(item, currentCharacterId, availableQuantity, direction);
        }

        // =====================================================================
        // EVENT HANDLERS
        // =====================================================================

        private void OnCharacterSelectionChanged(int index)
        {
            if (index < 0 || index >= userCharacters.Count) return;

            currentCharacter = userCharacters[index];
            currentCharacterId = currentCharacter.characterId;

            // Save selection
            PlayerPrefs.SetString("SelectedCharacterId", currentCharacterId);
            PlayerPrefs.Save();

            Debug.Log($"[PersonalInventory] Switched to character: {currentCharacter.characterName}");

            if (activeCharacterText != null)
            {
                activeCharacterText.text = $"Playing as: {currentCharacter.characterName}";
                activeCharacterText.color = currentCharacter.isActive ? Color.green : Color.white;
            }

            LoadCharacterInventory();
            UpdatePersonalStats();
            RefreshItemDisplay();
        }

        private void OnCharactersChanged(List<PlayerCharacter> characters)
        {
            LoadUserCharacters();
            LoadCurrentCharacter();
            LoadCharacterInventory();
            RefreshItemDisplay();
        }

        private async void OnItemModified(InventoryItem item)
        {
            if (string.IsNullOrEmpty(currentCharacterId)) return;

            Debug.Log($"[PersonalInventory] Item modified: {item.itemName}, Quantity: {item.quantity}");

            // Check if character is in a group or personal
            if (string.IsNullOrEmpty(currentCharacter.groupId))
            {
                // Personal character - update personal storage
                var personalStorage = PersonalStorageManager.Instance;
                if (personalStorage != null)
                {
                    await personalStorage.UpdateItemQuantityAsync(item.itemId, currentCharacterId, item.quantity);
                }
            }
            else
            {
                // Group character - update ownership
                var inventoryManager = InventoryManager.Instance;
                if (inventoryManager != null)
                {
                    await inventoryManager.UpdateOwnershipAsync(item.itemId, currentCharacterId, item.quantity);
                }
            }

            // Refresh to show updated stats
            LoadCharacterInventory();
            UpdatePersonalStats();
            RefreshItemDisplay();
        }

        private async void OnCustomItemCreated(CustomItemData customItem)
        {
            if (string.IsNullOrEmpty(currentCharacterId))
            {
                ShowMessage("Select a character first", MessageType.Warning);
                return;
            }

            Debug.Log($"[PersonalInventory] Custom item created: {customItem.itemName}");

            var inventoryItem = customItem.ToInventoryItem();
            var inventoryManager = InventoryManager.Instance;

            if (inventoryManager != null)
            {
                // Add item to group inventory
                await inventoryManager.AddItemAsync(inventoryItem);

                // Claim for current character
                await inventoryManager.UpdateOwnershipAsync(
                    inventoryItem.itemId,
                    currentCharacterId,
                    inventoryItem.quantity
                );

                await System.Threading.Tasks.Task.Delay(300);
                RefreshContent();
            }
        }

        private async void OnItemSelectedFromSearch(InventoryItem item)
        {
            if (string.IsNullOrEmpty(currentCharacterId))
            {
                ShowMessage("Select a character first", MessageType.Warning);
                return;
            }

            Debug.Log($"[PersonalInventory] Item selected from search: {item.itemName}");

            // Check if character is in a group or personal
            if (string.IsNullOrEmpty(currentCharacter.groupId))
            {
                // Personal character - add to personal storage
                await AddItemToPersonalStorage(item);
            }
            else
            {
                // Group character - add to group inventory and claim
                await AddItemToGroupInventory(item);
            }
            
            Debug.Log($"[PersonalInventorty] Successfully added and claimed {item.quantity} x {item.itemName}");

            await System.Threading.Tasks.Task.Delay(300);
            RefreshContent();
        }

        private async Task AddItemToPersonalStorage(InventoryItem item)
        {
            var personalStorage = PersonalStorageManager.Instance;
            if (personalStorage != null)
            {
                await personalStorage.AddItemAsync(item, currentCharacterId);
                ShowMessage($"Added {item.quantity}x {item.itemName} to personal inventory", MessageType.Success);
                Debug.Log($"[PersonalInventory] Added to personal storage: {item.itemName}");
            }
        }

        private async Task AddItemToGroupInventory(InventoryItem item)
        {
            var inventoryManager = InventoryManager.Instance;
            if (inventoryManager != null)
            {
                // Add item to group inventory
                await inventoryManager.AddItemAsync(item);

                await System.Threading.Tasks.Task.Delay(100);

                // Claim for current character
                await inventoryManager.UpdateOwnershipAsync(
                    item.itemId,
                    currentCharacterId,
                    item.quantity
                );

                ShowMessage($"Added {item.quantity}x {item.itemName} to group inventory", MessageType.Success);
                Debug.Log($"[PersonalInventory] Added to group inventory and claimed: {item.itemName}");
            }
        }


        private async void OnCharacterCreated(PlayerCharacter newCharacter)
        {
            var characterManager = CharacterManager.Instance;
            if (characterManager != null)
            {
                await characterManager.AddCharacterAsync(newCharacter);
                ShowMessage($"Created character: {newCharacter.characterName}", MessageType.Success);

                // Refresh will be triggered by OnCharactersChanged event
            }
        }        
        
        /// Transfer item from personal storage to group inventory (PERMANENT)
        /// Item is removed from personal storage and added to group
        private async void TransferItemToGroup(InventoryItem item, int quantity)
        {
            if (string.IsNullOrEmpty(currentCharacter?.groupId))
            {
                ShowMessage("Character must be in a group to transfer items", MessageType.Warning);
                return;
            }

            if (quantity <= 0 || quantity > item.quantity)
            {
                ShowMessage($"Invalid quantity. You have {item.quantity} available.", MessageType.Warning);
                return;
            }

            var inventoryManager = InventoryManager.Instance;
            var personalStorage = PersonalStorageManager.Instance;

            if (inventoryManager == null || personalStorage == null)
            {
                ShowMessage("Required managers not available", MessageType.Error);
                return;
            }

            try
            {
                Debug.Log($"[PersonalInventory] PERMANENT TRANSFER: {quantity}x {item.itemName} to group");

                // Check if item already exists in group inventory
                var existingGroupItem = inventoryManager.GetCurrentInventory()
                    .FirstOrDefault(i => i.itemName == item.itemName &&
                                         i.category == item.category);

                if (existingGroupItem != null)
                {
                    // Item exists - just increase the group's total quantity
                    await inventoryManager.UpdateItemQuantityAsync(
                        existingGroupItem.itemId,
                        existingGroupItem.quantity + quantity
                    );

                    Debug.Log($"[PersonalInventory] Added {quantity} to existing group item (new total: {existingGroupItem.quantity + quantity})");
                }
                else
                {
                    // Item doesn't exist - create new group item
                    var groupItem = new InventoryItem(item.itemName, item.category)
                    {
                        itemId = Guid.NewGuid().ToString(),
                        description = item.description,
                        quantity = quantity,
                        weight = item.weight,
                        valueInGold = item.valueInGold,
                        thumbnailUrl = item.thumbnailUrl,
                        sourceUrl = item.sourceUrl,
                        properties = new Dictionary<string, string>(item.properties ?? new Dictionary<string, string>())
                    };

                    await inventoryManager.AddItemAsync(groupItem);
                    Debug.Log($"[PersonalInventory] Created new group item with {quantity} quantity");
                }

                // Remove from personal storage
                int newPersonalQuantity = item.quantity - quantity;
                await personalStorage.UpdateItemQuantityAsync(
                    item.itemId,
                    currentCharacterId,
                    newPersonalQuantity
                );

                ShowMessage($"Transferred {quantity}x {item.itemName} to group (PERMANENT)", MessageType.Success);

                await System.Threading.Tasks.Task.Delay(200);
                RefreshContent();
            }
            catch (Exception e)
            {
                ShowMessage($"Transfer failed: {e.Message}", MessageType.Error);
                Debug.LogError($"[PersonalInventory] Transfer error: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// Transfer item from group back to personal inventory (PERMANENT)
        /// This "un-shares" it - removes from group entirely
        /// </summary>
        private async void TransferItemToPersonal(InventoryItem item, int quantity)
        {
            if (string.IsNullOrEmpty(currentCharacter?.groupId))
            {
                ShowMessage("Character is not in a group", MessageType.Warning);
                return;
            }

            var inventoryManager = InventoryManager.Instance;
            var personalStorage = PersonalStorageManager.Instance;

            if (inventoryManager == null || personalStorage == null)
            {
                ShowMessage("Required managers not available", MessageType.Error);
                return;
            }

            try
            {
                Debug.Log($"[PersonalInventory] PERMANENT TRANSFER: {quantity}x {item.itemName} to personal");

                // 1. Verify character owns this quantity
                var ownership = inventoryManager.GetAllOwnerships()
                    .FirstOrDefault(o => o.itemId == item.itemId && o.characterId == currentCharacterId);

                if (ownership == null || ownership.quantityOwned < quantity)
                {
                    ShowMessage($"You don't own enough. You have: {ownership?.quantityOwned ?? 0}", MessageType.Warning);
                    return;
                }

                // 2. Return ownership (unclaim from group)
                await inventoryManager.ReturnOwnershipAsync(item.itemId, currentCharacterId, quantity);

                // 3. Reduce group's total quantity
                var groupItem = inventoryManager.GetCurrentInventory()
                    .FirstOrDefault(i => i.itemId == item.itemId);
                if (groupItem != null)
                {
                    int newGroupQuantity = groupItem.quantity - quantity;
                    await inventoryManager.UpdateItemQuantityAsync(item.itemId, newGroupQuantity);
                }

                // 4. Add to personal storage
                var personalItem = new InventoryItem(item.itemName, item.category)
                {
                    itemId = Guid.NewGuid().ToString(), // New ID for personal copy
                    description = item.description,
                    quantity = quantity,
                    weight = item.weight,
                    valueInGold = item.valueInGold,
                    thumbnailUrl = item.thumbnailUrl,
                    sourceUrl = item.sourceUrl,
                    properties = new Dictionary<string, string>(item.properties ?? new Dictionary<string, string>())
                };

                await personalStorage.AddItemAsync(personalItem, currentCharacterId);

                ShowMessage($"Moved {quantity}x {item.itemName} to personal storage (PERMANENT)", MessageType.Success);

                await System.Threading.Tasks.Task.Delay(200);
                RefreshContent();
            }
            catch (Exception e)
            {
                ShowMessage($"Transfer failed: {e.Message}", MessageType.Error);
                Debug.LogError($"[PersonalInventory] Transfer error: {e.Message}");
            }
        }

        private async void OnItemDeleted(InventoryItem item)
        {
            if (item == null || string.IsNullOrEmpty(currentCharacterId)) return;

            var personalStorage = PersonalStorageManager.Instance;
            if (personalStorage == null) return;

            try
            {
                await personalStorage.RemoveItemAsync(item.itemId, currentCharacterId);

                ShowMessage($"Deleted {item.itemName}", MessageType.Success);

                RefreshContent();
            }
            catch (Exception e)
            {
                ShowMessage($"Failed to delete item: {e.Message}", MessageType.Error);
                Debug.LogError($"[PersonalInventory] Delete error: {e}");
            }
        }


        private void OnSearchChanged(string searchTerm)
        {
            RefreshItemDisplay();
        }

        private void OnCategoryFilterChanged(int filterIndex)
        {
            RefreshItemDisplay();
        }

        private void SetViewMode(bool gridView)
        {
            isGridView = gridView;

            if (gridViewButton != null)
                gridViewButton.GetComponent<Image>().color = gridView ? Color.blue : Color.white;
            if (listViewButton != null)
                listViewButton.GetComponent<Image>().color = !gridView ? Color.blue : Color.white;

            RefreshItemDisplay();
        }

        private void OnAddItemDialogClosed()
        {
            Debug.Log("[PersonalInventory] Add item dialog closed");
        }


        private void OnTransferConfirmed(TransferItemDialog.TransferRequest request)
        {
            var item = characterItems.FirstOrDefault(i => i.itemId == request.itemId);
            if (item == null)
            {
                ShowMessage("Item not found", MessageType.Error);
                return;
            }

            if (request.direction == TransferItemDialog.TransferDirection.PersonalToGroup)
            {
                TransferItemToGroup(item, request.quantity);
            }
            else
            {
                TransferItemToPersonal(item, request.quantity);
            }
        }

        private void OnTransferDialogClosed()
        {
            Debug.Log("[PersonalInventory] Transfer dialog closed");
        }

        private void OnTransferToGroupClicked()
        {
            // This can open a multi-select dialog or just show a message
            ShowMessage("Select an item to transfer, then click its Transfer button", MessageType.Info);
        }

        void OnDestroy()
        {
            var characterManager = CharacterManager.Instance;
            if (characterManager != null)
            {
                characterManager.OnCharactersChanged -= OnCharactersChanged;
            }

            if (addItemDialog != null)
            {
                addItemDialog.OnItemCreated -= OnCustomItemCreated;
                addItemDialog.OnItemSelected -= OnItemSelectedFromSearch;
                addItemDialog.OnDialogClosed -= OnAddItemDialogClosed;
            }

            if (createCharacterDialog != null)
            {
                createCharacterDialog.OnCharacterCreated -= OnCharacterCreated;
            }
            if (transferItemDialog != null)
            {
                transferItemDialog.OnTransferConfirmed -= OnTransferConfirmed;
                transferItemDialog.OnDialogClosed -= OnTransferDialogClosed;
            }
        }
    }
}