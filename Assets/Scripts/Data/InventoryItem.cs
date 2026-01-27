using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public enum ItemCategory
{
    Armor,
    Weapon,
    Shield,
    Jewelry,
    Consumable, //potions, food, anything thats got a one time use
    Tool,       //Theives tools, pittons, anything that are used
    Book,       //wizard spellbooks, lore, namuals to learn skills, etc
    Currency,   //Gold, silver, cooper
    MagicItem,  //Wands, rings, anything that has a charge
    Ammunition, // arrows, throwing axes, javalins
    Miscellaneous, //random catchall
    QuestItem  //specific quest items.
}

[System.Serializable]
public class PlayerNote
{
    public string playerName;
    public string note;
    public DateTime dateAdded;

    public PlayerNote(string player, string noteText)
    {
        playerName = player;
        note = noteText;
        dateAdded = DateTime.Now;
    }
}

[System.Serializable]
public class InventoryItem
{
    [Header("Basic Info")]
    public string itemName = "";
    [TextArea(3,5)]
    public string description = "";
    public ItemCategory category = ItemCategory.Miscellaneous;

    [Header("Properties")]
    public Dictionary<string, string> properties;

    [Header("Physical Properties")]
    public int quantity = 1;
    [Tooltip("Weight per individual item in lbs")]
    public float weight = 0.0f;
    [Tooltip("Value per individual item in gold coins")]
    public int valueInGold = 0;

    [Header("Visuals")]
    public string thumbnailUrl = "";
    public string sourceUrl = ""; //This should be where most item info should come from

    [Header("Player Information")]
    public List<PlayerNote> playerNotes;
    [Tooltip("Which Player currently 'owns' thi item")]
    public string currentOwner = "";


    // Hidden fields (for later)
    [HideInInspector] public string itemId;
    [HideInInspector] public DateTime dateAdded;
    [HideInInspector] public DateTime lastModified;

    // Constructor - runs when creating new items
    public InventoryItem()
    {
        Initialize();
    }

    // Constructor with name - for quick item creation
    public InventoryItem(string name, ItemCategory cat = ItemCategory.Miscellaneous)
    {
        itemName = name;
        category = cat;
        Initialize();
    }

    private void Initialize()
    {
        itemId = System.Guid.NewGuid().ToString();
        dateAdded = DateTime.Now;
        lastModified = DateTime.Now;
        playerNotes = new List<PlayerNote>();
        properties = new Dictionary<string, string>();
    }

    // Calculated Properties
    public float TotalWeight => weight * quantity;
    public int TotalValue => valueInGold * quantity;

    public void AddPlayerNote(string playerName, string note)
    {
        playerNotes.Add(new PlayerNote(playerName, note));
        lastModified = DateTime.Now;

    }

    public void RemovePlayerNote(string playerName, string note)
    {
        for (int i = playerNotes.Count-1; i >= 0; i--) 
        {
            if (playerNotes[i].playerName == playerName && playerNotes[i].note == note)
            {
                playerNotes.RemoveAt(i);
                lastModified = DateTime.Now;
            }
        }
    }

    public List<PlayerNote> GetNotesFromPlayer(string playerName)
    {
        return playerNotes.FindAll(n => n.playerName == playerName);
    }

    public string GetCategoryDisplayName()
    {
        return category switch
        {
            ItemCategory.MagicItem => "Magic Item",
            ItemCategory.QuestItem => "Quest Item",
            _ => category.ToString()
        };
    }

    public string GetWeightDisplay()
    {
        if (quantity == 1)
            return $"{weight:F1} lbs";
        else
            return $"{weight:F1} lbs each ({TotalWeight:F1} lbs total)";
    }

    public string GetValueDisplay()
    {
        if (quantity == 1)
            return $"{valueInGold} gp";
        else
            return $"{valueInGold} gp each ({TotalValue} gp total)";
    }

    public string GetQuantityDisplay(GroupInventory groupInventory = null)
    {
        if(groupInventory != null)
        {
            var ownership = groupInventory.GetOwnershipSummary(itemId);
            return $"x{quantity} (Owner: {ownership}";
        }

        //fallback ifno group content
        if (!string.IsNullOrEmpty(currentOwner))
            return $"x{quantity} (Owner: {currentOwner}";

        return $"x{quantity}";
    }
}
