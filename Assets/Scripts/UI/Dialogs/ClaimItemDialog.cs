using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;


namespace InventorySystem.UI.Dialogs
{

    /// <summary>
    /// Dialog for claiming items from group storage to a character's inventory
    /// </summary>
    public class ClaimItemDialog : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject dialogPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Image itemIconImage;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemDescriptionText;
        [SerializeField] private TextMeshProUGUI availableQuantityText;

        [Header("Claim Controls")]
        [SerializeField] private TMP_Dropdown characterDropdown;
        [SerializeField] private Slider quantitySlider;
        [SerializeField] private TMP_InputField quantityInputField;
        [SerializeField] private TextMeshProUGUI quantityDisplayText;
        [SerializeField] private Button claimButton;
        [SerializeField] private Button claimAllButton;
        [SerializeField] private Button cancelButton;

        [Header("Ownership Display")]
        [SerializeField] private Transform ownershipListContainer;
        [SerializeField] private GameObject ownershipEntryPrefab;
        [SerializeField] private TextMeshProUGUI currentOwnershipText;

        [Header("Validation")]
        [SerializeField] private TextMeshProUGUI errorText;
        [SerializeField] private Color validColor = Color.green;
        [SerializeField] private Color invalidColor = Color.red;

        // Events
        public Action<string, string, int> OnItemClaimed; // itemId, characterId, quantity

        // State
        private InventoryItem currentItem;
        private List<PlayerCharacter> availableCharacters = new List<PlayerCharacter>();
        private int maxAvailableQuantity;
        private int selectedQuantity = 1;
        private List<GameObject> spawnedOwnershipEntries = new List<GameObject>();

        private void Awake()
        {
            SetupEventHandlers();

            if (dialogPanel != null)
                dialogPanel.SetActive(false);
        }

        private void SetupEventHandlers()
        {
            claimButton?.onClick.AddListener(OnClaimClicked);
            claimAllButton?.onClick.AddListener(OnClaimAllClicked);
            cancelButton?.onClick.AddListener(CloseDialog);

            characterDropdown?.onValueChanged.AddListener(OnCharacterSelectionChanged);
            quantitySlider?.onValueChanged.AddListener(OnQuantitySliderChanged);
            quantityInputField?.onValueChanged.AddListener(OnQuantityInputChanged);
            quantityInputField?.onEndEdit.AddListener(OnQuantityInputEndEdit);
        }

        // =====================================================================
        // PUBLIC API
        // =====================================================================

        public void ShowDialog(InventoryItem item, List<PlayerCharacter> characters, int availableQuantity)
        {
            if (item == null || characters == null || characters.Count == 0)
            {
                Debug.LogError("[ClaimItemDialog] Invalid parameters");
                return;
            }

            currentItem = item;
            availableCharacters = characters;
            maxAvailableQuantity = availableQuantity;
            selectedQuantity = Mathf.Min(1, maxAvailableQuantity);

            PopulateDialog();

            if (dialogPanel != null)
                dialogPanel.SetActive(true);

            ValidateSelection();
        }

        // =====================================================================
        // DIALOG POPULATION
        // =====================================================================

        private void PopulateDialog()
        {
            // Title
            if (titleText != null)
                titleText.text = "Claim Item for Character";

            // Item info
            if (itemNameText != null)
                itemNameText.text = currentItem.itemName;

            if (itemDescriptionText != null)
                itemDescriptionText.text = string.IsNullOrEmpty(currentItem.description)
                    ? "No description available"
                    : currentItem.description;

            if (availableQuantityText != null)
                availableQuantityText.text = $"Available in Party Storage: {maxAvailableQuantity}";

            // Item icon (if you have thumbnail support)
            if (itemIconImage != null)
            {
                // TODO: Load thumbnail from currentItem.thumbnailUrl
                // For now, use a placeholder or default sprite
            }

            // Character dropdown
            PopulateCharacterDropdown();

            // Quantity controls
            SetupQuantityControls();

            // Show current ownership
            DisplayCurrentOwnership();
        }

        private void PopulateCharacterDropdown()
        {
            if (characterDropdown == null) return;

            characterDropdown.ClearOptions();

            var options = availableCharacters.Select(c =>
                $"{c.characterName} (Lvl {c.level} {c.characterClass})").ToList();

            characterDropdown.AddOptions(options);

            // Try to select active character
            int activeIndex = availableCharacters.FindIndex(c => c.isActive);
            if (activeIndex >= 0)
                characterDropdown.value = activeIndex;
            else
                characterDropdown.value = 0;
        }

        private void SetupQuantityControls()
        {
            if (quantitySlider != null)
            {
                quantitySlider.minValue = 1;
                quantitySlider.maxValue = maxAvailableQuantity;
                quantitySlider.wholeNumbers = true;
                quantitySlider.value = selectedQuantity;
            }

            if (quantityInputField != null)
            {
                quantityInputField.text = selectedQuantity.ToString();
            }

            UpdateQuantityDisplay();
        }

        private void DisplayCurrentOwnership()
        {
            ClearOwnershipEntries();

            if (ownershipListContainer == null || ownershipEntryPrefab == null)
            {
                // Fallback to text display
                if (currentOwnershipText != null)
                {
                    var ownership = GetCurrentOwnershipSummary();
                    currentOwnershipText.text = string.IsNullOrEmpty(ownership)
                        ? "Currently unclaimed"
                        : $"Current owners: {ownership}";
                }
                return;
            }

            // Get ownership from InventoryManager
            var inventoryManager = InventoryManager.Instance;
            if (inventoryManager == null) return;

            var ownerships = inventoryManager.GetOwnershipForItem(currentItem.itemId);

            if (ownerships.Count == 0)
            {
                if (currentOwnershipText != null)
                    currentOwnershipText.text = "No one has claimed this item yet";
                return;
            }

            if (currentOwnershipText != null)
                currentOwnershipText.text = "Currently owned by:";

            // Create ownership entries
            foreach (var ownership in ownerships)
            {
                CreateOwnershipEntry(ownership);
            }
        }

        private void CreateOwnershipEntry(ItemOwnership ownership)
        {
            var entryObj = Instantiate(ownershipEntryPrefab, ownershipListContainer);
            spawnedOwnershipEntries.Add(entryObj);

            // Find character name
            var characterManager = CharacterManager.Instance;
            string characterName = "Unknown";

            if (characterManager != null)
            {
                var character = characterManager.GetCharacterById(ownership.characterId);
                if (character != null)
                    characterName = character.characterName;
            }

            // Populate entry
            var nameText = entryObj.transform.Find("CharacterNameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
                nameText.text = characterName;

            var quantityText = entryObj.transform.Find("QuantityText")?.GetComponent<TextMeshProUGUI>();
            if (quantityText != null)
                quantityText.text = $"x{ownership.quantityOwned}";
        }

        private void ClearOwnershipEntries()
        {
            foreach (var entry in spawnedOwnershipEntries)
            {
                if (entry != null)
                    Destroy(entry);
            }
            spawnedOwnershipEntries.Clear();
        }

        private string GetCurrentOwnershipSummary()
        {
            var inventoryManager = InventoryManager.Instance;
            if (inventoryManager == null) return "";

            var ownerships = inventoryManager.GetOwnershipForItem(currentItem.itemId);
            if (ownerships.Count == 0) return "";

            var characterManager = CharacterManager.Instance;
            if (characterManager == null) return "";

            var ownerList = new List<string>();
            foreach (var ownership in ownerships)
            {
                var character = characterManager.GetCharacterById(ownership.characterId);
                string characterName = character?.characterName ?? "Unknown";
                ownerList.Add($"{characterName} ({ownership.quantityOwned})");
            }

            return string.Join(", ", ownerList);
        }

        // =====================================================================
        // EVENT HANDLERS
        // =====================================================================

        private void OnCharacterSelectionChanged(int index)
        {
            ValidateSelection();
        }

        private void OnQuantitySliderChanged(float value)
        {
            selectedQuantity = (int)value;

            if (quantityInputField != null)
                quantityInputField.text = selectedQuantity.ToString();

            UpdateQuantityDisplay();
            ValidateSelection();
        }

        private void OnQuantityInputChanged(string input)
        {
            if (int.TryParse(input, out int value))
            {
                selectedQuantity = Mathf.Clamp(value, 1, maxAvailableQuantity);

                if (quantitySlider != null)
                    quantitySlider.value = selectedQuantity;

                UpdateQuantityDisplay();
            }
        }

        private void OnQuantityInputEndEdit(string input)
        {
            // Ensure valid input on end edit
            if (!int.TryParse(input, out int value) || value < 1 || value > maxAvailableQuantity)
            {
                selectedQuantity = Mathf.Clamp(selectedQuantity, 1, maxAvailableQuantity);

                if (quantityInputField != null)
                    quantityInputField.text = selectedQuantity.ToString();
            }

            ValidateSelection();
        }

        private void OnClaimClicked()
        {
            if (!ValidateSelection())
            {
                ShowError("Invalid selection");
                return;
            }

            int selectedCharacterIndex = characterDropdown?.value ?? 0;
            if (selectedCharacterIndex < 0 || selectedCharacterIndex >= availableCharacters.Count)
            {
                ShowError("Invalid character selection");
                return;
            }

            var selectedCharacter = availableCharacters[selectedCharacterIndex];

            Debug.Log($"[ClaimItemDialog] Claiming {selectedQuantity}x {currentItem.itemName} for {selectedCharacter.characterName}");

            OnItemClaimed?.Invoke(currentItem.itemId, selectedCharacter.characterId, selectedQuantity);
            CloseDialog();
        }

        private void OnClaimAllClicked()
        {
            selectedQuantity = maxAvailableQuantity;

            if (quantitySlider != null)
                quantitySlider.value = selectedQuantity;

            if (quantityInputField != null)
                quantityInputField.text = selectedQuantity.ToString();

            UpdateQuantityDisplay();
            ValidateSelection();

            // Auto-claim after setting max
            OnClaimClicked();
        }

        // =====================================================================
        // VALIDATION
        // =====================================================================

        private bool ValidateSelection()
        {
            bool isValid = true;
            string errorMessage = "";

            // Check character selection
            int selectedCharacterIndex = characterDropdown?.value ?? -1;
            if (selectedCharacterIndex < 0 || selectedCharacterIndex >= availableCharacters.Count)
            {
                isValid = false;
                errorMessage = "Please select a character";
            }

            // Check quantity
            if (selectedQuantity < 1 || selectedQuantity > maxAvailableQuantity)
            {
                isValid = false;
                errorMessage = $"Quantity must be between 1 and {maxAvailableQuantity}";
            }

            // Update UI
            if (claimButton != null)
                claimButton.interactable = isValid;

            if (claimAllButton != null)
                claimAllButton.interactable = maxAvailableQuantity > 0;

            if (errorText != null)
            {
                if (isValid)
                {
                    errorText.text = "";
                    errorText.gameObject.SetActive(false);
                }
                else
                {
                    errorText.text = errorMessage;
                    errorText.color = invalidColor;
                    errorText.gameObject.SetActive(true);
                }
            }

            return isValid;
        }

        private void ShowError(string message)
        {
            if (errorText != null)
            {
                errorText.text = message;
                errorText.color = invalidColor;
                errorText.gameObject.SetActive(true);
            }
        }

        private void UpdateQuantityDisplay()
        {
            if (quantityDisplayText != null)
            {
                quantityDisplayText.text = $"Claiming: {selectedQuantity} of {maxAvailableQuantity}";
            }
        }

        // =====================================================================
        // DIALOG CONTROL
        // =====================================================================

        private void CloseDialog()
        {
            ClearOwnershipEntries();

            if (dialogPanel != null)
                dialogPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            claimButton?.onClick.RemoveAllListeners();
            claimAllButton?.onClick.RemoveAllListeners();
            cancelButton?.onClick.RemoveAllListeners();
            characterDropdown?.onValueChanged.RemoveAllListeners();
            quantitySlider?.onValueChanged.RemoveAllListeners();
            quantityInputField?.onValueChanged.RemoveAllListeners();
            quantityInputField?.onEndEdit.RemoveAllListeners();

            ClearOwnershipEntries();
        }
    }
}