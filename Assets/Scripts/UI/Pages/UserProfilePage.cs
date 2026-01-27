using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InventorySystem.UI.Navigation;
using InventorySystem.UI.Pages;
using InventorySystem.Data;

namespace InventorySystem.UI.Pages
{
    /// <summary>
    /// User profile and account management page
    /// Shows user info, their characters, and aggregate statistics
    /// </summary>
    public class UserProfilePage : UIPage
    {
        [Header("Profile Display")]
        [SerializeField] private TMP_InputField userNameField;
        [SerializeField] private Image avatarImage;
        [SerializeField] private TextMeshProUGUI memberSinceText;
        [SerializeField] private TextMeshProUGUI userIdText;
        [SerializeField] private TextMeshProUGUI activeCharacterText;

        [Header("Profile Actions")]
        [SerializeField] private Button changeAvatarButton;
        [SerializeField] private Button saveProfileButton;
        [SerializeField] private Button resetPasswordButton;
        [SerializeField] private Button exportDataButton;

        [Header("Characters Section")]
        [SerializeField] private Transform charactersContainer;
        [SerializeField] private GameObject characterCardPrefab;
        [SerializeField] private Button createCharacterButton;
        [SerializeField] private TextMeshProUGUI noCharactersText;
        [SerializeField] private TextMeshProUGUI characterCountText;

        [Header("Statistics")]
        [SerializeField] private TextMeshProUGUI totalCharactersText;
        [SerializeField] private TextMeshProUGUI totalInventoryValueText;
        [SerializeField] private TextMeshProUGUI totalItemsOwnedText;
        [SerializeField] private TextMeshProUGUI totalWeightText;
        [SerializeField] private TextMeshProUGUI mostPlayedCharacterText;
        [SerializeField] private TextMeshProUGUI favoriteItemCategoryText;

        [Header("Dialogs")]
        [SerializeField] private CreateCharacterDialog createCharacterDialog;
        [SerializeField] private ConfirmDeleteDialog confirmDeleteDialog;
        [SerializeField] private AvatarSelectionDialog avatarSelectionDialog;

        // State
        private string currentUserId;
        private SerializableUserInfo currentUserInfo;
        private PlayerCharacter characterToDelete;
        private List<PlayerCharacter> userCharacters = new List<PlayerCharacter>();
        private List<GameObject> spawnedCharacterCards = new List<GameObject>();


        void Awake()
        {
            pageType = UIPageType.UserProfile;
            pageTitle = "User Profile";

            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            saveProfileButton?.onClick.AddListener(SaveProfile);
            changeAvatarButton?.onClick.AddListener(ChangeAvatar);
            exportDataButton?.onClick.AddListener(ExportUserData);
            createCharacterButton?.onClick.AddListener(OpenCreateCharacterDialog);
        }

        protected override void RefreshContent()
        {
            LoadUserProfile();
            LoadUserCharacters();
            LoadUserStatistics();
            RefreshCharactersDisplay();
        }

        // =====================================================================
        // USER PROFILE LOADING
        // =====================================================================

        private void LoadUserProfile()
        {
            // Get current user from UserManager
            currentUserId = GetCurrentUserId();

            var userManager = UserManager.Instance;
            if (userManager != null)
            {
                currentUserInfo = userManager.GetUserByName(GetCurrentUserName());

                if (currentUserInfo != null)
                {
                    if (userNameField != null)
                        userNameField.text = currentUserInfo.userName.ToString();

                    if (userIdText != null)
                        userIdText.text = $"User ID: {currentUserInfo.clientId}";

                    if (memberSinceText != null)
                    {
                        DateTime connectionTime = DateTime.TryParse(currentUserInfo.connectionTime, out var parsed)
                            ? parsed
                            : DateTime.Now;
                        memberSinceText.text = $"Member since: {connectionTime:MMM yyyy}";
                    }
                }
                else
                {

                    // No user info found - create default user
                    Debug.Log("No user found, creating new user");
                    currentUserInfo = new SerializableUserInfo();
                    currentUserInfo.userName = GetCurrentUserName();


                    if (userNameField != null)
                        userNameField.text = GetCurrentUserName();

                    if (memberSinceText != null)
                        memberSinceText.text = $"Member since: {DateTime.Now:MMM yyyy}";
                }
            }
            else
            {
                Debug.LogWarning("[UserProfile] UserManager not available");
            }
            LoadActiveCharacterDisplay();
            LoadUserAvatar();
        }

        private void LoadUserCharacters()
        {
            userCharacters.Clear();

            var characterManager = CharacterManager.Instance;
            if (characterManager != null)
            {
                userCharacters = characterManager.GetCharactersByUser(currentUserId).ToList();
                Debug.Log($"[UserProfile] Loaded {userCharacters.Count} characters for user {currentUserId}");
            }
            else
            {
                Debug.LogWarning("[UserProfile] CharacterManager not available");
            }
        }

        private void LoadUserAvatar()
        {
            // TODO: Implement avatar loading from file or URL
            if (avatarImage != null)
            {
                // Placeholder: Use default avatar
                // avatarImage.sprite = Resources.Load<Sprite>("Avatars/DefaultAvatar");
            }
        }

        private void LoadActiveCharacterDisplay()
        {
            if (activeCharacterText == null) return;

            var characterManager = CharacterManager.Instance;
            if (characterManager != null)
            {
                var activeChar = characterManager.GetActiveCharacterForUser(currentUserId);
                if (activeChar != null)
                {
                    activeCharacterText.text = $"Active Character: {activeChar.characterName} (Lvl {activeChar.level} {activeChar.characterClass})";
                    activeCharacterText.color = new Color(0.4f, 1f, 0.4f); // Green
                }
                else
                {
                    activeCharacterText.text = "Active Character: None";
                    activeCharacterText.color = new Color(0.7f, 0.7f, 0.7f); // Gray
                }
            }
        }

        // =====================================================================
        // CHARACTERS DISPLAY
        // =====================================================================

        private void RefreshCharactersDisplay()
        {
            ClearCharacterCards();

            if (userCharacters.Count == 0)
            {
                if (noCharactersText != null)
                {
                    noCharactersText.gameObject.SetActive(true);
                    noCharactersText.text = "No characters yet. Create your first character!";
                }

                if (characterCountText != null)
                    characterCountText.text = "Characters: 0";

                return;
            }

            if (noCharactersText != null)
                noCharactersText.gameObject.SetActive(false);

            if (characterCountText != null)
                characterCountText.text = $"Characters: {userCharacters.Count}";

            // Create character cards
            foreach (var character in userCharacters.OrderByDescending(c => c.lastPlayed))
            {
                CreateCharacterCard(character);
            }
        }

        private void CreateCharacterCard(PlayerCharacter character)
        {
            if (characterCardPrefab == null || charactersContainer == null) return;

            var cardObj = Instantiate(characterCardPrefab, charactersContainer);
            spawnedCharacterCards.Add(cardObj);

            var cardUI = cardObj.GetComponent<CharacterCardUI>();
            if (cardUI != null)
            {
                cardUI.SetupCard(character);

                // Subscribe to events
                cardUI.OnCharacterSelected += OnCharacterSelected;
                cardUI.OnCharacterDeleted += OnCharacterDeleted;
                cardUI.OnSetActiveCharacter += OnSetActiveCharacter;
            }
            else
            {
                // Fallback: manually populate card elements
                PopulateCharacterCardManually(cardObj, character);
            }
        }

        private void PopulateCharacterCardManually(GameObject cardObj, PlayerCharacter character)
        {
            // Find and populate text elements
            var nameText = cardObj.transform.Find("CharacterNameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
                nameText.text = character.characterName;

            var classText = cardObj.transform.Find("ClassText")?.GetComponent<TextMeshProUGUI>();
            if (classText != null)
                classText.text = $"Level {character.level} {character.characterClass}";

            var lastPlayedText = cardObj.transform.Find("LastPlayedText")?.GetComponent<TextMeshProUGUI>();
            if (lastPlayedText != null)
            {
                TimeSpan timeSince = DateTime.Now - character.lastPlayed;
                string timeText = timeSince.TotalDays < 1
                    ? "Today"
                    : $"{(int)timeSince.TotalDays} days ago";
                lastPlayedText.text = $"Last played: {timeText}";
            }

            // Get character's inventory stats
            var inventoryStats = GetCharacterInventoryStats(character.characterId);
            var itemCountText = cardObj.transform.Find("ItemCountText")?.GetComponent<TextMeshProUGUI>();
            if (itemCountText != null)
                itemCountText.text = $"{inventoryStats.totalItems} items";

            var valueText = cardObj.transform.Find("ValueText")?.GetComponent<TextMeshProUGUI>();
            if (valueText != null)
                valueText.text = $"{inventoryStats.totalValue:N0} gp";

            // Active indicator
            var activeIndicator = cardObj.transform.Find("ActiveIndicator")?.gameObject;
            if (activeIndicator != null)
                activeIndicator.SetActive(character.isActive);

            // Setup buttons
            var viewButton = cardObj.transform.Find("ViewInventoryButton")?.GetComponent<Button>();
            if (viewButton != null)
            {
                viewButton.onClick.RemoveAllListeners();
                viewButton.onClick.AddListener(() => ViewCharacterInventory(character));
            }

            var deleteButton = cardObj.transform.Find("DeleteButton")?.GetComponent<Button>();
            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.onClick.AddListener(() => ConfirmDeleteCharacter(character));
            }

            var setActiveButton = cardObj.transform.Find("SetActiveButton")?.GetComponent<Button>();
            if (setActiveButton != null)
            {
                setActiveButton.onClick.RemoveAllListeners();
                setActiveButton.onClick.AddListener(() => SetActiveCharacter(character));
                setActiveButton.gameObject.SetActive(!character.isActive);
            }
        }

        private void ClearCharacterCards()
        {
            foreach (var cardObj in spawnedCharacterCards)
            {
                if (cardObj != null)
                    Destroy(cardObj);
            }
            spawnedCharacterCards.Clear();
        }

        // =====================================================================
        // STATISTICS
        // =====================================================================

        private void LoadUserStatistics()
        {
            // Total characters
            if (totalCharactersText != null)
                totalCharactersText.text = $"{userCharacters.Count} Characters";

            // Calculate aggregate inventory statistics across all characters
            int totalItems = 0;
            int totalValue = 0;
            float totalWeight = 0f;
            Dictionary<ItemCategory, int> categoryCount = new Dictionary<ItemCategory, int>();

            foreach (var character in userCharacters)
            {
                var stats = GetCharacterInventoryStats(character.characterId);
                totalItems += stats.totalItems;
                totalValue += stats.totalValue;
                totalWeight += stats.totalWeight;

                foreach (var kvp in stats.categoryBreakdown)
                {
                    if (categoryCount.ContainsKey(kvp.Key))
                        categoryCount[kvp.Key] += kvp.Value;
                    else
                        categoryCount[kvp.Key] = kvp.Value;
                }
            }

            if (totalItemsOwnedText != null)
                totalItemsOwnedText.text = $"{totalItems} Items Owned";

            if (totalInventoryValueText != null)
                totalInventoryValueText.text = $"{totalValue:N0} gp Total Value";

            if (totalWeightText != null)
                totalWeightText.text = $"{totalWeight:F1} lbs Total Weight";

            // Most played character
            if (mostPlayedCharacterText != null)
            {
                var mostPlayed = userCharacters.OrderByDescending(c => c.lastPlayed).FirstOrDefault();
                mostPlayedCharacterText.text = mostPlayed != null
                    ? $"Most Played: {mostPlayed.characterName}"
                    : "Most Played: None";
            }

            // Favorite category
            if (favoriteItemCategoryText != null)
            {
                var favoriteCategory = categoryCount.OrderByDescending(kvp => kvp.Value).FirstOrDefault();
                if (favoriteCategory.Value > 0)
                {
                    string categoryName = GetCategoryDisplayName(favoriteCategory.Key);
                    favoriteItemCategoryText.text = $"Favorite Category: {categoryName}";
                }
                else
                {
                    favoriteItemCategoryText.text = "Favorite Category: None";
                }
            }
        }

        private CharacterInventoryStats GetCharacterInventoryStats(string characterId)
        {
            var stats = new CharacterInventoryStats();

            var inventoryManager = InventoryManager.Instance;
            if (inventoryManager == null) return stats;

            // Get all ownership records for this character
            var ownerships = inventoryManager.GetAllOwnerships()
                .Where(o => o.characterId == characterId)
                .ToList();

            foreach (var ownership in ownerships)
            {
                // Get the actual item
                var allItems = inventoryManager.GetCurrentInventory();
                var item = allItems.FirstOrDefault(i => i.itemId == ownership.itemId);

                if (item != null)
                {
                    stats.totalItems += ownership.quantityOwned;
                    stats.totalValue += item.valueInGold * ownership.quantityOwned;
                    stats.totalWeight += item.weight * ownership.quantityOwned;

                    if (stats.categoryBreakdown.ContainsKey(item.category))
                        stats.categoryBreakdown[item.category] += ownership.quantityOwned;
                    else
                        stats.categoryBreakdown[item.category] = ownership.quantityOwned;
                }
            }

            return stats;
        }

        private string GetCategoryDisplayName(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.MagicItem => "Magic Items",
                ItemCategory.QuestItem => "Quest Items",
                _ => category.ToString()
            };
        }

        // =====================================================================
        // CHARACTER ACTIONS
        // =====================================================================

        private void OnCharacterSelected(PlayerCharacter character)
        {
            Debug.Log($"[UserProfile] Character selected: {character.characterName}");
            ViewCharacterInventory(character);
        }

        private async void OnCharacterDeleted(PlayerCharacter character)
        {
            await DeleteCharacter(character);
        }

        private async void OnSetActiveCharacter(PlayerCharacter character)
        {
            await SetActiveCharacter(character);
        }

        private void ViewCharacterInventory(PlayerCharacter character)
        {
            Debug.Log($"[UserProfile] Viewing inventory for: {character.characterName}");

            // Store the selected character ID so PersonalInventoryPage can filter by it
            PlayerPrefs.SetString("SelectedCharacterId", character.characterId);
            PlayerPrefs.Save();

            // Navigate to personal inventory page
            NavigateTo(UIPageType.PersonalInventory);
        }

        private void ConfirmDeleteCharacter(PlayerCharacter character)
        {
            Debug.Log($"[UserProfile] Confirm delete character: {character.characterName}");

            if (confirmDeleteDialog != null)
            {
                characterToDelete = character;  // Store for callback
                confirmDeleteDialog.ShowDialog(
                    $"Delete {character.characterName}?",
                    $"Are you sure you want to delete {character.characterName}? This will remove the character and return all their items to party storage. This action cannot be undone.",
                    onConfirm: () => _ = DeleteCharacter(character),
                    onCancel: () => characterToDelete = null
                );
            }
            else
            {
                Debug.LogWarning("[UserProfile] ConfirmDeleteDialog not assigned");
                // Direct delete without confirmation (for now)
                _ = DeleteCharacter(character);
            }
        }

        private async Task DeleteCharacter(PlayerCharacter character)
        {
            Debug.Log($"[UserProfile] Deleting character: {character.characterName}");

            var characterManager = CharacterManager.Instance;
            if (characterManager != null)
            {
                await characterManager.RemoveCharacterAsync(character.characterId);

                // Also remove all ownership records for this character
                var inventoryManager = InventoryManager.Instance;
                if (inventoryManager != null)
                {
                    var ownerships = inventoryManager.GetAllOwnerships()
                        .Where(o => o.characterId == character.characterId)
                        .ToList();

                    foreach (var ownership in ownerships)
                    {
                        await inventoryManager.UpdateOwnershipAsync(ownership.itemId, character.characterId, 0);
                    }
                }

                ShowMessage($"Deleted character: {character.characterName}", MessageType.Success);
                RefreshContent();
            }
        }

        private async Task SetActiveCharacter(PlayerCharacter character)
        {
            Debug.Log($"[UserProfile] Setting active character: {character.characterName}");

            var characterManager = CharacterManager.Instance;
            if (characterManager != null)
            {
                await characterManager.SetActiveCharacterAsync(currentUserId, character.characterId);

                ShowMessage($"Active character: {character.characterName}", MessageType.Success);
                RefreshCharactersDisplay();
            }
        }

        private void OpenCreateCharacterDialog()
        {
            if (createCharacterDialog != null)
            {
                createCharacterDialog.ShowDialog(currentUserId);
                createCharacterDialog.OnCharacterCreated += OnCharacterCreated;
            }
            else
            {
                Debug.LogWarning("[UserProfile] CreateCharacterDialog not assigned");
                ShowMessage("Create character dialog not available", MessageType.Error);
            }
        }

        private async void OnCharacterCreated(PlayerCharacter newCharacter)
        {
            Debug.Log($"[UserProfile] New character created: {newCharacter.characterName}");

            var characterManager = CharacterManager.Instance;
            if (characterManager != null)
            {
                await characterManager.AddCharacterAsync(newCharacter);
                ShowMessage($"Created character: {newCharacter.characterName}", MessageType.Success);
                RefreshContent();
            }
        }

        // =====================================================================
        // PROFILE ACTIONS
        // =====================================================================

        private async void SaveProfile()
        {
            string newUserName = userNameField?.text ?? "";

            if (string.IsNullOrWhiteSpace(newUserName))
            {
                ShowMessage("User name cannot be empty", MessageType.Error);
                return;
            }

            // Update in UserManager
            var userManager = UserManager.Instance;
            if (userManager != null && currentUserInfo.userName != null)
            {
                currentUserInfo.userName = newUserName;
                await userManager.AddUserAsync(currentUserInfo); // This updates existing user
            }

            // Save locally
            PlayerPrefs.SetString("UserName", newUserName);
            PlayerPrefs.Save();

            ShowMessage("Profile saved successfully!", MessageType.Success);
            Debug.Log($"[UserProfile] Saved profile: {newUserName}");
        }

        private void ChangeAvatar()
        {
            if (avatarSelectionDialog != null)
            {
                avatarSelectionDialog.ShowDialog(
                    currentAvatarSprite: avatarImage?.sprite,
                    onAvatarSelected: OnAvatarSelected
                );
            }
            else
            {
                Debug.LogWarning("[UserProfile] AvatarSelectionDialog not assigned");
                ShowMessage("Avatar selection dialog not available", MessageType.Error);
            }
        }

        private void OnAvatarSelected(Sprite selectedAvatar)
        {
            if (avatarImage != null)
            {
                avatarImage.sprite = selectedAvatar;

                // Save avatar preference
                // TODO: Implement proper avatar persistence
                PlayerPrefs.SetString("UserAvatarPath", selectedAvatar.name);
                PlayerPrefs.Save();

                ShowMessage("Avatar updated!", MessageType.Success);
            }
        }

        private async void ExportUserData()
        {
            ShowMessage("Exporting user data...", MessageType.Info);

            try
            {
                var exportData = new
                {
                    userId = currentUserId,
                    userName = userNameField?.text ?? "Unknown User",
                    exportDate = DateTime.Now.ToString("O"),

                    characters = userCharacters.Select(c => new
                    {
                        characterId = c.characterId,
                        name = c.characterName,
                        class_ = c.characterClass,
                        level = c.level,
                        dateCreated = c.dateCreated.ToString("O"),
                        lastPlayed = c.lastPlayed.ToString("O"),
                        isActive = c.isActive,
                        inventory = GetCharacterInventoryForExport(c.characterId)
                    }).ToList()
                };

                string jsonData = JsonUtility.ToJson(exportData, true);
                string fileName = $"UserProfile_Export_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);

                await System.IO.File.WriteAllTextAsync(filePath, jsonData);

                ShowMessage($"Data exported to: {filePath}", MessageType.Success);
                Debug.Log($"[UserProfile] Exported data to: {filePath}");

                // Optionally open the folder
#if UNITY_EDITOR || UNITY_STANDALONE
                Application.OpenURL("file://" + Application.persistentDataPath);
#endif
            }
            catch (Exception e)
            {
                ShowMessage($"Export error: {e.Message}", MessageType.Error);
                Debug.LogError($"[UserProfile] Export failed: {e}");
            }
        }

        private object GetCharacterInventoryForExport(string characterId)
        {
            var inventoryManager = InventoryManager.Instance;
            if (inventoryManager == null) return null;

            var ownerships = inventoryManager.GetAllOwnerships()
                .Where(o => o.characterId == characterId)
                .ToList();

            var allItems = inventoryManager.GetCurrentInventory();

            return ownerships.Select(ownership =>
            {
                var item = allItems.FirstOrDefault(i => i.itemId == ownership.itemId);
                if (item == null) return null;

                return new
                {
                    itemName = item.itemName,
                    category = item.category.ToString(),
                    quantity = ownership.quantityOwned,
                    weight = item.weight,
                    value = item.valueInGold,
                    totalValue = item.valueInGold * ownership.quantityOwned
                };
            }).Where(x => x != null).ToList();
        }

        // =====================================================================
        // UTILITIES
        // =====================================================================

        private string GetCurrentUserId()
        {
            // Get from PlayerPrefs or generate new one
            string userId = PlayerPrefs.GetString("UserId", "");

            if (string.IsNullOrEmpty(userId))
            {
                userId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString("UserId", userId);
                PlayerPrefs.Save();
            }

            return userId;
        }

        private string GetCurrentUserName()
        {
            return PlayerPrefs.GetString("UserName", Environment.UserName ?? "Player");
        }

        protected override void ShowMessage(string message, MessageType type)
        {
            // TODO: Show actual UI toast/notification
            base.ShowMessage(message, type);
        }

        private void OnDestroy()
        {
            // Cleanup
            ClearCharacterCards();

            if (createCharacterDialog != null)
                createCharacterDialog.OnCharacterCreated -= OnCharacterCreated;
        }

        // =====================================================================
        // HELPER CLASSES
        // =====================================================================

        private class CharacterInventoryStats
        {
            public int totalItems = 0;
            public int totalValue = 0;
            public float totalWeight = 0f;
            public Dictionary<ItemCategory, int> categoryBreakdown = new Dictionary<ItemCategory, int>();
        }
    }
}