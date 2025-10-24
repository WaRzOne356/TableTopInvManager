using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InventorySystem.Data;

public class AddItemDialog : MonoBehaviour
{
    [Header("Dialog References")]
    [SerializeField] private GameObject dialogPanel;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button cancelButton;

    [Header("Mode Switching")]
    [SerializeField] private GameObject searchModePanel;
    [SerializeField] private GameObject createModePanel;
    [SerializeField] private Button switchToCreateModeButton;
    [SerializeField] private Button switchToSearchModeButton;

    [Header("Search Mode UI")]
    [SerializeField] private TMP_InputField searchField;
    [SerializeField] private Button searchButton;
    [SerializeField] private Transform searchResultsContainer;
    [SerializeField] private GameObject searchResultPrefab;
    [SerializeField] private TextMeshProUGUI searchStatusText;
    [SerializeField] private Button addSelectedItemButton;
    [SerializeField] private TMP_InputField quantityInputField;

    [Header("Create Mode UI")]
    [SerializeField] private Button saveButton;
    [SerializeField] private TMP_InputField itemNameField;
    [SerializeField] private TMP_Dropdown categoryDropdown;
    [SerializeField] private TMP_InputField descriptionField;
    [SerializeField] private TMP_InputField createQuantityField;
    [SerializeField] private TMP_InputField weightField;
    [SerializeField] private TMP_InputField valueField;
    [SerializeField] private TMP_InputField tagField;
    [SerializeField] private TMP_InputField createdByField;

    [Header("Create Mode - Image Upload")]
    [SerializeField] private Button uploadImageButton;
    [SerializeField] private Image thumbnailPreview;
    [SerializeField] private TextMeshProUGUI imageStatusText;
    [SerializeField] private Button clearImageButton;

    [Header("Create Mode - Custom Properties")]
    [SerializeField] private Transform propertiesContainer;
    [SerializeField] private GameObject propertyRowPrefab;
    [SerializeField] private Button addPropertyButton;

    [Header("Validation")]
    [SerializeField] private TextMeshProUGUI validationMessageText;
    [SerializeField] private Color validColor = Color.green;
    [SerializeField] private Color invalidColor = Color.red;

    // Events
    public System.Action<CustomItemData> OnItemCreated;
    public System.Action<InventoryItem> OnItemSelected; // New event for search results
    public System.Action OnDialogClosed;

    // State
    private enum DialogMode { Search, Create }
    private DialogMode currentMode = DialogMode.Search;

    private List<PropertyRowUI> propertyRows;
    private byte[] selectedImageData;
    private string selectedImageFileName;
    private Texture2D previewTexture;

    private List<GameObject> searchResultObjects;
    private ItemSearchResult selectedSearchResult;
    private List<ItemSearchResult> currentSearchResults;

    void Awake()
    {
        propertyRows = new List<PropertyRowUI>();
        searchResultObjects = new List<GameObject>();
        currentSearchResults = new List<ItemSearchResult>();

        SetupEventHandlers();
        SetupCategoryDropdown();

        // Start hidden
        dialogPanel.SetActive(false);
    }

    private void SetupEventHandlers()
    {
        // Common
        closeButton?.onClick.AddListener(CloseDialog);
        cancelButton?.onClick.AddListener(CloseDialog);

        // Mode switching
        switchToCreateModeButton?.onClick.AddListener(() => SwitchMode(DialogMode.Create));
        switchToSearchModeButton?.onClick.AddListener(() => SwitchMode(DialogMode.Search));

        // Search mode
        searchButton?.onClick.AddListener(PerformSearch);
        searchField?.onSubmit.AddListener(_ => PerformSearch());
        addSelectedItemButton?.onClick.AddListener(AddSelectedItem);

        // Create mode
        saveButton?.onClick.AddListener(SaveCustomItem);
        uploadImageButton?.onClick.AddListener(OpenImageSelector);
        clearImageButton?.onClick.AddListener(ClearSelectedImage);
        addPropertyButton?.onClick.AddListener(AddPropertyRow);

        // Validation
        itemNameField?.onValueChanged.AddListener(ValidateForm);
        createQuantityField?.onValueChanged.AddListener(ValidateForm);
        weightField?.onValueChanged.AddListener(ValidateForm);
        valueField?.onValueChanged.AddListener(ValidateForm);
    }

    private void SetupCategoryDropdown()
    {
        if (categoryDropdown == null) return;

        categoryDropdown.ClearOptions();
        var options = new List<string>();

        foreach (ItemCategory category in Enum.GetValues(typeof(ItemCategory)))
            options.Add(GetCategoryDisplayName(category));

        categoryDropdown.AddOptions(options);
    }

    private string GetCategoryDisplayName(ItemCategory category)
    {
        return category switch
        {
            ItemCategory.MagicItem => "Magic Item",
            ItemCategory.QuestItem => "Quest Item",
            _ => category.ToString()
        };
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Show dialog in Search mode
    /// </summary>
    public void ShowDialog(string playerName = "")
    {
        dialogPanel.SetActive(true);
        SwitchMode(DialogMode.Search);

        // Set creator name for when they switch to Create mode
        if (createdByField != null)
            createdByField.text = string.IsNullOrEmpty(playerName) ? "Unknown user" : playerName;

        // Focus on search field
        searchField?.Select();
    }

    /// <summary>
    /// Show dialog in Create mode directly
    /// </summary>
    public void ShowCreateDialog(string playerName = "")
    {
        dialogPanel.SetActive(true);
        SwitchMode(DialogMode.Create);

        if (createdByField != null)
            createdByField.text = string.IsNullOrEmpty(playerName) ? "Unknown user" : playerName;

        ClearForm();
        itemNameField?.Select();
        ValidateForm("");
    }

    // =========================================================================
    // MODE SWITCHING
    // =========================================================================

    private void SwitchMode(DialogMode newMode)
    {
        currentMode = newMode;

        if (currentMode == DialogMode.Search)
        {
            searchModePanel?.SetActive(true);
            createModePanel?.SetActive(false);
            searchField?.Select();

            Debug.Log("[AddItemDialog] Switched to Search mode");
        }
        else // Create
        {
            searchModePanel?.SetActive(false);
            createModePanel?.SetActive(true);
            ClearForm();
            itemNameField?.Select();
            ValidateForm("");

            Debug.Log("[AddItemDialog] Switched to Create mode");
        }
    }

    // =========================================================================
    // SEARCH MODE
    // =========================================================================

    private async void PerformSearch()
    {
        string searchTerm = searchField?.text?.Trim();

        if (string.IsNullOrEmpty(searchTerm))
        {
            SetSearchStatus("Please enter a search term", false);
            return;
        }

        SetSearchStatus($"Searching for '{searchTerm}'...", true);
        ClearSearchResults();

        try
        {
            // Use ItemDataFetcher to search all sources
            currentSearchResults = await ItemDataFetcher.Instance.SearchItemsAsync(searchTerm);

            if (currentSearchResults.Count == 0)
            {
                SetSearchStatus($"No items found for '{searchTerm}'", false);
                return;
            }

            SetSearchStatus($"Found {currentSearchResults.Count} items", true);
            DisplaySearchResults(currentSearchResults);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AddItemDialog] Search error: {e.Message}");
            SetSearchStatus($"Search failed: {e.Message}", false);
        }
    }

    private void DisplaySearchResults(List<ItemSearchResult> results)
    {
        ClearSearchResults();

        foreach (var result in results)
        {
            CreateSearchResultCard(result);
        }
    }

    private void CreateSearchResultCard(ItemSearchResult result)
    {
        if (searchResultPrefab == null || searchResultsContainer == null)
        {
            Debug.LogWarning("[AddItemDialog] Search result prefab or container not assigned!");
            return;
        }

        var resultObj = Instantiate(searchResultPrefab, searchResultsContainer);
        var resultUI = resultObj.GetComponent<SearchResultUI>();

        if (resultUI != null)
        {
            resultUI.Setup(result);
            resultUI.OnResultSelected += OnSearchResultSelected;
        }

        searchResultObjects.Add(resultObj);
    }

    private void ClearSearchResults()
    {
        foreach (var obj in searchResultObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
        searchResultObjects.Clear();
        selectedSearchResult = null;

        if (addSelectedItemButton != null)
            addSelectedItemButton.interactable = false;
    }

    private void OnSearchResultSelected(ItemSearchResult result)
    {
        selectedSearchResult = result;

        Debug.Log($"[AddItemDialog] Selected: {result.Name} from {result.Source}");

        if (addSelectedItemButton != null)
            addSelectedItemButton.interactable = true;

        // Highlight selected result
        foreach (var obj in searchResultObjects)
        {
            var resultUI = obj.GetComponent<SearchResultUI>();
            if (resultUI != null)
                resultUI.SetHighlight(resultUI.SearchResult == result);
        }
    }

    private async void AddSelectedItem()
    {
        if (selectedSearchResult == null)
        {
            SetSearchStatus("Please select an item first", false);
            return;
        }

        // Get quantity
        int quantity = 1;
        if (quantityInputField != null && !string.IsNullOrEmpty(quantityInputField.text))
        {
            if (!int.TryParse(quantityInputField.text, out quantity) || quantity < 1)
            {
                SetSearchStatus("Invalid quantity", false);
                return;
            }
        }

        SetSearchStatus("Loading item details...", true);

        try
        {
            // Fetch full item details
            InventoryItem item = null;

            if (selectedSearchResult.Source == ItemSource.Manual)
            {
                // Custom item - fetch from database
                var customItems = CustomItemDatabase.Instance.GetAllCustomItems();
                var customItem = customItems.FirstOrDefault(i => i.itemId == selectedSearchResult.ApiId);

                if (customItem != null)
                    item = customItem.ToInventoryItem();
            }
            else
            {
                // API item - fetch from ItemDataFetcher
                item = await ItemDataFetcher.Instance.FetchItemDetailsAsync(selectedSearchResult);
            }

            if (item != null)
            {
                item.quantity = quantity;
                OnItemSelected?.Invoke(item);

                SetSearchStatus($"Added {quantity}x {item.itemName}", true);

                // Auto-close after short delay
                await Task.Delay(1000);
                CloseDialog();
            }
            else
            {
                SetSearchStatus("Failed to load item details", false);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AddItemDialog] Error adding item: {e.Message}");
            SetSearchStatus($"Error: {e.Message}", false);
        }
    }

    private void SetSearchStatus(string message, bool isValid)
    {
        if (searchStatusText != null)
        {
            searchStatusText.text = message;
            searchStatusText.color = isValid ? validColor : invalidColor;
        }
    }

    // =========================================================================
    // CREATE MODE
    // =========================================================================

    private void ClearForm()
    {
        itemNameField?.SetTextWithoutNotify("");
        categoryDropdown?.SetValueWithoutNotify(0);
        descriptionField?.SetTextWithoutNotify("");
        createQuantityField?.SetTextWithoutNotify("1");
        weightField?.SetTextWithoutNotify("1.0");
        valueField?.SetTextWithoutNotify("10");
        tagField?.SetTextWithoutNotify("");

        ClearSelectedImage();
        ClearPropertyRows();
        ClearValidationError();
    }

    private void ValidateForm(string _)
    {
        var validationResults = ValidateCurrentForm();

        if (saveButton != null)
            saveButton.interactable = validationResults.isValid;

        if (validationMessageText != null)
        {
            if (validationResults.isValid)
            {
                validationMessageText.text = "Ready to create item";
                validationMessageText.color = validColor;
            }
            else
            {
                validationMessageText.text = validationResults.errorMessage;
                validationMessageText.color = invalidColor;
            }
        }
    }

    private ValidationResult ValidateCurrentForm()
    {
        if (string.IsNullOrWhiteSpace(itemNameField?.text))
            return new ValidationResult(false, "Item name is required");

        if (itemNameField.text.Length > 100)
            return new ValidationResult(false, "Item name too long (max 100 characters)");

        if (!int.TryParse(createQuantityField?.text, out int quantity) || quantity < 1)
            return new ValidationResult(false, "Quantity must be a positive number");

        if (!float.TryParse(weightField?.text, out float weight) || weight < 0.0f)
            return new ValidationResult(false, "Weight must be a non-negative number");

        if (!int.TryParse(valueField?.text, out int value) || value < 0)
            return new ValidationResult(false, "Value must be a non-negative number");

        if (descriptionField?.text.Length > 1000)
            return new ValidationResult(false, "Description too long (max 1000 characters)");

        return new ValidationResult(true, "");
    }

    private async void SaveCustomItem()
    {
        var validation = ValidateCurrentForm();
        if (!validation.isValid)
        {
            ShowValidationError(validation.errorMessage);
            return;
        }

        try
        {
            var customItem = new CustomItemData
            {
                itemName = itemNameField.text.Trim(),
                category = GetSelectedCategory(),
                description = descriptionField?.text ?? "",
                quantity = int.Parse(createQuantityField.text),
                weight = float.Parse(weightField.text),
                valueInGold = int.Parse(valueField.text),
                tags = tagField?.text ?? "",
                createdBy = createdByField?.text ?? "Player"
            };

            // Add custom properties
            foreach (var propRow in propertyRows)
            {
                if (!string.IsNullOrWhiteSpace(propRow.GetPropertyName()) &&
                    !string.IsNullOrWhiteSpace(propRow.GetPropertyValue()))
                {
                    customItem.properties.Add(new CustomProperty(
                        propRow.GetPropertyName().Trim(),
                        propRow.GetPropertyValue().Trim()
                    ));
                }
            }

            // Save image if selected
            if (selectedImageData != null && !string.IsNullOrEmpty(selectedImageFileName))
            {
                string imagePath = await CustomItemDatabase.Instance.SaveCustomImageAsync(
                    selectedImageData, selectedImageFileName);
                customItem.customImagePath = imagePath;
            }

            // Add to database
            bool success = await CustomItemDatabase.Instance.AddCustomItemAsync(customItem);

            if (success)
            {
                Debug.Log($"[AddItemDialog] Created custom item: {customItem.itemName}");
                OnItemCreated?.Invoke(customItem);
                CloseDialog();
            }
            else
            {
                ShowValidationError("Failed to save item to database");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AddItemDialog] Error saving item: {e.Message}");
            ShowValidationError($"Error saving item: {e.Message}");
        }
    }

    // =========================================================================
    // CREATE MODE - IMAGE HANDLING
    // =========================================================================

    private void OpenImageSelector()
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel(
            "Select Item Image", "", "png,jpg,jpeg");

        if (!string.IsNullOrEmpty(path))
        {
            LoadImageFromPath(path);
        }
#else
        ShowValidationError("Image upload not currently supported in build");
#endif
    }

    private async void LoadImageFromPath(string path)
    {
        try
        {
            selectedImageData = await File.ReadAllBytesAsync(path);
            selectedImageFileName = Path.GetFileName(path);

            if (previewTexture != null)
                DestroyImmediate(previewTexture);

            previewTexture = new Texture2D(2, 2);
            if (previewTexture.LoadImage(selectedImageData))
            {
                if (thumbnailPreview != null)
                {
                    thumbnailPreview.sprite = Sprite.Create(
                        previewTexture,
                        new Rect(0, 0, previewTexture.width, previewTexture.height),
                        Vector2.one * 0.5f);
                }

                if (imageStatusText != null)
                    imageStatusText.text = $"Loaded: {selectedImageFileName}";

                if (clearImageButton != null)
                    clearImageButton.gameObject.SetActive(true);

                Debug.Log($"[AddItemDialog] Loaded image: {selectedImageFileName}");
            }
            else
            {
                throw new Exception("Failed to load image data");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AddItemDialog] Failed to load image: {e.Message}");
            ShowValidationError($"Failed to load image: {e.Message}");
            ClearSelectedImage();
        }
    }

    private void ClearSelectedImage()
    {
        selectedImageData = null;
        selectedImageFileName = null;

        if (previewTexture != null)
        {
            DestroyImmediate(previewTexture);
            previewTexture = null;
        }

        if (thumbnailPreview != null)
            thumbnailPreview.sprite = null;

        if (imageStatusText != null)
            imageStatusText.text = "No image selected";

        if (clearImageButton != null)
            clearImageButton.gameObject.SetActive(false);
    }

    // =========================================================================
    // CREATE MODE - CUSTOM PROPERTIES
    // =========================================================================

    private void AddPropertyRow()
    {
        if (propertyRowPrefab == null || propertiesContainer == null)
            return;

        var rowObj = Instantiate(propertyRowPrefab, propertiesContainer);
        var rowUI = rowObj.GetComponent<PropertyRowUI>();

        if (rowUI != null)
        {
            propertyRows.Add(rowUI);
            rowUI.OnRemoveRequested += RemovePropertyRow;
        }
    }

    private void RemovePropertyRow(PropertyRowUI row)
    {
        if (propertyRows.Contains(row))
        {
            propertyRows.Remove(row);
            Destroy(row.gameObject);
        }
    }

    private void ClearPropertyRows()
    {
        foreach (var prop in propertyRows)
        {
            if (prop != null)
                Destroy(prop.gameObject);
        }
        propertyRows.Clear();
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private ItemCategory GetSelectedCategory()
    {
        if (categoryDropdown == null)
            return ItemCategory.Miscellaneous;

        var categories = Enum.GetValues(typeof(ItemCategory));

        if (categoryDropdown.value >= 0 && categoryDropdown.value < categories.Length)
            return (ItemCategory)categories.GetValue(categoryDropdown.value);

        return ItemCategory.Miscellaneous;
    }

    private void ShowValidationError(string message)
    {
        if (validationMessageText != null)
        {
            validationMessageText.text = message;
            validationMessageText.color = invalidColor;
        }

        Debug.LogWarning($"[AddItemDialog] Validation error: {message}");
    }

    private void ClearValidationError()
    {
        if (validationMessageText != null)
            validationMessageText.text = "";
    }

    private void CloseDialog()
    {
        dialogPanel.SetActive(false);
        ClearForm();
        ClearSearchResults();
        OnDialogClosed?.Invoke();
    }

    private void OnDestroy()
    {
        if (previewTexture != null)
            DestroyImmediate(previewTexture);

        ClearSearchResults();
    }

    private struct ValidationResult
    {
        public bool isValid;
        public string errorMessage;

        public ValidationResult(bool valid, string error)
        {
            isValid = valid;
            errorMessage = error;
        }
    }
}