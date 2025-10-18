using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InventorySystem.UI;
using InventorySystem.Data;


namespace InventorySystem.UI.Pages
{
    //User profile and account managment page
    public class UserProfilePage : UIPage
    {
        [Header("Profile Display")]
        [SerializeField] private TMP_InputField userNameField;
        [SerializeField] private Image avatarImage;
        [SerializeField] private TextMeshProUGUI membersinceText;
        [SerializeField] private TextMeshProUGUI totalItemsCreatedText;
        [SerializeField] private TextMeshProUGUI favoriteCategories;

        [Header("Profile Actions")]
        [SerializeField] private Button changeAvatarButton;
        [SerializeField] private Button saveProfileButton;
        [SerializeField] private Button resetPasswordButton;
        [SerializeField] private Button exportDataButton;

        [Header("Statistics")]
        [SerializeField] private TextMeshProUGUI customItemsCount;
        [SerializeField] private TextMeshProUGUI totalInventoryValue;
        [SerializeField] private TextMeshProUGUI mostUsedCategory;
        [SerializeField] private TextMeshProUGUI sessionsCount;

        void Awake()
        {
            pageType = UIPageType.UserProfile;
            pageTitle = "User Profile";

            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            saveProfileButton?.onClick.AddListener(SaveProfile);
            changeAvatarButton?.onClick.AddListener(ChangeAvatar);
            exportDataButton?.onClick.AddListener(ExportDataButton);
        }

        protected override void RefreshContent()
        {
            LoadUserProfile();
            LoadUserStatistics();
        }

        private void LoadUserProfile()
        {
            string userName = GetCurrentUserName();
            if(userNameField != null)
            {
                userNameField.text = userName;
            }

            if (membersinceText != null)
                membersinceText.text = "Member since: " + System.DateTime.Now.AddDays(-30).ToString("MMM yyyy");

            LoadAvatar();
        }

        private void LoadUserStatistics()
        {
            var customItems = CustomItemDatabase.Instance.GetAllCustomItems();
            if (customItemsCount != null)
                customItemsCount.text = $"{customItems.Count} Custom Items";

            //Calculate total valu of all created items
            //ToDo, revise this to get a list of characters the user owns, loop through and display all their different gold values
            int totalValue = customItems.Sum(item => item.valueInGold * item.quantity);
            if (totalInventoryValue != null)
                totalInventoryValue.text = $"{totalValue:N0} gp total Value";

            //Most used category
            var topCategory = customItems.GroupBy(item => item.category).OrderByDescending(g => g.Count()).FirstOrDefault();

            //session count, TODO add legitimate counter
            if (sessionsCount != null)
                sessionsCount.text = "Sessions: " + UnityEngine.Random.Range(5, 50);
        }

        private void LoadUserAvatar()
        {
            //Load user avatar from file or use default
            if(avatarImage != null)
            {
                //Todo: Add Avatar pane and what not
                //avatarImage.sprite = Resources
            }
        }

        private void SaveProfile()
        {
            string newUserName = userNameField?.text ?? "";

            if (string.IsNullOrWhiteSpace(newUserName))
            {
                ShowMessage("User name cannot be empty", MessageType.Error);
                return;
            }

            //Save profiloe Data
            PlayerPrefs.SetString("UserName", newUserName);
            PlayerPrefs.Save();

            ShowMessage("Profile saved successfuly!", MessageType.Success);

            Debug.Log($"[UserProvile] Saved Profile: {newUserName}");
        }

        private void ChangeAvatar()
        {
            //Todo: Implement Avatar importer dialog, for now display a message
            ShowMessage("Avatar features coming soon!", MessageType.Error);
        }

        private async void ExportUserData()
        {
            ShowMessage("Exporing User data...", MessageType.Success);

            try
            {
                //Todo: we shouldn't be saving out custom items, but what items their characters posses
                var customItems = CustomItemDatabase.Instance.GetAllCustomItems();

                var exportData = new
                {
                    userName = userNameField?.text ?? "Unknown Player",
                    exportDate = System.DateTime.Now.ToString("0"),
                    customItemsCount = customItems.Count,
                    customItems = customItems.Select(item => new
                    {
                        name = item.itemName,
                        category = item.category.ToString(),
                        description = item.description,
                        value = item.valueInGold,
                        weight = item.weight,
                        dateCreated = item.dateCreated
                    })
                };

                string jsonData = JsonUtility.ToJson(exportData, true);
                string fileName = $"UserData_Export_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";

                //save to persistnt data path
                string filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
                await System.IO.File.WriteAllTextAsync(filePath, jsonData);

                ShowMessage($"Data Exported to: {filePath}", MessageType.Success);
                Debug.Log($"[UserProfile] Exported data to: {filePath}");
            }
            catch(System.Exception e)
            {
                ShowMessage($"[UserProfile] Error while trying to export: {e.Message}", MessageType.Error);
            }

        }

        private string GetCurrentUserName()
        {
            return PlayerPrefs.GetString("UserName", System.Environment.UserName ?? "Player");

        }

        protected override void ShowMessage(string message, MessageType type)
        {
            //Todo
            // You could show actual UI messages here instead of just debug logs
            // For example, update a status text element or show a toast notification
            base.ShowMessage(message, type);
        }

    }


}
