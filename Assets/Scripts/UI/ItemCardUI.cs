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

    [Header("Visual")]
    [SerializeField] private Image thumbnailImage;
    [SerializeField] private Image categoryColorImage;

    private InventoryItem currentItem;
    private InventoryUIManager uiManager;

    void Awake()
    {
        // Setup button listeners
        if (increaseQuantityButton != null)
            increaseQuantityButton.onClick.AddListener(() => ChangeQuantity(1));

        if (decreaseQuantityButton != null)
            decreaseQuantityButton.onClick.AddListener(() => ChangeQuantity(-1));

        if (deleteItemButton != null)
            deleteItemButton.onClick.AddListener(DeleteItem);

        if (addNoteButton != null)
            addNoteButton.onClick.AddListener(AddPlayerNote);
    }

    public void SetupCard(InventoryItem item, InventoryUIManager manager)
    {
        currentItem = item;
        uiManager = manager;

        UpdateDisplay();
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
            if (desc.Length > 100)
                desc = desc.Substring(0, 97) + "...";
            descriptionText.text = desc;
        }

        // Physical properties
        if (weightText != null)
            weightText.text = currentItem.GetWeightDisplay();

        if (valueText != null)
            valueText.text = currentItem.GetValueDisplay();

        if (quantityText != null)
            quantityText.text = $"Qty: {currentItem.quantity}";

        // Owner info
        if (ownerText != null)
        {
            if (string.IsNullOrEmpty(currentItem.currentOwner))
                ownerText.text = "Party Item";
            else
                ownerText.text = $"Owner: {currentItem.currentOwner}";
        }

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

        // TODO: Load thumbnail image
        // SetupThumbnail();
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

    private void ChangeQuantity(int delta)
    {
        if (currentItem != null && uiManager != null)
        {
            int newQuantity = currentItem.quantity + delta;
            if (newQuantity >= 0) // Allow 0 to delete item
            {
                uiManager.UpdateItemQuantity(currentItem.itemId, newQuantity);
            }
        }
    }

    private void DeleteItem()
    {
        if (currentItem != null && uiManager != null)
        {
            // Show confirmation dialog in a real implementation
            if (currentItem != null && uiManager != null)
            {
                Debug.Log($"deleting item {currentItem.itemName}");
                uiManager.RemoveItem(currentItem.itemId);
            }
        }
    }

    private void AddPlayerNote()
    {
        // TODO: Implement add note dialog
        Debug.Log($"Add note to {currentItem?.itemName} - To be implemented");
    }
}