using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Text;

using UnityEngine;
using UnityEngine.Networking;

public class HttpManager : MonoBehaviour
{
    [Header("API Configuration")]
    [SerializeField] private float timeoutSeconds = 10f;
    [SerializeField] private int maxRetries = 3;
    [SerializeField] private bool logRequests = true;

    [Header("API Endpoints")]
    [SerializeField] private string dnd5eBaseUrl = "https://www.dnd5eapi.co/api";
    [SerializeField] private string open5eBaseUrl = "https://api.open5e.com";

    [Header("JSON Logging")]
    [SerializeField] private bool saveJsonToFiles = true;
    [SerializeField] private string logFolder = "ApiResponses";
    [SerializeField] private bool logSuccessfulRequests = true;
    [SerializeField] private bool logFailedRequests = true;
    [SerializeField] private bool prettyPrintJson = true;

    private string logFolderPath;

    // Singleton pattern for easy access
    public static HttpManager Instance { get; private set; }

    // Cache for reducing API calls
    private Dictionary<string, string> responseCache;

    void Awake()
    {
        // Existing singleton code...
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            responseCache = new Dictionary<string, string>();

            //  Setup logging folder
            SetupLoggingFolder();
        }
        else
        {
            Destroy(gameObject);
        }
    }



    /// <summary>
    /// Make a GET request to any URL
    /// </summary>
    public async Task<HttpResponse> GetAsync(string url)
    {
        // Check cache first
        if (responseCache.ContainsKey(url))
        {
            if (logRequests)
                Debug.Log($"[HTTP] Cache hit: {url}");

            return new HttpResponse
            {
                IsSuccess = true,
                ResponseText = responseCache[url],
                StatusCode = 200
            };
        }

        if (logRequests)
            Debug.Log($"[HTTP] GET: {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            // Set timeout
            request.timeout = (int)timeoutSeconds;

            // Send request
            var operation = request.SendWebRequest();

            // Wait for completion
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            // Handle response
            var response = new HttpResponse
            {
                StatusCode = (int)request.responseCode,
                ResponseText = request.downloadHandler.text,
                Error = request.error
            };

            if (request.result == UnityWebRequest.Result.Success)
            {
                response.IsSuccess = true;

                // Cache successful responses
                if (!responseCache.ContainsKey(url))
                {
                    responseCache[url] = response.ResponseText;
                }


                if (logRequests)
                {
                    // Log successful response to file
                    LogSuccessfulResponse(url, response.ResponseText);
                    Debug.Log($"[HTTP] Success: {url} ({response.ResponseText.Length} chars)");
                }
            }
            else
            {
                response.IsSuccess = false;

                if (logRequests)
                    // NEW: Log failed response to file
                    LogFailedResponse(url, response.Error, response.StatusCode);
                Debug.LogWarning($"[HTTP] Failed: {url} - {request.error}");
            }

            return response;
        }
    }

    /// <summary>
    /// Search D&D 5e equipment by name
    /// </summary>
    public async Task<HttpResponse> SearchDnD5eEquipment(string searchTerm)
    {
        string url = $"{dnd5eBaseUrl}/equipment?name={UnityWebRequest.EscapeURL(searchTerm)}";
        return await GetAsync(url);
    }


    /// <summary>
    /// Get specific D&D 5e equipment by index
    /// </summary>
    public async Task<HttpResponse> GetDnD5eEquipment(string equipmentIndex)
    {
        string url = $"{dnd5eBaseUrl}/equipment/{equipmentIndex}";
        return await GetAsync(url);
    }

    /// <summary>
    /// Get all D&D 5e equipment (for browsing)
    /// </summary>
    public async Task<HttpResponse> GetAllDnD5eEquipment()
    {
        string url = $"{dnd5eBaseUrl}/equipment";
        return await GetAsync(url);
    }

    /// <summary>
    /// Search Open5e magic items
    /// </summary>
    public async Task<HttpResponse> SearchOpen5eMagicItems(string searchTerm)
    {
        string url = $"{open5eBaseUrl}/magicitems/?search={UnityWebRequest.EscapeURL(searchTerm)}";
        return await GetAsync(url);
    }


    /// <summary>
    /// Search all Open5e content types
    /// </summary>
    public async Task<HttpResponse> SearchOpen5eContent(string searchTerm, Open5eContentType contentType)
    {
        string endpoint = contentType switch
        {
            Open5eContentType.MagicItems => "magicitems",
            Open5eContentType.Spells => "spells",
            Open5eContentType.Weapons => "weapons",
            Open5eContentType.Armor => "armor",
            Open5eContentType.Monsters => "monsters",
            _ => "magicitems"
        };

        string url = $"{open5eBaseUrl}/{endpoint}/?search={UnityWebRequest.EscapeURL(searchTerm)}";
        return await GetAsync(url);
    }

    /// <summary>
    /// Get specific item details from Open5e
    /// </summary>
    public async Task<HttpResponse> GetOpen5eItemDetails(string itemSlug, Open5eContentType contentType)
    {
        string endpoint = contentType switch
        {
            Open5eContentType.MagicItems => "magicitems",
            Open5eContentType.Spells => "spells",
            Open5eContentType.Weapons => "weapons",
            Open5eContentType.Armor => "armor",
            Open5eContentType.Monsters => "monsters",
            _ => "magicitems"
        };

        string url = $"{open5eBaseUrl}/{endpoint}/{itemSlug}/";
        return await GetAsync(url);
    }

    /// <summary>
    /// Browse Open5e content by type (paginated)
    /// </summary>
    public async Task<HttpResponse> BrowseOpen5eContent(Open5eContentType contentType, int page = 1, int pageSize = 20)
    {
        string endpoint = contentType switch
        {
            Open5eContentType.MagicItems => "magicitems",
            Open5eContentType.Spells => "spells",
            Open5eContentType.Weapons => "weapons",
            Open5eContentType.Armor => "armor",
            Open5eContentType.Monsters => "monsters",
            _ => "magicitems"
        };

        string url = $"{open5eBaseUrl}/{endpoint}/?page={page}&page_size={pageSize}";
        return await GetAsync(url);
    }




    /// <summary>
    /// Download an image from URL
    /// </summary>
    public async Task<HttpResponse> GetImageAsync(string imageUrl)
    {
        if (logRequests)
            Debug.Log($"[HTTP] Image: {imageUrl}");

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            request.timeout = (int)timeoutSeconds;

            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                await Task.Yield();
            }

            var response = new HttpResponse
            {
                StatusCode = (int)request.responseCode,
                Error = request.error
            };

            if (request.result == UnityWebRequest.Result.Success)
            {
                response.IsSuccess = true;
                response.Texture = DownloadHandlerTexture.GetContent(request);

                if (logRequests)
                    Debug.Log($"[HTTP] Image Success: {imageUrl}");
            }
            else
            {
                response.IsSuccess = false;

                if (logRequests)
                    Debug.LogWarning($"[HTTP] Image Failed: {imageUrl} - {request.error}");
            }

            return response;
        }
    }

    /// <summary>
    /// Setup the folder where JSON logs will be saved
    /// </summary>
    private void SetupLoggingFolder()
    {
        if (!saveJsonToFiles) return;

        // Create folder in persistent data path (survives builds)
        logFolderPath = Path.Combine(Application.persistentDataPath, logFolder);

        try
        {
            if (!Directory.Exists(logFolderPath))
            {
                Directory.CreateDirectory(logFolderPath);
                Debug.Log($"[HTTP] Created logging folder: {logFolderPath}");
            }

            // Create a readme file explaining the logs
            string readmePath = Path.Combine(logFolderPath, "README.txt");
            if (!File.Exists(readmePath))
            {
                CreateReadmeFile(readmePath);
            }

            Debug.Log($"[HTTP] JSON logging enabled. Files saved to: {logFolderPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HTTP] Failed to setup logging folder: {e.Message}");
            saveJsonToFiles = false; // Disable logging if folder creation fails
        }
    }

    /// <summary>
    /// Create a readme file explaining the log format
    /// </summary>
    private void CreateReadmeFile(string path)
    {
        var readme = new StringBuilder();
        readme.AppendLine("API Response Logs");
        readme.AppendLine("==================");
        readme.AppendLine();
        readme.AppendLine("This folder contains JSON responses from tabletop game APIs.");
        readme.AppendLine();
        readme.AppendLine("File naming convention:");
        readme.AppendLine("- dnd5e_equipment_[item-name]_[timestamp].json");
        readme.AppendLine("- dnd5e_search_[search-term]_[timestamp].json");
        readme.AppendLine("- open5e_search_[search-term]_[timestamp].json");
        readme.AppendLine("- failed_[url-hash]_[timestamp].json");
        readme.AppendLine();
        readme.AppendLine("Use these files to:");
        readme.AppendLine("- Understand API response structure");
        readme.AppendLine("- Debug data parsing issues");
        readme.AppendLine("- Plan category mappings");
        readme.AppendLine("- Work offline with cached data");
        readme.AppendLine();
        readme.AppendLine($"Generated by Unity project on {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        File.WriteAllText(path, readme.ToString());
    }

    /// <summary>
    /// Log a successful API response to file
    /// </summary>
    private void LogSuccessfulResponse(string url, string jsonResponse)
    {
        if (!saveJsonToFiles || !logSuccessfulRequests) return;

        try
        {
            string filename = GenerateLogFilename(url, "success");
            string filePath = Path.Combine(logFolderPath, filename);

            var logData = new StringBuilder();
            logData.AppendLine($"// API Response Log");
            logData.AppendLine($"// URL: {url}");
            logData.AppendLine($"// Timestamp: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            logData.AppendLine($"// Status: SUCCESS");
            logData.AppendLine();

            // Pretty print JSON if enabled
            string jsonToWrite = prettyPrintJson ? PrettyPrintJson(jsonResponse) : jsonResponse;
            logData.Append(jsonToWrite);

            File.WriteAllText(filePath, logData.ToString());

            if (logRequests)
                Debug.Log($"[HTTP] Response logged to: {filename}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[HTTP] Failed to log response: {e.Message}");
        }
    }

    /// <summary>
    /// Log a failed API response to file
    /// </summary>
    private void LogFailedResponse(string url, string error, int statusCode)
    {
        if (!saveJsonToFiles || !logFailedRequests) return;

        try
        {
            string filename = GenerateLogFilename(url, "failed");
            string filePath = Path.Combine(logFolderPath, filename);

            var logData = new StringBuilder();
            logData.AppendLine($"// API Response Log - FAILED");
            logData.AppendLine($"// URL: {url}");
            logData.AppendLine($"// Timestamp: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            logData.AppendLine($"// Status: FAILED ({statusCode})");
            logData.AppendLine($"// Error: {error}");
            logData.AppendLine();
            logData.AppendLine("// No response data available");

            File.WriteAllText(filePath, logData.ToString());

            if (logRequests)
                Debug.Log($"[HTTP] Error logged to: {filename}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[HTTP] Failed to log error: {e.Message}");
        }
    }

    /// <summary>
    /// Generate a meaningful filename for the log
    /// </summary>
    private string GenerateLogFilename(string url, string status)
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // Extract meaningful parts from URL
        if (url.Contains("dnd5eapi.co"))
        {
            if (url.Contains("/equipment/"))
            {
                // Individual equipment item
                string itemName = ExtractItemNameFromUrl(url);
                return $"dnd5e_equipment_{SanitizeFilename(itemName)}_{timestamp}.json";
            }
            else if (url.Contains("/equipment?"))
            {
                // Equipment search
                string searchTerm = ExtractSearchTermFromUrl(url);
                return $"dnd5e_search_{SanitizeFilename(searchTerm)}_{timestamp}.json";
            }
            else
            {
                return $"dnd5e_other_{timestamp}.json";
            }
        }
        else if (url.Contains("open5e.com"))
        {
            string searchTerm = ExtractSearchTermFromUrl(url);
            return $"open5e_search_{SanitizeFilename(searchTerm)}_{timestamp}.json";
        }
        else
        {
            return $"{status}_unknown_{timestamp}.json";
        }
    }

    /// <summary>
    /// Extract item name from D&D 5e API URL
    /// </summary>
    private string ExtractItemNameFromUrl(string url)
    {
        try
        {
            // Extract from URLs like: https://www.dnd5eapi.co/api/equipment/longsword
            int lastSlash = url.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash < url.Length - 1)
            {
                return url.Substring(lastSlash + 1);
            }
        }
        catch { }

        return "unknown_item";
    }

    /// <summary>
    /// Extract search term from API URL
    /// </summary>
    private string ExtractSearchTermFromUrl(string url)
    {
        try
        {
            // Look for ?name= or ?search=
            if (url.Contains("?name="))
            {
                int nameIndex = url.IndexOf("?name=") + 6;
                int endIndex = url.IndexOf('&', nameIndex);
                if (endIndex < 0) endIndex = url.Length;

                string encoded = url.Substring(nameIndex, endIndex - nameIndex);
                return UnityWebRequest.UnEscapeURL(encoded);
            }
            else if (url.Contains("?search="))
            {
                int searchIndex = url.IndexOf("?search=") + 8;
                int endIndex = url.IndexOf('&', searchIndex);
                if (endIndex < 0) endIndex = url.Length;

                string encoded = url.Substring(searchIndex, endIndex - searchIndex);
                return UnityWebRequest.UnEscapeURL(encoded);
            }
        }
        catch { }

        return "unknown_search";
    }

    /// <summary>
    /// Make filename safe for all operating systems
    /// </summary>
    private string SanitizeFilename(string filename)
    {
        if (string.IsNullOrEmpty(filename)) return "unknown";

        // Replace invalid filename characters
        char[] invalidChars = Path.GetInvalidFileNameChars();
        foreach (char c in invalidChars)
        {
            filename = filename.Replace(c, '_');
        }

        // Also replace some other problematic characters
        filename = filename.Replace(' ', '_');
        filename = filename.Replace('%', '_');
        filename = filename.ToLower();

        // Limit length
        if (filename.Length > 50)
            filename = filename.Substring(0, 50);

        return filename;
    }

    /// <summary>
    /// Pretty print JSON for better readability
    /// </summary>
    private string PrettyPrintJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;

        try
        {
            // Simple pretty printing - add newlines and indentation
            var result = new StringBuilder();
            int indent = 0;
            bool inQuotes = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (c == '"' && (i == 0 || json[i - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                }

                if (!inQuotes)
                {
                    switch (c)
                    {
                        case '{':
                        case '[':
                            result.Append(c);
                            result.AppendLine();
                            indent++;
                            result.Append(new string(' ', indent * 2));
                            break;
                        case '}':
                        case ']':
                            result.AppendLine();
                            indent--;
                            result.Append(new string(' ', indent * 2));
                            result.Append(c);
                            break;
                        case ',':
                            result.Append(c);
                            result.AppendLine();
                            result.Append(new string(' ', indent * 2));
                            break;
                        default:
                            result.Append(c);
                            break;
                    }
                }
                else
                {
                    result.Append(c);
                }
            }

            return result.ToString();
        }
        catch
        {
            // If pretty printing fails, return original
            return json;
        }
    }

    /// <summary>
    /// Open the log folder in the file explorer (Editor only)
    /// </summary>
    [ContextMenu("Open Log Folder")]
    public void OpenLogFolder()
    {
        if (!saveJsonToFiles)
        {
            Debug.LogWarning("[HTTP] JSON logging is disabled");
            return;
        }

        if (string.IsNullOrEmpty(logFolderPath) || !Directory.Exists(logFolderPath))
        {
            Debug.LogWarning($"[HTTP] Log folder doesn't exist: {logFolderPath}");
            return;
        }

#if UNITY_EDITOR
        // Open in file explorer (Windows/Mac)
        System.Diagnostics.Process.Start(logFolderPath);
        Debug.Log($"[HTTP] Opened log folder: {logFolderPath}");
#else
        Debug.Log($"[HTTP] Log folder location: {logFolderPath}");
#endif
    }

    /// <summary>
    /// Clear all log files
    /// </summary>
    [ContextMenu("Clear Log Files")]
    public void ClearLogFiles()
    {
        if (!saveJsonToFiles || string.IsNullOrEmpty(logFolderPath)) return;

        try
        {
            string[] files = Directory.GetFiles(logFolderPath, "*.json");
            foreach (string file in files)
            {
                File.Delete(file);
            }

            Debug.Log($"[HTTP] Cleared {files.Length} log files");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HTTP] Failed to clear log files: {e.Message}");
        }
    }

    /// <summary>
    /// Clear the response cache
    /// </summary>
    public void ClearCache()
    {
        responseCache.Clear();
        Debug.Log("[HTTP] Cache cleared");
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public string GetCacheStats()
    {
        return $"Cache: {responseCache.Count} entries";
    }
}

/// <summary>
/// Response wrapper for HTTP requests
/// </summary>
[System.Serializable]
public class HttpResponse
{
    public bool IsSuccess;
    public int StatusCode;
    public string ResponseText;
    public string Error;
    public Texture2D Texture;  // For image requests

    public bool IsNotFound => StatusCode == 404;
    public bool IsServerError => StatusCode >= 500;
    public bool IsClientError => StatusCode >= 400 && StatusCode < 500;
}