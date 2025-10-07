using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace InventorySystem.Data
{
   //DB for user-created custom items
   //these should be stored and saved for search and furue groups
   public class CustomItemDatabase
    {
        //tHESE WILL ALL MOVE TO AN ACTUAL SQL DB EVENTUALLY
        private const string CUSTOM_ITEMS_FOLDER = "CustomItems";
        private const string CUSTOM_ITEMS_FILE = "custom_items.json";
        private const string CUSTOM_IMAGES_FOLDER = "CustomImages";

        private string customItemsPath;
        private string customImagesPath;
        private string customItemsFilePath;

        private List<CustomItemData> customItems;
        private static CustomItemDatabase instance;

        public static CustomItemDatabase Instance
        {
            get 
            {

                if (instance == null)
                    instance = new CustomItemDatabase();
                return instance;
                
            }

        }

        private CustomItemDatabase()
        {
            customItemsPath = Path.Combine(Application.persistentDataPath, CUSTOM_ITEMS_FOLDER);
            customImagesPath = Path.Combine(customItemsPath, CUSTOM_IMAGES_FOLDER);
            customItemsFilePath = Path.Combine(customItemsPath, CUSTOM_ITEMS_FILE);

            customItems = new List<CustomItemData>();
            //furture helper functions tobe built
            EnsureDirectoriesExist();
            LoadCustomItems();

        }

        private void EnsureDirectoriesExist()
        {
            try
            {
                if (!Directory.Exists(customItemsPath))
                {
                    Directory.CreateDirectory(customItemsPath);
                }
                if (!Directory.Exists(customImagesPath))
                    Directory.CreateDirectory(customImagesPath);

                Debug.Log($"[CustomImages] Directories ready");
            }
            catch
            {
                Debug.Log($"[CustomImages] Directories failed to create");
            }

        }
        
        //Add New Custom Items to Databas
        public async Task<bool> AddCustomItemAsync(CustomItemData item)
        {
            try
            {
                // Assign Unique ID if needed
                if (string.IsNullOrEmpty(item.itemId))
                    item.itemId = Guid.NewGuid().ToString();
                item.dateCreated = DateTime.Now.ToString("0");
                item.lastModified = DateTime.Now.ToString("0");

                //check for duplicates
                var existing = customItems.FirstOrDefault(i => i.itemName.Equals(item.itemName, StringComparison.OrdinalIgnoreCase));
                if(existing != null)
                {
                    Debug.LogWarning($"[CustomItems] Item {item.itemName} already exists.... replacing entry");
                    customItems.Remove(existing);
                }

                customItems.Add(item);
                await SaveCustomItems();

                Debug.Log($"[CustomItems] added {item.itemName} to database");
                return true;
            }
            catch(Exception e)
            {
                Debug.Log($"[CustomItems] Failed to Add Item due to: {e}");
                return false;                
            }
        }

        // Search Custom items by name or description
        public List<CustomItemData> SearchCustomItems(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
                return customItems.ToList();
            var lowerSearch = searchTerm.ToLower();
            return customItems.Where(item => item.itemName.ToLower().Contains(lowerSearch) ||
                                     item.description.ToLower().Contains(lowerSearch) ||
                                     item.category.ToString().ToLower().Contains(lowerSearch) ||
                                     (!string.IsNullOrEmpty(item.tags) && item.tags.ToLower().Contains(lowerSearch))).ToList();
        }

        //Return all items
        public List<CustomItemData> GetAllCustomItems()
        {
            return customItems.ToList();
        }

        //delete custom items
        public async Task<bool> DeleteCustomItemAsync(string itemId)
        {
            try
            {
                var item = customItems.FirstOrDefault(i => i.itemId == itemId);
                if (item != null)
                {
                    customItems.Remove(item);
                    //delete associate image files
                    if (!string.IsNullOrEmpty(item.customImagePath) && File.Exists(item.customImagePath))
                    {
                        File.Delete(item.customImagePath);
                    }

                    await SaveCustomItems();
                    Debug.Log($"[CustomItem] Deleted: {item.itemName}");
                    return true;
                }
                return false;
            }
            catch(Exception e)
            {
                Debug.LogError($"[CustomItems] Failed to delete because: {e}");
                return false;
            }
        }

        //Save Image for CustomItem and return the path to item
        public async Task<string> SaveCustomImageAsync(byte[] imageData, string fileName)
        {
            try
            {
                // Generate safe filename
                string safeFileName = SanitizeFileName(fileName);
                if(!safeFileName.ToLower().EndsWith(".png") && !safeFileName.ToLower().EndsWith(".jpg") && !safeFileName.ToLower().EndsWith(".jpeg"))
                {
                    safeFileName += ".png";
                }

                string fullPath = Path.Combine(customImagesPath, safeFileName);

                //Add timestamp if the file exists
                int counter = 1;
                string baseName = Path.GetFileNameWithoutExtension(safeFileName);
                string extension = Path.GetExtension(safeFileName);

                while (File.Exists(fullPath))
                {
                    safeFileName = $"{baseName}.{counter}{extension}";
                    fullPath = Path.Combine(customImagesPath, safeFileName);
                    counter++;
                }

                await File.WriteAllBytesAsync(fullPath, imageData);
                Debug.Log($"[CustomItems] Saved image: {safeFileName}");
                return fullPath;
            }
            catch(Exception e)
            {
                Debug.LogError($"[CustomItem] Failed to load image: {fileName} due to reason: {e}");
                return null;
            }
        }

        // Load Custom Items From Disk
        private async void LoadCustomItems()
        {
            try
            {
                if (!File.Exists(customItemsFilePath))
                {
                    Debug.Log($"[CustomItems] No CustomItems exist");
                    return;
                }

                string jsonData = await File.ReadAllTextAsync(customItemsFilePath);
                var wrapper = JsonUtility.FromJson<CustomItemWrapper>(jsonData);

                if (wrapper != null && wrapper.items != null)
                {
                    customItems = wrapper.items.ToList();
                    Debug.Log($"[customItems] Loaded {customItems.Count} custom items");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CustomItem] Failed to Load Custom Items: {e}");
                customItems = new List<CustomItemData>();
            }
        }

        //Save Custom Items to Disk
        private async Task SaveCustomItems()
        {
            try{
                var wrapper = new CustomItemWrapper { items=customItems.ToArray() };
                string jsonData = JsonUtility.ToJson(wrapper,true);
                await File.WriteAllTextAsync(customItemsFilePath, jsonData);

                Debug.Log($"[CustomItems] saved {customItems.Count} custom items");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CustomItems] Error while saving items: {e}");
            }
        }

        private string SanitizeFileName(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName))
                return "custom_item_image";
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
                fileName = fileName.Replace(c, '_');

            return fileName.Replace(' ', '_').ToLower();
        }

        public string GetCustomImagesPath() => customImagesPath;
        public int GetCustomItemCount() => customItems.Count;

    }
    ///////////////////////////////////////////////////////
    //Custom Item DataStructuer
    ///////////////////////////////////////////////////////
    [Serializable]
    public class CustomItemData
    {
        public string itemId;
        public string itemName;
        public string description;
        public ItemCategory category;
        public int quantity;
        public float weight;
        public int valueInGold;
        public string tags; // Comma-separated tags for better searching
        public string customImagePath; // Path to custom image file
        public string createdBy; // Player who created this item
        public string dateCreated;
        public string lastModified;

        public List<CustomProperty> properties;

        public CustomItemData()
        {
            properties = new List<CustomProperty>();
            quantity = 1;
            category = ItemCategory.Miscellaneous;
        }

        //conver to regular inventoritem for use in inventory system
        public InventoryItem ToInventoryItem()
        {
            var item = new InventoryItem(itemName, category);
            item.itemId = itemId;
            item.description = description;
            item.quantity = quantity;
            item.weight = weight;
            item.valueInGold = valueInGold;

            // Set Thumbnail URL to custom image path
            if (!string.IsNullOrEmpty(customImagePath) && File.Exists(customImagePath))
                item.thumbnailUrl = "File://" + customImagePath.Replace('\\', '/');

            if(item.properties == null)
                item.properties = new Dictionary<string, string>();

            foreach(var prop in properties)
            {
                item.properties[prop.name] = prop.value;
            }

            item.properties["Source"] = "Custom";
            item.properties["Created By"] = createdBy ?? "unknown user";

            if (!string.IsNullOrEmpty(tags))
                item.properties["Tags"] = tags;

            return item;
        }
    }

    ///////////////////////////////////////////////////////
    //Custom Item Properties, damage dice, AC, etc
    ///////////////////////////////////////////////////////
    [Serializable]
    public class CustomProperty
    {
        public string name;
        public string value;

        public CustomProperty() { }

        public CustomProperty(string propName, string propValue)
        {
            name = propName;
            value = propValue;
        }
    }

    ///////////////////////////////////////////////////////
    //Custom Item wrapper for JSON Serialization of custom items
    ///////////////////////////////////////////////////////
    [Serializable]
    public class CustomItemWrapper
    {
        public CustomItemData[] items;
    }
}
