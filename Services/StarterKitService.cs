using System;
using System.Collections.Generic;
using System.Text;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using VampireCommandFramework;
using System.IO;
using System.Globalization;

namespace PlayerServices.Services;

internal static class StarterKitService
{
    private static readonly string CONFIG_DIR = Path.Combine(BepInEx.Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
    private static readonly string LOG_FILE = Path.Combine(CONFIG_DIR, "starterkit_log.csv");
    private static readonly object LOG_LOCK = new();
    private static List<StarterKitEntry> _starterItems = new();

    public class StarterKitEntry
    {
        public int ItemPrefab { get; set; }
        public int Quantity { get; set; }
    }

    public static void Initialize()
    {
        LoadConfig();
    }

    // ---------------------------------------------------------------------
    // Auto give
    // ---------------------------------------------------------------------

    public static void GiveKit(Entity userEntity, User user)
    {
        if (!Plugin.starterKitEnabled.Value) return;
        if (user.PlatformId == 0) return;

        var cache = PlayerDataService.GetOrCreatePlayerCache(user.PlatformId);
        if (cache.HasReceivedStarterKit) return;

        var em = Core.EntityManager;
        var charEntity = user.LocalCharacter.GetEntityOnServer();

        if (charEntity.Equals(Entity.Null) || !em.Exists(charEntity)) return;
        if (_starterItems.Count == 0) return;

        int successCount = 0;
        
        foreach (var entry in _starterItems)
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

        string playerName = user.CharacterName.ToString();

        if (successCount > 0)
        {
            cache.HasReceivedStarterKit = true;
            PlayerDataService.SaveData();

            Helper.NotifyUser(userEntity, "You have received your <color=green>Starter Kit.</color>");
            Core.Log.LogInfo($"[StarterKit] Starter Kit auto-granted to {playerName} ({user.PlatformId})");
            CsvLogger.LogRow(user.PlatformId, playerName, "success_auto");
        }
        else
        {
            Helper.NotifyUser(userEntity, "<color=red>Failed to auto-grant the Starter Kit.</color> Please try <color=green>.pls sk</color> to claim your Starter Kit.");
            Core.Log.LogError($"[StarterKit] Failed to auto-grant Starter Kit to {playerName} ({user.PlatformId})");
            CsvLogger.LogRow(user.PlatformId, playerName, "fail_auto");
        }
    }

    // ---------------------------------------------------------------------
    // Manual give
    // ---------------------------------------------------------------------
    
    public static void ClaimKit(ChatCommandContext ctx)
    {
        if (!Plugin.starterKitEnabled.Value)
        {
            ctx.Reply("<color=red>Starter kit feature is currently disabled.</color>");
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

        if (cache.HasReceivedStarterKit)
        {
            ctx.Reply("<color=yellow>You have already claimed your starter kit.</color>");
            return;
        }

        if (_starterItems.Count == 0)
        {
            ctx.Reply("<color=yellow>The starter kit is currently empty. Please contact an admin.</color>");
            return;
        }

        int successCount = 0;
        foreach (var entry in _starterItems)
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

        if (successCount > 0)
        {
            cache.HasReceivedStarterKit = true;
            PlayerDataService.SaveData();

            ctx.Reply("<color=green>Successfully claimed your starter kit.</color>");
            Core.Log.LogInfo($"[StarterKit] {playerName} manually claimed the Starter Kit.");
            CsvLogger.LogRow(steamId, playerName, "success_manual");
        }
        else
        {
            ctx.Reply("<color=red>Failed to give the starter kit.</color>");
            CsvLogger.LogRow(steamId, playerName, "fail_manual");
        }
    }

    // ---------------------------------------------------------------------
    // Add, Remove, List
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

		var existingItem = _starterItems.Find(i => i.ItemPrefab == prefabGuid);
		if (existingItem != null)
		{
			existingItem.Quantity = quantity;
			ctx.Reply($"Updated starter kit item <color=#87CEFA>{label}</color> to <color=#FFD700>{quantity}</color>.");
			Core.Log.LogInfo($"[StarterKit] Updated starter kit item {label} ({prefabGuid}) to {quantity}");
		}
		else
		{
			_starterItems.Add(new StarterKitEntry { ItemPrefab = prefabGuid, Quantity = quantity });
			ctx.Reply($"Added <color=#87CEFA>{label}</color> ×<color=#FFD700>{quantity}</color> to the starter kit.");
			Core.Log.LogInfo($"[StarterKit] Added {label} ({prefabGuid}) x{quantity} to the starter kit.");
		}

		SaveToConfig();
	}

    public static void RemoveKit(ChatCommandContext ctx, int prefabGuid)
    {
        int removedCount = _starterItems.RemoveAll(i => i.ItemPrefab == prefabGuid);
        if (removedCount > 0)
        {
            ctx.Reply($"Removed item <color=#87CEFA>{prefabGuid}</color> from the starter kit.");
            Core.Log.LogInfo($"[StarterKit] Removed item {prefabGuid} from the starter kit.");
            SaveToConfig();
        }
        else
        {
            ctx.Reply($"<color=yellow>Item not found in the starter kit: {prefabGuid}</color>");
        }
    }

	public static void ListKit(ChatCommandContext ctx)
	{
		var sb = new StringBuilder();

		bool isEnabled = Plugin.starterKitEnabled.Value;
		string status = isEnabled ? "Enabled" : "Disabled";

		sb.AppendLine($"Starter Kit Status: {status}");

		if (_starterItems.Count == 0)
		{
			sb.AppendLine("(Empty)");
			ctx.Reply(sb.ToString().TrimEnd());
			return;
		}

		int shown = 0;

		foreach (var item in _starterItems)
		{
			var guid = new PrefabGUID(item.ItemPrefab);
			string label = ResolveItemLabel(guid, guid.GuidHash.ToString());
			string line = $"{label} ×{item.Quantity}";

			if (sb.Length + line.Length + Environment.NewLine.Length > Core.MAX_REPLY_LENGTH)
				break;

			sb.AppendLine(line);
			shown++;
		}

		int hidden = _starterItems.Count - shown;

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
			Core.Log.LogError($"[StarterKit] Failed to resolve item label: {ex}");
		}

		return string.IsNullOrWhiteSpace(fallbackInput) ? guid.GuidHash.ToString() : fallbackInput;
	}

    // ---------------------------------------------------------------------
    // Disk IO & Logging
    // ---------------------------------------------------------------------

	public static int ReloadConfig()
	{
		LoadConfig();
		return _starterItems.Count;
	}
    
    private static void LoadConfig()
	{
		_starterItems.Clear();
		string rawItems = Plugin.starterKitItems.Value;

		if (string.IsNullOrWhiteSpace(rawItems))
			return;

		string[] pairs = rawItems.Split(',');

		for (int i = 0; i < pairs.Length; i++)
		{
			string pair = pairs[i].Trim();

			if (string.IsNullOrWhiteSpace(pair))
			{
				Core.Log.LogWarning($"[StarterKit] Invalid config entry at position {i + 1}: empty entry.");
				continue;
			}

			string[] split = pair.Split(':');

			if (split.Length != 2)
			{
				Core.Log.LogWarning($"[StarterKit] Invalid config entry at position {i + 1}: \"{pair}\". Expected format: ItemPrefab:Quantity");
				continue;
			}

			string prefabText = split[0].Trim();
			string quantityText = split[1].Trim();

			if (!int.TryParse(prefabText, out int prefab))
			{
				Core.Log.LogWarning($"[StarterKit] Invalid item prefab at position {i + 1}: \"{prefabText}\".");
				continue;
			}

			if (!int.TryParse(quantityText, out int qty))
			{
				Core.Log.LogWarning($"[StarterKit] Invalid quantity at position {i + 1}: \"{quantityText}\".");
				continue;
			}

			if (qty <= 0)
			{
				Core.Log.LogWarning($"[StarterKit] Invalid quantity at position {i + 1}: {qty}. Quantity must be greater than 0.");
				continue;
			}

			_starterItems.Add(new StarterKitEntry { ItemPrefab = prefab, Quantity = qty });
		}

		Core.Log.LogInfo($"[StarterKit] Loaded {_starterItems.Count} item(s) from config.");
	}

    private static void SaveToConfig()
    {
        List<string> pairs = new List<string>();
        foreach (var item in _starterItems)
        {
            pairs.Add($"{item.ItemPrefab}:{item.Quantity}");
        }
        
        Plugin.starterKitItems.Value = string.Join(",", pairs);
        Plugin.starterKitItems.ConfigFile.Save();
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
                Core.Log.LogError($"[StarterKit] Failed to write starterkit_log.csv: {ex}");
            }
        }
    }
}