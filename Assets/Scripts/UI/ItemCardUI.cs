using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class ItemCardUI : MonoBehaviour
{
    [Header("Grid Card Elements")]
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI weightText;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private TextMeshProUGUI ownerText;
    [SerializeField] private TextMeshProUGUI playerNotesText;

    [Header("Controls")]
    [SerializeField] private Button increaseQuantityButton;
    [SerializeField] private Button decreaseQuantityButton;
    [SerializeField] private Button deleteItemButton;
    [SerializeField] private Button addNoteButton;
    [SerializeField] private Button claimButton;
    [SerializeField] private Button shareButton;
    [SerializeField] private TMP_Dropdown ownerDropdown;

    [Header("Visual")]
    [SerializeField] private Image thumbnailImage;
    [SerializeField] private Image categoryColorImage;
    [SerializeField] private Image ownershipIndicator;

    // Events
    public System.Action<InventoryItem> OnItemModified;
    public System.Action<InventoryItem, string> OnOwnershipChanged;
    public System.Action<InventoryItem> OnItemDeleted;

    private InventoryItem currentItem;
    private InventoryUIManager uiManager;
    private bool isGridView = true;
    private CardMode cardMode = CardMode.Personal; // Personal, Group, or ReadOnly

    public enum CardMode
    {
        Personal,  // For personal inventory with full controls
        Group,     // For group inventory with claim/share buttons
        ReadOnly   // For item browser (view only)
    }

    void Awake()
    {
        SetupButtonListeners();
    }

    private void SetupButtonListeners()
    {
        if (increaseQuantityButton != null)
            increaseQuantityButton.onClick.AddListener(() => ChangeQuantity(1));

        if (decreaseQuantityButton != null)
            decreaseQuantityButton.onClick.AddListener(() => ChangeQuantity(-1));

        if (deleteItemButton != null)
            deleteItemButton.onClick.AddListener(DeleteItem);

        if (addNoteButton != null)
            addNoteButton.onClick.AddListener(AddPlayerNote);

        if (claimButton != null)
            claimButton.onClick.AddListener(ClaimItem);

        if (shareButton != null)
            shareButton.onClick.AddListener(ShareItem);

        if (ownerDropdown != null)
            ownerDropdown.onValueChanged.AddListener(OnOwnerDropdownChanged);
    }

    /// <summary>
    /// Setup card for Personal Inventory mode
    /// </summary>
    public void SetupPersonalCard(InventoryItem item, bool gridView = true)
    {
        currentItem = item;
        isGridView = gridView;
        cardMode = CardMode.Personal;
        
        UpdateDisplay();
        ConfigureForPersonalMode();
        AdjustLayoutForViewMode(gridView);
    }

    /// <summary>
    /// Setup card for Group Inventory mode
    /// </summary>
    public void SetupGroupCard(InventoryItem item)
    {
        currentItem = item;
        isGridView = true; // Group view is typically grid
        cardMode = CardMode.Group;
        
        UpdateDisplay();
        ConfigureForGroupMode();
    }

    /// <summary>
    /// Legacy setup method for backward compatibility
    /// </summary>
    public void SetupCard(InventoryItem item, InventoryUIManager manager)
    {
        currentItem = item;
        uiManager = manager;
        cardMode = CardMode.Personal;

        UpdateDisplay();
        ConfigureForPersonalMode();
    }

    private void UpdateDisplay()
    {
        if (currentItem == null) return;

        // Basic info
        if (itemNameText != null)
            itemNameText.text = currentItem.itemName;

        if (categoryText != null)
            categoryText.text = currentItem.GetCategoryDisplayName();

        if (descriptionText != null)
        {
            // Truncate description for grid view
            string desc = currentItem.description;
            if (isGridView && desc.Length > 100)
                desc = desc.Substring(0, 97) + "...";
            descriptionText.text = desc;
        }

        // Physical properties
        if (weightText != null)
            weightText.text = $"{currentItem.TotalWeight:F1} lbs";

        if (valueText != null)
            valueText.text = $"{currentItem.TotalValue:N0} gp";

        if (quantityText != null)
            quantityText.text = cardMode == CardMode.Group ? $"×{currentItem.quantity}" : $"Qty: {currentItem.quantity}";

        // Owner info
        UpdateOwnershipDisplay();

        // Player notes
        if (playerNotesText != null)
        {
            if (currentItem.playerNotes.Count > 0)
            {
                var latestNote = currentItem.playerNotes.Last();
                playerNotesText.text = $"{latestNote.playerName}: {latestNote.note}";
            }
            else
            {
                playerNotesText.text = "No notes";
            }
        }

        // Visual elements
        SetupCategoryColor();
    }

    private void UpdateOwnershipDisplay()
    {
        string ownerName = string.IsNullOrEmpty(currentItem.currentOwner) ? "Party Item" : currentItem.currentOwner;
        bool isOwned = !string.IsNullOrEmpty(currentItem.currentOwner);
        
        if (ownerText != null)
        {
            if (cardMode == CardMode.Group)
                ownerText.text = ownerName;
            else if (cardMode == CardMode.Personal)
                ownerText.text = isOwned ? $"Owner: {currentItem.currentOwner}" : "Party Item";
        }

        // Update ownership indicator color
        if (ownershipIndicator != null)
        {
            if (cardMode == CardMode.Group)
                ownershipIndicator.color = isOwned ? Color.blue : Color.gray;
            else
                ownershipIndicator.color = isOwned ? Color.green : Color.gray;
        }

        // Setup owner dropdown for Personal mode
        if (ownerDropdown != null && cardMode == CardMode.Personal)
        {
            UpdateOwnerDropdown();
        }

        // Show/hide claim button for Group mode
        if (claimButton != null && cardMode == CardMode.Group)
        {
            string currentPlayer = PlayerPrefs.GetString("UserName", "Player");
            bool canClaim = string.IsNullOrEmpty(currentItem.currentOwner) || currentItem.currentOwner != currentPlayer;
            claimButton.gameObject.SetActive(canClaim);
        }
    }

    private void UpdateOwnerDropdown()
    {
        if (ownerDropdown == null) return;

        ownerDropdown.ClearOptions();
        ownerDropdown.AddOptions(new System.Collections.Generic.List<string> { "Unassigned", "Player1", "Player2", "Player3" });

        string currentOwner = currentItem.currentOwner ?? "Unassigned";
        int ownerIndex = ownerDropdown.options.FindIndex(option => option.text == currentOwner);
        ownerDropdown.value = ownerIndex >= 0 ? ownerIndex : 0;
    }

    private void ConfigureForPersonalMode()
    {
        // Show personal inventory controls
        SetControlVisibility(
            showQuantityButtons: true,
            showDeleteButton: true,
            showOwnerDropdown: true,
            showClaimButton: false,
            showShareButton: false
        );
    }

    private void ConfigureForGroupMode()
    {
        // Show group inventory controls
        SetControlVisibility(
            showQuantityButtons: false,
            showDeleteButton: false,
            showOwnerDropdown: false,
            showClaimButton: true,
            showShareButton: true
        );
    }

    private void SetControlVisibility(
        bool showQuantityButtons,
        bool showDeleteButton,
        bool showOwnerDropdown,
        bool showClaimButton,
        bool showShareButton)
    {
        if (increaseQuantityButton != null)
            increaseQuantityButton.gameObject.SetActive(showQuantityButtons);
        
        if (decreaseQuantityButton != null)
            decreaseQuantityButton.gameObject.SetActive(showQuantityButtons);
        
        if (deleteItemButton != null)
            deleteItemButton.gameObject.SetActive(showDeleteButton);
        
        if (ownerDropdown != null)
            ownerDropdown.gameObject.SetActive(showOwnerDropdown);
        
        if (claimButton != null)
            claimButton.gameObject.SetActive(showClaimButton);
        
        if (shareButton != null)
            shareButton.gameObject.SetActive(showShareButton);
    }

    private void AdjustLayoutForViewMode(bool gridView)
    {
        // Adjust UI layout based on grid vs list view
        var rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            if (gridView)
            {
                // Compact grid layout
                rectTransform.sizeDelta = new Vector2(200, 150);
            }
            else
            {
                // Extended list layout
                rectTransform.sizeDelta = new Vector2(400, 80);
            }
        }

        // Hide description in grid view to save space
        if (descriptionText != null)
            descriptionText.gameObject.SetActive(!gridView);
    }

    private void SetupCategoryColor()
    {
        if (categoryColorImage == null) return;

        // Color-code categories
        Color categoryColor = currentItem.category switch
        {
            ItemCategory.Weapon => new Color(0.8f, 0.2f, 0.2f, 0.3f), // Red
            ItemCategory.Armor => new Color(0.6f, 0.4f, 0.2f, 0.3f),  // Brown
            ItemCategory.MagicItem => new Color(0.6f, 0.2f, 0.8f, 0.3f), // Purple
            ItemCategory.Consumable => new Color(0.2f, 0.8f, 0.2f, 0.3f), // Green
            ItemCategory.Currency => new Color(1f, 0.8f, 0.2f, 0.3f),  // Gold
            ItemCategory.Tool => new Color(0.4f, 0.4f, 0.4f, 0.3f),    // Gray
            _ => new Color(0.5f, 0.5f, 0.5f, 0.3f) // Default gray
        };

        categoryColorImage.color = categoryColor;
    }

    // === Event Handlers ===

    private void ChangeQuantity(int delta)
    {
        if (currentItem == null) return;

        int newQuantity = currentItem.quantity + delta;
        if (newQuantity < 0) return;

        currentItem.quantity = newQuantity;
        
        if (quantityText != null)
            quantityText.text = cardMode == CardMode.Group ? $"×{newQuantity}" : $"Qty: {newQuantity}";

        // Notify via event or legacy manager
        OnItemModified?.Invoke(currentItem);
        
        if (uiManager != null)
            uiManager.UpdateItemQuantity(currentItem.itemId, newQuantity);
    }

    private void DeleteItem()
    {
        if (currentItem == null) return;

        Debug.Log($"Deleting item {currentItem.itemName}");
        
        OnItemDeleted?.Invoke(currentItem);
        
        if (uiManager != null)
            uiManager.RemoveItem(currentItem.itemId);
    }

    private void ClaimItem()
    {
        if (currentItem == null) return;

        string currentPlayer = PlayerPrefs.GetString("UserName", "Player");
        currentItem.currentOwner = currentPlayer;

        UpdateOwnershipDisplay();
        OnItemModified?.Invoke(currentItem);

        Debug.Log($"[GroupItem] {currentPlayer} claimed {currentItem.itemName}");
    }

    private void ShareItem()
    {
        if (currentItem == null) return;

        currentItem.currentOwner = "";

        UpdateOwnershipDisplay();
        OnItemModified?.Invoke(currentItem);

        Debug.Log($"[GroupItem] Shared {currentItem.itemName} with party");
    }

    private void OnOwnerDropdownChanged(int ownerIndex)
    {
        if (ownerDropdown == null || currentItem == null) return;
        if (ownerIndex >= ownerDropdown.options.Count) return;

        string newOwner = ownerDropdown.options[ownerIndex].text;
        if (newOwner == "Unassigned") newOwner = "";

        currentItem.currentOwner = newOwner;
        OnOwnershipChanged?.Invoke(currentItem, newOwner);
        UpdateOwnershipDisplay();
    }

    private void AddPlayerNote()
    {
        // TODO: Implement add note dialog
        Debug.Log($"Add note to {currentItem?.itemName} - To be implemented");
    }
}