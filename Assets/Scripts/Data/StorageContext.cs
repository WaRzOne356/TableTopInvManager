namespace InventorySystem.Data
{
    /// <summary>
    /// Defines how an item is stored/owned
    /// </summary>
    public enum StorageContext
    {
        Personal = 0,  // Character owns it solo (not in group)
        Group = 1,     // In group pool (unallocated)
        Borrowed = 2   // Character borrowed from group (has ItemOwnership)
    }
}
