using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using InventorySystem.Data;

/// <summary>
/// Manages player characters in the group
/// Each user can have multiple characters
/// </summary>
public class CharacterManager : MonoBehaviour
{
    [Header("Storage")]
    [SerializeField] private bool enablePersistence = true;
    [SerializeField] private InventoryManager.StorageType storageType = InventoryManager.StorageType.JSON;
    [SerializeField] private bool logging = true;

    [Header("Group/Storage Id")]
    [SerializeField] private string currentGroupId = "group_default";

    private List<PlayerCharacter> characters = new List<PlayerCharacter>();
    private IInventoryStorage storage;

    public Action<List<PlayerCharacter>> OnCharactersChanged;

    public static CharacterManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (storageType == InventoryManager.StorageType.JSON)
            storage = new JsonInventoryStorage(logging: logging);
    }

    private async void Start()
    {
        await LoadCharactersAsync();
    }

    #region Public API

    public IReadOnlyList<PlayerCharacter> GetCharacters()
    {
        return characters.AsReadOnly();
    }

    public PlayerCharacter GetCharacterById(string characterId)
    {
        return characters.FirstOrDefault(c => c.characterId == characterId);
    }

    public List<PlayerCharacter> GetCharactersByUser(string userId)
    {
        return characters.FindAll(c => c.ownerUserId == userId);
    }

    public PlayerCharacter GetActiveCharacterForUser(string userId)
    {
        return characters.FirstOrDefault(c => c.ownerUserId == userId && c.isActive);
    }

    public async Task AddCharacterAsync(PlayerCharacter character)
    {
        if (character == null) return;

        characters.RemoveAll(c => c.characterId == character.characterId); // Replace if exists
        characters.Add(character);

        await SaveCharactersAsync();
        OnCharactersChanged?.Invoke(characters);

        if (logging)
            Debug.Log($"[CharacterManager] Added character: {character.characterName}");
    }

    public async Task RemoveCharacterAsync(string characterId)
    {
        int removed = characters.RemoveAll(c => c.characterId == characterId);
        if (removed > 0)
        {
            await SaveCharactersAsync();
            OnCharactersChanged?.Invoke(characters);

            if (logging)
                Debug.Log($"[CharacterManager] Removed character: {characterId}");
        }
    }

    public async Task SetActiveCharacterAsync(string userId, string characterId)
    {
        // Deactivate all characters for this user
        foreach (var character in characters.Where(c => c.ownerUserId == userId))
        {
            character.isActive = false;
        }

        // Activate the selected character
        var selectedChar = characters.FirstOrDefault(c => c.characterId == characterId);
        if (selectedChar != null)
        {
            selectedChar.isActive = true;
            selectedChar.lastPlayed = DateTime.Now;

            await SaveCharactersAsync();
            OnCharactersChanged?.Invoke(characters);

            if (logging)
                Debug.Log($"[CharacterManager] Set active character: {selectedChar.characterName}");
        }
    }

    #endregion

    #region Persistence

    public async Task LoadCharactersAsync()
    {
        if (!enablePersistence || storage == null) return;

        try
        {
            var data = await storage.LoadAsync(currentGroupId);
            if (data != null && data.characters != null)
            {
                characters = data.characters
                    .Select(sc => sc.ToPlayerCharacter())
                    .ToList();

                if (logging)
                    Debug.Log($"[CharacterManager] Loaded {characters.Count} characters");

                OnCharactersChanged?.Invoke(characters);
            }
            else
            {
                characters = new List<PlayerCharacter>();
                if (logging)
                    Debug.Log("[CharacterManager] No characters found in storage");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterManager] LoadCharactersAsync failed: {e.Message}");
        }
    }

    public async Task SaveCharactersAsync()
    {
        if (!enablePersistence || storage == null) return;

        try
        {
            var data = await storage.LoadAsync(currentGroupId) ?? new InventoryPersistenceData();
            data.groupId = currentGroupId;
            data.lastSaved = DateTime.Now.ToString("O");

            // Update only the characters field
            data.characters = characters
                .Select(SerializablePlayerCharacter.FromPlayerCharacter)
                .ToList();

            await storage.SaveAsync(data);

            if (logging)
                Debug.Log($"[CharacterManager] Saved {characters.Count} characters");
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterManager] SaveCharactersAsync failed: {e.Message}");
        }
    }

    #endregion
}