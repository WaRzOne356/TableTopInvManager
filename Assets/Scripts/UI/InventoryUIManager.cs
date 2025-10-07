using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform itemContainer;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private GridLayoutGroup gridLayout;
    [SerializeField] private VerticalLayoutGroup listLayout;

    [Header("Control Elements")]
    [SerializeField] private Button gridViewButton;
    [SerializeField] private Button listViewButton;
    [SerializeField] private TMP_InputField searchField;
    [SerializeField] private TMP_Dropdown categoryDropdown;
    [SerializeField] private Button addItemButton;

    [Header("Info Display")]
    [SerializeField] private TextMeshProUGUI totalItemsText;
    [SerializeField] private TextMeshProUGUI totalWeightText;
    [SerializeField] private TextMeshProUGUI totalValueText;

    [Header("Prefabs")]
    [SerializeField] private GameObject gridItemCardPrefab;
    [SerializeField] private GameObject listItemCardPrefab;

    [Header("Settings")]
    [SerializeField] private UISettings uiSettings;

    // Data
    private GroupInventory currentInventory;
    private List<InventoryItem> filteredItems;
    private ItemCategory selectedCategory = ItemCategory.Miscellaneous;

    // Current view items (for cleanup)
    private List<GameObject> currentItemObjects;

    void Awake()
    {
        // Initialize collections
        currentItemObjects = new List<GameObject>();
        filteredItems = new List<InventoryItem>();

        // Initialize settings if null
        if (uiSettings == null)
            uiSettings = new UISettings();
    }

    void Start()
    {
        SetupUI();

        // Create test inventory if none exists
        if (currentInventory == null)
        {
            CreateTestInventory();
        }

        RefreshDisplay();
    }

    private void SetupUI()
    {
        // Setup button listeners
        if (gridViewButton != null)
            gridViewButton.onClick.AddListener(() => SetViewMode(ViewMode.Grid));

        if (listViewButton != null)
            listViewButton.onClick.AddListener(() => SetViewMode(ViewMode.List));

        if (addItemButton != null)
            addItemButton.onClick.AddListener(ShowAddItemDialog);

        // Setup search
        if (searchField != null)
            searchField.onValueChanged.AddListener(OnSearchChanged);

        // Setup category dropdown
        if (categoryDropdown != null)
        {
            SetupCategoryDropdown();
            categoryDropdown.onValueChanged.AddListener(OnCategoryChanged);
        }

        // Setup initial view mode
        SetViewMode(uiSettings.currentViewMode);
    }

    private void SetupCategoryDropdown()
    {
        var options = new List<string> { "All Categories" };

        // Add all enum values
        foreach (ItemCategory category in System.Enum.GetValues(typeof(ItemCategory)))
        {
            var item = new InventoryItem();
            item.category = category;
            options.Add(item.GetCategoryDisplayName());
        }

        categoryDropdown.ClearOptions();
        categoryDropdown.AddOptions(options);
    }

    public void SetViewMode(ViewMode mode)
    {
        uiSettings.currentViewMode = mode;

        // Update button states
        if (gridViewButton != null)
        {
            var gridColors = gridViewButton.colors;
            gridColors.normalColor = mode == ViewMode.Grid ? Color.blue : Color.white;
            gridViewButton.colors = gridColors;
        }

        if (listViewButton != null)
        {
            var listColors = listViewButton.colors;
            listColors.normalColor = mode == ViewMode.List ? Color.blue : Color.white;
            listViewButton.colors = listColors;
        }

        // Configure layouts
        ConfigureLayoutForViewMode(mode);

        // Refresh display
        RefreshDisplay();
    }

    private void ConfigureLayoutForViewMode(ViewMode mode)
    {
        if (itemContainer == null) return;

        // Disable both layout groups first
        if (gridLayout != null) gridLayout.enabled = false;
        if (listLayout != null) listLayout.enabled = false;

        switch (mode)
        {
            case ViewMode.Grid:
                if (gridLayout != null)
                {
                    gridLayout.enabled = true;
                    gridLayout.cellSize = new Vector2(uiSettings.gridCardWidth, uiSettings.gridCardHeight);
                    gridLayout.spacing = new Vector2(10, 10);
                    gridLayout.constraint = GridLayoutGroup.Constraint.Flexible;
                }
                break;

            case ViewMode.List:
                if (listLayout != null)
                {
                    listLayout.enabled = true;
                    listLayout.spacing = 5;
                    listLayout.childForceExpandWidth = true;
                    listLayout.childControlHeight = true;
                    listLayout.childControlWidth = true;
                }
                break;
        }
    }

    public void RefreshDisplay()
    {
        UpdateFilteredItems();
        ClearCurrentDisplay();
        CreateItemCards();
        UpdateInfoDisplay();
    }

    private void UpdateFilteredItems()
    {
        if (currentInventory == null)
        {
            filteredItems.Clear();
            return;
        }

        // Start with all items
        filteredItems = new List<InventoryItem>(currentInventory.items);

        // Apply search filter
        string searchText = searchField?.text ?? "";
        if (!string.IsNullOrEmpty(searchText))
        {
            filteredItems = currentInventory.SearchItems(searchText);
        }

        // Apply category filter
        if (categoryDropdown != null && categoryDropdown.value > 0) // 0 = "All Categories"
        {
            var categoryIndex = categoryDropdown.value - 1; // Adjust for "All Categories" option
            var categories = System.Enum.GetValues(typeof(ItemCategory)).Cast<ItemCategory>().ToArray();

            if (categoryIndex >= 0 && categoryIndex < categories.Length)
            {
                var selectedCat = categories[categoryIndex];
                filteredItems = filteredItems.Where(item => item.category == selectedCat).ToList();
            }
        }

        // Sort items (category first, then name)
        filteredItems = filteredItems.OrderBy(item => item.category).ThenBy(item => item.itemName).ToList();
    }

    private void ClearCurrentDisplay()
    {
        foreach (var obj in currentItemObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
        currentItemObjects.Clear();
    }

    private void CreateItemCards()
    {
        if (itemContainer == null) return;

        foreach (var item in filteredItems)
        {
            GameObject cardPrefab = uiSettings.currentViewMode == ViewMode.Grid ?
                                   gridItemCardPrefab : listItemCardPrefab;

            if (cardPrefab != null)
            {
                var cardObj = Instantiate(cardPrefab, itemContainer);
                var cardComponent = cardObj.GetComponent<ItemCardUI>();

                if (cardComponent != null)
                {
                    cardComponent.SetupCard(item, this);
                }

                currentItemObjects.Add(cardObj);
            }
        }
    }

    private void UpdateInfoDisplay()
    {
        if (currentInventory == null) return;

        if (totalItemsText != null)
            totalItemsText.text = $"Items: {currentInventory.items.Count}";

        if (totalWeightText != null)
            totalWeightText.text = $"Weight: {currentInventory.GetTotalWeight():F1} lbs";

        if (totalValueText != null)
            totalValueText.text = $"Value: {currentInventory.GetTotalValue()} gp";
    }

    // Event handlers
    private void OnSearchChanged(string searchText)
    {
        RefreshDisplay();
    }

    private void OnCategoryChanged(int categoryIndex)
    {
        RefreshDisplay();
    }

    // Public methods for item management
    public void AddItem(InventoryItem item)
    {
        if (currentInventory != null)
        {
            currentInventory.AddItem(item);
            RefreshDisplay();
        }
    }

    public void RemoveItem(string itemId)
    {
        if (currentInventory != null)
        {
            currentInventory.RemoveItem(itemId);
            RefreshDisplay();
        }
    }

    public void UpdateItemQuantity(string itemId, int newQuantity)
    {
        if (currentInventory != null)
        {
            var item = currentInventory.items.Find(i => i.itemId == itemId);
            if (item != null)
            {
                item.quantity = newQuantity;
                if (newQuantity <= 0)
                {
                    currentInventory.RemoveItem(itemId);
                }
                RefreshDisplay();
            }
        }
    }

    private void ShowAddItemDialog()
    {
        // TODO: Implement add item dialog
        Debug.Log("Add Item Dialog - To be implemented in next step");
    }

    private void CreateTestInventory()
    {
        currentInventory = new GroupInventory();
        currentInventory.groupName = "Test Adventure Party";
        currentInventory.playerNames.AddRange(new[] { "Alice", "Bob", "Charlie" });

        // Add some test items
        var sword = new InventoryItem("Longsword", ItemCategory.Weapon);
        sword.description = "A sharp steel blade with a leather-wrapped hilt. Well-balanced and deadly.";
        sword.weight = 3f;
        sword.valueInGold = 15;
        sword.currentOwner = "Alice";
        sword.AddPlayerNote("Alice", "Found this in the goblin chief's hoard");

        var potion = new InventoryItem("Healing Potion", ItemCategory.Consumable);
        potion.description = "A swirling red liquid that restores health when consumed.";
        potion.weight = 0.5f;
        potion.valueInGold = 50;
        potion.quantity = 3;
        potion.AddPlayerNote("Bob", "Bought from the alchemist in town");

        var gold = new InventoryItem("Gold Coins", ItemCategory.Currency);
        gold.description = "Standard gold pieces accepted throughout the realm.";
        gold.weight = 0.02f;
        gold.valueInGold = 1;
        gold.quantity = 247;

        var rope = new InventoryItem("Hemp Rope", ItemCategory.Tool);
        rope.description = "50 feet of sturdy hemp rope. Essential for any adventuring party.";
        rope.weight = 10f;
        rope.valueInGold = 2;
        rope.currentOwner = "Charlie";
        rope.AddPlayerNote("Charlie", "Always handy for climbing or tying up enemies");

        currentInventory.AddItem(sword);
        currentInventory.AddItem(potion);
        currentInventory.AddItem(gold);
        currentInventory.AddItem(rope);
    }

    // Getter for external scripts
    public GroupInventory GetCurrentInventory()
    {
        return currentInventory;
    }
}