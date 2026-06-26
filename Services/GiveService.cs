using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using VampireCommandFramework;

namespace PlayerServices.Services;

internal static class GiveService
{
    private static readonly string CONFIG_DIR = Path.Combine(BepInEx.Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
    private static readonly string CONFIG_FILE = Path.Combine(CONFIG_DIR, "gives.json");
    private static readonly string LOG_FILE = Path.Combine(CONFIG_DIR, "give_log.csv");
    private static readonly object LOG_LOCK = new();

    private static Dictionary<string, List<GiveEntry>> _giveSets = new(StringComparer.OrdinalIgnoreCase);
    private static bool _initialized = false;
    
    public class GiveEntry
    {
        public int ItemPrefab { get; set; }
        public int Quantity { get; set; }
    }

    public static void Initialize()
    {
        if (_initialized) return;
        LoadConfig();
        _initialized = true;
    }

    // ---------------------------------------------------------------------
    // Add, Remove, List, Reload
    // ---------------------------------------------------------------------

    public static void AddGiveItem(ChatCommandContext ctx, string setName, string itemPrefab, int quantity)
    {
        if (!TryResolveItemGuid(itemPrefab, out PrefabGUID guid))
        {
            ctx.Reply($"<color=red>Unknown item:</color> {itemPrefab}");
            return;
        }

        setName = setName.Trim();
        if (!_giveSets.TryGetValue(setName, out var list))
        {
            list = new List<GiveEntry>();
            _giveSets[setName] = list;
        }

        var existing = list.Find(x => x.ItemPrefab == guid.GuidHash);
        if (existing != null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            list.Add(new GiveEntry { ItemPrefab = guid.GuidHash, Quantity = quantity });
        }

        SaveToDisk();
        string label = ResolveItemLabel(guid, itemPrefab);
        ctx.Reply($"Added <color=#87CEFA>{label} ×{quantity}</color> to give set <color=white>{setName}</color>.");
        Core.Log.LogInfo($"[Give] Added {label} x{quantity} to {setName}");
    }

    public static void RemoveGiveSet(ChatCommandContext ctx, string setName)
    {
        setName = setName.Trim();
        if (_giveSets.Remove(setName))
        {
            SaveToDisk();
            ctx.Reply($"Removed give set <color=white>{setName}</color>.");
            Core.Log.LogInfo($"[Give] Removed set {setName}");
        }
        else
        {
            ctx.Reply($"<color=yellow>Give set not found:</color> {setName}");
        }
    }

    public static void ListGiveSets(ChatCommandContext ctx)
    {
        if (_giveSets.Count == 0)
        {
            ctx.Reply("<color=yellow>No give sets found.</color>");
            return;
        }

        foreach (var kvp in _giveSets)
        {
            var sb = new StringBuilder();
            sb.Append($"Give sets <color=#87CEFA>{kvp.Key}</color>:\n");
            
            var items = new List<string>();
            foreach (var entry in kvp.Value)
            {
                var guid = new PrefabGUID(entry.ItemPrefab);
                string label = ResolveItemLabel(guid, guid.GuidHash.ToString());
                items.Add($"{label} ×{entry.Quantity}");
            }
            sb.Append(string.Join(", ", items));
            ctx.Reply(sb.ToString());
        }
    }

    // ---------------------------------------------------------------------
    // Give Player, Radius, Clan
    // ---------------------------------------------------------------------

    public static void GiveToPlayer(ChatCommandContext ctx, string playerName, string setName, int multiplier)
    {
        if (!VerifyAndGetSet(ctx, setName, multiplier, out var itemsToGive, out string descriptor)) return;

        if (!Helper.TryFindUserByName(playerName, out var targetUserEnt, out var targetUser, out var candidates))
        {
            HandleFindPlayer(ctx, playerName, candidates);
            return;
        }

        if (!targetUser.IsConnected)
        {
            ctx.Reply($"<color=white>{targetUser.CharacterName}</color> <color=yellow>is offline. Cannot send give.</color>");
            return;
        }

        var targetChar = targetUser.LocalCharacter.GetEntityOnServer();
        if (GiveItemsToCharacter(targetChar, itemsToGive))
        {
            string displayName = targetUser.CharacterName.ToString();
            string adminName = ctx.Event.SenderUserEntity.Read<User>().CharacterName.ToString();
            
            Helper.NotifyUser(targetUserEnt, $"You received a give: <color=#87CEFA>{descriptor}</color>.");
            ctx.Reply($"Gave <color=#87CEFA>{descriptor}</color> to <color=white>{displayName}</color>.");
            
            CsvLogger.LogRow("player", setName, displayName, multiplier, adminName);
        }
    }

    public static void GiveToRadius(ChatCommandContext ctx, float radius, string setName, int multiplier)
    {
        if (!VerifyAndGetSet(ctx, setName, multiplier, out var itemsToGive, out string descriptor))
            return;

        var senderChar = ctx.Event.SenderCharacterEntity;
        string adminName = ctx.Event.SenderUserEntity.Read<User>().CharacterName.ToString();

        if (!Helper.TryFindUsersByRadius(senderChar, radius, out var players))
        {
            ctx.Reply($"<color=yellow>No eligible players found within {radius:0.#}m.</color>");
            return;
        }

        int successCount = 0;

        foreach (var target in players)
        {
            if (GiveItemsToCharacter(target.CharacterEntity, itemsToGive))
            {
                successCount++;

                string displayName = target.User.CharacterName.ToString();

                Helper.NotifyUser(target.UserEntity, $"You received a give: <color=#87CEFA>{descriptor}</color>.");

                CsvLogger.LogRow("radius", setName, displayName, multiplier, adminName);
            }
        }

        if (successCount > 0)
        {
            ctx.Reply($"Gave <color=#87CEFA>{descriptor}</color> to <color=#98FB98>{successCount}</color> players within {radius:0.#}m.");
        }
        else
        {
            ctx.Reply($"<color=yellow>No eligible players found within {radius:0.#}m.</color>");
        }
    }

	public static void GiveToClan(ChatCommandContext ctx, string playerName, string setName, int multiplier)
	{
		if (!VerifyAndGetSet(ctx, setName, multiplier, out var itemsToGive, out string descriptor)) return;

		if (!Helper.TryFindUserByName(playerName, out var refUserEnt, out var refUser, out var candidates))
		{
			HandleFindPlayer(ctx, playerName, candidates);
			return;
		}

		Entity refClanEntity = refUser.ClanEntity._Entity;
		if (refClanEntity == Entity.Null)
		{
			ctx.Reply($"<color=white>{refUser.CharacterName}</color> is not in a clan.");
			return;
		}

		string adminName = ctx.Event.SenderUserEntity.Read<User>().CharacterName.ToString();
		var users = Helper.GetEntitiesByComponentType<User>();
		int successCount = 0;

		try
		{
			foreach (var uEnt in users)
			{
				var u = uEnt.Read<User>();
				if (!u.IsConnected || u.IsAdmin) continue;
				if (u.ClanEntity._Entity != refClanEntity) continue;

				var ch = u.LocalCharacter.GetEntityOnServer();
				if (ch == Entity.Null) continue;

				if (GiveItemsToCharacter(ch, itemsToGive))
				{
					successCount++;
					string displayName = u.CharacterName.ToString();
					Helper.NotifyUser(uEnt, $"You received a give: <color=#87CEFA>{descriptor}</color>.");

					CsvLogger.LogRow("clan", setName, displayName, multiplier, adminName);
				}
			}
		}
		finally
		{
			if (users.IsCreated)
				users.Dispose();
		}

		if (successCount > 0)
			ctx.Reply($"Gave <color=#87CEFA>{descriptor}</color> to <color=#98FB98>{successCount}</color> online members of <color=white>{refUser.CharacterName}</color>'s clan.");
		else
			ctx.Reply($"<color=yellow>No eligible clan members found online for {refUser.CharacterName}.</color>");
	}

    // ---------------------------------------------------------------------
    // Utilities & Helpers
    // ---------------------------------------------------------------------

    public static void ReplyHelp(ChatCommandContext ctx)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("<color=yellow>Give Commands:</color>");
        sb.AppendLine("<color=green>.pls give <player> <setname> (multiplier)</color>");
        sb.AppendLine("<color=green>.pls giveradius <radius> <setname> (multiplier)</color>");
        sb.AppendLine("<color=green>.pls giveclan <player> <setname> (multiplier)</color>");
        sb.AppendLine("<color=green>.pls addgive <setname> <item> <quantity></color>");
        sb.AppendLine("<color=green>.pls removegive <setname></color>");
        sb.AppendLine("<color=green>.pls listgive</color>");
        
        ctx.Reply(sb.ToString().TrimEnd());
    }

    private static bool VerifyAndGetSet(ChatCommandContext ctx, string setName, int multiplier, out List<GiveEntry> finalItems, out string descriptor)
    {
        finalItems = new List<GiveEntry>();
        descriptor = string.Empty;

        if (!_giveSets.TryGetValue(setName, out var baseList) || baseList.Count == 0)
        {
            ctx.Reply($"<color=yellow>Give set not found or empty:</color> <color=white>{setName}</color>");
            return false;
        }

        foreach (var item in baseList)
        {
            long totalQuantity = (long)item.Quantity * multiplier;
            if (totalQuantity > 0)
            {
                finalItems.Add(new GiveEntry 
                { 
                    ItemPrefab = item.ItemPrefab, 
                    Quantity = (int)Math.Clamp(totalQuantity, 1, int.MaxValue) 
                });
            }
        }

        descriptor = multiplier > 1 ? $"{setName} ×{multiplier}" : setName;
        return true;
    }

    private static bool GiveItemsToCharacter(Entity character, List<GiveEntry> items)
    {
        bool success = false;
        foreach (var item in items)
        {
            try 
            { 
                Helper.AddItemToInventory(character, new PrefabGUID(item.ItemPrefab), item.Quantity); 
                success = true; 
            }
        catch (Exception ex)
        {
            Core.Log.LogError($"[Give] Failed to give item to character: {ex}");
        }
        }
        return success;
    }

    private static void HandleFindPlayer(ChatCommandContext ctx, string playerName, List<string> candidates)
    {
        if (string.IsNullOrWhiteSpace(playerName) || playerName.Trim().Length < 2)
        {
            ctx.Reply("<color=yellow>Please enter at least 2 characters.</color>");
            return;
        }

        playerName = playerName.Trim();

        if (candidates != null && candidates.Count > 0)
        {
            if (candidates.Count > 10)
            {
                ctx.Reply($"<color=yellow>Too many players found for</color> <color=white>{playerName}</color>. Please enter a more specific name.");
            }
            else
            {
                ctx.Reply($"<color=yellow>Multiple players matched</color> <color=white>{playerName}</color>: {string.Join(", ", candidates)}");
            }
        }
        else
        {
            ctx.Reply($"<color=red>Player not found:</color> <color=white>{playerName}</color>");
        }
    }

    private static bool TryResolveItemGuid(string token, out PrefabGUID guid)
    {
        token = token?.Trim() ?? "";
        
        if (token.StartsWith("Prefabs.", StringComparison.OrdinalIgnoreCase))
            token = token.Substring("Prefabs.".Length);

        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
        {
            guid = new PrefabGUID(id);
            return true;
        }

        guid = default;
        return false;
    }

    private static string ResolveItemLabel(PrefabGUID guid, string fallbackInput)
    {
        try
        {
            string name = guid.LookupName();
            
            if (name != "GUID Not Found") 
            {
                int idx = name.IndexOf(" PrefabGuid");
                if (idx > 0) return name.Substring(0, idx);
                return name;
            }
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[Give] Failed to resolve item label: {ex}");
        }
        
        return string.IsNullOrWhiteSpace(fallbackInput) ? guid.GuidHash.ToString() : fallbackInput;
    }

    // ---------------------------------------------------------------------
    // Disk IO & Logging
    // ---------------------------------------------------------------------
    
    private static void EnsureConfigFileExists()
    {
        if (File.Exists(CONFIG_FILE))
            return;

        File.WriteAllText(CONFIG_FILE, GetDefaultJson(), new UTF8Encoding(false));
    }

	public static bool ReloadConfig(out int setCount)
	{
		bool ok = LoadConfig();
		setCount = _giveSets.Count;
		return ok;
	}

    private static bool LoadConfig()
    {
        try
        {
            Directory.CreateDirectory(CONFIG_DIR);
            EnsureConfigFileExists();

            string json = File.ReadAllText(CONFIG_FILE, new UTF8Encoding(false));
            var parsed = JsonSerializer.Deserialize<Dictionary<string, List<GiveEntry>>>(json);
            
            _giveSets.Clear();
            if (parsed != null)
            {
                foreach (var kvp in parsed)
                {
                    _giveSets[kvp.Key] = kvp.Value ?? new List<GiveEntry>();
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[Give] Failed to reload gives.json: {ex}");
        }

        return false;
    }

    private static void SaveToDisk()
    {
        try
        {
            Directory.CreateDirectory(CONFIG_DIR);
            string json = JsonSerializer.Serialize(_giveSets, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(CONFIG_FILE, json, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[Give] Failed to save gives.json: {ex}");
        }
    }

    private static string GetDefaultJson()
    {
        var defaults = new Dictionary<string, List<GiveEntry>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Potion"] = new List<GiveEntry>
            {
                new() { ItemPrefab = 429052660, Quantity = 10 },
                new() { ItemPrefab = 800879747, Quantity = 10 }
            },
            ["Buff"] = new List<GiveEntry>
            {
                new() { ItemPrefab = 1510182325, Quantity = 1 },
                new() { ItemPrefab = -1568756102, Quantity = 1 },
                new() { ItemPrefab = 541321301, Quantity = 1 },
                new() { ItemPrefab = -38051433, Quantity = 1 },
                new() { ItemPrefab = 970650569, Quantity = 1 }
            }
        };

        return JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true });
    }

    private static class CsvLogger
    {
        internal static void LogRow(string method, string setName, string targetName, int multiplier, string adminName)
        {
            try
            {
                lock (LOG_LOCK)
                {
                    Directory.CreateDirectory(CONFIG_DIR);
                    bool newFile = !File.Exists(LOG_FILE);
                    
                    using var fs = new FileStream(LOG_FILE, FileMode.Append, FileAccess.Write, FileShare.Read);
                    using var sw = new StreamWriter(fs, new UTF8Encoding(false));

                    if (newFile) sw.WriteLine("time,method,setname,target_player,multiplier,admin_name");

                    string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    sw.WriteLine($"{ts},{Helper.Csv(method)},{Helper.Csv(setName)},{Helper.Csv(targetName)},{multiplier},{Helper.Csv(adminName)}");
                }
            }
            catch (Exception e)
            {
                Core.LogException(e);
            }
        }
    }
}