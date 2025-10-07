using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GroupInventory
{
    [Header("Group Info")]
    public string groupName = "My Party";
    public string groupId = "";

    [Header("Inventory")]
    public List<InventoryItem> items;

    [Header("Users")]
    public List<string> playerNames;

    // Hidden management fields
    [HideInInspector] public DateTime lastModified;
    [HideInInspector] public int version = 1;

    // Constructor
    public GroupInventory()
    {
        groupId = System.Guid.NewGuid().ToString();
        items = new List<InventoryItem>();
        playerNames = new List<string>();
        lastModified = DateTime.Now;
    }

    //Getter Methods
    
    //return all items of a specific category
    public List<InventoryItem> GetItemsByCategory(ItemCategory category)
    {
        return items.FindAll(item => item.category == category);
    }

    //return a list of all categories currently used. This is good for filtering.
    public List<ItemCategory> GetUsedCategories()
    {
        var usedCategories = new List<ItemCategory>();
        foreach (var item in items)
        {
            if (!usedCategories.Contains(item.category))
            {
                usedCategories.Add(item.category);
            }

        }
        return usedCategories;
    }

    //Calcualte total's
    public float GetTotalWeight()
    {
        float total = 0;
        foreach( var item in items)
        {
            total += item.weight;
        }
        return total;
    }

    public int GetTotalValue()
    {
        int total = 0;
        foreach (var item in items)
        {
            total += item.valueInGold;
        }
        return total;
    }

    //Helper method to search for items by text
    public List<InventoryItem> SearchItems(string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
            return new List<InventoryItem>(items);

        var searchLower = searchText.ToLower();
        return items.FindAll(item =>
         item.itemName.ToLower().Contains(searchLower) ||
        item.description.ToLower().Contains(searchLower) ||
        item.GetCategoryDisplayName().ToLower().Contains(searchLower) ||
        item.currentOwner.ToLower().Contains(searchLower));
    }


    // Helper method to add items
    public void AddItem(InventoryItem newItem)
    {
        // Check if we already have this item
        InventoryItem existingItem = items.Find(item => item.itemName == newItem.itemName);

        if (existingItem != null)
        {
            // Add to existing quantity
            existingItem.quantity += newItem.quantity;
        }
        else
        {
            // Add as new item
            items.Add(newItem);
        }

        // Update modification time
        lastModified = DateTime.Now;
        version++;
    }

    // Helper method to remove items
    public bool RemoveItem(string itemId)
    {
        InventoryItem itemToRemove = items.Find(item => item.itemId == itemId);

        if (itemToRemove != null)
        {
            items.Remove(itemToRemove);
            lastModified = DateTime.Now;
            version++;
            return true;
        }

        return false;
    }


}