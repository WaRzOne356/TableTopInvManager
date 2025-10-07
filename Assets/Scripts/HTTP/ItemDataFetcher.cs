using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;
using InventorySystem.Data;

public class ItemDataFetcher : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool enableImageFetching = true;
    [SerializeField] private bool preferOfficialSources = true;

    [Header("Default Values")]
    [SerializeField] private ItemCategory defaultCategory = ItemCategory.Miscellaneous;
    [SerializeField] private float defaultWeight = 1f;
    [SerializeField] private int defaultValue = 1;

    // Singleton
    public static ItemDataFetcher Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Search for items across all available APIs
    /// </summary>
    public async Task<List<ItemSearchResult>> SearchItemsAsync(string searchTerm)
    {
        var results = new List<ItemSearchResult>();

        if (string.IsNullOrEmpty(searchTerm))
            return results;

        Debug.Log($"[ItemFetcher] Comprehensive search for: {searchTerm}");

        //Search Custom Items First
        //Todo, make priroty preference in user account settings for more enhanced results
        await SearchCustomItemsAsync(searchTerm, results);

        // Search D&D 5e equipment
        await SearchDnD5eEquipmentAsync(searchTerm, results);

        // Search ALL Open5e content types
        var open5eResults = await SearchOpen5eAllContentAsync(searchTerm);
        results.AddRange(open5eResults);

        Debug.Log($"[ItemFetcher] Found {results.Count} total results from all sources");
        return results;
    }

    /// <summary>
    /// Get full item details and convert to InventoryItem
    /// </summary>
    public async Task<InventoryItem> FetchItemDetailsAsync(ItemSearchResult searchResult)
    {
        Debug.Log($"[ItemFetcher] Fetching details for: {searchResult.Name}");

        switch (searchResult.Source)
        {
            case ItemSource.DnD5eAPI:
                return await FetchDnD5eItemAsync(searchResult.ApiId);

            case ItemSource.Open5eAPI:
                return await FetchOpen5eItemAsync(searchResult);

            default:
                Debug.LogWarning($"[ItemFetcher] Unknown source: {searchResult.Source}");
                return CreateFallbackItem(searchResult.Name);
        }
    }

    /// <summary>
    /// Quick add common items without API calls
    /// </summary>
    public InventoryItem CreateQuickItem(string itemName, ItemCategory category = ItemCategory.Miscellaneous)
    {
        var quickItems = new Dictionary<string, (string desc, float weight, int value)>
        {
            ["Health Potion"] = ("Restores 2d4+2 hit points when consumed.", 0.5f, 50),
            ["Gold Coins"] = ("Standard currency accepted throughout the realm.", 0.02f, 1),
            ["Trail Rations"] = ("One day's food for one person.", 2f, 5),
            ["Hemp Rope"] = ("50 feet of sturdy rope.", 10f, 2),
            ["Torch"] = ("Provides bright light in a 20-foot radius.", 1f, 1),
            ["Bedroll"] = ("A simple sleeping roll.", 7f, 1),
            ["Backpack"] = ("Can hold 30 pounds of gear.", 5f, 2),
            ["Waterskin"] = ("Holds 4 pints of liquid.", 5f, 2)
        };

        var item = new InventoryItem(itemName, category);

        if (quickItems.ContainsKey(itemName))
        {
            var (desc, weight, value) = quickItems[itemName];
            item.description = desc;
            item.weight = weight;
            item.valueInGold = value;
        }
        else
        {
            item.description = $"A {itemName.ToLower()}.";
            item.weight = defaultWeight;
            item.valueInGold = defaultValue;
        }

        return item;
    }

    // Private helper methods

    //Search Custom Items created by users
    private async Task SearchCustomItemsAsync(string searchTerm, List<ItemSearchResult> results)
    {
        try
        {
            await Task.Yield(); //to make it async

            var customItems = CustomItemDatabase.Instance.SearchCustomItems(searchTerm);

            foreach(var customItem in customItems)
            {
                results.Add(new ItemSearchResult
                {
                    Name = customItem.itemName,
                    ApiId = customItem.itemId,
                    Source = ItemSource.Manual,
                    Description = $"Custom {customItem.category} - {TruncateDescription(customItem.description, 100)}",
                    ImageUrl = customItem.customImagePath
                });
            }

            if(customItems.Count > 0)
            {
                Debug.Log($"[ItemFetcher] Found {customItems.Count} custom items");
            }
        }
        catch(Exception e)
        {
            Debug.LogError($"[ItemFetcher] Error Searching Custom items: {e.Message}");
        }
    }

    private async Task SearchDnD5eEquipmentAsync(string searchTerm, List<ItemSearchResult> results)
    {
        try
        {
            var response = await HttpManager.Instance.SearchDnD5eEquipment(searchTerm);

            if (response.IsSuccess)
            {
                var searchResponse = JsonUtility.FromJson<DnD5eSearchResponse>(response.ResponseText);

                if (searchResponse?.results != null)
                {
                    foreach (var result in searchResponse.results)
                    {
                        results.Add(new ItemSearchResult
                        {
                            Name = result.name,
                            ApiId = result.index,
                            Source = ItemSource.DnD5eAPI,
                            Description = $"D&D 5e equipment: {result.name}"
                        });
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[ItemFetcher] D&D 5e search failed: {response.Error}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ItemFetcher] D&D 5e search error: {e.Message}");
        }
    }

    /// <summary>
    /// Enhanced search that covers all Open5e content types
    /// </summary>
    public async Task<List<ItemSearchResult>> SearchOpen5eAllContentAsync(string searchTerm)
    {
        var results = new List<ItemSearchResult>();

        if (string.IsNullOrEmpty(searchTerm))
            return results;

        Debug.Log($"[ItemFetcher] Searching all Open5e content for: {searchTerm}");

        // Search all content types in parallel
        var searchTasks = new[]
        {
            SearchOpen5eContentTypeAsync(searchTerm, Open5eContentType.MagicItems, results),
            SearchOpen5eContentTypeAsync(searchTerm, Open5eContentType.Spells, results),
            SearchOpen5eContentTypeAsync(searchTerm, Open5eContentType.Weapons, results),
            SearchOpen5eContentTypeAsync(searchTerm, Open5eContentType.Armor, results),
            SearchOpen5eContentTypeAsync(searchTerm, Open5eContentType.Monsters, results)
        };

        await Task.WhenAll(searchTasks);

        Debug.Log($"[ItemFetcher] Found {results.Count} total Open5e results");
        return results;
    }

    // <summary>
    /// Search specific Open5e content type
    /// </summary>
    private async Task SearchOpen5eContentTypeAsync(string searchTerm, Open5eContentType contentType, List<ItemSearchResult> results)
    {
        try
        {
            var response = await HttpManager.Instance.SearchOpen5eContent(searchTerm, contentType);

            if (!response.IsSuccess)
            {
                Debug.LogWarning($"[ItemFetcher] Open5e {contentType} search failed: {response.Error}");
                return;
            }

            // Parse based on content type
            switch (contentType)
            {
                case Open5eContentType.MagicItems:
                    ParseMagicItemResults(response.ResponseText, results);
                    break;
                case Open5eContentType.Spells:
                    ParseSpellResults(response.ResponseText, results);
                    break;
                case Open5eContentType.Weapons:
                    ParseWeaponResults(response.ResponseText, results);
                    break;
                case Open5eContentType.Armor:
                    ParseArmorResults(response.ResponseText, results);
                    break;
                case Open5eContentType.Monsters:
                    ParseMonsterResults(response.ResponseText, results);
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ItemFetcher] Error searching Open5e {contentType}: {e.Message}");
        }
    }

    /*
    private async Task SearchOpen5eMagicItemsAsync(string searchTerm, List<ItemSearchResult> results)
    {
        try
        {
            var response = await HttpManager.Instance.SearchOpen5eMagicItems(searchTerm);

            if (response.IsSuccess)
            {
                var searchResponse = JsonUtility.FromJson<Open5eSearchResponse>(response.ResponseText);

                if (searchResponse?.results != null)
                {
                    foreach (var result in searchResponse.results)
                    {
                        results.Add(new ItemSearchResult
                        {
                            Name = result.name,
                            ApiId = result.name, // Open5e uses name as ID
                            Source = ItemSource.Open5eAPI,
                            Description = $"Magic item: {result.name} ({result.rarity})"
                        });
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[ItemFetcher] Open5e search failed: {response.Error}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ItemFetcher] Open5e search error: {e.Message}");
        }
    }
    */
    ///////////////////////////////////////////////////////////////////////////////
    /// FETCHERS
    ///////////////////////////////////////////////////////////////////////////////
    ///
    private async Task<InventoryItem> FetchCustomItemAsync(string itemID)
    {
        try
        {
            await Task.Yield();

            var customItems = CustomItemDatabase.Instance.GetAllCustomItems();
            var customItem = customItems.FirstOrDefault(i => i.itemId == itemID);

            if (customItem != null)
            {
                Debug.Log($"[ItemFetcher] Found custom item: {customItem.itemName}");
                return customItem.ToInventoryItem();
            }
            else
            {
                Debug.Log($"[ItemFetcher] custom item not found: {customItem.itemName}");
                return null;
            }
        }
        catch(Exception e)
        {
            Debug.LogError($"[ItemFetcher] Error fetching custom item: {e.Message}");
            return null;
        }
    }


    private async Task<InventoryItem> FetchDnD5eItemAsync(string equipmentIndex)
    {
        try
        {
            var response = await HttpManager.Instance.GetDnD5eEquipment(equipmentIndex);

            if (response.IsSuccess)
            {
                var equipment = JsonUtility.FromJson<DnD5eEquipmentResponse>(response.ResponseText);
                return ConvertDnD5eToInventoryItem(equipment);
            }
            else
            {
                Debug.LogWarning($"[ItemFetcher] Failed to fetch D&D 5e item: {equipmentIndex}");
                return CreateFallbackItem(equipmentIndex);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ItemFetcher] D&D 5e fetch error: {e.Message}");
            return CreateFallbackItem(equipmentIndex);
        }
    }

    // <summary>
    /// Enhanced Open5e item fetcher - handles all content types
    /// </summary>
    public async Task<InventoryItem> FetchOpen5eItemAsync(ItemSearchResult searchResult)
    {
        try
        {
            // Determine content type from stored data
            Open5eContentType contentType = Open5eContentType.MagicItems; // Default

            if (Enum.TryParse(searchResult.ImageUrl, out Open5eContentType parsedType))
            {
                contentType = parsedType;
            }

            Debug.Log($"[ItemFetcher] Fetching Open5e {contentType}: {searchResult.Name}");

            // Fetch detailed data
            var response = await HttpManager.Instance.GetOpen5eItemDetails(searchResult.ApiId, contentType);

            if (!response.IsSuccess)
            {
                Debug.LogWarning($"[ItemFetcher] Failed to fetch Open5e details: {response.Error}");
                return CreateFallbackItem(searchResult.Name, contentType);
            }

            // Convert based on content type
            return contentType switch
            {
                Open5eContentType.MagicItems => ConvertMagicItem(response.ResponseText),
                Open5eContentType.Spells => ConvertSpell(response.ResponseText),
                Open5eContentType.Weapons => ConvertWeapon(response.ResponseText),
                Open5eContentType.Armor => ConvertArmor(response.ResponseText),
                Open5eContentType.Monsters => ConvertMonster(response.ResponseText),
                _ => CreateFallbackItem(searchResult.Name, contentType)
            };
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ItemFetcher] Error fetching Open5e item: {e.Message}");
            return CreateFallbackItem(searchResult.Name, Open5eContentType.MagicItems);
        }
    }

    ///////////////////////////////////////////////////////////////////////////////////////
    /// PARSERS
    ///////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Parse magic item search results
    /// </summary>
    private void ParseMagicItemResults(string jsonResponse, List<ItemSearchResult> results)
    {
        try
        {
            var searchResponse = JsonUtility.FromJson<Open5eSearchResponse<Open5eMagicItem>>(jsonResponse);

            if (searchResponse?.results != null)
            {
                foreach (var item in searchResponse.results)
                {
                    results.Add(new ItemSearchResult
                    {
                        Name = item.name,
                        ApiId = item.slug,
                        Source = ItemSource.Open5eAPI,
                        Description = $"Magic Item ({item.rarity}) - {CleanHTMLDescription(item.desc, 100)}"
                    });
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ItemFetcher] Failed to parse magic item results: {e.Message}");
        }
    }

    /// <summary>
    /// Parse spell search results
    /// </summary>
    private void ParseSpellResults(string jsonResponse, List<ItemSearchResult> results)
    {
        try
        {
            var searchResponse = JsonUtility.FromJson<Open5eSearchResponse<Open5eSpell>>(jsonResponse);

            if (searchResponse?.results != null)
            {
                foreach (var spell in searchResponse.results)
                {
                    results.Add(new ItemSearchResult
                    {
                        Name = spell.name,
                        ApiId = spell.slug,
                        Source = ItemSource.Open5eAPI,
                        Description = $"Spell (Level {spell.level}) - {CleanHTMLDescription(spell.desc, 100)}",
                        // Store content type for later fetching
                        ImageUrl = Open5eContentType.Spells.ToString() // Hack: store content type in unused field
                    });
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ItemFetcher] Failed to parse spell results: {e.Message}");
        }
    }

    /// <summary>
    /// Parse weapon search results  
    /// </summary>
    private void ParseWeaponResults(string jsonResponse, List<ItemSearchResult> results)
    {
        try
        {
            var searchResponse = JsonUtility.FromJson<Open5eSearchResponse<Open5eWeapon>>(jsonResponse);

            if (searchResponse?.results != null)
            {
                foreach (var weapon in searchResponse.results)
                {
                    results.Add(new ItemSearchResult
                    {
                        Name = weapon.name,
                        ApiId = weapon.slug,
                        Source = ItemSource.Open5eAPI,
                        Description = $"Weapon ({weapon.category}) - {weapon.damage_dice} {weapon.damage_type} damage",
                        ImageUrl = Open5eContentType.Weapons.ToString()
                    });
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ItemFetcher] Failed to parse weapon results: {e.Message}");
        }
    }

    /// <summary>
    /// Parse armor search results
    /// </summary>
    private void ParseArmorResults(string jsonResponse, List<ItemSearchResult> results)
    {
        try
        {
            var searchResponse = JsonUtility.FromJson<Open5eSearchResponse<Open5eArmor>>(jsonResponse);

            if (searchResponse?.results != null)
            {
                foreach (var armor in searchResponse.results)
                {
                    results.Add(new ItemSearchResult
                    {
                        Name = armor.name,
                        ApiId = armor.slug,
                        Source = ItemSource.Open5eAPI,
                        Description = $"Armor ({armor.category}) - AC {armor.armor_class}",
                        ImageUrl = Open5eContentType.Armor.ToString()
                    });
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ItemFetcher] Failed to parse armor results: {e.Message}");
        }
    }

    /// <summary>
    /// Parse monster search results (for summoned creatures, etc.)
    /// </summary>
    private void ParseMonsterResults(string jsonResponse, List<ItemSearchResult> results)
    {
        try
        {
            var searchResponse = JsonUtility.FromJson<Open5eSearchResponse<Open5eMonster>>(jsonResponse);

            if (searchResponse?.results != null)
            {
                foreach (var monster in searchResponse.results)
                {
                    results.Add(new ItemSearchResult
                    {
                        Name = monster.name,
                        ApiId = monster.slug,
                        Source = ItemSource.Open5eAPI,
                        Description = $"Creature ({monster.type}) - CR {monster.challenge_rating}, {monster.hit_points} HP",
                        ImageUrl = Open5eContentType.Monsters.ToString()
                    });
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ItemFetcher] Failed to parse monster results: {e.Message}");
        }
    }


    ///////////////////////////////////////////////////////////////////////////////////////
    /// Converters
    ///////////////////////////////////////////////////////////////////////////////////////
    private InventoryItem ConvertDnD5eToInventoryItem(DnD5eEquipmentResponse equipment)
    {
        var item = new InventoryItem(equipment.name);

        // Set description
        if (equipment.desc != null && equipment.desc.Length > 0)
        {
            item.description = string.Join("\n", equipment.desc);
        }

        // Set category based on equipment category
        item.category = ConvertDnD5eCategory(equipment.equipment_category?.name);

        // Set weight
        item.weight = equipment.weight;

        // Set value
        if (equipment.cost != null)
        {
            item.valueInGold = ConvertCostToGold(equipment.cost);
        }

        // Set source URL
        item.sourceUrl = $"https://www.dnd5eapi.co{equipment.url}";

        return item;
    }

    private ItemCategory ConvertDnD5eCategory(string categoryName)
    {
        if (string.IsNullOrEmpty(categoryName))
            return defaultCategory;

        return categoryName.ToLower() switch
        {
            "weapon" => ItemCategory.Weapon,
            "armor" => ItemCategory.Armor,
            "shield" => ItemCategory.Shield,
            "adventuring gear" => ItemCategory.Tool,
            "tools" => ItemCategory.Tool,
            "mounts and vehicles" => ItemCategory.Miscellaneous,
            _ => defaultCategory
        };
    }

    /// <summary>
    /// Convert Open5e magic item to inventory item
    /// </summary>
    private InventoryItem ConvertMagicItem(string jsonResponse)
    {
        var magicItem = JsonUtility.FromJson<Open5eMagicItem>(jsonResponse);
        var item = new InventoryItem(magicItem.name, ItemCategory.MagicItem);

        item.description = CleanHTMLDescription(magicItem.desc);
        item.weight = EstimateItemWeight(magicItem.type, magicItem.name);
        item.valueInGold = EstimateMagicItemValue(magicItem.rarity);
        item.sourceUrl = $"https://api.open5e.com/magicitems/{magicItem.slug}/";

        // Add properties
        item.properties = item.properties ?? new Dictionary<string, string>();
        item.properties["Rarity"] = CapitalizeFirst(magicItem.rarity ?? "Unknown");
        item.properties["Type"] = CapitalizeFirst(magicItem.type ?? "Magic Item");
        item.properties["Attunement"] = magicItem.requires_attunement ? "Required" : "Not Required";
        item.properties["Source"] = magicItem.document__title ?? "Open5e";

        return item;
    }

    /// <summary>
    /// Convert Open5e spell to inventory item
    /// </summary>
    private InventoryItem ConvertSpell(string jsonResponse)
    {
        var spell = JsonUtility.FromJson<Open5eSpell>(jsonResponse);
        var item = new InventoryItem(spell.name, ItemCategory.Consumable); // Treat as consumable scroll

        item.description = CleanHTMLDescription(spell.desc);
        if (!string.IsNullOrEmpty(spell.higher_level))
            item.description += "\n\nAt Higher Levels: " + CleanHTMLDescription(spell.higher_level);

        item.weight = 0.1f; // Spell scrolls are light
        item.valueInGold = EstimateSpellValue(spell.level);
        item.sourceUrl = $"https://api.open5e.com/spells/{spell.slug}/";

        // Add spell properties
        item.properties = item.properties ?? new Dictionary<string, string>();
        item.properties["Spell Level"] = spell.level.ToString();
        item.properties["School"] = CapitalizeFirst(spell.school ?? "Unknown");
        item.properties["Casting Time"] = spell.casting_time ?? "1 action";
        item.properties["Range"] = spell.range ?? "Self";
        item.properties["Duration"] = spell.duration ?? "Instantaneous";
        item.properties["Components"] = spell.components ?? "V, S";
        item.properties["Ritual"] = spell.ritual ? "Yes" : "No";
        item.properties["Concentration"] = spell.concentration ? "Yes" : "No";
        item.properties["Source"] = spell.document__title ?? "Open5e";

        return item;
    }

    /// <summary>
    /// Convert Open5e weapon to inventory item
    /// </summary>
    private InventoryItem ConvertWeapon(string jsonResponse)
    {
        var weapon = JsonUtility.FromJson<Open5eWeapon>(jsonResponse);
        var item = new InventoryItem(weapon.name, ItemCategory.Weapon);

        item.description = $"A {weapon.category.ToLower()} that deals {weapon.damage_dice} {weapon.damage_type} damage.";
        item.weight = ParseWeight(weapon.weight);
        item.valueInGold = ParseCost(weapon.cost);
        item.sourceUrl = $"https://api.open5e.com/weapons/{weapon.slug}/";

        // Add weapon properties
        item.properties = item.properties ?? new Dictionary<string, string>();
        item.properties["Weapon Type"] = weapon.category ?? "Unknown";
        item.properties["Damage"] = $"{weapon.damage_dice} {weapon.damage_type}";
        item.properties["Properties"] = weapon.properties != null ? string.Join(", ", weapon.properties) : "None";
        item.properties["Source"] = weapon.document__title ?? "Open5e";

        return item;
    }

    /// <summary>
    /// Convert Open5e armor to inventory item
    /// </summary>
    private InventoryItem ConvertArmor(string jsonResponse)
    {
        var armor = JsonUtility.FromJson<Open5eArmor>(jsonResponse);
        var item = new InventoryItem(armor.name, ItemCategory.Armor);

        item.description = $"{armor.category} that provides {armor.armor_class} armor class.";
        if (armor.strength_requirement > 0)
            item.description += $" Requires {armor.strength_requirement} Strength.";
        if (armor.stealth_disadvantage)
            item.description += " Gives disadvantage on Stealth checks.";

        item.weight = ParseWeight(armor.weight);
        item.valueInGold = ParseCost(armor.cost);
        item.sourceUrl = $"https://api.open5e.com/armor/{armor.slug}/";

        // Add armor properties
        item.properties = item.properties ?? new Dictionary<string, string>();
        item.properties["Armor Type"] = armor.category ?? "Unknown";
        item.properties["Armor Class"] = armor.armor_class ?? "Unknown";
        item.properties["Stealth Disadvantage"] = armor.stealth_disadvantage ? "Yes" : "No";
        if (armor.strength_requirement > 0)
            item.properties["Strength Requirement"] = armor.strength_requirement.ToString();
        item.properties["Source"] = armor.document__title ?? "Open5e";

        return item;
    }

    /// <summary>
    /// Convert Open5e monster to inventory item (for summoned creatures, familiars, etc.)
    /// </summary>
    private InventoryItem ConvertMonster(string jsonResponse)
    {
        var monster = JsonUtility.FromJson<Open5eMonster>(jsonResponse);
        var item = new InventoryItem($"{monster.name} (Summoned)", ItemCategory.Miscellaneous);

        item.description = $"A {monster.size.ToLower()} {monster.type.ToLower()} that can be summoned or appears as a companion. " +
                          $"AC {monster.armor_class}, {monster.hit_points} hit points, Challenge Rating {monster.challenge_rating}.";

        if (!string.IsNullOrEmpty(monster.desc))
            item.description += "\n\n" + CleanHTMLDescription(monster.desc);

        item.weight = 0f; // Summoned creatures have no physical weight
        item.valueInGold = EstimateCreatureValue(monster.challenge_rating);
        item.sourceUrl = $"https://api.open5e.com/monsters/{monster.slug}/";

        // Add creature properties
        item.properties = item.properties ?? new Dictionary<string, string>();
        item.properties["Size"] = CapitalizeFirst(monster.size ?? "Medium");
        item.properties["Type"] = CapitalizeFirst(monster.type ?? "Unknown");
        if (!string.IsNullOrEmpty(monster.subtype))
            item.properties["Subtype"] = CapitalizeFirst(monster.subtype);
        item.properties["Alignment"] = monster.alignment ?? "Unaligned";
        item.properties["Armor Class"] = monster.armor_class.ToString();
        item.properties["Hit Points"] = monster.hit_points.ToString();
        item.properties["Challenge Rating"] = monster.challenge_rating.ToString();
        item.properties["Speed"] = monster.speed ?? "30 ft.";
        item.properties["Source"] = monster.document__title ?? "Open5e";

        return item;
    }

    private int ConvertCostToGold(DnD5eCost cost)
    {
        return cost.unit.ToLower() switch
        {
            "cp" => cost.quantity / 100,  // Copper to gold
            "sp" => cost.quantity / 10,   // Silver to gold
            "gp" => cost.quantity,        // Gold
            "pp" => cost.quantity * 10,   // Platinum to gold
            _ => cost.quantity
        };
    }
    // =============================================================================
    // HELPER METHODS
    // =============================================================================

    private int EstimateSpellValue(int spellLevel)
    {
        // Based on spell scroll costs from D&D 5e
        return spellLevel switch
        {
            0 => 25,      // Cantrip
            1 => 50,      // 1st level
            2 => 150,     // 2nd level
            3 => 300,     // 3rd level
            4 => 500,     // 4th level
            5 => 1000,    // 5th level
            6 => 2000,    // 6th level
            7 => 5000,    // 7th level
            8 => 10000,   // 8th level
            9 => 25000,   // 9th level
            _ => 100      // Unknown level
        };
    }

    private float EstimateItemWeight(string itemType, string itemName)
    {
        if(string.IsNullOrEmpty(itemName) && string.IsNullOrEmpty(itemType))
        {
            Debug.Log("[ItemFetcher] Estimating Weight was sent empty strings, returning default");
            return 1f;
        }

        string lowerItemType = itemType.ToLower() ?? "";
        string lowerItemName = itemName.ToLower() ?? "";

        var weightMappings = new Dictionary<string, float>
        {
            // Jewelry
            ["ring"] = 0.1f,
            ["amulet"] = 0.2f,
            ["necklace"] = 0.2f,

            // Consumables
            ["potion"] = 0.5f,
            ["scroll"] = 0.1f,

            // Magic implements  
            ["wand"] = 1f,
            ["rod"] = 2f,
            ["staff"] = 4f,
            ["orb"] = 3f,
            ["crystal"] = 3f,

            // Equipment
            ["weapon"] = 3f,
            ["armor"] = 20f,
            ["shield"] = 6f,

            // Clothing
            ["cloak"] = 1f,
            ["robe"] = 1f,
            ["boots"] = 1f,
            ["gloves"] = 1f,

            // Headwear
            ["helmet"] = 2f,
            ["circlet"] = 2f,
            ["crown"] = 2f,

            // Containers
            ["bag"] = 0.5f,
            ["container"] = 0.5f,
            ["pouch"] = 0.5f
        };

        //Check type first, then name
        string textToCheck = !string.IsNullOrEmpty(lowerItemType) ? lowerItemType : lowerItemName;

        foreach(var mapping in weightMappings)
        {
            if (textToCheck.Contains(mapping.Key)) return mapping.Value;
        }
        //default
        return 1f;
    }

    private int EstimateMagicItemValue(string rarity)
    {
        if (string.IsNullOrEmpty(rarity))
        {
            Debug.Log($"Rarity not found");
            return 500;
        }

        string lowerRarity = rarity.ToLower().Trim();

        return lowerRarity switch
        {
            "common" => 100,           // 50-100 gp range
            "uncommon" => 500,         // 101-500 gp range
            "rare" => 2500,            // 501-5,000 gp range
            "very rare" => 12500,      // 5,001-50,000 gp range  
            "legendary" => 75000,      // 50,001+ gp range
            "artifact" => 150000,      // Priceless, but we need a number
            "varies" => 1000,          // Some items have variable rarity
            _ => 500                   // Default to uncommon value
        };

    }

    private int EstimateCreatureValue(int challengeRating)
    {
        // Rough estimate based on hiring costs or summoning material costs
        return challengeRating switch
        {
            0 => 10,
            1 => 50,
            2 => 100,
            3 => 200,
            4 => 400,
            5 => 800,
            _ => challengeRating * 200 // Rough scaling for higher CR
        };
    }

    private float ParseWeight(string weightString)
    {
        if (string.IsNullOrEmpty(weightString))
            return 1f;

        // Extract number from strings like "3 lb." or "10 pounds"
        var numbers = System.Text.RegularExpressions.Regex.Match(weightString, @"\d+\.?\d*");
        if (numbers.Success && float.TryParse(numbers.Value, out float weight))
            return weight;

        return 1f; // Default
    }

    private int ParseCost(string costString)
    {
        if (string.IsNullOrEmpty(costString))
            return 1;

        // Extract number and currency from strings like "15 gp" or "2 sp"
        var match = System.Text.RegularExpressions.Regex.Match(costString, @"(\d+)\s*(gp|sp|cp|pp)");
        if (match.Success)
        {
            if (int.TryParse(match.Groups[1].Value, out int amount))
            {
                string currency = match.Groups[2].Value.ToLower();
                return currency switch
                {
                    "cp" => amount / 100,  // Copper to gold
                    "sp" => amount / 10,   // Silver to gold
                    "gp" => amount,        // Gold
                    "pp" => amount * 10,   // Platinum to gold
                    _ => amount
                };
            }
        }

        return 1; // Default
    }

    private string FormatCurrencyValue(int goldValue)
    {
        if (goldValue == 0) return "0 gp";

        if (goldValue >= 10000)
        {
            //Show in Platnum peices for very expensive items
            int platnum = goldValue / 10;
            int remainingGold = goldValue % 10;

            if (remainingGold == 0) return $"{platnum} pp";
            else return $"{platnum} pp, {remainingGold} gp";
        }
        else if (goldValue >= 1000)
        {
            //show with comma seperator for readability
            return $"{goldValue:N0} gp";
        }
        else return $"{goldValue} gp";
    }

    //Capitalize the first leeter of a string
    private string CapitalizeFirst(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input ?? "";

        if (input.Length == 1)
            return input.ToUpper();

        string trimmed = input.Trim();
        if (string.IsNullOrEmpty(trimmed)) return input;


        return char.ToUpper(trimmed[0]) + trimmed.Substring(1).ToLower();
    }

    //Vapitalize Words for things like titles
    private string CapitalizeWords(string input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? "";

        string[] words = input.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);

        for(int i = 0; i < words.Length; i++)
        {
            if(words[i].Length > 0)
            {
                // don't capitalize certain small words
                string lowerWord = words[i].ToLower();
                if(i>0 && (lowerWord == "of" || lowerWord == "the" || lowerWord == "a" || lowerWord == "is" || lowerWord == "an" || lowerWord == "and" || lowerWord == "at" || lowerWord == "in" || lowerWord == "on"))
                {
                    words[i] = lowerWord;
                }
                else
                {
                    words[i] = CapitalizeFirst(words[i]);
                }
            }
        }
        return string.Join(" ", words);
    }

    private string CleanItemName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown Item";

        string cleaned = name.Trim();

        //remove any html tags
        while (cleaned.Contains("<") && cleaned.Contains(">"))
        {
            int startTag = cleaned.IndexOf("<");
            int endTag = cleaned.IndexOf(">", startTag);
            if (endTag > startTag)
            {
                cleaned = cleaned.Remove(startTag, endTag - startTag + 1);
            }
            else
            {
                Debug.Log($"Malformed HTML found: {cleaned}");
                break; //malformed html
            }
        }
        cleaned = CapitalizeWords(cleaned);

        if (cleaned.Length > 100)
            cleaned = cleaned.Substring(0, 97) + "...";

        return string.IsNullOrEmpty(cleaned) ? "Unknown Item" : cleaned;
    }

    private string CleanHTMLDescription(string htmlDesc, int maxLength = 512)
    {
        if (string.IsNullOrEmpty(htmlDesc))
            return "A mysterious item with unknown properites";
        string cleaned = htmlDesc;
        //remove all html tags
        cleaned = cleaned.Replace("<p>", "");
        cleaned = cleaned.Replace("</p>", "\n");
        cleaned = cleaned.Replace("<br>", "\n");
        cleaned = cleaned.Replace("<br/>", "\n");
        cleaned = cleaned.Replace("<br />", "\n");
        cleaned = cleaned.Replace("<em>", "");
        cleaned = cleaned.Replace("</em>", "");
        cleaned = cleaned.Replace("<strong>", "");
        cleaned = cleaned.Replace("</strong>", "");
        cleaned = cleaned.Replace("<i>", "");
        cleaned = cleaned.Replace("</i>", "");
        cleaned = cleaned.Replace("<b>", "");
        cleaned = cleaned.Replace("</b>", "");
        cleaned = cleaned.Replace("<ul>", "");
        cleaned = cleaned.Replace("</ul>", "");
        cleaned = cleaned.Replace("<li>", "• ");
        cleaned = cleaned.Replace("</li>", "\n");

        while (cleaned.Contains("<") && cleaned.Contains(">"))
        {
            int startTag = cleaned.IndexOf("<");
            int endTag = cleaned.IndexOf(">", startTag);
            if (endTag > startTag)
            {
                cleaned = cleaned.Remove(startTag, endTag - startTag + 1);
            }
            else
            {
                Debug.Log($"Malformed HTML found: {cleaned}");
                break; //malformed html
            }

        }

        //clean up any whitespace and newlings
        while (cleaned.Contains("\n\n\n"))
            cleaned = cleaned.Replace("\n\n\n", "\n\n");
        
        while (cleaned.Contains("   "))
            cleaned = cleaned.Replace("   ", " ");
        cleaned = cleaned.Trim();

        //limit length if specified
        if (maxLength > 0 && cleaned.Length > maxLength)
            cleaned = cleaned.Substring(0, maxLength - 3) + "...";
        return string.IsNullOrEmpty(cleaned) ? "No Description Available." : cleaned;
    }

    //Get Category appropriate emoji icons
    private string GetItemIcon(ItemCategory category, string itemName = "", string itemType = "")
    {
        string lowerItemName = itemName.ToLower() ?? "";
        string lowerItemType = itemType.ToLower() ?? "";

        // Check specifi item names first
        if (lowerItemName.Contains("ring")) return "💍";
        if (lowerItemName.Contains("crown") || lowerItemName.Contains("tiara")) return "👑";
        if (lowerItemName.Contains("potion")) return "🧪";
        if (lowerItemName.Contains("scroll")) return "📜";
        if (lowerItemName.Contains("wand")) return "🪄";
        if (lowerItemName.Contains("staff")) return "🦯";
        if (lowerItemName.Contains("orb") || lowerItemName.Contains("crystal")) return "🔮";
        if (lowerItemName.Contains("bag") || lowerItemName.Contains("pouch")) return "🎒";
        if (lowerItemName.Contains("cloak") || lowerItemName.Contains("robe")) return "🧥";
        if (lowerItemName.Contains("boots") || lowerItemName.Contains("shoes")) return "🥾";
        if (lowerItemName.Contains("gloves")) return "🧤";
        if (lowerItemName.Contains("book") || lowerItemName.Contains("tome")) return "📚";
        if (lowerItemName.Contains("deck") || lowerItemName.Contains("cards")) return "🃏";
        if (lowerItemName.Contains("horn")) return "📯";
        if (lowerItemName.Contains("gem") || lowerItemName.Contains("diamond")) return "💎";

        //Fallback based on category
        return category switch
        {
            ItemCategory.Weapon => "⚔️",
            ItemCategory.Armor => "🛡️",
            ItemCategory.Shield => "🛡️",
            ItemCategory.MagicItem => "✨",
            ItemCategory.Consumable => "🧪",
            ItemCategory.Tool => "🔧",
            ItemCategory.Currency => "💰",
            ItemCategory.Ammunition => "🏹",
            ItemCategory.Book => "📚",
            ItemCategory.Jewelry => "💍",
            ItemCategory.QuestItem => "🗝️",
            _ => "📦"
        };
    }


    private InventoryItem CreateFallbackItem(string name, Open5eContentType contentType=Open5eContentType.MagicItems)
    {
        var category = contentType switch
        {
            Open5eContentType.MagicItems => ItemCategory.MagicItem,
            Open5eContentType.Spells => ItemCategory.Consumable,
            Open5eContentType.Weapons => ItemCategory.Weapon,
            Open5eContentType.Armor => ItemCategory.Armor,
            Open5eContentType.Monsters => ItemCategory.Miscellaneous,
            _ => ItemCategory.Miscellaneous
        };

        var item = new InventoryItem(name, category);
        item.description = $"A {contentType.ToString().ToLower().TrimEnd('s')} from the Open5e database. Details could not be retrieved.";
        item.weight = 1f;
        item.valueInGold = 10;

        return item;
    }

   


    public async void TestExpandedOpen5eSearch()
    {
        var fetcher = ItemDataFetcher.Instance;
        if (fetcher == null) return;

        // Test searches that will find different content types
        string[] testSearches = {
        "fireball",           // Should find spell
        "longsword",          // Should find weapon  
        "chain mail",         // Should find armor
        "ring of protection", // Should find magic item
        "dragon",            // Should find monsters
        "healing"            // Should find spells, potions, etc.
    };

        foreach (string search in testSearches)
        {
            Debug.Log($"\n=== Testing search: '{search}' ===");

            var results = await fetcher.SearchItemsAsync(search);

            Debug.Log($"Found {results.Count} results:");

            foreach (var result in results.Take(3)) // Show first 3 results
            {
                Debug.Log($"  • {result.Name} ({result.Source}) - {result.Description}");

                // Test fetching details for first result
                if (result == results.First())
                {
                    var item = await fetcher.FetchItemDetailsAsync(result);
                    if (item != null)
                    {
                        Debug.Log($"    ✅ Fetched: {item.itemName} - {item.valueInGold}gp, {item.weight}lbs");
                        if (item.properties != null && item.properties.Count > 0)
                        {
                            Debug.Log($"    Properties: {string.Join(", ", item.properties.Select(p => $"{p.Key}={p.Value}"))}");
                        }
                    }
                }
            }
        }
    }
    
}

/// <summary>
/// Search result from APIs
/// </summary>
[System.Serializable]
public class ItemSearchResult
{
    public string Name;
    public string ApiId;
    public ItemSource Source;
    public string Description;
    public string ImageUrl;
}

/// <summary>
/// Which API the item came from
/// </summary>
public enum ItemSource
{
    DnD5eAPI,
    Open5eAPI,
    Manual,     // User-created
    QuickAdd    // Pre-defined quick items
}