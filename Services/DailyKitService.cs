using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ProjectM.Network;
using Stunlock.Core;
using VampireCommandFramework;

namespace PlayerServices.Services;

internal static class DailyKitService
{
    private static readonly string CONFIG_DIR = Path.Combine(BepInEx.Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
    private static readonly string LOG_FILE = Path.Combine(CONFIG_DIR, "dailykit_log.csv");
    private static readonly object LOG_LOCK = new();

    private static List<DailyKitEntry> _dailykitItems = new();

    public class DailyKitEntry
    {
        public int ItemPrefab { get; set; }
        public int Quantity { get; set; }
    }

    public static void Initialize()
    {
        LoadConfig();
    }

    // ---------------------------------------------------------------------
    // Add, Remove, List, Claim
    // ---------------------------------------------------------------------

    public static void AddKit(ChatCommandContext ctx, int prefabGuid, int quantity)
	{
		if (quantity <= 0)
		{
			ctx.Reply("<color=red>Quantity must be greater than 0.</color>");
			return;
		}

		var guid = new PrefabGUID(prefabGuid);
		string label = ResolveItemLabel(guid, prefabGuid.ToString());

		var existingItem = _dailykitItems.Find(i => i.ItemPrefab == prefabGuid);
		if (existingItem != null)
		{
			existingItem.Quantity = quantity;
			ctx.Reply($"Updated daily kit item <color=#87CEFA>{label}</color> to <color=#FFD700>{quantity}</color>.");
			Core.Log.LogInfo($"[DailyKit] Updated daily kit item {label} ({prefabGuid}) to {quantity}.");
		}
		else
		{
			_dailykitItems.Add(new DailyKitEntry { ItemPrefab = prefabGuid, Quantity = quantity });
			ctx.Reply($"Added <color=#87CEFA>{label}</color> ×<color=#FFD700>{quantity}</color> to the daily kit.");
			Core.Log.LogInfo($"[DailyKit] Added {label} ({prefabGuid}) x{quantity} to the daily kit.");
		}

		SaveToConfig();
	}

    public static void RemoveKit(ChatCommandContext ctx, int prefabGuid)
    {
        int removedCount = _dailykitItems.RemoveAll(i => i.ItemPrefab == prefabGuid);
        if (removedCount > 0)
        {
            ctx.Reply($"Removed item <color=#87CEFA>{prefabGuid}</color> from the daily kit.");
            Core.Log.LogInfo($"[DailyKit] Removed item {prefabGuid} from the daily kit.");
            SaveToConfig();
        }
        else
        {
            ctx.Reply($"<color=yellow>Item not found in the daily kit: {prefabGuid}</color>");
        }
    }

	public static void ListKit(ChatCommandContext ctx)
	{
		var sb = new StringBuilder();

		bool isEnabled = Plugin.dailyKitEnabled.Value;
		string status = isEnabled ? "Enabled" : "Disabled";

		sb.AppendLine($"Daily Kit Status: {status}");

		if (_dailykitItems.Count == 0)
		{
			sb.AppendLine("(Empty)");
			ctx.Reply(sb.ToString().TrimEnd());
			return;
		}

		int shown = 0;

		foreach (var item in _dailykitItems)
		{
			var guid = new PrefabGUID(item.ItemPrefab);
			string label = ResolveItemLabel(guid, guid.GuidHash.ToString());
			string line = $"{label} ×{item.Quantity}";

			if (sb.Length + line.Length + Environment.NewLine.Length > Core.MAX_REPLY_LENGTH)
				break;

			sb.AppendLine(line);
			shown++;
		}

		int hidden = _dailykitItems.Count - shown;

		if (hidden > 0)
		{
			string moreLine = $"...and {hidden} more item(s).";

			if (sb.Length + moreLine.Length + Environment.NewLine.Length <= Core.MAX_REPLY_LENGTH)
			{
				sb.AppendLine(moreLine);
			}
			else
			{
				string dots = "...";

				if (sb.Length + dots.Length + Environment.NewLine.Length <= Core.MAX_REPLY_LENGTH)
					sb.AppendLine(dots);
			}
		}

		ctx.Reply(sb.ToString().TrimEnd());
	}

    /*
    public static void ReplyHelp(ChatCommandContext ctx)
    {
        var sb = new StringBuilder();

		bool isEnabled = Plugin.dailyKitEnabled.Value;
        string status = isEnabled ? "<color=green>Enabled</color>" : "<color=red>Disabled</color>";

		sb.AppendLine("<color=yellow>Daily Kit Commands:</color>");
		sb.AppendLine("<color=green>.pls adddailykit <prefabGuid> <quantity></color>");
		sb.AppendLine("<color=green>.pls removedailykit <prefabGuid></color>");
		sb.AppendLine("<color=green>.pls listdailykit</color>");
        sb.AppendLine($"Daily Kit Status: {status}");

        ctx.Reply(sb.ToString().TrimEnd());
    }
    */

    private static string ResolveItemLabel(PrefabGUID guid, string fallbackInput)
	{
		try
		{
			string name = guid.LookupName();

			if (name != "GUID Not Found")
			{
				int idx = name.IndexOf(" PrefabGuid", StringComparison.Ordinal);
				if (idx > 0) return name.Substring(0, idx);
				return name;
			}
		}
		catch (Exception ex)
		{
			Core.Log.LogError($"[DailyKit] Failed to resolve item label: {ex}");
		}

		return string.IsNullOrWhiteSpace(fallbackInput) ? guid.GuidHash.ToString() : fallbackInput;
	}

    public static void ClaimKit(ChatCommandContext ctx)
    {
        if (!Plugin.dailyKitEnabled.Value)
        {
            ctx.Reply("<color=red>Daily kit feature is currently disabled.</color>");
            return;
        }

        var userEntity = ctx.Event.SenderUserEntity;
        var charEntity = ctx.Event.SenderCharacterEntity;
        var user = userEntity.Read<User>();
        ulong steamId = user.PlatformId;
        string playerName = user.CharacterName.ToString();

        var cache = PlayerDataService.GetPlayerCache(steamId);
        if (cache == null)
        {
            ctx.Reply("<color=yellow>System is initializing your data. Please wait a few seconds and try again.</color>");
            return;
        }

        string todayDate = DateTime.Now.ToString("yyyy-MM-dd");
        if (cache.LastDailyKitClaim == todayDate)
        {
            ctx.Reply("<color=yellow>You have already claimed your daily kit today.</color>");
            return;
        }

        if (_dailykitItems.Count == 0)
        {
            ctx.Reply("<color=yellow>The daily kit is currently empty. Please contact an admin.</color>");
            return;
        }

        int successCount = 0;
        foreach (var entry in _dailykitItems)
        {
            if (entry.Quantity <= 0) continue;
            
            try
            {
                var prefab = new PrefabGUID(entry.ItemPrefab); 
                Helper.AddItemToInventory(charEntity, prefab, entry.Quantity);
                successCount++;
            }
            catch (Exception ex)
            {
                Core.LogException(ex);
            }
        }

        if (successCount == 0)
        {
            ctx.Reply("<color=red>Failed to give the daily kit.</color> An error occurred.");
            Core.Log.LogError($"[DailyKit] Failed to give the daily kit to {playerName}.");
            CsvLogger.LogRow(steamId, playerName, "fail");
            return;
        }

        cache.LastDailyKitClaim = todayDate;
        PlayerDataService.SaveData();

        ctx.Reply("<color=green>Successfully claimed your daily kit.</color>");
        Core.Log.LogInfo($"[DailyKit] {playerName} has claimed a daily kit.");
        CsvLogger.LogRow(steamId, playerName, "success");
    }

    // ---------------------------------------------------------------------
    // Disk IO & Logging
    // ---------------------------------------------------------------------
	
    public static int ReloadConfig()
	{
		LoadConfig();
		return _dailykitItems.Count;
	}

    private static void LoadConfig()
	{
		_dailykitItems.Clear();
		string rawItems = Plugin.dailyKitItems.Value;

		if (string.IsNullOrWhiteSpace(rawItems))
			return;

		string[] pairs = rawItems.Split(',');

		for (int i = 0; i < pairs.Length; i++)
		{
			string pair = pairs[i].Trim();

			if (string.IsNullOrWhiteSpace(pair))
			{
				Core.Log.LogWarning($"[DailyKit] Invalid config entry at position {i + 1}: empty entry.");
				continue;
			}

			string[] split = pair.Split(':');

			if (split.Length != 2)
			{
				Core.Log.LogWarning($"[DailyKit] Invalid config entry at position {i + 1}: \"{pair}\". Expected format: ItemPrefab:Quantity");
				continue;
			}

			string prefabText = split[0].Trim();
			string quantityText = split[1].Trim();

			if (!int.TryParse(prefabText, out int prefab))
			{
				Core.Log.LogWarning($"[DailyKit] Invalid item prefab at position {i + 1}: \"{prefabText}\".");
				continue;
			}

			if (!int.TryParse(quantityText, out int qty))
			{
				Core.Log.LogWarning($"[DailyKit] Invalid quantity at position {i + 1}: \"{quantityText}\".");
				continue;
			}

			if (qty <= 0)
			{
				Core.Log.LogWarning($"[DailyKit] Invalid quantity at position {i + 1}: {qty}. Quantity must be greater than 0.");
				continue;
			}

			_dailykitItems.Add(new DailyKitEntry { ItemPrefab = prefab, Quantity = qty });
		}

		Core.Log.LogInfo($"[DailyKit] Loaded {_dailykitItems.Count} item(s) from config.");
	}

    private static void SaveToConfig()
    {
        List<string> pairs = new List<string>();
        foreach (var item in _dailykitItems)
        {
            pairs.Add($"{item.ItemPrefab}:{item.Quantity}");
        }
        
        Plugin.dailyKitItems.Value = string.Join(",", pairs);
        Plugin.dailyKitItems.ConfigFile.Save();
    }

    private static class CsvLogger
    {
        internal static void LogRow(ulong steamId, string playerName, string status)
        {
            try
            {
                Directory.CreateDirectory(CONFIG_DIR);
                bool newFile = !File.Exists(LOG_FILE);

                lock (LOG_LOCK)
                {
                    using var fs = new FileStream(LOG_FILE, FileMode.Append, FileAccess.Write, FileShare.Read);
                    using var sw = new StreamWriter(fs, new UTF8Encoding(false));

                    if (newFile)
                        sw.WriteLine("time,steam_id,player_name,status");

                    string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    sw.WriteLine($"{time},{steamId},{Helper.Csv(playerName)},{Helper.Csv(status)}");
                }
            }
            catch (Exception ex)
            {
                Core.Log.LogError($"[DailyKit] Failed to write dailykit_log.csv: {ex}");
            }
        }
    }
}
