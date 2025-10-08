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

        public virtual void Inititialize()
        {
            Debug.Log($"[UIPages] Page initializing: {pageType}");

        }
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}