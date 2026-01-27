using System;
using System.Collections.Generic;
using System.Linq;
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

    [Header("Ownership Tracking")]
    public List<ItemOwnership> itemOwnerships;

   // Hidden management fields
   [HideInInspector] public DateTime lastModified;
    [HideInInspector] public int version = 1;

    // Constructor
    public GroupInventory()
    {
        groupId = System.Guid.NewGuid().ToString();
        items = new List<InventoryItem>();
        playerNames = new List<string>();
        itemOwnerships = new List<ItemOwnership>();
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
    //Returns how many of an item is unallocated IE still in cart or group inventory location
    public int GetUnallocatedQuantity(string itemId)
    {
        var item = items.Find(i => i.itemId == itemId);
        if (item == null) return 0;

        int totalOwned = itemOwnerships.FindAll(o => o.itemId == itemId).Sum(o => o.quantityOwned);

        return item.quantity - totalOwned;
    }

    //Get all owners of a specific item with their quantities
    public List<OwnershipInfo> GetItemOwners(string itemId)
    {
        var characterManager = CharacterManager.Instance;
        if (characterManager == null)
        {
            Debug.LogWarning("[GroupInventory] CharacterManager not available");
            return new List<OwnershipInfo>();
        }

        return itemOwnerships.Where(o => o.itemId == itemId).Select(o =>
            {
                var character = characterManager.GetCharacterById(o.characterId);
                string displayName = character != null ? character.characterName : "Unknown";
                return new OwnershipInfo(o.characterId, displayName, o.quantityOwned);
            })
            .ToList();
    }
    // Claim Items from group storage for specific player
    public bool ClaimItem(string itemId, string characterId, int quantity)
    {
        var item = items.Find(i => i.itemId == itemId);
        if (item == null)
        {
            Debug.LogWarning($"Item {itemId} not found");
            return false;
        }

        int unallocated = GetUnallocatedQuantity(itemId);
        if (quantity > unallocated)
        {
            Debug.LogWarning($"Cannot claim {quantity}. Only {unallocated} unallocated");
            return false;
        }

        // Find existing ownership or create new
        var ownership = itemOwnerships.Find(o =>
            o.itemId == itemId && o.characterId == characterId);

        if (ownership != null)
        {
            ownership.quantityOwned += quantity;
        }
        else
        {
            itemOwnerships.Add(new ItemOwnership(itemId, characterId, quantity));
        }

        lastModified = DateTime.Now;
        version++;
        return true;
    }

    // Return Items from player inventory to group storage
    public bool ReturnItem(string itemId, string characterId, int quantity)
    {
        var ownership = itemOwnerships.Find(o =>
            o.itemId == itemId && o.characterId == characterId);

        if (ownership == null || ownership.quantityOwned < quantity)
        {
            Debug.LogWarning($"Character {characterId} doesn't own {quantity} of item {itemId}");
            return false;
        }

        ownership.quantityOwned -= quantity;

        // Remove ownership entry if quantity reaches 0
        if (ownership.quantityOwned <= 0)
        {
            itemOwnerships.Remove(ownership);
        }

        lastModified = DateTime.Now;
        version++;
        return true;
    }

    public bool TransferItem(string itemId, string fromCharacterId, string toCharacterId, int quantity)
    {
        var fromOwnership = itemOwnerships.Find(o =>
            o.itemId == itemId && o.characterId == fromCharacterId);

        if (fromOwnership == null || fromOwnership.quantityOwned < quantity)
        {
            Debug.LogWarning($"Character {fromCharacterId} doesn't own {quantity} to transfer");
            return false;
        }

        // Reduce from sender
        fromOwnership.quantityOwned -= quantity;
        if (fromOwnership.quantityOwned <= 0)
        {
            itemOwnerships.Remove(fromOwnership);
        }

        // Add to receiver
        var toOwnership = itemOwnerships.Find(o =>
            o.itemId == itemId && o.characterId == toCharacterId);

        if (toOwnership != null)
        {
            toOwnership.quantityOwned += quantity;
        }
        else
        {
            itemOwnerships.Add(new ItemOwnership(itemId, toCharacterId, quantity));
        }

        lastModified = DateTime.Now;
        version++;
        return true;
    }

    public List<InventoryItem> GetCharacterInventory(string characterId)
    {
        var characterItems = new List<InventoryItem>();

        var ownerships = itemOwnerships.FindAll(o => o.characterId == characterId);

        foreach (var ownership in ownerships)
        {
            var item = items.Find(i => i.itemId == ownership.itemId);
            if (item != null)
            {
                // Create a copy with the character's quantity
                var characterItem = new InventoryItem(item.itemName, item.category);
                characterItem.itemId = item.itemId;
                characterItem.description = item.description;
                characterItem.quantity = ownership.quantityOwned;
                characterItem.weight = item.weight;
                characterItem.valueInGold = item.valueInGold;
                characterItem.thumbnailUrl = item.thumbnailUrl;
                characterItem.sourceUrl = item.sourceUrl;
                characterItem.properties = new Dictionary<string, string>(item.properties);

                characterItems.Add(characterItem);
            }
        }

        return characterItems;
    }
    
    public string GetOwnershipSummary(string itemId)
    {
        var owners = GetItemOwners(itemId);
        var unallocated = GetUnallocatedQuantity(itemId);

        if (owners.Count == 0)
        {
            return unallocated > 0 ? $"Party Storage ({unallocated})" : "None";
        }

        var ownerStrings = owners.Select(o => $"{o.characterName}: {o.quantity}");
        var result = string.Join(", ", ownerStrings);

        if (unallocated > 0)
        {
            result += $", Party: {unallocated}";
        }

        return result;
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