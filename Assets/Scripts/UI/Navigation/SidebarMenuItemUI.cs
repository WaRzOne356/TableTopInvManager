using System;
using System.Drawing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySystem.UI.Navigation
{
    //UI Component for Individual Sidebar menu item.
    public class SidebarMenuItemUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI iconText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Button button;
        [SerializeField] private GameObject collapsedVersion;
        [SerializeField] private GameObject activeIndicator;
        [SerializeField] private GameObject expandedVersion;



        [Header("Sytling")]
        [SerializeField] private Color activeColor = Color.blue;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverColor = Color.lightGray;

        //state
        private bool isActive = false;
        private SidebarMenuItem menuItem;
        private Action<UIPageType> onClickCallback;

        private void Setup(SidebarMenuItem item, Action<UIPageType> clickCallback)
        {
            menuItem = item;
            onClickCallback = clickCallback;
            if (iconText != null)
                iconText.text = GetIconForPageType(item.pageType);
            if (nameText != null)
                nameText.text = item.displayName;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClickCallback?.Invoke(item.pageType));

                // Add hover listeners
                var buttonTransition = button.transition;
                if (buttonTransition == Selectable.Transition.ColorTint)
                {
                    var colors = button.colors;
                    colors.normalColor = normalColor;
                    colors.highlightedColor = hoverColor;
                    colors.selectedColor = activeColor;
                    button.colors = colors;
                }
            }

            if (!string.IsNullOrEmpty(item.description))
            {
                // Add tooltip component or logic here
                gameObject.AddComponent<UITooltip>().SetTooltip(item.description);
            }
            SetEnabled(item.isEnabled);
        }

        //set this menu item as active or inactive
        private void SetActive(bool active)
        {
            isActive = active;
            if (backgroundImage != null)
                backgroundImage.color = active ? activeColor : normalColor;
            if (activeIndicator != null)
                activeIndicator.SetActive(active);

            Color textColor = active ? Color.white : Color.gray;
            if (iconText != null)
                iconText.color = textColor;
            if (nameText != null)
                nameText.color = textColor;
        }

        //Set collpased state of this menu item
        private void SetCollapsed(bool collapsed)
        {
            if (collapsedVersion != null)
                collapsedVersion.SetActive(collapsed);
            if (expandedVersion != null)
                expandedVersion.SetActive(!collapsed);
        }

        //Set Enabled state of this menu item
        private void SetEnabled(bool enabled)
        {
            if (button != null)
                button.interactable = enabled;
            var alpha = enabled ? 1f : 0.5f;
            GetComponent<CanvasGroup>()?.SetAlpha(alpha);

        }

        //get emoji icon for a given page type
        private void GetIconForPageType(UIPageType pageType)
        {
            return pageType switch
            {
                UIPageType.UserProfile => "👤",
                UIPageType.PersonalInventory => "🎒",
                UIPageType.GroupInventory => "📦",
                UIPageType.ItemBrowser => "🔍",
                UIPageType.Statistics => "📊",
                UIPageType.Settings => "⚙️",
                _ => "❓",
            };
        }
    }

    // Simple tooltip componen
    public class UITooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private string tooltipText;
        private GameObject tooltipObject;
        public void SetTooltip(string text)
        {
            tooltipText = text;
            // Create tooltip object here or reference an existing one
            tooltipObject = new GameObject("Tooltip");
            var textComponent = tooltipObject.AddComponent<TextMeshProUGUI>();
            textComponent.text = tooltipText;
            tooltipObject.SetActive(false);
            // Positioning and styling would go here
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltipObject != null)
                tooltipObject.SetActive(true);
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltipObject != null)
                tooltipObject.SetActive(false);
        }
    }
}