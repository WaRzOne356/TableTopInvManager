using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI component for displaying a single search result in the AddItemDialog
/// </summary>
public class SearchResultUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI sourceText;
    [SerializeField] private Image sourceIcon;
    [SerializeField] private Button selectButton;
    [SerializeField] private Image highlightImage;

    [Header("Source Colors")]
    [SerializeField] private Color customItemColor = new Color(0.6f, 0.2f, 0.8f); // Purple
    [SerializeField] private Color dnd5eColor = new Color(0.8f, 0.2f, 0.2f);      // Red
    [SerializeField] private Color open5eColor = new Color(0.2f, 0.6f, 0.8f);    // Blue
    [SerializeField] private Color defaultColor = new Color(0.5f, 0.5f, 0.5f);   // Gray

    // Events
    public System.Action<ItemSearchResult> OnResultSelected;

    // State
    public ItemSearchResult SearchResult { get; private set; }

    private void Awake()
    {
        if (selectButton != null)
            selectButton.onClick.AddListener(SelectThis);
    }

    /// <summary>
    /// Setup the search result card with data
    /// </summary>
    public void Setup(ItemSearchResult result)
    {
        SearchResult = result;

        // Set item name
        if (itemNameText != null)
            itemNameText.text = result.Name;

        // Set description
        if (descriptionText != null)
        {
            string desc = result.Description;
            if (desc.Length > 120)
                desc = desc.Substring(0, 117) + "...";
            descriptionText.text = desc;
        }

        // Set source
        if (sourceText != null)
        {
            sourceText.text = GetSourceDisplayName(result.Source);
        }

        // Set source icon color
        if (sourceIcon != null)
        {
            sourceIcon.color = GetSourceColor(result.Source);
        }

        // Start not highlighted
        SetHighlight(false);
    }

    /// <summary>
    /// Toggle highlight state
    /// </summary>
    public void SetHighlight(bool highlighted)
    {
        if (highlightImage != null)
        {
            highlightImage.enabled = highlighted;

            if (highlighted)
            {
                highlightImage.color = new Color(1f, 1f, 1f, 0.2f);
            }
        }

        // Optional: scale effect
        if (highlighted)
        {
            transform.localScale = Vector3.one * 1.05f;
        }
        else
        {
            transform.localScale = Vector3.one;
        }
    }

    private void SelectThis()
    {
        OnResultSelected?.Invoke(SearchResult);
    }

    private string GetSourceDisplayName(ItemSource source)
    {
        return source switch
        {
            ItemSource.Manual => "Custom Item",
            ItemSource.DnD5eAPI => "D&D 5e",
            ItemSource.Open5eAPI => "Open5e",
            ItemSource.QuickAdd => "Quick Add",
            _ => "Unknown"
        };
    }

    private Color GetSourceColor(ItemSource source)
    {
        return source switch
        {
            ItemSource.Manual => customItemColor,
            ItemSource.DnD5eAPI => dnd5eColor,
            ItemSource.Open5eAPI => open5eColor,
            _ => defaultColor
        };
    }

    private void OnDestroy()
    {
        if (selectButton != null)
            selectButton.onClick.RemoveListener(SelectThis);
    }
}