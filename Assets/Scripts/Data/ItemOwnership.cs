using System;
using UnityEngine;

/// <summary>
/// Tracks which player character owns how much of an item
/// Note: Uses characterId (not userId) because users can have multiple characters
/// </summary>
[System.Serializable]
public class ItemOwnership
{
    public string itemId;           // Links to InventoryItem.itemId
    public string characterId;      // Which character owns it (NOT userId)
    public int quantityOwned;
    public DateTime claimedDate;
    public string notes;            // Optional: "equipped", "in backpack", etc.

    public ItemOwnership(string itemId, string characterId, int qty)
    {
        this.itemId = itemId;
        this.characterId = characterId;
        this.quantityOwned = qty;
        this.claimedDate = DateTime.Now;
        this.notes = "";
    }
}

/// <summary>
/// Simplified ownership info for display purposes
/// </summary>
[System.Serializable]
public class OwnershipInfo
{
    public string characterId;
    public string characterName;    // For display
    public int quantity;

    public OwnershipInfo(string charId, string charName, int qty)
    {
        characterId = charId;
        characterName = charName;
        quantity = qty;
    }
}

/// <summary>
/// Serializable version for storage
/// </summary>
[System.Serializable]
public class SerializableItemOwnership
{
    public string itemId;
    public string characterId;
    public int quantityOwned;
    public string claimedDate;
    public string notes;

    public static SerializableItemOwnership FromOwnership(ItemOwnership ownership)
    {
        return new SerializableItemOwnership
        {
            itemId = ownership.itemId,
            characterId = ownership.characterId,
            quantityOwned = ownership.quantityOwned,
            claimedDate = ownership.claimedDate.ToString("O"),
            notes = ownership.notes ?? ""
        };
    }

    public ItemOwnership ToOwnership()
    {
        return new ItemOwnership(itemId, characterId, quantityOwned)
        {
            claimedDate = DateTime.TryParse(claimedDate, out var parsed) ? parsed : DateTime.Now,
            notes = notes ?? ""
        };
    }
}