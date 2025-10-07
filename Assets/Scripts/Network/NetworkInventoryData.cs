using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Network-serializable version of InventoryItem
/// Simpler structure that can travel over the network
/// </summary>
[System.Serializable]
public struct NetworkInventoryItem : INetworkSerializable
{
    public FixedString64Bytes itemId;
    public FixedString128Bytes itemName;
    public FixedString512Bytes description;
    public ItemCategory category;
    public int quantity;
    public float weight;
    public int valueInGold;
    public FixedString64Bytes currentOwner;
    public FixedString128Bytes thumbnailUrl;
    public long lastModifiedTicks;  // DateTime as ticks for networking

    // Convert from full InventoryItem to network version
    public static NetworkInventoryItem FromInventoryItem(InventoryItem item)
    {
        return new NetworkInventoryItem
        {
            itemId = item.itemId,
            itemName = item.itemName,
            description = item.description.Length > 512 ?
                         item.description.Substring(0, 509) + "..." :
                         item.description,
            category = item.category,
            quantity = item.quantity,
            weight = item.weight,
            valueInGold = item.valueInGold,
            currentOwner = item.currentOwner ?? "",
            thumbnailUrl = item.thumbnailUrl ?? "",
            lastModifiedTicks = item.lastModified.Ticks
        };
    }

    // Convert back to full InventoryItem
    public InventoryItem ToInventoryItem()
    {
        var item = new InventoryItem(itemName.ToString(), category);
        item.itemId = itemId.ToString();
        item.description = description.ToString();
        item.quantity = quantity;
        item.weight = weight;
        item.valueInGold = valueInGold;
        item.currentOwner = currentOwner.ToString();
        item.thumbnailUrl = thumbnailUrl.ToString();
        item.lastModified = new DateTime(lastModifiedTicks);

        return item;
    }

    // Required by INetworkSerializable
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref itemId);
        serializer.SerializeValue(ref itemName);
        serializer.SerializeValue(ref description);
        serializer.SerializeValue(ref category);
        serializer.SerializeValue(ref quantity);
        serializer.SerializeValue(ref weight);
        serializer.SerializeValue(ref valueInGold);
        serializer.SerializeValue(ref currentOwner);
        serializer.SerializeValue(ref thumbnailUrl);
        serializer.SerializeValue(ref lastModifiedTicks);
    }
}

/// <summary>
/// User permission levels for inventory management
/// </summary>
public enum NetworkUserPermission : byte
{
    ReadOnly = 0,      // Can view only
    EditItems = 1,     // Can modify quantities and notes
    AddItems = 2,      // Can add new items
    DeleteItems = 3,   // Can remove items
    Admin = 4          // Full control including user permissions
}

/// <summary>
/// Information about connected users
/// </summary>
[System.Serializable]
public struct NetworkUserInfo : INetworkSerializable
{
    public ulong clientId;
    public FixedString64Bytes userName;
    public NetworkUserPermission permission;
    public long connectionTimeTicks;
    public bool isOnline;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref userName);
        serializer.SerializeValue(ref permission);
        serializer.SerializeValue(ref connectionTimeTicks);
        serializer.SerializeValue(ref isOnline);
    }
}

/// <summary>
/// Network events for inventory operations
/// </summary>
public enum NetworkInventoryAction : byte
{
    ItemAdded,
    ItemRemoved,
    ItemQuantityChanged,
    ItemOwnerChanged,
    ItemNoteAdded,
    PermissionChanged,
    InventoryCleared
}

/// <summary>
/// Data package for inventory change events
/// </summary>
[System.Serializable]
public struct NetworkInventoryEvent : INetworkSerializable
{
    public NetworkInventoryAction action;
    public FixedString64Bytes itemId;
    public FixedString64Bytes userId;
    public int newValue;           // For quantity changes
    public FixedString128Bytes stringData;  // For notes, owner names, etc.
    public long timestampTicks;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref action);
        serializer.SerializeValue(ref itemId);
        serializer.SerializeValue(ref userId);
        serializer.SerializeValue(ref newValue);
        serializer.SerializeValue(ref stringData);
        serializer.SerializeValue(ref timestampTicks);
    }
}