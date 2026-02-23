using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InventorySystem.UI.Dialogs
{
    /// <summary>
    /// Dialog for transferring items between personal and group inventory.
    /// Allows user to select quantity and direction of transfer.
    /// </summary>
    public class TransferItemDialog : MonoBehaviour
    {
        [Header("Dialog UI")]
        [SerializeField] private GameObject dialogPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI messageText;

        [Header("Item Info Display")]
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI currentQuantityText;
        [SerializeField] private Image itemThumbnail;

        [Header("Transfer Controls")]
        [SerializeField] private TMP_InputField quantityInput;
        [SerializeField] private Slider quantitySlider;
        [SerializeField] private Button maxButton;
        [SerializeField] private TextMeshProUGUI availableQuantityText;

        [Header("Direction Display")]
        [SerializeField] private TextMeshProUGUI sourceText;
        [SerializeField] private TextMeshProUGUI destinationText;
        [SerializeField] private Image directionArrow;

        [Header("Buttons")]
        [SerializeField] private Button transferButton;
        [SerializeField] private Button cancelButton;

        [Header("Colors")]
        [SerializeField] private Color personalColor = new Color(0.3f, 0.6f, 1f);
        [SerializeField] private Color groupColor = new Color(1f, 0.6f, 0.3f);

        // Events
        public Action<TransferRequest> OnTransferConfirmed;
        public Action OnDialogClosed;

        // State
        private InventoryItem currentItem;
        private string characterId;
        private int maxQuantity;
        private TransferDirection transferDirection;

        /// <summary>
        /// Direction of transfer
        /// </summary>
        public enum TransferDirection
        {
            PersonalToGroup,
            GroupToPersonal
        }

        /// <summary>
        /// Transfer request data
        /// </summary>
        public struct TransferRequest
        {
            public string itemId;
            public string characterId;
            public int quantity;
            public TransferDirection direction;
        }

        void Awake()
        {
            SetupEventHandlers();

            // Start hidden
            if (dialogPanel != null)
                dialogPanel.SetActive(false);
        }

        private void SetupEventHandlers()
        {
            transferButton?.onClick.AddListener(OnTransferClicked);
            cancelButton?.onClick.AddListener(CloseDialog);
            maxButton?.onClick.AddListener(SetMaxQuantity);

            quantityInput?.onValueChanged.AddListener(OnQuantityInputChanged);
            quantitySlider?.onValueChanged.AddListener(OnQuantitySliderChanged);
        }

        /// <summary>
        /// Show the transfer dialog
        /// </summary>
        /// <param name="item">Item to transfer</param>
        /// <param name="charId">Character ID performing the transfer</param>
        /// <param name="availableQty">Maximum quantity available to transfer</param>
        /// <param name="direction">Direction of transfer (personal<->group)</param>
        public void ShowDialog(InventoryItem item, string charId, int availableQty, TransferDirection direction)
        {
            if (item == null)
            {
                Debug.LogError("[TransferItemDialog] Cannot show dialog - item is null");
                return;
            }

            currentItem = item;
            characterId = charId;
            maxQuantity = availableQty;
            transferDirection = direction;

            UpdateDialogContent();

            if (dialogPanel != null)
                dialogPanel.SetActive(true);
        }

        private void UpdateDialogContent()
        {
            if (currentItem == null) return;

            // Title
            if (titleText != null)
            {
                titleText.text = transferDirection == TransferDirection.PersonalToGroup
                    ? "Transfer to Group Inventory"
                    : "Transfer to Personal Storage";
            }

            // Item name
            if (itemNameText != null)
                itemNameText.text = currentItem.itemName;

            // Current quantity
            if (currentQuantityText != null)
                currentQuantityText.text = $"Available: {maxQuantity}";

            // Source/Destination with colors
            UpdateSourceDestination();

            // Quantity controls
            SetupQuantityControls();

            // Message
            UpdateMessage();

            // Thumbnail (optional - might be null)
            UpdateThumbnail();
        }

        private void UpdateSourceDestination()
        {
            if (sourceText != null && destinationText != null)
            {
                if (transferDirection == TransferDirection.PersonalToGroup)
                {
                    sourceText.text = "From: Personal Inventory";
                    sourceText.color = personalColor;

                    destinationText.text = "To: Group Inventory";
                    destinationText.color = groupColor;
                }
                else
                {
                    sourceText.text = "From: Group Inventory";
                    sourceText.color = groupColor;

                    destinationText.text = "To: Personal Storage";
                    destinationText.color = personalColor;
                }
            }
        }

        private void SetupQuantityControls()
        {
            // Slider
            if (quantitySlider != null)
            {
                quantitySlider.minValue = 1;
                quantitySlider.maxValue = Mathf.Max(1, maxQuantity);
                quantitySlider.value = Mathf.Min(1, maxQuantity);
                quantitySlider.wholeNumbers = true;
            }

            // Input field
            if (quantityInput != null)
            {
                quantityInput.text = "1";
                quantityInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            }

            // Available text
            if (availableQuantityText != null)
                availableQuantityText.text = $"Max: {maxQuantity}";

            // Max button
            if (maxButton != null)
                maxButton.interactable = maxQuantity > 1;
        }

        private void UpdateMessage()
        {
            if (messageText == null) return;

            string message = transferDirection == TransferDirection.PersonalToGroup
                ? "⚠️ This item will be permanently moved to the group inventory. You will still own it, but it will be part of the group's shared pool."
                : "⚠️ This item will be permanently removed from the group and moved to your personal storage. Other party members will no longer have access to it.";

            messageText.text = message;
        }

        private void UpdateThumbnail()
        {
            if (itemThumbnail == null) return;

            // If you have thumbnail URLs, load them here
            // For now, just disable if no thumbnail
            if (string.IsNullOrEmpty(currentItem.thumbnailUrl))
            {
                itemThumbnail.gameObject.SetActive(false);
            }
            else
            {
                // TODO: Load thumbnail from URL
                itemThumbnail.gameObject.SetActive(true);
            }
        }

        private void OnQuantityInputChanged(string value)
        {
            if (int.TryParse(value, out int qty))
            {
                qty = Mathf.Clamp(qty, 1, maxQuantity);

                if (quantitySlider != null)
                    quantitySlider.value = qty;

                // Update the input field if it was clamped
                if (quantityInput != null && qty.ToString() != value)
                    quantityInput.text = qty.ToString();
            }
        }

        private void OnQuantitySliderChanged(float value)
        {
            int qty = Mathf.RoundToInt(value);

            if (quantityInput != null)
                quantityInput.text = qty.ToString();
        }

        private void SetMaxQuantity()
        {
            if (quantityInput != null)
                quantityInput.text = maxQuantity.ToString();

            if (quantitySlider != null)
                quantitySlider.value = maxQuantity;
        }

        private void OnTransferClicked()
        {
            if (!ValidateTransfer(out string errorMsg))
            {
                ShowError(errorMsg);
                return;
            }

            int quantity = GetSelectedQuantity();

            var request = new TransferRequest
            {
                itemId = currentItem.itemId,
                characterId = characterId,
                quantity = quantity,
                direction = transferDirection
            };

            Debug.Log($"[TransferItemDialog] Confirming transfer: {quantity}x {currentItem.itemName} " +
                     $"({transferDirection})");

            OnTransferConfirmed?.Invoke(request);
            CloseDialog();
        }

        private bool ValidateTransfer(out string errorMessage)
        {
            errorMessage = "";

            if (currentItem == null)
            {
                errorMessage = "No item selected";
                return false;
            }

            int qty = GetSelectedQuantity();
            if (qty <= 0)
            {
                errorMessage = "Quantity must be greater than 0";
                return false;
            }

            if (qty > maxQuantity)
            {
                errorMessage = $"Cannot transfer more than {maxQuantity}";
                return false;
            }

            return true;
        }

        private int GetSelectedQuantity()
        {
            if (quantityInput != null && int.TryParse(quantityInput.text, out int qty))
                return Mathf.Clamp(qty, 1, maxQuantity);

            return 1;
        }

        private void ShowError(string message)
        {
            if (messageText != null)
            {
                messageText.text = $"⚠️ {message}";
                messageText.color = Color.red;
            }

            Debug.LogWarning($"[TransferItemDialog] Validation error: {message}");
        }

        public void CloseDialog()
        {
            if (dialogPanel != null)
                dialogPanel.SetActive(false);

            OnDialogClosed?.Invoke();

            // Reset state
            currentItem = null;
            characterId = null;
            maxQuantity = 0;

            // Reset message color
            if (messageText != null)
                messageText.color = Color.white;

            Debug.Log("[TransferItemDialog] Dialog closed");
        }

        void OnDestroy()
        {
            transferButton?.onClick.RemoveAllListeners();
            cancelButton?.onClick.RemoveAllListeners();
            maxButton?.onClick.RemoveAllListeners();
            quantityInput?.onValueChanged.RemoveAllListeners();
            quantitySlider?.onValueChanged.RemoveAllListeners();
        }
    }
}