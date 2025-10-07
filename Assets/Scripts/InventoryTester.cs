using UnityEngine;
using System.Threading.Tasks;

public class InventoryTester : MonoBehaviour
{
    [Header("UI Manager Reference")]
    [SerializeField] private InventoryUIManager uiManager;

    [Header("HTTP Testing")]
    [SerializeField] private string testSearchTerm = "longsword";
    [SerializeField] private bool testAPISearch = false;
    [SerializeField] private bool testQuickItems = false;
    [SerializeField] private bool showCacheStats = false;

    [Header("Original Test Controls")]
    [SerializeField] private bool addTestWeapons = false;
    [SerializeField] private bool clearAllItems = false;

    void Update()
    {
        if (Application.isEditor)
        {
            if (testAPISearch)
            {
                TestAPISearch();
                testAPISearch = false;
            }

            if (testQuickItems)
            {
                TestQuickItems();
                testQuickItems = false;
            }

            if (showCacheStats)
            {
                ShowCacheStats();
                showCacheStats = false;
            }

            // Original tests
            if (addTestWeapons)
            {
                AddTestWeapons();
                addTestWeapons = false;
            }

            if (clearAllItems)
            {
                ClearAllItems();
                clearAllItems = false;
            }
        }
    }

    private async void TestAPISearch()
    {
        Debug.Log($"[Test] Starting API search for: {testSearchTerm}");

        if (ItemDataFetcher.Instance == null)
        {
            Debug.LogError("[Test] ItemDataFetcher not found! Make sure HttpManager and ItemDataFetcher are in the scene.");
            return;
        }

        try
        {
            // Search for items
            var searchResults = await ItemDataFetcher.Instance.SearchItemsAsync(testSearchTerm);

            Debug.Log($"[Test] Found {searchResults.Count} search results");

            if (searchResults.Count > 0)
            {
                // Fetch details for the first result
                var firstResult = searchResults[0];
                Debug.Log($"[Test] Fetching details for: {firstResult.Name}");

                var item = await ItemDataFetcher.Instance.FetchItemDetailsAsync(firstResult);

                if (item != null)
                {
                    Debug.Log($"[Test] Successfully created item: {item.itemName}");
                    Debug.Log($"[Test] Description: {item.description}");
                    Debug.Log($"[Test] Category: {item.category}");
                    Debug.Log($"[Test] Weight: {item.weight} lbs");
                    Debug.Log($"[Test] Value: {item.valueInGold} gp");

                    // Add to inventory
                    if (uiManager != null)
                    {
                        uiManager.AddItem(item);
                        Debug.Log("[Test] Item added to inventory!");
                    }
                }
                else
                {
                    Debug.LogWarning("[Test] Failed to create item from API data");
                }
            }
            else
            {
                Debug.LogWarning($"[Test] No results found for: {testSearchTerm}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Test] API search failed: {e.Message}");
        }
    }

    private void TestQuickItems()
    {
        if (ItemDataFetcher.Instance == null || uiManager == null)
        {
            Debug.LogError("[Test] Missing required components");
            return;
        }

        var quickItems = new string[]
        {
            "Health Potion",
            "Gold Coins",
            "Trail Rations",
            "Hemp Rope",
            "Torch"
        };

        foreach (var itemName in quickItems)
        {
            var item = ItemDataFetcher.Instance.CreateQuickItem(itemName);
            if (item != null)
            {
                uiManager.AddItem(item);
                Debug.Log($"[Test] Added quick item: {itemName}");
            }
        }

        Debug.Log($"[Test] Added {quickItems.Length} quick items");
    }

    private void ShowCacheStats()
    {
        if (HttpManager.Instance != null)
        {
            string stats = HttpManager.Instance.GetCacheStats();
            Debug.Log($"[Test] HTTP {stats}");
        }
        else
        {
            Debug.LogWarning("[Test] HttpManager not found");
        }
    }

    // Original test methods (keep these)
    private void AddTestWeapons()
    {
        if (uiManager == null) return;

        var dagger = new InventoryItem("Silver Dagger", ItemCategory.Weapon);
        dagger.description = "A gleaming silver blade, effective against lycanthropes.";
        dagger.weight = 1f;
        dagger.valueInGold = 25;
        dagger.currentOwner = "Alice";
        dagger.AddPlayerNote("Alice", "Bought this specifically for werewolves");

        uiManager.AddItem(dagger);
        Debug.Log("Added test weapons!");
    }

    private void ClearAllItems()
    {
        if (uiManager != null)
        {
            var inventory = uiManager.GetCurrentInventory();
            if (inventory != null)
            {
                inventory.items.Clear();
                uiManager.RefreshDisplay();
                Debug.Log("Cleared all items!");
            }
        }
    }
}