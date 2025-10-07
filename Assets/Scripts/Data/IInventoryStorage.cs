using System.Threading.Tasks;
using UnityEngine;

namespace InventorySystem.Data
{
    /// <summary>
    /// Interface for inventory storage systems
    /// Implementations: JSON files, SQLite, Cloud storage, etc.
    /// </summary>
    public interface IInventoryStorage
    {
        /// <summary>
        /// Load inventory data for a specific group
        /// </summary>
        Task<InventoryPersistenceData> LoadAsync(string groupId);

        /// <summary>
        /// Save inventory data
        /// </summary>
        Task SaveAsync(InventoryPersistenceData data);

        /// <summary>
        /// Check if inventory exists for a group
        /// </summary>
        Task<bool> ExistsAsync(string groupId);

        /// <summary>
        /// Delete inventory data for a group
        /// </summary>
        Task DeleteAsync(string groupId);

        /// <summary>
        /// Create backup of current data
        /// </summary>
        Task CreateBackupAsync(string groupId);

        /// <summary>
        /// Get storage info (file size, location, etc.)
        /// </summary>
        string GetStorageInfo(string groupId);
    }
}