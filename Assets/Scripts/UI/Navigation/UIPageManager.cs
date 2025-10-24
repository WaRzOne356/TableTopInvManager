using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem.UI.Navigation
{
    /// <summary>
    /// Manages navigation between different UI pages/views
    /// </summary>
    public class UIPageManager : MonoBehaviour
    {
        [Header("Page Configuration")]
        [SerializeField] private List<UIPage> pages;
        [SerializeField] private UIPageType defaultPage = UIPageType.GroupInventory;
        [SerializeField] private bool rememberLastPage = true;

        [Header("Navigation")]
        [SerializeField] private SidebarNavigation sidebar;
        [SerializeField] private Transform pageContainer;

        [Header("Transitions")]
        [SerializeField] private bool enableTransitions = true;
        [SerializeField] private float transitionDuration = 0.3f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // Events
        public static event Action<UIPageType, UIPageType> OnPageChanged; // old, new

        // State
        private UIPageType currentPageType;
        private UIPage currentPage;
        private Dictionary<UIPageType, UIPage> pageMap;
        private Coroutine transitionCoroutine;

        // Singleton
        public static UIPageManager Instance { get; private set; }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializePages();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            SetupNavigation();
            NavigateToDefaultPage();
        }

        private void InitializePages()
        {
            pageMap = new Dictionary<UIPageType, UIPage>();

            foreach (var page in pages)
            {
                if (page != null)
                {
                    pageMap[page.PageType] = page;
                    page.gameObject.SetActive(false);
                    page.Initialize();
                }
            }

            Debug.Log($"[UIPageManager] Initialized {pageMap.Count} pages");
        }

        private void SetupNavigation()
        {
            if (sidebar != null)
            {
                sidebar.OnNavigationRequested += NavigateToPage;
                sidebar.SetActivePageType(defaultPage);
            }
        }

        private void NavigateToDefaultPage()
        {
            UIPageType targetPage = defaultPage;

            // Try to restore last page if enabled
            if (rememberLastPage)
            {
                string lastPageKey = "LastUIPage";
                if (PlayerPrefs.HasKey(lastPageKey))
                {
                    string lastPageStr = PlayerPrefs.GetString(lastPageKey);
                    if (Enum.TryParse(lastPageStr, out UIPageType lastPage))
                    {
                        targetPage = lastPage;
                    }
                }
            }

            NavigateToPage(targetPage);
        }

        /// <summary>
        /// Navigate to specified page
        /// </summary>
        public void NavigateToPage(UIPageType pageType)
        {
            if (currentPageType == pageType)
            {
                Debug.Log($"[UIPageManager] Already on page: {pageType}");
                return;
            }

            if (!pageMap.ContainsKey(pageType))
            {
                Debug.LogError($"[UIPageManager] Page not found: {pageType}");
                return;
            }

            var previousPageType = currentPageType;
            var previousPage = currentPage;
            var newPage = pageMap[pageType];

            Debug.Log($"[UIPageManager] Navigating: {previousPageType} → {pageType}");

            // Stop any ongoing transition
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            // Page transition
            if (enableTransitions && previousPage != null)
            {
                transitionCoroutine = StartCoroutine(TransitionBetweenPages(previousPage, newPage, pageType, previousPageType));
            }
            else
            {
                CompletePageSwitch(previousPage, newPage, pageType, previousPageType);
            }

            // Update sidebar
            if (sidebar != null)
                sidebar.SetActivePageType(pageType);

            // Remember page
            if (rememberLastPage)
                PlayerPrefs.SetString("LastUIPage", pageType.ToString());
        }

        /// <summary>
        /// Coroutine-based page transition with fade effect
        /// </summary>
        private IEnumerator TransitionBetweenPages(UIPage fromPage, UIPage toPage, UIPageType toPageType, UIPageType fromPageType)
        {
            // Phase 1: Fade out current page
            if (fromPage != null)
            {
                fromPage.OnPageExit();

                CanvasGroup fromGroup = GetOrAddCanvasGroup(fromPage.gameObject);
                float elapsed = 0f;

                while (elapsed < transitionDuration / 2)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / (transitionDuration / 2);
                    fromGroup.alpha = Mathf.Lerp(1f, 0f, transitionCurve.Evaluate(t));
                    yield return null;
                }

                fromGroup.alpha = 0f;
                fromPage.gameObject.SetActive(false);
            }

            // Phase 2: Fade in new page
            toPage.gameObject.SetActive(true);
            toPage.OnPageEnter();

            CanvasGroup toGroup = GetOrAddCanvasGroup(toPage.gameObject);
            toGroup.alpha = 0f;

            float elapsed2 = 0f;
            while (elapsed2 < transitionDuration / 2)
            {
                elapsed2 += Time.deltaTime;
                float t = elapsed2 / (transitionDuration / 2);
                toGroup.alpha = Mathf.Lerp(0f, 1f, transitionCurve.Evaluate(t));
                yield return null;
            }

            toGroup.alpha = 1f;

            // Complete transition
            CompletePageSwitch(fromPage, toPage, toPageType, fromPageType);
            transitionCoroutine = null;
        }

        /// <summary>
        /// Get or add CanvasGroup component for transitions
        /// </summary>
        private CanvasGroup GetOrAddCanvasGroup(GameObject obj)
        {
            CanvasGroup group = obj.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = obj.AddComponent<CanvasGroup>();
            }
            return group;
        }

        private void CompletePageSwitch(UIPage previousPage, UIPage newPage, UIPageType pageType, UIPageType previousPageType)
        {
            if (previousPage != null && previousPage != newPage)
            {
                previousPage.gameObject.SetActive(false);

                // Ensure previous page alpha is reset
                CanvasGroup prevGroup = previousPage.GetComponent<CanvasGroup>();
                if (prevGroup != null)
                    prevGroup.alpha = 1f;
            }

            newPage.gameObject.SetActive(true);

            // Ensure new page is fully visible
            CanvasGroup newGroup = newPage.GetComponent<CanvasGroup>();
            if (newGroup != null)
                newGroup.alpha = 1f;

            // Call OnPageEnter if not already called
            if (!enableTransitions || previousPage == null)
            {
                newPage.OnPageEnter();
            }

            currentPage = newPage;
            currentPageType = pageType;

            Debug.Log($"[UIPageManager] Page switch complete: {pageType}");

            // Fire event
            OnPageChanged?.Invoke(previousPageType, pageType);
        }

        /// <summary>
        /// Get current active page
        /// </summary>
        public UIPage GetCurrentPage() => currentPage;

        /// <summary>
        /// Get current page type
        /// </summary>
        public UIPageType GetCurrentPageType() => currentPageType;

        /// <summary>
        /// Check if specific page type is active
        /// </summary>
        public bool IsPageActive(UIPageType pageType) => currentPageType == pageType;

        /// <summary>
        /// Go back to previous page (if available)
        /// </summary>
        public void GoBack()
        {
            // Simple implementation - go to default page
            // You could implement a proper page history stack here
            if (currentPageType != defaultPage)
            {
                NavigateToPage(defaultPage);
            }
        }

        /// <summary>
        /// Disable transitions (useful for initial load or performance)
        /// </summary>
        public void SetTransitionsEnabled(bool enabled)
        {
            enableTransitions = enabled;
        }

        void OnDestroy()
        {
            // Clean up
            if (sidebar != null)
            {
                sidebar.OnNavigationRequested -= NavigateToPage;
            }

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
        }
    }

    /// <summary>
    /// Available UI page types
    /// </summary>
    public enum UIPageType
    {
        UserProfile,
        PersonalInventory,
        GroupInventory,
        ItemBrowser,
        Settings,
        Statistics
    }
}