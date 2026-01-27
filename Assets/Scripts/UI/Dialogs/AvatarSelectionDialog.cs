using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

/// <summary>
/// Dialog for selecting user avatar from predefined options
/// </summary>
public class AvatarSelectionDialog : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Transform avatarGridContainer;
    [SerializeField] private GameObject avatarOptionPrefab;
    [SerializeField] private Button selectButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button uploadButton;
    [SerializeField] private TextMeshProUGUI selectedAvatarNameText;

    [Header("Avatar Options")]
    [SerializeField] private List<Sprite> defaultAvatars = new List<Sprite>();

    [Header("Visual Settings")]
    [SerializeField] private Color selectedColor = new Color(0.4f, 1f, 0.4f);
    [SerializeField] private Color normalColor = Color.white;

    private Action<Sprite> onAvatarSelectedCallback;
    private Sprite currentlySelectedAvatar;
    private List<GameObject> spawnedAvatarOptions = new List<GameObject>();

    private void Awake()
    {
        selectButton?.onClick.AddListener(OnSelectClicked);
        cancelButton?.onClick.AddListener(CloseDialog);
        uploadButton?.onClick.AddListener(OnUploadClicked);

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Show the dialog with callback for avatar selection
    /// </summary>
    public void ShowDialog(Sprite currentAvatarSprite, Action<Sprite> onAvatarSelected)
    {
        onAvatarSelectedCallback = onAvatarSelected;
        currentlySelectedAvatar = currentAvatarSprite;

        PopulateAvatarOptions();
        UpdateSelectedDisplay();

        gameObject.SetActive(true);
    }

    private void PopulateAvatarOptions()
    {
        ClearAvatarOptions();

        if (avatarOptionPrefab == null || avatarGridContainer == null)
        {
            Debug.LogWarning("[AvatarSelectionDialog] AvatarOptionPrefab or GridContainer not assigned");
            return;
        }

        // Create option for each default avatar
        foreach (var avatarSprite in defaultAvatars)
        {
            if (avatarSprite == null) continue;

            CreateAvatarOption(avatarSprite);
        }
    }

    private void CreateAvatarOption(Sprite avatarSprite)
    {
        var optionObj = Instantiate(avatarOptionPrefab, avatarGridContainer);
        spawnedAvatarOptions.Add(optionObj);

        // Set the avatar image
        var avatarImage = optionObj.transform.Find("AvatarImage")?.GetComponent<Image>();
        
        if (avatarImage != null)
            avatarImage.sprite = avatarSprite;
        else
            Debug.Log("[AvatarSelectionDialog] AvatarOption Creation Avatar Image is null");
        // Setup button
        var button = optionObj.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => OnAvatarOptionClicked(avatarSprite, button));
        }
        else
        {
            Debug.Log("[AvatarSelectionDialog] AvatarOption button null");
        }

        // Highlight if currently selected
        if (currentlySelectedAvatar == avatarSprite)
        {
            HighlightOption(button, true);
        }
        else
        {
            Debug.Log("[AvatarSelectionDialog] AvatarOption currentlySelectedAvatar is not avatarSprite");
        }
    }

    private void OnAvatarOptionClicked(Sprite avatarSprite, Button button)
    {
        currentlySelectedAvatar = avatarSprite;

        // Update all button highlights
        foreach (var optionObj in spawnedAvatarOptions)
        {
            var btn = optionObj.GetComponent<Button>();
            if (btn != null)
            {
                HighlightOption(btn, btn == button);
            }
        }

        UpdateSelectedDisplay();

        Debug.Log($"[AvatarSelectionDialog] Selected avatar: {avatarSprite.name}");
    }

    private void HighlightOption(Button button, bool highlight)
    {
        if (button == null) return;

        var buttonImage = button.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = highlight ? selectedColor : normalColor;
        }

        // Optional: Add border or outline
        var outline = button.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = highlight;
            outline.effectColor = selectedColor;
        }
    }

    private void UpdateSelectedDisplay()
    {
        if (selectedAvatarNameText != null && currentlySelectedAvatar != null)
        {
            selectedAvatarNameText.text = $"Selected: {currentlySelectedAvatar.name}";
        }

        // Enable/disable select button
        if (selectButton != null)
        {
            selectButton.interactable = currentlySelectedAvatar != null;
        }
    }

    private void OnSelectClicked()
    {
        if (currentlySelectedAvatar != null)
        {
            onAvatarSelectedCallback?.Invoke(currentlySelectedAvatar);
            CloseDialog();
        }
    }

    private void OnUploadClicked()
    {
        // TODO: Implement custom avatar upload
        // This would open a file picker and load a custom image
        Debug.Log("[AvatarSelectionDialog] Custom avatar upload - coming soon!");

        // For now, show a message
        if (selectedAvatarNameText != null)
        {
            selectedAvatarNameText.text = "Custom upload coming soon!";
            selectedAvatarNameText.color = Color.yellow;
        }
    }

    private void ClearAvatarOptions()
    {
        foreach (var optionObj in spawnedAvatarOptions)
        {
            if (optionObj != null)
                Destroy(optionObj);
        }
        spawnedAvatarOptions.Clear();
    }

    private void CloseDialog()
    {
        ClearAvatarOptions();
        onAvatarSelectedCallback = null;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        selectButton?.onClick.RemoveAllListeners();
        cancelButton?.onClick.RemoveAllListeners();
        uploadButton?.onClick.RemoveAllListeners();
        ClearAvatarOptions();
    }

    private void Update()
    {
      
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem != null)
            {
                var pointerData = new UnityEngine.EventSystems.PointerEventData(eventSystem)
                {
                    position = mouse.position.ReadValue()
                };

                var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                eventSystem.RaycastAll(pointerData, results);

                Debug.Log($"[AvatarDialog] Click detected at {pointerData.position}. Raycast hit {results.Count} objects:");
                foreach (var result in results)
                {
                    Debug.Log($"  - {result.gameObject.name} (depth: {result.depth}, sortingOrder: {result.sortingOrder})");
                }
            }
        }
    }


}