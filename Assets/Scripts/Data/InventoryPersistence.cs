using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem.Data
{
    //DataStructure for saving and loading inventory to any stoarage system
    //Works with JSON for now, but others to be implemented and should not induce changes here
    [System.Serializable]
    public class InventoryPersistenceData
    {
        public string groupId;
        public string groupName;
        public string lastSaved;
        public int version;
        public List<SerializableInventoryItem> items;
        public List<SerializableUserInfo> users;
        public List<SerializablePlayerCharacter> characters;
        public List<SerializableItemOwnership> itemOwnerships;

        public InventoryPersistenceData()
        {
            items = new List<SerializableInventoryItem>();
            users = new List<SerializableUserInfo>();
            characters = new List<SerializablePlayerCharacter>();  
            itemOwnerships = new List<SerializableItemOwnership>();
        }
    }

    //JSON Serializable version of inventory item
    [System.Serializable]
    public class SerializableInventoryItem
    {
        public string itemId;
        public string itemName;
        public string description;
        public ItemCategory category;
        public int quantity;
        public float weight;
        public int valueInGold;
        public string currentOwner;
        public string thumbnailUrl;
        public string lastModified; // DateTime as ISO string

        // Convert from NetworkInventoryItem (for network->storage)
        public static SerializableInventoryItem FromNetworkItem(NetworkInventoryItem networkItem)
        {
            return new SerializableInventoryItem
            {
                itemId = networkItem.itemId.ToString(),
                itemName = networkItem.itemName.ToString(),
                description = networkItem.description.ToString(),
                category = networkItem.category,
                quantity = networkItem.quantity,
                weight = networkItem.weight,
                valueInGold = networkItem.valueInGold,
                currentOwner = networkItem.currentOwner.ToString(),
                thumbnailUrl = networkItem.thumbnailUrl.ToString(),
                lastModified = new DateTime(networkItem.lastModifiedTicks).ToString("O")
            };
        }

        // Convert to NetworkInventoryItem (for storage->network)
        public NetworkInventoryItem ToNetworkItem()
        {
            DateTime parsedDate = DateTime.TryParse(lastModified, out parsedDate) ? parsedDate : DateTime.Now;

            return new NetworkInventoryItem
            {
                itemId = itemId ?? "",
                itemName = itemName ?? "",
                description = description ?? "",
                category = category,
                quantity = quantity,
                weight = weight,
                valueInGold = valueInGold,
                currentOwner = currentOwner ?? "",
                thumbnailUrl = thumbnailUrl ?? "",
                lastModifiedTicks = parsedDate.Ticks
            };
        }

        // Convert from regular InventoryItem (for single-player saves)
        public static SerializableInventoryItem FromInventoryItem(InventoryItem item)
        {
            return new SerializableInventoryItem
            {
                itemId = item.itemId,
                itemName = item.itemName,
                description = item.description,
                category = item.category,
                quantity = item.quantity,
                weight = item.weight,
                valueInGold = item.valueInGold,
                currentOwner = item.currentOwner,
                thumbnailUrl = item.thumbnailUrl,
                lastModified = item.lastModified.ToString("O")
            };
        }
    }

    /// <summary>
    /// JSON-serializable version of user info
    /// </summary>
    [System.Serializable]
    public class SerializableUserInfo
    {
        public ulong clientId;
        public string userId;
        public string userName;
        public GroupPermission permission;
        public string connectionTime;
        public bool isOnline;

        public static SerializableUserInfo FromNetworkUser(NetworkUserInfo networkUser)
        {
            return new SerializableUserInfo
            {
                clientId = networkUser.clientId,
                userName = networkUser.userName.ToString(),
                permission = networkUser.permission,
                connectionTime = new DateTime(networkUser.connectionTimeTicks).ToString("O"),
                isOnline = networkUser.isOnline
            };
        }

        public NetworkUserInfo ToNetworkUser()
        {
            DateTime parsedDate = DateTime.TryParse(connectionTime, out parsedDate) ? parsedDate : DateTime.Now;

            return new NetworkUserInfo
            {
                clientId = clientId,
                userName = userName ?? "",
                permission = permission,
                connectionTimeTicks = parsedDate.Ticks,
                isOnline = isOnline
            };
        }
    }
}
