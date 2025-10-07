using System;
using System.Collections.Generic;
using System.IO;
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
    [SerializeField] private Button saveButton;

    [Header("Form Fields")]
    [SerializeField] private TMP_InputField itemNameField;
    [SerializeField] private TMP_Dropdown   categoryDropdown;
    [SerializeField] private TMP_InputField descriptionField;
    [SerializeField] private TMP_InputField quantityField;
    [SerializeField] private TMP_InputField weightField;
    [SerializeField] private TMP_InputField valueField;
    [SerializeField] private TMP_InputField tagField;
    [SerializeField] private TMP_InputField createdByField;

    [Header("Image Upload")]
    [SerializeField] private Button uploadImageButton;
    [SerializeField] private Image thumbnailPreview;
    [SerializeField] private TextMeshProUGUI imageStatusText;
    [SerializeField] private Button clearItemButton;

    [Header("Custom Properties")]
    [SerializeField] private Transform propertiesContainer;
    [SerializeField] private GameObject propertyRowPrefab;
    [SerializeField] private Button addPeropertyButton;

    [Header("Validation")]
    [SerializeField] private TextMeshProUGUI validationMessageText;
    [SerializeField] private Color validColor = Color.green;
    [SerializeField] private Color invalidColor = Color.red;


    //Events 
    public System.Action<CustomItemData> OnItemCreated;
    public System.Action OnDialogClosed;

    //states
    private List<PropertyRowUI> propertyRows;
    private byte[] selectedImageData;
    private string selectedImageFileName;
    private Texture2D previewTexture;


    
    void Awake()
    {
        propertyRows = new List<PropertyRowUI>();
        SetupEventHandlers();
        SetupCategoryDropdown();

        //Start with this UI hidden.
        dialogPanel.SetActive(false);
        
    }

    private void SetupEventHandlers()
    {
        closeButton?.onClick.AddListener(CloseDialog);
        cancelButton?.onClick.AddListener(CloseDialog);
        saveButton?.onClick.AddListener(SaveItem);

        uploadImageButton?.onClick.AddListener(OpenImageSelector);
        clearItemButton?.onClick.AddListener(ClearSelectedImage);
        addPeropertyButton?.onClick.AddListener(AddPropertyRow);

        itemNameField?.onValueChanged.AddListener(ValidateForm);
        quantityField?.onValueChanged.AddListener(ValidateForm);
        weightField?.onValueChanged.AddListener(ValidateForm);
        valueField?.onValueChanged.AddListener(ValidateForm);
    }

    private void SetupCategoryDropdown()
    {
        if (categoryDropdown == null)
            return;
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

    //Show the Dialog for creating new items
    public void ShowDialog(string playerName = "")
    {
        dialogPanel.SetActive(true);
        ClearForm();

        //Set Defualt Creator
        if (createdByField != null)
            createdByField.text = string.IsNullOrEmpty(playerName) ? "Unknown user" : playerName;

        //focuson on the name field
        itemNameField?.Select();

        ValidateForm("");
    }

    private void ClearForm()
    {
        itemNameField?.SetTextWithoutNotify("");  
        categoryDropdown?.SetValueWithoutNotify(0); 
        descriptionField?.SetTextWithoutNotify(""); 
        quantityField?.SetTextWithoutNotify("1");
        weightField?.SetTextWithoutNotify("1.0");
        valueField?.SetTextWithoutNotify("10");
        tagField?.SetTextWithoutNotify("");
        createdByField?.SetTextWithoutNotify("");


        ClearSelectedImage();
        ClearPropertyRows();
        ClearValidationError();
    }

    //Validat Form and Update UI Feedback
    private void ValidateForm(string _)
    {
        var validationResults = ValidateCurrentForm();

        //Update Save Button
        if (saveButton != null)
            saveButton.interactable = validationResults.isValid;
        //Update validation message
        if (validationMessageText != null)
        {
            if(validationResults.isValid)
            {
                validationMessageText.text = "Ready to Create item";
                validationMessageText.color = validColor;
            }
            else
            {
                validationMessageText.text = "Unable to Create item" + validationResults.errorMessage;
                validationMessageText.color = invalidColor;
            }
        }
    }

    //Validate Form Data
    private ValidationResult ValidateCurrentForm()
    {
        // Check Required Fields
        if (string.IsNullOrWhiteSpace(itemNameField?.text))
            return new ValidationResult(false, "Item Name is required");

        if (itemNameField.text.Length > 100)
            return new ValidationResult(false, "Item Name too long (max length 100)");

        if (!int.TryParse(quantityField?.text, out int quantity) || quantity < 1)
            return new ValidationResult(false, "Quantity must be a possitive number");

        if (!float.TryParse(weightField?.text, out float weight) || weight < 0.0f)
            return new ValidationResult(false, "Weight must be a non-negative number");

        if (!int.TryParse(valueField?.text, out int value) || value < 0)
            return new ValidationResult(false, "Value must be a non-negative number");

        //Check Description Length
        if (descriptionField?.text.Length > 1000)
            return new ValidationResult(false, "Description too long (max 1000 characters)");

        return new ValidationResult(true, "");
    }


    // Save the Custom item
    private async void SaveItem()
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
                quantity = int.Parse(quantityField.text),
                weight = float.Parse(weightField.text),
                valueInGold = int.Parse(valueField.text),
                tags = tagField?.text ?? "",
                createdBy = createdByField?.text ?? "Player"

            };

            //add custom properties
            foreach(var propRow in propertyRows)
            {
                if(!string.IsNullOrWhiteSpace(propRow.GetPropertyName()) &&
                   !string.IsNullOrWhiteSpace(propRow.GetPropertyValue()))
                {
                    customItem.properties.Add(new CustomProperty(
                        propRow.GetPropertyName().Trim(),
                        propRow.GetPropertyValue().Trim()
                        ));
                }
            }

            //Save Image if there is one seleceted
            if(selectedImageData != null && !string.IsNullOrEmpty(selectedImageFileName))
            {
                string imagePath = await CustomItemDatabase.Instance.SaveCustomImageAsync(
                    selectedImageData, selectedImageFileName);
                customItem.customImagePath = imagePath;
            }

            //add to DB
            bool success = await CustomItemDatabase.Instance.AddCustomItemAsync(customItem);

            if(success)
            {
                Debug.Log($"[AddItemDialog] Created custom item: {customItem.itemName}");
                OnItemCreated?.Invoke(customItem);
                CloseDialog();
            }
            else
            {
                ShowValidationError("Failed to save item to DB");
            }
        }
        catch(Exception e)
        {
            Debug.LogError($"[AddItemDialog] Error Saving item: {e.Message}");
            ShowValidationError($"Error saving item: {e.Message}");
        }
    }

    //Open Image File Selector
    private void OpenImageSelector()
    {
        #if UNITY_EDITOR
            //uSE unity open file selector if in unity
            string path = UnityEditor.EditorUtility.OpenFilePanel(
                "Select Item Image", "", "png,jpg,jpeg");

            if (!string.IsNullOrEmpty(path))
            {
                LoadImageFromPath(path);
        }
        #else 
            //This needs to be relplaced later
            ShowValidationError("Image Upload not currently supported in app");
        #endif
    }

    //Load Image from provided path
    private async void LoadImageFromPath(string path)
    {
        try
        {
            selectedImageData = await File.ReadAllBytesAsync(path);
            selectedImageFileName = Path.GetFileName(path);

            if (previewTexture != null)
            {
                DestroyImmediate(previewTexture);
            }

            previewTexture = new Texture2D(2, 2);
            if (previewTexture.LoadImage(selectedImageData))
            {
                //update preview
                if (thumbnailPreview != null)
                {
                    thumbnailPreview.sprite = Sprite.Create(previewTexture, new Rect(0, 0, previewTexture.width, previewTexture.height), Vector2.one * 0.5f);
                }

                if (imageStatusText != null) imageStatusText.text = $"Success: {selectedImageFileName} has been loaded";

                if (clearItemButton != null) clearItemButton.gameObject.SetActive(true);

                Debug.Log($"[AddItemDialog] Loaded image {selectedImageFileName}");
            }
            else throw new Exception("Failed to load image data");
        }
        catch (Exception e)
        {
            Debug.LogError($"[AddItemDialog] Failed to load image: {e.Message}");
            ShowValidationError($"Failed to load Image: {e.Message}");
            ClearSelectedImage();
        }
    }

    //Clear Selected Image
    private void ClearSelectedImage()
    {
        selectedImageData = null;
        selectedImageFileName = null;

        if (previewTexture != null)
        {
            DestroyImmediate(previewTexture);
            previewTexture = null;
        }

        if (thumbnailPreview != null) thumbnailPreview.sprite = null;
        if (imageStatusText != null) imageStatusText.text = "No Image Selected";
        if (clearItemButton != null) clearItemButton.gameObject.SetActive(false);

    }

    //Add new Property Row
    private void AddPropertyRow()
    {
        if (propertyRowPrefab == null || propertiesContainer == null)
            return;

        var rowObj = Instantiate(propertyRowPrefab, propertiesContainer);
        var rowUI = rowObj.GetComponent<PropertyRowUI>();

        if(rowUI != null)
        {
            propertyRows.Add(rowUI);
            rowUI.OnRemoveRequested += RemovePropertyRow;
        }
    }

    //Remove Property Row
    private void RemovePropertyRow(PropertyRowUI row)
    {
        if (propertyRows.Contains(row))
        {
            propertyRows.Remove(row);
            Destroy(row.gameObject);
        }
    }

    //Clear All Property Rows
    private void ClearPropertyRows()
    {
        foreach(var prop in propertyRows)
        {
            if(prop != null)
            {
                Destroy(prop.gameObject);
            }
        }
        propertyRows.Clear();
    }

    private ItemCategory GetSelectedCategory()
    {
        if (categoryDropdown == null) return ItemCategory.Miscellaneous;

        var categories = Enum.GetValues(typeof(ItemCategory));

        if (categoryDropdown.value >= 0 && categoryDropdown.value <= categories.Length)
            return (ItemCategory)categories.GetValue(categoryDropdown.value);

        return ItemCategory.Miscellaneous;
    }

    private void ShowValidationError(string str)
    {
        if (validationMessageText != null)
        {
            validationMessageText.text = str;
            validationMessageText.color = invalidColor;
        }

        Debug.LogWarning($"[AddItemDialog] Validation Error: {str}");
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
        OnDialogClosed?.Invoke();
    }

    private void OnDestroy()
    {
        if (previewTexture != null)
            DestroyImmediate(previewTexture);
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
