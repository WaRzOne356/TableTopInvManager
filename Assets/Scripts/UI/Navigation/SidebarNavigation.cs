using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Web;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySystem.UI.Navigation
{
    /// <summary>
    /// Sidebar navigation component with menu items
    /// </summary>
    public class SidebarNavigation : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform menuContainer;
        [SerializeField] private GameObject menuItemPrefab;
        [SerializeField] private Button collapseButton;
        [SerializeField] private GameObject collapsedState;
        [SerializeField] private GameObject expandedState;

        [Header("User Info")]
        [SerializeField] private TextMeshProUGUI userNameText;
        [SerializeField] private TextMeshProUGUI connectionStatusText;
        [SerializeField] private Image userAvatarImage;

        [Header("Menu Configuration")]
        [SerializeField] private List<SidebarMenuItem> menuItems;
        [SerializeField] private bool startCollapsed = false;

        [Header("Styling")]
        [SerializeField] private UnityEngine.Color activeItemColor = UnityEngine.Color.blue;
        [SerializeField] private UnityEngine.Color inactiveItemColor = UnityEngine.Color.gray;
        [SerializeField] private UnityEngine.Color hoverItemColor = UnityEngine.Color.lightGray;

        // Events
        public event Action<UIPageType> OnNavigationRequested;

        // State
        private bool isCollapsed;
        private UIPageType activePageType;
        private Dictionary<UIPageType, SidebarMenuItemUI> menuItemsUI;

        void Awake()
        {
            menuItemsUI = new Dictionary<UIPageType, SidebarMenuItemUI>();
            SetupMenuItems();
            SetupEventHandlers();
        }

        void Start()
        {
            SetCollapsed(startCollapsed, false);
            UpdateUserInfo();
            UpdateConnectionStatus();
        }

        private void SetupMenuItems()
        {
            if (menuContainer == null || menuItemPrefab == null) return;

            foreach (var menuItem in menuItems)
            {
                CreateMenuItemUI(menuItem);
            }

            UnityEngine.Debug.Log($"[Sidebar] Created {menuItemsUI.Count} menu items");
        }

        private void CreateMenuItemUI(SidebarMenuItem menuItem)
        {
            var itemObj = Instantiate(menuItemPrefab, menuContainer);
            var itemUI = itemObj.GetComponent<SidebarMenuItemUI>();

            if (itemUI != null)
            {
                itemUI.Setup(menuItem, OnMenuItemClicked);
                menuItemsUI[menuItem.pageType] = itemUI;
            }
        }

        private void SetupEventHandlers()
        {
            collapseButton?.onClick.AddListener(ToggleCollapse);
        }

        private void OnMenuItemClicked(UIPageType pageType)
        {
            UnityEngine.Debug.Log($"[Sidebar] Menu item clicked: {pageType}");
            OnNavigationRequested?.Invoke(pageType);
        }

        /// <summary>
        /// Set the active page type and update visual state
        /// </summary>
        public void SetActivePageType(UIPageType pageType)
        {
            var previousActive = activePageType;
            activePageType = pageType;

            // Update menu item states
            foreach (var kvp in menuItemsUI)
            {
                kvp.Value.SetActive(kvp.Key == pageType);
            }

            UnityEngine.Debug.Log($"[Sidebar] Active page: {previousActive} ? {pageType}");
        }

        /// <summary>
        /// Toggle sidebar collapsed state
        /// </summary>
        public void ToggleCollapse()
        {
            SetCollapsed(!isCollapsed, true);
        }

        /// <summary>
        /// Set sidebar collapsed state
        /// </summary>
        public void SetCollapsed(bool collapsed, bool animate = true)
        {
            isCollapsed = collapsed;

            if (animate)
            {
                // Animate collapse/expand
                AnimateCollapse(collapsed);
            }
            else
            {
                // Immediate state change
                expandedState?.SetActive(!collapsed);
                collapsedState?.SetActive(collapsed);
            }

            // Update menu items for collapsed state
            foreach (var menuItemUI in menuItemsUI.Values)
            {
                menuItemUI.SetCollapsed(collapsed);
            }

            UnityEngine.Debug.Log($"[Sidebar] Collapsed: {collapsed}");
        }

        private void AnimateCollapse(bool collapse)
        {
            // Simple animation using LeanTween or Unity Animation
            // For now, just immediate switch
            expandedState?.SetActive(!collapse);
            collapsedState?.SetActive(collapse);
        }

        /// <summary>
        /// Update user info display
        /// </summary>
        private void UpdateUserInfo()
        {
            if (userNameText != null)
            {
                string userName = GetCurrentUserName();
                userNameText.text = userName;
            }

            // Update avatar if available
            if (userAvatarImage != null)
            {
                // Set default avatar or load user avatar
                // userAvatarImage.sprite = GetUserAvatar();
            }
        }

        /// <summary>
        /// Update connection status display
        /// </summary>
        private void UpdateConnectionStatus()
        {
            if (connectionStatusText != null)
            {
                if (NetworkInventoryManager.Instance != null && NetworkInventoryManager.Instance.IsConnected())
                {
                    connectionStatusText.text = "?? Online";
                    connectionStatusText.color = UnityEngine.Color.green;
                }
                else
                {
                    connectionStatusText.text = "?? Offline";
                    connectionStatusText.color = UnityEngine.Color.red;
                }
            }
        }

        private string GetCurrentUserName()
        {
            // Try to get from network manager
            if (NetworkInventoryManager.Instance != null)
            {
                // You'd implement this based on your user system
                return "Player"; // Placeholder
            }

            // Fallback to system username
            return System.Environment.UserName ?? "Guest";
        }

        // Update connection status periodically
        void Update()
        {
            // Update every few seconds
            if (Time.time % 5f < Time.deltaTime)
            {
                UpdateConnectionStatus();
            }
        }
    }

    /// <summary>
    /// Configuration for a sidebar menu item
    /// </summary>
    [System.Serializable]
    public class SidebarMenuItem
    {
        public UIPageType pageType;
        public string displayName;
        public string iconName;        // Could be emoji or icon identifier
        public string description;     // Tooltip text
        public bool isEnabled = true;
        public int sortOrder = 0;

        public SidebarMenuItem(UIPageType type, string name, string icon, string desc = "")
        {
            pageType = type;
            displayName = name;
            iconName = icon;
            description = desc;
        }
    }
}