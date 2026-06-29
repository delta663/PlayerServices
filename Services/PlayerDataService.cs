using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ProjectM;
using ProjectM.CastleBuilding;
using ProjectM.Network;
using Unity.Entities;
using UnityEngine;
using PlayerServices.Data;

namespace PlayerServices.Services;

internal static class PlayerDataService
{
    private static readonly string CONFIG_DIR = Path.Combine(BepInEx.Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
    private static readonly string CONFIG_FILE = Path.Combine(CONFIG_DIR, "player_data.json");
    private static readonly string BACKUP_FILE = CONFIG_FILE + ".bak";

    private static float SCAN_INTERVAL = 10f;
    private static readonly TimeSpan PROFILE_SAVE_INTERVAL = TimeSpan.FromSeconds(60);
    
    private static Dictionary<ulong, PlayerCacheData> _cache = new();
    private static DateTime _lastProfileSaveTime = DateTime.MinValue;
    private static bool _profileDirty = false;
    private static bool _isInitialized = false;
    private static bool _saveDisabledDueToLoadFailure = false;
    private static bool _skipBackupCopyOnNextSave = false;

    public static void Initialize()
    {
        if (_isInitialized) return;
        
        Directory.CreateDirectory(CONFIG_DIR);
        LoadData();
        Core.StartCoroutine(MonitorPlayersCoroutine());
        _isInitialized = true;

        Core.Log.LogInfo($"[PlayerData] Initialized and loaded {_cache.Count} player(s).");
    }

    // ---------------------------------------------------------------------
    // Monitor Players
    // ---------------------------------------------------------------------

	private static IEnumerator MonitorPlayersCoroutine()
	{
		while (true)
		{
			try
			{
				MonitorPlayersTick();
			}
			catch (Exception e)
			{
				Core.LogException(e);
			}

			yield return new WaitForSeconds(SCAN_INTERVAL);
		}
	}

    private static void MonitorPlayersTick()
	{
		var ownerRegionMap = BuildOwnerRegionMap();

		bool profileChanged = UpdatePlayerCaches(ownerRegionMap);

		if (profileChanged)
		{
			MarkProfileDirty();
		}

		SaveProfileIfNeeded();
	}
 
    // ---------------------------------------------------------------------
    // Update Caches
    // ---------------------------------------------------------------------

	private static bool UpdatePlayerCaches(Dictionary<Entity, Dictionary<string, int>> ownerRegionMap)
	{
		bool profileChanged = false;

		var userEntities = Helper.GetEntitiesByComponentType<User>();

		try
		{
			foreach (var userEntity in userEntities)
			{
				if (!Core.EntityManager.Exists(userEntity) || !Core.EntityManager.HasComponent<User>(userEntity))
					continue;

				var user = userEntity.Read<User>();
				if (user.PlatformId == 0)
					continue;

				if (UpdatePlayerCacheFromUser(userEntity, user, ownerRegionMap))
				{
					profileChanged = true;
				}
			}
		}
		finally
		{
			if (userEntities.IsCreated)
				userEntities.Dispose();
		}

		return profileChanged;
	}

	private static bool UpdatePlayerCacheFromUser(Entity userEntity, User user, Dictionary<Entity, Dictionary<string, int>> ownerRegionMap)
	{
		bool profileChanged = false;

		ulong steamId = user.PlatformId;
		string charName = user.CharacterName.ToString();

		ownerRegionMap.TryGetValue(userEntity, out var currentRegions);
		currentRegions ??= new Dictionary<string, int>();

		if (!_cache.TryGetValue(steamId, out var data))
		{
			if (Plugin.onlyWhitelistEnable.Value)
				return false;

			data = new PlayerCacheData
			{
				SteamID = steamId,
				KnownAs = string.Empty,
                IsWhitelisted = false
			};

			_cache[steamId] = data;
			profileChanged = true;
		}

		if (data.InGameName != charName)
		{
			data.InGameName = charName;
			profileChanged = true;
		}

		int currentLevel = GetCurrentPlayerLevel(user, data.CurrentLevel);

		if (data.CurrentLevel != currentLevel)
		{
			data.CurrentLevel = currentLevel;
			profileChanged = true;
		}

		if (currentLevel > data.MaxLevel)
		{
			data.MaxLevel = currentLevel;
			profileChanged = true;
		}

		if (!DictionariesEqual(data.CastleRegions, currentRegions))
		{
			data.CastleRegions = currentRegions;
			profileChanged = true;
		}

		return profileChanged;
	}

	private static int GetCurrentPlayerLevel(User user, int fallbackLevel)
	{
		var charEntity = user.LocalCharacter.GetEntityOnServer();

		if (charEntity.Equals(Entity.Null) || !Core.EntityManager.Exists(charEntity) || !Core.EntityManager.HasComponent<Equipment>(charEntity))
			return fallbackLevel;

		var equipment = Core.EntityManager.GetComponentData<Equipment>(charEntity);

		return Mathf.RoundToInt(equipment.ArmorLevel + equipment.SpellLevel + equipment.WeaponLevel);
	}

    // ---------------------------------------------------------------------
    // Castle Regions
    // ---------------------------------------------------------------------
    
    private static Dictionary<Entity, Dictionary<string, int>> BuildOwnerRegionMap()
	{
		var ownerRegionMap = new Dictionary<Entity, Dictionary<string, int>>();

		var territories = Helper.GetEntitiesByComponentType<CastleTerritory>();

		try
		{
			foreach (var terrEnt in territories)
			{
				if (!Core.EntityManager.Exists(terrEnt))
					continue;

				var terr = terrEnt.Read<CastleTerritory>();
				if (terr.CastleHeart.Equals(Entity.Null))
					continue;

				var castleHeart = terr.CastleHeart;

				if (!Core.EntityManager.Exists(castleHeart) || !Core.EntityManager.HasComponent<UserOwner>(castleHeart))
					continue;

				var owner = castleHeart.Read<UserOwner>().Owner.GetEntityOnServer();
				if (owner.Equals(Entity.Null))
					continue;

				if (!Core.EntityManager.HasComponent<ProjectM.Terrain.TerritoryWorldRegion>(terrEnt))
					continue;

				var regionName = terrEnt.Read<ProjectM.Terrain.TerritoryWorldRegion>().Region.ToString();

				if (!ownerRegionMap.TryGetValue(owner, out var dict))
					ownerRegionMap[owner] = dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

				dict.TryGetValue(regionName, out var c);
				dict[regionName] = c + 1;
			}
		}
		finally
		{
			if (territories.IsCreated)
				territories.Dispose();
		}

		return ownerRegionMap;
	}

    private static bool DictionariesEqual(Dictionary<string, int> dict1, Dictionary<string, int> dict2)
    {
        if (dict1 == dict2) return true;
        if (dict1 == null || dict2 == null) return false;
        if (dict1.Count != dict2.Count) return false;
        foreach (var kvp in dict1)
        {
            if (!dict2.TryGetValue(kvp.Key, out int val) || val != kvp.Value) return false;
        }
        return true;
    }

    // ---------------------------------------------------------------------
    // Cache Data
    // ---------------------------------------------------------------------

    public static IEnumerable<PlayerCacheData> GetAllPlayerCaches()
    {
        return _cache.Values;
    }

    public static PlayerCacheData GetPlayerCache(ulong steamId)
    {
        return _cache.TryGetValue(steamId, out var data) ? data : null;
    }

    public static PlayerCacheData GetOrCreatePlayerCache(ulong steamId)
    {
        if (!_cache.TryGetValue(steamId, out var data))
        {
            data = new PlayerCacheData 
            { 
                SteamID = steamId, 
                InGameName = string.Empty, 
                KnownAs = string.Empty,
                LastDailyKitClaim = string.Empty,
                IsBanned = false,
                IsWhitelisted = false
            };
            _cache[steamId] = data;
        }
        return data;
    }

    public static PlayerCacheData GetPlayerCacheByName(string inGameName)
    {
        foreach (var cache in _cache.Values)
        {
            if (!string.IsNullOrEmpty(cache.InGameName) && 
                string.Equals(cache.InGameName, inGameName, StringComparison.OrdinalIgnoreCase))
            {
                return cache;
            }
        }
        return null;
    }
    
    public static List<PlayerCacheData> GetBannedPlayers()
    {
        var list = new List<PlayerCacheData>();
        foreach (var cache in _cache.Values)
        {
            if (cache.IsBanned)
            {
                list.Add(cache);
            }
        }
        return list;
    }
    
    public static void RemovePlayerCache(ulong steamId)
    {
        if (_cache.ContainsKey(steamId))
        {
            _cache.Remove(steamId);
            SaveData();
        }
    }

    // ---------------------------------------------------------------------
    // Last Online
    // ---------------------------------------------------------------------

    public static void RecordDisconnectedUser(User user)
    {
	    if (user.PlatformId == 0)
		    return;

	    var cache = GetPlayerCache(user.PlatformId);

	    if (cache == null)
	    {
		    if (Plugin.onlyWhitelistEnable.Value)
			    return;

		    cache = GetOrCreatePlayerCache(user.PlatformId);
	    }

	    string currentName = user.CharacterName.ToString();

	    if (!string.IsNullOrWhiteSpace(currentName) && cache.InGameName != currentName)
	    {
		    cache.InGameName = currentName;
	    }

	    cache.LastOnlineTicks = DateTime.UtcNow.Ticks;

	    SaveNow();

	    Core.Log.LogInfo($"[Disconnected] Player disconnected: {currentName} ({user.PlatformId}) | LastOnlineTicks updated");
    }

    public static TimeSpan GetTimeSinceLastOnline(long lastOnlineUtcTicks)
    {
	    if (lastOnlineUtcTicks <= 0)
		    return TimeSpan.Zero;

	    try
	    {
		    var lastOnlineUtc = new DateTime(lastOnlineUtcTicks, DateTimeKind.Utc);
		    var elapsed = DateTime.UtcNow - lastOnlineUtc;

		    if (elapsed < TimeSpan.Zero)
			    return TimeSpan.Zero;

		    return elapsed;
	    }
	    catch
	    {
		    return TimeSpan.Zero;
	    }
    }

    // ---------------------------------------------------------------------
    // Find Player
    // ---------------------------------------------------------------------

    public static bool TryFindPlayerCacheByPlayerName(string query, out PlayerCacheData cacheData, out List<string> candidates)
    {
        cacheData = null;
        candidates = null;

        var all = _cache.Values.Where(c => !string.IsNullOrWhiteSpace(c.InGameName)).ToList();

        var exact = all.Where(x => string.Equals(x.InGameName, query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 1)
        {
            cacheData = exact[0];
            return true;
        }

        var contains = all.Where(x => x.InGameName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        if (contains.Count == 1)
        {
            cacheData = contains[0];
            return true;
        }

        if (contains.Count > 1)
        {
            candidates = contains.Select(x => x.InGameName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return false;
    }

    public static bool TryFindPlayersCacheByKnownAs(string query, out List<PlayerCacheData> matchedProfiles, out List<string> candidates)
    {
        matchedProfiles = null;
        candidates = null;

        var allWithKnownAs = _cache.Values.Where(c => !string.IsNullOrWhiteSpace(c.KnownAs)).ToList();

        var exactMatchedNames = allWithKnownAs
            .Where(x => string.Equals(x.KnownAs, query, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.KnownAs)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (exactMatchedNames.Count == 1)
        {
            string exactName = exactMatchedNames[0];
            matchedProfiles = allWithKnownAs.Where(x => string.Equals(x.KnownAs, exactName, StringComparison.OrdinalIgnoreCase)).ToList();
            return true;
        }

        var partialMatchedNames = allWithKnownAs
            .Where(x => x.KnownAs.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            .Select(x => x.KnownAs)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (partialMatchedNames.Count == 1)
        {
            string partialName = partialMatchedNames[0];
            matchedProfiles = allWithKnownAs.Where(x => string.Equals(x.KnownAs, partialName, StringComparison.OrdinalIgnoreCase)).ToList();
            return true;
        }

        if (partialMatchedNames.Count > 1)
        {
            candidates = partialMatchedNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        return false;
    }

    // ---------------------------------------------------------------------
    // Disk Load Save Backup
    // ---------------------------------------------------------------------

    private static void LoadData()
    {
        _saveDisabledDueToLoadFailure = false;
        _skipBackupCopyOnNextSave = false;

        if (TryLoadPlayerData(CONFIG_FILE, out var data))
        {
            _cache = data;
            return;
        }

        if (File.Exists(CONFIG_FILE))
        {
            Core.Log.LogWarning("[PlayerData] Failed to load player_data.json. Trying backup file.");
        }

        if (TryLoadPlayerData(BACKUP_FILE, out data))
        {
            _cache = data;
            Core.Log.LogWarning("[PlayerData] Loaded player data from player_data.json.bak.");

            _skipBackupCopyOnNextSave = !RestoreBackupToPrimaryFile();
            return;
        }

        if (File.Exists(CONFIG_FILE) || File.Exists(BACKUP_FILE))
        {
            _saveDisabledDueToLoadFailure = true;
            Core.Log.LogError("[PlayerData] Failed to load both player_data.json and player_data.json.bak. Automatic saving is disabled to avoid overwriting existing data.");
        }
    }

    private static bool TryLoadPlayerData(string path, out Dictionary<ulong, PlayerCacheData> data)
    {
        data = null;

        if (!File.Exists(path))
            return false;

        try
        {
            var json = File.ReadAllText(path);
            data = JsonSerializer.Deserialize<Dictionary<ulong, PlayerCacheData>>(json);

            if (data == null)
            {
                Core.Log.LogWarning($"[PlayerData] {Path.GetFileName(path)} contained no player data.");
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            Core.Log.LogWarning($"[PlayerData] Failed to read {Path.GetFileName(path)}: {e.Message}");
            return false;
        }
    }

    private static bool RestoreBackupToPrimaryFile()
    {
        try
        {
            Directory.CreateDirectory(CONFIG_DIR);
            File.Copy(BACKUP_FILE, CONFIG_FILE, true);

            Core.Log.LogWarning("[PlayerData] Restored player_data.json from backup.");
            return true;
        }
        catch (Exception e)
        {
            Core.Log.LogError($"[PlayerData] Loaded backup successfully, but failed to restore player_data.json: {e}");
            return false;
        }
    }

    public static void SaveData()
    {
        if (_saveDisabledDueToLoadFailure)
        {
            Core.Log.LogError("[PlayerData] Save skipped because player data failed to load. Fix player_data.json or player_data.json.bak, then restart or reload the plugin.");
            return;
        }

        try
        {
            Directory.CreateDirectory(CONFIG_DIR);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_cache, options);

            string tempFile = CONFIG_FILE + ".tmp";

            File.WriteAllText(tempFile, json);

            if (File.Exists(CONFIG_FILE) && !_skipBackupCopyOnNextSave)
            {
                File.Copy(CONFIG_FILE, BACKUP_FILE, true);
            }
            else if (_skipBackupCopyOnNextSave)
            {
                Core.Log.LogWarning("[PlayerData] Skipped backup copy this save because the primary file was not trusted after loading from backup.");
            }

            File.Move(tempFile, CONFIG_FILE, true);
            _skipBackupCopyOnNextSave = false;

            _profileDirty = false;
            _lastProfileSaveTime = DateTime.Now;
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    private static void MarkProfileDirty()
    {
        _profileDirty = true;
    }

    private static void SaveProfileIfNeeded()
    {
        if (!_profileDirty)
            return;

        if (DateTime.Now - _lastProfileSaveTime < PROFILE_SAVE_INTERVAL)
            return;
        
        Core.Log.LogInfo($"[PlayerData] Saving scheduled player data for {_cache.Count} player(s).");

        SaveData();
    }

    public static void ForceSaveIfDirty()
    {
        if (!_profileDirty)
            return;

        SaveData();
    }

    public static void SaveNow()
    {
        SaveData();
    }
}
