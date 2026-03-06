using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


namespace InventorySystem.UI.Dialogs
{

    public class LeaveGroupDialog : MonoBehaviour
    {
        [SerializeField] private GameObject dialogPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Transform itemListContainer;
        [SerializeField] private GameObject itemRowPrefab; // Simple text prefab
        [SerializeField] private Button returnToGroupButton;
        [SerializeField] private Button keepItemsButton;
        [SerializeField] private Button cancelButton;

        public Action<bool> OnChoiceMade; // true = keep, false = return
        public Action OnCancelled;

        void Awake()
        {
            returnToGroupButton?.onClick.AddListener(() =>
            {
                dialogPanel.SetActive(false);
                OnChoiceMade?.Invoke(false); // return to group
            });
            keepItemsButton?.onClick.AddListener(() =>
            {
                dialogPanel.SetActive(false);
                OnChoiceMade?.Invoke(true); // keep items
            });
            cancelButton?.onClick.AddListener(() =>
            {
                dialogPanel.SetActive(false);
                OnCancelled?.Invoke();
            });
            dialogPanel?.SetActive(false);
        }

        public void ShowDialog(string characterName, List<string> itemNames)
        {
            if (titleText) titleText.text = $"{characterName} is leaving the group";

            if (messageText)
                messageText.text = $"This character has {itemNames.Count} item(s) claimed from the group. What should happen to them?";

            // Clear and populate item list
            if (itemListContainer)
            {
                foreach (Transform child in itemListContainer)
                    Destroy(child.gameObject);

                foreach (var name in itemNames)
                {
                    if (itemRowPrefab)
                    {
                        var row = Instantiate(itemRowPrefab, itemListContainer);
                        var text = row.GetComponentInChildren<TextMeshProUGUI>();
                        if (text) text.text = $"• {name}";
                    }
                }
            }

            dialogPanel?.SetActive(true);
        }
    }
}