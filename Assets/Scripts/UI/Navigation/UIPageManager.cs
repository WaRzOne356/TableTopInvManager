using System;
using System.Collections.Generic;
using UnityEngine;


namespace InventorySystem.UI.Navigation
{
    //Class that manages Page Navigation and what page the user is currently on
    public class UIPageManager : MonoBehaviour
    {
        [Header("Page Configuration")]
        [SerializeField] private List<UIPage> pages;
        [SerializeField] private UIPageType defaultPage = UIPageType.GroupInventory;
        [SerializeField] private bool rememberLastPage = true;

        [Header("Navigation")]
        [SerializeField] private SidebarNavigation sideBar;
        [SerializeField] private Transform pageContainer;

        [Header("Transitions")]
        [SerializeField] private bool enableTransitions = true;
        [SerializeField] private float transitionDuration = 0.3f;

        //Events
        public static event Action<UIPageType, UIPageType> OnPageChaged; //old,new

        //State
        private UIPageType currentPageType;
        private UIPage currentPage;
        private Dictionary<UIPageType, UIPage> pageMap;

        //Singleton
        private static UIPageManager Instance { get; private set; }

        private void Awake()
        {
            if(Instance == null)
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

        private void Start()
        {
            SetupNavigation();
            NatigateToDefaultPage();
        }

        private void InitializePages()
        {
            pageMap = new Dictionary<UIPageType, UIPage>();

            foreach(var page in pages)
            {
                if(page != null)
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
            if(sideBar != null)
            {
                sideBar.OnNavigationRequested += NavigateToPage;
                sideBar.SetActivePageType(defaultPage);
            }
        }

        private void NavigateToDefaultPage()
        {
            UIPageType targetPage = defaultPage;

            //Try to restore last page if setting enabled
            if(rememberLastPage)
            {
                string lastPageKey = "LastUIPage";
                if(PlayerPrefs.HasKey(lastPageKey))
                {
                    string lastPageStr = PlayerPrefs.GetString(lastPageKey);
                    if (Enum.TryParse(lastPageStr, out UIPageType lastPage))
                        targetPage = lastPage;
                }
            }

            NavigateToPage(targetPage);
        }

        private void NavigateToPage(UIPageType targetPage)
        {
            if (currentPage == targetPage)
            {
                Debug.Log($"[UIPageManager] Already on page: {targetPage}");
                return;
            }

            if (!pageMap.ContainsKey(targetPage))
            {
                Debug.LogError($"[UIPageManager] Page not found: {targetPage}");
            }

            var previousPageType = currentPageType;
            var previousPage = currentPage;
            var newPage = pageMap[targetPage];

            Debug.Log($"[UIPageManager] Navigating: {previousPageType} -> {targetPage}");

            //start page transition
            if(enableTransitions && previousPage != null)
            {
                StartPageTransition(previousPage, newPage, targetPage);
            }
            else
            {
                CompletePageSwitch(previousPage, newPage, targetPage);
            }

            //update sidebar
            if (sideBar != null)
                sideBar.SetActivePageType(targetPage);

            //Repmember page
            if (rememberLastPage)
                PlayerPrefs.SetString("LastUIPage", targetPage.ToString());

            //Fire Event
            OnPageChaged?.Invoke(previousPageType, targetPage);

        }

        private void StartPageTransition(UIPage fromPage, UIPage toPage, UIPageType toPageType)
        {
            //Simple Fade Transition
            // Todo, make better transitions
            if (fromPage != null)
            {
                fromPage.OnPageExit();
                LeanTween.alpha(fromPage.GetComponent<RectTransform>(), 0.0f, transitionDuration / 2)
                    .setOnComplete(() =>
                    {
                        fromPage.gameObject.SetActive(false);
                        ShowNewPage(toPage, toPageType);
                    });
            }
            else
            {
                ShowNewPage(toPage, toPageType);
            }
           
        }

        private void ShowNewPage(UIPage newPage, UIPageType pageType)
        {
            newPage.gameObject.SetActive(true);
            newPage.OnPageEnter();

            if (enableTransitions)
            {
                var rectTransform = newPage.GetComponent<RectTransform>();
                rectTransform.alpha = 0.0f;
                LeanTween.alpha(rectTransform, 1.0f, transitionDuration / 2)
                    .setOnComplete(() => CompletePageSwitch(null, newPage, pageType));
            }
            else
            {
                CompletePageSwitch(null, newPage, pageType);
            }
        }

        private void CompletePageSwitch(UIPage previousPage, UIPage newPage, UIPageType pageType)
        {
            if (previousPage != null && previousPage != newPage)
            {
                previousPage.gameObject.SetActive(false);
            }

            newPage.gameObject.SetActive(true);
            newPage.OnPageEnter();

            currentPage = newPage;
            currentPageType = pageType;

            Debug.Log($"[UIPageManager] Page Switch Complete: {pageType}");

        }

        /////////////////////////////////////////////////////////////////////////////////////////////
        /// Helpers and Getter/setters
        /// /////////////////////////////////////////////////////////////////////////////////////////
        /// 
        public UIPage GetCurrentPage() => currentPage;

        public UIPageType GetCurrentPageType() => currentPageType;

        public bool IsPageActive(UIPageType pageType) => currentPageType == pageType;

        public void GoBack()
        {
            //ToDo; make a go back stack to allow the user to go back several pages
            if (currentPageType != defaultPage)
                NavigateToPage(defaultPage);
        }

    }


    //Enum of the different page types
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
