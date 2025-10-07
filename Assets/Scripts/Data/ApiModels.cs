using System;
using System.Collections.Generic;
using UnityEngine;

// D&D 5e API Models
[System.Serializable]
public class DnD5eEquipmentResponse
{
    public string index;           // "longsword"
    public string name;           // "Longsword"  
    public string[] desc;         // Description array
    public DnD5eCost cost;        // Price information
    public float weight;          // Weight in pounds
    public DnD5eCategory equipment_category;
    public DnD5eWeaponDetails weapon_category;  // For weapons
    public DnD5eArmorDetails armor_category;    // For armor
    public string url;            // API endpoint
}

[System.Serializable]
public class DnD5eCost
{
    public int quantity;          // 15
    public string unit;           // "gp"
}

[System.Serializable]
public class DnD5eCategory
{
    public string index;          // "weapon"
    public string name;           // "Weapon"
}

[System.Serializable]
public class DnD5eWeaponDetails
{
    public string name;           // "Martial", "Simple"
}

[System.Serializable]
public class DnD5eArmorDetails
{
    public string name;           // "Light", "Heavy"
}

// Search Results
[System.Serializable]
public class DnD5eSearchResponse
{
    public int count;
    public DnD5eSearchResult[] results;
}

[System.Serializable]
public class DnD5eSearchResult
{
    public string index;
    public string name;
    public string url;
}

/// <summary>
/// Open5e API endpoints and content types
/// </summary>
public enum Open5eContentType
{
    MagicItems,     // /magicitems/
    Equipment,      // /equipment/ (not available - use D&D 5e API instead)  
    Spells,         // /spells/
    Monsters,       // /monsters/
    Weapons,        // /weapons/
    Armor          // /armor/
}

/// <summary>
/// Generic Open5e search response structure
/// </summary>
[System.Serializable]
public class Open5eSearchResponse<T>
{
    public int count;
    public string next;
    public string previous;
    public T[] results;
}

/// <summary>
/// Open5e Magic Item details (existing)
/// </summary>
[System.Serializable]
public class Open5eMagicItem
{
    public string slug;
    public string name;
    public string type;
    public string desc;
    public string rarity;
    public bool requires_attunement;
    public string document__title;
    public string document__slug;
}

/// <summary>
/// Open5e Spell details
/// </summary>
[System.Serializable]
public class Open5eSpell
{
    public string slug;
    public string name;
    public string desc;
    public string higher_level;
    public string range;
    public string components;
    public string material;
    public bool ritual;
    public string duration;
    public bool concentration;
    public string casting_time;
    public int level;
    public string school;
    public string dnd_class;
    public string document__title;
}

/// <summary>
/// Open5e Weapon details
/// </summary>
[System.Serializable]
public class Open5eWeapon
{
    public string slug;
    public string name;
    public string category;        // "Simple Melee", "Martial Ranged", etc.
    public string document__title;
    public string cost;            // "2 gp"
    public string damage_dice;     // "1d6"
    public string damage_type;     // "slashing"
    public string weight;          // "3 lb."
    public string[] properties;   // ["finesse", "light"]
}

/// <summary>
/// Open5e Armor details
/// </summary>
[System.Serializable]
public class Open5eArmor
{
    public string slug;
    public string name;
    public string category;        // "Light Armor", "Medium Armor", etc.
    public string document__title;
    public string cost;            // "45 gp"
    public string armor_class;     // "11 + Dex modifier"
    public string weight;          // "10 lb."
    public int strength_requirement;
    public bool stealth_disadvantage;
}

/// <summary>
/// Open5e Monster details (for summoned creatures, familiars, etc.)
/// </summary>
[System.Serializable]
public class Open5eMonster
{
    public string slug;
    public string name;
    public string size;
    public string type;
    public string subtype;
    public string alignment;
    public int armor_class;
    public int hit_points;
    public string hit_dice;
    public string speed;
    public int challenge_rating;
    public string document__title;
    public string desc;            // Often empty, but sometimes has lore
}


// Generic API Error Response
[System.Serializable]
public class ApiErrorResponse
{
    public string error;
    public string message;
    public int status_code;
}