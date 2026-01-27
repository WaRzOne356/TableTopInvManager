using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI component for custom property rows in the AddItemDialog
/// Used to add custom key-value properties to items
/// </summary>
public class PropertyRowUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField propertyNameField;
    [SerializeField] private TMP_InputField propertyValueField;
    [SerializeField] private Button removeButton;

    [Header("Validation")]
    [SerializeField] private Color validColor = new Color(0.8f, 1f, 0.8f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.8f, 0.8f);

    // Events
    public System.Action<PropertyRowUI> OnRemoveRequested;

    private void Awake()
    {
        SetupEventHandlers();
    }

    private void SetupEventHandlers()
    {
        if (removeButton != null)
            removeButton.onClick.AddListener(RequestRemove);

        if (propertyNameField != null)
            propertyNameField.onValueChanged.AddListener(ValidatePropertyName);

        if (propertyValueField != null)
            propertyValueField.onValueChanged.AddListener(ValidatePropertyValue);
    }

    /// <summary>
    /// Get the property name from the input field
    /// </summary>
    public string GetPropertyName()
    {
        return propertyNameField?.text?.Trim() ?? "";
    }

    /// <summary>
    /// Get the property value from the input field
    /// </summary>
    public string GetPropertyValue()
    {
        return propertyValueField?.text?.Trim() ?? "";
    }

    /// <summary>
    /// Set initial values for the property
    /// </summary>
    public void SetProperty(string propertyName, string propertyValue)
    {
        if (propertyNameField != null)
            propertyNameField.text = propertyName;

        if (propertyValueField != null)
            propertyValueField.text = propertyValue;

        ValidatePropertyName(propertyName);
        ValidatePropertyValue(propertyValue);
    }

    /// <summary>
    /// Make the property row read-only (hide remove button)
    /// </summary>
    public void SetReadOnly(bool readOnly)
    {
        if (removeButton != null)
            removeButton.gameObject.SetActive(!readOnly);
    }

    /// <summary>
    /// Check if this property row has valid data
    /// </summary>
    public bool IsValid()
    {
        string name = GetPropertyName();
        string value = GetPropertyValue();

        return !string.IsNullOrWhiteSpace(name) && 
               !string.IsNullOrWhiteSpace(value) &&
               name.Length <= 50 &&
               value.Length <= 200;
    }

    private void ValidatePropertyName(string propertyName)
    {
        if (propertyNameField == null) return;

        bool isValid = !string.IsNullOrWhiteSpace(propertyName) && propertyName.Length <= 50;

        // Visual feedback
        var colors = propertyNameField.colors;
        colors.normalColor = isValid ? Color.white : invalidColor;
        propertyNameField.colors = colors;

        // Show placeholder or error
        if (propertyNameField.placeholder is TextMeshProUGUI placeholder)
        {
            placeholder.text = isValid || string.IsNullOrEmpty(propertyName) 
                ? "Property Name (e.g., 'Damage', 'Armor Class')" 
                : "Name too long (max 50 chars)";
        }
    }

    private void ValidatePropertyValue(string propertyValue)
    {
        if (propertyValueField == null) return;

        bool isValid = !string.IsNullOrWhiteSpace(propertyValue) && propertyValue.Length <= 200;

        // Visual feedback
        var colors = propertyValueField.colors;
        colors.normalColor = isValid ? Color.white : invalidColor;
        propertyValueField.colors = colors;

        // Show placeholder or error
        if (propertyValueField.placeholder is TextMeshProUGUI placeholder)
        {
            placeholder.text = isValid || string.IsNullOrEmpty(propertyValue) 
                ? "Property Value (e.g., '1d8+2', '+2 AC')" 
                : "Value too long (max 200 chars)";
        }
    }

    private void RequestRemove()
    {
        OnRemoveRequested?.Invoke(this);
    }

    private void OnDestroy()
    {
        // Clean up button listener
        if (removeButton != null)
            removeButton.onClick.RemoveListener(RequestRemove);
    }
}