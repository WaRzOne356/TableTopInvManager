using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Collections.Generic;

public class ItemCardUI : MonoBehaviour
{
    [Header("Grid Card Elements (Collapsed)")]
    [SerializeField] private GameObject collapsedView;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private TextMeshProUGUI weightText;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private Image categoryColorImage;

    [Header("Expanded View (Detailed)")]
    [SerializeField] private GameObject expandedView;
    [SerializeField] private TextMeshProUGUI expandedNameText;
    [SerializeField] private TextMeshProUGUI expandedCategoryText;
    [SerializeField] private TextMeshProUGUI expandedDescriptionText;
    [SerializeField] private TextMeshProUGUI expandedQuantityText;
    [SerializeField] private TextMeshProUGUI expandedWeightText;
    [SerializeField] private TextMeshProUGUI expandedValueText;
    [SerializeField] private TextMeshProUGUI expandedOwnerText;

    [Header("Properties Section (Prefab Container)")]
    [SerializeField] private Transform propertiesContainer;
    [SerializeField] private GameObject propertyRowPrefab;
    [SerializeField] private TextMeshProUGUI noPropertiesText; // "No special properties"

    [Header("Ownership Section")]
    [SerializeField] private TextMeshProUGUI noOwnersText;
    [SerializeField] private TextMeshProUGUI availableQuantityText;
    [SerializeField] private GameObject ownerEntryPrefab;
    [SerializeField] private TextMeshProUGUI ownershipErrorText;


    [Header("Ownership Display")]
    [SerializeField] private GameObject ownershipSection;  // Shows who owns what quantities
    [SerializeField] private Transform ownersListContainer;
    [SerializeField] private GameObject ownershipRowPrefab; // Prefab with: CharacterName + Quantity texts
    [SerializeField] private TextMeshProUGUI unclaimedQuantityText; // "Unclaimed: 3"


    [Header("Notes Section (Prefab Container)")]
    [SerializeField] private Transform notesContainer;
    [SerializeField] private GameObject noteRowPrefab;
    [SerializeField] private TextMeshProUGUI noNotesText; // "No notes yet"

    [Header("Controls")]
    [SerializeField] private Button cardButton;
    [SerializeField] private Button closeExpandedButton;
    [SerializeField] private Button increaseQuantityButton;
    [SerializeField] private Button decreaseQuantityButton;
    [SerializeField] private Button deleteItemButton;
    [SerializeField] private Button addNoteButton;

    [Header("Transfer Controls")]
    [SerializeField] private Button transferButton;      // PERMANENT move (personal<->group)
    [SerializeField] private Button shareButton;         // Group-owned, character holds it
    [SerializeField] private Button returnButton;        // Returns shared item to pool
    

    [Header("Visual")]
    [SerializeField] private Image thumbnailImage;
    [SerializeField] private Image selectedHighlight;

    [Header("Expansion Settings")]
    [SerializeField] private float expandedWidth = 600f;
    [SerializeField] private float expandedHeight = 400f;
    [SerializeField] private float collapsedWidth = 220f;
    [SerializeField] private float collapsedHeight = 180f;
    [SerializeField] private bool animateExpansion = true;
    [SerializeField] private float animationDuration = 0.2f;

    // Events
    public System.Action<ItemCardUI> OnCardSelected;
    public System.Action<InventoryItem> OnItemModified;
    public System.Action<InventoryItem> OnItemDeleted;
    public System.Action<InventoryItem> OnClaimRequested;
    public System.Action<InventoryItem> OnTransferRequested; // Permanent move
    public System.Action<InventoryItem, int> OnShareRequested; // Claim ownership
    public System.Action<InventoryItem, int> OnReturnRequested; // Return to pool



    // State
    private InventoryItem currentItem;
    private bool isExpanded = false;
    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private int originalSiblingIndex;
    private CardMode cardMode = CardMode.Group;


    // Spawned prefab tracking
    private List<GameObject> spawnedPropertyRows = new List<GameObject>();
    private List<GameObject> spawnedNoteRows = new List<GameObject>();
    private List<GameObject> spawnedOwnerRows = new List<GameObject>();
    private List<PlayerCharacter> availableCharacters = new List<PlayerCharacter>();
    private Dictionary<string, int> currentOwnership = new Dictionary<string, int>(); // characterId -> quantity

    public enum CardMode
    {
        Personal,
        Group,
        ReadOnly
    }

    public bool IsExpanded => isExpanded;
    public InventoryItem CurrentItem => currentItem;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        SetupButtonListeners();

        // Start collapsed
        SetViewState(false, instant: true);
    }

    private void SetupButtonListeners()
    {
        cardButton?.onClick.AddListener(OnCardClicked);
        closeExpandedButton?.onClick.AddListener(CollapseCard);

        increaseQuantityButton?.onClick.AddListener(() => ChangeQuantity(1));
        decreaseQuantityButton?.onClick.AddListener(() => ChangeQuantity(-1));
        deleteItemButton?.onClick.AddListener(DeleteItem);
        addNoteButton?.onClick.AddListener(AddPlayerNote);
        transferButton?.onClick.AddListener(TransferItem);
        shareButton?.onClick.AddListener(ShareItem);
        returnButton?.onClick.AddListener(ReturnItem);
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    public void SetupCard(InventoryItem item, CardMode mode = CardMode.Group)
    {
        currentItem = item;
        cardMode = mode;

        UpdateDisplay();
        ConfigureForMode(mode);
    }

    public void ExpandCard()
    {
        if (isExpanded) return;

        Debug.Log($"[ItemCard] Expanding card: {currentItem.itemName}");

        // Save original state
        originalPosition = rectTransform.anchoredPosition;
        originalSiblingIndex = transform.GetSiblingIndex();

        // Move to front (render on top)
        transform.SetAsLastSibling();

        // Populate properties and notes when expanding
        PopulatePropertiesSection();
        PopulateNotesSection();
        PopulateOwnershipSection();
        DisplayOwnershipInfo();

        // Expand
        SetViewState(true, instant: !animateExpansion);

        // Notify
        OnCardSelected?.Invoke(this);
    }

    public void CollapseCard()
    {
        if (!isExpanded) return;

        Debug.Log($"[ItemCard] Collapsing card: {currentItem.itemName}");

        // Clean up spawned prefabs
        ClearPropertiesSection();
        ClearNotesSection();
        ClearOwnershipSection();

        // Hide ownership display
        if (ownershipSection != null)
            ownershipSection.SetActive(false);

        // Restore original sibling index
        transform.SetSiblingIndex(originalSiblingIndex);

        // Collapse
        SetViewState(false, instant: !animateExpansion);
    }

    public void ForceCollapse()
    {
        // Instant collapse without animation
        ClearPropertiesSection();
        ClearNotesSection();
        SetViewState(false, instant: true);
    }

    // =========================================================================
    // DISPLAY UPDATES
    // =========================================================================

    private void UpdateDisplay()
    {
        if (currentItem == null) return;

        // Update collapsed view
        UpdateCollapsedView();

        // Update expanded view basic info
        UpdateExpandedViewBasicInfo();

        // Properties and notes are populated when expanded (not here)

        // Visual elements
        SetupCategoryColor();
    }

    private void UpdateCollapsedView()
    {
        if (itemNameText != null)
            itemNameText.text = currentItem.itemName;

        if (categoryText != null)
            categoryText.text = currentItem.GetCategoryDisplayName();

        if (quantityText != null)
            quantityText.text = $"×{currentItem.quantity}";

        if (weightText != null)
            weightText.text = $"{currentItem.TotalWeight:F1} lbs";

        if (valueText != null)
            valueText.text = $"{currentItem.TotalValue:N0} gp";
    }

    private void UpdateExpandedViewBasicInfo()
    {
        if (expandedNameText != null)
            expandedNameText.text = currentItem.itemName;

        if (expandedCategoryText != null)
            expandedCategoryText.text = currentItem.GetCategoryDisplayName();

        if (expandedDescriptionText != null)
            expandedDescriptionText.text = string.IsNullOrEmpty(currentItem.description)
                ? "No description available."
                : currentItem.description;

        if (expandedQuantityText != null)
            expandedQuantityText.text = $"Quantity: {currentItem.quantity}";

        if (expandedWeightText != null)
            expandedWeightText.text = currentItem.GetWeightDisplay();

        if (expandedValueText != null)
            expandedValueText.text = currentItem.GetValueDisplay();

        if (expandedOwnerText != null)
        {
            string owner = string.IsNullOrEmpty(currentItem.currentOwner) ? "Party Item" : currentItem.currentOwner;
            expandedOwnerText.text = $"Owner: {owner}";
        }
    }

    // =========================================================================
    // PROPERTIES SECTION
    // =========================================================================

    private void PopulatePropertiesSection()
    {
        ClearPropertiesSection();

        if (currentItem.properties == null || currentItem.properties.Count == 0)
        {
            // Show "no properties" message
            if (noPropertiesText != null)
                noPropertiesText.gameObject.SetActive(true);
            return;
        }

        // Hide "no properties" message
        if (noPropertiesText != null)
            noPropertiesText.gameObject.SetActive(false);

        // Spawn property rows
        if (propertyRowPrefab != null && propertiesContainer != null)
        {
            foreach (var property in currentItem.properties)
            {
                var rowObj = Instantiate(propertyRowPrefab, propertiesContainer);
                var rowUI = rowObj.GetComponent<PropertyRowUI>();

                if (rowUI != null)
                {
                    rowUI.SetProperty(property.Key, property.Value);

                    // Make read-only (no remove button)
                    rowUI.SetReadOnly(true);
                }

                spawnedPropertyRows.Add(rowObj);
            }

            Debug.Log($"[ItemCard] Spawned {spawnedPropertyRows.Count} property rows");
        }
    }

    private void ClearPropertiesSection()
    {
        foreach (var row in spawnedPropertyRows)
        {
            if (row != null)
                Destroy(row);
        }
        spawnedPropertyRows.Clear();

        if (noPropertiesText != null)
            noPropertiesText.gameObject.SetActive(false);
    }

    // =========================================================================
    // OWNERSHIP SECTION
    // =========================================================================



    private void PopulateOwnershipSection()
    {
        ClearOwnershipSection();

        if (currentItem == null) return;

        // Only show ownership section in Group mode
        if (cardMode != CardMode.Group)
        {
            if (ownershipSection != null)
                ownershipSection.SetActive(false);
            return;
        }

        if (ownershipSection != null)
            ownershipSection.SetActive(true);

        // Load ownership data
        LoadOwnershipData();

        // Load available characters
        LoadAvailableCharacters();

        // Populate UI
        PopulateOwnersList();
        UpdateAvailableQuantity();


        
        // Hide error text
        if (ownershipErrorText != null)
            ownershipErrorText.gameObject.SetActive(false);
    }

    private void LoadOwnershipData()
    {
        currentOwnership.Clear();

        if (currentItem == null) return;

        // Get ownership from InventoryManager
        var ownerships = InventoryManager.Instance?.GetOwnershipForItem(currentItem.itemId);

        if (ownerships != null)
        {
            foreach (var ownership in ownerships)
            {
                currentOwnership[ownership.characterId] = ownership.quantityOwned;
            }
        }

        Debug.Log($"[ItemCard] Loaded {currentOwnership.Count} ownership records for {currentItem.itemName}");
    }

    private void LoadAvailableCharacters()
    {
        availableCharacters.Clear();

        var characterManager = CharacterManager.Instance;
        if (characterManager != null)
        {
            availableCharacters = characterManager.GetCharacters().ToList();
        }

        Debug.Log($"[ItemCard] Loaded {availableCharacters.Count} available characters");
    }

    private void PopulateOwnersList()
    {
        ClearOwnersList();

        if (currentItem == null) return;

        var characterManager = CharacterManager.Instance;
        if (characterManager == null)
        {
            Debug.LogWarning("[ItemCard] CharacterManager not available");
            return;
        }

        // Check if there are any owners
        if (currentOwnership.Count == 0)
        {
            if (noOwnersText != null)
            {
                noOwnersText.gameObject.SetActive(true);
                noOwnersText.text = $"All {currentItem.quantity} in Party Storage";
            }
            return;
        }

        if (noOwnersText != null)
            noOwnersText.gameObject.SetActive(false);

        // Create owner entry for each owner
        foreach (var kvp in currentOwnership.OrderByDescending(x => x.Value))
        {
            string characterId = kvp.Key;
            int quantity = kvp.Value;

            var character = characterManager.GetCharacterById(characterId);
            if (character != null)
            {
                CreateOwnerEntry(character.characterName, quantity, characterId);
            }
            else
            {
                CreateOwnerEntry($"Unknown Character", quantity, characterId);
            }
        }

        // Add "Party Storage" entry for unallocated items
        int unallocated = GetUnallocatedQuantity();
        if (unallocated > 0)
        {
            CreateOwnerEntry("Party Storage", unallocated, "", isPartyStorage: true);
        }
    }

    private void CreateOwnerEntry(string ownerName, int quantity, string characterId, bool isPartyStorage = false)
    {
        if (ownerEntryPrefab == null || ownersListContainer == null) return;

        var entryObj = Instantiate(ownerEntryPrefab, ownersListContainer);
        spawnedOwnerRows.Add(entryObj);

        // Set owner name
        var nameText = entryObj.transform.Find("OwnerNameText")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
            nameText.text = ownerName;

        // Set quantity
        var qtyText = entryObj.transform.Find("QuantityText")?.GetComponent<TextMeshProUGUI>();
        if (qtyText != null)
            qtyText.text = $"×{quantity}";

        // Set background color
        var background = entryObj.GetComponent<Image>();
        if (background != null)
        {
            if (isPartyStorage)
                background.color = new Color(0.8f, 0.8f, 0.8f, 0.5f); // Gray for party storage
            else
                background.color = new Color(0.6f, 0.9f, 0.6f, 0.5f); // Green for characters
        }

        // Add return button (only for character-owned items, not party storage)
        if (!isPartyStorage && !string.IsNullOrEmpty(characterId))
        {
            var returnButton = entryObj.transform.Find("ReturnButton")?.GetComponent<Button>();
            if (returnButton != null)
            {
                returnButton.gameObject.SetActive(true);
                returnButton.onClick.AddListener(() => ReturnItemToParty(characterId, quantity));
            }
        }
        else
        {
            var returnButton = entryObj.transform.Find("ReturnButton")?.GetComponent<Button>();
            if (returnButton != null)
                returnButton.gameObject.SetActive(false);
        }
    }

    private void ClearOwnersList()
    {
        foreach (var row in spawnedOwnerRows)
        {
            if (row != null)
                Destroy(row);
        }
        spawnedOwnerRows.Clear();

        if (noOwnersText != null)
            noOwnersText.gameObject.SetActive(false);
    }


    private void UpdateAvailableQuantity()
    {
        if (currentItem == null) return;

        int unallocated = GetUnallocatedQuantity();

        if (availableQuantityText != null)
        {
            availableQuantityText.text = $"Available: {unallocated} / {currentItem.quantity}";
        }

    }

    private int GetUnallocatedQuantity()
    {
        if (currentItem == null) return 0;

        int totalOwned = currentOwnership.Values.Sum();
        return currentItem.quantity - totalOwned;
    }

 

    private void ClearOwnershipSection()
    {
        ClearOwnersList();
    }

    /// Set ownership summary in collapsed view (for Group Inventory)
    /// This shows ownership info without expanding the card
    public void SetOwnershipInfo(string ownershipSummary, int available, int totalOwned)
    {
        // You could add new UI elements to show this in collapsed view
        // For now, this ensures the method exists for GroupInventoryPage

        // Optional: Update a text field in collapsed view
        // if (collapsedOwnershipText != null)
        //     collapsedOwnershipText.text = ownershipSummary;

        Debug.Log($"[ItemCardUI] Ownership: {ownershipSummary}, Available: {available}, Claimed: {totalOwned}");
    }

    /// <summary>
    /// Display who owns what quantities of this group item
    /// Shows: Steve: 4, Bella: 3, Unclaimed: 3
    /// </summary>
    private void DisplayOwnershipInfo()
    {
        if (ownersListContainer == null) return;

        // Clear existing rows
        foreach (Transform child in ownersListContainer)
            Destroy(child.gameObject);

        var inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null) return;

        // Get all ownerships for this item
        var ownerships = inventoryManager.GetAllOwnerships()
            .Where(o => o.itemId == currentItem.itemId)
            .ToList();

        int totalOwned = ownerships.Sum(o => o.quantityOwned);
        int unclaimed = currentItem.quantity - totalOwned;

        // Get current group from current character
        var characterManager = CharacterManager.Instance;
        var groupManager = GroupManager.Instance;

        string currentCharacterId = PlayerPrefs.GetString("SelectedCharacterId", "");
        var currentCharacter = characterManager?.GetCharacterById(currentCharacterId);

        if (currentCharacter == null || string.IsNullOrEmpty(currentCharacter.groupId))
        {
            // Not in a group, hide ownership panel
            if (ownershipSection != null)
                ownershipSection.SetActive(false);
            return;
        }

        // Get all characters in the group
        var groupCharacters = groupManager?.GetGroupCharacters(currentCharacter.groupId)
            ?? new List<PlayerCharacter>();

        // Show ownership panel
        if (ownershipSection != null)
            ownershipSection.SetActive(true);

        foreach (var character in groupCharacters)
        {
            var ownership = ownerships.FirstOrDefault(o => o.characterId == character.characterId);
            int owned = ownership?.quantityOwned ?? 0;

            CreateOwnershipRow(character.characterName, character.characterId, owned);
        }

        // Update unclaimed text
        if (unclaimedQuantityText != null)
            unclaimedQuantityText.text = $"Unclaimed: {unclaimed}";
    }
    private void CreateOwnershipRow(string characterName, string characterId, int quantity)
    {
        if (ownershipRowPrefab == null) return;

        var row = Instantiate(ownershipRowPrefab, ownersListContainer);

        // Set character name
        var nameText = row.transform.Find("CharacterNameText")?.GetComponent<TextMeshProUGUI>();
        if (nameText) nameText.text = characterName;

        // Set quantity
        var qtyText = row.transform.Find("QuantityText")?.GetComponent<TextMeshProUGUI>();
        if (qtyText) qtyText.text = quantity.ToString();

        // Wire up buttons
        var plusButton = row.transform.Find("PlusButton")?.GetComponent<Button>();
        var minusButton = row.transform.Find("MinusButton")?.GetComponent<Button>();

        if (plusButton != null)
        {
            plusButton.onClick.AddListener(() => {
                OnShareRequested?.Invoke(currentItem, 1); // Share 1 more
            });

            // Disable if no unclaimed items
            int unclaimed = GetUnclaimedQuantity();
            plusButton.interactable = unclaimed > 0;
        }

        if (minusButton != null)
        {
            minusButton.onClick.AddListener(() => {
                OnReturnRequested?.Invoke(currentItem, 1); // Return 1
            });

            // Disable if they own 0
            minusButton.interactable = quantity > 0;
        }
    }

    private int GetUnclaimedQuantity()
    {
        var inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null) return 0;

        return inventoryManager.GetUnallocatedQuantity(currentItem.itemId);
    }
    public void HideOwnershipInfo()
    {
        // In Personal Inventory mode, hide ownership section
        if (ownershipSection != null)
            ownershipSection.SetActive(false);
    }

    // =========================================================================
    // OWNERSHIP EVENT HANDLERS
    // =========================================================================

    private void ReturnItemToParty(string characterId, int maxQuantity)
    {
        // Simple version: return all items from this character
        // You could add a quantity selector dialog for partial returns

        if (!currentOwnership.ContainsKey(characterId))
            return;

        int quantityToReturn = currentOwnership[characterId];

        // Remove from ownership
        currentOwnership.Remove(characterId);

        // Update through InventoryManager
        var updateTask = InventoryManager.Instance?.UpdateOwnershipAsync(
            currentItem.itemId,
            characterId,
            0 // Setting to 0 removes the ownership
        );

        // Refresh display
        PopulateOwnersList();
        UpdateAvailableQuantity();

        var characterManager = CharacterManager.Instance;
        var character = characterManager?.GetCharacterById(characterId);
        string characterName = character?.characterName ?? "Character";

        ShowOwnershipError($"{characterName} returned {quantityToReturn}x {currentItem.itemName} to party", false);

        // Notify that item was modified
        OnItemModified?.Invoke(currentItem);

        Debug.Log($"[ItemCard] {characterName} returned {quantityToReturn}x {currentItem.itemName}");
    }

    private void OnQuantityInputChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        if (int.TryParse(value, out int quantity))
        {
            int unallocated = GetUnallocatedQuantity();

            if (quantity > unallocated)
            {
                ShowOwnershipError($"Only {unallocated} available");
            }
            else
            {
                HideOwnershipError();
            }
        }
        else
        {
            ShowOwnershipError("Please enter a valid number");
        }
    }

    private bool ValidateOwnershipAssignment(PlayerCharacter character, int quantity, out string errorMessage)
    {
        errorMessage = "";

        if (currentItem == null)
        {
            errorMessage = "No item selected";
            return false;
        }

        if (character == null)
        {
            errorMessage = "Please select a character";
            return false;
        }

        if (quantity <= 0)
        {
            errorMessage = "Quantity must be greater than 0";
            return false;
        }

        int unallocated = GetUnallocatedQuantity();
        if (quantity > unallocated)
        {
            errorMessage = $"Only {unallocated} available";
            return false;
        }

        return true;
    }

    private void ShowOwnershipError(string message, bool isError = true)
    {
        if (ownershipErrorText == null) return;

        ownershipErrorText.text = message;
        ownershipErrorText.color = isError ? Color.red : Color.green;
        ownershipErrorText.gameObject.SetActive(true);

        // Auto-hide success messages after 2 seconds
        if (!isError)
        {
            CancelInvoke(nameof(HideOwnershipError));
            Invoke(nameof(HideOwnershipError), 2f);
        }
    }

    private void HideOwnershipError()
    {
        if (ownershipErrorText != null)
            ownershipErrorText.gameObject.SetActive(false);
    }

    // =========================================================================
    // NOTES SECTION
    // =========================================================================

    private void PopulateNotesSection()
    {
        ClearNotesSection();

        if (currentItem.playerNotes == null || currentItem.playerNotes.Count == 0)
        {
            // Show "no notes" message
            if (noNotesText != null)
                noNotesText.gameObject.SetActive(true);
            return;
        }

        // Hide "no notes" message
        if (noNotesText != null)
            noNotesText.gameObject.SetActive(false);

        // Spawn note rows
        if (noteRowPrefab != null && notesContainer != null)
        {
            foreach (var note in currentItem.playerNotes)
            {
                var rowObj = Instantiate(noteRowPrefab, notesContainer);
                var rowUI = rowObj.GetComponent<NoteRowUI>();

                if (rowUI != null)
                {
                    rowUI.SetNote(note);

                    // Subscribe to remove event
                    rowUI.OnRemoveRequested += () => RemoveNote(note, rowUI);
                }

                spawnedNoteRows.Add(rowObj);
            }

            Debug.Log($"[ItemCard] Spawned {spawnedNoteRows.Count} note rows");
        }
        else
        {
            Debug.LogWarning("[ItemCard] NoteRowPrefab or NotesContainer not assigned!");
        }
    }

    private void ClearNotesSection()
    {
        foreach (var row in spawnedNoteRows)
        {
            if (row != null)
                Destroy(row);
        }
        spawnedNoteRows.Clear();

        if (noNotesText != null)
            noNotesText.gameObject.SetActive(false);
    }

    private void RemoveNote(PlayerNote note, NoteRowUI rowUI)
    {
        Debug.Log($"[ItemCard] Removing note by {note.playerName}");

        currentItem.RemovePlayerNote(note.playerName, note.note);

        // Remove from UI
        if (rowUI != null)
        {
            spawnedNoteRows.Remove(rowUI.gameObject);
            Destroy(rowUI.gameObject);
        }

        // Show "no notes" if empty
        if (currentItem.playerNotes.Count == 0 && noNotesText != null)
        {
            noNotesText.gameObject.SetActive(true);
        }

        OnItemModified?.Invoke(currentItem);
    }

    // =========================================================================
    // VISUAL SETUP
    // =========================================================================

    private void SetupCategoryColor()
    {
        if (categoryColorImage == null) return;

        Color categoryColor = currentItem.category switch
        {
            ItemCategory.Weapon => new Color(0.8f, 0.2f, 0.2f, 0.3f),
            ItemCategory.Armor => new Color(0.6f, 0.4f, 0.2f, 0.3f),
            ItemCategory.MagicItem => new Color(0.6f, 0.2f, 0.8f, 0.3f),
            ItemCategory.Consumable => new Color(0.2f, 0.8f, 0.2f, 0.3f),
            ItemCategory.Currency => new Color(1f, 0.8f, 0.2f, 0.3f),
            ItemCategory.Tool => new Color(0.4f, 0.4f, 0.4f, 0.3f),
            _ => new Color(0.5f, 0.5f, 0.5f, 0.3f)
        };

        categoryColorImage.color = categoryColor;
    }

    // =========================================================================
    // VIEW STATE MANAGEMENT
    // =========================================================================

    private void SetViewState(bool expanded, bool instant = false)
    {
        isExpanded = expanded;

        if (instant)
        {
            SetViewStateImmediate(expanded);
        }
        else if (animateExpansion)
        {
            StartCoroutine(AnimateViewState(expanded));
        }
        else
        {
            SetViewStateImmediate(expanded);
        }
    }

    private void SetViewStateImmediate(bool expanded)
    {
        // Toggle view GameObjects
        collapsedView?.SetActive(!expanded);
        expandedView?.SetActive(expanded);

        // Set size
        if (expanded)
        {
            rectTransform.sizeDelta = new Vector2(expandedWidth, expandedHeight);

            // Disable layout element so it doesn't mess with grid
            var layoutElement = GetComponent<LayoutElement>();
            if (layoutElement != null)
                layoutElement.ignoreLayout = true;
        }
        else
        {
            rectTransform.sizeDelta = new Vector2(collapsedWidth, collapsedHeight);

            // Re-enable layout element
            var layoutElement = GetComponent<LayoutElement>();
            if (layoutElement != null)
                layoutElement.ignoreLayout = false;
        }

        // Update highlight
        if (selectedHighlight != null)
            selectedHighlight.gameObject.SetActive(expanded);
    }

    private System.Collections.IEnumerator AnimateViewState(bool expanding)
    {
        float elapsed = 0f;

        Vector2 startSize = rectTransform.sizeDelta;
        Vector2 targetSize = expanding
            ? new Vector2(expandedWidth, expandedHeight)
            : new Vector2(collapsedWidth, collapsedHeight);

        // Disable layout during animation
        var layoutElement = GetComponent<LayoutElement>();
        if (layoutElement != null)
            layoutElement.ignoreLayout = true;

        // Animate size
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            t = Mathf.SmoothStep(0, 1, t);

            rectTransform.sizeDelta = Vector2.Lerp(startSize, targetSize, t);
            yield return null;
        }

        rectTransform.sizeDelta = targetSize;

        // Toggle views at the end
        collapsedView?.SetActive(!expanding);
        expandedView?.SetActive(expanding);

        // Re-enable layout if collapsed
        if (!expanding && layoutElement != null)
            layoutElement.ignoreLayout = false;

        // Update highlight
        if (selectedHighlight != null)
            selectedHighlight.gameObject.SetActive(expanding);
    }

    // =========================================================================
    // EVENT HANDLERS
    // =========================================================================

    private void OnCardClicked()
    {
        if (!isExpanded)
        {
            ExpandCard();
        }
    }

    private void ChangeQuantity(int delta)
    {
        if (currentItem == null) return;

        int newQuantity = currentItem.quantity + delta;
        if (newQuantity < 0) return;

        currentItem.quantity = newQuantity;

        UpdateDisplay();
        OnItemModified?.Invoke(currentItem);
    }

    private void DeleteItem()
    {
        if (currentItem == null) return;

        Debug.Log($"[ItemCard] Deleting item: {currentItem.itemName}");
        OnItemDeleted?.Invoke(currentItem);
    }

    private void ClaimItem()
    {
        if (currentItem == null) return;

        string currentPlayer = PlayerPrefs.GetString("UserName", "Player");
        currentItem.currentOwner = currentPlayer;

        UpdateDisplay();
        OnItemModified?.Invoke(currentItem);

        Debug.Log($"[ItemCard] {currentPlayer} claimed {currentItem.itemName}");
    }


    private void TransferItem()
    {
        if (currentItem == null) return;
        Debug.Log($"[ItemCard] TRANSFER (permanent) requested for {currentItem.itemName}");
        OnTransferRequested?.Invoke(currentItem);
        UpdateDisplay();
    }

    private void ShareItem()
    {
        if (currentItem == null) return;

        // Open dialog to select quantity to claim
        Debug.Log($"[ItemCard] SHARE (claim) requested for {currentItem.itemName}");

        // For now, just claim 1. Later you can add a quantity dialog
        int quantityToClaim = 1;
        OnShareRequested?.Invoke(currentItem, quantityToClaim);
        UpdateDisplay();
    }

    private void ReturnItem()
    {
        if (currentItem == null) return;

        // Open dialog to select quantity to return
        Debug.Log($"[ItemCard] RETURN (unclaim) requested for {currentItem.itemName}");

        // For now, just return 1. Later you can add a quantity dialog
        int quantityToReturn = 1;
        OnReturnRequested?.Invoke(currentItem, quantityToReturn);
        UpdateDisplay();
    }

    private void AddPlayerNote()
    {
        // TODO: Open add note dialog
        Debug.Log($"[ItemCard] Add note to {currentItem?.itemName}");

        // Temporary: Add a test note
        string playerName = PlayerPrefs.GetString("UserName", "Player");
        currentItem.AddPlayerNote(playerName, "This is a test note!");

        // Refresh notes section
        PopulateNotesSection();

        OnItemModified?.Invoke(currentItem);
    }

    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    private void ConfigureForMode(CardMode mode)
    {
        cardMode = mode;

        switch (mode)
        {
            case CardMode.Personal:
                shareButton?.gameObject.SetActive(false);
                returnButton?.gameObject.SetActive(false);
                increaseQuantityButton?.gameObject.SetActive(true);
                decreaseQuantityButton?.gameObject.SetActive(true);
                deleteItemButton?.gameObject.SetActive(true);
                if (transferButton != null)
                {
                    var characterMang = CharacterManager.Instance;
                    var character = characterMang?.GetCharacterById(currentItem?.currentOwner);
                    bool inGroup = !string.IsNullOrEmpty(character?.groupId);
                    transferButton.gameObject.SetActive(inGroup); // Only show if in group

                    // Update button text
                    var buttonText = transferButton.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText) buttonText.text = "Transfer to Group";
                }
                break;

            case CardMode.Group:
                // Group items: can claim (share), return, or transfer to personal
                var characterManager = CharacterManager.Instance;
                string currentCharacterId = PlayerPrefs.GetString("SelectedCharacterId", "");

                // Check if current character owns any of this item
                var inventoryManager = InventoryManager.Instance;
                var ownership = inventoryManager?.GetAllOwnerships()
                    .FirstOrDefault(o => o.itemId == currentItem?.itemId &&
                                         o.characterId == currentCharacterId);

                int ownedQuantity = ownership?.quantityOwned ?? 0;
                bool ownsAny = ownedQuantity > 0;

                // Get unclaimed amount
                int unclaimed = inventoryManager?.GetUnallocatedQuantity(currentItem?.itemId ?? "") ?? 0;
                bool hasUnclaimed = unclaimed > 0;

                // Share button: visible if there's unclaimed quantity
                shareButton?.gameObject.SetActive(hasUnclaimed);

                // Return button: visible if character owns some
                returnButton?.gameObject.SetActive(ownsAny);

                // Transfer button: visible if character owns some (transfer to personal)
                if (transferButton != null)
                {
                    transferButton.gameObject.SetActive(ownsAny);
                    var buttonText = transferButton.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText) buttonText.text = "Transfer to Personal";
                }

                
                increaseQuantityButton?.gameObject.SetActive(true);
                decreaseQuantityButton?.gameObject.SetActive(true);
                deleteItemButton?.gameObject.SetActive(true);
                break;

            case CardMode.ReadOnly:
                shareButton?.gameObject.SetActive(false);
                transferButton?.gameObject.SetActive(false);
                returnButton?.gameObject.SetActive(false);
                increaseQuantityButton?.gameObject.SetActive(false);
                decreaseQuantityButton?.gameObject.SetActive(false);
                deleteItemButton?.gameObject.SetActive(false);
                break;
        }
    }

    void OnDestroy()
    {
        // Clean up spawned prefabs
        ClearPropertiesSection();
        ClearNotesSection();
        ClearOwnershipSection();

        // Clean up button listeners
        cardButton?.onClick.RemoveAllListeners();
        closeExpandedButton?.onClick.RemoveAllListeners();
        increaseQuantityButton?.onClick.RemoveAllListeners();
        decreaseQuantityButton?.onClick.RemoveAllListeners();
        deleteItemButton?.onClick.RemoveAllListeners();
        shareButton?.onClick.RemoveAllListeners();
        returnButton?.onClick.RemoveAllListeners();
        transferButton?.onClick.RemoveAllListeners();
        addNoteButton?.onClick.RemoveAllListeners();
    }
}