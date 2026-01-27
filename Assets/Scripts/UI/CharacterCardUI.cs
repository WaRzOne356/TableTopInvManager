using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class CharacterCardUI : MonoBehaviour
{
    [Header("Display Elements")]
    [SerializeField] private Image characterAvatarImage;
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private TextMeshProUGUI classLevelText;
    [SerializeField] private TextMeshProUGUI lastPlayedText;
    [SerializeField] private TextMeshProUGUI itemCountText;
    [SerializeField] private TextMeshProUGUI totalValueText;
    [SerializeField] private GameObject activeIndicator;

    [Header("Action Buttons")]
    [SerializeField] private Button viewInventoryButton;
    [SerializeField] private Button setActiveButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button editButton;

    // Events
    public Action<PlayerCharacter> OnCharacterSelected;
    public Action<PlayerCharacter> OnCharacterDeleted;
    public Action<PlayerCharacter> OnSetActiveCharacter;
    public Action<PlayerCharacter> OnEditCharacter;

    private PlayerCharacter currentCharacter;

    public void SetupCard(PlayerCharacter character)
    {
        currentCharacter = character;

        LoadCharacterAvatar();

        if (characterNameText != null)
            characterNameText.text = character.characterName;

        if (classLevelText != null)
            classLevelText.text = $"Level {character.level} {character.characterClass}";

        if (lastPlayedText != null)
        {
            TimeSpan timeSince = DateTime.Now - character.lastPlayed;
            string timeText = timeSince.TotalDays < 1
                ? "Today"
                : $"{(int)timeSince.TotalDays} days ago";
            lastPlayedText.text = $"Last played: {timeText}";
        }

        if (activeIndicator != null)
            activeIndicator.SetActive(character.isActive);

        if (setActiveButton != null)
            setActiveButton.gameObject.SetActive(!character.isActive);

        SetupButtons();
    }

    private void LoadCharacterAvatar()
    {
        if (characterAvatarImage == null || currentCharacter == null) return;

        // Try to load the avatar from Resources
        string avatarPath = $"Avatars/{currentCharacter.avatarSpriteName}";
        Sprite avatarSprite = Resources.Load<Sprite>(avatarPath);

        if (avatarSprite != null)
        {
            characterAvatarImage.sprite = avatarSprite;
        }
        else
        {
            // Fallback to default
            avatarSprite = Resources.Load<Sprite>("Avatars/Avatar_Default");
            if (avatarSprite != null)
            {
                characterAvatarImage.sprite = avatarSprite;
            }
            else
            {
                Debug.LogWarning($"[CharacterCard] Could not load avatar: {avatarPath}");
            }
        }
    }

    private void SetupButtons()
    {
        viewInventoryButton?.onClick.AddListener(() => OnCharacterSelected?.Invoke(currentCharacter));
        setActiveButton?.onClick.AddListener(() => OnSetActiveCharacter?.Invoke(currentCharacter));
        deleteButton?.onClick.AddListener(() => OnCharacterDeleted?.Invoke(currentCharacter));
        editButton?.onClick.AddListener(() => OnEditCharacter?.Invoke(currentCharacter));
    }
}