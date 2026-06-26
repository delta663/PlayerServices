using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using PlayerServices.Data;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using VampireCommandFramework;

namespace PlayerServices.Services;

internal static class AuraService
{
    private static readonly string LOG_DIR = Path.Combine(BepInEx.Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
    private static readonly string LOG_FILE = Path.Combine(LOG_DIR, "aura_log.csv");
    private static readonly object LOG_LOCK = new();

    private const int AURA_COOLDOWN_SECONDS = 5;
    private const int AURA_PREVIEW_SECONDS = 10;
    private const int AURAS_PER_LIST_REPLY = 6;
    private const float AFTER_CONNECT_REPLY = 2f;

	private static readonly Dictionary<(ulong SteamId, int AuraGuid), DateTime> _auraPreviewUntil = new();
    private static readonly Dictionary<ulong, DateTime> _auraToggleCooldowns = new();
    private static readonly List<string> _auraCurrencyNames = new();
    private static readonly List<int> _auraCurrencyPrefabGuids = new();
    private static readonly List<int> _auraPrefabGuids = new();
    private static readonly List<int> _auraCosts = new();

    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
            return;

        LoadConfig();
        _initialized = true;

        if (!Plugin.auraFeatureEnabled.Value)
        {
            DeactivateAllPlayerAuras();
            Core.Log.LogInfo("[Aura] Aura system is disabled. Active aura states will be disabled and aura buffs will be removed on player connect.");
            return;
        }

        Core.Log.LogInfo($"[Aura] Initialized and loaded {_auraPrefabGuids.Count} aura(s).");
    }

    // ---------------------------------------------------------------------
    // OnConnect
    // ---------------------------------------------------------------------

    public static void RefreshOnConnect(Entity userEntity, User user)
    {
        EnsureInitialized();

        if (!user.IsConnected || user.PlatformId == 0)
            return;

        if (!Plugin.auraFeatureEnabled.Value)
        {
            Core.StartCoroutine(RemoveAuraBuffsOnConnectRoutine(userEntity, user.PlatformId));
            return;
        }

        Core.StartCoroutine(RefreshOnConnectRoutine(userEntity, user.PlatformId));
    }

    private static IEnumerator RefreshOnConnectRoutine(Entity userEntity, ulong steamId)
    {
        yield return new WaitForSeconds(AFTER_CONNECT_REPLY);

        try
        {
            var em = Core.EntityManager;

            if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                yield break;

            var user = em.GetComponentData<User>(userEntity);

            if (!user.IsConnected || user.PlatformId != steamId)
                yield break;

            var character = user.LocalCharacter.GetEntityOnServer();

            if (character == Entity.Null || !em.Exists(character))
                yield break;

            var cache = PlayerDataService.GetPlayerCache(user.PlatformId);

            if (cache == null || cache.Auras == null || cache.Auras.Count == 0)
                yield break;

            var configuredAuraGuids = new HashSet<int>(_auraPrefabGuids);

            int orphanInactiveCount = 0;
            int orphanRemovedBuffCount = 0;

            foreach (var aura in cache.Auras)
            {
                if (configuredAuraGuids.Contains(aura.PrefabGuid))
                    continue;

                if (RemoveAuraBuff(character, aura.PrefabGuid))
                    orphanRemovedBuffCount++;

                if (aura.Active)
                {
                    aura.Active = false;
                    orphanInactiveCount++;
                }
            }

            if (orphanInactiveCount > 0)
            {
                PlayerDataService.SaveNow();
            }

            if (orphanInactiveCount > 0 || orphanRemovedBuffCount > 0)
            {
                Core.Log.LogInfo($"[Aura] Cleaned up {orphanInactiveCount} inactive old aura state(s) and removed {orphanRemovedBuffCount} old aura buff(s) from {user.CharacterName} ({user.PlatformId}) on connect.");
            }

            foreach (var aura in cache.Auras)
            {
                if (!aura.Active)
                    continue;

                if (!configuredAuraGuids.Contains(aura.PrefabGuid))
                    continue;

                ApplyAuraBuff(userEntity, character, aura.PrefabGuid);
            }
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    private static IEnumerator RemoveAuraBuffsOnConnectRoutine(Entity userEntity, ulong steamId)
    {
        yield return new WaitForSeconds(AFTER_CONNECT_REPLY);

        try
        {
            var em = Core.EntityManager;

            if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                yield break;

            var user = em.GetComponentData<User>(userEntity);

            if (!user.IsConnected || user.PlatformId != steamId)
                yield break;

            var character = user.LocalCharacter.GetEntityOnServer();

            if (character == Entity.Null || !em.Exists(character))
                yield break;

            var auraGuidsToRemove = new HashSet<int>(_auraPrefabGuids);

            var cache = PlayerDataService.GetPlayerCache(user.PlatformId);

            if (cache?.Auras != null)
            {
                foreach (var aura in cache.Auras)
                {
                    auraGuidsToRemove.Add(aura.PrefabGuid);
                }
            }

            int removedCount = 0;

            foreach (int auraGuid in auraGuidsToRemove)
            {
                if (RemoveAuraBuff(character, auraGuid))
                    removedCount++;
            }

            if (removedCount > 0)
            {
                Core.Log.LogInfo($"[Aura] Aura system is disabled. Removed {removedCount} aura buff(s) from {user.CharacterName} ({user.PlatformId}) on connect.");
            }
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    // ---------------------------------------------------------------------
    // Buy Aura
    // ---------------------------------------------------------------------

    public static void BuyAura(ChatCommandContext ctx, string idToken)
    {
        EnsureInitialized();

        var userEntity = ctx.Event.SenderUserEntity;
        var character = ctx.Event.SenderCharacterEntity;
        var user = userEntity.Read<User>();

        ulong steamId = user.PlatformId;
        string playerName = user.CharacterName.ToString();

        if (!Plugin.auraFeatureEnabled.Value)
        {
            ctx.Reply("<color=red>Aura system is currently disabled.</color>");
            return;
        }

        if (steamId == 0)
        {
            ctx.Reply("<color=red>Cannot read your SteamID.</color>");
            AppendAuraLog("buy", 0, playerName, 0, playerName, 0, 0, 0, 0, "", false, "invalid_steam_id");
            return;
        }

        if (!TryResolveAuraId(ctx, idToken, out int auraId, out int auraGuid))
        {
            AppendAuraLog("buy", steamId, playerName, steamId, playerName, 0, 0, 0, 0, "", false, "invalid_aura_id");
            return;
        }

        if (IsAuraPreviewActive(steamId, auraGuid, out double previewRemaining))
		{
			ctx.Reply($"<color=yellow>Please wait {previewRemaining:0.0}s for the aura preview to end before buying this aura.</color>");
            AppendAuraLog("buy", steamId, playerName, steamId, playerName, auraId, auraGuid, 0, 0, "", false, "preview_active");
			return;
		}

		if (!TryGetAuraCost(auraId, out int cost, out bool purchaseDisabled))
		{
			if (purchaseDisabled)
			{
				ctx.Reply("<color=yellow>This aura is not available for purchase.</color>");
				AppendAuraLog("buy", steamId, playerName, steamId, playerName, auraId, auraGuid, 0, 0, "", false, "purchase_disabled");
				return;
			}

			ctx.Reply("<color=red>This aura does not have a configured cost. Please contact an admin.</color>");
			AppendAuraLog("buy", steamId, playerName, steamId, playerName, auraId, auraGuid, 0, 0, "", false, "cost_not_configured");
			return;
		}
        
        if (!TryGetAuraCurrency(auraId, out int currencyGuid, out string currencyName))
        {
            ctx.Reply("<color=red>This aura does not have a configured currency. Please contact an admin.</color>");
            AppendAuraLog("buy", steamId, playerName, steamId, playerName, auraId, auraGuid, cost, 0, "", false, "currency_not_configured");
            return;
        }

        var cache = PlayerDataService.GetOrCreatePlayerCache(steamId);

        if (HasAura(cache, auraGuid))
        {
            ctx.Reply("<color=yellow>You already own this aura.</color>");
            AppendAuraLog("buy", steamId, playerName, steamId, playerName, auraId, auraGuid, 0, 0, "", false, "already_owned");
            return;
        }

        if (!TrySpendCurrency(character, currencyGuid, currencyName, cost, out string spendLogReason, out string spendReplyMessage))
        {
            ctx.Reply(spendReplyMessage);
            AppendAuraLog("buy", steamId, playerName, steamId, playerName, auraId, auraGuid, cost, currencyGuid, currencyName, false, spendLogReason);
            return;
        }

        AddOrSetAura(cache, auraGuid, true);
        ApplyAuraBuff(userEntity, character, auraGuid);

        PlayerDataService.SaveNow();

        string broadcastMsgStr = Plugin.auraBroadcastMessage.Value
            .Replace("#player#", playerName)
            .Replace("#aura#", auraId.ToString());

        if (!string.IsNullOrWhiteSpace(broadcastMsgStr))
        {
	        var msg = new FixedString512Bytes(broadcastMsgStr);
	        ServerChatUtils.SendSystemMessageToAllClients(Core.EntityManager, ref msg);
        }

        ctx.Reply($"Purchased <color=#87CEFA>aura {auraId}</color> for <color=white>{cost}</color> {currencyName}.");
        Core.Log.LogInfo($"[Aura] {playerName} ({steamId}) bought aura {auraId} ({auraGuid}) for {cost} {currencyName} ({currencyGuid}).");
        AppendAuraLog("buy", steamId, playerName, steamId, playerName, auraId, auraGuid, cost, currencyGuid, currencyName, true, "successful");
    }

    private static bool TrySpendCurrency(Entity character, int currencyGuid, string currencyName, int amount, out string spendLogReason, out string spendReplyMessage)
    {
        spendLogReason = string.Empty;
        spendReplyMessage = string.Empty;

        try
        {
            if (character == Entity.Null || !Core.EntityManager.Exists(character))
            {
                spendLogReason = "character_not_ready";
                spendReplyMessage = "<color=red>Character not ready.</color>";
                return false;
            }

            if (currencyGuid == 0)
            {
                spendLogReason = "invalid_currency_prefab";
                spendReplyMessage = "<color=red>Invalid aura currency.</color>";
                return false;
            }

            if (amount <= 0)
            {
                spendLogReason = "invalid_cost";
                spendReplyMessage = "<color=red>Invalid aura cost.</color>";
                return false;
            }

            var currencyPrefab = new PrefabGUID(currencyGuid);

            int have = Helper.GetItemCountInInventory(character, currencyPrefab);

            if (have < amount)
            {
                spendLogReason = $"not_enough_currency_{have}/{amount}";
                spendReplyMessage = $"<color=red>Not enough {currencyName} ({have}/{amount}).</color>";
                return false;
            }

            if (!Helper.TryRemoveItemsFromInventory(character, currencyPrefab, amount))
            {
                spendLogReason = "remove_items_failed";
                spendReplyMessage = "<color=red>Failed to remove the required currency.</color>";
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            spendLogReason = "exception: " + e.Message;
            spendReplyMessage = "<color=red>Error: An unexpected error occurred while spending currency.</color>";
            Core.LogException(e);
            return false;
        }
    }

    private static bool TryResolveAuraId(ChatCommandContext ctx, string idToken, out int auraId, out int auraGuid)
    {
        auraId = 0;
        auraGuid = 0;

        if (!int.TryParse(idToken, out auraId) || auraId <= 0)
        {
            ReplyHelp(ctx);
            return false;
        }

        int index = auraId - 1;

        if (index < 0 || index >= _auraPrefabGuids.Count)
        {
            ctx.Reply("<color=yellow>Invalid aura ID.</color>");
            return false;
        }

        auraGuid = _auraPrefabGuids[index];
        return true;
    }

	private static bool TryGetAuraCost(int auraId, out int cost, out bool purchaseDisabled)
	{
		cost = 0;
		purchaseDisabled = false;

		int index = auraId - 1;

		if (index < 0 || index >= _auraCosts.Count)
			return false;

		cost = _auraCosts[index];

		if (cost <= 0)
		{
			purchaseDisabled = true;
			return false;
		}

		return true;
	}

    private static bool TryGetAuraCurrency(int auraId, out int currencyGuid, out string currencyName)
    {
        currencyGuid = 0;
        currencyName = string.Empty;

        int index = auraId - 1;

        if (index < 0)
            return false;

        if (index >= _auraCurrencyPrefabGuids.Count)
            return false;

        if (index >= _auraCurrencyNames.Count)
            return false;

        currencyGuid = _auraCurrencyPrefabGuids[index];
        currencyName = _auraCurrencyNames[index];

        return currencyGuid != 0 && !string.IsNullOrWhiteSpace(currencyName);
    }

    // ---------------------------------------------------------------------
    // Preview Aura
    // ---------------------------------------------------------------------

    public static void PreviewAura(ChatCommandContext ctx, string idToken)
	{
		EnsureInitialized();

		var userEntity = ctx.Event.SenderUserEntity;
		var character = ctx.Event.SenderCharacterEntity;
		var user = userEntity.Read<User>();

		if (!Plugin.auraFeatureEnabled.Value)
		{
			ctx.Reply("<color=red>Aura system is currently disabled.</color>");
			return;
		}

		if (user.PlatformId == 0)
		{
			ctx.Reply("<color=red>Cannot read your SteamID.</color>");
			return;
		}

		if (!TryResolveAuraId(ctx, idToken, out int auraId, out int auraGuid))
		{
			return;
		}

		var cache = PlayerDataService.GetPlayerCache(user.PlatformId);

		if (cache != null && HasAura(cache, auraGuid))
		{
			ctx.Reply(
				$"<color=yellow>You already own aura {auraId}</color>.\n" +
				$"Usage: <color=green>.aura on {auraId}</color> to activate or <color=green>.aura off {auraId}</color> to deactivate.");
			return;
		}

		if (!TryApplyPreviewAuraBuff(userEntity, character, auraGuid, out string reason))
		{
			ctx.Reply(reason);
			return;
		}

        _auraPreviewUntil[(user.PlatformId, auraGuid)] = DateTime.UtcNow.AddSeconds(AURA_PREVIEW_SECONDS);

		string buyText;

		if (TryGetAuraCost(auraId, out int cost, out bool purchaseDisabled) && TryGetAuraCurrency(auraId, out _, out string currencyName))
		{
			buyText = $"Buy this aura with <color=green>.buy aura {auraId}</color> for <color=#87CEFA>{cost}x {currencyName}</color>.";
		}
		else if (purchaseDisabled)
		{
			buyText = "This aura is not available for purchase.";
		}
		else
		{
			buyText = "This aura does not have a configured price.";
		}

		ctx.Reply(
			$"<color=yellow>Previewing aura {auraId} for {AURA_PREVIEW_SECONDS} seconds.</color>\n" +
			buyText);
	}

    private static bool TryApplyPreviewAuraBuff(Entity userEntity, Entity character, int auraGuid, out string reason)
	{
		reason = string.Empty;

		try
		{
			var em = Core.EntityManager;
			var buff = new PrefabGUID(auraGuid);

			if (userEntity == Entity.Null || !em.Exists(userEntity))
			{
				reason = "<color=red>User not ready.</color>";
				return false;
			}

			if (character == Entity.Null || !em.Exists(character))
			{
				reason = "<color=red>Character not ready.</color>";
				return false;
			}

			if (BuffUtility.HasBuff(em, character, buff))
			{
				reason = "<color=yellow>This aura is already active.</color>";
				return false;
			}

			Buffs.AddBuff(userEntity, character, buff, AURA_PREVIEW_SECONDS, false);
			return true;
		}
		catch (Exception e)
		{
			Core.LogException(e);
			reason = "<color=red>Failed to apply aura preview.</color>";
			return false;
		}
	}

    private static bool IsAuraPreviewActive(ulong steamId, int auraGuid, out double remainingSeconds)
	{
		remainingSeconds = 0;

		if (steamId == 0)
			return false;

		var key = (steamId, auraGuid);

		if (!_auraPreviewUntil.TryGetValue(key, out DateTime until))
			return false;

		var now = DateTime.UtcNow;

		if (now >= until)
		{
			_auraPreviewUntil.Remove(key);
			return false;
		}

		remainingSeconds = Math.Max(0, (until - now).TotalSeconds);
		return true;
	}

    // ---------------------------------------------------------------------
    // Add, Remove, List, help 
    // ---------------------------------------------------------------------

    public static void AdminAddAura(ChatCommandContext ctx, string playerName, string idToken)
    {
        EnsureInitialized();

        var admin = ctx.Event.User;
        ulong adminSteamId = admin.PlatformId;
        string adminName = admin.CharacterName.ToString();

        if (!Plugin.auraFeatureEnabled.Value)
        {
            ctx.Reply("<color=red>Aura system is currently disabled.</color>");
            return;
        }

        if (!TryResolveAuraId(ctx, idToken, out int auraId, out int auraGuid))
        {
            return;
        }

        if (!TryFindPlayerByName(playerName, out var targetUserEntity, out var targetUser, out var candidates))
        {
            HandleFindPlayer(ctx, playerName, candidates);
            return;
        }

        var targetChar = targetUser.LocalCharacter.GetEntityOnServer();

        if (targetChar == Entity.Null || !Core.EntityManager.Exists(targetChar))
        {
            ctx.Reply("<color=red>Target character not found.</color>");
            return;
        }

        var cache = PlayerDataService.GetOrCreatePlayerCache(targetUser.PlatformId);

        bool alreadyHad = HasAura(cache, auraGuid);

        AddOrSetAura(cache, auraGuid, true);
        ApplyAuraBuff(targetUserEntity, targetChar, auraGuid);
        TryGetAuraCurrency(auraId, out int currencyGuid, out string currencyName);

        PlayerDataService.SaveNow();

        if (alreadyHad)
        {
            ctx.Reply($"<color=white>{targetUser.CharacterName}</color> already has aura {auraId}. It has been activated.");
            AppendAuraLog("admin_add", adminSteamId, adminName, targetUser.PlatformId, targetUser.CharacterName.ToString(), auraId, auraGuid, 0, 0, "", true, "already_owned_activated");
        }
        else
        {
            ctx.Reply($"Granted <color=#87CEFA>aura {auraId}</color> to <color=white>{targetUser.CharacterName}</color>.");
            Helper.NotifyUser(targetUserEntity, $"You have been granted <color=#87CEFA>aura {auraId}</color>.");
            AppendAuraLog("admin_add", adminSteamId, adminName, targetUser.PlatformId, targetUser.CharacterName.ToString(), auraId, auraGuid, 0, 0, "", true, "successful");
        }

        Core.Log.LogInfo($"[Aura] Admin {adminName} ({adminSteamId}) granted aura {auraId} ({auraGuid}) to {targetUser.CharacterName} ({targetUser.PlatformId}).");
    }

    public static void AdminRemoveAura(ChatCommandContext ctx, string playerName, string idToken)
    {
        EnsureInitialized();

        var admin = ctx.Event.User;
        ulong adminSteamId = admin.PlatformId;
        string adminName = admin.CharacterName.ToString();

        if (!TryResolveAuraId(ctx, idToken, out int auraId, out int auraGuid))
        {
            return;
        }

        if (!TryFindPlayerByName(playerName, out var targetUserEntity, out var targetUser, out var candidates))
        {
            HandleFindPlayer(ctx, playerName, candidates);
            return;
        }

        var targetChar = targetUser.LocalCharacter.GetEntityOnServer();

        if (targetChar == Entity.Null || !Core.EntityManager.Exists(targetChar))
        {
            ctx.Reply("<color=red>Target character not found.</color>");
            return;
        }

        var cache = PlayerDataService.GetOrCreatePlayerCache(targetUser.PlatformId);
        cache.Auras ??= new List<PlayerAuraData>();

        var auraData = cache.Auras.FirstOrDefault(a => a.PrefabGuid == auraGuid);

        if (auraData == null)
        {
            ctx.Reply($"<color=white>{targetUser.CharacterName}</color> does not own aura {auraId}.");
            return;
        }

        cache.Auras.Remove(auraData);
        RemoveAuraBuff(targetChar, auraGuid);
        TryGetAuraCurrency(auraId, out int currencyGuid, out string currencyName);

        PlayerDataService.SaveNow();

        ctx.Reply($"Removed <color=#87CEFA>aura {auraId}</color> from <color=white>{targetUser.CharacterName}</color>.");
        Helper.NotifyUser(targetUserEntity, $"Your <color=#87CEFA>aura {auraId}</color> has been removed by an admin.");
        AppendAuraLog("admin_remove", adminSteamId, adminName, targetUser.PlatformId, targetUser.CharacterName.ToString(), auraId, auraGuid, 0, 0, "", true, "successful");
        Core.Log.LogInfo($"[Aura] Admin {adminName} ({adminSteamId}) removed aura {auraId} ({auraGuid}) from {targetUser.CharacterName} ({targetUser.PlatformId}).");
    }

    private static void AddOrSetAura(PlayerCacheData cache, int auraGuid, bool active)
    {
        cache.Auras ??= new List<PlayerAuraData>();

        var auraData = cache.Auras.FirstOrDefault(a => a.PrefabGuid == auraGuid);

        if (auraData == null)
        {
            cache.Auras.Add(new PlayerAuraData
            {
                PrefabGuid = auraGuid,
                Active = active
            });

            return;
        }

        auraData.Active = active;
    }

	public static void ListAuras(ChatCommandContext ctx)
	{
		EnsureInitialized();

		if (_auraPrefabGuids.Count == 0)
		{
			ctx.Reply("<color=yellow>No auras are configured.</color>");
			return;
		}

		var user = ctx.Event.SenderUserEntity.Read<User>();

		if (user.PlatformId == 0)
		{
			ctx.Reply("<color=red>Cannot read your SteamID.</color>");
			return;
		}

		var cache = PlayerDataService.GetOrCreatePlayerCache(user.PlatformId);
		cache.Auras ??= new List<PlayerAuraData>();

		int totalAuras = _auraPrefabGuids.Count;
		int totalPages = (int)Math.Ceiling(totalAuras / (double)AURAS_PER_LIST_REPLY);

		for (int page = 0; page < totalPages; page++)
		{
			int startIndex = page * AURAS_PER_LIST_REPLY;
			int endIndex = Math.Min(startIndex + AURAS_PER_LIST_REPLY, totalAuras);

			var sb = new StringBuilder();

			sb.AppendLine($"<color=yellow>Aura list</color> ({page + 1}/{totalPages})");

			for (int i = startIndex; i < endIndex; i++)
			{
				int auraId = i + 1;
				int auraGuid = _auraPrefabGuids[i];

				string costText;

				if (TryGetAuraCost(auraId, out int cost, out bool purchaseDisabled) &&
					TryGetAuraCurrency(auraId, out _, out string currencyName))
				{
					costText = $"{cost}x {currencyName}";
				}
				else if (purchaseDisabled)
				{
					costText = "Not for sale";
				}
				else
				{
					costText = "Not configured";
				}

				var owned = cache.Auras.FirstOrDefault(a => a.PrefabGuid == auraGuid);

				string status = owned == null
					? "<color=yellow>Not owned</color>"
					: owned.Active ? "<color=green>ON</color>" : "<color=red>OFF</color>";

				sb.AppendLine($"[{auraId}] {costText} - ({status})");
			}

			ctx.Reply(sb.ToString().TrimEnd());
		}
	}

    public static void ReplyHelp(ChatCommandContext ctx)
    {
        
        var sb = new StringBuilder();

        bool isEnabled = Plugin.auraFeatureEnabled.Value;
        string status = isEnabled ? "<color=green>Enabled</color>" : "<color=red>Disabled</color>";

        if (isEnabled)
        {
            sb.AppendLine("<color=yellow>Aura Commands:</color>");
            sb.AppendLine("<color=green>.aura on <id></color> or <color=green>.aura on all</color>");
            sb.AppendLine("<color=green>.aura off <id></color> or <color=green>.aura off all</color>");
            sb.AppendLine("<color=green>.buy aura <id></color> or <color=green>.aura buy <id></color>");
            sb.AppendLine("<color=green>.aura preview <id></color>");
            sb.AppendLine("<color=green>.aura list</color>");
        
            if (ctx.Event.User.IsAdmin)
            {
                sb.AppendLine("<color=green>.aura add <player> <id></color>");
                sb.AppendLine("<color=green>.aura remove <player> <id></color>");
            }
        }

        sb.AppendLine($"<color=yellow>Aura Status:</color> {status}");

        ctx.Reply(sb.ToString().TrimEnd());
    }

    // ---------------------------------------------------------------------
    // Set Active Inactive
    // ---------------------------------------------------------------------

    public static void SetAuraActive(ChatCommandContext ctx, string idToken, bool active)
    {
        EnsureInitialized();

        if (string.Equals(idToken, "all", StringComparison.OrdinalIgnoreCase))
        {
            SetAllAurasActive(ctx, active);
            return;
        }

        if (!Plugin.auraFeatureEnabled.Value && active)
        {
            ctx.Reply("<color=red>Aura system is currently disabled.</color>");
            return;
        }

        if (!TryResolveAuraId(ctx, idToken, out int auraId, out int auraGuid))
            return;

        var userEntity = ctx.Event.SenderUserEntity;
        var character = ctx.Event.SenderCharacterEntity;
        var user = userEntity.Read<User>();

        if (user.PlatformId == 0)
        {
            ctx.Reply("<color=red>Cannot read your SteamID.</color>");
            return;
        }

        var cache = PlayerDataService.GetOrCreatePlayerCache(user.PlatformId);
        cache.Auras ??= new List<PlayerAuraData>();

        var auraData = cache.Auras.FirstOrDefault(a => a.PrefabGuid == auraGuid);

        if (auraData == null)
        {
            ctx.Reply("<color=yellow>You do not own this aura.</color>");
            return;
        }

        if (auraData.Active == active)
        {
            if (!active)
            {
                RemoveAuraBuff(character, auraGuid);
            }

            ctx.Reply(active
                ? "<color=yellow>This aura is already active.</color>"
                : "<color=yellow>This aura is already inactive.</color>");
            return;
        }

        if (!TryBeginAuraSaveCooldown(user.PlatformId, out double remainingSeconds))
        {
            ctx.Reply($"<color=yellow>Please wait {remainingSeconds:0.0}s before changing aura again.</color>");
            return;
        }

        auraData.Active = active;
        PlayerDataService.SaveNow();

        if (active)
        {
            ApplyAuraBuff(userEntity, character, auraGuid);
            ctx.Reply($"<color=green>Aura {auraId} activated.</color>");
        }
        else
        {
            RemoveAuraBuff(character, auraGuid);
            ctx.Reply($"<color=yellow>Aura {auraId} deactivated.</color>");
        }
    }

    public static void SetAllAurasActive(ChatCommandContext ctx, bool active)
    {
        EnsureInitialized();

        if (!Plugin.auraFeatureEnabled.Value && active)
        {
            ctx.Reply("<color=red>Aura system is currently disabled.</color>");
            return;
        }

        var userEntity = ctx.Event.SenderUserEntity;
        var character = ctx.Event.SenderCharacterEntity;
        var user = userEntity.Read<User>();

        if (user.PlatformId == 0)
        {
            ctx.Reply("<color=red>Cannot read your SteamID.</color>");
            return;
        }

        var cache = PlayerDataService.GetOrCreatePlayerCache(user.PlatformId);
        cache.Auras ??= new List<PlayerAuraData>();

        if (cache.Auras.Count == 0)
        {
            ctx.Reply("<color=yellow>You do not own any auras.</color>");
            return;
        }

        var changedAuras = cache.Auras
            .Where(aura => _auraPrefabGuids.Contains(aura.PrefabGuid))
            .Where(aura => aura.Active != active)
            .ToList();

        if (changedAuras.Count == 0)
        {
            if (!active)
            {
                foreach (var aura in cache.Auras)
                {
                    if (!_auraPrefabGuids.Contains(aura.PrefabGuid))
                        continue;

                    RemoveAuraBuff(character, aura.PrefabGuid);
                }
            }

            ctx.Reply(active
                ? "<color=yellow>All owned auras are already active.</color>"
                : "<color=yellow>All owned auras are already inactive.</color>");
            return;
        }

        if (!TryBeginAuraSaveCooldown(user.PlatformId, out double remainingSeconds))
        {
            ctx.Reply($"<color=yellow>Please wait {remainingSeconds:0.0}s before changing aura again.</color>");
            return;
        }

        foreach (var aura in changedAuras)
        {
            aura.Active = active;

            if (active)
                ApplyAuraBuff(userEntity, character, aura.PrefabGuid);
            else
                RemoveAuraBuff(character, aura.PrefabGuid);
        }

        PlayerDataService.SaveNow();

        ctx.Reply(active
            ? $"<color=green>Activated {changedAuras.Count} aura(s).</color>"
            : $"<color=yellow>Deactivated {changedAuras.Count} aura(s).</color>");
    }

    private static void DeactivateAllPlayerAuras()
    {
        int changedPlayers = 0;
        int changedAuras = 0;

        foreach (var cache in PlayerDataService.GetAllPlayerCaches())
        {
            if (cache.Auras == null || cache.Auras.Count == 0)
                continue;

            bool changed = false;

            foreach (var aura in cache.Auras)
            {
                if (!aura.Active)
                    continue;

                aura.Active = false;
                changed = true;
                changedAuras++;
            }

            if (changed)
                changedPlayers++;
        }

        if (changedAuras <= 0)
            return;

        PlayerDataService.SaveNow();
        Core.Log.LogInfo($"[Aura] Set {changedAuras} aura(s) inactive for {changedPlayers} player(s).");
    }

    // ---------------------------------------------------------------------
    // Apply Remove Buff, HasAura, Cooldown
    // ---------------------------------------------------------------------

    private static void ApplyAuraBuff(Entity userEntity, Entity character, int auraGuid)
    {
        var em = Core.EntityManager;
        var buff = new PrefabGUID(auraGuid);

        if (character == Entity.Null || !em.Exists(character))
            return;

        if (userEntity == Entity.Null || !em.Exists(userEntity))
            return;

        if (BuffUtility.HasBuff(em, character, buff))
            return;

        Buffs.AddBuff(userEntity, character, buff, -1, true);
    }

    private static bool RemoveAuraBuff(Entity character, int auraGuid)
    {
        var em = Core.EntityManager;
        var buff = new PrefabGUID(auraGuid);

        if (character == Entity.Null || !em.Exists(character))
            return false;

        if (!BuffUtility.HasBuff(em, character, buff))
            return false;

        Buffs.RemoveBuff(character, buff);
        return true;
    }
    
    private static bool HasAura(PlayerCacheData cache, int auraGuid)
    {
        return cache.Auras != null && cache.Auras.Any(a => a.PrefabGuid == auraGuid);
    }
    
    private static bool TryBeginAuraSaveCooldown(ulong steamId, out double remainingSeconds)
    {
        remainingSeconds = 0;

        if (steamId == 0)
            return true;

        var now = DateTime.UtcNow;

        if (_auraToggleCooldowns.TryGetValue(steamId, out DateTime nextAllowedTime) && now < nextAllowedTime)
        {
            remainingSeconds = Math.Max(0, (nextAllowedTime - now).TotalSeconds);
            return false;
        }

        _auraToggleCooldowns[steamId] = now.AddSeconds(AURA_COOLDOWN_SECONDS);
        return true;
    }

    // ---------------------------------------------------------------------
    // Find Player
    // ---------------------------------------------------------------------
    
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

    private static bool TryFindPlayerByName(string query, out Entity userEntity, out User user, out List<string> candidates)
    {
        userEntity = Entity.Null;
        user = default;
        candidates = null;

        query = (query ?? string.Empty).Trim();

        if (query.Length < 2)
        {
            candidates = new List<string>();
            return false;
        }

        var em = Core.EntityManager;
        NativeArray<Entity> userEntities = default;

        try
        {
            userEntities = Helper.GetEntitiesByComponentType<User>();

            var matches = new List<(Entity Entity, User User, string Name)>();

            foreach (var entity in userEntities)
            {
                if (entity == Entity.Null || !em.Exists(entity) || !em.HasComponent<User>(entity))
                    continue;

                var currentUser = em.GetComponentData<User>(entity);

                if (currentUser.PlatformId == 0)
                    continue;

                string name = currentUser.CharacterName.ToString();

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                matches.Add((entity, currentUser, name));
            }

            var exactMatches = matches
                .Where(x => string.Equals(x.Name, query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (exactMatches.Count == 1)
            {
                userEntity = exactMatches[0].Entity;
                user = exactMatches[0].User;
                return true;
            }

            if (exactMatches.Count > 1)
            {
                candidates = exactMatches
                    .Select(x => x.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return false;
            }

            var partialMatches = matches
                .Where(x => x.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (partialMatches.Count == 1)
            {
                userEntity = partialMatches[0].Entity;
                user = partialMatches[0].User;
                return true;
            }

            if (partialMatches.Count > 1)
            {
                candidates = partialMatches
                    .Select(x => x.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return false;
        }
        finally
        {
            if (userEntities.IsCreated)
                userEntities.Dispose();
        }
    }

    // ---------------------------------------------------------------------
    // Disk IO & Logging
    // ---------------------------------------------------------------------

    private static void EnsureInitialized()
    {
        if (!_initialized)
            Initialize();
    }

    public static int ReloadConfig()
	{
		LoadConfig();
		return _auraPrefabGuids.Count;
	}

	public static void LoadConfig()
	{
		var auraPrefabGuids = new List<int>();
		var auraCosts = new List<int>();
		var auraCurrencyNames = new List<string>();
		var auraCurrencyPrefabGuids = new List<int>();

		bool valid = true;

		valid &= ParseIntListStrict("AuraPrefabGuids", Plugin.auraPrefabGuids.Value, auraPrefabGuids);
		valid &= ParseIntListStrict("AuraCosts", Plugin.auraCosts.Value, auraCosts);
		valid &= ParseStringListStrict("AuraCurrencyName", Plugin.auraCurrencyName.Value, auraCurrencyNames);
		valid &= ParseIntListStrict("AuraCurrencyPrefabGuid", Plugin.auraCurrencyPrefabGuid.Value, auraCurrencyPrefabGuids);

		if (!valid)
		{
			Core.Log.LogError("[Aura] Aura config contains invalid value(s). Keeping previous aura config. Fix the config and restart or reload the plugin.");
			return;
		}

		_auraPrefabGuids.Clear();
		_auraCosts.Clear();
		_auraCurrencyNames.Clear();
		_auraCurrencyPrefabGuids.Clear();

		_auraPrefabGuids.AddRange(auraPrefabGuids);
		_auraCosts.AddRange(auraCosts);
		_auraCurrencyNames.AddRange(auraCurrencyNames);
		_auraCurrencyPrefabGuids.AddRange(auraCurrencyPrefabGuids);

		ValidateAuraConfigCounts();
	}

    private static bool ParseIntListStrict(string configName, string input, List<int> output)
	{
		output.Clear();

		if (string.IsNullOrWhiteSpace(input))
			return true;

		var parts = input.Split(',');

		for (int i = 0; i < parts.Length; i++)
		{
			string part = parts[i].Trim();

			if (string.IsNullOrWhiteSpace(part))
			{
				Core.Log.LogError($"[Aura] {configName} has an empty value at position {i + 1}.");
				return false;
			}

			if (!int.TryParse(part, out int value))
			{
				Core.Log.LogError($"[Aura] {configName} has an invalid integer at position {i + 1}: \"{part}\".");
				return false;
			}

			output.Add(value);
		}

		return true;
	}

	private static bool ParseStringListStrict(string configName, string input, List<string> output)
	{
		output.Clear();

		if (string.IsNullOrWhiteSpace(input))
			return true;

		var parts = input.Split(',');

		for (int i = 0; i < parts.Length; i++)
		{
			string part = parts[i].Trim();

			if (string.IsNullOrWhiteSpace(part))
			{
				Core.Log.LogError($"[Aura] {configName} has an empty value at position {i + 1}.");
				return false;
			}

			output.Add(part);
		}

		return true;
	}

	private static void ValidateAuraConfigCounts()
	{
		if (_auraCosts.Count < _auraPrefabGuids.Count)
		{
			Core.Log.LogWarning($"[Aura] AuraCosts has fewer entries than AuraPrefabGuids. Some auras cannot be purchased. Auras: {_auraPrefabGuids.Count}, Costs: {_auraCosts.Count}");
		}
		else if (_auraCosts.Count > _auraPrefabGuids.Count)
		{
			Core.Log.LogWarning($"[Aura] AuraCosts has more entries than AuraPrefabGuids. Extra costs will be ignored. Auras: {_auraPrefabGuids.Count}, Costs: {_auraCosts.Count}");
		}

		if (_auraCurrencyNames.Count < _auraPrefabGuids.Count)
		{
			Core.Log.LogWarning($"[Aura] AuraCurrencyName has fewer entries than AuraPrefabGuids. Some auras cannot be purchased. Auras: {_auraPrefabGuids.Count}, CurrencyNames: {_auraCurrencyNames.Count}");
		}
		else if (_auraCurrencyNames.Count > _auraPrefabGuids.Count)
		{
			Core.Log.LogWarning($"[Aura] AuraCurrencyName has more entries than AuraPrefabGuids. Extra names will be ignored. Auras: {_auraPrefabGuids.Count}, CurrencyNames: {_auraCurrencyNames.Count}");
		}

		if (_auraCurrencyPrefabGuids.Count < _auraPrefabGuids.Count)
		{
			Core.Log.LogWarning($"[Aura] AuraCurrencyPrefabGuid has fewer entries than AuraPrefabGuids. Some auras cannot be purchased. Auras: {_auraPrefabGuids.Count}, CurrencyPrefabGuids: {_auraCurrencyPrefabGuids.Count}");
		}
		else if (_auraCurrencyPrefabGuids.Count > _auraPrefabGuids.Count)
		{
			Core.Log.LogWarning($"[Aura] AuraCurrencyPrefabGuid has more entries than AuraPrefabGuids. Extra currency prefab GUIDs will be ignored. Auras: {_auraPrefabGuids.Count}, CurrencyPrefabGuids: {_auraCurrencyPrefabGuids.Count}");
		}
	}

    private static void AppendAuraLog(
        string action,
        ulong actorSteamId,
        string actorName,
        ulong targetSteamId,
        string targetName,
        int auraId,
        int auraGuid,
        int cost,
        int currencyGuid,
        string currencyName,
        bool success,
        string reason = "")
    {
        try
        {
            lock (LOG_LOCK)
            {
                Directory.CreateDirectory(LOG_DIR);
                bool newFile = !File.Exists(LOG_FILE);
                using var fs = new FileStream(LOG_FILE, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var sw = new StreamWriter(fs, new UTF8Encoding(false));

                if (newFile)
                    sw.WriteLine("times,action,actor_steam_id,actor_name,target_steam_id,target_name,aura_id,aura_guid,cost,currency_prefab,currency_name,success,reason");

                sw.WriteLine(
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}," +
                    $"{Helper.Csv(action)}," +
                    $"{actorSteamId}," +
                    $"{Helper.Csv(actorName)}," +
                    $"{targetSteamId}," +
                    $"{Helper.Csv(targetName)}," +
                    $"{auraId}," +
                    $"{auraGuid}," +
                    $"{cost}," +
                    $"{currencyGuid}," +
                    $"{Helper.Csv(currencyName)}," +
                    $"{(success ? "true" : "false")}," +
                    $"{Helper.Csv(reason)}"
                );
            }
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }
}