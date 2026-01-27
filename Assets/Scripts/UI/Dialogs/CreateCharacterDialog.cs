using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Linq;

public class CreateCharacterDialog : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField characterNameInput;
    [SerializeField] private TMP_InputField characterClassInput;
    [SerializeField] private TMP_InputField levelInput;
    [SerializeField] private Button createButton;
    [SerializeField] private Button cancelButton;
    
    [Header("Avatar Selection")]
    [SerializeField] private Image characterAvatarPreview;
    [SerializeField] private Button selectAvatarButton;
    [SerializeField] private AvatarSelectionDialog avatarSelectionDialog;

    public Action<PlayerCharacter> OnCharacterCreated;
    private Sprite selectedAvatarSprite;
    private string currentUserId;

    private void Awake()
    {
        createButton?.onClick.AddListener(OnCreateClicked);
        cancelButton?.onClick.AddListener(CloseDialog);
        selectAvatarButton?.onClick.AddListener(OpenAvatarSelection);

        gameObject.SetActive(false);
    }

    public void ShowDialog(string userId)
    {
        currentUserId = userId;

        // Clear inputs
        if (characterNameInput != null)
            characterNameInput.text = "";
        if (characterClassInput != null)
            characterClassInput.text = "Fighter";
        if (levelInput != null)
            levelInput.text = "1";

        // Reset avatar to default
        LoadDefaultAvatar();

        gameObject.SetActive(true);
    }

    private void LoadDefaultAvatar()
    {
        // Load default avatar sprite
        selectedAvatarSprite = Resources.Load<Sprite>("Avatars/Avatar_Default");

        if (characterAvatarPreview != null && selectedAvatarSprite != null)
        {
            characterAvatarPreview.sprite = selectedAvatarSprite;
        }
    }

    private void OpenAvatarSelection()
    {
        if (avatarSelectionDialog != null)
        {
            avatarSelectionDialog.ShowDialog(
                currentAvatarSprite: selectedAvatarSprite,
                onAvatarSelected: OnAvatarSelected
            );
        }
        else
        {
            Debug.LogWarning("[CreateCharacter] AvatarSelectionDialog not assigned");
        }
    }

    private void OnAvatarSelected(Sprite newAvatar)
    {
        selectedAvatarSprite = newAvatar;

        if (characterAvatarPreview != null)
        {
            characterAvatarPreview.sprite = selectedAvatarSprite;
        }

        Debug.Log($"[CreateCharacter] Avatar selected: {newAvatar.name}");
    }

    private void OnCreateClicked()
    {
        string name = characterNameInput?.text ?? "";
        string class_ = characterClassInput?.text ?? "Fighter";
        int level = int.TryParse(levelInput?.text, out int l) ? l : 1;

        if (string.IsNullOrWhiteSpace(name))
        {
            Debug.LogWarning("Character name cannot be empty");
            return;
        }
        // VERIFY userId is valid before creating character
        var userManager = UserManager.Instance;
        if (userManager != null)
        {
            var user = userManager.GetUsers().FirstOrDefault(u => u.userId == currentUserId);
            if (string.IsNullOrEmpty(user.userId))
            {
                Debug.LogError($"[CreateCharacter] Invalid userId: {currentUserId}");
                return;
            }
        }

        var newCharacter = new PlayerCharacter(name, currentUserId)
        {
            characterClass = class_,
            level = Mathf.Clamp(level, 1, 20),
             avatarSpriteName = selectedAvatarSprite?.name ?? "Avatar_Default"
        };

        OnCharacterCreated?.Invoke(newCharacter);
        CloseDialog();
    }

    private void CloseDialog()
    {
        gameObject.SetActive(false);
    }
}