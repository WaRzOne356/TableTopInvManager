using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a player character that can own items
/// A user can have multiple characters
/// </summary>
[System.Serializable]
public class PlayerCharacter
{
    public string characterId;
    public string characterName;
    public string ownerUserId;      // Which user owns this character
    public string characterClass;   // Fighter, Wizard, etc.
    public int level;

    public string avatarSpriteName; // Stores sprite reference

    [TextArea(2, 3)]
    public string notes;

    public DateTime dateCreated;
    public DateTime lastPlayed;
    public bool isActive;           // Currently being played

    public PlayerCharacter()
    {
        characterId = Guid.NewGuid().ToString();
        dateCreated = DateTime.Now;
        lastPlayed = DateTime.Now;
        isActive = false;
        avatarSpriteName = "Avatar_Default";
    }

    public PlayerCharacter(string name, string userId)
    {
        characterId = Guid.NewGuid().ToString();
        characterName = name;
        ownerUserId = userId;
        dateCreated = DateTime.Now;
        lastPlayed = DateTime.Now;
        isActive = false;
        avatarSpriteName = "Avatar_Default";
    }
}

/// <summary>
/// Serializable version for storage/network
/// </summary>
[System.Serializable]
public class SerializablePlayerCharacter
{
    public string characterId;
    public string characterName;
    public string ownerUserId;
    public string characterClass;
    public int level;
    public string avatarSpriteName;
    public string notes;
    public string dateCreated;
    public string lastPlayed;
    public bool isActive;

    public static SerializablePlayerCharacter FromPlayerCharacter(PlayerCharacter character)
    {
        return new SerializablePlayerCharacter
        {
            characterId = character.characterId,
            characterName  = character.characterName,
            ownerUserId = character.ownerUserId,
            characterClass = character.characterClass,
            level = character.level,
            avatarSpriteName = character.avatarSpriteName,
            notes = character.notes,
            dateCreated = character.dateCreated.ToString("O"),
            lastPlayed = character.lastPlayed.ToString("O"),
            isActive = character.isActive
        };
    }

    public PlayerCharacter ToPlayerCharacter()
    {
        return new PlayerCharacter
        {
            characterId = characterId,
            characterName = characterName,
            ownerUserId = ownerUserId,
            characterClass = characterClass ?? "",
            level = level,
            avatarSpriteName = avatarSpriteName ?? "Avatar_Default",
            notes = notes ?? "",
            dateCreated = DateTime.TryParse(dateCreated, out var dc) ? dc : DateTime.Now,
            lastPlayed = DateTime.TryParse(lastPlayed, out var lp) ? lp : DateTime.Now,
            isActive = isActive
        };
    }
}