using UnityEngine;


namespace InventorySystem.UI.Navigation
{
    //Base Class for all UIPages
    public abstract class UIPage : MonoBehaviour
    {
        [Header("Page Info")]
        [SerializeField] protected UIPageType pageType;
        [SerializeField] protected string pageTitle;
        [SerializeField] protected bool refreshOnEnter = true;

        [Header("Loading")]
        [SerializeField] protected GameObject loadingIndicator;
        [SerializeField] protected bool showLoadingOnEnter = true;

        public UIPageType PageType => PageType;
        public string PageTitle => pageTitle;

        public virtual void Initialize()
        {
            Debug.Log($"[UIPages] Page Initializing: {pageType}");

        }
         
        public virtual void OnPageEnter()
        {
            Debug.Log($"[UIPage] Entering page: {pageType}");
            
            if (showLoadingOnEnter)
                SetLoadingState(true);
                
            if (refreshOnEnter)
                RefreshContent();
        }
        
        public virtual void OnPageExit()
        {
            Debug.Log($"[UIPage] Exiting page: {pageType}");
            SetLoadingState(false);
        }
        
        protected virtual void RefreshContent()
        {
            // Override in derived classes to refresh page content
        }
        
        protected void SetLoadingState(bool loading)
        {
            if (loadingIndicator != null)
                loadingIndicator.SetActive(loading);
        }
        
        /// <summary>
        /// Navigate to another page from this page
        /// </summary>
        protected void NavigateTo(UIPageType targetPage)
        {
            UIPageManager.Instance?.NavigateToPage(targetPage);
        }
        
        /// <summary>
        /// Show a notification/message on this page
        /// </summary>
        protected virtual void ShowMessage(string message, MessageType type = MessageType.Info)
        {
            Debug.Log($"[{pageType}] {type}: {message}");
            // Override in derived classes to show actual UI messages
        }
        
        protected enum MessageType
        {
            Info,
            Warning,
            Error,
            Success
        }
    }
}
 