using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace InventorySystem.Data
{
    /// <summary>
    /// JSON file implementation of inventory storage
    /// </summary>
    public class JsonInventoryStorage : IInventoryStorage
    {
        private readonly string saveFolder;
        private readonly bool prettyPrintJson;
        private readonly bool enableLogging;

        public JsonInventoryStorage(string customSaveFolder = null, bool prettyPrint = true, bool logging = true)
        {
            saveFolder = customSaveFolder ?? Path.Combine(Application.persistentDataPath, "InventoryData");
            prettyPrintJson = prettyPrint;
            enableLogging = logging;

            EnsureFolderExists();
        }

        public async Task<InventoryPersistenceData> LoadAsync(string groupId)
        {
            try
            {
                string filePath = GetFilePath(groupId);

                if (!File.Exists(filePath))
                {
                    if (enableLogging)
                        Debug.Log($"[JsonStorage] No save file found for group: {groupId}");
                    return null;
                }

                if (enableLogging)
                    Debug.Log($"[JsonStorage] Loading inventory from: {filePath}");

                string jsonData = await File.ReadAllTextAsync(filePath);

                if (string.IsNullOrEmpty(jsonData))
                {
                    Debug.LogWarning($"[JsonStorage] Save file is empty: {filePath}");
                    return null;
                }

                var data = JsonUtility.FromJson<InventoryPersistenceData>(jsonData);

                if (enableLogging && data != null)
                    Debug.Log($"[JsonStorage] Loaded {data.items?.Count ?? 0} items for group: {groupId}");

                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonStorage] Failed to load inventory: {e.Message}");
                return null;
            }
        }

        public async Task SaveAsync(InventoryPersistenceData data)
        {
            if (data == null)
            {
                Debug.LogError("[JsonStorage] Cannot save null data");
                return;
            }

            try
            {
                string filePath = GetFilePath(data.groupId);
                data.lastSaved = DateTime.Now.ToString("O");

                if (enableLogging)
                    Debug.Log($"[JsonStorage] Saving to: {filePath}");

                string jsonData = JsonUtility.ToJson(data, prettyPrintJson);
                await File.WriteAllTextAsync(filePath, jsonData);

                if (enableLogging)
                    Debug.Log($"[JsonStorage] Saved {data.items?.Count ?? 0} items");
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonStorage] Save failed: {e.Message}");
            }
        }

        public async Task<bool> ExistsAsync(string groupId)
        {
            await Task.Yield();
            return File.Exists(GetFilePath(groupId));
        }

        public async Task DeleteAsync(string groupId)
        {
            try
            {
                string filePath = GetFilePath(groupId);
                if (File.Exists(filePath))
                {
                    await CreateBackupAsync(groupId);
                    File.Delete(filePath);

                    if (enableLogging)
                        Debug.Log($"[JsonStorage] Deleted inventory for: {groupId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonStorage] Delete failed: {e.Message}");
            }
        }

        public async Task CreateBackupAsync(string groupId)
        {
            try
            {
                string originalPath = GetFilePath(groupId);
                if (!File.Exists(originalPath)) return;

                string backupPath = GetBackupFilePath(groupId);
                File.Copy(originalPath, backupPath, overwrite: true);

                if (enableLogging)
                    Debug.Log($"[JsonStorage] Created backup: {Path.GetFileName(backupPath)}");

                await Task.CompletedTask;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JsonStorage] Backup failed: {e.Message}");
            }
        }

        public string GetStorageInfo(string groupId)
        {
            try
            {
                string filePath = GetFilePath(groupId);
                if (!File.Exists(filePath))
                    return $"No save file exists for: {groupId}";

                var fileInfo = new FileInfo(filePath);
                return $"Group: {groupId} | Size: {fileInfo.Length / 1024} KB | Modified: {fileInfo.LastWriteTime:MM/dd HH:mm}";
            }
            catch (Exception e)
            {
                return $"Error reading info: {e.Message}";
            }
        }

        // Helper methods
        private void EnsureFolderExists()
        {
            try
            {
                if (!Directory.Exists(saveFolder))
                {
                    Directory.CreateDirectory(saveFolder);
                    if (enableLogging)
                        Debug.Log($"[JsonStorage] Created folder: {saveFolder}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonStorage] Folder creation failed: {e.Message}");
            }
        }

        private string GetFilePath(string groupId)
        {
            string safeGroupId = SanitizeFileName(groupId);
            return Path.Combine(saveFolder, $"{safeGroupId}_inventory.json");
        }

        private string GetBackupFilePath(string groupId)
        {
            string safeGroupId = SanitizeFileName(groupId);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return Path.Combine(saveFolder, $"{safeGroupId}_backup_{timestamp}.json");
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "default";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
                fileName = fileName.Replace(c, '_');

            return fileName.Length > 50 ? fileName.Substring(0, 50) : fileName;
        }

        public void OpenSaveFolder()
        {
#if UNITY_EDITOR
            if (Directory.Exists(saveFolder))
                System.Diagnostics.Process.Start(saveFolder);
#endif
        }
    }
}