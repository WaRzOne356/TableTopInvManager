using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Reusable confirmation dialog for delete operations
/// </summary>
public class ConfirmDeleteDialog : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TextMeshProUGUI confirmButtonText;

    [Header("Visual Settings")]
    [SerializeField] private Color dangerColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private GameObject dialogPanel;

    private Action onConfirmCallback;
    private Action onCancelCallback;

    private void Awake()
    {
        confirmButton?.onClick.AddListener(OnConfirmClicked);
        cancelButton?.onClick.AddListener(OnCancelClicked);

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Show the dialog with custom title and message
    /// </summary>
    public void ShowDialog(string title, string message, Action onConfirm, Action onCancel = null)
    {
        if (titleText != null)
            titleText.text = title;

        if (messageText != null)
            messageText.text = message;

        // Set confirm button to danger color
        if (confirmButton != null)
        {
            var buttonImage = confirmButton.GetComponent<Image>();
            if (buttonImage != null)
                buttonImage.color = dangerColor;
        }

        if (confirmButtonText != null)
            confirmButtonText.text = "Delete";

        onConfirmCallback = onConfirm;
        onCancelCallback = onCancel;

        gameObject.SetActive(true);
    }

    private void OnConfirmClicked()
    {
        onConfirmCallback?.Invoke();
        CloseDialog();
    }

    private void OnCancelClicked()
    {
        onCancelCallback?.Invoke();
        CloseDialog();
    }

    private void CloseDialog()
    {
        onConfirmCallback = null;
        onCancelCallback = null;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        confirmButton?.onClick.RemoveAllListeners();
        cancelButton?.onClick.RemoveAllListeners();
    }
}