using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PropertyRowUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField propertyNameField;
    [SerializeField] private TMP_InputField propertyValueField;
    [SerializeField] private Button removeButton;

    public System.Action<PropertyRowUI> OnRemoveRequested;

    private void Start()
    {
        removeButton?.onClick.AddListener(()=>OnRemoveRequested?.Invoke(this));
    }
    
    public string GetPropertyName()=> propertyNameField?.text ?? "";
    public string GetPropertyValue()=> propertyNameField?.text ?? "";

    public void SetProperty(string name, string value)
    {
        if (propertyNameField != null)
            propertyNameField.text = name;
        if (propertyValueField != null)
            propertyValueField.text = value;
    }
    
    
}
