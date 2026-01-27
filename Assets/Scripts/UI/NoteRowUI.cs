using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI component for displaying player notes in ItemCard expanded view
/// </summary>
public class NoteRowUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI dateText;
    [SerializeField] private TextMeshProUGUI noteText;
    [SerializeField] private Button removeButton;

    [Header("Visual")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color ownNoteColor = new Color(0.2f, 0.3f, 0.4f, 1f);

    // Events
    public System.Action OnRemoveRequested;

    // State
    private PlayerNote currentNote;
    private bool isReadOnly = false;

    private void Awake()
    {
        SetupEventHandlers();
    }

    private void SetupEventHandlers()
    {
        if (removeButton != null)
            removeButton.onClick.AddListener(RequestRemove);
    }

    /// <summary>
    /// Set the note data to display
    /// </summary>
    public void SetNote(PlayerNote note)
    {
        currentNote = note;

        if (playerNameText != null)
            playerNameText.text = note.playerName;

        if (dateText != null)
            dateText.text = note.dateAdded.ToString("MM/dd/yyyy HH:mm");

        if (noteText != null)
            noteText.text = note.note;

        // Highlight if this is the current player's note
        UpdateBackgroundColor();
    }

    /// <summary>
    /// Make the note read-only (hide remove button)
    /// </summary>
    public void SetReadOnly(bool readOnly)
    {
        isReadOnly = readOnly;

        if (removeButton != null)
            removeButton.gameObject.SetActive(!readOnly);
    }

    /// <summary>
    /// Check if this note belongs to the current player
    /// </summary>
    public bool IsOwnNote()
    {
        if (currentNote == null) return false;

        string currentPlayer = PlayerPrefs.GetString("UserName", "Player");
        return currentNote.playerName == currentPlayer;
    }

    private void UpdateBackgroundColor()
    {
        if (backgroundImage == null) return;

        // Highlight own notes with different color
        backgroundImage.color = IsOwnNote() ? ownNoteColor : normalColor;
    }

    private void RequestRemove()
    {
        // Only allow removing your own notes
        if (IsOwnNote() || !isReadOnly)
        {
            OnRemoveRequested?.Invoke();
        }
        else
        {
            Debug.LogWarning("[NoteRowUI] Cannot remove another player's note");
        }
    }

    private void OnDestroy()
    {
        if (removeButton != null)
            removeButton.onClick.RemoveListener(RequestRemove);
    }
}